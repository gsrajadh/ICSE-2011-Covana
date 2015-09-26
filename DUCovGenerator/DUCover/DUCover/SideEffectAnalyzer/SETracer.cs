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

namespace DUCover
{
    /// <summary>
    /// For each thread of the monitored application, 
    /// an instance of this class is created by the <see cref="Tracer"/>.
    /// Traces the side effects
    /// </summary>
    [__DoNotInstrument]
    public sealed class SETracer :
        ThreadExecutionMonitorEmpty
    {
        /// <summary>
        /// Initializing logger
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

        SafeStack<Method> methodStack = new SafeStack<Method>();
        bool bStartMonitoring = false;

        SideEffectStore ses = SideEffectStore.GetInstance();
        SEMethodStore currSEMethodStore = null;
        SafeStack<SEMethodStore> methodStoreStack = new SafeStack<SEMethodStore>();

        /// <summary>
        /// Represents the last loaded fields inside a method. Needs to be kept in a stack for every entry and exit of a method
        /// </summary>
        SafeList<Field> lastLoadedFields = new SafeList<Field>();
        SafeStack<SafeList<Field>> lastLoadedFieldsStack = new SafeStack<SafeList<Field>>();

        string currentAssemblyLoc = System.Environment.GetEnvironmentVariable(DUCoverConstants.DUCoverAssemblyVar);

        /// <summary>
        /// Stores offsets and pops them
        /// </summary>
        SafeStack<int> offsetStack = new SafeStack<int>();

        public SETracer(int threadId)
            : base(threadId)
        {
            logger.Info("SideEffect Tracer initialized");
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
                    ses.CurrAssembly = assembly;
                    bStartMonitoring = true;
                }
            }

            if (bStartMonitoring)
            {
                this.methodStack.Push(method);

                if (currSEMethodStore != null)
                    this.methodStoreStack.Push(currSEMethodStore);
                currSEMethodStore = new SEMethodStore(method.FullName);
                currSEMethodStore.OptionalMethod = method;

                var tempLastLoadedFields = new SafeList<Field>();
                tempLastLoadedFields.AddRange(lastLoadedFields);
                this.lastLoadedFieldsStack.Push(tempLastLoadedFields);
                this.lastLoadedFields.Clear();

                this.offsetStack.Push(this.currOffset);
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
            this.currOffset = this.offsetStack.Pop();

            var previousLoadedFields = this.lastLoadedFieldsStack.Pop();
            //If the last method call is a getter, add the field just loaded by that getter method
            //Or any other remaining loaded fields
            if (poppedMethod.ShortName.StartsWith("get_"))
            {
                Field usedField;
                int foffset;
                if (DeclEntityCollector.TryGetFieldOfGetter(poppedMethod, out usedField, out foffset))
                {
                    previousLoadedFields.AddRange(usedField);
                }
            }
            this.lastLoadedFields.Clear();
            this.lastLoadedFields.AddRange(previousLoadedFields);

            //Updating the defined and used field list
            if (this.methodStoreStack.Count > 0)
            {
                ses.AppendMethodStore(currSEMethodStore);
                var poppedStore = this.methodStoreStack.Pop();
                if (!poppedMethod.ShortName.StartsWith("get_"))
                {
                    //Do not propogate for getter methods
                    PropagateModificationsToCaller(poppedStore, currSEMethodStore, poppedMethod);
                }
                poppedStore.AppendMethodStore(currSEMethodStore, this.currOffset);
                currSEMethodStore = poppedStore;
            }

            if (poppedMethod.ShortName.Equals("Main") || poppedMethod.ShortName.Equals("DUCoverTerminate"))
            {
                ses.DumpToDatabase();
                bStartMonitoring = false;
            }
        }

        /// <summary>
        /// Propagates the changes made by a called method to its caller, based on the lastLoadedFields        
        /// For example, if the called method modifies any fields, then the related parent fields in the caller method
        /// are marked as updated
        /// </summary>
        /// <param name="callerMStore"></param>
        /// <param name="calledMethodStore"></param>
        private void PropagateModificationsToCaller(SEMethodStore callerMStore, SEMethodStore calledMethodStore, Method calledMethod)
        {
            if (this.lastLoadedFields.Count == 0)
                return;

            //Idenitfy relevant fields from the last loadedFields
            SafeList<Field> fieldsProcessed = new SafeList<Field>();
            Field receiverField = null;

            //Check whether the current method call is actually on the field previously loaded                        
            TypeEx methodDeclType;
            if (!calledMethod.TryGetDeclaringType(out methodDeclType))
            {
                logger.Error("Failed to get the declaring type of the method: " + calledMethod.FullName);
                return;
            }

            if (methodDeclType.FullName == "System.String" || methodDeclType.FullName == "System.string")
            {
                return; //Do not process string types.
            }

            //Check whether the declaring type is in the fields list
            foreach (var field in this.lastLoadedFields)
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
            foreach (var argtype in calledMethod.ParameterTypes)
            {
                foreach (var field in this.lastLoadedFields)
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

            if (calledMethodStore.DefinedFieldSet.Count == 0)
            {
                //If the called method does not define any field, all current fields are just loaded fields
                foreach (var field in fieldsProcessed)
                {
                    callerMStore.AddToUsedList(field, this.currOffset);
                    this.lastLoadedFields.Remove(field);
                }
            }
            else
            {                
                //process each defined field
                foreach (var deffield in calledMethodStore.DefinedFieldSet.Values)
                {
                    TypeEx deffieldType;
                    if (deffield.OptionalField.TryGetDeclaringType(out deffieldType))
                    {
                        //Identify the related field in the lastLoadedFields
                        Field processedField = null;
                        foreach (var field in fieldsProcessed)
                        {
                            var fieldType = field.Type;
                            if (fieldType == deffieldType || fieldType.IsAssignableTo(deffieldType) || deffieldType.IsAssignableTo(fieldType))
                            {
                                processedField = field;
                                callerMStore.AddToDefinedList(field, this.currOffset);
                                break;
                            }
                        }

                        if (processedField != null)
                        {
                            fieldsProcessed.Remove(processedField);
                            this.lastLoadedFields.Remove(processedField);
                        }
                    }
                }                             

                //Consider the remaining fields at usedfields and remove them from lastLoadedFields
                foreach (var field in fieldsProcessed)
                {
                    callerMStore.AddToUsedList(field, this.currOffset);
                    this.lastLoadedFields.Remove(field);
                }
            }            
        }

        int currOffset;
        public override void AtOffset(int offset)
        {            
            base.AtOffset(offset);
            this.currOffset = offset;
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
        }

        /// <summary>
        /// Handles use of the field
        /// </summary>
        private void HandleFieldUse(Field field)
        {            
            //Check if it is a primitive field
            if (PexMeFilter.IsPrimitiveType(field.Type))
            {
                this.currSEMethodStore.AddToUsedList(field, this.currOffset);
            }
            else
            {
                //First a field is added a loaded. Later it is decided whether it is modified or not
                //based on the method call invoked in this field
                this.lastLoadedFields.Add(field);
            }
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
            this.currSEMethodStore.AddToDefinedList(field, this.currOffset);
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
                this.currSEMethodStore.AddToUsedList(field, this.currOffset);
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
                this.currSEMethodStore.AddToUsedList(field, this.currOffset);
            }

            this.lastLoadedFields.Clear();
        }

        #endregion
    }
}
