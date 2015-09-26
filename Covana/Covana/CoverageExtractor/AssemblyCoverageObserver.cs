using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.ExtendedReflection.Coverage;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Symbols;
using Microsoft.ExtendedReflection.Utilities;
using Microsoft.Pex.Engine;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.Pex.Engine.Coverage;
using Microsoft.Pex.Framework.Packages;
using Covana.Util;

namespace Covana.CoverageExtractor
{
    [Serializable]
    public class BranchLocation
    {
        public CodeLocation Location;
        public int OutgointBranchLabel;

        public BranchLocation(CodeLocation location, int label)
        {
            this.Location = location;
            this.OutgointBranchLabel = label;
        }

        public bool Equals(BranchLocation other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.Location.Equals(Location) && other.OutgointBranchLabel == OutgointBranchLabel;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (BranchLocation)) return false;
            return Equals((BranchLocation) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Location.GetHashCode()*397) ^ OutgointBranchLabel;
            }
        }
    }


    public class AssemblyCoverageObserver : PexExecutionPackageAttributeBase
    {
        private string assemblyCovFileName;
        private string InfoFileDirectory;
        private HashSet<BranchLocation> locations;
        private StringBuilder Log;
        private StringBuilder ErrorLog;
        private Dictionary<string, Dictionary<int, int>> instructionCov = new Dictionary<string, Dictionary<int, int>>();
        private Dictionary<string, HashSet<int>> basicBlockOffsets = new Dictionary<string, HashSet<int>>();
        private bool debug = true;
        private Dictionary<MethodDefinition, HashSet<BranchCoverageDetail>> branchCoverageDetails = new Dictionary<MethodDefinition, HashSet<BranchCoverageDetail>>();

        protected override object BeforeExecution(IPexComponent host)
        {
            host.Services.CoverageManager.BeforePublishAssemblyCoverage += Handler(host);
            locations = host.GetService<ProblemTrackDatabase>().UnCoveredBranchCodeLocations;
            Log = new StringBuilder();
            ErrorLog = host.GetService<ProblemTrackDatabase>().ErrorLog;
            return null;
        }

        private RemoteEventHandler<RemoteEventArgs> Handler(IPexComponent host)
        {
            return e =>
                       {
                           try
                           {
                               FindUncoveredBranches(host);

                               if (debug)
                               {
                                   host.GetService<ProblemTrackDatabase>().SimpleLog.AppendLine(Log.ToString());
                               }


                               host.GetService<ProblemTrackDatabase>().DumpAssemblyCoverage(host);
                           }
                           catch (Exception ex)
                           {
                               host.GetService<ProblemTrackDatabase>().ErrorLog.AppendLine("exception in assembly cov: " +
                                                                                         ex);
                           }
                       };
        }

        public void FindUncoveredBranches(IPexComponent host)
        {
            TaggedBranchCoverageBuilder<PexGeneratedTestName> cov;
            IPexCoverageManager manager = host.Services.CoverageManager;
            StringBuilder sb = new StringBuilder("method coverage: \n");

            try
            {
                if (manager.TryGetAssemblyCoverageBuilder(out cov))
                {
//                    var definitions = cov.Methods;
                    IEnumerable<MethodDefinition> definitions = cov.Methods;

                    host.Log.Dump("test", "test", "methods: " + definitions.Count());
                    Log.AppendLine("assembly methods: " + definitions.Count());
                    sb.AppendLine("methods: " + definitions.Count());

                    foreach (var method in definitions)
                    {
                        Log.AppendLine("method: " + method.FullName);
//                        Method m = method.Instantiate(TypeEx.NoTypes, TypeEx.NoTypes);
//                        MethodBodyEx body;
//                        if (!m.TryGetBody(out body))
//                        {
//                            //error
//                            Log.AppendLine("load method body failed");
//                        }

//                        method.Instantiate()
                        CoverageDomain domain;
                        if (!branchCoverageDetails.ContainsKey(method))
                        {
                            branchCoverageDetails.Add(method,new HashSet<BranchCoverageDetail>());
                        }
                        int[] hits;
                        if (cov.TryGetMethodHits(method, out domain, out hits))
                        {
                            Log.AppendLine("method hits: " + hits.Length + " " + hits);
                            for (int branchLabel = 0; branchLabel < hits.Length; branchLabel++)
                            {
                                CodeLocation location = method.GetBranchLabelSource(branchLabel);
                                Log.AppendLine("current location: " + location);
                                MethodDefinitionBodyInstrumentationInfo info;
                                if (method.TryGetBodyInstrumentationInfo(out info))
                                {
                                    ISymbolManager sm = host.Services.SymbolManager;
                                    SequencePoint sp;
                                    sm.TryGetSequencePoint(method, location.Offset, out sp);
                                    Log.AppendLine("info: " + info.MethodDefinition.FullName);
//                                    foreach (var index in info.BasicBlockStartOffsets)
//                                    {
//                                        Log.AppendLine("start of BB: " + index.ToString("x"));
//                                    }

//                                    var util = new BasicBlockUtil(Log, body, info.BasicBlockStartOffsets);
//                                    ReadInstructionCov(method, hits, info);


                                    foreach (
                                        var outgoingBranchLabel in
                                            info.GetOutgoingBranchLabels(location.Offset))
                                    {
                                        CodeBranch codeBranch = new CodeBranch(location.Method,
                                                                               outgoingBranchLabel);
                                        if (!codeBranch.IsBranch && !codeBranch.IsSwitch && !codeBranch.IsCheck) // is explicit branch?
                                        {
                                            host.Log.Dump("coverage", "coverage",
                                                          "CodeBranch: " + codeBranch +
                                                          " is not explicit");
                                            sb.AppendLine("CodeBranch: " + codeBranch +
                                                          " is not explicit");

                                            Log.AppendLine("CodeBranch: " + codeBranch +
                                                           " is not explicit");
                                            continue; // if not, we don't log it                         
                                        }

                                        var fromMethod = method.FullName + "," +
                                                            sp.Document + "," +
                                                            sp.Line + ", column: " + sp.Column + " outgoing label: " +
                                                            outgoingBranchLabel;
                                        BranchInfo branchInfo = new BranchInfo(sp.Document,sp.Line,sp.Column,sp.EndColumn,method.FullName,location.Offset);
                                        Log.AppendLine("Checking CodeBranch: " + codeBranch);
                                        Log.AppendLine("CodeBranch location: " + fromMethod);
                                        Log.AppendLine("hits.Length: " + hits.Length);
                                        Log.AppendLine("codeBranch.IsBranch: " + codeBranch.IsBranch);
                                        Log.AppendLine("codeBranch.IsCheck: " + codeBranch.IsCheck);
                                        Log.AppendLine("codeBranch.IsContinue: " + codeBranch.IsContinue);
                                        Log.AppendLine("codeBranch.IsFailedCheck: " + codeBranch.IsFailedCheck);
                                        Log.AppendLine("codeBranch.IsStartMethod: " + codeBranch.IsStartMethod);
                                        Log.AppendLine("codeBranch.IsSwitch: " + codeBranch.IsSwitch);
                                        Log.AppendLine("codeBranch.IsTarget: " + codeBranch.IsTarget);

                                        int branchhit = 0;
                                        if (outgoingBranchLabel < hits.Length &&
                                            hits[outgoingBranchLabel] != 0)
                                        {
                                            Log.AppendLine("CodeBranch: " + codeBranch + " is covered " + hits[outgoingBranchLabel] + " times");
                                            branchhit = hits[outgoingBranchLabel];
                                        }

                                        if(outgoingBranchLabel >= hits.Length){
                                            Log.AppendLine("CodeBranch: " + codeBranch + " is not covered " +  " since outgoing label"+ outgoingBranchLabel +" is larger than" + hits.Length + ".");
                                            branchhit = 0;
                                        
                                        }

                                        string type = "";
                                        if (codeBranch.IsBranch)
                                        {
                                            type = "Explicit";
                                        }
                                        else if(codeBranch.IsCheck)
                                        {
                                            type = "Implicit";
                                        }


                                        BranchCoverageDetail coverageDetail = new BranchCoverageDetail(branchInfo,branchhit,null,0,type);
                                        coverageDetail.CopyBranchProperties(codeBranch);
                                        coverageDetail.OutgoingLabel = outgoingBranchLabel;
                                        coverageDetail.BranchLabel = branchLabel;
                                        int targetOffset;
                                        if (info.TryGetTargetOffset(outgoingBranchLabel, out targetOffset))
                                        {
                                            sm.TryGetSequencePoint(method, targetOffset, out sp);
                                            var targetCovTimes =
                                                info.GetInstructionCoverage(hits).Invoke(targetOffset);
                                            Log.AppendLine(fromMethod
                                                           + "\n going to: " + sp.Document + " line: " +
                                                           sp.Line + " column: " + sp.Column + " endcolumn: " +
                                                           sp.EndColumn + " target: " + targetOffset.ToString("x"));
                                            Log.AppendLine("target offset: " + targetOffset.ToString("x"));
                                            Log.AppendLine("it is covered at " + targetCovTimes + " times");
                                            Log.AppendLine("outgoing lables: " +
                                                           info.GetOutgoingBranchLabels(targetOffset).Count);
                                            BranchInfo targetInfo = new BranchInfo(sp.Document, sp.Line, sp.Column, sp.EndColumn, method.FullName, targetOffset);
                                            coverageDetail.TargetLocation = targetInfo;
                                            coverageDetail.targetCoveredTimes = targetCovTimes;
                                            if (!branchCoverageDetails[method].Contains(coverageDetail))
                                            {
                                                branchCoverageDetails[method].Add(coverageDetail);
                                            }
                                            else
                                            {
                                                foreach (BranchCoverageDetail detail in branchCoverageDetails[method])
                                                {
                                                    if (detail.Equals(coverageDetail))
                                                    {
                                                        if (coverageDetail.CoveredTimes > detail.CoveredTimes)
                                                        {
                                                            detail.CoveredTimes = coverageDetail.CoveredTimes;
                                                        }
                                                    }
                                                }
                                            }
                                            

                                        }
                                        
                                        if (outgoingBranchLabel < hits.Length &&
                                            hits[outgoingBranchLabel] == 0 || branchhit == 0)
                                        {
                                            Log.AppendLine("CodeBranch: " + codeBranch + " is not covered" +
                                                           " current offset: " + location.Offset.ToString("x"));

                                           
                                           
//                                            if (!targetBlockCovered)
//                                            {
                                            locations.Add(new BranchLocation(location, outgoingBranchLabel));

                                            continue;
//                                            }
                                            if (info.TryGetTargetOffset(outgoingBranchLabel, out targetOffset))
                                            {
                                                sm.TryGetSequencePoint(method, targetOffset, out sp);
                                                var times = info.GetInstructionCoverage(hits).Invoke(targetOffset);
                                                Log.AppendLine(fromMethod + " going to: " + sp.Document + " line: " +
                                                               sp.Line + " column: " + sp.Column + " endcolumn: " +
                                                               sp.EndColumn + " target: " + targetOffset.ToString("x"));
                                                Log.AppendLine("target offset: " + targetOffset.ToString("x"));
                                                Log.AppendLine("it is covered at " + times + " times");
                                                Log.AppendLine("outgoing lables: " +
                                                               info.GetOutgoingBranchLabels(targetOffset).Count);
                                                if (times == 0)
                                                {
                                                    IIndexable<int> basicBlockStartOffsets = info.BasicBlockStartOffsets;
                                                    int indexOfBasicBlock;
                                                    for (indexOfBasicBlock = 0;
                                                         indexOfBasicBlock < basicBlockStartOffsets.Count;
                                                         indexOfBasicBlock++)
                                                    {
                                                        Log.AppendLine("basicBlockStartOffsets: " +
                                                                       basicBlockStartOffsets[indexOfBasicBlock].
                                                                           ToString("x"));
                                                        if (basicBlockStartOffsets[indexOfBasicBlock] == targetOffset)
                                                        {
                                                            break;
                                                        }
                                                    }

                                                    bool targetBlockCovered = true;
                                                    if (targetOffset + 1 >=
                                                        basicBlockStartOffsets[indexOfBasicBlock + 1])
                                                    {
                                                        Log.AppendLine("basicBlockStartOffsets: " +
                                                                       basicBlockStartOffsets[indexOfBasicBlock + 1]);
                                                        Log.AppendLine("Target Offset " +
                                                                       (targetOffset + 1).ToString("x") +
                                                                       " reach next block");
                                                        targetBlockCovered = false;
                                                    }
                                                    else
                                                    {
                                                        Log.AppendLine("basicBlockStartOffsets: " +
                                                                       basicBlockStartOffsets[indexOfBasicBlock + 1].
                                                                           ToString("x"));
                                                        Log.AppendLine("Target Offset " +
                                                                       (targetOffset + 1).ToString("x") +
                                                                       " inside same block");
                                                        int coveredTimes =
                                                            info.GetInstructionCoverage(hits).Invoke(targetOffset + 1);
                                                        Log.AppendLine("Target Offset " +
                                                                       (targetOffset + 1).ToString("x") +
                                                                       " is covered at " + coveredTimes + " times.");
                                                        if (coveredTimes == 0)
                                                        {
                                                            targetBlockCovered = false;
                                                        }
                                                    }

                                                    if (!targetBlockCovered)
                                                    {
//                                                        locations.Add(location, outgoingBranchLabel);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    sb.AppendLine("manager.TryGetAssemblyCoverageBuilder is null!");
                }

                host.GetService<ProblemTrackDatabase>().InstructionCov = instructionCov;
                host.GetService<ProblemTrackDatabase>().BasicBlocksOffsets = basicBlockOffsets;
                host.GetService<ProblemTrackDatabase>().BranchCoverageDetails = branchCoverageDetails;
                DumpInfoToDebugFile(sb.ToString(), assemblyCovFileName);
            }
            catch (Exception ex)
            {
                host.Log.Dump("coverage", "cov ex", ex.Message);
                host.GetService<ProblemTrackDatabase>().ErrorLog.AppendLine("exception in FindUncoveredBranches: " + ex);
                DumpInfoToDebugFile("cov ex" + ex.Message, assemblyCovFileName);
            }
        }

        private void ReadInstructionCov(MethodDefinition method, int[] hits,
                                        MethodDefinitionBodyInstrumentationInfo info)
        {
            Instruction instruction;
            Method instantiate = null;
            bool logInstruction = false;
            if (method.FullName.StartsWith("System") || method.FullName.StartsWith("Microsoft.Pex.Framework"))
            {
                return;
            }

            try
            {
                instantiate = method.Instantiate(TypeEx.NoTypes, TypeEx.NoTypes);


                MethodBodyEx body;
                if (instantiate.TryGetBody(out body))
                {
                    foreach (var i in info.BasicBlockStartOffsets)
                    {
                        if (!basicBlockOffsets.ContainsKey(method.FullName))
                        {
                            basicBlockOffsets[method.FullName] = new HashSet<int>();
                        }
                        basicBlockOffsets[method.FullName].Add(i);
                    }

                    body.TryGetInstruction(0, out instruction);

                    Converter<int, int> covConverter = info.GetInstructionCoverage(hits);
                    int coveredTimes = covConverter.Invoke(0);
                    int nextOffset = instruction.NextOffset;
                    
                    if (logInstruction)
                    {
                        Log.AppendLine("instruction: " + instruction.Offset.ToString("x") +
                                " code: " + instruction.OpCode + " covered " +
                                coveredTimes + " times " +
                                " next: " + nextOffset.ToString("x"));
                    }
                 
                    if (!instructionCov.ContainsKey(method.FullName))
                    {
                        instructionCov[method.FullName] = new Dictionary<int, int>();
                        instructionCov[method.FullName][0] = coveredTimes;
                    }
                    else
                    {
                        instructionCov[method.FullName][0] = coveredTimes;
                    }

                    while (body.TryGetInstruction(nextOffset, out instruction))
                    {
                        nextOffset = instruction.NextOffset;
                        coveredTimes = covConverter.Invoke(nextOffset);
                        if (logInstruction)
                        {
                            Log.AppendLine("instruction: " + instruction.Offset.ToString("x") +
                                           " code: " + instruction.OpCode + " covered " +
                                           coveredTimes + " times " +
                                           " next: " + nextOffset.ToString("x"));
                        }
                        instructionCov[method.FullName][instruction.Offset] = coveredTimes;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.AppendLine("error in ReadInstructionCov of method: " + method + "\n" + ex);
                return;
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
            }
        }
    }
}