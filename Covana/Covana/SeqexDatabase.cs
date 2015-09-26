using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Symbols;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.ComponentModel;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.ExtendedReflection.Utilities.Safe.Text;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Coverage;
using System.Diagnostics;
using Microsoft.Pex.Engine;

namespace Covana
{
    /*
     * This Seqex tool is designed to collect runtime information to help method sequence generation
     * 
     * To run the portion of the tool for collecting runtime information, add the following two lines in the test
     * project's AssemblyInfo.cs 
     * 
     *  using Seqex;
     * [assembly: Seqex]
     * 
     * To additionally run the portion of using the info for sequence generation, add the following two lines in the test
     * project's AssemblyInfo.cs 
     * 
     * using SeqExplorable;
     * [assembly: SeqexExplorableGuesser]
     * 
     * If you just want to collect runtime info for other uses, you don't need to run the portion for sequence generation
     * 
     * The communication between these two portions is that 
     * 
     * (1) you first apply Pex for the first pass while collecting the runtime info, which is written to some searlized files 
     * (and textual files for debugging)
     * 
     * (2) you then apply Pex for the second pass where the searlized file info is read into the memory and then sequence
     * generation will use the info to guide the generation of better sequences towards not-covered branches. 
     * 
     * The runtime info being collected includes the following:
     * 
     * Field being written by each method (dynamically monitored to be so)
     *    stored in a file prefixed with the test project's assembly name: TESTPROJECTASSEMBLYNAME.fieldAccess.bin 
     *                                                                 and TESTPROJECTASSEMBLYNAME.fieldAccess.bin.txt
     *    e.g., Seqex.Tests.fieldAccess.bin (searilized file)
     *          Seqex.Tests.fieldAccess.bin.text (debugging file being human readable)
     *
     *   Note that the contents in these files are not overwritten and new info from later applications of Pex exploration
     *   on the same test project will get accumulated/appended in this file. You may need to manually delete these files if
     *   you don't want the info from previous applciations of Pex exploration on the test project)
     *  
     * Object fields being involved in constraints of branches not being covered in the immediate pass application of Pex exploration
     *   stored in a file prefixed with the test project's assembly name: TESTPROJECTASSEMBLYNAME.insufficientFields.bin 
     *                                                                 and TESTPROJECTASSEMBLYNAME.insufficientFields.bin.txt
     *    e.g., Seqex.Tests.insufficientFields.bin (searilized file)
     *          Seqex.Tests.insufficientFields.bin.text (debugging file being human readable)
     *    Note that the contents in these files are always overwritten in a new application of Pex exploration on the 
     *    test project.
     *    
     * For the method sequence generation portion, Seqex.Tests.factories.text includes the debugging info for the 
     * factory methods being generated for a class type (note that currently this content in this file is empty because 
     * the method body of WriteOutMethodBody in the end of SeqexExplorableGuesserAttribute.cs is commented out since 
     * this debugging feature would cause running Seqex inside Visual Studio to crash. The Pex team is debugging the issue.
     * But you can uncomment the code and run Pex/Seqex in the command line. See 
     * https://sites.google.com/site/asegrp/Home/pex-development-space/pex-extension-development-notes for how to run a Pex 
     * extension via command line.
     * 
     * The implementation of sequence generation is not complete. The original key idea is to prefer (in generating sequences inside
     * a factory method) to use methods that write object fields that are involved in the path constraints of branches 
     * not being covered. A further possibly better idea is to furtehr analyze whether the not-covered branches would 
     * require "growth" of a field (from null to non-null, or from smaller integer value to larger integer value, ...)
     * or "reduction" of a field (from non-null to null, or from larger integer value to smaller integer value, ...).
     * Then we can do more detailed analysis on the side effect of a method on a field to see whether it is a "growth" type
     * or a "reduction" type. Then we can be more smart in selecting appropriate methods in sequences.
     * 
     * Complications or TODO: when a class under test uses some .NET base classes (such as collection classes) as field types.
     *  Our field write info may not be complete. Need more investigation.
     * 
     * Seqex.Tests contains some examples including a nontrivial graph example.
     *     
     * */

