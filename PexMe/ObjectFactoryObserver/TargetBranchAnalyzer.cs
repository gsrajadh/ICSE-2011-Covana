using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PexMe.Core;
using Microsoft.ExtendedReflection.Interpretation;
using System.IO;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Emit;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Utilities.Safe.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using PexMe.Common;
using Microsoft.ExtendedReflection.Interpretation.Visitors;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Utilities.Safe.IO;
using PexMe.ComponentModel;
using PexMe.TermHandler;
using System.Collections.ObjectModel;

namespace PexMe.ObjectFactoryObserver
{
    public class TargetBranchAnalyzer
    {
        PexMeDynamicDatabase pmd;
        IPexComponent host;        
        IPexComponentServices services;
        IPexExplorationComponent explorationComponent;

        public TargetBranchAnalyzer(PexMeDynamicDatabase pmd, IPexComponentServices services, IPexExplorationComponent explorationComponent)
        {
            this.pmd = pmd;
            this.host = pmd;            
            this.services = services;
            this.explorationComponent = explorationComponent;
        }

        public void ConvertTermToText(TextWriter writer, Term term, TermManager termManager)
        {            
            var emitter = new TermEmitter(termManager, new NameCreator());

            IMethodBodyWriter codeWriter = this.services.TestManager.Language.CreateBodyWriter(
                writer,
                VisibilityContext.Private,
                100);

            if (!emitter.TryEvaluate(
                new Term[] { term },
                10000, // bound on size of expression we are going to pretty-print
                codeWriter))
            {
                writer.WriteLine("expression too big");
                return;
            }

            codeWriter.Return(SystemTypes.Bool);
        }

        /// <summary>
        /// Gets called when an un-explored branch is encountered during program execution
        /// </summary>
        /// <param name="executionNode"></param>
        /// <param name="explorableType"></param>
        public void HandleTargetBranch(CodeLocation location, Term condition, TermManager termManager, TypeEx explorableType)
        {            
            var accessedFields = new SafeList<Field>();

            if (PexMeConstants.IGNORE_UNCOV_BRANCH_IN_SYSTEM_LIB)
            {
                if(IsUncoveredLocationInSystemLib(location))
                {
                    this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "uncoveredlocation",
                        "Ignoring the uncovered location " + location.ToString() + ", since it is in system library");
                    return;
                }
            }

            Term unnegatedCondition;
            bool bNegated = false;
            if (termManager.TryGetInnerLogicallyNegatedValue(condition, out unnegatedCondition))
                bNegated = true;
            else
                unnegatedCondition = condition;

            var culpritFields = new SafeList<Field>();
            Term left, right;
            BinaryOperator binOp;

            SafeStringBuilder sbTerm = new SafeStringBuilder();
            this.ConvertTermToText(new SafeStringWriter(sbTerm), condition, termManager);

            //Handling only binary conditions. TODO: Needs to check what are the other conditions
            //The related code is in TermSolver function
            if (!termManager.TryGetBinary(unnegatedCondition, out binOp, out left, out right))
            {
                this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, PexMeLogCategories.Term,
                    "Handling only binary operations in terms");
                return;
            }

            if (PexMeConstants.USE_TERM_SOLVER)
            {
                //TODO: Temporarily ignoring the scenario where both the sides are symbolic values
                if (!termManager.IsValue(left) && !termManager.IsValue(right))
                {
                    this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, PexMeLogCategories.Term,
                       "Handling only binary operations where atleast one side of the condition is concrete. Current expression has both sides symbolic");
                    return;
                }

                SafeDictionary<Field, FieldValueHolder> expectedFieldValues;
                SafeDictionary<Field, FieldValueHolder> actualFieldValues;
                SafeList<Field> allFieldsInCondition;
                SafeList<TypeEx> allFieldTypes;
                TermSolver.SolveTerm(this.explorationComponent, condition, binOp, 
                    out actualFieldValues, out expectedFieldValues, out allFieldsInCondition, out allFieldTypes);

                //Compute an intersection to identify culprit fields
                List<Field> actualKeys = actualFieldValues.Keys.ToList();
                List<Field> expectedKeys = expectedFieldValues.Keys.ToList();

