using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.Pex.Engine.Explorable;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using Microsoft.ExtendedReflection.Collections;
using System.Reflection.Emit;
using Microsoft.ExtendedReflection.Utilities.Safe.Text;

namespace Covana.Analyzer
{
    /// <summary>
    /// Class that includes several methods for analyzing
    /// a given field such as whether that field is externally visible or not
    /// </summary>
    public static class MethodOrFieldAnalyzer
    {
        private static readonly IFiniteSet<OpCode> LdcOpCodes = Set.Enumerable(null, new OpCode[]
                                                                                         {
                                                                                             OpCodes.Ldc_I4,
                                                                                             OpCodes.Ldc_I4_0,
                                                                                             OpCodes.Ldc_I4_1,
                                                                                             OpCodes.Ldc_I4_2,
                                                                                             OpCodes.Ldc_I4_3,
                                                                                             OpCodes.Ldc_I4_4,
                                                                                             OpCodes.Ldc_I4_5,
                                                                                             OpCodes.Ldc_I4_6,
                                                                                             OpCodes.Ldc_I4_7,
                                                                                             OpCodes.Ldc_I4_8,
                                                                                             OpCodes.Ldc_I4_M1,
                                                                                             OpCodes.Ldc_I4_S,
                                                                                             OpCodes.Ldc_I8,
                                                                                             OpCodes.Ldc_R4,
                                                                                             OpCodes.Ldc_R8,
                                                                                             OpCodes.Ldnull,
                                                                                             OpCodes.Ldstr
                                                                                         });

        private static readonly IFiniteSet<OpCode> ConvOpCodes = Set.Enumerable(null, new OpCode[]
                                                                                          {
                                                                                              OpCodes.Conv_I,
                                                                                              OpCodes.Conv_I1,
                                                                                              OpCodes.Conv_I2,
                                                                                              OpCodes.Conv_I4,
                                                                                              OpCodes.Conv_I8,
                                                                                              OpCodes.Conv_R_Un,
                                                                                              OpCodes.Conv_R4,
                                                                                              OpCodes.Conv_R8,
                                                                                              OpCodes.Conv_U,
                                                                                              OpCodes.Conv_U1,
                                                                                              OpCodes.Conv_U2,
                                                                                              OpCodes.Conv_U4,
                                                                                              OpCodes.Conv_U8
                                                                                          });

        private static readonly IFiniteSet<OpCode> LdArgOpCodes = Set.Enumerable(null, new OpCode[]
                                                                                           {
                                                                                               OpCodes.Ldarg,
                                                                                               OpCodes.Ldarg_0,
                                                                                               OpCodes.Ldarg_1,
                                                                                               OpCodes.Ldarg_2,
                                                                                               OpCodes.Ldarg_3,
                                                                                               OpCodes.Ldarg_S,
                                                                                               OpCodes.Ldarga,
                                                                                               OpCodes.Ldarga_S
                                                                                           });

        private static readonly IFiniteSet<OpCode> StElemOpCodes = Set.Enumerable(null, new OpCode[]
                                                                                            {
                                                                                                OpCodes.Stelem,
                                                                                                OpCodes.Stelem_I,
                                                                                                OpCodes.Stelem_I1,
                                                                                                OpCodes.Stelem_I2,
                                                                                                OpCodes.Stelem_I4,
                                                                                                OpCodes.Stelem_I8,
                                                                                                OpCodes.Stelem_R4,
                                                                                                OpCodes.Stelem_R8,
                                                                                                OpCodes.Stelem_Ref
                                                                                            });

        public static StringBuilder Log = new StringBuilder("Log For MethodOrFieldAnalyzer: \n");


