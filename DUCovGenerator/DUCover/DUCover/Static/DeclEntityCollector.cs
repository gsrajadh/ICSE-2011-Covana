using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using System.Reflection;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using DUCover.Core;
using Microsoft.ExtendedReflection.Collections;
using PexMe.Core;
using System.Reflection.Emit;
using PexMe.Common;
using NLog;
using DUCover.SideEffectAnalyzer;
using PexMe.ComponentModel.Hardcoded;

namespace DUCover.Static
{
    /// <summary>
    /// Uses static analysis to collect all declared entities in the program
    /// </summary>
    [__DoNotInstrument]
    public class DeclEntityCollector
    {
        static DUCoverStore ade = null;
        /// <summary>
        /// Initializing logger
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Analyzes the given assembly statically and gathers all declared entities
        /// such as member variables and local variables
        /// </summary>
        /// <param name="assembly"></param>
        public static void CollectAllDeclEntitiesInAssembly(AssemblyEx assembly)
        {
            SafeDebug.AssumeNotNull(assembly, "assembly");
            logger.Debug("Beginning of static analysis");
            if (ade == null)
            {
                ade = DUCoverStore.GetInstance();
            }

            foreach (TypeDefinition td in assembly.TypeDefinitions)
            {
                CollectAllDeclEntitiesInTypeDef(td);          
            }            
        }        

        /// <summary>
        /// Collects all definitions and uses among each class in the assembly under analysis
        /// </summary>
        /// <param name="ade"></param>
        private static void CollectAllDefsAndUsesInTypeDef(TypeDefinition td, DeclClassEntity ade)
        {
            var host = DUCoverStore.GetInstance().Host;
            SideEffectStore ses = SideEffectStore.GetInstance();
            var psd = host.GetService<PexMeStaticDatabase>() as PexMeStaticDatabase;
            foreach (var constructor in td.DeclaredInstanceConstructors)
            {
                var method = constructor.Instantiate(MethodOrFieldAnalyzer.GetGenericTypeParameters(host, td),
                    MethodOrFieldAnalyzer.GetGenericMethodParameters(host, constructor));
                CollectDefsAndUsesInMethod(psd, td, ade, method, null, ses);
            }

            foreach (var mdef in td.DeclaredInstanceMethods)
            {
                try
                {
                    var method = mdef.Instantiate(MethodOrFieldAnalyzer.GetGenericTypeParameters(host, td),
                        MethodOrFieldAnalyzer.GetGenericMethodParameters(host, mdef));
                    CollectDefsAndUsesInMethod(psd, td, ade, method, null, ses);
                }
                catch (Exception ex)
                {
                    logger.ErrorException("Failed to instantiate method " + mdef.FullName, ex);
                }
            }

            foreach (var prop in td.DeclaredProperties)
            {
                var getter = prop.Getter;
                if (getter != null)
                {
                    Field usedField;
                    int foffset;
                    var method = getter.Instantiate(MethodOrFieldAnalyzer.GetGenericTypeParameters(host, td),
                        MethodOrFieldAnalyzer.GetGenericMethodParameters(host, getter));
                    if (TryGetFieldOfGetter(method, out usedField, out foffset))
                    {
                        DeclFieldEntity dfe;
                        if (ade.FieldEntities.TryGetValue(usedField.FullName, out dfe))
                        {
                            //Found an accessor. register the usage
                            dfe.AddToUseList(method, foffset);
                        }
                    }
                }

                var setter = prop.Setter;
                if (setter != null)
                {
                    var method = setter.Instantiate(MethodOrFieldAnalyzer.GetGenericTypeParameters(host, td),
                    MethodOrFieldAnalyzer.GetGenericMethodParameters(host, setter));
                    CollectDefsAndUsesInMethod(psd, td, ade, method, null, ses);
                }
            }
        }

