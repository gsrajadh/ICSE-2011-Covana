//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using Microsoft.Pex.Framework.Explorable;
//using Microsoft.Pex.Engine.ComponentModel;
//using Microsoft.Pex.Engine.Explorable;
//using Microsoft.ExtendedReflection.Metadata;
//using Microsoft.ExtendedReflection.Feedback;
//using Microsoft.ExtendedReflection.Collections;
//using System.IO;
//using FieldAccessExtractor;
//using System.Runtime.Serialization.Formatters.Binary;
//using System.Reflection.Emit;
//using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
//using Seqex;
//using Microsoft.ExtendedReflection.Utilities.Safe.Text;
//
//namespace SeqExplorable
//{
//    [AttributeUsage(AttributeTargets.Assembly)]
//    public class SeqexExplorableGuesserAttribute
//        : PexExplorableGuesserAttributeBase
//    {
//        protected override IEnumerable<IPexExplorableGuesser> CreateExplorableGuessers(IPexComponent host)
//        {
//            yield return new SeqexExplorableGuesser(host);
//        }
//
//        private class SeqexExplorableGuesser
//            : PexComponentElementBase
//              , IPexExplorableGuesser
//        {
//            private FieldAccessInfo FieldAccessInfoObj;
//            private InsufficientObjectFactoryFieldInfo InsufficientObjectFactoryFieldInfoObj;
//            private SeqexDatabase database;
//
//            //SafeSet<string> RelevantMethods;//currently not used
//
//            public SeqexExplorableGuesser(IPexComponent host)
//                : base(host)
//            {
//                //we associate the references with database
//                database = host.GetService<SeqexDatabase>();
//                FieldAccessInfoObj = database.FieldAccessInfoObj;
//                InsufficientObjectFactoryFieldInfoObj = database.InsufficientObjectFactoryFieldInfoObj;
//
//                /*
//                RelevantMethods = new SafeSet<string>();
//                
//                //below we filter out those methods whose written fields are not in the relevent field set           
//                foreach (var kvp in FieldAccessInfoObj.Results)
//                {
//                    var methodName = kvp.Key;
//                    var methodFieldResults = kvp.Value;                  
//                    foreach (var kvp2 in methodFieldResults)
//                    {
//                        var field = kvp2.Key;
//                        if (InsufficientObjectFactoryFieldInfoObj.ReleventFields.Contains(field))//the field is relevant
//                            RelevantMethods.Add(methodName);
//                    }
//                }
//                 */
//
//                SafeStringBuilder sb = new SafeStringBuilder();
//                sb.AppendLine("");
//                foreach (var f in InsufficientObjectFactoryFieldInfoObj.ReleventFields)
//                {
//                    sb.AppendLine(f);
//                }
//                database.AddFactoryMethodForDebug("Relevant fields", " (for un-covered branches): " + sb.ToString());
//            }
//
//            #region MethodEffects and their computation
//
//            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
//            private struct MethodEffects
//            {
//                public readonly IFiniteSet<int> WrittenInstanceFields;
//                public readonly IFiniteSet<string> WrittenInstanceFieldNames;
//                public readonly int CallDepth;
//
//                public MethodEffects(IFiniteSet<int> writtenInstanceFields, IFiniteSet<string> writtenInstanceFieldNames,
//                                     int callDepth)
//                {
//                    SafeDebug.AssumeNotNull(writtenInstanceFields, "writtenInstanceFields");
//                    SafeDebug.Assume(callDepth >= 0, "callDepth>=0");
//                    this.WrittenInstanceFields = writtenInstanceFields;
//                    this.WrittenInstanceFieldNames = writtenInstanceFieldNames;
//                    this.CallDepth = callDepth;
//                }
//            }
//
//            private static readonly SafeDictionary<int, MethodEffects> methodEffects =
//                new SafeDictionary<int, MethodEffects>();
//
//            private static MethodEffects GetMethodEffects(Method method)
//            {
//                SafeDebug.AssumeNotNull(method, "method");
//                MethodEffects res;
//                if (!methodEffects.TryGetValue(method.GlobalIndex, out res))
//                    lock (methodEffects)
//                    {
//                        methodEffects[method.GlobalIndex] = res =
//                                                            new MethodEffects(Set.Empty<int>(), Set.Empty<string>(), 0);
//                        // to prevent cycles
//
//                        MethodBodyEx body;
//                        TypeEx declaringType;
//                        if (method.TryGetDeclaringType(out declaringType) &&
//                            method.TryGetBody(out body) &&
//                            body.HasInstructions)
//                        {
//                            methodEffects[method.GlobalIndex] = res = ComputeMethodEffects(declaringType, body);
//                        }
//                    }
//                SafeDebug.AssertNotNull(res, "res");
//                return res;
//            }
//
//            private static readonly IFiniteSet<OpCode> LdcOpCodes = Set.Enumerable(null, new OpCode[]
//                                                                                             {
//                                                                                                 OpCodes.Ldc_I4,
//                                                                                                 OpCodes.Ldc_I4_0,
//                                                                                                 OpCodes.Ldc_I4_1,
//                                                                                                 OpCodes.Ldc_I4_2,
//                                                                                                 OpCodes.Ldc_I4_3,
//                                                                                                 OpCodes.Ldc_I4_4,
//                                                                                                 OpCodes.Ldc_I4_5,
//                                                                                                 OpCodes.Ldc_I4_6,
//                                                                                                 OpCodes.Ldc_I4_7,
//                                                                                                 OpCodes.Ldc_I4_8,
//                                                                                                 OpCodes.Ldc_I4_M1,
//                                                                                                 OpCodes.Ldc_I4_S,
//                                                                                                 OpCodes.Ldc_I8,
//                                                                                                 OpCodes.Ldc_R4,
//                                                                                                 OpCodes.Ldc_R8,
//                                                                                                 OpCodes.Ldnull,
//                                                                                                 OpCodes.Ldstr
//                                                                                             });
//
//            private static readonly IFiniteSet<OpCode> ConvOpCodes = Set.Enumerable(null, new OpCode[]
//                                                                                              {
//                                                                                                  OpCodes.Conv_I,
//                                                                                                  OpCodes.Conv_I1,
//                                                                                                  OpCodes.Conv_I2,
//                                                                                                  OpCodes.Conv_I4,
//                                                                                                  OpCodes.Conv_I8,
//                                                                                                  OpCodes.Conv_R_Un,
//                                                                                                  OpCodes.Conv_R4,
//                                                                                                  OpCodes.Conv_R8,
//                                                                                                  OpCodes.Conv_U,
//                                                                                                  OpCodes.Conv_U1,
//                                                                                                  OpCodes.Conv_U2,
//                                                                                                  OpCodes.Conv_U4,
//                                                                                                  OpCodes.Conv_U8
//                                                                                              });
//
//            private static MethodEffects ComputeMethodEffects(TypeEx declaringType, MethodBodyEx body)
//            {
//                SafeDebug.AssumeNotNull(declaringType, "declaringType");
//                SafeDebug.AssumeNotNull(body, "body");
//                SafeSet<int> res = null;
//                SafeSet<string> writtenFieldNames = null;
//
//                int callDepth = 0;
//                int offset = 0;
//                Instruction instruction;
//                bool topIsConstant = false;
//                while (body.TryGetInstruction(offset, out instruction))
//                {
//                    SafeDebug.AssumeNotNull(instruction, "instruction");
//                    OpCode opCode = instruction.OpCode;
//                    if (LdcOpCodes.Contains(opCode))
//                    {
//                        topIsConstant = true;
//                    }
//                    else if (ConvOpCodes.Contains(opCode))
//                    {
//                        // do not change topIsConstant
//                    }
//                    else
//                    {
//                        if (opCode == OpCodes.Stfld)
//                        {
//                            if (!topIsConstant) // for now, ignore storing constants; TODO: do better.
//                            {
//                                SafeDebug.Assume(opCode.OperandType == OperandType.InlineField,
//                                                 "opCode.OperandType == OperandType.InlineField");
//                                Field field = instruction.Field;
//                                SafeDebug.AssumeNotNull(field, "field");
//                                SafeDebug.Assume(!field.IsStatic, "!field.IsStatic");
//                                TypeEx fieldDeclaringType;
//                                if (field.TryGetDeclaringType(out fieldDeclaringType) &&
//                                    declaringType.IsAssignableTo(fieldDeclaringType))
//                                {
//                                    if (res == null)
//                                        res = new SafeSet<int>();
//                                    res.Add(field.LocalIndex);
//
//                                    if (writtenFieldNames == null)
//                                        writtenFieldNames = new SafeSet<string>();
//                                    writtenFieldNames.Add(field.FullName);
//                                }
//                            }
//                        }
//                        else if (opCode == OpCodes.Call)
//                        {
//                            SafeDebug.Assume(opCode.OperandType == OperandType.InlineMethod,
//                                             "opCode.OperandType == OperandType.InlineMethod");
//                            Method method = instruction.Method;
//                            SafeDebug.AssumeNotNull(method, "method");
//                            TypeEx methodDeclaringType;
//                            if (method.TryGetDeclaringType(out methodDeclaringType) &&
//                                declaringType.IsAssignableTo(methodDeclaringType))
//                            {
//                                MethodEffects methodEffects = GetMethodEffects(method);
//                                if (methodEffects.WrittenInstanceFields.Count > 0)
//                                {
//                                    if (res == null)
//                                        res = new SafeSet<int>();
//                                    res.AddRange(methodEffects.WrittenInstanceFields);
//
//                                    if (writtenFieldNames == null)
//                                        writtenFieldNames = new SafeSet<string>();
//                                    writtenFieldNames.AddRange(methodEffects.WrittenInstanceFieldNames);
//
//                                    if (methodEffects.CallDepth > callDepth)
//                                        callDepth = methodEffects.CallDepth;
//                                }
//                            }
//                        }
//                        topIsConstant = false;
//                    }
//
//                    offset = instruction.NextOffset;
//                }
//                return new MethodEffects(res == null ? Set.Empty<int>() : (IFiniteSet<int>) res,
//                                         writtenFieldNames == null
//                                             ? Set.Empty<string>()
//                                             : (IFiniteSet<string>) writtenFieldNames,
//                                         callDepth + 1);
//            }
//
//            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
//            private struct OrderedMethodEffects : IComparable<OrderedMethodEffects>
//            {
//                public readonly Method Method;
//                public readonly MethodEffects Effects;
//
//                public OrderedMethodEffects(Method method)
//                {
//                    SafeDebug.AssumeNotNull(method, "method");
//                    this.Method = method;
//                    this.Effects = GetMethodEffects(method);
//                }
//
//                #region IComparable<OrderedMethodEffects> Members
//
//                int IComparable<OrderedMethodEffects>.CompareTo(OrderedMethodEffects other)
//                {
//                    return this.Effects.CallDepth - other.Effects.CallDepth;
//                }
//
//                #endregion
//            }
//
//            #endregion
//
//            //this method implements the key algorithm for returning a set of factory methods whose bodies encode method sequences
//            public IEnumerable<PexExplorableCandidate> GuessExplorables(TypeEx explorableType)
//            {
//                if (database.isDebug)
//                {
//                    database.AddFactoryMethodForDebug(explorableType.FullName, "is requesting factory methods");
//                }
//
//                //if this method returns no candidates, then the default guess explorable will be used
//                //if (RelevantMethods.Count == 0) //no relevant methods
//                //    yield break;
//                if (FieldAccessInfoObj.Results.Count == 0) //no mapped fields for methods
//                    yield break;
//
//                //if (explorableType.FullName != "CustomExplorableGuesser.Stack")
//                //    yield break;
//
//                var visibilityContext = VisibilityContext.Exported; // TODO: Use proper visibility context
//                if (!explorableType.IsVisible(visibilityContext)) //if the type is not public
//                    yield break;
//
//                var defaultConstructor = explorableType.DefaultConstructor;
//                if (defaultConstructor == null) //if there is no constructor for the type
//                    yield break;
//
//                /*
//                //below is the example hard-coded implementation for returning a fixed sequence for stack class
//                for (int n = 0; n < 4; n++)
//                {
//                    var explorableFactory = new PexExplorableFactory(
//                        this.Host,
//                        explorableType,
//                        visibilityContext);
//
//                    if (!explorableFactory.TrySetFactoryMethod(defaultConstructor))
//                        continue;
//
//                    var mutatorMethod = explorableType.GetMethod(
//                        "Push", // name of method
//                        new TypeEx[] { SystemTypes.Int32 }); // argument types
//                    if (mutatorMethod == null)
//                        continue;
//
//                    for (int j = 0; j < n; j++)
//                        if (!explorableFactory.TryAddMethodSetter(mutatorMethod))
//                            continue;
//
//                    IPexExplorable explorable = explorableFactory.CreateExplorable();
//                    CodeUpdate.AddMethodCodeUpdate previewUpdate;
//                    CodeUpdate update = explorableFactory.CreateExplorableFactoryUpdate(out previewUpdate);
//                    yield return new PexExplorableCandidate(explorable, update);
//                }
//                 */
//
//                //the following factory is not returned to be used in sequence explroation but used to check
//                //whether things are valid
//                var localExplorableFactory = new PexExplorableFactory(
//                    this.Host,
//                    explorableType,
//                    visibilityContext);
//
//                Method bestConstructorMethod = null;
//                SafeList<Field> fieldToBeSetList = new SafeList<Field>();
//                SafeList<Property> propertyToBeSetList = new SafeList<Property>();
//
//                #region scan visible constructors, order by call depth, select best constructor
//
//                var orderedMethodEffects = new SafeList<OrderedMethodEffects>();
//                var bestConstructor = new OrderedMethodEffects();
//                foreach (var constructor in explorableType.GetVisibleInstanceConstructors(visibilityContext))
//                {
//                    if (!localExplorableFactory.IsValidFactoryMethod(constructor))
//                        continue;
//                    orderedMethodEffects.Add(new OrderedMethodEffects(constructor));
//                }
//                orderedMethodEffects.Sort();
//                foreach (var entry in orderedMethodEffects)
//                    if (bestConstructor.Method == null ||
//                        bestConstructor.Effects.WrittenInstanceFields.Count < entry.Effects.WrittenInstanceFields.Count)
//                    {
//                        bestConstructor = entry;
//                    }
//                orderedMethodEffects.Clear();
//                if (bestConstructor.Method == null) //cannot find a constructor
//                {
//                    yield break;
//                }
//
//                /*
//                 * if (!localExplorableFactory.TrySetFactoryMethod(bestConstructor.Method))
//                    SafeDebug.Fail("we checked before that it is valid");
//                */
//                bestConstructorMethod = bestConstructor.Method;
//
//                #endregion
//
//                var allWrittenFields = new SafeSet<int>(bestConstructor.Effects.WrittenInstanceFields);
//                var tempSet = new SafeSet<int>();
//
//                #region add visible fields as setters (note that here we don't do any filtering based on relevant fields since some fields may be useable
//
//                foreach (var instanceField in explorableType.InstanceFields)
//                {
//                    if (!localExplorableFactory.IsValidFieldSetter(instanceField))
//                        continue;
//
//                    allWrittenFields.Add(instanceField.LocalIndex);
//                    // visible fields are set by the solution visitor 
//                    /*
//                    if (!localExplorableFactory.TryAddFieldSetter(instanceField))
//                        SafeDebug.Fail("could not add field setter");
//                     */
//                    fieldToBeSetList.Add(instanceField);
//                }
//
//                #endregion
//
//                #region add visible properties as setters (note that here we don't do any filtering based on relevant fields since some fields may be useable
//
//                for (TypeEx type = explorableType; type != null; type = type.BaseType)
//                    foreach (var property in type.DeclaredProperties)
//                    {
//                        if (!localExplorableFactory.IsValidPropertySetter(property))
//                            continue;
//                        Method method = property.Setter;
//                        if (method.IsVirtual)
//                            method = explorableType.VTableLookup(method);
//                        SafeDebug.AssertNotNull(method, "method");
//                        MethodEffects methodEffects = GetMethodEffects(method);
//                        if (methodEffects.WrittenInstanceFields.Count == 0)
//                            continue;
//                        SafeDebug.Assert(tempSet.Count == 0, "tempSet.Count == 0");
//                        tempSet.AddRange(methodEffects.WrittenInstanceFields);
//                        tempSet.RemoveRange(allWrittenFields);
//                        if (tempSet.Count > 0)
//                        {
//                            allWrittenFields.AddRange(tempSet);
//                            tempSet.Clear();
//                            /*
//                            if (!localExplorableFactory.TryAddPropertySetter(property))
//                                SafeDebug.Fail("we checked before that it is valid");
//                             */
//                            propertyToBeSetList.Add(property);
//                        }
//                    }
//
//                #endregion
//
//                #region default factory method from the original Pex: scan visible methods, order by call depth, add methods as setters
//
//                //start building the method sequence
//                orderedMethodEffects.Clear();
//                var originalExplorableFactory = new PexExplorableFactory(
//                    this.Host,
//                    explorableType,
//                    visibilityContext);
//
//                //add constructor
//                if (!originalExplorableFactory.TrySetFactoryMethod(bestConstructorMethod))
//                {
//                    SafeDebug.Fail("we checked before that it is valid");
//                    yield break;
//                }
//
//                foreach (var field in fieldToBeSetList)
//                {
//                    if (!originalExplorableFactory.TryAddFieldSetter(field))
//                        SafeDebug.Fail("could not add field setter");
//                }
//
//                foreach (var property in propertyToBeSetList)
//                {
//                    if (!originalExplorableFactory.TryAddPropertySetter(property))
//                        SafeDebug.Fail("we checked before that it is valid");
//                }
//
//                SafeDebug.Assert(orderedMethodEffects.Count == 0, "orderedMethodEffects.Count == 0");
//                for (TypeEx type = explorableType; type != null; type = type.BaseType)
//                    foreach (var methodDefinition in type.Definition.DeclaredInstanceMethods)
//                    {
//                        if (methodDefinition.GenericMethodParameters.Length > 0)
//                            continue;
//                        Method method = methodDefinition.Instantiate(type.GenericTypeArguments, TypeEx.NoTypes);
//                        if (!originalExplorableFactory.IsValidMethodSetter(method, false)
//                            /*||
//                            method.ResultType.SerializableName != SystemTypes.Void.SerializableName*/)
//                            continue;
//                        var name = method.ShortName;
//                        if (name.Contains("Remove") ||
//                            name.Contains("Clear") ||
//                            name.Contains("Reset")) // crude heuristic to filter out destructive methods
//                            continue;
//                        orderedMethodEffects.Add(new OrderedMethodEffects(method));
//                    }
//                orderedMethodEffects.Sort();
//                foreach (var entry in orderedMethodEffects)
//                {
//                    if (entry.Effects.WrittenInstanceFields.Count == 0)
//                        continue;
//                    SafeDebug.Assert(tempSet.Count == 0, "tempSet.Count == 0");
//                    tempSet.AddRange(entry.Effects.WrittenInstanceFields);
//                    tempSet.RemoveRange(allWrittenFields);
//                    if (tempSet.Count > 0)
//                    {
//                        allWrittenFields.AddRange(tempSet);
//                        tempSet.Clear();
//                        if (!originalExplorableFactory.TryAddMethodSetter(entry.Method))
//                            SafeDebug.Fail("we checked before that it is valid");
//                    }
//                }
//
//                IPexExplorable originalExplorable = originalExplorableFactory.CreateExplorable();
//
//                CodeUpdate.AddMethodCodeUpdate originalPreviewUpdate;
//                CodeUpdate originalUpdate =
//                    originalExplorableFactory.CreateExplorableFactoryUpdate(out originalPreviewUpdate);
//
//                WriteOutMethodBody(explorableType, originalPreviewUpdate);
//
//                yield return new PexExplorableCandidate(originalExplorable, false, originalUpdate);
//                orderedMethodEffects.Clear();
//
//                #endregion
//
//                //below is the new factory methods with the new strategies based on relevant fields
//
//                #region scan visible methods, order by call depth, add methods as setters
//
//                SafeDebug.Assert(orderedMethodEffects.Count == 0, "orderedMethodEffects.Count == 0");
//                for (TypeEx type = explorableType; type != null; type = type.BaseType)
//                    foreach (var methodDefinition in type.Definition.DeclaredInstanceMethods)
//                    {
//                        //????
//                        if (methodDefinition.GenericMethodParameters.Length > 0)
//                            continue;
//                        Method method = methodDefinition.Instantiate(type.GenericTypeArguments, TypeEx.NoTypes);
//                        //a sanity check for filtering out public methods that cannot be invoked
//                        if (!localExplorableFactory.IsValidMethodSetter(method, false) //||
//                            // method.ResultType.SerializableName != SystemTypes.Void.SerializableName
//                            ) //this above line filters out non-void-return methods
//                            continue;
//
//                        //filter out destructive methods
//                        var name = method.ShortName;
//                        if (name.Contains("Remove") ||
//                            name.Contains("Clear") ||
//                            name.Contains("Reset")) // crude heuristic to filter out destructive methods
//                            continue;
//
//                        bool isChosen = false;
//                        bool isSideEffectFree = true;
//                        OrderedMethodEffects effect = new OrderedMethodEffects(method);
//                        //we choose it if its statically determined modified fields include relevant fields
//                        IEnumerable<string> writtenFieldsStaticallyDetermined =
//                            effect.Effects.WrittenInstanceFieldNames.Intersect<string>(
//                                InsufficientObjectFactoryFieldInfoObj.ReleventFields);
//                        if ((writtenFieldsStaticallyDetermined.Count<string>() > 0))
//                        {
//                            isChosen = true;
//                            if (database.isDebug)
//                            {
//                                SafeStringBuilder sb = new SafeStringBuilder();
//                                sb.AppendLine("");
//                                foreach (var s in writtenFieldsStaticallyDetermined)
//                                {
//                                    sb.AppendLine(s);
//                                }
//                                database.AddFactoryMethodForDebug(explorableType.FullName,
//                                                                  name + " statically determined to write " +
//                                                                  sb.ToString());
//                            }
//                        }
//                        if (effect.Effects.WrittenInstanceFieldNames.Count >= 0)
//                            isSideEffectFree = false;
//
//                        //we choose it if its dynamically determined modified fields inlcude relevant fields
//                        System.Collections.Generic.Dictionary<string, HashSet<string>> writtenFields;
//                        if (FieldAccessInfoObj.Results.TryGetValue(method.FullName, out writtenFields))
//                        {
//                            IEnumerable<string> writtenFieldsDynamicallyDetermined =
//                                writtenFields.Keys.Intersect<string>(
//                                    InsufficientObjectFactoryFieldInfoObj.ReleventFields);
//                            if (writtenFieldsDynamicallyDetermined.Count<string>() > 0)
//                            {
//                                isChosen = true;
//                                if (database.isDebug)
//                                {
//                                    SafeStringBuilder sb = new SafeStringBuilder();
//                                    sb.AppendLine("");
//                                    foreach (var s in writtenFieldsDynamicallyDetermined)
//                                    {
//                                        sb.AppendLine(s);
//                                    }
//                                    database.AddFactoryMethodForDebug(explorableType.FullName,
//                                                                      name + " dynamically determined to write " +
//                                                                      sb.ToString());
//                                }
//                            }
//                            if (writtenFields.Count > 0)
//                                isSideEffectFree = true;
//                        }
//
//                        if (!isChosen)
//                            continue;
//
//                        //start building the method sequence
//                        var explorableFactory = new PexExplorableFactory(
//                            this.Host,
//                            explorableType,
//                            visibilityContext);
//
//                        //add constructor
//                        if (!explorableFactory.TrySetFactoryMethod(bestConstructorMethod))
//                        {
//                            SafeDebug.Fail("we checked before that it is valid");
//                            yield break;
//                        }
//
//                        foreach (var field in fieldToBeSetList)
//                        {
//                            if (!explorableFactory.TryAddFieldSetter(field))
//                                SafeDebug.Fail("could not add field setter");
//                        }
//
//                        foreach (var property in propertyToBeSetList)
//                        {
//                            if (!explorableFactory.TryAddPropertySetter(property))
//                                SafeDebug.Fail("we checked before that it is valid");
//                        }
//
//
//                        if (!explorableFactory.TryAddMethodSetter(method))
//                            continue;
//
//                        IPexExplorable explorable = explorableFactory.CreateExplorable();
//                        CodeUpdate.AddMethodCodeUpdate previewUpdate;
//                        CodeUpdate update = explorableFactory.CreateExplorableFactoryUpdate(out previewUpdate);
//
//                        //for debugging use 
//                        WriteOutMethodBody(explorableType, previewUpdate);
//
//                        yield return new PexExplorableCandidate(explorable, false, update);
//                    }
//
//                /*
//                orderedMethodEffects.Sort();
//
//                foreach (var entry in orderedMethodEffects)
//                {
//                    if (entry.Effects.WrittenInstanceFields.Count == 0)
//                        continue;
//                    SafeDebug.Assert(tempSet.Count == 0, "tempSet.Count == 0");
//                    tempSet.AddRange(entry.Effects.WrittenInstanceFields);
//                    tempSet.RemoveRange(allWrittenFields);
//                    if (tempSet.Count > 0)
//                    {
//                        allWrittenFields.AddRange(tempSet);
//                        tempSet.Clear();
//                        if (!localExplorableFactory.TryAddMethodSetter(entry.Method))
//                            SafeDebug.Fail("we checked before that it is valid");
//                    }
//                }
//                 * */
//
//                #endregion
//            }
//
//            private void WriteOutMethodBody(TypeEx explorableType, CodeUpdate.AddMethodCodeUpdate originalPreviewUpdate)
//            {
//                /*
//                if (database.isDebug)
//                {
//                    string originalPreview = originalPreviewUpdate.GetPreviewDescription(() => this.Host.Services.LanguageManager.DefaultLanguage);
//                    database.AddFactoryMethodForDebug(explorableType.FullName, originalPreview);
//                }
//               * */
//            }
//        }
//    }
//}