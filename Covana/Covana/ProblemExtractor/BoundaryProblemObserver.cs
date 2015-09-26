using System;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.ExtendedReflection.Coverage;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Metadata.Names;
using Microsoft.ExtendedReflection.Reasoning.ExecutionNodes;
using Microsoft.ExtendedReflection.Symbols;
using Microsoft.ExtendedReflection.Utilities;
using Microsoft.ExtendedReflection.Utilities.Safe.IO;
using Microsoft.Pex.Engine;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.Pex.Engine.Coverage;
using Microsoft.Pex.Engine.ExecutionNodes;
using Microsoft.Pex.Engine.Logging;
using Microsoft.Pex.Engine.Packages;
using Microsoft.Pex.Engine.PathExecution;
using Microsoft.Pex.Framework.ComponentModel;
using Microsoft.Pex.Framework.Packages;

namespace Covana.ProblemExtractor
{
    public class BoundaryProblemObserver:  PexComponentElementDecoratorAttributeBase, IPexExplorationPackage
    {

        private bool debug = true;
        private StringBuilder log;

        private void AppendLine(string text)
        {
            if (debug)
            {
                log.AppendLine(text);
            }
        }

        private void AppendLine()
        {
            if (debug)
            {
                log.AppendLine();
            }
        }


        public void Load(IContainer explorationContainer)
        {
            return;
        }

        public void Initialize(IPexExplorationEngine host)
        {
            return;
        }

        public object BeforeExploration(IPexExplorationComponent host)
        {
            return null;
        }

        public void AfterExploration(IPexExplorationComponent host, object data)
        {
            var graph = host.ExplorationServices.Searcher.ExecutionGraphBuilder as IVisualExecutionGraph;
            log = host.GetService<ProblemTrackDatabase>().SimpleLog;
            AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            foreach (var rootNode in graph.ExecutionRootNodes)
            {
               
                LogRootNode(host, log, rootNode);
            }
            AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            AppendLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
            IFiniteMap<TypeName, StackFrameTree<PexStackFrameTag>.ExceptionNode> exceptions = host.Log.ExceptionFrameTree.GetExceptions();
            foreach (var exception in exceptions)
            {
                AppendLine("TypeName: " + exception.Key);
                IIndexable<PexGeneratedTestName> indexable = exception.Value.Tag.Tests;
                foreach (var name in indexable)
                {
                    AppendLine("ExceptionNode.Tag.Test: id: " + name.ID + " method: "+name.TestMethodName);
                }
               
                AppendLine("ExceptionNode.Tag.ExceptionState: " + exception.Value.Tag.ExceptionState);
            }
            AppendLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
            return;
        }

        private void LogRootNode(IPexExplorationComponent host, StringBuilder log, IExecutionNode rootNode)
        {
            AppendLine("node: ");
            LogNode(rootNode, log,host);
//                host.GetService<IssueTrackDatabase>().
            IFiniteMap<Term, IExecutionNode> successors = rootNode.Successors;
            AppendLine("Successors: ");
            foreach (SafeKeyValuePair<Term, IExecutionNode> keyValuePair in successors)
            {
                AppendLine("term: " + prettyPrintPathCondition(host, new[] {keyValuePair.Key}));
                LogRootNode(host, log, keyValuePair.Value);
            }
        }

        private void LogNode(IExecutionNode rootNode, StringBuilder log, IPexExplorationComponent host)
        {
            var visualExecutionNode = rootNode as IVisualExecutionNode;
            IIndexable<PexPathExecutionResult> indexable = visualExecutionNode.AttachedPathExecutionResults;
            if (rootNode.CodeLocation != null && rootNode.CodeLocation.Method != null)
            {
                AppendLine("node CodeLocation: " + rootNode.CodeLocation.Method.FullName + ":" + rootNode.CodeLocation.Offset); 
            }
            else
            {
                AppendLine("node CodeLocation: " + rootNode.CodeLocation);
            }

            if (rootNode.InCodeBranch != null && rootNode.InCodeBranch.Method != null)
            {
                CodeLocation location = rootNode.InCodeBranch.Method.GetBranchLabelSource(rootNode.InCodeBranch.BranchLabel);
                AppendLine("node InCodeBranch: " + location.Method.FullName + ":" + location.Offset.ToString("x") + " out: " + rootNode.InCodeBranch.BranchLabel);
                AppendLine("node InCodeBranch: " + rootNode.InCodeBranch);
            }
            else
            {
                AppendLine("node InCodeBranch: " + rootNode.InCodeBranch);
            }
           
            AppendLine("node Pathcondition: ");
            foreach (var term in rootNode.GetPathCondition().Conjuncts)
            {
                AppendLine(prettyPrintPathCondition(host, new[]{term}));
            }
           
            
            AppendLine("node OutCodeBranches: ");
            foreach (CodeBranch branch in rootNode.OutCodeBranches)
            {
                if (branch != null && branch.Method != null)
                {
                    CodeLocation location = branch.Method.GetBranchLabelSource(branch.BranchLabel);
                    AppendLine("node OutCodeBranch: " + location.Method.FullName + ":" + location.Offset.ToString("x") + " out: " + branch.BranchLabel);
                }
                else
                {
                    AppendLine("node OutCodeBranch: " + branch);
                }
            }
            foreach (PexPathExecutionResult result in indexable)
            {
                AppendLine("node result: " + result.Kind);
            }
            AppendLine("node Pathcondition: " + rootNode.ModelHints);
            AppendLine();
        }

        public string Name
        {
            get { return "BoundaryProblemObserver"; }
        }

        private static string prettyPrintPathCondition(IPexExplorationComponent host, Term[] pathConditions)
        {
            var writer = new SafeStringWriter();
            var codeWriter = host.Services.TestManager.Language.CreateBodyWriter(
                writer,
                VisibilityContext.Private,
                100);
            var emitter = new TermEmitter(
                host.ExplorationServices.TermManager,
                new NameCreator());
            for (int i = pathConditions.Length - 1; i >= 0; i--)
            {
                var pathCondition = pathConditions[i];
                if (!emitter.TryEvaluate(
                         new Term[] { pathCondition },
                         10000, // bound on size of expression we are going to pretty-print
                         codeWriter))
                {
                    writer.WriteLine("(expression too big; consider using environment variable {0})",
                                     ExtendedReflectionEnvironmentSettings.NoCodeEmitterSizeLimit.Name);
                }
                else
                {
                    codeWriter.Return(SystemTypes.Bool);
                }
            }
            return writer.ToString();
        }

        protected override void Decorate(Name location, IPexDecoratedComponentElement host)
        {
            host.AddExplorationPackage(location, this);
        }
    }
}