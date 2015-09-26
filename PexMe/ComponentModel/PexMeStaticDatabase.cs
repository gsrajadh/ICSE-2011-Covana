using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using Microsoft.ExtendedReflection.Logging;
using PexMe.PersistentStore;
using System.Reflection.Emit;
using PexMe.Common;
using PexMe.ComponentModel.Hardcoded;
using PexMe.ComponentModel;

namespace PexMe.Core
{ 
    /// <summary>
    /// Mainly for serializing and de-serializing
    /// </summary>
    [Serializable]
    public class PexMeStaticDatabaseStore
    {        
        public SafeDictionary<int, MethodEffects> methodEffectsDictionary = new SafeDictionary<int, MethodEffects>();
        public SafeDictionary<Method, MethodStore> methodDic = new SafeDictionary<Method, MethodStore>();
        public SafeDictionary<Field, FieldStore> fieldDic = new SafeDictionary<Field, FieldStore>();
        public SafeDictionary<TypeDefinition, TypeStore> typeDic = new SafeDictionary<TypeDefinition, TypeStore>();
    }

    /// <summary>
    /// Stores the entire statically identified data.
    /// </summary>
    public class PexMeStaticDatabase
        : PexComponentBase, IPexMeStaticDatabase
    {
        public PexMeStaticDatabaseStore psds;       

        public PexMeStaticDatabase()
        {
            this.psds = new PexMeStaticDatabaseStore();
            PexMeDumpReader.TryLoadStaticDatabase(this);
        }

        /// <summary>
        /// Stores statically computed effects
        /// </summary>                
        public SafeDictionary<int, MethodEffects> MethodEffectsDic
        {
            get
            {
                return psds.methodEffectsDictionary;
            }
        }

        /// <summary>
        /// Stores the mapping from methods to fields
        /// </summary>        
        
        public SafeDictionary<Method, MethodStore> MethodDictionary
        {
            get
            {
                return psds.methodDic;
            }
            set
            {
                psds.methodDic = value;
            }
        }

        /// <summary>
        /// Stores the mapping from fields to methods
        /// </summary>                
        public SafeDictionary<Field, FieldStore> FieldDictionary
        {
            get
            {
                return psds.fieldDic;
            }
            set
            {
                psds.fieldDic = value;
            }
        }


        public SafeDictionary<TypeDefinition, TypeStore> TypeDictionary
        {
            get
            {
                return psds.typeDic;
            }
            set
            {
                psds.typeDic = value;
            }
        }

        internal string assemblyName;
        /// <summary>
        /// Stores the name of the assembly
        /// </summary>        
        public string AssemblyName
        {
            get
            {
                if (assemblyName == null)
                {
                    assemblyName = this.Services.CurrentAssembly.Assembly.Assembly.ShortName;
                }
                return assemblyName;
            }

            set
            {
                this.assemblyName = value;
            }
        }

        /// <summary>
        /// Stores the name of the assembly
        /// </summary>        
        public AssemblyEx CurrAssembly
        {
            get
            {
                return this.Services.CurrentAssembly.Assembly.Assembly;
            }
        }

        /// <summary>
        /// Checks whether a given method is an observer or not
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public bool IsAnObserver(Method method)
        {
            TypeEx declaringType;
            if (!method.TryGetDeclaringType(out declaringType))
                return false;

            MethodEffects me;
            if (!MethodOrFieldAnalyzer.TryComputeMethodEffects(this, declaringType, method,
                null, out me))
                return false;

            if (me.WrittenInstanceFields.Count == 0)
                return true;

            return false;
        }

        /// <summary>
        /// Gets calling methods of a given called method on the given field in a given target type
        /// </summary>
        /// <returns></returns>
        public bool TryGetCallingMethodsInType(Method calledMethod, Field field, TypeEx targetType, out SafeSet<Method> callingMethods)
        {
            SafeDebug.AssumeNotNull(calledMethod, "method");
            SafeDebug.AssumeNotNull(targetType, "type");

            //Get the associated property of the field            
            Property property;
            if (!MethodOrFieldAnalyzer.TryGetPropertyReadingField(this, targetType, field, out property))
            {
                //TODO: error;
            }

            MethodStore mstore = null;
            if (this.MethodDictionary.TryGetValue(calledMethod, out mstore))
            {
                if (mstore.CallingMethods.TryGetValue(targetType, out callingMethods))
                    return true;
            }

            //No method store found. create a fresh one
            if (mstore == null)
            {
                mstore = new MethodStore();
                mstore.methodName = calledMethod;                
                this.MethodDictionary[calledMethod] = mstore;
            }

            callingMethods = new SafeSet<Method>();
            mstore.CallingMethods[targetType] = callingMethods;
            TypeEx calledMethodType;
            if (!calledMethod.TryGetDeclaringType(out calledMethodType))
            {
                this.Log.LogError(WikiTopics.MissingWikiTopic, "callingmethods",
                    "Failed to get the declaring type for the method " + calledMethod.FullName);
                return false;
            }
            
            //Needs to addess the array type issue over here.
            var targetdef = targetType.Definition;
            if (targetdef == null)
            {
                if (targetType.TypeKind != TypeKind.SzArrayElements)
                {
                    this.Log.LogError(WikiTopics.MissingWikiTopic, "callingmethods",
                        "The definition for the type " + targetType.FullName + " is null");
                    return false;
                }
                else
                {
                    targetdef = targetType.ElementType.Definition;
                }
            }

            //Analyze each method in the given type to identify the calling methods
            //of the given method
            foreach (var typeMethod in targetdef.DeclaredInstanceMethods)
            {
                Method minstance = typeMethod.Instantiate(targetType.GenericTypeArguments, MethodOrFieldAnalyzer.GetGenericMethodParameters(this, typeMethod));

                MethodEffects meffects;
                if (!MethodOrFieldAnalyzer.TryComputeMethodEffects(this, targetType, minstance, null, out meffects))
                    continue;

                //Check for a direct comparison
                if (meffects.DirectCalledMethods.Contains(calledMethod))
                    callingMethods.Add(minstance);
                else
                {
                    //Use vtable lookup for addressing the abstract issues
                    foreach (var dcallMethod in meffects.DirectCalledMethods)
                    {
                        if (dcallMethod.IsConstructor)
                            continue;

                        TypeEx dcallMethodType;
                        if (dcallMethod.TryGetDeclaringType(out dcallMethodType))
                        {
                            if (dcallMethodType == calledMethodType || !dcallMethodType.IsAbstract || !dcallMethodType.IsInterface)
                                continue;

                            if (!dcallMethodType.IsAssignableTo(calledMethodType) && !calledMethodType.IsAssignableTo(dcallMethodType))
                                continue;
                        }

                        try
                        {
                            var lookupMethod = calledMethodType.VTableLookup(dcallMethod);
                            if (lookupMethod != null && calledMethod == lookupMethod)
                            {
                                callingMethods.Add(minstance);
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            this.Log.LogWarningFromException(ex, WikiTopics.MissingWikiTopic,
                                "vtablelookup", "Failed to perform vtablelookup for " + dcallMethod.FullName + " in type " + calledMethodType.FullName);
                        }
                    }
                }
            }       

            //Check whether there are any private methods in calling methods.
            //If yes, replace them with their callers.
            if (!PexMeConstants.DISABLE_CALLING_METHODS_WITHIN_CLASS)
            {
                var newCallingMethods = new SafeSet<Method>();
                foreach (var callingM in callingMethods)
                {
                    if (callingM.Definition.DeclaredVisibility != Visibility.Private || callingM.IsConstructor)
                    {
                        newCallingMethods.Add(callingM);
                        continue;
                    }
                       
                    //Get other calling methods within this type
                    SafeSet<Method> localCM;
                    if (this.TryGetCallingMethodsInType(callingM, field, targetType, out localCM))
                    {
                        newCallingMethods.AddRange(localCM);
                    }
                }

                callingMethods.Clear();
                callingMethods.AddRange(newCallingMethods);
            }

            //Needs to further analyze parent types
            TypeEx baseType = targetType.BaseType;             
            if (baseType != null && baseType.FullName != "System.Object")   //TODO: Avoid string comparisons. needs to figure out how to do that
            {
                SafeSet<Method> baseCallingMethods;
                TryGetCallingMethodsInType(calledMethod, field, baseType, out baseCallingMethods);
                callingMethods.AddRange(baseCallingMethods);
            }

            return true;
        }

        /// <summary>
        /// Includes filtering of the methods based on desired format
        /// </summary>
        /// <returns></returns>
        public bool TryGetFilteredWriteMethods(Field field, TypeEx declaringType, FieldModificationType desiredfmt, out SafeSet<Method> writeMethods)
        {
            //Check in the predefined store before proceeding
            if (PreDefinedMethodEffects.TryGetWriteMethods(this, field, desiredfmt, out writeMethods))
            {
                SafeSet<Method> newWriteMethods = new SafeSet<Method>();
                GetCallingMethods(field, declaringType, writeMethods, newWriteMethods);
                
                //Filter those methods whose actual types are abstract
                SafeSet<Method> returnMethods = new SafeSet<Method>();
                foreach (Method m in writeMethods)
                {
                    TypeEx declType;
                    if (m.TryGetDeclaringType(out declType) && !declType.IsAbstract && !declType.IsInterface)
                    {
                        returnMethods.Add(m);
                    }
                }

                foreach (Method m in newWriteMethods)
                {
                    TypeEx declType;
                    if (m.TryGetDeclaringType(out declType) && !declType.IsAbstract && !declType.IsInterface)
                    {
                        returnMethods.Add(m);
                    }
                }

                writeMethods.Clear();
                writeMethods.AddRange(returnMethods);
                return true;
            }

            //Identify those method that directly modify this field
            if (!TryGetWriteMethods(field, declaringType, out writeMethods))
                return false;

            //C# supports properties. In that case, the property setter can also
            //be a viable method. add property setter and its callers also
            Property property;
            if (MethodOrFieldAnalyzer.TryGetPropertyModifyingField(this, declaringType, field, out property))
            {
                if (property.IsVisible(VisibilityContext.Exported))
                    writeMethods.Add(property.Setter);

                var newWriteMethods = new SafeSet<Method>();
                GetCallingMethods(field, declaringType, writeMethods, newWriteMethods);
                writeMethods.AddRange(newWriteMethods);
            }

            writeMethods = FilterWriteMethodsOfUpdateType(field, desiredfmt, writeMethods);
            return true;
        }

        /// <summary>
        /// Gets the set of write methods associated with a field in the given type
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public bool TryGetWriteMethods(Field field, TypeEx declaringType, out SafeSet<Method> writeMethods)
        {
            SafeDebug.AssumeNotNull(field, "field");
            SafeDebug.AssumeNotNull(declaringType, "declaringType");

            //Declaring type should include defintinion
            if (declaringType == null || declaringType.Definition == null)
            {
                this.Log.LogWarning(WikiTopics.MissingWikiTopic, "WriteMethods",
                    "Missing definition for the declaring type " + declaringType.FullName);
                writeMethods = null;
                return false;
            }

            FieldStore fs;
            if (this.FieldDictionary.TryGetValue(field, out fs))
            {
                if (fs.WriteMethods.TryGetValue(declaringType, out writeMethods))
                    return true;
                else
                {
                    writeMethods = new SafeSet<Method>();
                    fs.WriteMethods[declaringType] = writeMethods;
                }
            }
            else
            {
                fs = new FieldStore();
                fs.FieldName = field;
                writeMethods = new SafeSet<Method>();
                fs.WriteMethods[declaringType] = writeMethods;
                this.FieldDictionary[field] = fs;
            }            

            foreach (MethodDefinition instanceMethod in declaringType.Definition.DeclaredInstanceMethods)
            {
                //Ignore abstract methods
                if (instanceMethod.IsAbstract)
                    continue;
                
                //If the DISABLE_CALLING_METHODS_WITHIN_CLASS is set to true, then private methods need not be considered
                //since they will be filtered out.
                if (PexMeConstants.DISABLE_CALLING_METHODS_WITHIN_CLASS && instanceMethod.DeclaredVisibility == Visibility.Private)
                    continue;

                Method method = instanceMethod.Instantiate(declaringType.GenericTypeArguments, MethodOrFieldAnalyzer.GetGenericMethodParameters(this, instanceMethod));
                MethodEffects me;

                if (!MethodOrFieldAnalyzer.TryComputeMethodEffects(this, declaringType, method, null, out me))
                {
                    this.Log.LogWarning(WikiTopics.MissingWikiTopic, "methodeffects",
                        "Failed to get the method effects for method " + method);
                    continue;
                }

                if (me.WrittenInstanceFields.Contains(field.ShortName))
                {
                    writeMethods.Add(method);
                }

                FieldModificationType fmt;
                if (me.ModificationTypeDictionary.TryGetValue(field.ShortName, out fmt))
                {
                    fs.ModificationTypeDictionary[method] = fmt;
                }
            }

            //Check whether there are any private methods in calling methods.
            //If yes, replace them with their callers.
            if (!PexMeConstants.DISABLE_CALLING_METHODS_WITHIN_CLASS)
            {
                var newWriteMethods = new SafeSet<Method>();
                GetCallingMethods(field, declaringType, writeMethods, newWriteMethods);

                writeMethods.Clear();
                writeMethods.AddRange(newWriteMethods);
            }

            TypeEx baseType = declaringType.BaseType;
            if (baseType != null && baseType.FullName != "System.Object")
            {
                SafeSet<Method> innerWriteMethods;
                this.TryGetWriteMethods(field, baseType, out innerWriteMethods);
                if (innerWriteMethods != null)
                    writeMethods.AddRange(innerWriteMethods);
            }  
                     
            return true;
        }

        private void GetCallingMethods(Field field, TypeEx declaringType, SafeSet<Method> writeMethods, SafeSet<Method> newWriteMethods)
        {
            foreach (var writeM in writeMethods)
            {
                if (writeM.Definition.DeclaredVisibility != Visibility.Private || writeM.IsConstructor)
                {
                    newWriteMethods.Add(writeM);
                    continue;
                }

                //Get other calling methods within this type
                SafeSet<Method> localCM;
                if (this.TryGetCallingMethodsInType(writeM, field, declaringType, out localCM))
                {
                    newWriteMethods.AddRange(localCM);
                }
            }
        }

        /// <summary>
        /// Given a set of write methods, this method basically filters them out
        /// to create a new set. Also filters the method that belongs to the interface type
        /// </summary>
        /// <param name="field"></param>
        /// <param name="fieldModificationType"></param>
        /// <param name="writeMethods"></param>
        /// <returns></returns>
        private SafeSet<Method> FilterWriteMethodsOfUpdateType(Field field,
            FieldModificationType fieldModificationType, SafeSet<Method> writeMethods)
        {
            FieldStore fs;
            if (!this.FieldDictionary.TryGetValue(field, out fs))
                return writeMethods;

            SafeSet<Method> returnSet = new SafeSet<Method>();
            foreach (var method in writeMethods)
            {
                //no need of methods in interfaces or abstract classes.
                TypeEx declType;
                if (method.TryGetDeclaringType(out declType) && (declType.IsAbstract || declType.IsInterface))
                {
                    continue;
                }

                //We currently allow unknown types
                FieldModificationType fmt;

                //Problem of static analysis impreciseness. If no modificuatui
                if (!fs.ModificationTypeDictionary.TryGetValue(method, out fmt))
                    returnSet.Add(method);

                if (fmt == fieldModificationType || fmt == FieldModificationType.UNKNOWN || fmt == FieldModificationType.METHOD_CALL)
                    returnSet.Add(method);
            }

            return returnSet;
        }

        internal SafeSet<Method> FilterCallingMethodsBasedOnField(Method tmw, Field field, 
            SafeSet<Method> callingMethods)
        {
            SafeSet<Method> filteredMethods = new SafeSet<Method>();

            foreach (var callingm in callingMethods)
            {
                //Filter the calling method based on the field
                MethodBodyEx body;
                if (!callingm.TryGetBody(out body) || !body.HasInstructions)
                {
                    continue;
                }

                int offset = 0;
                Instruction instruction;
                bool bContinueWithNextMethod = false;
                Field lastAccessedField = null;
                while (body.TryGetInstruction(offset, out instruction) && !bContinueWithNextMethod)
                {
                    SafeDebug.AssumeNotNull(instruction, "instruction");
                    OpCode opCode = instruction.OpCode;
                    if (opCode == OpCodes.Ldfld || opCode == OpCodes.Ldflda)
                    {
                        SafeDebug.Assume(opCode.OperandType == OperandType.InlineField, "opCode.OperandType == OperandType.InlineField");
                        lastAccessedField = instruction.Field;
                    } else if (opCode == OpCodes.Call || opCode == OpCodes.Callvirt)
                    {
                        SafeDebug.Assume(opCode.OperandType == OperandType.InlineMethod, "opCode.OperandType == OperandType.InlineMethod");
                        Method methodinner = instruction.Method;

                        if (methodinner == tmw && field == lastAccessedField)
                        {
                            filteredMethods.Add(callingm);
                            bContinueWithNextMethod = true;
                        }
                        lastAccessedField = null;
                    }                   
                    offset = instruction.NextOffset;
                }
            }
            return filteredMethods;
        }

        /// <summary>
        /// Flag that prevents loading hierarchy more than once
        /// </summary>
        private bool isInheritanceHierarchyLoaded = false;
        /// <summary>
        /// Loads all inheritance hierarchies in the current library
        /// into TypeDictionary in the form of a tree suitable for traversal.
        /// </summary>
        public void LoadInheritanceHierarchies()
        {
            if (isInheritanceHierarchyLoaded)
                return;

            isInheritanceHierarchyLoaded = true;
            AssemblyEx currAssembly = this.Services.CurrentAssembly.Assembly.Assembly;
            
            foreach (var tdef in currAssembly.TypeDefinitions)
            {                
                TypeStore currTypeStore;
                if (!this.TypeDictionary.TryGetValue(tdef, out currTypeStore))
                {
                    currTypeStore = new TypeStore(tdef);
                    this.TypeDictionary.Add(tdef, currTypeStore);
                }
                var baseType = tdef.BaseTypeDefinition;
                
                //This restricts our implementation to a single assembly for time being.
                if (baseType != null && baseType.Module.Assembly == currAssembly)
                {                    
                    TypeStore baseTypeStore;
                    if (!this.TypeDictionary.TryGetValue(baseType, out baseTypeStore))
                    {
                        baseTypeStore = new TypeStore(baseType);
                        this.TypeDictionary.Add(baseType, baseTypeStore);
                    }
                    baseTypeStore.ExtendingTypes.Add(currTypeStore);
                }

                try
                {
                    var typeEx = tdef.Instantiate(MethodOrFieldAnalyzer.GetGenericTypeParameters(this, tdef));
                    if (typeEx != null)
                    {
                        foreach (var ifTypeEx in typeEx.DeclaredInterfaces)
                        {
                            var parentIfDef = ifTypeEx.Definition;
                            if (parentIfDef.Module.Assembly != currAssembly)
                                continue;

                            TypeStore ifTypeStore;
                            if (!this.TypeDictionary.TryGetValue(parentIfDef, out ifTypeStore))
                            {
                                ifTypeStore = new TypeStore(parentIfDef);
                                this.TypeDictionary.Add(parentIfDef, ifTypeStore);
                            }
                            ifTypeStore.ExtendingTypes.Add(currTypeStore);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.Log.LogWarningFromException(ex, WikiTopics.MissingWikiTopic, "InheritanceLoader",
                        "Failed to instantiate class " + tdef.FullName.ToString());
                }
            }           
        }
    }
}
