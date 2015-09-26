using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.ExtendedReflection.Coverage;
using Microsoft.ExtendedReflection.Emit;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Interpretation.Visitors;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Metadata.Names;
using Microsoft.ExtendedReflection.Symbols;
using Microsoft.ExtendedReflection.Utilities;
using Microsoft.Pex.Engine;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.Pex.Engine.Coverage;
using Microsoft.Pex.Engine.Logging;
using Covana.Analyzer;
using Covana.CoverageExtractor;
using Covana.ProblemExtractor;
using Covana.ResultTrackingExtrator;

namespace Covana
{
    [Serializable]
    public class ProblemInfo
    {
        public Dictionary<PexExplorableEventKind, HashSet<string>> ObjectCreationProblems;
        public HashSet<UninstrumentedMethod> ExternalMethodCallProblems;

        public ProblemInfo()
        {
            ObjectCreationProblems = new Dictionary<PexExplorableEventKind, HashSet<string>>();
            ExternalMethodCallProblems = new HashSet<UninstrumentedMethod>();
        }
    }

    [Serializable]
    public class NonCoveredBranchInfo
    {
        public HashSet<BranchInfo> Branches;

        public NonCoveredBranchInfo()
        {
            Branches = new HashSet<BranchInfo>();
        }

        public HashSet<BranchInfo> BranchLocations
        {
            get
            {
                HashSet<BranchInfo> result = new HashSet<BranchInfo>();
                foreach (var branch in Branches)
                {
                    var location = branch.ToLocation;
                    result.Add(location);
                }
                return result;
            }
        }
    }

    [Serializable]
    public class ResultTrackingInfo
    {
        public Dictionary<string, HashSet<BranchInfo>> UnIntrumentedMethodInBranch =
            new Dictionary<string, HashSet<BranchInfo>>();
    }

    public class ProblemTrackDatabase : PexComponentBase, IService
    {
        private const string TXT = ".txt";
        private string InfoFileDirectory = "c:\\tempSeqex\\";
        private string PexObjectIssueFileName;
        private string problemIssueFileName;

        public SafeDictionary<PexExplorableEventKind, SafeSet<TypeName>> ObjectCreationIssueDictionary =
            new SafeDictionary<PexExplorableEventKind, SafeSet<TypeName>>();

        public IList<ProblemEventArgs> PexProblemEventArgsList = new List<ProblemEventArgs>();
        private string uncoveredBranchFileName;
        public StringBuilder Test;
        private string testFileName;
        private ProblemInfo _issueInfo;
        private NonCoveredBranchInfo _nonCoveredBranchInfo;

        public HashSet<BranchLocation> UnCoveredBranchCodeLocations = new HashSet<BranchLocation>();
        private ResultTrackingInfo resultTrackingInfo = new ResultTrackingInfo();

        public List<SafeSet<string>> UnCoveredBranchSets = new List<SafeSet<string>>();
        private string assemblyCovFileName;
        private string assemblyName;
        private string logFileName;
        private string errorLogFileName;

        private Dictionary<Problem, HashSet<BranchInfo>> ProblemWithBranches = new Dictionary<Problem, HashSet<BranchInfo>>();

        public SafeDictionary<CodeLocation, SafeSet<Field>> FieldsForUnsuccessfullyFlippedCodeLocations { get; set; }

        public Dictionary<Method, HashSet<CodeLocation>> ExternalMethodInBranch { get; set; }

        private HashSet<CandidateObjectCreationProblem> _candidateObjectCreationProblems;

        public HashSet<CandidateObjectCreationProblem> CandidateObjectCreationProblems
        {
            get { return _candidateObjectCreationProblems; }
            set { _candidateObjectCreationProblems = value; }
        }

        public SafeSet<UninstrumentedMethod> ExternalMethods = new SafeSet<UninstrumentedMethod>();

        public StringBuilder SimpleLog = new StringBuilder("Log: \n");
        public StringBuilder ErrorLog = new StringBuilder("Error: \n");
        private string externalMethodProblemFileName;
        private string resultTrackingFileName;
        private string candidateObjectCreationIssueFileName;
        public Dictionary<string, Dictionary<int, int>> InstructionCov = new Dictionary<string, Dictionary<int, int>>();
        private string instructionCovFileName;
        public Dictionary<string, HashSet<int>> BasicBlocksOffsets = new Dictionary<string, HashSet<int>>();
        private string basicBlockOffsetFileName;
        public ProblemEventArgs CurrentSuccessfulFlippedPathCondition;
        public HashSet<ExceptionExternalMethod> ExceptionExternalMethods = new HashSet<ExceptionExternalMethod>();
        private string exceptionExternalMethodsFileName;
        public HashSet<BoundaryProblem> BoundaryIssues = new HashSet<BoundaryProblem>();
        private string boundaryIssueFileName;
        public Dictionary<string, HashSet<BranchInfo>> TargetObjectTypes = new Dictionary<string, HashSet<BranchInfo>>();
        private string targetObjectTypeFileName;
        private string foundObjectTypeFileName;
        private string foundUninstrumentedInObjectCreationFileName;
        private string currentReportPathFileName;
        public HashSet<string> FoundTypes = new HashSet<string>();

