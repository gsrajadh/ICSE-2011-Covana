using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Interpretation.Visitors;
using Microsoft.ExtendedReflection.Utilities.Safe.IO;
using Microsoft.ExtendedReflection.Reasoning;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Utilities;
using PexMe.Common;
using Microsoft.ExtendedReflection.Logging;

namespace PexMe.TermHandler
{
    /// <summary>
    /// A solver for terms. This is a base for computing actual and expected field values.
    /// </summary>
    internal class TermSolver
    {
        /// <summary>
        /// Solves a term and computes the actual and expected values of fields. There are various possibilites of the term. The actual
        /// fiels in a condition, returned by this function should be only from one side.
        /// 
        /// a. One side of the term is concrete and not symbolic. It is easy to handle this case as one side is concrete
        /// b. Both the sides are symbolic. Pex assigns different values to both the sides, since both are symbolic, but this is not possible
        ///     in the context of ours. So, we treat one side as a concrete and other side as symbolic. We use heuristics in deciding which
        ///     side is concrete and which side is symbolic.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="condition"></param>
        /// <param name="binOp"></param>
        /// <param name="actualFieldValues"></param>
        /// <param name="expectedFieldValues"></param>
        /// <param name="allFieldsInCondition"></param>
        /// <returns></returns>
        public static bool SolveTerm(IPexExplorationComponent host, Term condition, BinaryOperator binOp,
            out SafeDictionary<Field, FieldValueHolder> actualFieldValues, 
            out SafeDictionary<Field, FieldValueHolder> expectedFieldValues,
            out SafeList<Field> allFieldsInCondition, out SafeList<TypeEx> allFieldTypes)
        {
            var termManager = host.ExplorationServices.TermManager;
            var solverFactory = host.ExplorationServices.SolverFactory;
            var solverStatistics = solverFactory.CreateStatistics();

            actualFieldValues = new SafeDictionary<Field, FieldValueHolder>();
            expectedFieldValues = new SafeDictionary<Field, FieldValueHolder>();
            allFieldTypes = new SafeList<TypeEx>();

            //Simplifies term based on the contents within the term. gathering a simplied form of condition
            condition = SimplifyTerm(host, termManager, condition);

            allFieldsInCondition = new SafeList<Field>();
            var variables = VariablesCollector.Collect(termManager, condition,
                actualFieldValues, allFieldsInCondition, allFieldTypes);
            
            using (var solver = solverFactory.CreateSolver(solverStatistics))
            {
                var writer = new SafeStringWriter();
                foreach (var symbolIdWithType in variables)
                {
                    writer.WriteLine("variable: {0}", symbolIdWithType.Description);
                    solver.AddDomainVariable(0, symbolIdWithType);
                }
                writer.WriteLine("condition: {0}", prettyPrintTerm(host, condition, SystemTypes.Bool));

                solver.Assert("is satisfiable check", condition);
                IModel model;
                var result = solver.TryGetModel(null, out model);
                writer.WriteLine("TryGetModel => {0}", result);
                if (result == TryGetModelResult.Success)
                {
                    foreach (var symbolIdWithType in variables)
                    {
                        writer.Write("{0} => ", symbolIdWithType.Description);
                        var variable = termManager.Symbol(symbolIdWithType);
                        var value = model.GetValue(variable);
                        writer.WriteLine(prettyPrintTerm(host, value, symbolIdWithType.Type));

                        TypeEx type;

                        if (symbolIdWithType.Layout.Kind == LayoutKind.Ref &&
                            termManager.TryGetObjectType(value, out type) &&
                            type != SystemTypes.Null)
                        {
                            foreach (var field in type.InstanceFields)
                            {
                                var fieldVariable = termManager.SelectInstanceFieldInInitialState(variable, field);                                
                                var fieldValue = model.GetValue(fieldVariable);                                            

                                writer.Write("{0}.{1} => ", symbolIdWithType.Description, field.ShortName);
                                writer.WriteLine(prettyPrintTerm(host, fieldValue, field.Type));
                                
                                TypeEx fieldValueType;
                                if (termManager.TryGetObjectType(fieldValue, out fieldValueType)) // is this an object, and if so, what’s its type?
                                {
                                    // do the same thing are before for the nested fields
                                    foreach (var nestedField in field.Type.InstanceFields)
                                    {
                                        var nestedFieldVariable = termManager.SelectInstanceFieldInInitialState(fieldValue, nestedField);
                                        var nestedFieldValue = model.GetValue(nestedFieldVariable);
                                        writer.Write("{0}.{1}.{2} => ", symbolIdWithType.Description, field.ShortName, nestedField.ShortName);
                                        writer.WriteLine(prettyPrintTerm(host, nestedFieldValue, nestedField.Type));
                                    }
                                }

                                //stores the field value into expected field values
                                StoreFieldValue(termManager, field, fieldValue, expectedFieldValues, actualFieldValues, allFieldsInCondition, host, condition, binOp);
                            }
                        }
                    }
                }
                if (model != null) model.Dispose();

                host.Log.Dump("Satisfiable Checker", "TermSolver", writer.ToString());
            }

            return true;
        }

