using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using __Auxiliary;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Interpretation.Visitors;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Reasoning;
using Microsoft.ExtendedReflection.Symbols;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.Pex.Framework.Packages;
using Covana.Analyzer;
using StringBuilder=System.Text.StringBuilder;

namespace Covana.ProblemExtractor
{
    [__DoNotInstrument]
    public class ProblemObserverAttribute :
        PexExecutionPackageAttributeBase
    {
        protected override object BeforeExecution(IPexComponent host)
        {
            SafeDebug.AssumeNotNull(host, "host");
            return new NewTestLogger(host);
        }

        protected override sealed void AfterExecution(Microsoft.Pex.Engine.ComponentModel.IPexComponent host,
                                                      object data)
        {
        }

        [__DoNotInstrument]
        private class NewTestLogger : PexComponentElementBase
        {
            private string InfoFileDirectory = "c:\\tempSeqex\\";
            private readonly string outputFile = "";


            public NewTestLogger(IPexComponent host)
                : base(host)
            {
                Host.Log.ProblemHandler += Log_ProblemHandler;
                outputFile = InfoFileDirectory + Host.Services.CurrentAssembly.Assembly.Assembly.ShortName +
                             ".problem.txt";
                //                this.outputFile = Path.Combine(host.Services.ReportManager.ReportPath, "newtests.txt");
            }

            private void Log_ProblemHandler(Microsoft.ExtendedReflection.Logging.ProblemEventArgs e)
            {
                RecordFlipCount(e);

                if (e.Result == TryGetModelResult.Success)
                {
                    Host.GetService<ProblemTrackDatabase>().SimpleLog.AppendLine("flipped result: " + e.Result);
                    Host.GetService<ProblemTrackDatabase>().CurrentSuccessfulFlippedPathCondition = e;
                    return;
                }
                if (e.Result != TryGetModelResult.Success)
                {
                    Host.GetService<ProblemTrackDatabase>().SimpleLog.AppendLine("flipped result: " + e.Result);
//                    return;
                }
                try
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    CodeLocation location = e.FlippedLocation;
                    var database = Host.GetService<ProblemTrackDatabase>();
                    database.PexProblemEventArgsList.Add(e);

//                e.Result == TryGetModelResult.
                    this.Host.Log.Dump("My Category", "flipped location: " + location, null);
                    SequencePoint sp;
                    Host.Services.SymbolManager.TryGetSequencePoint(location.Method, location.Offset, out sp);
                    sb.AppendLine("flipped location: " + sp.Document + " line: " + sp.Line + " offset: " +
                                  location.Offset);
                    sb.AppendLine("e.ParentOfFlipped.InCodeBranch: " + e.ParentOfFlipped.InCodeBranch);
                    e.ParentOfFlipped.OutCodeBranches.ToList().ForEach(x => sb.AppendLine("out: " + x));
                    var flippedCondition = e.Suffix;
                    var parentOfFlipped = e.ParentOfFlipped.CodeLocation;
                    Host.Services.SymbolManager.TryGetSequencePoint(parentOfFlipped.Method, parentOfFlipped.Offset,
                                                                    out sp);
                    sb.AppendLine("parent flipped location: " + sp.Document + " line: " + sp.Line + " offset: " +
                                  parentOfFlipped.Offset);

                    var stringWriter = new StringWriter();
                    var bodyWriter = this.Host.Services.LanguageManager.DefaultLanguage.CreateBodyWriter(stringWriter,
                                                                                                         VisibilityContext
                                                                                                             .
                                                                                                             Private);
                    var emitter = new TermEmitter(e.TermManager, new NameCreator());
                    if (emitter.TryEvaluate(Indexable.One(flippedCondition), 1000, bodyWriter))
                    {
                        bodyWriter.Return(SystemTypes.Bool);
                    }
                    string flippedTerm = stringWriter.ToString();
                    stringWriter.WriteLine();
                    stringWriter.WriteLine("Feasible prefixes:");
                    if (e.FeasiblePrefix != null && e.FeasiblePrefix.Length > 0)
                    {
                        var bodyWriter2 =
                            this.Host.Services.LanguageManager.DefaultLanguage.CreateBodyWriter(stringWriter,
                                                                                                VisibilityContext
                                                                                                    .
                                                                                                    Private);
                        foreach (Term prefix in e.FeasiblePrefix)
                        {
                            if (emitter.TryEvaluate(Indexable.One(prefix), 1000, bodyWriter2))
                            {
                                bodyWriter2.Return(SystemTypes.Bool);
                            }
                        }
                    }
                    else
                    {
                        stringWriter.WriteLine("No feasible prefixes.");
                    }

                    string feasiblePrefix =
                        stringWriter.ToString().Substring(stringWriter.ToString().IndexOf("Feasible prefixes:"));
                    this.Host.Log.Dump("My Category", "condition", stringWriter.ToString());
                    sb.AppendLine(stringWriter.ToString());

                    int returnIndex = flippedTerm.IndexOf("return");
                    sb.AppendLine("flipped term: " + flippedTerm.Substring(returnIndex));
                    string targetObjectType = null;
                    bool infeasible = false;
                    if (flippedTerm.Contains("!=") && flippedTerm.Contains("null"))
                    {
                        targetObjectType = flippedTerm.Substring(flippedTerm.IndexOf("(") + 1,
                                                                 flippedTerm.IndexOf(")") - flippedTerm.IndexOf("(") -
                                                                 1);
                        sb.AppendLine("targetObjectType: " + targetObjectType);

                        int index = flippedTerm.IndexOf("!=") - 2;
                        int length = 0;
                        while (flippedTerm[index] != ' ')
                        {
                            index--;
                            length++;
                        }
                        string variable = flippedTerm.Substring(index + 1, length);
                        sb.AppendLine("variable for targetObjectType: " + variable);

                        string infeasibleCheck = variable + ".GetType() != typeof(" + targetObjectType + ")";
                        string conflictCheck = variable + " == (" + targetObjectType + ")null";
                        sb.AppendLine("test for infeasible: " + infeasibleCheck);
                        if (feasiblePrefix.Contains(infeasibleCheck))
                        {
                            sb.AppendLine("found infeasible constraint: " + infeasibleCheck);
                            infeasible = true;
                        }
                        else if (feasiblePrefix.Contains(conflictCheck))
                        {
                            sb.AppendLine("found conflict constraint: " + conflictCheck);
                            infeasible = true;
                        }
                        else if (targetObjectType.Contains("(") || targetObjectType.Contains(")") || targetObjectType.Contains("="))
                        {
                            sb.AppendLine("found wrong object type: " + targetObjectType);
                            infeasible = true;
                        }
                        else
                        {
                            var branchInfo = new BranchInfo(sp.Document, sp.Line, sp.Column, sp.EndColumn,
                                                            location.Method.FullName, location.Offset);
                            if (database.TargetObjectTypes.ContainsKey(targetObjectType))
                            {
                                database.TargetObjectTypes[targetObjectType].Add(branchInfo);
                            }
                            else
                            {
                                database.TargetObjectTypes.Add(targetObjectType,
                                                               new HashSet<BranchInfo> {branchInfo});
                            }
                        }
                    }

                    IEnumerable<Field> fields = GetInvolvedFields(e.TermManager, flippedCondition);
                    IEnumerable<TypeEx> types = GetInvolvedObjectTypes(e.TermManager, flippedCondition);
                    var simpleLog = database.SimpleLog;
                    var errorLog = database.ErrorLog;
                    Field target;
                    TypeEx declaringType;
                    simpleLog.AppendLine("============Log Problem================");
                    simpleLog.AppendLine("result: " + e.Result);
                    simpleLog.AppendLine(sb.ToString());

                    TypeEx targetType;
                    if (
                        !ObjectCreationProblemAnalyzer.GetTargetExplorableField(fields.Reverse(), out target,
                                                                              out declaringType,
                                                                              Host, out targetType))
                    {
                        simpleLog.AppendLine("can not analyze");
                    }


                    simpleLog.AppendLine("failed term: \n" + stringWriter.ToString());
                    fields.ToList().ForEach(x => simpleLog.AppendLine("involved field: " + x));
                    foreach (var f in fields)
                    {
                        simpleLog.AppendLine("involved field: ");
                        simpleLog.AppendLine("f.FullName:" + f.FullName);
                        simpleLog.AppendLine("f.Definition.FullName" + f.Definition.FullName);
                        simpleLog.AppendLine("f.InstanceFieldMapType:" + f.InstanceFieldMapType.FullName);
                        TypeEx type;
                        //                        type.
                        f.TryGetDeclaringType(out type);
                        simpleLog.AppendLine("f.TryGetDeclaringType: " + type.FullName);
                    }

                    types.ToList().ForEach(x => simpleLog.AppendLine("found object type: " + x));
                    types.ToList().ForEach(x => Host.GetService<ProblemTrackDatabase>().FoundTypes.Add(x.FullName));
                    fields.ToList().ForEach(x => Host.GetService<ProblemTrackDatabase>().FoundTypes.Add(x.Type.FullName));
                    fields.ToList().ForEach(x =>
                                                {
                                                    TypeEx decType;
                                                    if (x.TryGetDeclaringType(out decType))
                                                    {
                                                        Host.GetService<ProblemTrackDatabase>().FoundTypes.Add(
                                                            decType.FullName);
                                                    }
                                                    ;
                                                });
                    simpleLog.AppendLine("target field: " + target);

                    if (fields != null && fields.Count() > 0)
                    {
                        CreateCandidateObjectCreationProblem(database, location, sp, stringWriter, simpleLog, fields,
                                                           target,
                                                           errorLog, targetType, targetObjectType);
                    }

                    if (fields == null || fields.Count() == 0 && targetObjectType != null && !infeasible)
                    {
                        CreateCandidateObjectCreationProblemForSingleType(stringWriter, sp, location, targetObjectType,
                                                                        database, simpleLog, errorLog);
                    }

                    simpleLog.AppendLine("============end Log Problem================");
                    simpleLog.AppendLine();
                }
                catch (Exception ex)
                {
                    Host.GetService<ProblemTrackDatabase>().ErrorLog.AppendLine("Error in problem observer: " + ex);
                }
//                DumpInfoToDebugFile(sb.ToString(),outputFile);
            }

            private void RecordFlipCount(ProblemEventArgs e)
            {
                CodeLocation location = e.FlippedLocation;
                SequencePoint sp;
                if (Host.Services.SymbolManager.TryGetSequencePoint(location.Method, location.Offset, out sp))
                {
                    Dictionary<BranchInfo, int> branchFlipCounts =
                        Host.GetService<ProblemTrackDatabase>().BranchFlipCounts;
                    //                sb.AppendLine("flipped location: " + sp.Document + " line: " + sp.Line + " offset: " + location.Offset);
                    var info = new BranchInfo(sp.Document, sp.Line, sp.Column, sp.EndColumn, location.Method.FullName,
                                              location.Offset);
                    if (!branchFlipCounts.ContainsKey(info))
                    {
                        branchFlipCounts.Add(info, 0);
                    }
                    branchFlipCounts[info]++;
                }
            }

            private void CreateCandidateObjectCreationProblemForSingleType(StringWriter stringWriter, SequencePoint sp,
                                                                         CodeLocation location, string type,
                                                                         ProblemTrackDatabase database,
                                                                         StringBuilder simpleLog, StringBuilder errorLog)
            {
                try
                {
                    var issue = new CandidateObjectCreationProblem();
                    var fieldInfos = new HashSet<FieldInfo>();


                    issue.InvolvedFields = fieldInfos;

                    issue.DetailDescription = " add for target object type: " + stringWriter.ToString();
                    issue.BranchLocation = new BranchInfo(sp.Document, sp.Line, sp.Column, sp.EndColumn,
                                                          location.Method.FullName,
                                                          location.Offset);
                    issue.TargetType = type;
                    issue.TargetObjectType = type;
                    database.CandidateObjectCreationProblems.Add(issue);
                    simpleLog.AppendLine("add candidate objection issue succeed: " + issue);
                }
                catch (Exception ex)
                {
                    simpleLog.AppendLine("add candidate objection issue faile: ");
                    simpleLog.AppendLine(ex.ToString());
                }
            }

            private IEnumerable<TypeEx> GetInvolvedObjectTypes(TermManager manager, Term term)
            {
                using (var ofc = new ObjectFieldCollector(manager))
                {
                    ofc.VisitTerm(default(TVoid), term);
                    return ofc.Types;
                }
            }

            private void CreateCandidateObjectCreationProblem(ProblemTrackDatabase database, CodeLocation location,
                                                            SequencePoint sp, StringWriter stringWriter,
                                                            StringBuilder simpleLog, IEnumerable<Field> fields,
                                                            Field target, StringBuilder errorLog, TypeEx type,
                                                            string targetObjectType)
            {
                TypeEx declaringType;
                try
                {
                    var issue = new CandidateObjectCreationProblem();
                    var fieldInfos = new HashSet<FieldInfo>();
                    foreach (var field in fields)
                    {
                        if (!field.TryGetDeclaringType(out declaringType))
                        {
                            errorLog.AppendLine("fail to get declaring type of field: " + field.FullName);
                            continue;
                        }
                        fieldInfos.Add(new FieldInfo(field.FullName, field.Type.FullName, declaringType.FullName));
                    }

                    issue.InvolvedFields = fieldInfos;

                    if (target != null)
                    {
                        if (!target.TryGetDeclaringType(out declaringType))
                        {
                            errorLog.AppendLine("fail to get declaring type of field: " + target.FullName);
                        }
                        else
                        {
                            issue.TargetField = new FieldInfo(target.FullName, target.Type.FullName,
                                                              declaringType.FullName);
                        }
                    }

                    issue.DetailDescription = stringWriter.ToString();
                    issue.BranchLocation = new BranchInfo(sp.Document, sp.Line, sp.Column, sp.EndColumn,
                                                          location.Method.FullName,
                                                          location.Offset);
                    issue.TargetType = type != null ? type.FullName : "null";
                    issue.TargetObjectType = targetObjectType;
                    database.CandidateObjectCreationProblems.Add(issue);
                    simpleLog.AppendLine("add candidate objection issue succeed: " + issue);
                }
                catch (Exception ex)
                {
                    simpleLog.AppendLine("add candidate objection issue faile: ");
                    simpleLog.AppendLine(ex.ToString());
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
                    this.Host.Log.Dump("problems", "wrting problem info to " + fileName, e.ToString());
                }
            }

            private IEnumerable<Field> GetInvolvedFields(TermManager termManager, Term t)
            {
                using (var ofc = new ObjectFieldCollector(termManager))
                {
                    ofc.VisitTerm(default(TVoid), t);
                    return ofc.Fields;
                }
            }

            private sealed class ObjectFieldCollector : TermInternalizingRewriter<TVoid>
            {
                public List<Field> Fields = new List<Field>();
                public List<TypeEx> Types = new List<TypeEx>();

                public ObjectFieldCollector(TermManager termManager)
                    : base(termManager, TermInternalizingRewriter<TVoid>.OnCollection.Fail)
                {
                }

                public override Term VisitSymbol(TVoid parameter, Term term, ISymbolId key)
                {
                    Field instanceField;
                    if (this.TermManager.TryGetInstanceField(term, out instanceField))
                        this.Fields.Add(instanceField);


                    TypeEx objectType;
                    if (key is ISymbolIdWithType)
                    {
                        var type = key as ISymbolIdWithType;
                        Types.Add(type.Type);
                    }
                    return base.VisitSymbol(parameter, term, key);
                }
            }
        }
    }
}