        public Dictionary<MethodDefinition, HashSet<BranchCoverageDetail>> BranchCoverageDetails =
            new Dictionary<MethodDefinition, HashSet<BranchCoverageDetail>>();

        private string coverageDetailsFileName;
        private string xmlFileDirectory;
        public Dictionary<BranchInfo, int> BranchFlipCounts = new Dictionary<BranchInfo, int>();
        private Dictionary<string, HashSet<BranchInfo>> uninstrumentedMethodsFoundInObj;
        private string issueWithBranchesFileName;

        private Dictionary<string, Dictionary<BranchInfo, HashSet<Problem>>> branchWithIssues =
            new Dictionary<string, Dictionary<BranchInfo, HashSet<Problem>>>();

        public AssemblyEx AssemblyUnderTest;
        public string ReportPath;
        public string RelativePath;

       
        protected override void Initialize()
        {
            base.Initialize();
            assemblyName = this.Services.CurrentAssembly.Assembly.Assembly.ShortName;
            InfoFileDirectory = "c:\\tempSeqex\\" + assemblyName + "\\";
            PexObjectIssueFileName = InfoFileDirectory + assemblyName + ".object.bin";
            externalMethodProblemFileName = InfoFileDirectory + assemblyName + ".uninstrumented.bin";
            instructionCovFileName = InfoFileDirectory + assemblyName + ".instructionCov.bin";
            basicBlockOffsetFileName = InfoFileDirectory + assemblyName + ".basicBlockOffset.bin";
            exceptionExternalMethodsFileName = InfoFileDirectory + assemblyName + ".exceptionExternalMethods.bin";
            boundaryIssueFileName = InfoFileDirectory + assemblyName + ".boundaryIssue.bin";
            targetObjectTypeFileName = InfoFileDirectory + assemblyName + ".targetObjectType.bin";
            foundObjectTypeFileName = InfoFileDirectory + assemblyName + ".foundObjectType.bin";
            foundUninstrumentedInObjectCreationFileName = InfoFileDirectory + assemblyName + ".uninstrumentObj.bin";
            problemIssueFileName = InfoFileDirectory + assemblyName + ".problem.txt";
            xmlFileDirectory = InfoFileDirectory + "xml\\";
            coverageDetailsFileName = xmlFileDirectory + assemblyName + ".coverageDetails";
            issueWithBranchesFileName = xmlFileDirectory + assemblyName + ".problems";
            uncoveredBranchFileName = InfoFileDirectory + assemblyName + ".uncoveredBranch.txt";
            testFileName = InfoFileDirectory + assemblyName + ".test.txt";
            assemblyCovFileName = InfoFileDirectory + assemblyName + ".assembly.bin";
            logFileName = InfoFileDirectory + assemblyName + ".log.txt";
            errorLogFileName = InfoFileDirectory + assemblyName + ".errorLog.txt";
            resultTrackingFileName = InfoFileDirectory + assemblyName + ".resultTrack.bin";
            candidateObjectCreationIssueFileName = InfoFileDirectory + assemblyName +
                                                   ".candidateObjectCreationIssue.bin";
            currentReportPathFileName = "c:\\tempSeqex\\" + "currentPath.txt";
            _issueInfo = new ProblemInfo();
            _nonCoveredBranchInfo = new NonCoveredBranchInfo();
            ExternalMethodInBranch = new Dictionary<Method, HashSet<CodeLocation>>();
            _candidateObjectCreationProblems = new HashSet<CandidateObjectCreationProblem>();
        }

        private
            void DumpInfoToDebugFile(string s, string fileName)
        {
            try
            {
                if (!
                    Directory.Exists(InfoFileDirectory))
                    Directory.CreateDirectory(InfoFileDirectory);
                TextWriter tw =
                    new
                        StreamWriter(fileName);
                tw.WriteLine(s);
                tw.Close();
            }
            catch (Exception e)
            {
                ErrorLog.AppendLine("DumpInfoToDebugFile problem info to " + fileName + " : " + e.ToString());
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
                this.ErrorLog.AppendLine("exception happen in dump info to file!");
                this.ErrorLog.AppendLine("wrting info to " + fileName + " : " + e.ToString());
            }
        }

        public void AfterExecution()
        {
            try
            {
                SimpleLog.AppendLine("report path: " + ReportPath);
                SimpleLog.AppendLine("relative path: " + RelativePath);

                DumpIssues();
                DumpProblem();
                DumpUncoveredBranches();
                DumpResultTrackingInfo();
                DumpExceptionExternalMethods();
                DumpBoudaryIssues();
                DumpTargetObjectTypes();
                DumpFoundObjectTypes();
                DumpIssuesIntoXml();
                DumpCoverageDetails();
                DumpCFGXML();
                DumpCurrentReportPath();
            }
            catch (Exception e)
            {
                ErrorLog.AppendLine("exception: " + e);
            }
            finally
            {
                DumpInfoToDebugFile(SimpleLog.ToString(), logFileName);
                DumpInfoToDebugFile(ErrorLog.ToString(), errorLogFileName);
            }
        }

        private void DumpCurrentReportPath()
        {
            DumpInfoToDebugFile(ReportPath, currentReportPathFileName);
        }