        /// <summary>
        /// Returns true if the field is visible externally of the given type
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static bool IsFieldExternallyVisible(IPexComponent host, TypeEx type, Field field)
        {
            var visibilityContext = VisibilityContext.Exported;

            //Step 1: Is field visible externally?
            if (field.IsVisible(visibilityContext))
                return true;

            //Step 2: Check whether this is an associated property and see whether it is public
            Property property;
            if (TryGetPropertyModifyingField(host, type, field, out property))
            {
                if (property.IsVisible(visibilityContext))
                    return true;
            }

            //Step 3: Check whether any constructor directly sets this field
            foreach (Method constructor in type.GetVisibleInstanceConstructors(visibilityContext))
            {
                MethodEffects me;
                if (TryComputeMethodEffects(host, type, constructor, null, out me))
                {
                    if (me.DirectSetterFields.Contains(field))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the type definition of a field
        /// </summary>
        /// <param name="host"></param>
        /// <param name="field"></param>
        /// <param name="ftex"></param>
        /// <returns></returns>
        public static bool TryGetDeclaringTypeDefinition(IPexComponent host, Field field, out TypeEx ftex)
        {
            var fdefinition = field.Definition;
            TypeDefinition td;
            if (!fdefinition.TryGetDeclaringType(out td))
            {
                host.Log.LogError(WikiTopics.MissingWikiTopic, "fieldanalyzer",
                                  "Failed to retrieve the declaring type of the field " + field.FullName);
                ftex = null;
                return false;
            }
            try
            {
                ftex = td.Instantiate(new TypeEx[0]);
            }
            catch (ArgumentException argEx)
            {
                ftex = td.Instantiate(new TypeEx[] {MetadataFromReflection.GetType(typeof(object))});
            }
            catch (Exception ex)
            {
                ftex = null;
                Log.AppendLine("Instantiate typeEx fail: " + ex);
            }

            return true;
        }

        /// <summary>
        /// Tries to get a property that modifies the given field
        /// </summary>
        /// <param name="host"></param>
        /// <param name="type"></param>
        /// <param name="field"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        public static bool TryGetPropertyModifyingField(IPexComponent host, TypeEx type, Field field,
                                                        out Property property)
        {
            foreach (Property prop in type.DeclaredProperties)
            {
                Method setter = prop.Setter;
                if (setter == null)
                    continue;

                MethodEffects me;
                if (TryComputeMethodEffects(host, type, setter, null, out me) &&
                    me.WrittenInstanceFields.Contains(field))
                {
                    property = prop;
                    return true;
                }
            }

            var baseType = type.BaseType;
            if (baseType != null)
            {
                TryGetPropertyModifyingField(host, baseType, field, out property);
            }

            property = null;
            return false;
        }

        /// <summary>
        /// Tries to get a property that returns a given field. Needs to go base classes
        /// also
        /// </summary>
        /// <param name="host"></param>
        /// <param name="type"></param>
        /// <param name="field"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        public static bool TryGetPropertyReadingField(IPexComponent host, TypeEx type, Field field,
                                                      out Property property)
        {
            foreach (Property prop in type.DeclaredProperties)
            {
                Method getter = prop.Getter;
                if (getter == null)
                    continue;

                MethodEffects me;
                if (TryComputeMethodEffects(host, type, getter, null, out me) && me.ReturnFields.Contains(field))
                {
                    property = prop;
                    return true;
                }
            }

            var baseType = type.BaseType;
            if (baseType != null)
            {
                TryGetPropertyReadingField(host, baseType, field, out property);
            }

            property = null;
            return false;
        }


        /// <summary>
        /// Computes method effects statically. All written fields of a method.
        /// Can be imprecise and conservative
        /// </summary>
        /// <param name="declaringType"></param>
        /// <param name="method"></param>
        /// <param name="effects"></param>
        /// <returns></returns>
        public static bool TryComputeMethodEffects(IPexComponent host, TypeEx declaringType, Method method,
                                                   SafeSet<Method> visitedMethods, out MethodEffects effects)
        {
            SafeDebug.AssumeNotNull(declaringType, "declaringType");
            SafeDebug.AssumeNotNull(method, "method");

            try
            {
                if (visitedMethods == null)
                    visitedMethods = new SafeSet<Method>();

                if (visitedMethods.Contains(method))
                {
                    effects = null;
                    return false;
                }

                visitedMethods.Add(method);

//                //Check whether this has been computed before
//                var psd = host.GetService<IPexMeStaticDatabase>() as PexMeStaticDatabase;
//                if (psd.MethodEffectsDic.TryGetValue(method.GlobalIndex, out effects))
//                    return true;

                var res = new SafeSet<Field>();
                var directSetFields = new SafeSet<Field>();
                var directCalledMethods = new SafeSet<Method>();
                var returnFields = new SafeSet<Field>();
                var modificationTypeDic = new SafeDictionary<Field, FieldModificationType>();
                var parameters = method.Parameters;

                MethodBodyEx body;
                if (!method.TryGetBody(out body) || !body.HasInstructions)
                {
                    effects = null;
                    return false;
                }

                int callDepth = 0;
                int offset = 0;
                Instruction instruction;
                OpCode prevOpcode = OpCodes.Nop;

                //Stack for load instructions
                Field lastAccessedArrayField = null;

                while (body.TryGetInstruction(offset, out instruction))
                {
                    SafeDebug.AssumeNotNull(instruction, "instruction");
                    OpCode opCode = instruction.OpCode;
                    if (LdcOpCodes.Contains(opCode))
                    {
                        //topIsConstant = true;
                    }
                    else if (ConvOpCodes.Contains(opCode))
                    {
                        // do not change topIsConstant
                    }
                    else
                    {
                        if (opCode == OpCodes.Stfld)
                        {
                            SafeDebug.Assume(opCode.OperandType == OperandType.InlineField,
                                             "opCode.OperandType == OperandType.InlineField");
                            Field field = instruction.Field;

                            AddFieldToMethodEffects(host, declaringType, res, directSetFields, modificationTypeDic,
                                                    prevOpcode, field, field.Type);
                        }
                        else if (opCode == OpCodes.Ldfld || opCode == OpCodes.Ldflda)
                        {
                            SafeDebug.Assume(opCode.OperandType == OperandType.InlineField,
                                             "opCode.OperandType == OperandType.InlineField");
                            Field accessedField = instruction.Field;

                            if (accessedField.Type.Spec == TypeSpec.SzArray)
                            {
                                lastAccessedArrayField = accessedField;
                            }
                        }
                        else if (StElemOpCodes.Contains(opCode))
                        {
                            if (lastAccessedArrayField != null)
                            {
                                //Indicates that there is n array type modified
                                AddFieldToMethodEffects(host, declaringType, res, directSetFields, modificationTypeDic,
                                                        prevOpcode, lastAccessedArrayField, lastAccessedArrayField.Type);
                            }
                        }
                        else if (opCode == OpCodes.Call || opCode == OpCodes.Callvirt)
                        {
                            SafeDebug.Assume(opCode.OperandType == OperandType.InlineMethod,
                                             "opCode.OperandType == OperandType.InlineMethod");
                            Method methodinner = instruction.Method;
                            SafeDebug.AssumeNotNull(method, "method");

                            directCalledMethods.Add(methodinner);
                            TypeEx methodDeclaringType;

                            if (methodinner.TryGetDeclaringType(out methodDeclaringType) &&
                                declaringType.IsAssignableTo(methodDeclaringType))
                            {
                                MethodEffects methodEffects;
                                if (TryComputeMethodEffects(host, methodDeclaringType, methodinner, visitedMethods,
                                                            out methodEffects))
                                {
                                    res.AddRange(methodEffects.WrittenInstanceFields);
                                    foreach (var key in methodEffects.ModificationTypeDictionary.Keys)
                                        modificationTypeDic[key] = methodEffects.ModificationTypeDictionary[key];
                                    directSetFields.AddRange(methodEffects.DirectSetterFields);
                                    if (methodEffects.CallDepth > callDepth)
                                        callDepth = methodEffects.CallDepth;
                                }
                            }
                        }
                        else if (opCode == OpCodes.Ret)
                        {
                            if (instruction.Field != null)
                                returnFields.Add(instruction.Field);
                        }
                        //topIsConstant = false;
                    }

                    prevOpcode = opCode;
                    offset = instruction.NextOffset;
                }

                effects = new MethodEffects((IFiniteSet<Field>) res, directSetFields, directCalledMethods, returnFields,
                                            modificationTypeDic, callDepth + 1);
//                psd.MethodEffectsDic[method.GlobalIndex] = effects;
                return true;
            }
            catch (Exception ex)
            {
                host.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "methodeffects",
                                               "Failed to compute method effects for method " + method.FullName + "," +
                                               ex.Message);
                effects = null;
                return false;
            }
        }