    [Serializable]
    public class FieldAccessInfo
    {
        //public SafeDictionary<Method, SafeDictionary<Field, SafeSet<Term>>> Results = new SafeDictionary<Method, SafeDictionary<Field, SafeSet<Term>>>();
        public
            System.Collections.Generic.Dictionary
                <string, System.Collections.Generic.Dictionary<string, HashSet<string>>> Results;

        public FieldAccessInfo()
        {
            Results =
                new System.Collections.Generic.Dictionary
                    <string, System.Collections.Generic.Dictionary<string, HashSet<string>>>();
        }
    }

    [Serializable]
    public class InsufficientObjectFactoryFieldInfo
    {
        //mapping from relevant field to associated code locations
        public HashSet<string> ReleventFields;
        //mapping from code location to relevant fields
        public System.Collections.Generic.Dictionary<string, HashSet<string>> CodeLocationToRelevantFields;

        public InsufficientObjectFactoryFieldInfo()
        {
            ReleventFields = new HashSet<string>();
            CodeLocationToRelevantFields = new System.Collections.Generic.Dictionary<string, HashSet<string>>();
        }
    }

    [Serializable]
    public class InsufficientFieldInfoWithBranchInfo
    {
        public HashSet<string> ReleventFields;
        //mapping from code location to relevant fields
        public System.Collections.Generic.Dictionary<BranchInfo, HashSet<string>> CodeLocationToRelevantFields;

        public InsufficientFieldInfoWithBranchInfo()
        {
            ReleventFields = new HashSet<string>();
            CodeLocationToRelevantFields = new Dictionary<BranchInfo, HashSet<string>>();
        }
    }

    //this class lives across PUTs/paths
    public class SeqexDatabase : PexComponentBase, IService
    {
        public FieldAccessInfo FieldAccessInfoObj;
        public InsufficientObjectFactoryFieldInfo InsufficientObjectFactoryFieldInfoObj;

        public SafeDictionary<CodeLocation, SafeSet<Field>> FieldsForUnsuccessfullyFlippedCodeLocations =
            new SafeDictionary<CodeLocation, SafeSet<Field>>();

        public SafeDictionary<string, SafeSet<string>> TermsForUnsuccessfullyFlippedCodeLocations =
            new SafeDictionary<string, SafeSet<string>>();

        //class type -> list of factory method code postfixed with relevant fields and methods
        public SafeDictionary<string, SafeList<string>> FactoryMethodInfo =
            new SafeDictionary<string, SafeList<string>>();

        private TaggedBranchCoverageBuilder<PexGeneratedTestName> CoverageBuilderMaxAggregator;

        public string InfoFileDirectory = "c:\\tempSeqex\\";
        public string FieldAcccessFileName;
        public string InsufficientObjectFactoryFieldFileName;
        public string FactoryMethodDebugFileName;
        public InsufficientFieldInfoWithBranchInfo insufficientFieldInfoWithBranchInfo;

        public bool isDebug = true;
        private string insufficientFieldInfoWithBranchInfoFileName;

        public SeqexDatabase()
        {
        }

