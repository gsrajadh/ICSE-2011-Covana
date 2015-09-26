using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using Microsoft.ExtendedReflection.Collections;
using PexMe.Core;
using System.Reflection.Emit;
using Microsoft.ExtendedReflection.Logging;
using NLog;

namespace DUCover.Static
{
    [__DoNotInstrument]
    public class EffectAnalyzer
    {
        /// <summary>
        /// Initializing logger
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

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

                //Check whether this has been computed before
                var psd = host.GetService<PexMeStaticDatabase>() as PexMeStaticDatabase;
                if (psd.MethodEffectsDic.TryGetValue(method.GlobalIndex, out effects))
                    return true;

                var res = new SafeSet<string>();
                var directSetFields = new SafeSet<string>();
                var directCalledMethods = new SafeSet<Method>();
                var returnFields = new SafeSet<Field>();
                var modificationTypeDic = new SafeDictionary<string, FieldModificationType>();
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
                Field lastAccessedField = null;

                while (body.TryGetInstruction(offset, out instruction))
                {
                    SafeDebug.AssumeNotNull(instruction, "instruction");
                    OpCode opCode = instruction.OpCode;
                    if (MethodOrFieldAnalyzer.LdcOpCodes.Contains(opCode))
                    {
                        //topIsConstant = true;
                    }
                    else if (MethodOrFieldAnalyzer.ConvOpCodes.Contains(opCode))
                    {
                        // do not change topIsConstant
                    }
                    else
                    {
                        if (opCode == OpCodes.Stfld)
                        {
                            SafeDebug.Assume(opCode.OperandType == OperandType.InlineField, "opCode.OperandType == OperandType.InlineField");
                            Field field = instruction.Field;
                            AddFieldToMethodEffects(host, declaringType, res, directSetFields, modificationTypeDic, prevOpcode, field, field.Type);
                        }
                        else if (opCode == OpCodes.Ldfld || opCode == OpCodes.Ldflda)
                        {
                            SafeDebug.Assume(opCode.OperandType == OperandType.InlineField, "opCode.OperandType == OperandType.InlineField");
                            Field accessedField = instruction.Field;

                            if (accessedField.Type.Spec == TypeSpec.SzArray)
                            {
                                lastAccessedArrayField = accessedField;
                            }
                            else
                                lastAccessedField = accessedField;
                        }
                        else if (MethodOrFieldAnalyzer.StElemOpCodes.Contains(opCode))
                        {
                            if (lastAccessedArrayField != null)
                            {
                                //Indicates that there is n array type modified
                                AddFieldToMethodEffects(host, declaringType, res, directSetFields, modificationTypeDic, prevOpcode, lastAccessedArrayField, lastAccessedArrayField.Type);
                                lastAccessedArrayField = null;
                            }
                        }
                        else if (opCode == OpCodes.Call || opCode == OpCodes.Callvirt)
                        {
                            SafeDebug.Assume(opCode.OperandType == OperandType.InlineMethod, "opCode.OperandType == OperandType.InlineMethod");
                            Method methodinner = instruction.Method;
                            SafeDebug.AssumeNotNull(method, "method");

                            directCalledMethods.Add(methodinner);
                            TypeEx methodDeclaringType;

                            //are these function calls are within the parent types
                            if (methodinner.TryGetDeclaringType(out methodDeclaringType))                            
                            {
                                if (methodinner.ShortName.StartsWith("get_"))
                                {
                                    //a getter method. get the field accessed and store it in the lastaccessed field
                                    int tempoffset;
                                    DeclEntityCollector.TryGetFieldOfGetter(methodinner, out lastAccessedField, out tempoffset);
                                }
                                else
                                {
                                    MethodEffects methodEffects;
                                    if (TryComputeMethodEffects(host, methodDeclaringType, methodinner, visitedMethods, out methodEffects))
                                    {
                                        if (declaringType.IsAssignableTo(methodDeclaringType))
                                        {
                                            res.AddRange(methodEffects.WrittenInstanceFields);
                                            foreach (var key in methodEffects.ModificationTypeDictionary.Keys)
                                                modificationTypeDic[key] = methodEffects.ModificationTypeDictionary[key];
                                            directSetFields.AddRange(methodEffects.DirectSetterFields);
                                            if (methodEffects.CallDepth > callDepth)
                                                callDepth = methodEffects.CallDepth;
                                        }
                                        else
                                        {
                                            //An invocation is made on one of the fields
                                            if (methodEffects.WrittenInstanceFields.Count > 0)
                                            {
                                                //The method call is modifying the last accessed field
                                                AddFieldToMethodEffects(host, declaringType, res, directSetFields, modificationTypeDic,
                                                    prevOpcode, lastAccessedField, lastAccessedField.Type);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                //introducing heuristics for inter-procedural static analysis
                                if (lastAccessedField != null && lastAccessedField.Type.IsReferenceType &&
                                    !(methodinner.ShortName.StartsWith("Get") || methodinner.ShortName.StartsWith("get")
                                    || methodinner.ShortName.StartsWith("Set") || methodinner.ShortName.StartsWith("set")))
                                {
                                    AddFieldToMethodEffects(host, declaringType, res, directSetFields, modificationTypeDic,
                                        prevOpcode, lastAccessedField, lastAccessedField.Type);
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

                effects = new MethodEffects((IFiniteSet<string>)res, directSetFields, directCalledMethods, returnFields, modificationTypeDic, callDepth + 1);
                psd.MethodEffectsDic[method.GlobalIndex] = effects;
                return true;
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error occurred while computing side effects of a method: " + ex.Message, ex);
                effects = null;
                return false;
            }
        }

        public static TypeEx AddFieldToMethodEffects(IPexComponent host, TypeEx declaringType, SafeSet<string> res,
            SafeSet<string> directSetFields, SafeDictionary<string, FieldModificationType> modificationTypeDic,
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
                res.Add(field.ShortName);

                FieldModificationType fmt = FieldModificationType.UNKNOWN;
                if (fieldType == SystemTypes.Int32 || fieldType == SystemTypes.Int64 || fieldType == SystemTypes.Int16)
                {
                    if (prevOpcode == OpCodes.Add)
                        fmt = FieldModificationType.INCREMENT;
                    else if (prevOpcode == OpCodes.Sub)
                        fmt = FieldModificationType.DECREMENT;
                    else if (prevOpcode == OpCodes.Call || prevOpcode == OpCodes.Calli || prevOpcode == OpCodes.Callvirt)
                        fmt = FieldModificationType.METHOD_CALL;
                    else
                        logger.Warn("Encountered unknown modification type for integer type " + prevOpcode);
                }
                else
                {
                    if (field.Type.IsReferenceType)
                    {
                        if (prevOpcode == OpCodes.Ldnull)
                            fmt = FieldModificationType.NULL_SET;
                        else if (prevOpcode == OpCodes.Newarr || prevOpcode == OpCodes.Newobj)
                            fmt = FieldModificationType.NON_NULL_SET;
                        else if (MethodOrFieldAnalyzer.LdArgOpCodes.Contains(prevOpcode))
                            fmt = FieldModificationType.NON_NULL_SET;
                        else
                        {
                            fmt = FieldModificationType.METHOD_CALL;    //A method call is invoked on this field, which updates this field
                        }
                    }
                    else if (fieldType == SystemTypes.Bool)
                    {
                        if (prevOpcode == OpCodes.Ldc_I4_0)
                            fmt = FieldModificationType.FALSE_SET;
                        else if (prevOpcode == OpCodes.Ldc_I4_1)
                            fmt = FieldModificationType.TRUE_SET;
                        else
                            logger.Warn("Encountered unknown modification type for boolean type " + prevOpcode);
                    }
                }

                //Store the value of fmt. Sometimes, the same field
                //can be modified in different ways within the method, for example
                //setting a boolean field to both true or false. In that case, the modification
                //type is left as unknown
                FieldModificationType prevFMT;
                if (modificationTypeDic.TryGetValue(field.ShortName, out prevFMT))
                {
                    //There is some entry for this field
                    if (prevFMT != FieldModificationType.UNKNOWN && prevFMT != fmt)
                    {
                        modificationTypeDic[field.ShortName] = FieldModificationType.UNKNOWN;
                    }
                }
                else
                {
                    modificationTypeDic[field.ShortName] = fmt;
                }


                //A heuristic based approach for aliasing analysis for checking whether the field is directly
                //assigned any parameters
                if (MethodOrFieldAnalyzer.LdArgOpCodes.Contains(prevOpcode))
                    directSetFields.Add(field.ShortName);
            }
            return fieldDeclaringType;
        }
    }
}