        private void DumpCFGXML()
        {
            SimpleLog.AppendLine("Test Assembly: " + AssemblyUnderTest.ShortName);
            string POSTFIX = ".CFG.xml";
            DUCoverConsole.GraphXMLTester.TestInstructionGraphXML(AssemblyUnderTest.ShortName, xmlFileDirectory + AssemblyUnderTest.ShortName + POSTFIX);
            DUCoverConsole.GraphXMLTester.TestInstructionGraphXML(AssemblyUnderTest.ShortName, ReportPath + "\\" + AssemblyUnderTest.ShortName + POSTFIX);
            AssemblyUnderTest.ReferencedAssemblies.ToList().ForEach(
                x =>
                    {
                        bool isSys = isSystemLibraries(x.Name);
                        SimpleLog.AppendLine("referenced: " + x.Name + " system library:" +
                                             isSys);
                        if (!isSys)
                        {
                            DUCoverConsole.GraphXMLTester.TestInstructionGraphXML(x.Name, xmlFileDirectory + x.Name + POSTFIX);
                            DUCoverConsole.GraphXMLTester.TestInstructionGraphXML(x.Name, ReportPath + "\\" + x.Name + POSTFIX);
                        }
                    });

        }

        private void DumpIssuesIntoXml()
        {
            foreach (var candidateObjectCreationIssue in _candidateObjectCreationProblems)
            {
                var branchInfo = candidateObjectCreationIssue.BranchLocation;
                if (_nonCoveredBranchInfo.BranchLocations.Contains(branchInfo.ToLocation))
                {
                    var issue = new Problem(ProblemKind.ObjectCreation, candidateObjectCreationIssue.TargetType,
                                          candidateObjectCreationIssue.ToString());
                    if (!ProblemWithBranches.ContainsKey(issue))
                    {
                        ProblemWithBranches.Add(issue, new HashSet<BranchInfo>());
                    }

                    ProblemWithBranches[issue].Add(branchInfo);
                }

                if (candidateObjectCreationIssue.BranchLocation.Line == -1)
                {
                    if (candidateObjectCreationIssue.TargetObjectType != null)
                    {
                        var issue = new Problem(ProblemKind.ObjectCreation, candidateObjectCreationIssue.TargetType,
                                              candidateObjectCreationIssue.ToString());
                        if (!ProblemWithBranches.ContainsKey(issue))
                        {
                            ProblemWithBranches.Add(issue, new HashSet<BranchInfo>());
                        }

                        ProblemWithBranches[issue].Add(branchInfo);
                    }
                }
            }


            foreach (KeyValuePair<string, HashSet<BranchInfo>> pair in resultTrackingInfo.UnIntrumentedMethodInBranch)
            {
                var methodName = pair.Key;
                var infos = pair.Value;
                foreach (var info in infos)
                {
                    if (_nonCoveredBranchInfo.Branches.Contains(info))
                    {
                        var issue = new Problem(ProblemKind.UnInstrumentedMethod,
                                              methodName, "UninstrumentedMethod found in branch: " + methodName);

                        if (!ProblemWithBranches.ContainsKey(issue))
                        {
                            ProblemWithBranches.Add(issue, new HashSet<BranchInfo>());
                        }

                        ProblemWithBranches[issue].Add(info);
                    }
                }
            }


            foreach (var methodsFoundInObj in uninstrumentedMethodsFoundInObj)
            {
                var branchInfo = methodsFoundInObj.Value;

                string methodName = methodsFoundInObj.Key;
                var issue = new Problem(ProblemKind.UnInstrumentedMethod,
                                      methodName, "UninstrumentedMethod found in objec creation: " + methodName);

                if (!ProblemWithBranches.ContainsKey(issue))
                {
                    ProblemWithBranches.Add(issue, new HashSet<BranchInfo>());
                }
                foreach (var info in branchInfo)
                {
                    ProblemWithBranches[issue].Add(info);
                }
            }

            foreach (var exceptionExternalMethod in ExceptionExternalMethods)
            {
                int line = 0;
                try
                {
                    line = Convert.ToInt32(exceptionExternalMethod.Line);
                }
                catch (Exception)
                {
                }
                var branchInfo = new BranchInfo("", line, 0, 0, exceptionExternalMethod.CallerMethod, 0);

                string methodName = exceptionExternalMethod.MethodName;
                var issue = new Problem(ProblemKind.UnInstrumentedMethod,
                                      methodName,
                                      "uninstrumented method found in exception:\n" +
                                      exceptionExternalMethod.ExceptionString);

                if (!ProblemWithBranches.ContainsKey(issue))
                {
                    ProblemWithBranches.Add(issue, new HashSet<BranchInfo>());
                }

                ProblemWithBranches[issue].Add(branchInfo);
            }

            foreach (BoundaryProblem boundaryIssue in BoundaryIssues)
            {
                var issue = new Problem(ProblemKind.Boundary,
                                      boundaryIssue.FlippedLocation.Method,
                                      "boundary issue:\n" +
                                      boundaryIssue);
                if (!ProblemWithBranches.ContainsKey(issue))
                {
                    ProblemWithBranches.Add(issue, new HashSet<BranchInfo>());
                }

                ProblemWithBranches[issue].Add(boundaryIssue.FlippedLocation);
            }

            try
            {
                if (!
                    Directory.Exists(xmlFileDirectory))
                    Directory.CreateDirectory(xmlFileDirectory);
            }
            catch (Exception e)
            {
                ErrorLog.AppendLine("DumpXml problem info to " + xmlFileDirectory + " : " + e.ToString());
            }


            FileStream stream = new FileStream(issueWithBranchesFileName + ".xml", FileMode.Create);
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\"?><issues />");