        private static TypeEx AddFieldToMethodEffects(IPexComponent host, TypeEx declaringType, SafeSet<Field> res,
                                                      SafeSet<Field> directSetFields,
                                                      SafeDictionary<Field, FieldModificationType> modificationTypeDic,
                                                      OpCode prevOpcode, Field field, TypeEx fieldType)
        {
            SafeDebug.AssumeNotNull(field, "field");
            SafeDebug.Assume(!field.IsStatic, "!field.IsStatic");

            TypeEx fieldDeclaringType;
            //The following check ensures that the field belongs to this class
            //or its base classes
            if (field.TryGetDeclaringType(out fieldDeclaringType) &&
                declaringType.IsAssignableTo(fieldDeclaringType))
            {
                res.Add(field);

                var fieldTypeStr = fieldType.ToString();
                if (fieldTypeStr == "System.Int32" || fieldTypeStr == "System.Int64" || fieldTypeStr == "System.Int16")
                {
                    if (prevOpcode == OpCodes.Add)
                        modificationTypeDic[field] = FieldModificationType.INCREMENT;
                    else if (prevOpcode == OpCodes.Sub)
                        modificationTypeDic[field] = FieldModificationType.DECREMENT;
                    else
                        host.Log.LogWarning(WikiTopics.MissingWikiTopic, "fieldmodificationtype",
                                            "Encountered unknown modification type for integer type " + prevOpcode);
                }
                else
                {
                    if (field.Type.IsReferenceType)
                    {
                        if (prevOpcode == OpCodes.Ldnull)
                            modificationTypeDic[field] = FieldModificationType.NULL_SET;
                        else if (prevOpcode == OpCodes.Newarr || prevOpcode == OpCodes.Newobj)
                            modificationTypeDic[field] = FieldModificationType.NON_NULL_SET;
                        else
                            host.Log.LogWarning(WikiTopics.MissingWikiTopic, "fieldmodificationtype",
                                                "Encountered unknown modification type for reference type " + prevOpcode);
                    }
                    else if (fieldTypeStr == "System.Boolean")
                    {
                        if (prevOpcode == OpCodes.Ldc_I4_0)
                            modificationTypeDic[field] = FieldModificationType.FALSE_SET;
                        else if (prevOpcode == OpCodes.Ldc_I4_1)
                            modificationTypeDic[field] = FieldModificationType.TRUE_SET;
                        else
                            host.Log.LogWarning(WikiTopics.MissingWikiTopic, "fieldmodificationtype",
                                                "Encountered unknown modification type for boolean type " + prevOpcode);
                    }
                }

                //A heuristic based approach for aliasing analysis for checking whether the field is directly
                //assigned any parameters
                if (LdArgOpCodes.Contains(prevOpcode))
                    directSetFields.Add(field);
            }
            return fieldDeclaringType;
        }

        /// <summary>
        /// Returns a signature for the method including its name and parameters.
        /// Return type is not included
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string GetMethodSignature(Method method)
        {
            SafeStringBuilder sb = new SafeStringBuilder();
            sb.Append(method.FullName);
            sb.Append("(");

            int paramCount = 0, numParams = method.ParameterTypes.Length;
            foreach (var parametertype in method.ParameterTypes)
            {
                sb.Append(parametertype.FullName);
                paramCount++;
                if (paramCount != numParams)
                    sb.Append(",");
            }

            sb.Append(")");
            return sb.ToString();
        }
    }
}