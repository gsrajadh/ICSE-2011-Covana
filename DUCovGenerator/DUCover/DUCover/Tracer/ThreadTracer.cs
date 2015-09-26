// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Monitoring;
using Microsoft.ExtendedReflection.Utilities;
using Microsoft.ExtendedReflection.Utilities.Safe;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using System.Collections;
using DUCover.Core;
using Microsoft.ExtendedReflection.Collections;
using NLog;
using PexMe.Common;
using DUCover.Static;
using DUCover.SideEffectAnalyzer;

namespace DUCover.Core
{
    /// <summary>
    /// For each thread of the monitored application, 
    /// an instance of this class is created by the <see cref="Tracer"/>.
    /// </summary>
    [__DoNotInstrument]
    public sealed class ThreadTracer :
        ThreadExecutionMonitorEmpty
    {
        /// <summary>
        /// Initializing logger
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Flag that decides when to start monitoring
        /// </summary>
        bool bStartMonitoring = false;
        string currentAssemblyLoc = System.Environment.GetEnvironmentVariable(DUCoverConstants.DUCoverAssemblyVar);

        SafeStack<Method> methodStack = new SafeStack<Method>();
        SafeDictionary<int, FieldDefUseStore> lastFieldDefsDic = new SafeDictionary<int, FieldDefUseStore>();
        
        /// <summary>
        /// Represents the last loaded fields inside a method. Needs to be kept in a stack for every entry and exit of a method
        /// </summary>
        SafeList<Field> lastLoadedFields = new SafeList<Field>();
        SafeStack<SafeList<Field>> lastLoadedFieldsStack = new SafeStack<SafeList<Field>>();
        SideEffectStore ses = SideEffectStore.GetInstance();
        DUCoverStore dcs;        

        public ThreadTracer(int threadId)
            : base(threadId)
        {
            this.dcs = DUCoverStore.GetInstance();
            logger.Info("DUCover Tracer initialized");
        }

        #region tracing
        private void trace(string formatString, params object[] args)
        {
            trace(SafeString.Format(formatString, args));
        }

        static readonly object traceLock = new object();
        private void trace(string message)
        {
            lock (traceLock)
            {
                Console.Write("{0}[{1}] ", this.indentString, this.ThreadId);
                Console.WriteLine(message);
            }
        }

        int level;
        string indentString;
        private void indent(int delta)
        {
            this.level += delta;
            if (this.level < 0) this.level = 0;
            this.indentString = StringHelper.GetSpaces(this.level);
        }
        #endregion

        #region Callbacks that indicate major control-flow events
        public override EnterMethodFlags EnterMethod(Method method)
        {
            //Console.WriteLine(method.FullName);

            if (this.methodStack.Count == 0 && !bStartMonitoring)
            { 
                var assemblyShortname = method.Definition.Module.Assembly.ShortName;
                if (assemblyShortname == this.currentAssemblyLoc)
                {
                    //Whereever the main method, it is the assembly under analysis,
                    //since our tool assumes that there is only one assembly
                    var assembly = method.Definition.Module.Assembly;
                    DUCoverMain.Initialize(assembly);
                    bStartMonitoring = true;
                }
            }

            if (bStartMonitoring)
            {
                this.methodStack.Push(method);
                var tempLastLoadedFields = new SafeList<Field>();
                tempLastLoadedFields.AddRange(lastLoadedFields);
                this.lastLoadedFieldsStack.Push(tempLastLoadedFields);
                this.lastLoadedFields.Clear();
            }

            return base.EnterMethod(method); // 'true' indicates that we want callbacks for the argument values
        }     

        public override void LeaveMethod()
        {
            if (!bStartMonitoring)
                return;

            if (this.methodStack.Count == 0)
                return;

            var poppedMethod = this.methodStack.Pop();
            var previousLoadedFields = this.lastLoadedFieldsStack.Pop();
            this.lastLoadedFields.Clear();
            this.lastLoadedFields.AddRange(previousLoadedFields);

            if (poppedMethod.ShortName.Equals("Main") || poppedMethod.ShortName.Equals("DUCoverTerminate"))
            {
                DUCoverMain.Terminate();
                //bStartMonitoring = false;
            }
        }

        public override void Call(Method method)
        {
            if (!bStartMonitoring)
                return; 

            this.HandleMethodCall(method);
        }

        public override void Callvirt(Method method)
        {
            if (!bStartMonitoring)
                return; 

            this.HandleMethodCall(method);
        }

        int currOffset;
        public override void AtOffset(int offset)
        {
            base.AtOffset(offset);
            this.currOffset = offset;
        }

        /// <summary>
        /// Handles use of the field
        /// </summary>
        private void HandleFieldUse(Field field)
        {
            FieldDefUseStore llfs;
            //Check if it is a primitive field
            if (PexMeFilter.IsPrimitiveType(field.Type))
            {
                UpdateDUCoverTable(field, out llfs);                
            }
            else
            {
                //If the current method is a getter method, then just update the entry
                var currMethod = this.GetCurrentMethod();
                if (currMethod.ShortName.StartsWith("get_"))
                {                    
                    this.UpdateDUCoverTable(field, out llfs);
                }

                //Handling non-primitive fields are postponed till the branching decision or function call
                this.lastLoadedFields.Add(field);
            }
        }

        private bool UpdateDUCoverTable(Field field, out FieldDefUseStore llfs)
        {
            if (this.lastFieldDefsDic.TryGetValue(field.GlobalIndex, out llfs))
            {
                TypeEx declType;
                if (!field.TryGetDeclaringType(out declType))
                {
                    logger.Error("Failed to retrieve declaring type for the field " + field.FullName);
                    return false;
                }

                DeclClassEntity dce;
                if (!this.dcs.DeclEntityDic.TryGetValue(declType.FullName, out dce))
                {
                    logger.Error("Failed to retrieve DeclClassEntity for the class " + declType.FullName);
                    return false;
                }

                DeclFieldEntity dfe;
                if (!dce.FieldEntities.TryGetValue(field.FullName, out dfe))
                {
                    logger.Error("Failed to retrieve DeclFieldEntity for the field " + field.FullName);
                    return false;
                }

                return dfe.UpdateDUCoverageTable(llfs.Method, llfs.Offset, this.GetCurrentMethod(), this.currOffset);
            }
            else
            {
                //logger.Warn("Encountered load field on " + field.FullName + " at " + this.GetCurrentMethod().FullName
                //    + "(" + this.currOffset + ") without any field definition");
                return false;
            }            
        }

        /// <summary>
        /// Handles a method call.
        /// </summary>
        /// <param name="method"></param>
        private void HandleMethodCall(Method method)
        {
            if (lastLoadedFields.Count != 0)
            {
                bool bSuccessfulUpdate = true;
                //handling method calls on fields of this class
                if (method.ShortName.StartsWith("set_"))
                {
                    //Update definition table with the current offsets these fields are modified here
                    foreach (var field in lastLoadedFields)
                    {
                        FieldDefUseStore fdus = new FieldDefUseStore(field, this.GetCurrentMethod(), this.currOffset);
                        this.lastFieldDefsDic[field.GlobalIndex] = fdus;
                    }
                }
                else if (method.ShortName.StartsWith("get_"))
                {
                    //Register all usages
                    foreach (var field in lastLoadedFields)
                    {
                        FieldDefUseStore fdus;
                        this.UpdateDUCoverTable(field, out fdus);
                    }
                }
                else
                {                    
                    foreach (var field in lastLoadedFields)
                    {
                        bool defined, used;
                        if (ses.TryGetFieldDefOrUseByMethod(this.GetCurrentMethod(), field, this.currOffset, out defined, out used))
                        {
                            FieldDefUseStore fdus;
                            if (used)
                            {
                                //This field is used by this method
                                if (this.lastFieldDefsDic.ContainsKey(field.GlobalIndex))
                                {
                                    this.UpdateDUCoverTable(field, out fdus);
                                }
                            }

                            if (defined)
                            {
                                //This field is defined by this method
                                fdus = new FieldDefUseStore(field, this.GetCurrentMethod(), this.currOffset);
                                this.lastFieldDefsDic[field.GlobalIndex] = fdus;

                                if (used)
                                {
                                    //Since it is both used and defined.
                                    this.UpdateDUCoverTable(field, out fdus);
                                }
                            }
                        }
                        else
                        {
                            //Backup option: declaring as both used and defined.
                            //In general, control cannot come over here
                            FieldDefUseStore fdus;
                            if (this.lastFieldDefsDic.ContainsKey(field.GlobalIndex))
                            {
                                bSuccessfulUpdate = this.UpdateDUCoverTable(field, out fdus);
                            }

                            if (bSuccessfulUpdate)
                            {
                                //Override the current definition, since this field is being modified here
                                fdus = new FieldDefUseStore(field, this.GetCurrentMethod(), this.currOffset);
                                this.lastFieldDefsDic[field.GlobalIndex] = fdus;

                                //also add a self def-use
                                this.UpdateDUCoverTable(field, out fdus);
                            }
                        }
                    }
                }

                if(bSuccessfulUpdate)
                    lastLoadedFields.Clear();
            }
            else
            {
                var shortname = method.ShortName;
                if (shortname.StartsWith("set_"))
                {
                    Field definedField;
                    if (DeclEntityCollector.TryGetFieldOfSetter(method, out definedField))
                    {
                        //Override if there is any previous definition of this field
                        FieldDefUseStore fdus = new FieldDefUseStore(definedField, this.GetCurrentMethod(), this.currOffset);
                        this.lastFieldDefsDic[definedField.GlobalIndex] = fdus;
                    }
                }
                else if (shortname.StartsWith("get_"))
                {
                    int foffset;
                    Field usedField;
                    if (DeclEntityCollector.TryGetFieldOfGetter(method, out usedField, out foffset))
                    {
                        FieldDefUseStore llfs;
                        this.UpdateDUCoverTable(usedField, out llfs);
                        this.lastLoadedFields.Add(usedField);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the current method. bypasses if the current method is a setter or getter method corresponding
        /// to the property of the field
        /// </summary>
        /// <returns></returns>
        private Method GetCurrentMethod()
        {
            if (this.methodStack.Count == 0)
            {
                logger.Error("MethodStack cannot be empty!!!");
                return null;
            }
            var currMethod = this.methodStack.Peek();
            var shortname = currMethod.ShortName;
            return currMethod;

            /*if (shortname.StartsWith("get_") || shortname.StartsWith("set_"))
            {
                //get the enclosing method instead of setter or getter
                var methodarr = this.methodStack.ToArray();
                currMethod = methodarr[this.methodStack.Count - 2];
            }*/
        }

        /// <summary>
        /// Methods related to loading fields
        /// </summary>
        /// <param name="field"></param>
        public override void Ldfld(Field field)
        {
            base.Ldfld(field);
            if (!bStartMonitoring)
                return; 
            this.HandleFieldUse(field);   
        }

        public override void Ldflda(Field field)
        {
            base.Ldflda(field);
            if (!bStartMonitoring)
                return; 
            this.HandleFieldUse(field);
        }

        /// <summary>
        /// Registers the last definition of the field
        /// </summary>
        /// <param name="field"></param>
        private void AddToFieldDefinitionStore(Field field)
        {
            var peekmethod = this.methodStack.Peek();
            //Set method is already been handled
            if (peekmethod.ShortName.StartsWith("set_") && this.lastFieldDefsDic.ContainsKey(field.GlobalIndex))
                return;
            FieldDefUseStore llfs = new FieldDefUseStore(field, this.GetCurrentMethod(), this.currOffset);            
            this.lastFieldDefsDic[field.GlobalIndex] = llfs;
        }
        
        /// <summary>
        /// Storing the field information
        /// </summary>
        /// <param name="field"></param>
        public override void Stfld(Field field)
        {
            base.Stfld(field);
            if (!bStartMonitoring)
                return; 
            this.AddToFieldDefinitionStore(field);
        }

        public override void Stsfld(Field field)
        {
            base.Stsfld(field);
            if (!bStartMonitoring)
                return; 
            this.AddToFieldDefinitionStore(field);                   
        }

        /**** Branching Stuff ******/
        private void HandleBranchingStatement(int codeLabel)
        {            
            foreach (var field in lastLoadedFields)
            {
                FieldDefUseStore fdus;
                if (this.lastFieldDefsDic.ContainsKey(field.GlobalIndex))
                {
                    this.UpdateDUCoverTable(field, out fdus);
                }                
            }
            
            this.lastLoadedFields.Clear();
        }

        public override void AtConditionalBranchFallthrough(int codeLabel)
        {
            base.AtConditionalBranchFallthrough(codeLabel);
            if (!bStartMonitoring)
                return; 
            this.HandleBranchingStatement(codeLabel);
        }

        public override void AtConditionalBranchTarget(int codeLabel)
        {
            base.AtConditionalBranchTarget(codeLabel);
            if (!bStartMonitoring)
                return; 
            this.HandleBranchingStatement(codeLabel);
        }

        public override void AtSwitchFallthrough(int codeLabel)
        {
            base.AtSwitchFallthrough(codeLabel);
            if (!bStartMonitoring)
                return; 
            this.HandleBranchingStatement(codeLabel);
        }

        public override void AtSwitchTarget(int index, int codeLabel)
        {
            base.AtSwitchTarget(index, codeLabel);
            if (!bStartMonitoring)
                return; 
            this.HandleBranchingStatement(codeLabel);
        }

        /**** Return statement stuff ******/
        public override void Ret()
        {
            base.Ret();
            if (!bStartMonitoring)
                return; 
            //Detected a return statement.
            foreach (var field in lastLoadedFields)
            {
                FieldDefUseStore fdus;
                if (this.lastFieldDefsDic.ContainsKey(field.GlobalIndex))
                {
                    this.UpdateDUCoverTable(field, out fdus);
                }
            }

            this.lastLoadedFields.Clear();
        }


        #endregion
    }
}