        protected override void Initialize()
        {
            base.Initialize();

            CoverageBuilderMaxAggregator = new TaggedBranchCoverageBuilder<PexGeneratedTestName>();

            var assemblyName = this.Services.CurrentAssembly.Assembly.Assembly.ShortName;
            FieldAcccessFileName = InfoFileDirectory + assemblyName + ".fieldAccess.bin";
            InsufficientObjectFactoryFieldFileName = InfoFileDirectory + assemblyName + ".insufficientFields.bin";
            insufficientFieldInfoWithBranchInfoFileName = InfoFileDirectory + assemblyName +
                                                          ".insufficientFields.branch.bin";
            FactoryMethodDebugFileName = InfoFileDirectory + assemblyName + ".factories.txt";

            FieldAccessInfoObj = (FieldAccessInfo) LoadFInfoFromFile(FieldAccessInfoObj, FieldAcccessFileName);

            if (FieldAccessInfoObj == null)
            {
                FieldAccessInfoObj = new FieldAccessInfo();
            }

            InsufficientObjectFactoryFieldInfoObj =
                (InsufficientObjectFactoryFieldInfo)
                LoadFInfoFromFile(InsufficientObjectFactoryFieldInfoObj, InsufficientObjectFactoryFieldFileName);
            if (InsufficientObjectFactoryFieldInfoObj == null)
            {
                InsufficientObjectFactoryFieldInfoObj = new InsufficientObjectFactoryFieldInfo();
            }

            insufficientFieldInfoWithBranchInfo = new InsufficientFieldInfoWithBranchInfo();
            int countRelevantFiles = InsufficientObjectFactoryFieldInfoObj.ReleventFields.Count;
        }

        public void AccumulateMaxCoverage(TaggedBranchCoverageBuilder<PexGeneratedTestName> branchCovBuilder)
        {
            CoverageBuilderMaxAggregator.Max(branchCovBuilder);
        }

        private Object LoadFInfoFromFile(Object reference, string fileName)
        {
            if (File.Exists(fileName)) //if exisitng field access file exists, read it in
            {
                try
                {
                    Stream streamRead = File.OpenRead(fileName);
                    BinaryFormatter binaryRead = new BinaryFormatter();
                    reference = binaryRead.Deserialize(streamRead);
                    streamRead.Close();
                    return reference;
                }
                catch (Exception e)
                {
                    this.Log.Dump("fields", "reading info from " + fileName, e.ToString());
                }
            }
            return null;
        }

        private void FilterOutCoveredCodeLocations()
        {
            HashSet<CodeLocation> coveredLocations = new HashSet<CodeLocation>();
            foreach (var cl in this.FieldsForUnsuccessfullyFlippedCodeLocations.Keys)
            {
                MethodDefinitionBodyInstrumentationInfo info;
                if (cl.Method.TryGetBodyInstrumentationInfo(out info))
                {
                    int coveredBranchesCount = 0;
                    foreach (var outgoingBranchLabel in info.GetOutgoingBranchLabels(cl.Offset))
                    {
                        CoverageDomain domain;
                        int[] hits;
                        if (this.CoverageBuilderMaxAggregator.TryGetMethodHits(cl.Method, out domain, out hits) &&
                            outgoingBranchLabel < hits.Length &&
                            hits[outgoingBranchLabel] > 0)
                            //we have branches not being covered for the code location
                        {
                            coveredBranchesCount++;
                        }
                    }
                    if (coveredBranchesCount > 1) //the location has been covered
                    {
                        coveredLocations.Add(cl);
                    }
                }
            }
            foreach (var cl in coveredLocations)
            {
                this.FieldsForUnsuccessfullyFlippedCodeLocations.Remove(cl);
            }
        }

        //we dump info to files when we finish all exploration
        public void AfterExecution()
        {
            FilterOutCoveredCodeLocations();
            this.GetService<ProblemTrackDatabase>().FieldsForUnsuccessfullyFlippedCodeLocations =
                this.FieldsForUnsuccessfullyFlippedCodeLocations;
            DumpFieldAccessInfoIntoToFile();
            DumpInSufficientObjectFactoryFieldInfoIToFile();
            if (isDebug)
            {
                SafeStringBuilder sb = DumpFactoryMethodInfoToDebugFile();
                DumpInfoToDebugFile(sb.ToString(), FactoryMethodDebugFileName);
            }
        }

