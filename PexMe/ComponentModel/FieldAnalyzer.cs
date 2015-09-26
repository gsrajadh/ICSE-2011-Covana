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
using Microsoft.ExtendedReflection.Utilities;
using PexMe.Common;
using PexMe.ComponentModel.Hardcoded;

namespace PexMe.Core
{
    /// <summary>
    /// Class that includes several methods for analyzing
    /// a given field such as whether that field is externally visible or not
    /// </summary>
    public static class MethodOrFieldAnalyzer
    {
        public static readonly IFiniteSet<OpCode> LdcOpCodes = Set.Enumerable(null, new OpCode[]{
                OpCodes.Ldc_I4, OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2, OpCodes.Ldc_I4_3, OpCodes.Ldc_I4_4, OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6, OpCodes.Ldc_I4_7, OpCodes.Ldc_I4_8, OpCodes.Ldc_I4_M1, OpCodes.Ldc_I4_S, OpCodes.Ldc_I8, OpCodes.Ldc_R4, OpCodes.Ldc_R8, OpCodes.Ldnull, OpCodes.Ldstr
            });

        public static readonly IFiniteSet<OpCode> ConvOpCodes = Set.Enumerable(null, new OpCode[]{
                OpCodes.Conv_I, OpCodes.Conv_I1, OpCodes.Conv_I2, OpCodes.Conv_I4, OpCodes.Conv_I8, OpCodes.Conv_R_Un, OpCodes.Conv_R4, OpCodes.Conv_R8, OpCodes.Conv_U, OpCodes.Conv_U1, OpCodes.Conv_U2, OpCodes.Conv_U4, OpCodes.Conv_U8
            });

        public static readonly IFiniteSet<OpCode> LdArgOpCodes = Set.Enumerable(null, new OpCode[]{
                OpCodes.Ldarg, OpCodes.Ldarg_0, OpCodes.Ldarg_1, OpCodes.Ldarg_2, OpCodes.Ldarg_3, OpCodes.Ldarg_S, OpCodes.Ldarga, OpCodes.Ldarga_S
            });

        public static readonly IFiniteSet<OpCode> StElemOpCodes = Set.Enumerable(null, new OpCode[]{
                OpCodes.Stelem, OpCodes.Stelem_I, OpCodes.Stelem_I1, OpCodes.Stelem_I2, OpCodes.Stelem_I4, OpCodes.Stelem_I8, OpCodes.Stelem_R4, OpCodes.Stelem_R8, OpCodes.Stelem_Ref
            });

        public static readonly IFiniteSet<OpCode> BranchOpCodes = Set.Enumerable(null, new OpCode[]{
                OpCodes.Beq, OpCodes.Beq_S, OpCodes.Bge, OpCodes.Bge_S, OpCodes.Bge_Un, OpCodes.Bge_Un_S, 
                OpCodes.Bgt, OpCodes.Bgt_S, OpCodes.Bgt_Un, OpCodes.Bgt_Un_S, OpCodes.Ble, OpCodes.Ble_S, 
                OpCodes.Ble_Un, OpCodes.Ble_Un_S, OpCodes.Blt, OpCodes.Blt_S, OpCodes.Blt_Un, OpCodes.Blt_Un_S, 
                OpCodes.Bne_Un, OpCodes.Bne_Un_S, OpCodes.Brfalse, OpCodes.Brfalse_S, OpCodes.Brtrue, OpCodes.Brtrue_S
            });

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
                    if (me.DirectSetterFields.Contains(field.ShortName))
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
        public static bool TryGetDeclaringTypeDefinition(IPexComponent host, Field field, out TypeDefinition td)
        {
            var fdefinition = field.Definition;            
            if (!fdefinition.TryGetDeclaringType(out td))
            {
                host.Log.LogError(WikiTopics.MissingWikiTopic, "fieldanalyzer",
                    "Failed to retrieve the declaring type of the field " + field.FullName);
                td = null;
                return false;
            }            
            return true;
        }

        /// <summary>
        /// Gets the type definition of a field
        /// </summary>
        /// <param name="host"></param>
        /// <param name="field"></param>
        /// <param name="ftex"></param>
        /// <returns></returns>
        public static bool TryGetDeclaringTypeEx(IPexComponent host, Field field, out TypeEx ftex)
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

