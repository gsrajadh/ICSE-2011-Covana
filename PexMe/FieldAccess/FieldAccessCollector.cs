using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Pex.Engine.PostAnalysis;
using Microsoft.ExtendedReflection.Interpretation.Effects;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Interpretation.States;
using PexMe.Core;

namespace PexMe.FieldAccess
{
    /// <summary>
    /// A class that captures methods and the fields that are modified by that method. It captures
    /// the values of all instance fiels in the beginning of the method and checks at the end whether
    /// the values remain the same or not.
    /// </summary>
    internal class FieldAccessCollector
            : PexTrackingControllerBase
            , IEffectsTracker
    {
        /// <summary>
        /// Fields storing the observed entities
        /// </summary>
        IFieldAccessPathObserver pathObserver;
        IFieldAccessExplorationObserver explorationObserver;
        PexMeDynamicDatabase pmd;
        PexMeStaticDatabase psd;
        
        int nextId;
        SafeDictionary<Int32, Term[]> derivedArguments = new SafeDictionary<Int32, Term[]>();
        SafeDictionary<Int32, Term[]> initialFieldValues = new SafeDictionary<Int32, Term[]>();
        SafeDictionary<Int32, Term[]> initialFieldArrayValues = new SafeDictionary<Int32, Term[]>();
        int trackedFrameId;
        SafeBag<Method> methodsOnStack = new SafeBag<Method>();

        /// <summary>
        /// Collects a sequence starting from the factory method to the beginning of PUT based on the attributes
        /// </summary>
        public SafeList<Method> FactoryMethodCallSequence = new SafeList<Method>();
        public bool bCollectFactorySequence = false;

        /// <summary>
        /// Collects the sequence starting from the PUT to the end of the execution
        /// </summary>
        public SafeList<Method> CUTMethodCallSequence = new SafeList<Method>();
        public bool bCollectCUTMethodCallSequence = false;
        
        SafeDictionary<Int32, int> oldDepths = new SafeDictionary<Int32, int>();
        int depth, level, framesHandled;
        static readonly Term[] noIndices = new Term[0];

        public FieldAccessCollector(IFieldAccessPathObserver pathObserver, 
            IFieldAccessExplorationObserver explorationObserver, 
            IPexMeDynamicDatabase pmd, PexMeStaticDatabase psd,
            int level)
        {
            this.pathObserver = pathObserver;
            this.explorationObserver = explorationObserver;
            this.pmd = pmd as PexMeDynamicDatabase;
            this.psd = psd;
            this.trackedFrameId = -1;
            this.level = level;
            this.depth = -1;
        }

        public int FramesCount
        {
            get { return this.nextId; }
        }

        public int FramesHandled
        {
            get { return this.framesHandled; }
        }

        #region ITrackingController Members
        public override int GetNextFrameId(int threadId, Method method)
        {
            this.oldDepths[this.nextId] = this.depth;
            this.depth++;          
            this.methodsOnStack.Add(method);

            if (this.bCollectCUTMethodCallSequence)
                this.CUTMethodCallSequence.Add(method);

            if (!this.bCollectFactorySequence)
            {
                this.BeginFactorySequenceCollection(method);
            }
            else
            {                
                this.StopFactorySequenceCollection(method);
                if (this.bCollectFactorySequence)
                {
                    //Check whether the method is a pure observer. Ignore the method
                    if(!this.psd.IsAnObserver(method))
                        this.FactoryMethodCallSequence.Add(method);
                }
            }           

            return this.nextId++;
        }

        /// <summary>
        /// Checks when to stop collecting factory method call sequence
        /// and when to start collecting PUT method call sequence
        /// </summary>
        /// <param name="method"></param>
        private void StopFactorySequenceCollection(Method method)
        {
            foreach (var attr in method.DeclaredAttributes)
            {
                if (attr.SerializableName.ToString().Contains("PexMethodAttribute"))
                {
                    this.bCollectFactorySequence = false;
                    this.bCollectCUTMethodCallSequence = true;
                    this.CUTMethodCallSequence.Add(method);
                    break;
                }
            }
        }

        /// <summary>
        /// Sequence collection starts at the begin of the method
        /// </summary>
        /// <param name="method"></param>
        private void BeginFactorySequenceCollection(Method method)
        {
            foreach (var attr in method.DeclaredAttributes)
            {
                if (attr.SerializableName.ToString().Contains("PexFactoryMethodAttribute"))
                {
                    this.bCollectFactorySequence = true;
                    break;
                }
            }
        }

        PexTrackingThread _thread;
        public override PexArgumentTracking GetArgumentTreatment(PexTrackingThread thread, int frameId, Method method)
        {
            if (trackedFrameId >= 0)
                return PexArgumentTracking.Derived;
            else if (this.depth != this.level)
                return PexArgumentTracking.Concrete;
            else
            {
                //let's reset the state before the execution of the method
                //((PexTrackingState)thread.State).RestartObjectFieldMaps();
                // let's start to collect that path condition from scratch
                this.PathConditionBuilder.Clear(); 
                _thread = thread;
                trackedFrameId = frameId;

                /*
                TypeEx declaringType;
                if (method.TryGetDeclaringType(out declaringType))
                    foreach (var f in declaringType.DeclaredStaticFields)
                    {
                        Term symbol = this.TermManager.Symbol(
                            new PexTrackedStaticFieldId(f));
                        thread.State.StoreStaticField(f, symbol);
                    }
                */

                IState state = thread.State;
                state.StartTrackingEffects(this);
                this.newObjects.Clear();

                //changed from track to derived so that we don't introduce symbolic values for arguments and object fields
                return PexArgumentTracking.Derived;
            }
        }

        public override void DerivedArguments(int frameId, Method method, Term[] arguments)
        {
            this.derivedArguments[frameId] = (Term[]) arguments.Clone();
            if (trackedFrameId == frameId)
            {
                TypeEx declaringType;
                if (method.TryGetDeclaringType(out declaringType) &&
                    declaringType.IsReferenceType &&
                    !method.IsStatic)
                {
                    var receiver = arguments[0];                                    

                    var instanceFields = declaringType.InstanceFields;
                    Term[] initialFieldValues = new Term[instanceFields.Length];
                    Term[] initialFieldArrayValues = new Term[instanceFields.Length];
                    for (int i = 0; i < instanceFields.Length; i++)
                    {
                        initialFieldValues[i] = _thread.State.ReadObjectField(receiver, instanceFields[i]);
                        if (instanceFields[i].Type.Spec == TypeSpec.SzArray)
                            initialFieldArrayValues[i] = _thread.State.ReadSzArrayElement(instanceFields[i].Type.ElementType.Layout, initialFieldValues[i], this.TermManager.Symbol(SzArrayIndexId.Instance));
                    }

                    this.initialFieldValues[frameId] = initialFieldValues;
                    this.initialFieldArrayValues[frameId] = initialFieldArrayValues;
                }
            }
        }

        public override void FrameDisposed(int frameId, PexTrackingFrame frame)
        {
            IThread thread = frame.Thread;
            IFrame caller = thread.CurrentFrame;
            //Term[] arguments;

            //Tao's TODO add also reciever/argument object fields **read** by the method
            //Tao's TODO can we handle recurstive fields being accessed?
            if (frameId == trackedFrameId)
            {
                this.framesHandled++;

                if (caller != null &&
                    caller.IsCallInProgress)
                {
                    // add instance field results
                    TypeEx declaringType;
                    if (frame.Method.TryGetDeclaringType(out declaringType))
                    {
                        //we add the method in our database even if it doesn't write fields since 
                        //we want to know whether a mehtod is ever encountered or a method doesn't write any field
                        this.pmd.AddMonitoredMethod(frame.Method);

                        /*
                        foreach (var f in declaringType.DeclaredStaticFields)
                        {
                            Term fieldValue = thread.State.ReadStaticField(f);//read the constraints of the final symbolic state after method execution
                            if (fieldValue != TermManager.Symbol(new PexTrackedStaticFieldId(f)))//filter out select(size, this)
                                processResult(frame, f, f.Type, fieldValue);
                            processArrayResult(frame, f, f.Type, fieldValue);
                        }
                        */

                        Term[] initialFieldValues;
                        Term[] initialFieldArrayValues;
                        if (this.initialFieldValues.TryGetValue(frameId, out initialFieldValues) &&
                            this.initialFieldArrayValues.TryGetValue(frameId, out initialFieldArrayValues))
                        {
                            Term receiver = frame.ReadArgument(0);
                            var instanceFields = declaringType.InstanceFields;
                            for (int i = 0; i < instanceFields.Length; i++)
                            {
                                var f = instanceFields[i];
                                Term fieldValue = thread.State.ReadObjectField(receiver, f);//read the constraints of the final symbolic state after method execution
                                if (fieldValue != initialFieldValues[i])//filter out select(size, this)
                                    this.explorationObserver.HandleMonitoredField(frame.Method, f, noIndices, fieldValue, initialFieldArrayValues[i]);
                                if (initialFieldArrayValues[i] != null)
                                     processArrayResult(frame, f, f.Type, initialFieldArrayValues[i], fieldValue, null);
                            }
                        }

                        //Tao's TODO add also **argument** object fields written by the method
                        /*
                        TypeEx[] argumentTypes = frame.ArgumentTypes;
                        int argumentNum = argumentTypes.Length;
                        int argumentStartIndex = 0;
                        if (!frame.Method.IsStatic) 
                            argumentStartIndex = 1;
                        for (int argumentIndex = argumentStartIndex; argumentIndex < argumentNum; argumentIndex++)
                        {
                            Term argument = frame.ReadArgument(argumentIndex);
                            //Note: the following line won't work when the argument declared argument types are different from the actual runtime argument types
                            foreach (var f in argumentTypes[argumentIndex].InstanceFields)
                            {
                                Term fieldValue = thread.State.ReadObjectField(argument, f);
                                processResult(frame, f, f.Type, fieldValue, argumentTypes[argumentIndex]);
                            }
                        }
                         **/
                    }

                    //Adding caller/calling mapping relationship
                    pmd.AddMethodMapping(caller.Method, frame.Method);
                }

                IState state = thread.State;
                state.EndTrackingEffects();
                this.trackedFrameId = -1;
            }

            this.methodsOnStack.Remove(frame.Method);
            this.depth = this.oldDepths[frameId]; // this should just decrement depth, but in a more robust way
        }


        /// <summary>
        /// SzArrayIndexId
        /// </summary>
        class SzArrayIndexId : ISymbolId
        {
            public static readonly SzArrayIndexId Instance = new SzArrayIndexId();
            private SzArrayIndexId() { }

            #region ISymbolId Members

            public string Description
            {
                get { return "$x"; }
            }

            public Layout Layout
            {
                get { return Layout.I; }
            }

            public ObjectCreationTime ObjectCreationTime
            {
                get { return ObjectCreationTime.Unknown; }
            }

            public Int64 GetPersistentHashCode()
            {
                return 0; // TODO
            }
            #endregion
        }       

        private void processArrayResult(PexTrackingFrame frame, Field f, TypeEx type, Term initialElementValue, Term fieldValue, Term initialValue)
        {
            //below handles array-type fields
            if (type.Spec == TypeSpec.SzArray &&//is array
                !this.TermManager.IsDefaultValue(fieldValue))//is not null pointer
            {
                Term index = this.TermManager.Symbol(SzArrayIndexId.Instance);

                Term elementValue = frame.Thread.State.ReadSzArrayElement(type.ElementType.Layout, fieldValue, index);

                if (elementValue != initialElementValue)
                    this.explorationObserver.HandleMonitoredField(
                        frame.Method,
                        f,
                        new Term[] { index },
                        this.TermManager.Widen(elementValue, type.ElementType.StackWidening),
                        initialValue); //some complicated trick, no need to know
            }
        }

        public override void FrameHasExplicitFailures(int frameId, Method method, IEnumerable<object> exceptionObjects)
        {
        }

        public override void FrameHasExceptions(int frameId, Method method)
        {
        }

        #endregion

        #region IEffectsTracker Members
        SafeSet<Term> newObjects = new SafeSet<Term>();
        void IEffectsTracker.AddNewObject(Term @object)
        {
            this.newObjects.Add(@object);
        }

        bool IEffectsTracker.IsNewObject(Term @object)
        {
            return this.newObjects.Contains(@object);
        }

        void IEffectsTracker.AddEffect(Method source, IEffect effect)
        {
        }

        void IEffectsTracker.ReadField(Method source, Field field)
        {
        }

        void IEffectsTracker.ReadFromMemoryRegion(Method source, UIntPtr baseAddress)
        {
        }

        void IEffectsTracker.WriteField(Method source, Field field)
        {
        }

        void IEffectsTracker.WriteToMemoryRegion(Method source, UIntPtr baseAddress)
        {
        }

        #endregion
    }
}