        /// <summary>
        /// Collects defs and uses within a method
        /// </summary>
        /// <param name="td"></param>
        /// <param name="ade"></param>
        /// <param name="constructor"></param>
        private static void CollectDefsAndUsesInMethod(PexMeStaticDatabase psd, TypeDefinition td, DeclClassEntity ade, 
            Method method, SafeSet<Method> visitedMethods, SideEffectStore ses)
        {          
            try
            {                
                if (visitedMethods == null)
                    visitedMethods = new SafeSet<Method>();

                if (visitedMethods.Contains(method))
                {
                    return;
                }
                visitedMethods.Add(method);

                MethodBodyEx body;
                if (!method.TryGetBody(out body) || !body.HasInstructions)
                {
                    return;
                }
                                
                int offset = 0;
                Instruction instruction;
                OpCode prevOpcode = OpCodes.Nop;

                //Stack for load instructions
                Field lastAccessedArrayField = null;
                SafeList<Field> lastAccessedFieldList = new SafeList<Field>();
                
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
                            DeclFieldEntity dfe;
                            if (ade.FieldEntities.TryGetValue(field.FullName, out dfe))
                            {
                                //Found a definition of the field. register the definition
                                dfe.AddToDefList(method, offset);
                            }
                        }
                        else if (opCode == OpCodes.Ldfld || opCode == OpCodes.Ldflda)
                        {
                            SafeDebug.Assume(opCode.OperandType == OperandType.InlineField, "opCode.OperandType == OperandType.InlineField");
                            Field accessedField = instruction.Field;
                            if (PexMeFilter.IsPrimitiveType(accessedField.Type))
                            {
                                DeclFieldEntity dfe;
                                if (ade.FieldEntities.TryGetValue(accessedField.FullName, out dfe))
                                {
                                    //Found an accessor. register the usage
                                    dfe.AddToUseList(method, offset);
                                }                                
                                lastAccessedArrayField = null;
                            }
                            else
                            {
                                if (accessedField.Type.Spec == TypeSpec.SzArray)
                                {
                                    lastAccessedArrayField = accessedField;
                                }
                                else
                                {   
                                    //Any access needs to be registered as use 
                                    lastAccessedFieldList.Add(accessedField);                                    
                                }
                            }
                        }
                        else if (MethodOrFieldAnalyzer.StElemOpCodes.Contains(opCode))
                        {
                            if (lastAccessedArrayField != null)
                            {                                
                                DeclFieldEntity dfe;
                                if (ade.FieldEntities.TryGetValue(lastAccessedArrayField.FullName, out dfe))
                                {
                                    //Found a definition of the field. register the definition
                                    dfe.AddToDefList(method, offset);
                                }
                                lastAccessedArrayField = null;
                            }
                        }
                        else if (MethodOrFieldAnalyzer.BranchOpCodes.Contains(opCode))
                        {
                            if (lastAccessedFieldList.Count > 0)
                            {
                                //A field is loaded and used in conditional statement. We use the offset of conditional statement as the usage point
                                foreach (var field in lastAccessedFieldList)
                                {
                                    DeclFieldEntity dfe;
                                    if (ade.FieldEntities.TryGetValue(field.FullName, out dfe))
                                    {
                                        //Found a definition of the field. register the definition
                                        dfe.AddToUseList(method, offset);
                                    }
                                }
                                lastAccessedFieldList.Clear();
                            }

                            if (lastAccessedArrayField != null)
                            {
                                DeclFieldEntity dfe;
                                if (ade.FieldEntities.TryGetValue(lastAccessedArrayField.FullName, out dfe))
                                {
                                    //Found a definition of the field. register the definition
                                    dfe.AddToUseList(method, offset);
                                }
                                lastAccessedArrayField = null;
                            }
                        }
                        else if (opCode == OpCodes.Ret)
                        {
                            //A field is accessed and is returned from here
                            if (lastAccessedFieldList.Count > 0)
                            {                                
                                foreach (var field in lastAccessedFieldList)
                                {
                                    DeclFieldEntity dfe;
                                    if (ade.FieldEntities.TryGetValue(field.FullName, out dfe))
                                    {
                                        //Found a use of the field. register the definition
                                        dfe.AddToUseList(method, offset);
                                    }
                                }
                                lastAccessedFieldList.Clear();
                            }
                        }
                        else if (opCode == OpCodes.Call || opCode == OpCodes.Callvirt)
                        {
                            SafeDebug.Assume(opCode.OperandType == OperandType.InlineMethod, "opCode.OperandType == OperandType.InlineMethod");
                            Method methodinner = instruction.Method;
                            SafeDebug.AssumeNotNull(method, "method");

                            //If this condition is not satisfied, it could be a local method call, which is dealt separately
                            if (lastAccessedFieldList.Count != 0)
                            {
                                HandleMethodCall(psd, ade, method, offset, lastAccessedFieldList, methodinner, ses);
                                lastAccessedFieldList.Clear();
                            }
                            else
                            {
                                var shortname = methodinner.ShortName;
                                if (shortname.StartsWith("set_"))
                                {
                                    HandleSetterMethod(ade, method, offset, methodinner);
                                }
                                else if (shortname.StartsWith("get_"))
                                {
                                    HandleGetterMethod(ade, method, offset, methodinner, lastAccessedFieldList);
                                }
                            }
                        }
                    }
                    prevOpcode = opCode;
                    offset = instruction.NextOffset;
                }
                
            }
            catch (Exception ex)
            {
                logger.ErrorException("Exception thrown with static analysis " + ex.StackTrace, ex);
            }
        }

        /// <summary>
        /// Handles a method call
        /// </summary>
        /// <param name="psd"></param>
        /// <param name="ade"></param>
        /// <param name="method"></param>
        /// <param name="offset"></param>
        /// <param name="lastAccessedFieldList"></param>
        /// <param name="methodinner"></param>
        private static void HandleMethodCall(PexMeStaticDatabase psd, DeclClassEntity ade, Method method, int offset, 
            SafeList<Field> lastAccessedFieldList, Method methodinner, SideEffectStore ses)
        {
            SafeList<Field> fieldsProcessed = new SafeList<Field>();
            Field receiverField = null;

            //Check whether the current method call is actually on the field previously loaded                        
            TypeEx methodDeclType;
            if (!methodinner.TryGetDeclaringType(out methodDeclType))
            {
                logger.Error("Failed to get the declaring type of the method: " + methodinner.FullName);
                return;
            }

            if (methodDeclType.FullName == "System.String" || methodDeclType.FullName == "System.string")
            {
                return; //Do not process string types.
            }

            //Check whether the declaring type is in the fields list
            foreach (var field in lastAccessedFieldList)
            {
                var fieldType = field.Type;
                if (fieldType == methodDeclType || fieldType.IsAssignableTo(methodDeclType) || methodDeclType.IsAssignableTo(fieldType))
                {
                    fieldsProcessed.Add(field);
                    receiverField = field;
                    break;
                }
            }

            if (receiverField == null)
            {
                //Failed to identify the reciver field of this method
                return;
            }

            //Identify arguments of the current method call
            foreach (var argtype in methodinner.ParameterTypes)
            {
                foreach (var field in lastAccessedFieldList)
                {
                    if (field == receiverField)
                        continue;
                    
                    var fieldType = field.Type;
                    if (fieldType == argtype || fieldType.IsAssignableTo(argtype) || argtype.IsAssignableTo(fieldType))
                    {
                        fieldsProcessed.Add(field);                        
                        break;
                    }
                }
            }

            DUCoverStore dcs = DUCoverStore.GetInstance();
            //If the method is known, its side-effects are well-known
            if (methodinner.ShortName.StartsWith("set_"))
            {
                foreach (var processedField in fieldsProcessed)
                {
                    DeclFieldEntity dfe;
                    if (ade.FieldEntities.TryGetValue(processedField.FullName, out dfe))
                    {
                        dfe.AddToDefList(method, offset);
                    }
                    lastAccessedFieldList.Remove(processedField);
                }
            }
            else if (methodinner.ShortName.StartsWith("get_"))
            {
                foreach (var processedField in fieldsProcessed)
                {
                    DeclFieldEntity dfe;
                    if (ade.FieldEntities.TryGetValue(processedField.FullName, out dfe))
                    {
                        dfe.AddToUseList(method, offset);
                    }
                    lastAccessedFieldList.Remove(processedField);
                }
            }
            else
            {                
                //add all fiels to def or use, since we may not aware of their side-effects here
                foreach (var processedField in fieldsProcessed)
                {
                    DeclFieldEntity dfe;
                    if (ade.FieldEntities.TryGetValue(processedField.FullName, out dfe))
                    {                        
                        bool defined, used;
                        if(ses.TryGetFieldDefOrUseByMethod(method, processedField, offset, out defined, out used))
                        {
                            if(defined)
                                dfe.AddToDefList(method, offset);

                            if(used)
                                dfe.AddToUseList(method, offset);
                        }
                        else
                            dfe.AddToDefOrUseList(method, offset, methodinner);
                    }
                    lastAccessedFieldList.Remove(processedField);
                }
            }
        }

        private static void HandleGetterMethod(DeclClassEntity ade, Method method, int offset, Method methodinner, SafeList<Field> lastAccessedFieldList)
        {
            //found a use
            Field usedField;
            int foffset;
            if (TryGetFieldOfGetter(methodinner, out usedField, out foffset))
            {
                lastAccessedFieldList.Add(usedField);
                DeclFieldEntity dfe;
                if (ade.FieldEntities.TryGetValue(usedField.FullName, out dfe))
                {
                    dfe.AddToUseList(method, offset);
                }
            }
            else
            {
                logger.Warn("Failed to retrive field associated with the getter method: " + methodinner.FullName);
            }
        }

        private static void HandleSetterMethod(DeclClassEntity ade, Method method, int offset, Method methodinner)
        {
            //found a definition
            Field definedField;
            if (TryGetFieldOfSetter(methodinner, out definedField))
            {
                DeclFieldEntity dfe;
                if (ade.FieldEntities.TryGetValue(definedField.FullName, out dfe))
                {
                    dfe.AddToDefList(method, offset);
                }
            }
            else
            {
                logger.Warn("Failed to retrive field associated with the setter method: " + methodinner.FullName);
            }
        }

        /// <summary>
        /// Retries the field accessed in a getter method
        /// </summary>
        /// <param name="method"></param>
        /// <param name="usedField"></param>
        /// <returns></returns>
        public static bool TryGetFieldOfGetter(Method method, out Field usedField, out int foffset)
        {
            SafeDebug.Assert(method.ShortName.StartsWith("get_"), "");
            usedField = null;
            foffset = 0;
            MethodBodyEx body;
            if (!method.TryGetBody(out body) || !body.HasInstructions)
            {
                return false;
            }
            
            int offset = 0;
            Instruction instruction;
            OpCode prevOpcode = OpCodes.Nop;           
            while (body.TryGetInstruction(offset, out instruction))
            {
                SafeDebug.AssumeNotNull(instruction, "instruction");
                OpCode opCode = instruction.OpCode;
                if (opCode == OpCodes.Ldfld || opCode == OpCodes.Ldsfld || opCode == OpCodes.Ldflda || opCode == OpCodes.Ldsflda)
                {
                    usedField = instruction.Field;
                    foffset = offset;
                    return true;
                }
                offset = instruction.NextOffset;
            }

            return false;
        }

        /// <summary>
        /// Retrieves the defined field
        /// </summary>
        /// <param name="method"></param>
        /// <param name="definedField"></param>
        /// <returns></returns>
        public static bool TryGetFieldOfSetter(Method method, out Field definedField)
        {
            SafeDebug.Assert(method.ShortName.StartsWith("set_"), "");
            definedField = null;
            MethodBodyEx body;
            if (!method.TryGetBody(out body) || !body.HasInstructions)
            {
                return false;
            }

            int offset = 0;
            Instruction instruction;
            OpCode prevOpcode = OpCodes.Nop;
            while (body.TryGetInstruction(offset, out instruction))
            {
                SafeDebug.AssumeNotNull(instruction, "instruction");
                OpCode opCode = instruction.OpCode;
                if (opCode == OpCodes.Stfld || opCode == OpCodes.Stsfld)
                {
                    definedField = instruction.Field;
                    return true;
                }
                offset = instruction.NextOffset;
            }

            return false;
        }

        /// <summary>
        /// Collects all declared entities in a class
        /// </summary>
        /// <param name="td"></param>
        private static void CollectAllDeclEntitiesInTypeDef(TypeDefinition td)
        {           
            //Handle if there are more than one possibility for any generics defined by this td
            if (!PreDefinedGenericClasses.HasMoreOptionsForGenericName(td))
            {
                HandleTypeDef(td);
            }
            else
            {
                foreach (var definedType in PreDefinedGenericClasses.GetAllDefinedTypes(td))
                {
                    PreDefinedGenericClasses.recentAccessedTypes.Add(definedType.FullName);
                    HandleTypeDef(td);
                    PreDefinedGenericClasses.recentAccessedTypes.Remove(definedType.FullName);
                }
            }
        }

        /// <summary>
        /// Handles one single typedef
        /// </summary>
        /// <param name="td"></param>
        private static void HandleTypeDef(TypeDefinition td)
        {
            DeclClassEntity dce;
            ade.AddToDeclEntityDic(td, out dce);
            foreach (FieldDefinition fd in td.DeclaredInstanceFields)
            {
                dce.AddFieldEntity(td, fd);
            }

            foreach (FieldDefinition fd in td.DeclaredStaticFields)
            {
                dce.AddFieldEntity(td, fd);
            }

            CollectAllDefsAndUsesInTypeDef(td, dce);
        }
    }
}
