using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.Pex.Engine.Explorable;
using Microsoft.ExtendedReflection.ComponentModel;
using PexMe.Core;
using Microsoft.ExtendedReflection.Interpretation.Visitors;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Metadata;
using System.IO;
using Microsoft.ExtendedReflection.Emit;
using Microsoft.ExtendedReflection.Reasoning.ExecutionNodes;
using Microsoft.ExtendedReflection.Utilities.Safe.Text;
using Microsoft.ExtendedReflection.Symbols;
using Microsoft.ExtendedReflection.Coverage;
using Microsoft.ExtendedReflection.Utilities.Safe.IO;
using Microsoft.ExtendedReflection.Logging;
using PexMe.Common;
using Microsoft.Pex.Engine.Search;
using __Substitutions.__Auxiliary;
using PexMe.TermHandler;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using PexMe.ComponentModel;
using PexMe.FactoryRecommender;

namespace PexMe.ObjectFactoryObserver
{    
    internal class InsufficientObjectFactoryObserver
        : PexExplorationComponentBase
        , IPexExplorableInsufficienyObserver
        , IService
    {        
        PexMeDynamicDatabase pmd;
        PexMeStaticDatabase psd;
        TargetBranchAnalyzer tba;
        TypeHintProvider thp;
        
        protected override void Initialize()
        {
            base.Initialize();
            this.pmd = this.GetService<IPexMeDynamicDatabase>() as PexMeDynamicDatabase;
            this.psd = this.GetService<IPexMeStaticDatabase>() as PexMeStaticDatabase;
            this.tba = new TargetBranchAnalyzer(this.pmd, this.Services, this);
            this.ExplorationServices.ExplorableManager.AddExplorableInsufficienyObserver(this);
            
            this.Log.LogMessage("hint provider", "Registered the hint provider");
            this.thp = new TypeHintProvider(this.pmd, this.psd);
            this.ExplorationServices.DomainManager.AddTypeHintProvider(thp);
        }

        #region IPexExplorableInsufficienyObserver Members
        
        /// <summary>
        /// Gets called when an un-explored branch is encountered during program execution
        /// </summary>
        /// <param name="executionNode"></param>
        /// <param name="explorableType"></param>
        public void LogExplorableInsufficiency(IExecutionNode executionNode, TypeEx explorableType)
        {         
            var termManager = this.ExplorationServices.TermManager;         
            var condition = executionNode.SuccessorLabelToExplore;
            this.GatherDebuggingInfoFromInsufficiency(executionNode, termManager, condition, explorableType);
            this.tba.HandleTargetBranch(executionNode.CodeLocation, condition, termManager, explorableType);                      
        }

        private void GatherDebuggingInfoFromInsufficiency(IExecutionNode executionNode, TermManager termManager, 
            Term condition, TypeEx explorableType)
        {         
            var sb = new SafeStringBuilder();
            sb.AppendLine("condition:");
            sb.AppendLine();
            this.tba.ConvertTermToText(new SafeStringWriter(sb), condition, this.ExplorationServices.TermManager);
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
            
            var targetFieldValues = new SafeDictionary<Field, object>();
            Term left, right;
            BinaryOperator binOp;
            if (termManager.TryGetBinary(unnegatedCondition, out binOp, out left, out right))
            {
                sb.AppendFormat("binary relation: {0}", binOp);
                sb.AppendLine();

                if (!termManager.IsValue(left) && !termManager.IsValue(right))
                {
                    sb.AppendLine("No constant on either left side or right side.");
                    return;
                }

                Term non_constant_term = null;
                Term constant_term = null;
                if (termManager.IsValue(left))
                {
                    non_constant_term = right;
                    constant_term = left;
                }
                else if (termManager.IsValue(right))
                {
                    non_constant_term = left;
                    constant_term = right;
                }

                sb.AppendLine("against constant");
                if (constant_term == null || termManager.IsDefaultValue(constant_term))
                {
                    sb.AppendLine("against default value ('null' for references)");
                }

                int value;
                if (constant_term != null && termManager.TryGetI4Constant(constant_term, out value))
                {
                    sb.AppendLine("against integer: " + value);
                }

                Term objectValue;
                ObjectProperty objectProperty;
                if (constant_term != null && termManager.TryGetObjectProperty(constant_term, out objectValue, out objectProperty))
                {
                    sb.AppendLine("against object property: object=" + objectValue + ", property=" + objectProperty);
                }

                sb.AppendLine(" involving fields: ");
                SafeDictionary<Field, FieldValueHolder> innerFieldValues;
                SafeList<TypeEx> innerFieldTypes;
                SafeList<Field> fs = TargetBranchAnalyzer.GetInvolvedFields(this, termManager,
                    non_constant_term, out innerFieldValues, out innerFieldTypes);
                foreach (var f in fs)
                {
                    sb.AppendLine(f.FullName);
                }
            }

            sb.AppendLine("Executed method call sequence");
            if(this.pmd.LastExecutedFactoryMethodCallSequence != null)
                foreach (var m in this.pmd.LastExecutedFactoryMethodCallSequence)
                {
                    sb.AppendLine("\t" + m);
                }

            this.Log.Dump("foo", "insufficiency for " + (explorableType != null ? explorableType.FullName : "?"), sb.ToString());
            return;
        }
        #endregion
        
        public void AfterExecution()
        {
        }
    }
}