        /// <summary>
        /// simplifies the term based on the contents of the term. all 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="termManager"></param>
        /// <param name="condition"></param>
        /// <param name="binOp"></param>
        private static Term SimplifyTerm(IPexExplorationComponent host, TermManager termManager, Term condition)
        {
            Term left, right;
            BinaryOperator binOp;
                                                
            if (!termManager.TryGetBinary(condition, out binOp, out left, out right))
                return condition;
            
            if (!IsInteger(termManager, left) || !IsInteger(termManager, right))
                return condition;

            //Check whether the term is of the form x > 20, where one side is a constant. then no simplification needs to be done
            if (termManager.IsValue(left))
            {
                //one side is constant. so just return over here
                return condition;
            }          

            if (termManager.IsValue(right))
            {
                //one side is constant. so just return over here
                return condition;
            }
                       

            //none of the sides are concrete. both sides are symbolic.
            //find out which side can be more controlled based on the variables
            //contained on that side
            SafeList<Field> allFieldsInLeftCondition = new SafeList<Field>();
            SafeList<TypeEx> allFieldTypes = new SafeList<TypeEx>();
            SafeDictionary<Field, FieldValueHolder> leftfields = new SafeDictionary<Field, FieldValueHolder>();
            VariablesCollector.Collect(termManager, left, leftfields, allFieldsInLeftCondition, allFieldTypes);

            //TODO: How to get the concrete value of the other side, could be either left
            //or right. and make a term out of the concrete value
            int lvalue;
            if (termManager.TryGetI4Constant(left, out lvalue))
            {
                
            }

            int rvalue;
            if (termManager.TryGetI4Constant(right, out rvalue))
            {

            }

            return condition;
        }

        /// <summary>
        /// Stores the expected field value. Currently implemented for integers for time being
        /// </summary>
        /// <param name="termManager"></param>
        /// <param name="field"></param>
        /// <param name="fieldValue"></param>
        /// <param name="expectedFieldValues"></param>
        /// <param name="host"></param>
        /// <param name="condition"></param>
        /// <param name="binOp"></param>
        private static void StoreFieldValue(TermManager termManager, Field field, Term fieldValue,
            SafeDictionary<Field, FieldValueHolder> expectedFieldValues, SafeDictionary<Field, FieldValueHolder> actualFieldValues,
            SafeList<Field> allFieldsInCondition, IPexComponent host, Term condition, BinaryOperator binOp)
        {         
            TypeEx fieldType = field.Type;

            if (fieldType == SystemTypes.Int32)
            {
                int value;
                if (termManager.TryGetI4Constant(fieldValue, out value))
                {
                    FieldValueHolder fvh = new FieldValueHolder(FieldValueType.INTEGER);
                    fvh.intValue = value;
                    expectedFieldValues.Add(field, fvh);
                }
                return;
            }

            //Gathering the expected value for boolean field
            Term unnegatedCondition;
            bool bNegated = false;
            if (termManager.TryGetInnerLogicallyNegatedValue(condition, out unnegatedCondition))
                bNegated = true;
            else
                unnegatedCondition = condition; 

            if (fieldType == SystemTypes.Bool)
            {
                if (binOp == BinaryOperator.Ceq)
                {                    
                    FieldValueHolder fvh = new FieldValueHolder(FieldValueType.BOOLEAN);
                    fvh.boolValue = bNegated;
                    expectedFieldValues.Add(field, fvh);                   

                    //For boolean, actual value does not matter. However, without
                    //proper entry in the acutal field, it would not considered for further processing
                    if (allFieldsInCondition.Contains(field) && !actualFieldValues.Keys.Contains(field))
                    {
                        FieldValueHolder fvhtemp = new FieldValueHolder(FieldValueType.BOOLEAN);
                        fvhtemp.boolValue = false;
                        actualFieldValues.Add(field, fvhtemp);    
                    }
                }
                return;
            }

            if (fieldType == SystemTypes.Int16)
            {
                short value;
                if (termManager.TryGetI2Constant(fieldValue, out value))
                {
                    FieldValueHolder fvh = new FieldValueHolder(FieldValueType.SHORT);
                    fvh.shortValue = value;
                    expectedFieldValues.Add(field, fvh);                                
                }
                return;
            }

            if (fieldType == SystemTypes.Int64)
            {
                long value;
                if (termManager.TryGetI8Constant(fieldValue, out value))
                {
                    FieldValueHolder fvh = new FieldValueHolder(FieldValueType.LONG);
                    fvh.longValue = value;
                    expectedFieldValues.Add(field, fvh);
                }
                return;
            }

            if (field.Type.IsReferenceType)
            {
                Object obj = null;
                termManager.TryGetObject(fieldValue, out obj);
                FieldValueHolder fvh = new FieldValueHolder(FieldValueType.OBJECT);
                fvh.objValue = obj;
                expectedFieldValues.Add(field, fvh);

                //For reference value the actual value does not matter. However, without
                //proper entry in the acutal field, it would not considered for further processing
                if (allFieldsInCondition.Contains(field) && !actualFieldValues.Keys.Contains(field))
                {
                    FieldValueHolder fvhtemp = new FieldValueHolder(FieldValueType.OBJECT);
                    fvhtemp.objValue = null;
                    actualFieldValues.Add(field, fvhtemp);
                }

                return;
            }
            
            host.Log.LogWarning(WikiTopics.MissingWikiTopic, "TermSolver", "Expected values are computed only for integers, boolean and objects. Requires manual analysis of this sceanario");            
        }