            XmlNode root = xmlDoc.DocumentElement;
            var settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;
            XmlWriter writer = XmlWriter.Create(stream, settings);
            //            XmlElement root = xmlDoc.CreateElement("BranchCoverage");
            //            docElement.AppendChild(root);
            XmlWriter writerInReportPath = XmlWriter.Create(new FileStream(ReportPath + "\\" + assemblyName+".problems.xml",FileMode.Create),settings);
            branchWithIssues = new Dictionary<string, Dictionary<BranchInfo, HashSet<Problem>>>();
            foreach (var pair in ProblemWithBranches)
            {
                Problem issue = pair.Key;
                HashSet<BranchInfo> infos = pair.Value;
                foreach (BranchInfo branchInfo in infos)
                {
                    string document = branchInfo.Document ?? "null";
                    if (!branchWithIssues.ContainsKey(document))
                    {
                        branchWithIssues.Add(document, new Dictionary<BranchInfo, HashSet<Problem>>());
                    }
                    if (!branchWithIssues[document].ContainsKey(branchInfo))
                    {
                        branchWithIssues[document].Add(branchInfo, new HashSet<Problem>());
                    }
                    branchWithIssues[document][branchInfo].Add(issue);
                }
            }


            foreach (var pair in ProblemWithBranches)
            {
                Problem issue = pair.Key;
                HashSet<BranchInfo> infos = pair.Value;
                XmlElement issueElement = xmlDoc.CreateElement("issue");
                issueElement.SetAttribute("kind", issue.Kind.ToString());
                issueElement.SetAttribute("type", issue.Type);
                issueElement.SetAttribute("description", issue.Description);
                foreach (var branchInfo in infos)
                {
                    XmlElement methodElement = xmlDoc.CreateElement("branch");

//                    TypeDefinition type;
//                    if (valuePair.Key.TryGetDeclaringType(out type))
//                    {
//                        
//                    }

                    XmlElement branchElement = xmlDoc.CreateElement("branch");
                    //                        branchElement.SetAttribute("filename", coverageDetail.BranchInfo.Document);
                    branchElement.SetAttribute("src", branchInfo.Document ?? "null");
                    branchElement.SetAttribute("line", branchInfo.Line.ToString());
                    branchElement.SetAttribute("column", branchInfo.Column.ToString());
                    branchElement.SetAttribute("endColumn", branchInfo.EndColumn.ToString());
                    branchElement.SetAttribute("offset", branchInfo.ILOffset.ToString());
                    issueElement.AppendChild(branchElement);
                }
                root.AppendChild(issueElement);
            }
            xmlDoc.Save(writer);
            xmlDoc.Save(writerInReportPath);
            writer.Close();
            writerInReportPath.Close();
//            DumpXml(BranchCoverageDetails, coverageDetailsFileName + ".xml");
        }

        private void DumpCoverageDetails()
        {
            StringBuilder sb = new StringBuilder("coverage details:\n");
            foreach (KeyValuePair<MethodDefinition, HashSet<BranchCoverageDetail>> pair in BranchCoverageDetails)
            {
                sb.AppendLine("method name: " + pair.Key + "\n");
                foreach (BranchCoverageDetail coverageDetail in pair.Value)
                {
                    sb.AppendLine("branch: " + coverageDetail.BranchInfo);
                    sb.AppendLine("covered: " + coverageDetail.CoveredTimes);
                    sb.AppendLine("target: " + coverageDetail.TargetLocation);
                    sb.AppendLine("targetcovered: " + coverageDetail.targetCoveredTimes);
                }
            }
            DumpInfoToDebugFile(sb.ToString(), coverageDetailsFileName + TXT);
            DumpXml(BranchCoverageDetails, coverageDetailsFileName + ".xml");
        }

        private void DumpXml(Dictionary<MethodDefinition, HashSet<BranchCoverageDetail>> data
                             , string filename)
        {
            try
            {
                if (!
                    Directory.Exists(xmlFileDirectory))
                    Directory.CreateDirectory(xmlFileDirectory);
            }
            catch (Exception e)
            {
                ErrorLog.AppendLine("DumpXml problem info to " + xmlFileDirectory + " : " + e.ToString());
            }

            string[] paths = filename.Split(new char[]{'\\'});
            string lastFileName = paths[paths.Length - 1]; 

            FileStream stream = new FileStream(filename, FileMode.Create);
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\"?><branchCoverage />");



            XmlNode root = xmlDoc.DocumentElement;
            var settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;
            XmlWriter writer = XmlWriter.Create(stream, settings);
            XmlWriter writerInReportPath = XmlWriter.Create(new FileStream(ReportPath + "\\" + lastFileName, FileMode.Create), settings);

//            XmlElement root = xmlDoc.CreateElement("BranchCoverage");
//            docElement.AppendChild(root);

