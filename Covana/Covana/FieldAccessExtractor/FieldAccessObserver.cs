using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Pex.Framework.ComponentModel;
using Microsoft.Pex.Engine.Packages;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.Pex.Engine.Drivers;
using Microsoft.ExtendedReflection.Utilities.Safe;
using Microsoft.Pex.Engine.PostAnalysis;
using Microsoft.ExtendedReflection.Interpretation.Effects;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Interpretation.States;
using Microsoft.ExtendedReflection.Interpretation.Visitors;
using Microsoft.ExtendedReflection.Metadata.Names;
using Microsoft.ExtendedReflection.Utilities.Safe.Text;
using Microsoft.ExtendedReflection.Utilities.Safe.IO;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Covana;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;


namespace FieldAccessExtractor
{
    internal sealed class FieldAccessPathObserver
        : PexPathComponentBase
          , IService
    {
        private FieldAccessObserver FieldAccessObserver;

        private TermManager TermManager
        {
            get { return this.ExplorationServices.TermManager; }
        }

        protected override void Initialize()
        {
            this.FieldAccessObserver = this.GetService<FieldAccessObserver>();
        }

        public void Analyze()
        {
            this.Log.LogMessage(
                "FieldAccessObserver",
                "begin");

            int framesCount = 0;
            int framesHandled = 0;
            int maxLevelCount = 16;
            for (int level = 0; level < maxLevelCount; level++)
            {
                FieldAccessCollector controller = new FieldAccessCollector(this, level);
                using (IEngine trackingEngine = this.PathServices.TrackingEngineFactory.CreateTrackingEngine(controller)
                    )
                {
                    IPexTrackingDriver driver = trackingEngine.GetService<IPexTrackingDriver>();
                    if (!driver.Run())
                        break;
                }
                framesCount = SafeMath.Max(framesCount, controller.FramesCount);
                framesHandled += controller.FramesHandled;
                if (framesHandled > framesCount) framesHandled = framesCount;
                this.Log.LogMessage(
                    "FieldAccessObserver",
                    "collecting data, {0:P} of all frames up to level {1} / {2}",
                    ((double) framesHandled/framesCount), level, maxLevelCount);
                if (controller.FramesHandled == 0 || // did we make any progress?
                    framesHandled >= framesCount) // or have we processed all frames there are?
                    break;
            }

            this.Log.LogMessage(
                "FieldAccessObserver",
                "end");
        }

        private class FieldAccessCollector
            : PexTrackingControllerBase
              , IEffectsTracker
        {
            private FieldAccessPathObserver owner;
            private int nextId;
            private SafeDictionary<int, Term[]> derivedArguments = new SafeDictionary<int, Term[]>();
            private SafeDictionary<int, Term[]> initialFieldValues = new SafeDictionary<int, Term[]>();
            private SafeDictionary<int, Term[]> initialFieldArrayValues = new SafeDictionary<int, Term[]>();
            private int trackedFrameId;
            private SafeBag<Method> methodsOnStack = new SafeBag<Method>();
            private SafeDictionary<int, int> oldDepths = new SafeDictionary<int, int>();
            private int depth;
            private int level;
            private int framesHandled;

            public FieldAccessCollector(
                FieldAccessPathObserver owner,
                int level)
            {
                this.owner = owner;
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
                return this.nextId++;
            }

            private PexTrackingThread _thread;

            public override PexArgumentTracking GetArgumentTreatment(PexTrackingThread thread, int frameId,
                                                                     Method method)
            {
                if (trackedFrameId >= 0)
                    return PexArgumentTracking.Derived;
                else if (this.depth != this.level)
                    return PexArgumentTracking.Concrete;
                else
                {
                    // ((PexTrackingState)thread.State).RestartObjectFieldMaps();//let's reset the state before the execution of the method
                    this.PathConditionBuilder.Clear(); // let's start to collect that path condition from scratch
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

                    return PexArgumentTracking.Derived;
                        //changed from track to derived so that we don't introduce symbolic values for arguments and object fields
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
                                initialFieldArrayValues[i] =
                                    _thread.State.ReadSzArrayElement(instanceFields[i].Type.ElementType.Layout,
                                                                     initialFieldValues[i],
                                                                     this.TermManager.Symbol(SzArrayIndexId.Instance));
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
                Term[] arguments;

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
                            this.owner.FieldAccessObserver.AddMonitoredMethod(frame.Method);

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
                                    Term fieldValue = thread.State.ReadObjectField(receiver, f);
                                        //read the constraints of the final symbolic state after method execution
                                    if (fieldValue != initialFieldValues[i]) //filter out select(size, this)
                                        processResult(frame, f, f.Type, fieldValue);
                                    if (initialFieldArrayValues[i] != null)
                                        processArrayResult(frame, f, f.Type, initialFieldArrayValues[i], fieldValue);
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
            private class SzArrayIndexId : ISymbolId
            {
                public static readonly SzArrayIndexId Instance = new SzArrayIndexId();

                private SzArrayIndexId()
                {
                }

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

            private static readonly Term[] noIndices = new Term[0];


            private void processResult(PexTrackingFrame frame, Field f, TypeEx type, Term fieldValue)
            {
                //below handles non-array-type fields
                this.owner.FieldAccessObserver.AddResult(
                    frame.Method,
                    f,
                    noIndices,
                    fieldValue);
            }

            private void processArrayResult(PexTrackingFrame frame, Field f, TypeEx type, Term initialElementValue,
                                            Term fieldValue)
            {
                //below handles array-type fields
                if (type.Spec == TypeSpec.SzArray && //is array
                    !this.TermManager.IsDefaultValue(fieldValue)) //is not null pointer
                {
                    Term index = this.TermManager.Symbol(SzArrayIndexId.Instance);

                    Term elementValue = frame.Thread.State.ReadSzArrayElement(type.ElementType.Layout, fieldValue, index);

                    if (elementValue != initialElementValue)
                        this.owner.FieldAccessObserver.AddResult(
                            frame.Method,
                            f,
                            new Term[] {index},
                            this.TermManager.Widen(elementValue, type.ElementType.StackWidening));
                            //some complicated trick, no need to know
                }
            }

            public override void FrameHasExplicitFailures(int frameId, Method method,
                                                          IEnumerable<object> exceptionObjects)
            {
            }

            public override void FrameHasExceptions(int frameId, Method method)
            {
            }

            #endregion

            #region IEffectsTracker Members

            private SafeSet<Term> newObjects = new SafeSet<Term>();

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

    internal sealed class FieldAccessObserver
        : PexExplorationComponentBase
          , IService
    {
        //SafeDictionary<Method, SafeDictionary<Field, SafeSet<Term>>> Results = new SafeDictionary<Method, SafeDictionary<Field, SafeSet<Term>>>();
        private FieldAccessInfo FieldAccessInfoObj;
        private SeqexDatabase database;

        #region private fields

        private TermManager termManager;

        #endregion

        protected override void Initialize()
        {
            database = this.GetService<SeqexDatabase>();
            FieldAccessInfoObj = database.FieldAccessInfoObj;
            this.termManager = this.ExplorationServices.TermManager;
        }

        internal void AddMonitoredMethod(Method method)
        {
            System.Collections.Generic.Dictionary<string, HashSet<string>> methodResults;
            if (!this.FieldAccessInfoObj.Results.TryGetValue(method.FullName, out methodResults))
                this.FieldAccessInfoObj.Results[method.FullName] =
                    methodResults = new System.Collections.Generic.Dictionary<string, HashSet<string>>();
        }

        internal void AddResult(Method method, Field f, Term[] indices, Term fieldValue)
        {
            /* SafeDictionary<Field, SafeSet<Term>> methodResults;
            if (!this.FieldAccessInfoContainer.Results.TryGetValue(method, out methodResults))
                this.FieldAccessInfoContainer.Results[method] = methodResults = new SafeDictionary<Field, SafeSet<Term>>();
            SafeSet<Term> fieldResults;
            if (!methodResults.TryGetValue(f, out fieldResults))
                methodResults[f] = fieldResults = new SafeSet<Term>();
            */
            //TODO why do we need it?
            /*
            if (database == null)
            {
                database = new SeqexDatabase();
                FieldAccessInfoObj = database.FieldAccessInfoObj;     
            }
            */
            string arrayIndex = "";
            using (SomeRewriter someRewriter = new SomeRewriter(this.termManager))
            {
                fieldValue = someRewriter.VisitTerm(default(TVoid), fieldValue);
                    //update the field value to accomodate array-type field           
                //if (indices.Length == 0)//not an array-type field               
                if (indices.Length == 1) //is an array-type field
                {
                    arrayIndex = " at index of " + indices[0].UniqueIndex.ToString();
                }
            }

            System.Collections.Generic.Dictionary<string, HashSet<string>> methodResults;
            if (!this.FieldAccessInfoObj.Results.TryGetValue(method.FullName, out methodResults))
                this.FieldAccessInfoObj.Results[method.FullName] =
                    methodResults = new System.Collections.Generic.Dictionary<string, HashSet<string>>();
            HashSet<string> fieldResults;
            if (!methodResults.TryGetValue(f.FullName, out fieldResults))
                methodResults[f.FullName] = fieldResults = new HashSet<string>();

            var sb = new SafeStringBuilder();
            var swriter = new TermSExpWriter(this.ExplorationServices.TermManager, new SafeStringWriter(sb), true, false);
            swriter.Write(fieldValue);
            sb.Append(arrayIndex);

            int value;
            if (termManager.TryGetI4Constant(fieldValue, out value))
            {
                sb.Append("  constant value: " + value);
            }
            else if (termManager.IsDefaultValue(fieldValue))
            {
                sb.Append("  null reference");
            }
            else
            {
                sb.Append("  not-null reference");
            }

            fieldResults.Add(sb.ToString());
        }

        public void Dump()
        {
            var termManager = this.ExplorationServices.TermManager;

            SafeStringBuilder sb = new SafeStringBuilder();
            foreach (var kvp in this.FieldAccessInfoObj.Results)
            {
                var method = kvp.Key;
                var methodResults = kvp.Value;
                sb.AppendLine();
                sb.AppendLine("method:" + method /*.FullName*/);
                foreach (var kvp2 in methodResults)
                {
                    var field = kvp2.Key;
                    var fieldResults = kvp2.Value;
                    sb.AppendLine("  modifies field:" + field /*.FullName*/);
                    sb.AppendLine("    with new values:");
                    foreach (var newFieldValueTerm in fieldResults)
                    {
                        var swriter = new TermSExpWriter(termManager, new SafeStringWriter(sb), true, false);
                        swriter.Write(newFieldValueTerm);
                        sb.AppendLine();
                    }
                }
            }

            this.Log.Dump("fields", "field changes", sb.ToString());
            //database.DumpFieldAccessInfoIntoToFile();
        }
    }

    public sealed class FieldAccessObserverAttribute
        : PexComponentElementDecoratorAttributeBase
          , IPexExplorationPackage, IPexPathPackage
    {
        /// <summary>
        /// Gets the name of this package.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return "FieldAccessObserver"; }
        }

        protected override sealed void Decorate(Name location, IPexDecoratedComponentElement host)
        {
            host.AddExplorationPackage(location, this);
            host.AddPathPackage(location, this);
        }

        #region IPexExplorationPackage Members

        void IPexExplorationPackage.Load(
            IContainer explorationContainer)
        {
            explorationContainer.AddComponent(
                "FieldAccessObserver",
                new FieldAccessObserver());
        }

        void IPexExplorationPackage.Initialize(
            IPexExplorationEngine host)
        {
        }

        object IPexExplorationPackage.BeforeExploration(
            IPexExplorationComponent host)
        {
            return null;
        }

        void IPexExplorationPackage.AfterExploration(
            IPexExplorationComponent host,
            object data)
        {
            FieldAccessObserver observer =
                ServiceProviderHelper.GetService<FieldAccessObserver>(host.Site);
            observer.Dump();
        }

        #endregion

        #region IPexPathPackage Members

        void IPexPathPackage.Load(IContainer pathContainer)
        {
            pathContainer.AddComponent("FieldAccessPathObserver", new FieldAccessPathObserver());
        }

        object IPexPathPackage.BeforeRun(IPexPathComponent host)
        {
            return null;
        }

        void IPexPathPackage.AfterRun(IPexPathComponent host, object data)
        {
            FieldAccessPathObserver fieldAccessPathObserver =
                ServiceProviderHelper.GetService<FieldAccessPathObserver>(host.Site);
            fieldAccessPathObserver.Analyze();
        }

        #endregion
    }

    #region private classes

    internal class SomeId : IObjectId
    {
        private int index;

        public SomeId(int index)
        {
            this.index = index;
        }

        public override bool Equals(object obj)
        {
            SomeId someId = obj as SomeId;
            return someId != null && someId.index == this.index;
        }

        public override int GetHashCode()
        {
            return this.index;
        }

        #region IObjectId Members

        public string Description
        {
            get { return "some." + this.index.ToString(); }
        }

        public bool TrackFieldAccesses
        {
            get { return false; }
        }

        public ObjectCreationTime CreationTime
        {
            get { return ObjectCreationTime.Unknown; }
        }

        public bool IsFullyDefined
        {
            get { return false; }
        }

        public Int64 GetPersistentHashCode()
        {
            return 0; // TODO
        }

        #endregion
    }

    internal class SomeRewriter : TermInternalizingRewriter<TVoid>
    {
        private SafeBag<TypeEx> types = new SafeBag<TypeEx>();

        public SomeRewriter(TermManager termManager)
            : base(termManager, OnCollection.Fail)
        {
        }

        public override Term VisitObject(TVoid parameter, Term term, IObjectId id, ObjectPropertyCollection properties)
        {
            object value;
            if (this.TermManager.TryGetObject(term, out value))
                return base.VisitObject(parameter, term, id, properties);
            else
            {
                TypeEx type;
                if (!this.TermManager.TryGetObjectType(term, out type))
                    SafeDebug.Fail("cannet get object type");
                int index = types.Add(type);
                return this.TermManager.Object(new SomeId(index), properties);
            }
        }

        public override Term VisitSelect(TVoid parameter, Term term, Term compound, Term index)
        {
            index = this.VisitTerm(parameter, index);
            compound = this.VisitTerm(parameter, compound);

            ISymbolId key;
            if (this.TermManager.TryGetSymbol(index, out key))
            {
                ISymbolIdFromParameter parameterKey = key as ISymbolIdFromParameter;
                if (parameterKey != null &&
                    parameterKey.Parameter.IsThis)
                {
                    Term baseCompound;
                    ITermMap updates;
                    if (this.TermManager.TryGetUpdate(compound, out baseCompound, out updates) &&
                        updates.AreKeysValues)
                        return this.VisitTerm(
                            parameter,
                            this.TermManager.Select(baseCompound, index));
                }
            }

            return this.TermManager.Select(compound, index);
        }
    }

    #endregion
}