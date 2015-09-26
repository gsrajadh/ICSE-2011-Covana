using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.Pex.Framework.ComponentModel;
using Microsoft.Pex.Engine.Packages;
using Microsoft.Pex.Engine.Explorable;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Reasoning.ExecutionNodes;
using Microsoft.ExtendedReflection.Metadata.Names;
using Microsoft.ExtendedReflection.Utilities.Safe.Text;
using Microsoft.ExtendedReflection.Emit;
using System.IO;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Utilities.Safe.IO;
using Microsoft.ExtendedReflection.Interpretation.Visitors;
using Microsoft.ExtendedReflection.Metadata.Interfaces;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Coverage;
using System.Runtime.Serialization.Formatters.Binary;
using Covana;
using Microsoft.ExtendedReflection.Symbols;


namespace FieldAccessExtractor
{
    internal class InsufficientObjectFactoryObserver
        : PexExplorationComponentBase
          , IPexExplorableInsufficienyObserver
          , IService
    {
        private InsufficientObjectFactoryFieldInfo InsufficientObjectFactoryFieldInfoObj;
        private SeqexDatabase database;

        //SafeSet<CodeLocation> unsuccessfullyFlippedCodeLocations = new SafeSet<CodeLocation>();

        protected override void Initialize()
        {
            base.Initialize();
            database = this.GetService<SeqexDatabase>();
            /*if (database == null)
                database = new SeqexDatabase();*/
            InsufficientObjectFactoryFieldInfoObj = database.InsufficientObjectFactoryFieldInfoObj;
            this.ExplorationServices.ExplorableManager.AddExplorableInsufficienyObserver(this);
        }

        private sealed class ObjectFieldCollector : TermInternalizingRewriter<TVoid>
        {
            public SafeSet<Field> Fields = new SafeSet<Field>();

            public ObjectFieldCollector(TermManager termManager)
                : base(termManager, TermInternalizingRewriter<TVoid>.OnCollection.Fail)
            {
            }

            public override Term VisitSymbol(TVoid parameter, Term term, ISymbolId key)
            {
                Field instanceField;
                if (this.TermManager.TryGetInstanceField(term, out instanceField))
                    this.Fields.Add(instanceField);
                return base.VisitSymbol(parameter, term, key);
            }
        }

        #region IPexExplorableInsufficienyObserver Members

        private void dumpHelper(TextWriter writer, Term term)
        {
            var termManager = this.ExplorationServices.TermManager;
            var emitter = new TermEmitter(
                termManager,
                new NameCreator());

            IMethodBodyWriter codeWriter = this.Services.TestManager.Language.CreateBodyWriter(
                writer,
                VisibilityContext.Private,
                100);

            if (!emitter.TryEvaluate(
                     new Term[] {term},
                     10000, // bound on size of expression we are going to pretty-print
                     codeWriter))
            {
                writer.WriteLine("expression too big");
                return;
            }

            codeWriter.Return(SystemTypes.Bool);
        }

        public void LogExplorableInsufficiency(IExecutionNode executionNode, TypeEx explorableType)
        {
            var termManager = this.ExplorationServices.TermManager;
            var accessedFields = new SafeSet<Field>();

            var condition = executionNode.SuccessorLabelToExplore;
            SafeStringBuilder sb = new SafeStringBuilder();
            sb.AppendLine("condition:");
            sb.AppendLine();
            dumpHelper(new SafeStringWriter(sb), condition);
            sb.AppendLine();
            var swriter = new TermSExpWriter(termManager, new SafeStringWriter(sb), true, false);
            swriter.Write(condition);
            sb.AppendLine();
            sb.AppendLine("location:");
            sb.AppendLine();
            sb.AppendLine(executionNode.CodeLocation.ToString());
            sb.AppendLine();
            sb.AppendLine("properties:");
            Term unnegatedCondition;
            if (termManager.TryGetInnerLogicallyNegatedValue(condition, out unnegatedCondition))
                sb.AppendLine("negated");
            else
                unnegatedCondition = condition;
            Term left, right;
            BinaryOperator binOp;
            if (termManager.TryGetBinary(unnegatedCondition, out binOp, out left, out right))
            {
                sb.AppendFormat("binary relation: {0}", binOp);
                sb.AppendLine();
                // binOp==BinaryOperator.Ceq  tests for equality
                bool isLeftValue = termManager.IsValue(left);

                if (termManager.IsValue(left) || termManager.IsValue(right))
                {
                    sb.AppendLine("against constant");
                    if (termManager.IsDefaultValue(left) || termManager.IsDefaultValue(right))
                    {
                        sb.AppendLine("against default value ('null' for references)");
                    }
                    int value;
                    if (termManager.TryGetI4Constant(left, out value) || termManager.TryGetI4Constant(right, out value))
                    {
                        sb.AppendLine("against integer: " + value);
                    }
                    Term objectValue;
                    ObjectProperty objectProperty;
                    if (termManager.TryGetObjectProperty(left, out objectValue, out objectProperty) ||
                        termManager.TryGetObjectProperty(right, out objectValue, out objectProperty))
                    {
                        sb.AppendLine("against object property: object=" + objectValue + ", property=" + objectProperty);
                    }
                    //the following determins what fields are involved on the other side of the constraint
                    Term t;
                    if (isLeftValue)
                        t = right;
                    else
                        t = left;
                    sb.AppendLine(" involving fields: ");
                    SafeSet<Field> fs = this.GetInvolvedFields(termManager, t);
                    foreach (var f in fs)
                    {
                        sb.AppendLine("f.FullName:" + f.FullName);
                        sb.AppendLine("f.Definition.FullName" + f.Definition.FullName);
                        sb.AppendLine("f.InstanceFieldMapType:" + f.InstanceFieldMapType.FullName);
                        TypeEx type;
//                        type.
                        f.TryGetDeclaringType(out type);
                        sb.AppendLine("f.TryGetDeclaringType: " + type.FullName);
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("fields:");
            sb.AppendLine();

            SafeSet<Field> fields = this.GetInvolvedFields(termManager, condition);
            foreach (var f in fields)
            {
                sb.AppendLine(f.FullName);
                accessedFields.Add(f);
            }

            this.Log.Dump("foo", "insufficiency for " + (explorableType != null ? explorableType.FullName : "?"),
                          sb.ToString());
            StringBuilder simpleLog = this.GetService<ProblemTrackDatabase>().SimpleLog;
            simpleLog.AppendLine("insufficiency for " + (explorableType != null ? explorableType.FullName : "?") + sb);
            //this.unsuccessfullyFlippedCodeLocations.Add(executionNode.CodeLocation);

            SafeSet<Field> existingAssessedFields;
            if (
                !this.database.FieldsForUnsuccessfullyFlippedCodeLocations.TryGetValue(executionNode.CodeLocation,
                                                                                       out existingAssessedFields))
            {
                database.FieldsForUnsuccessfullyFlippedCodeLocations.Add(executionNode.CodeLocation, accessedFields);
            }
            else
            {
                existingAssessedFields.AddRange(accessedFields);
            }

            SafeStringBuilder sbTerm = new SafeStringBuilder();
            dumpHelper(new SafeStringWriter(sbTerm), condition);

            SafeSet<string> terms;
            if (
                !this.database.TermsForUnsuccessfullyFlippedCodeLocations.TryGetValue(
                     executionNode.CodeLocation.ToString(), out terms))
            {
                terms = new SafeSet<string>();
                terms.Add(sbTerm.ToString());
                database.TermsForUnsuccessfullyFlippedCodeLocations.Add(executionNode.CodeLocation.ToString(), terms);
            }
            else
            {
                terms.Add(sbTerm.ToString());
            }
        }

        private SafeSet<Field> GetInvolvedFields(TermManager termManager, Term t)
        {
            using (var ofc = new ObjectFieldCollector(termManager))
            {
                ofc.VisitTerm(default(TVoid), t);
                return ofc.Fields;
            }
        }

        #endregion

        public void Dump()
        {
            //aggregate the coverage 
            database.AccumulateMaxCoverage(this.ExplorationServices.Driver.TotalCoverageBuilder);

            //for debugging purpose in report
            SafeStringBuilder sb = new SafeStringBuilder();
            HashSet<CodeLocation> coveredLocations = new HashSet<CodeLocation>();
            foreach (var cl in database.FieldsForUnsuccessfullyFlippedCodeLocations.Keys)
            {
                sb.AppendLine("about code location" + cl.ToString());
                MethodDefinitionBodyInstrumentationInfo info;
                if (cl.Method.TryGetBodyInstrumentationInfo(out info))
                {
                    ISymbolManager sm = this.Services.SymbolManager;
                    SequencePoint sp;
                    sm.TryGetSequencePoint(null, 0, out sp);

                    int coveredBranchesCount = 0;
                    foreach (var outgoingBranchLabel in info.GetOutgoingBranchLabels(cl.Offset))
                    {
                        CodeBranch codeBranch = new CodeBranch(cl.Method, outgoingBranchLabel);
                        if (!codeBranch.IsBranch) // is explicit branch?
                        {
                            coveredBranchesCount = 2; // if not, pretend we covered it
                            continue;
                        }
                        CoverageDomain domain;
                        int[] hits;
                        if (
                            this.ExplorationServices.Driver.TotalCoverageBuilder.TryGetMethodHits(cl.Method, out domain,
                                                                                                  out hits) &&
                            outgoingBranchLabel < hits.Length &&
                            hits[outgoingBranchLabel] > 0)
                            //we have branches not being covered for the code location
                        {
                            sb.AppendLine("  outgoing branch " + outgoingBranchLabel + " hit");
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
                database.FieldsForUnsuccessfullyFlippedCodeLocations.Remove(cl);
            }

            this.Log.Dump("foo", "insufficiency summary", sb.ToString());
            //database.DumpInSufficientObjectFactoryFieldInfoIToFile();
        }
    }

    public sealed class InsufficientObjectFactoryObserverAttribute
        : PexComponentElementDecoratorAttributeBase
          , IPexExplorationPackage
    {
        /// <summary>
        /// Gets the name of this package.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return "InsufficientObjectFactoryObserver"; }
        }

        protected override sealed void Decorate(Name location, IPexDecoratedComponentElement host)
        {
            host.AddExplorationPackage(location, this);
        }

        #region IPexExplorationPackage Members

        void IPexExplorationPackage.Load(
            IContainer explorationContainer)
        {
            explorationContainer.AddComponent(
                "InsufficientObjectFactoryObserver",
                new InsufficientObjectFactoryObserver());
        }

        void IPexExplorationPackage.Initialize(
            IPexExplorationEngine host)
        {
            var observer =
                ServiceProviderHelper.GetService<InsufficientObjectFactoryObserver>(host);
        }

        object IPexExplorationPackage.BeforeExploration(
            IPexExplorationComponent host)
        {
            return null;
        }

        void IPexExplorationPackage.AfterExploration(
            IPexExplorationComponent host,
            object data)
        {
            var observer =
                ServiceProviderHelper.GetService<InsufficientObjectFactoryObserver>(host.Site);
            observer.Dump();
            
        }

        #endregion
    }
}