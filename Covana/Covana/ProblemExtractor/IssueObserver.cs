using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Emit;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Metadata.Names;
using Microsoft.ExtendedReflection.Symbols;
using Microsoft.ExtendedReflection.Utilities;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.Pex.Engine.Logging;
using Microsoft.Pex.Framework.Packages;

namespace Covana.ProblemExtractor
{
    [Serializable]
    public class BoundaryProblem
    {
        public string Kind;
        public string TargetName;
        public string Message;
        public BranchInfo FlippedLocation;
        public string FlippedCondition;

        public BoundaryProblem(string kind, string targetName, string message, BranchInfo flippedLocation, string flippedCondition)
        {
            Kind = kind;
            TargetName = targetName;
            Message = message;
            FlippedLocation = flippedLocation;
            FlippedCondition = flippedCondition;
        }

        public bool Equals(BoundaryProblem other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Kind, Kind) && Equals(other.TargetName, TargetName) && Equals(other.Message, Message) && Equals(other.FlippedLocation, FlippedLocation) && Equals(other.FlippedCondition, FlippedCondition);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (BoundaryProblem)) return false;
            return Equals((BoundaryProblem) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (Kind != null ? Kind.GetHashCode() : 0);
                result = (result*397) ^ (TargetName != null ? TargetName.GetHashCode() : 0);
                result = (result*397) ^ (Message != null ? Message.GetHashCode() : 0);
                result = (result*397) ^ (FlippedLocation != null ? FlippedLocation.GetHashCode() : 0);
                result = (result*397) ^ (FlippedCondition != null ? FlippedCondition.GetHashCode() : 0);
                return result;
            }
        }

        public override string ToString()
        {
            return "Boundary isse kind: " + Kind + " target " + TargetName + " message: " + Message + " \nlocation: " + FlippedLocation + "\n" +
                   "flipped condition: " + FlippedCondition;
        }
    }

    public class IssueObserverAttribute :
        PexExecutionPackageAttributeBase
    {
        private IPexComponent Host;

        protected override object BeforeExecution(IPexComponent host)
        {
            Host = host;
            host.Log.ExplorableHandler += Log_ExplorableHandler;
            host.Log.UninstrumentedMethodHandler += Log_UninstrumentedMethodHandler;
            host.Log.ExplorationBoundaryHandler += new RemoteEventHandler<ExplorationBoundaryEventArgs>(Log_ExplorationBoundaryHandler);
            return null;
        }

        

        void Log_ExplorationBoundaryHandler(ExplorationBoundaryEventArgs e)
        {
            var database = Host.GetService<ProblemTrackDatabase>();
            ProblemEventArgs successfulFlippedPathCondition = database.CurrentSuccessfulFlippedPathCondition;
            StringBuilder log = database.SimpleLog;

            
            SequencePoint sp;
            if (successfulFlippedPathCondition == null)
            {
                return;
            }
            CodeLocation location = successfulFlippedPathCondition.FlippedLocation;
            Host.Services.SymbolManager.TryGetSequencePoint(location.Method, location.Offset, out sp);
            StringBuilder sb = new StringBuilder("/////////////////////////////////// \n");
            log.AppendLine("exception: " + e.Kind + " e.TargetName: " + e.TargetName + " message: " + e);
            sb.AppendLine("flipped location: " + sp.Document + " line: " + sp.Line);
            var branchInfo = new BranchInfo("",0,0,0,"",0);
            try
            {
                branchInfo = new BranchInfo(sp.Document, sp.Line, sp.Column, sp.EndColumn, location.Method.FullName, location.Offset);
            }
            catch (Exception)
            {
                
                
            }
            
            
            var flippedCondition = successfulFlippedPathCondition.Suffix;
            var stringWriter = new StringWriter();
            var bodyWriter = this.Host.Services.LanguageManager.DefaultLanguage.CreateBodyWriter(stringWriter,
                                                                                                 VisibilityContext.
                                                                                                     Private);
            var emitter = new TermEmitter(successfulFlippedPathCondition.TermManager, new NameCreator());
            if (emitter.TryEvaluate(Indexable.One(flippedCondition), 1000, bodyWriter))
            {
                bodyWriter.Return(SystemTypes.Bool);
            }

            stringWriter.WriteLine();
            stringWriter.WriteLine("Feasible prefixes:");
            if (successfulFlippedPathCondition.FeasiblePrefix != null && successfulFlippedPathCondition.FeasiblePrefix.Length > 0)
            {
                var bodyWriter2 = this.Host.Services.LanguageManager.DefaultLanguage.CreateBodyWriter(stringWriter,
                                                                                                VisibilityContext.
                                                                                                    Private);
                foreach (Term prefix in successfulFlippedPathCondition.FeasiblePrefix)
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

            this.Host.Log.Dump("My Category", "condition", stringWriter.ToString());
            sb.AppendLine(stringWriter.ToString());

            sb.AppendLine("///////////////////////////////////");
            var issue = new BoundaryProblem(e.Kind.ToString(), e.TargetName.ToString(), e.Message, branchInfo, stringWriter.ToString());
            Host.GetService<ProblemTrackDatabase>().BoundaryIssues.Add(issue);
            log.AppendLine(sb.ToString());
//            e.
        }

        private void Log_UninstrumentedMethodHandler(UninstrumentedMethodEventArgs e)
        {
            var uninstrumentedMethods = Host.GetService<ProblemTrackDatabase>().ExternalMethods;
            
            UninstrumentedMethod method = e.UninstrumentedMethod;
            UninstrumentedMethodFilterResult result;
            e.UninstrumentedMethod.TryGetFilterResult(out result);
            uninstrumentedMethods.Add(method);
        }

        protected override sealed void AfterExecution(IPexComponent host, object data)
        {
        }

        private void Log_ExplorableHandler(PexExplorableEventArgs e)
        {
            var objectIssueDictionary = Host.GetService<ProblemTrackDatabase>().ObjectCreationIssueDictionary;

            if (!objectIssueDictionary.ContainsKey(e.Kind))
            {
                objectIssueDictionary.Add(e.Kind, new SafeSet<TypeName>());
            }

            var set = objectIssueDictionary[e.Kind];
            set.Add(e.ExplorableType);
        }
    }
}