                AddToCulpritField(allFieldsInCondition, culpritFields);
                if (culpritFields.Count == 0)
                {
                    this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, PexMeLogCategories.Term,
                        "Failed to retrieve culprit fields from the uncovered branch");
                }
                else
                {
                    foreach (Field field in culpritFields)
                    {
                        FieldModificationType fieldfmt;
                        int fitnessval;
                        FitnessMeasure.ComputeFitnessValue(field, actualFieldValues[field], expectedFieldValues[field], this.host, out fieldfmt, out fitnessval);

                        if (fieldfmt == FieldModificationType.UNKNOWN)
                            continue;
                        this.pmd.AddFieldsOfUncoveredCodeLocations(location, allFieldsInCondition, fieldfmt,
                            condition, sbTerm.ToString(), fitnessval, explorableType, allFieldTypes);
                    }
                }
            }
            else
            {
                FieldModificationType fmt;
                if (!termManager.IsValue(left) && !termManager.IsValue(right))
                {
                    SafeDictionary<Field, FieldValueHolder> leftFieldValues;
                    SafeList<TypeEx> leftFieldTypes;
                    var leftAccessedFields = GetInvolvedFields(this.host, termManager, left, out leftFieldValues, out leftFieldTypes);
                    if (leftAccessedFields.Count > 0)
                        AddToCulpritField(leftAccessedFields, culpritFields);

                    SafeDictionary<Field, FieldValueHolder> rightFieldValues;
                    SafeList<TypeEx> rightFieldTypes;
                    var rightAccessedFields = GetInvolvedFields(this.host, termManager, right, out rightFieldValues, out rightFieldTypes);
                    if (rightAccessedFields.Count > 0)
                        AddToCulpritField(rightAccessedFields, culpritFields);

                    int fitnessval;
                    this.handleNoConstantsInTerm(termManager, left, right, binOp, bNegated, 
                        culpritFields, unnegatedCondition, out fmt, out fitnessval);

                    //TODO: fitnessval can be different from left and right handside terms. Needs to deal with this later
                    this.pmd.AddFieldsOfUncoveredCodeLocations(location, leftAccessedFields,
                        fmt, condition, sbTerm.ToString(), fitnessval, explorableType, leftFieldTypes);
                    this.pmd.AddFieldsOfUncoveredCodeLocations(location, rightAccessedFields,
                        fmt, condition, sbTerm.ToString(), fitnessval, explorableType, rightFieldTypes);
                }
                else
                {
                    Term non_constant_term = null;
                    if (termManager.IsValue(left))
                        non_constant_term = right;
                    else if (termManager.IsValue(right))
                        non_constant_term = left;
                    else
                        SafeDebug.AssumeNotNull(null, "Control should not come here!!!");


                    //Get accessed fields in the uncovered branching condition
                    SafeDictionary<Field, FieldValueHolder> fieldValues;
                    SafeList<TypeEx> fieldTypes;
                    accessedFields = GetInvolvedFields(this.host, termManager, non_constant_term, out fieldValues, out fieldTypes);
                    if (accessedFields.Count != 0)
                        AddToCulpritField(accessedFields, culpritFields);

                    if (culpritFields.Count == 0)
                    {
                        this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, PexMeLogCategories.Term,
                            "Failed to retrieve culprit fields from the uncovered branch");
                    }
                    else
                    {
                        int fitnessval;
                        this.handleAConstantInTerm(termManager, left, right, binOp, bNegated, fieldValues, culpritFields[0], out fmt, out fitnessval);
                        this.pmd.AddFieldsOfUncoveredCodeLocations(location, accessedFields,
                            fmt, condition, sbTerm.ToString(), fitnessval, explorableType, fieldTypes);
                    }
                }
            }
        }

        public static bool IsUncoveredLocationInSystemLib(CodeLocation location)
        {
            //Get the position of uncovered location. If it is in system libraries,
            //Do not register the location
            try
            {
                var loc_assemblyname = location.Method.Module.Assembly.ShortName;
                if (PexMeConstants.SystemLibraries.Contains(loc_assemblyname))
                {                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                //Ignore any exception caused by this check. Should not happen in general
                //this.host.Log.LogWarningFromException(ex, WikiTopics.MissingWikiTopic,
                //    "InsufficientObjectFactory", "Codelocation empty in executionnode");
            }
            return false;
        }

        private static void AddToCulpritField(IEnumerable<Field> accessedFields, SafeList<Field> culpritFields)
        {
            foreach (var acfield in accessedFields)
            {
                if (PexMeFilter.IsTypeSupported(acfield.Type))
                {
                    culpritFields.Add(acfield);                    
                }
            }
        }

        /// <summary>
        /// OBSOLETE: Handles a scenario where there is a term in the condition
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="binOp"></param>
        /// <param name="fmt"></param>
        private void handleAConstantInTerm(TermManager termManager, Term left, Term right,
            BinaryOperator binOp, bool bNegated, SafeDictionary<Field, FieldValueHolder> fieldValues, Field culpritField,
            out FieldModificationType fmt, out int fitnessval)
        {
            fitnessval = Int32.MaxValue;
            fmt = FieldModificationType.UNKNOWN;
            Term non_constant_term = null;
            Term constant_term = null;

            bool bleftRightMaintainted = true;
            if (termManager.IsValue(left))
            {
                non_constant_term = right;
                constant_term = left;
                bleftRightMaintainted = true;
            }
            else if (termManager.IsValue(right))
            {
                non_constant_term = left;
                constant_term = right;
                bleftRightMaintainted = false;
            }

            int value;
            if (termManager.TryGetI4Constant(constant_term, out value))
            {                
                fmt = FieldModificationType.INCREMENT;

                FieldValueHolder fvh;
                if (fieldValues.TryGetValue(culpritField, out fvh))
                {
                    int non_constant_field_value = fvh.intValue;    //TODO: Assuming that the fieldType is Int32
                    if (bleftRightMaintainted)
                        fitnessval = FitnessMeasure.ComputeFitnessValue(this.host, binOp, value, non_constant_field_value, bNegated);
                    else
                        fitnessval = FitnessMeasure.ComputeFitnessValue(this.host, binOp, non_constant_field_value, value, bNegated);
                }
                else
                    this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "fitness measure",
                        "Failed to retrieve value for field " + culpritField.ToString());
            }
            else if (termManager.IsDefaultValue(constant_term))
            {
                if (binOp == BinaryOperator.Ceq)
                {
                    if (culpritField.Type.ToString() == "System.Boolean")
                    {
                        fmt = bNegated ? FieldModificationType.TRUE_SET : FieldModificationType.FALSE_SET;
                    }
                    else
                        fmt = bNegated ? FieldModificationType.NON_NULL_SET : FieldModificationType.NULL_SET;
                }
            }

            Term objectValue;
            ObjectProperty objectProperty;
            if (termManager.TryGetObjectProperty(constant_term, out objectValue, out objectProperty))
            {
                //TODO??? How to handle this scenario?
            }
        }

        /// <summary>
        /// OBSOLETE:
        /// </summary>
        /// <param name="termManager"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="binOp"></param>
        /// <param name="bNegated"></param>
        /// <param name="culpritFields"></param>
        /// <param name="completeTerm"></param>
        /// <param name="fmt"></param>
        /// <param name="fitnessval"></param>
        private void handleNoConstantsInTerm(TermManager termManager, Term left, Term right, BinaryOperator binOp,
            bool bNegated, SafeList<Field> culpritFields, Term completeTerm, out FieldModificationType fmt, out int fitnessval)
        {
            fmt = FieldModificationType.UNKNOWN;
            //Term termUnderAnalysis = null;
            //Term otherTerm = null;

            //Field instanceField;
            //if (termManager.TryGetInstanceField(left, out instanceField) && culpritFields.Contains(instanceField))
            //{
            //    termUnderAnalysis = left;
            //    otherTerm = right;
            //}
            //else
            //{
            //    if (termManager.TryGetInstanceField(right, out instanceField))
            //        if (culpritFields.Contains(instanceField))
            //        {
            //            termUnderAnalysis = right;
            //            otherTerm = left;
            //        }
            //}

            //if (termUnderAnalysis == null)
            //    return;

            //object value;
            //if (termManager.TryGetObject(left, out value))
            //{
            //    if (value == null && binOp == BinaryOperator.Ceq)
            //    {
            //        fmt = bNegated ? FieldModificationType.NON_NULL_SET : FieldModificationType.NULL_SET;
            //    }
            //    else if (value is int || value is Int16 || value is Int32 || value is Int64)
            //    {
            //        fmt = FieldModificationType.INCREMENT; //TODO: Needs to get actual values and decide based on that
            //    }
            //} else if (termManager.TryGetObject(right, out value))
            //{
            //    if (value == null && binOp == BinaryOperator.Ceq)
            //    {
            //        fmt = bNegated ? FieldModificationType.NON_NULL_SET : FieldModificationType.NULL_SET;                    
            //    }
            //    else if (value is int || value is Int16 || value is Int32 || value is Int64)
            //    {
            //        fmt = FieldModificationType.INCREMENT; //TODO: Needs to get actual values and decide based on that
            //    }
            //}                       

            //TODO: A worst fix to proceed further                        
            fitnessval = Int32.MaxValue;
            if (culpritFields.Count == 0)
                return;            
            var culprittype = culpritFields[0].Type;
            if (culprittype.IsReferenceType)
                fmt = FieldModificationType.NON_NULL_SET;
            else
            {
                var typestr = culprittype.ToString();
                if (typestr == "System.Boolean")
                    fmt = FieldModificationType.TRUE_SET;
                else if (typestr == "System.Int32" || typestr == "System.Int64" || typestr == "System.Int16")
                {
                    SafeDictionary<Field, FieldValueHolder> fieldValues;
                    //TermSolver.SolveTerm(this.ter, completeTerm, out fieldValues);                    
                    fmt = FieldModificationType.INCREMENT;
                }
            }
        }        

        public static SafeList<Field> GetInvolvedFields(IPexComponent host, TermManager termManager, Term t,
            out SafeDictionary<Field, FieldValueHolder> fieldValues, out SafeList<TypeEx> allFieldTypes)
        {
            using (var ofc = new ObjectFieldCollector(host, termManager))
            {
                ofc.VisitTerm(default(TVoid), t);
                fieldValues = ofc.FieldValues;
                allFieldTypes = ofc.Types;
                return ofc.Fields;
            }
        }

        public static SafeList<TypeEx> GetInvolvedTypes(IPexComponent host, TermManager termManager, Term t)
        {
            using (var ofc = new ObjectFieldCollector(host, termManager))
            {
                ofc.VisitTerm(default(TVoid), t);
                return ofc.Types;
            }
        }
    }
}