            Dictionary<string, Dictionary<MethodDefinition, HashSet<BranchCoverageDetail>>> d =
                new Dictionary<string, Dictionary<MethodDefinition, HashSet<BranchCoverageDetail>>>();
            foreach (KeyValuePair<MethodDefinition, HashSet<BranchCoverageDetail>> pair in data)
            {
                var method = pair.Key;
                foreach (BranchCoverageDetail coverageDetail in pair.Value)
                {
                    string document = coverageDetail.BranchInfo.Document ?? "null";
                    if (!d.ContainsKey(document))
                    {
                        d.Add(document, new Dictionary<MethodDefinition, HashSet<BranchCoverageDetail>>());
                    }
                    if (!d[document].ContainsKey(method))
                    {
                        d[document].Add(method, new HashSet<BranchCoverageDetail>());
                    }
                    d[document][method].Add(coverageDetail);
                }
            }

            foreach (KeyValuePair<string, Dictionary<MethodDefinition, HashSet<BranchCoverageDetail>>> pair in d)
            {
                XmlElement docElement = xmlDoc.CreateElement("document");
                string document = pair.Key;
                docElement.SetAttribute("src", document);
                foreach (KeyValuePair<MethodDefinition, HashSet<BranchCoverageDetail>> valuePair in pair.Value)
                {
                    XmlElement methodElement = xmlDoc.CreateElement("method");
                    methodElement.SetAttribute("name", valuePair.Key.ShortName);
                    docElement.AppendChild(methodElement);
                    TypeDefinition type;
                    if (valuePair.Key.TryGetDeclaringType(out type))
                    {
                        methodElement.SetAttribute("type", type.FullName);
                    }
                    foreach (BranchCoverageDetail coverageDetail in valuePair.Value)
                    {
                        XmlElement branchElement = xmlDoc.CreateElement("branch");
//                        branchElement.SetAttribute("filename", coverageDetail.BranchInfo.Document);
                        branchElement.SetAttribute("line", coverageDetail.BranchInfo.Line.ToString());
                        branchElement.SetAttribute("column", coverageDetail.BranchInfo.Column.ToString());
                        branchElement.SetAttribute("endColumn", coverageDetail.BranchInfo.EndColumn.ToString());
                        branchElement.SetAttribute("offset", coverageDetail.BranchInfo.ILOffset.ToString());
                        branchElement.SetAttribute("type", coverageDetail.Type);

                        if (BranchFlipCounts.ContainsKey(coverageDetail.BranchInfo))
                        {
                            branchElement.SetAttribute("flipCount",
                                                       BranchFlipCounts[coverageDetail.BranchInfo].ToString());
                        }
                        else
                        {
                            branchElement.SetAttribute("flipCount", "0");
                        }

                        branchElement.SetAttribute("branchLabel", coverageDetail.BranchLabel.ToString());
                        branchElement.SetAttribute("outgoingLabel", coverageDetail.OutgoingLabel.ToString());
                        branchElement.SetAttribute("isBranch", coverageDetail.IsBranch.ToString());
                        branchElement.SetAttribute("isCheck", coverageDetail.IsCheck.ToString());
                        branchElement.SetAttribute("isContinue", coverageDetail.IsContinue.ToString());
                        branchElement.SetAttribute("isFailedCheck", coverageDetail.IsFailedCheck.ToString());
                        branchElement.SetAttribute("isStartMethod", coverageDetail.IsStartMethod.ToString());
                        branchElement.SetAttribute("isSwitch", coverageDetail.IsSwitch.ToString());
                        branchElement.SetAttribute("isTarget", coverageDetail.IsTarget.ToString());
                        branchElement.SetAttribute("coveredTimes", coverageDetail.CoveredTimes.ToString());

                        XmlElement targetElement = xmlDoc.CreateElement("targetLocation");
                        if (coverageDetail.TargetLocation != null)
                        {
                            targetElement.SetAttribute("filename", coverageDetail.TargetLocation.Document);
                            targetElement.SetAttribute("line", coverageDetail.TargetLocation.Line.ToString());
                            targetElement.SetAttribute("column", coverageDetail.TargetLocation.Column.ToString());
                            targetElement.SetAttribute("endColumn", coverageDetail.TargetLocation.EndColumn.ToString());
                            targetElement.SetAttribute("offset", coverageDetail.TargetLocation.ILOffset.ToString());
                            targetElement.SetAttribute("coveredTimes", coverageDetail.targetCoveredTimes.ToString());
                        }

                        XmlElement issuesElement = xmlDoc.CreateElement("issues");
                        try
                        {
                            if (branchWithIssues.ContainsKey(document))
                            {
                                if (branchWithIssues[document].ContainsKey(coverageDetail.BranchInfo))
                                {
                                    foreach (Problem issue in branchWithIssues[document][coverageDetail.BranchInfo])
                                    {
                                        XmlElement issueElement = xmlDoc.CreateElement("issue");
                                        issueElement.SetAttribute("kind", issue.Kind.ToString());
                                        issueElement.SetAttribute("type", issue.Type);
                                        issueElement.SetAttribute("description", issue.Description);
                                        issuesElement.AppendChild(issueElement);
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            ErrorLog.AppendLine("adding issue xml element of in doc: " + document + " branch: " +
                                                coverageDetail.BranchInfo + " exception: " + e);
                        }
                        branchElement.AppendChild(targetElement);
                        branchElement.AppendChild(issuesElement);
                        methodElement.AppendChild(branchElement);
                    }
                }
                root.AppendChild(docElement);
            }
            xmlDoc.Save(writer);
            xmlDoc.Save(writerInReportPath);
            writer.Close();
            writer.Close();
        }

        private void DumpFoundObjectTypes()
        {
            StringBuilder sb = new StringBuilder("found object types: \n");
            FoundTypes.ToList().ForEach(x => sb.AppendLine("object type: " + x));
            DumpInfoToDebugFile(sb.ToString(), foundObjectTypeFileName + TXT);
            DumpInfoToFile(FoundTypes, foundObjectTypeFileName);
        }

        private void DumpTargetObjectTypes()
        {
            StringBuilder sb = new StringBuilder("target types: \n");
            foreach (var type in TargetObjectTypes)
            {
                sb.AppendLine("type: " + type.Key);
                sb.AppendLine("branch: ");
                type.Value.ToList().ForEach(x => sb.AppendLine(x.ToString()));
            }
            DumpInfoToDebugFile(sb.ToString(), targetObjectTypeFileName + TXT);

            StringBuilder sb2 = new StringBuilder("found uninstrumented methods in object creation: \n");
            uninstrumentedMethodsFoundInObj = new Dictionary<string, HashSet<BranchInfo>>();
            foreach (var targetObjectType in TargetObjectTypes)
            {
                foreach (var uninstrumentedMethod in ExternalMethods)
                {
                    string fullName = uninstrumentedMethod.Method.FullName;
                    if (fullName.IndexOf("ctor") != -1 &&
                        fullName.IndexOf(targetObjectType.Key) != -1 &&
                        ((fullName.IndexOf("+") != -1 && targetObjectType.Key.IndexOf("+") != -1) ||
                         fullName.IndexOf("+") == -1 && targetObjectType.Key.IndexOf("+") == -1)
                        )
                    {
                        if (!uninstrumentedMethodsFoundInObj.ContainsKey(fullName))
                        {
                            uninstrumentedMethodsFoundInObj[fullName] = targetObjectType.Value;
                        }
                    }
                }
            }

//            foreach (var uninstrumentedMethod in UnInstrumentedMethods)
//            {
//                 if (uninstrumentedMethod.Method.FullName.IndexOf("ctor") != -1)
//                    {
//                        if (!methods.ContainsKey(uninstrumentedMethod.Method.FullName))
//                        {
//                            StackTraceName stackTrace;
//                            uninstrumentedMethod.TryGetStackTrace(out stackTrace);
//                            sb2.AppendLine("trace");
//                            foreach (var name in stackTrace.Frames)
//                            {
//                                sb2.AppendLine("method: " + name.Method + " offset: " + name.Offset);
//                            }
////                            methods[uninstrumentedMethod.Method.FullName] = new BranchInfo("");
//                        }
//                    }
//            }
            foreach (var method in uninstrumentedMethodsFoundInObj)
            {
                sb2.AppendLine(
                    "method: " + method.Key + " branch: "
                    );
                method.Value.ToList().ForEach(x => sb2.AppendLine(x.ToString()));
            }
            DumpInfoToDebugFile(sb2.ToString(), foundUninstrumentedInObjectCreationFileName + TXT);
            DumpInfoToFile(uninstrumentedMethodsFoundInObj, foundUninstrumentedInObjectCreationFileName);
        }

        private void DumpBoudaryIssues()
        {
            StringBuilder sb = new StringBuilder("boudary issues: \n");
            foreach (var boundaryIssue in BoundaryIssues)
            {
                sb.AppendLine(boundaryIssue.ToString());
            }
            DumpInfoToDebugFile(sb.ToString(), boundaryIssueFileName + TXT);
            DumpInfoToFile(BoundaryIssues, boundaryIssueFileName);
        }

        private void DumpExceptionExternalMethods()
        {
            StringBuilder sb = new StringBuilder("exception external methods: \n");
            foreach (var exceptionExternalMethod in ExceptionExternalMethods)
            {
                sb.AppendLine(exceptionExternalMethod.ToString());
            }
            DumpInfoToDebugFile(sb.ToString(), exceptionExternalMethodsFileName + TXT);
            DumpInfoToFile(ExceptionExternalMethods, exceptionExternalMethodsFileName);
        }

        private void DumpResultTrackingInfo()
        {
//            DumpInfoToDebugFile(sb.ToString(), resultTrackingFileName+TXT);

            foreach (var keyValue in ExternalMethodInBranch)
            {
                var method = keyValue.Key;
                var locations = keyValue.Value;

                ISymbolManager sm = Services.SymbolManager;
                SequencePoint sp;
                MethodDefinitionBodyInstrumentationInfo info;
                foreach (var location in locations)
                {
                    if (sm.TryGetSequencePoint(location.Method, location.Offset, out sp) &&
                        location.Method.TryGetBodyInstrumentationInfo(out info))
                    {
                        //                    sb.AppendLine(codeLocation.Method.FullName + "," +
                        //                                  sp.Document + "," +
                        //                                  sp.Line + "," + sp.Column
                        //                                  + " outgoingbranch label: " + item.Value);
                        var branchInfo = new BranchInfo(sp.Document, sp.Line, sp.Column, sp.EndColumn,
                                                        location.Method.FullName, location.Offset);
                        if (!resultTrackingInfo.UnIntrumentedMethodInBranch.ContainsKey(method.FullName))
                        {
                            resultTrackingInfo.UnIntrumentedMethodInBranch.Add(method.FullName,
                                                                               new HashSet<BranchInfo>());
                        }
                        resultTrackingInfo.UnIntrumentedMethodInBranch[method.FullName].Add(branchInfo);
                    }
                }
            }


            var sb = new StringBuilder("result track: ");
            foreach (var branch in resultTrackingInfo.UnIntrumentedMethodInBranch)
            {
                sb.AppendLine("branch: " + branch.Key + " method: " + branch.Value);
            }

            DumpInfoToDebugFile(sb.ToString(), resultTrackingFileName + TXT);
            DumpInfoToFile(resultTrackingInfo, resultTrackingFileName);
        }

        private void DumpTest()
        {
            Log.Dump("test", "test", "test: ");

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("test: ");
            try
            {
                TaggedBranchCoverageBuilder<PexGeneratedTestName> cov;
                Services.CoverageManager.TryGetAssemblyCoverageBuilder(out cov);
                IEnumerable<MethodDefinition> definitions = cov.GetMethods(CoverageDomain.UserCodeUnderTest);
                IEnumerator<MethodDefinition> enumerator = definitions.GetEnumerator();
                Log.Dump("test", "test", "method count: " + definitions.Count().ToString());
                while (enumerator.MoveNext())
                {
                    MethodDefinition definition = enumerator.Current;
                    CoverageDomain domain;
                    int[] hits;
                    cov.TryGetMethodHits(definition, out domain, out hits);

                    Log.Dump("test", "test", "method hits: " + hits.Length);
                    MethodDefinitionBodyInstrumentationInfo body;
                    definition.TryGetBodyInstrumentationInfo(out body);
                    Converter<int, int> converter = body.GetInstructionCoverage(hits);
                    for (int i = 0; i < hits.Length; i++)
                    {
                        int offset = converter.Invoke(hits[i]);
                        ISymbolManager sm = Services.SymbolManager;
                        SequencePoint sp;
                        sm.TryGetSequencePoint(definition, offset, out sp);
                        sb.AppendLine("test: " + definition.FullName + " domain: " + domain + " doc: " + sp.Document +
                                      " line: " + sp.Line + " column: " + sp.Column);
                    }

                    Log.Dump("test", "test", sb.ToString());
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("exception: " + e);
                Log.Dump("test", "test", "exception: " + e);
            }

            DumpInfoToDebugFile(sb.ToString(), testFileName);
        }

        private void DumpUncoveredBranches()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var unCoveredBranchSet in UnCoveredBranchSets)
            {
                foreach (var item in unCoveredBranchSet)
                {
                    sb.AppendLine("uncovered: " + item);
                }
            }
            DumpInfoToDebugFile(sb.ToString(), uncoveredBranchFileName);
        }

        private void DumpProblem()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (var problemEventArgs in PexProblemEventArgsList)
            {
                CodeLocation location = problemEventArgs.FlippedLocation;
                Log.Dump("My Category", "flipped location: " + location, null);
                SequencePoint sp;
                Services.SymbolManager.TryGetSequencePoint(location.Method, location.Offset, out sp);
                sb.AppendLine("=======================================================");
                sb.AppendLine("result: " + problemEventArgs.Result);
                sb.AppendLine("flipped location: " + sp.Document + " line: " + sp.Line);
                var flippedCondition = problemEventArgs.Suffix;
//                sb.AppendLine("suffix term: " + ((ISymbolId)flippedCondition).Description);
                var location1 = problemEventArgs.ParentOfFlipped.CodeLocation;
                Services.SymbolManager.TryGetSequencePoint(location1.Method, location1.Offset, out sp);
                sb.AppendLine("ParentOfFlipped location: " + sp.Document + " line: " + sp.Line);

                var stringWriter = new StringWriter();
                var bodyWriter = this.Services.LanguageManager.DefaultLanguage.CreateBodyWriter(stringWriter,
                                                                                                VisibilityContext.
                                                                                                    Private);
                var extractor = new ResultTrackConditionExtractor(problemEventArgs.TermManager);
                extractor.VisitTerm(default(TVoid), flippedCondition);

                var emitter = new TermEmitter(problemEventArgs.TermManager, new NameCreator());
                if (emitter.TryEvaluate(Indexable.One(flippedCondition), 1000, bodyWriter))
                {
                    bodyWriter.Return(SystemTypes.Bool);
                }
                Log.Dump("My Category", "condition", stringWriter.ToString());
                sb.AppendLine(stringWriter.ToString());
            }

            DumpInfoToDebugFile(sb.ToString(), problemIssueFileName);
        }