            ftex = td.Instantiate(GetGenericTypeParameters(host, td));
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
        public static bool TryGetPropertyModifyingField(IPexComponent host, TypeEx type, Field field, out Property property)
        {            
            foreach (Property prop in type.DeclaredProperties)
            {
                Method setter = prop.Setter;
                if (setter == null)
                    continue;

                MethodEffects me;
                if (TryComputeMethodEffects(host, type, setter, null, out me) && me.WrittenInstanceFields.Contains(field.ShortName))
                {
                    FieldModificationType fmt;
                    if (me.ModificationTypeDictionary.TryGetValue(field.ShortName, out fmt) && fmt != FieldModificationType.METHOD_CALL
                        && fmt != FieldModificationType.UNKNOWN)
                    {
                        property = prop;
                        return true;
                    }
                }
            }

            var baseType = type.BaseType;
            if (baseType != null)
            {
                if (TryGetPropertyModifyingField(host, baseType, field, out property))
                    return true;
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
        public static bool TryGetPropertyReadingField(IPexComponent host, TypeEx type, Field field, out Property property)
        {
            if (type == null)
            {
                property = null;
                return false;
            }

            if (type.DeclaredProperties != null)
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
            }

            var baseType = type.BaseType;
            if (baseType != null)
            {
                if (TryGetPropertyReadingField(host, baseType, field, out property))
                    return true;
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

                //Check whether this has been computed before
                var psd = host.GetService<IPexMeStaticDatabase>() as PexMeStaticDatabase;
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
                        else if (StElemOpCodes.Contains(opCode))
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
                            if (methodinner.TryGetDeclaringType(out methodDeclaringType) &&
                                declaringType.IsAssignableTo(methodDeclaringType))
                            {
                                MethodEffects methodEffects;
                                if (TryComputeMethodEffects(host, methodDeclaringType, methodinner, visitedMethods, out methodEffects))
                                {
                                    res.AddRange(methodEffects.WrittenInstanceFields);
                                    foreach (var key in methodEffects.ModificationTypeDictionary.Keys)
                                        modificationTypeDic[key] = methodEffects.ModificationTypeDictionary[key];
                                    directSetFields.AddRange(methodEffects.DirectSetterFields);
                                    if (methodEffects.CallDepth > callDepth)
                                        callDepth = methodEffects.CallDepth;
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
                host.Log.LogErrorFromException(ex, WikiTopics.MissingWikiTopic, "methodeffects",
                    "Failed to compute method effects for method " + method.FullName + "," + ex.Message);
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
                        host.Log.LogWarning(WikiTopics.MissingWikiTopic, "fieldmodificationtype",
                                "Encountered unknown modification type for integer type " + prevOpcode);
                }
                else
                {
                    if (field.Type.IsReferenceType)
                    {
                        if (prevOpcode == OpCodes.Ldnull)
                            fmt = FieldModificationType.NULL_SET;
                        else if (prevOpcode == OpCodes.Newarr || prevOpcode == OpCodes.Newobj)
                            fmt = FieldModificationType.NON_NULL_SET;
                        else if (LdArgOpCodes.Contains(prevOpcode))
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
                            host.Log.LogWarning(WikiTopics.MissingWikiTopic, "fieldmodificationtype",
                                    "Encountered unknown modification type for boolean type " + prevOpcode);
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
                if (LdArgOpCodes.Contains(prevOpcode))
                    directSetFields.Add(field.ShortName);
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

        /// <summary>
        /// Returns a signature for the method including its name and parameters.
        /// Return type is not included
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string GetMethodSignature(MethodDefinition methoddef)
        {
            SafeStringBuilder sb = new SafeStringBuilder();
            sb.Append(methoddef.FullName);
            sb.Append("(");

            int paramCount = 0, numParams = methoddef.ParameterTypes.Count;
            foreach (var parametertype in methoddef.ParameterTypes)
            {
                sb.Append(parametertype.ToString());
                paramCount++;
                if (paramCount != numParams)
                    sb.Append(",");
            }

            sb.Append(")");
            return sb.ToString();
        }

        /// <summary>
        /// Accepts a method and returns the persistent string form "assemblyname#typename#methodsignature" of the method.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string GetPersistentStringFormOfMethod(Method m)
        {
            var assemblyname = m.Definition.Module.Assembly.Location;
            string typename;
            TypeDefinition typedef = null;
            if (!m.Definition.TryGetDeclaringType(out typedef))
            {
                SafeDebug.AssumeNotNull(typedef, "Type definition is empty");
                typename = PexMeConstants.PexMeDummyTypeName;
            }
            else
            {
                typename = typedef.FullName;
            }

            var signature = MethodOrFieldAnalyzer.GetMethodSignature(m);
            return assemblyname + PexMeConstants.PexMePersistenceFormSeparator
                + typename + PexMeConstants.PexMePersistenceFormSeparator + signature;
        }

        /// <summary>
        /// Accepts a method in the form "assemblyname#typename#methodsignature" and returns the actual method
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static bool TryGetMethodFromPersistentStringForm(IPexComponent host, string mstr, out Method m)
        {
            m = null;

            var splitarr = mstr.Split(new char[] { PexMeConstants.PexMePersistenceFormSeparator });
            SafeDebug.Assume(splitarr.Length == 3, "Incorrect persistent store method name");

            var assemblyname = splitarr[0];
            var typename = splitarr[1];
            var signature = splitarr[2];

            MethodDefinition mdef;
            if (!TryGetMethodDefinition(host, assemblyname, typename, signature, out mdef))
                return false;

            TypeDefinition tdef;
            if (!mdef.TryGetDeclaringType(out tdef))
                return false;

            m = mdef.Instantiate(GetGenericTypeParameters(host, tdef), GetGenericMethodParameters(host, mdef));
            return true;
        }

        /// <summary>
        /// Accepts a field and returns the persistent string form "assemblyname#typename#fieldname" of the field.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string GetPersistentStringFormOfField(Field f)
        {
            var assemblyname = f.Definition.Module.Assembly.Location;
            string typename;
            TypeDefinition typedef = null;
            if (!f.Definition.TryGetDeclaringType(out typedef))
            {
                SafeDebug.AssumeNotNull(typedef, "Type definition is empty");
                typename = PexMeConstants.PexMeDummyTypeName;
            }
            else
            {
                typename = typedef.FullName;
            }

            return assemblyname + PexMeConstants.PexMePersistenceFormSeparator
                + typename + PexMeConstants.PexMePersistenceFormSeparator + f.FullName;
        }


        /// <summary>
        /// Accepts a field in the form "assemblyname#typename#fieldname" and returns the actual field
        /// </summary>
        /// <param name="Field"></param>
        /// <returns></returns>
        public static bool TryGetFieldFromPersistentStringForm(IPexComponent host, string fstr, out Field field)
        {
            field = null;

            var splitarr = fstr.Split(new char[] { PexMeConstants.PexMePersistenceFormSeparator });
            SafeDebug.Assume(splitarr.Length == 3, "Incorrect persistent store field name");

            var assemblyname = splitarr[0];
            var typename = splitarr[1];
            var signature = splitarr[2];

            FieldDefinition fdef;
            if (!TryGetFieldDefinition(host, assemblyname, typename, signature, out fdef))
                return false;

            TypeDefinition tdef;
            if (!fdef.TryGetDeclaringType(out tdef))
                return false;

            field = fdef.Instantiate(GetGenericTypeParameters(host, tdef));
            return true;
        }

        /// <summary>
        /// Accepts a type and returns the persistent string form "assemblyname#typename" of the type.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string GetPersistentStringFormOfTypeEx(TypeEx type)
        {
            var assemblyname = type.Definition.Module.Assembly.Location;
            return assemblyname + PexMeConstants.PexMePersistenceFormSeparator + type.FullName;
        }


        /// <summary>
        /// Accepts a type in the form "assemblyname#typename" and returns the type name
        /// </summary>
        /// <param name="Field"></param>
        /// <returns></returns>
        public static bool TryGetTypeExFromPersistentStringForm(IPexComponent host, string typename, out TypeEx typeEx)
        {
            typeEx = null;

            var splitarr = typename.Split(new char[] { PexMeConstants.PexMePersistenceFormSeparator });
            SafeDebug.Assume(splitarr.Length == 2, "Incorrect persistent store type name");

            try
            {
                bool result = TryGetTypeExFromName(host, splitarr[0], splitarr[1], out typeEx);
                if (typeEx == null || !result)
                    return false;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Retrieves the method object back from the signature
        /// </summary>
        /// <param name="typename"></param>
        /// <param name="methodsignature"></param>
        /// <returns></returns>
        public static bool TryGetMethod(IPexComponent host, string assemblyname, string typename,
            string methodsignature, out Method method)
        {
            method = null;
            MethodDefinition mdef;
            if (!TryGetMethodDefinition(host, assemblyname, typename, methodsignature, out mdef))
                return false;

            TypeDefinition tdef;
            if (!mdef.TryGetDeclaringType(out tdef))
                return false;

            method = mdef.Instantiate(GetGenericTypeParameters(host, tdef), GetGenericMethodParameters(host, mdef));
            return true;
        }

        /// <summary>
        /// Retrieves the method object back from the signature
        /// </summary>
        /// <param name="typename"></param>
        /// <param name="methodsignature"></param>
        /// <returns></returns>
        public static bool TryGetMethodDefinition(IPexComponent host, string assemblyname, string typename, 
            string methodsignature, out MethodDefinition methoddef)
        {
            methoddef = null;
            TypeDefinition typeDef = null;
            try
            {
                bool result = TryGetTypeDefinitionFromName(host, assemblyname, typename, out typeDef);
                if (typeDef == null || !result)
                    return false;
            }
            catch (Exception)
            {                
                return false;
            }

            foreach (var constructor in typeDef.DeclaredInstanceConstructors)
            {
                if (GetMethodSignature(constructor) == methodsignature)
                {
                    methoddef = constructor;
                    return true;
                }
            }

            foreach (var definition in typeDef.DeclaredInstanceMethods)
            {
                if (GetMethodSignature(definition) == methodsignature)
                {
                    methoddef = definition;
                    return true;
                }
            }

            foreach (var definition in typeDef.DeclaredStaticMethods)
            {
                if (GetMethodSignature(definition) == methodsignature)
                {
                    methoddef = definition;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetTypeDefinitionFromName(IPexComponent host, string assemblyname, string typename, out TypeDefinition typeDef)
        {
            typeDef = null;
            AssemblyEx assembly;
            ReflectionHelper.TryLoadAssemblyEx(assemblyname, out assembly);

            foreach (var assemtype in assembly.TypeDefinitions)
            {
                if (assemtype.FullName == typename)
                {
                    typeDef = assemtype;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetTypeExFromName(IPexComponent host, string assemblyname, string typename, out TypeEx typeEx)
        {
            typeEx = null;
            AssemblyEx assembly;
            ReflectionHelper.TryLoadAssemblyEx(assemblyname, out assembly);
            return TryGetTypeExFromName(host, assembly, typename, out typeEx);
        }


        static SafeDictionary<string, TypeEx> typeExCache = new SafeDictionary<string, TypeEx>();
        public static bool TryGetTypeExFromName(IPexComponent host, AssemblyEx assembly, string typename, out TypeEx typeEx)
        {
            if(typeExCache.TryGetValue(typename, out typeEx))
                return true;

            typeEx = null;         
            foreach (var assemtype in assembly.TypeDefinitions)
            {
                if (assemtype.FullName == typename)
                {
                    typeEx = assemtype.Instantiate(GetGenericTypeParameters(host, assemtype));
                    typeExCache.Add(typename, typeEx);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Given a type definition, this function gets the generic parameters
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static TypeEx[] GetGenericTypeParameters(IPexComponent host, TypeDefinition tdef)
        {
            TypeEx[] typErr = new TypeEx[tdef.GenericTypeParametersCount];
            TypeEx inttype = MetadataFromReflection.GetType(typeof(int));            
            int count = 0;
            foreach (var genp in tdef.GenericTypeParameters)
            {
                TypeEx predefTypeEx;
                if (PreDefinedGenericClasses.TryGetInstantiatedClass(host, genp.Name, tdef, out predefTypeEx))
                    typErr[count] = predefTypeEx;
                else
                    typErr[count] = inttype;
                count++;
            } 

            return typErr;
        }

        /// <summary>
        /// Given a method definition, this function gets the generic parameters
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static TypeEx[] GetGenericMethodParameters(IPexComponent host, MethodDefinition mdef)
        {
            TypeEx[] typErr = new TypeEx[mdef.GenericMethodParametersCount];
            TypeEx inttype = MetadataFromReflection.GetType(typeof(int));
            int count = 0;
            foreach (var genp in mdef.GenericMethodParameters)
            {
                TypeEx predefTypeEx;
                if (PreDefinedGenericClasses.TryGetInstantiatedClass(host, genp.Name, null, out predefTypeEx))
                    typErr[count] = predefTypeEx;
                else
                    typErr[count] = inttype;
                count++;
            }

            return typErr;
        }      

        /// <summary>
        /// Retrieves the method object back from the signature
        /// </summary>
        /// <param name="typename"></param>
        /// <param name="methodsignature"></param>
        /// <returns></returns>
        public static bool TryGetFieldDefinition(IPexComponent host, string assemblyname, string typename,
            string fieldname, out FieldDefinition fielddef)
        {
            fielddef = null;
            TypeDefinition typeDef = null;
            try
            {
                bool result = TryGetTypeDefinitionFromName(host, assemblyname, typename, out typeDef);
                if (typeDef == null || !result)
                    return false;
            }
            catch (Exception)
            {
                return false;
            }

            foreach (var definition in typeDef.DeclaredInstanceFields)
            {
                if (definition.FullName == fieldname)
                {
                    fielddef = definition;
                    return true;
                }
            }

            foreach (var definition in typeDef.DeclaredStaticFields)
            {
                if (definition.FullName == fieldname)
                {
                    fielddef = definition;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Retrieves the field from the given fields
        /// </summary>
        /// <param name="typename"></param>
        /// <param name="methodsignature"></param>
        /// <returns></returns>
        public static bool TryGetField(IPexComponent host, string assemblyname, string typename,
            string fieldname, out Field field)
        {
            field = null;
            FieldDefinition fdef;
            if (!TryGetFieldDefinition(host, assemblyname, typename, fieldname, out fdef))
                return false;

            TypeDefinition tdef;
            if (!fdef.TryGetDeclaringType(out tdef))
                return false;

            field = fdef.Instantiate(GetGenericTypeParameters(host, tdef));
            return true;
        }        
    }
}