        private static string prettyPrintTerm(IPexExplorationComponent host, Term term, TypeEx type)
        {
            var writer = new SafeStringWriter();
            var codeWriter = host.Services.TestManager.Language.CreateBodyWriter(
                writer,
                VisibilityContext.Private,
                100);
            var emitter = new TermEmitter(
                 host.ExplorationServices.TermManager,
                 new NameCreator());

            if (!emitter.TryEvaluate(
                    new Term[] { term },
                    10000, // bound on size of expression we are going to pretty-print
                    codeWriter))
            {
                writer.WriteLine("(expression too big; consider using environment variable {0})", 
                    ExtendedReflectionEnvironmentSettings.NoCodeEmitterSizeLimit.Name);
            }
            else
            {
                codeWriter.Return(type);
            }
            return writer.ToString();
        }

        /// <summary>
        /// Checks whether the term includes integer constants
        /// </summary>
        /// <param name="termManager"></param>
        /// <param name="condition"></param>
        /// <returns></returns>
        private static bool IsInteger(TermManager termManager, Term condition)
        {
            //further processing is required only for integer types
            switch (termManager.GetLayoutKind(condition))
            {
                case LayoutKind.I1:
                case LayoutKind.I2:
                case LayoutKind.I4:
                case LayoutKind.I8:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Collects variables. Also collects the actual field values. However, this function works only for integer field values
        /// </summary>
        sealed class VariablesCollector : TermInternalizingRewriter<TVoid>
        {
            SafeSet<ISymbolIdWithType> variables = new SafeSet<ISymbolIdWithType>();
            public Field lastAccessedField = null;
            public SafeList<Field> Fields = null;
            public SafeList<TypeEx> Types = null;
            public SafeDictionary<Field, FieldValueHolder> FieldValues;

            VariablesCollector(TermManager termManager, SafeDictionary<Field, FieldValueHolder> fieldValues, 
                SafeList<Field> allFields, SafeList<TypeEx> allFieldTypes) : base(termManager, OnCollection.Fail) 
            {
                this.FieldValues = fieldValues;
                this.Fields = allFields;
                this.Types = allFieldTypes;
            }
            
            public override Term VisitSymbol(TVoid parameter, Term term, ISymbolId key)
            {
                if (this.TermManager.TryGetInstanceField(term, out this.lastAccessedField))
                {
                    //Currently we are handling only these types
                    if (PexMeFilter.IsTypeSupported(this.lastAccessedField.Type))
                    {
                        this.Fields.Add(this.lastAccessedField);
                    }
                }
                
                if (key is ISymbolIdWithType)
                    this.variables.Add((ISymbolIdWithType)key);
                //return term;
                return base.VisitSymbol(parameter, term, key);
            }

            public static ISymbolIdWithType[] Collect(TermManager termManager, Term term, 
                SafeDictionary<Field, FieldValueHolder> fieldValues, SafeList<Field> fields, SafeList<TypeEx> allFieldTypes)
            {
                using (var collector = new VariablesCollector(termManager, fieldValues, fields, allFieldTypes))
                {
                    collector.VisitTerm(default(TVoid), term);
                    return collector.variables.ToArray();
                }
            }            

            public override Term VisitI2(TVoid parameter, Term term, short value)
            {
                if (this.lastAccessedField != null && this.lastAccessedField.Type.ToString() == "System.Int16")
                {
                    FieldValueHolder fvh = new FieldValueHolder(FieldValueType.SHORT);
                    fvh.shortValue = value;
                    this.FieldValues[lastAccessedField] = fvh;
                }
                return base.VisitI2(parameter, term, value);
            }

            public override Term VisitI4(TVoid parameter, Term term, int value)
            {
                if (this.lastAccessedField != null && this.lastAccessedField.Type.ToString() == "System.Int32")
                {
                    FieldValueHolder fvh = new FieldValueHolder(FieldValueType.INTEGER);
                    fvh.intValue = value;
                    this.FieldValues[lastAccessedField] = fvh;
                }
                return base.VisitI4(parameter, term, value);
            }

            public override Term VisitI8(TVoid parameter, Term term, long value)
            {
                if (this.lastAccessedField != null && this.lastAccessedField.Type.ToString() == "System.Int64")
                {
                    FieldValueHolder fvh = new FieldValueHolder(FieldValueType.LONG);
                    fvh.longValue = value;
                    this.FieldValues[lastAccessedField] = fvh;
                }
                return base.VisitI8(parameter, term, value);
            }
        }
    }
}