        private void DumpIssues()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("object creation issues: ");
            foreach (var kind in ObjectCreationIssueDictionary.Keys)
            {
                foreach (var type in ObjectCreationIssueDictionary[kind])
                {
                    sb.AppendLine("kind: " + kind + " type: " + type);
                    var issues = _issueInfo.ObjectCreationProblems;
                    if (!issues.ContainsKey(kind))
                    {
                        issues[kind] = new HashSet<string>();
                    }
                    issues[kind].Add(type.FullName);
                }

//                string shortDescription = ex.GetShortDescription(constant);
//                sb.AppendLine("kind: " + ex.Kind);
//                sb.AppendLine("short: " + shortDescription);
//                var writer = new StringWriter();
//                if (ex.TryWriteLongDescription(constant, writer))
//                {
//                    sb.AppendLine("long: " + writer.ToString());
//                }
//                sb.AppendLine("preview descriptionL: " + ex.GetPreviewDescription(constant));
//                sb.AppendLine("title: " + ex.Title);
//                sb.AppendLine("ExplorableType: " + ex.ExplorableType);
//                sb.AppendLine();
            }

            var sb2 = new StringBuilder();
            sb2.AppendLine("uninstrumented method issues: ");
            var unInstumentedMethodNames = new List<string>();
            foreach (var method in ExternalMethods)
            {
                _issueInfo.ExternalMethodCallProblems.Add(method);
                sb2.AppendLine("method: " + method.Method.FullName);
                unInstumentedMethodNames.Add("method: " + method.Method.FullName);
            }