        public void DumpInSufficientObjectFactoryFieldInfoIToFile()
        {
            ConvertInfoFromFieldsForUnsuccessfullyFlippedCodeLocationsToInsufficientObjectFactoryFieldInfo();
            DumpInfoToFile(InsufficientObjectFactoryFieldInfoObj, InsufficientObjectFactoryFieldFileName);
            DumpInfoToFile(insufficientFieldInfoWithBranchInfo, insufficientFieldInfoWithBranchInfoFileName);
            if (isDebug)
            {
                SafeStringBuilder sb = DumpInSufficientObjectFactoryFieldInfoToDebugFile();
                DumpInfoToDebugFile(sb.ToString(), InsufficientObjectFactoryFieldFileName + ".txt");
            }
        }

        private void ConvertInfoFromFieldsForUnsuccessfullyFlippedCodeLocationsToInsufficientObjectFactoryFieldInfo()
        {
            InsufficientObjectFactoryFieldInfoObj.ReleventFields.Clear();
            InsufficientObjectFactoryFieldInfoObj.CodeLocationToRelevantFields.Clear();

            SafeStringBuilder sb = new SafeStringBuilder();

            foreach (var cl in FieldsForUnsuccessfullyFlippedCodeLocations.Keys)
            {
                HashSet<string> fs = new HashSet<string>();
                HashSet<string> fs2 = new HashSet<string>();
                SafeSet<Field> fields;
                SequencePoint sequencePoint;
                Services.SymbolManager.TryGetSequencePoint(cl.Method, cl.Offset, out sequencePoint);
                fs.Add("column: " + sequencePoint.Column);
                fs.Add("end column: " + sequencePoint.EndColumn);
                fs.Add("sp2: " + sequencePoint.Document + " line: " + sequencePoint.Line);
                fs.Add("offset: " + sequencePoint.Offset);
                fs.Add("cl offset: " + cl.Offset);
                var branchInfo = new BranchInfo(sequencePoint.Document, sequencePoint.Line, sequencePoint.Column,
                                                sequencePoint.EndColumn,
                                                cl.Method.FullName, cl.Offset);

                if (FieldsForUnsuccessfullyFlippedCodeLocations.TryGetValue(cl, out fields))
                {
                    foreach (var f in fields)
                    {
                        InsufficientObjectFactoryFieldInfoObj.ReleventFields.Add(f.FullName);
                        insufficientFieldInfoWithBranchInfo.ReleventFields.Add(f.FullName);
                        fs.Add(f.FullName);
                        fs2.Add(f.FullName);
                    }
                }
                if (InsufficientObjectFactoryFieldInfoObj.CodeLocationToRelevantFields.ContainsKey(cl.ToString()))
                {
                    InsufficientObjectFactoryFieldInfoObj.CodeLocationToRelevantFields.Remove(cl.ToString());
                }
                if (insufficientFieldInfoWithBranchInfo.CodeLocationToRelevantFields.ContainsKey(branchInfo))
                {
                    insufficientFieldInfoWithBranchInfo.CodeLocationToRelevantFields.Remove(branchInfo);
                }
                InsufficientObjectFactoryFieldInfoObj.CodeLocationToRelevantFields.Add(cl.ToString(), fs);
                insufficientFieldInfoWithBranchInfo.CodeLocationToRelevantFields.Add(branchInfo, fs2);
            }
        }

        public void DumpFieldAccessInfoIntoToFile()
        {
            DumpInfoToFile(FieldAccessInfoObj, FieldAcccessFileName);
            if (isDebug)
            {
                SafeStringBuilder sb = DumpFieldAccessInfoToDebugFile();
                DumpInfoToDebugFile(sb.ToString(), FieldAcccessFileName + ".txt");
            }
        }