            DumpInfoToFile(_issueInfo, PexObjectIssueFileName);
            DumpInfoToDebugFile(sb.ToString(), PexObjectIssueFileName + TXT);
            DumpInfoToFile(unInstumentedMethodNames, externalMethodProblemFileName);
            DumpInfoToDebugFile(sb2.ToString(), externalMethodProblemFileName + TXT);

            DumpInfoToFile(_candidateObjectCreationProblems, candidateObjectCreationIssueFileName);
            var sb3 = new StringBuilder("candidate object cretion issues: ");
            sb3.AppendLine("count: " + CandidateObjectCreationProblems.Count);
            try
            {
                if (CandidateObjectCreationProblems.Count > 0)
                {
                    foreach (CandidateObjectCreationProblem issue in CandidateObjectCreationProblems)
                    {
                        sb3.AppendLine(issue.ToString());
                    }
                }
                else
                {
                    sb3.AppendLine("No object creation issues.");
                }
            }
            catch (Exception ex)
            {
                sb3.AppendLine("ex: " + ex);
            }
            sb3.AppendLine(MethodOrFieldAnalyzer.Log.ToString());
            DumpInfoToDebugFile(sb3.ToString(), candidateObjectCreationIssueFileName + TXT);
        }

        public void DumpAssemblyCoverage(IPexComponent host)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in UnCoveredBranchCodeLocations)
            {
                CodeLocation codeLocation = item.Location;
                ISymbolManager sm = host.Services.SymbolManager;
                SequencePoint sp;
                MethodDefinitionBodyInstrumentationInfo info;
                if (sm.TryGetSequencePoint(codeLocation.Method, codeLocation.Offset, out sp) &&
                    codeLocation.Method.TryGetBodyInstrumentationInfo(out info))
                {
                    sb.AppendLine(codeLocation.Method.FullName + "," +
                                  sp.Document + "," +
                                  sp.Line + "," + sp.Column
                                  + " outgoingbranch label: " + item.OutgointBranchLabel + " offset: " +
                                  codeLocation.Offset.ToString("x"));

                    var branchInfo = new BranchInfo(sp.Document, sp.Line, sp.Column, sp.EndColumn,
                                                    codeLocation.Method.FullName, codeLocation.Offset);
                    _nonCoveredBranchInfo.Branches.Add(branchInfo);
                }
            }

            DumpInfoToDebugFile(sb.ToString(), assemblyCovFileName + TXT);
            DumpInfoToFile(_nonCoveredBranchInfo, assemblyCovFileName);

            StringBuilder sb2 = new StringBuilder("instruction cov: \n");
            foreach (var cov in InstructionCov)
            {
                sb2.AppendLine("method: " + cov.Key);
                foreach (var pair in cov.Value)
                {
                    sb2.AppendLine("offset: " + pair.Key.ToString("x") + " covered: " + pair.Value);
                }
            }
            DumpInfoToDebugFile(sb2.ToString(), instructionCovFileName + TXT);
            DumpInfoToFile(InstructionCov, instructionCovFileName);

            StringBuilder sb3 = new StringBuilder("basicblockOffsets cov: \n");
            foreach (var offset in BasicBlocksOffsets)
            {
                sb3.AppendLine("method: " + offset.Key);
                foreach (var pair in offset.Value)
                {
                    sb3.AppendLine("offset: " + pair.ToString("x"));
                }
            }
            DumpInfoToDebugFile(sb3.ToString(), basicBlockOffsetFileName + TXT);
        }

        private bool isSystemLibraries(string assemblyName)
        {
            HashSet<String> systemLibraries = new HashSet<string>
                                                  {
                                                      "mscorlib"
                                                  };

            if (assemblyName.StartsWith("System"))
            {
                return true;
            }

            if (assemblyName.StartsWith("Microsoft"))
            {
                return true;
            }


            if (systemLibraries.Contains(assemblyName))
            {
                return true;
            }



            return false;
        }
    }
}