        private void DumpInfoToFile(object reference, string fileName)
        {
            try
            {
                if (!Directory.Exists(InfoFileDirectory))
                    Directory.CreateDirectory(InfoFileDirectory);
                Stream streamWrite = File.Create(fileName);
                BinaryFormatter binaryWrite = new BinaryFormatter();
                binaryWrite.Serialize(streamWrite, reference);
                streamWrite.Close();
            }
            catch (Exception e)
            {
                this.Log.Dump("fields", "wrting info to " + fileName, e.ToString());
            }
        }

        private void DumpInfoToDebugFile(string s, string fileName)
        {
            try
            {
                if (!Directory.Exists(InfoFileDirectory))
                    Directory.CreateDirectory(InfoFileDirectory);
                TextWriter tw = new StreamWriter(fileName);
                tw.WriteLine(s);
                tw.Close();
            }
            catch (Exception e)
            {
                this.Log.Dump("fields", "wrting debug info to " + fileName, e.ToString());
            }
        }

        private SafeStringBuilder DumpFieldAccessInfoToDebugFile()
        {
            SafeStringBuilder sb = new SafeStringBuilder();
            foreach (var meth in FieldAccessInfoObj.Results.Keys)
            {
                sb.AppendLine("Method: " + meth);
                System.Collections.Generic.Dictionary<string, HashSet<string>> mapInfo;
                if (FieldAccessInfoObj.Results.TryGetValue(meth, out mapInfo))
                {
                    foreach (var field in mapInfo.Keys)
                    {
                        sb.AppendLine("  -- Field: " + field);
                        HashSet<string> terms;
                        if (mapInfo.TryGetValue(field, out terms))
                        {
                            foreach (var t in terms)
                            {
                                sb.AppendLine("        term: " + t);
                            }
                        }
                    }
                }
            }
            return sb;
        }

        private SafeStringBuilder DumpInSufficientObjectFactoryFieldInfoToDebugFile()
        {
            SafeStringBuilder sb = new SafeStringBuilder();
            sb.AppendLine("Relevant fields for non-covered branches ====");
            foreach (var field in InsufficientObjectFactoryFieldInfoObj.ReleventFields)
            {
                sb.AppendLine(field);
            }
            sb.AppendLine();
            sb.AppendLine("Detailed code locations and relevant fields ~~~~");

            foreach (var cl in InsufficientObjectFactoryFieldInfoObj.CodeLocationToRelevantFields.Keys)
            {
                sb.AppendLine();
                sb.AppendLine("Code location: " + cl);
                HashSet<string> fs;
                if (InsufficientObjectFactoryFieldInfoObj.CodeLocationToRelevantFields.TryGetValue(cl.ToString(), out fs))
                {
                    foreach (var f in fs)
                    {
                        sb.AppendLine("field: " + f);
                    }
                }

                SafeSet<string> terms;
                if (TermsForUnsuccessfullyFlippedCodeLocations.TryGetValue(cl, out terms))
                {
                    foreach (var t in terms)
                    {
                        sb.AppendLine("path condition term: ");
                        sb.AppendLine(t);
                    }
                }
            }
            return sb;
        }

        public void AddFactoryMethodForDebug(string typeName, string factoryMethod)
        {
            if (isDebug)
            {
                SafeList<string> mList;
                if (!this.FactoryMethodInfo.TryGetValue(typeName, out mList))
                    this.FactoryMethodInfo[typeName] = mList = new SafeList<string>();
                mList.Add(factoryMethod);
            }
        }

        private SafeStringBuilder DumpFactoryMethodInfoToDebugFile()
        {
            SafeStringBuilder sb = new SafeStringBuilder();
            sb.AppendLine("Synthesized factory methods ====");
            foreach (var type in FactoryMethodInfo.Keys)
            {
                sb.AppendLine("Class type: " + type);
                SafeList<string> mList;
                if (FactoryMethodInfo.TryGetValue(type, out mList))
                {
                    foreach (var m in mList)
                    {
                        sb.AppendLine(m);
                        sb.AppendLine("");
                    }
                }
            }
            return sb;
        }
    }
}