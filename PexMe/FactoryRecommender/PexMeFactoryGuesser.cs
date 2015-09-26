using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.Pex.Engine.Explorable;
using Microsoft.ExtendedReflection.Metadata;
using PexMe.Core;
using PexMe.Common;
using PexMe.ObjectFactoryObserver;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using Microsoft.ExtendedReflection.Utilities.Safe;
using Microsoft.ExtendedReflection.Feedback;
using PexMe.ComponentModel.Hardcoded;
using PexMe.MSeqGen;
using Microsoft.ExtendedReflection.ComponentModel;
using PexMe.ComponentModel;

namespace PexMe.FactoryRecommender
{
    internal class PexMeFactoryGuesser
        : PexComponentElementBase, IPexExplorableGuesser
    {
        IPexComponent host;
        PexMeDynamicDatabase pmd;
        PexMeStaticDatabase psd;
        MSeqGenRecommender mseqgen = null;

        public PexMeFactoryGuesser(IPexComponent host)
            : base(host)
        {
            this.host = host;
            this.pmd = host.GetService<IPexMeDynamicDatabase>() as PexMeDynamicDatabase;
            this.psd = host.GetService<IPexMeStaticDatabase>() as PexMeStaticDatabase;
        }

        struct OrderedMethodEffects : IComparable<OrderedMethodEffects>
        {
            public readonly Method Method;
            public readonly MethodEffects Effects;
            public OrderedMethodEffects(IPexComponent host, Method method)
            {
                SafeDebug.AssumeNotNull(method, "method");
                this.Method = method;

                TypeEx declaringType;
                if(!method.TryGetDeclaringType(out declaringType))
                {
                    //TODO: error
                }

                MethodOrFieldAnalyzer.TryComputeMethodEffects(host, declaringType, method, null, out this.Effects);
            }

            #region IComparable<OrderedMethodEffects> Members
            int IComparable<OrderedMethodEffects>.CompareTo(OrderedMethodEffects other)
            {
                return this.Effects.CallDepth - other.Effects.CallDepth;
            }
            #endregion
        }

        #region IPexExplorableGuesser Members
        /// <summary>
        /// Call-back that gets invoked when Pex requires a factory method
        /// for a specific type.
        /// TODO: May not be invoked when there is some existing factory method
        /// already there and the new uncovered branch is due to object creation issue
        /// Or will be invoked only if Pex thinks that it needs a factory method.
        /// </summary>
        /// <param name="explorableType"></param>
        /// <returns></returns>
        public IEnumerable<PexExplorableCandidate> GuessExplorables(TypeEx explorableType)
        {
            SafeDebug.AssumeNotNull(explorableType, "explorableType");

            this.host.Log.LogMessage(PexMeLogCategories.MethodBegin, "Beginning of PexMeFactoryGuesser.GuessExplorables method");
            this.host.Log.LogMessage(PexMeLogCategories.Debug, "Requested for type: " + explorableType);
                       
            //A trick to make generics work properly with the combination of inheritance.
            //Check the class PreDefinedGenericClasses for more details of why this line of code is required
            PreDefinedGenericClasses.recentAccessedTypes.Add(explorableType.FullName);                   
                        
            var visibilityContext = VisibilityContext.Exported;
            if (!explorableType.IsVisible(visibilityContext))//if the type is not public
                yield break;

            //Giving mseqgen factories the highest preferemce
            if (PexMeConstants.ENABLE_MSEQGEN_RECOMMENDER)
            {
                if (mseqgen == null)
                    mseqgen = this.pmd.GetService<MSeqGenRecommender>();

                foreach (var factory in mseqgen.GetMSeqGenFactories(explorableType))
                    yield return factory;
            }

            //the following factory is not returned to be used in sequence explroation but used to check
            //whether things are valid
            PexExplorableFactory localExplorableFactory;
            bool result = PexExplorableFactory.TryGetExplorableFactory(this.host, explorableType, out localExplorableFactory);
            
            Method bestConstructorMethod = null;
            if (explorableType.DefaultConstructor == null)
            {
                #region scan visible constructors, order by call depth, select best constructor
                var orderedMethodEffectsList = new SafeList<OrderedMethodEffects>();
                var bestConstructor = new OrderedMethodEffects();
                bool bNoVisibleConstructors = true;
                foreach (var constructor in explorableType.GetVisibleInstanceConstructors(visibilityContext))
                {                    
                    if (!localExplorableFactory.IsValidFactoryMethod(constructor))
                        continue;
                    orderedMethodEffectsList.Add(new OrderedMethodEffects(this.host, constructor));
                    bNoVisibleConstructors = false;
                }

                if (!bNoVisibleConstructors)
                {
                    //Finding the default constructor. We always start with the default constructor
                    orderedMethodEffectsList.Sort();
                    foreach (var entry in orderedMethodEffectsList)
                    {
                        if (bestConstructor.Method == null ||
                            bestConstructor.Effects.WrittenInstanceFields.Count > entry.Effects.WrittenInstanceFields.Count)
                        {
                            bestConstructor = entry;
                        }
                    }

                    orderedMethodEffectsList.Clear();
                    if (bestConstructor.Method != null) //cannot find a constructor
                    {
                        bestConstructorMethod = bestConstructor.Method;
                    }
                }

                if (bestConstructorMethod == null)
                {
                    if (!TypeAnalyzer.TryGetProducingMethods(this.pmd, explorableType, out bestConstructorMethod))
                        yield break;
                }
                #endregion
            }
            else
                bestConstructorMethod = explorableType.DefaultConstructor;
                        
            #region default factory method from the original Pex: scan visible methods, order by call depth, add methods as setters

            //start building the method sequence            
            PexExplorableFactory originalExplorableFactory;
            result = PexExplorableFactory.TryGetExplorableFactory(this.Host, explorableType, out originalExplorableFactory);

            //add constructor
            if (!originalExplorableFactory.TrySetFactoryMethod(bestConstructorMethod))
            {
                SafeDebug.Fail("we checked before that it is valid");
                yield break;
            }

            IPexExplorable originalExplorable1 = originalExplorableFactory.CreateExplorable();
            CodeUpdate.AddMethodCodeUpdate originalPreviewUpdate1;
            CodeUpdate originalUpdate1 = originalExplorableFactory.CreateExplorableFactoryUpdate(out originalPreviewUpdate1);

            //return the original one after own suggested
            this.WriteOutMethodBody(explorableType, originalPreviewUpdate1);
            yield return new PexExplorableCandidate(originalExplorable1, false, originalUpdate1);            
            
            var fsuggestions = this.pmd.FactorySuggestionsDictionary;
            
            //No suggestions for this type are available
            FactorySuggestionStore fss;
            if (fsuggestions.Count != 0 && fsuggestions.TryGetValue(explorableType.ToString(), out fss))
            {
                var methodNameToMethodMapper = new SafeDictionary<string, Method>();
                var propertyNameToPropertyMapper = new SafeDictionary<string, Property>();

                //Trying to add the remaining method setters
                ExtractMethodsAndProperties(explorableType, methodNameToMethodMapper, propertyNameToPropertyMapper);

                //PexMe suggested factory methods
                foreach (var msequence in fss.GetSuggestedMethodSequences(this.pmd))
                {
                    PexExplorableFactory pexmeExplorableFactory;
                    result = PexExplorableFactory.TryGetExplorableFactory(this.host, explorableType, out pexmeExplorableFactory);

                    bool bRecommendThisFactoryMethod = false;
                    try
                    {
                        //Check whether the sequence includes a constructor
                        //If yes use the constructor
                        Method bestConstructorMethodSuggested = null;
                        foreach (var methodid in msequence.Sequence)
                        {
                            if (!methodid.Contains("..ctor("))
                                continue;

                            Method tempMethod;
                            if (methodNameToMethodMapper.TryGetValue(methodid, out tempMethod))
                            {
                                bestConstructorMethodSuggested = tempMethod;
                            }
                            else
                            {
                                this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "factoryguesser",
                                    "Failed to the get the method with ID: " + methodid);
                            }
                        }

                        if (bestConstructorMethodSuggested == null)
                        {
                            if (!pexmeExplorableFactory.TrySetFactoryMethod(bestConstructorMethod))
                            {
                                SafeDebug.Fail("we checked before that it is valid");
                                yield break;
                            }
                        }
                        else
                        {
                            if (!pexmeExplorableFactory.TrySetFactoryMethod(bestConstructorMethodSuggested))
                            {
                                this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "factoryguesser",
                                    "Failed to set best suggested constructor method " + bestConstructorMethodSuggested.FullName);
                                yield break;
                            }
                            else
                                bRecommendThisFactoryMethod = true;
                        }

                        //handle other methods
                        foreach (var methodid in msequence.Sequence)
                        {
                            if (methodid.Contains("..ctor("))
                                continue;

                            //Could be a setter method for the property
                            if (methodid.Contains("set_"))
                            {
                                Property prop;
                                if (propertyNameToPropertyMapper.TryGetValue(methodid, out prop))
                                {
                                    if (!pexmeExplorableFactory.TryAddPropertySetter(prop))
                                    {
                                        this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "factoryguesser",
                                            "Failed to add property " + prop.FullName + " to the factory method");
                                    }
                                    else
                                        bRecommendThisFactoryMethod = true;

                                    continue;
                                }
                            }

                            Method smethod;
                            if (methodNameToMethodMapper.TryGetValue(methodid, out smethod))
                            {
                                if (!pexmeExplorableFactory.TryAddMethodSetter(smethod))
                                {
                                    this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "factoryguesser",
                                        "Failed to add method " + smethod.FullName + " to the factory method");
                                }
                                else
                                    bRecommendThisFactoryMethod = true;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        this.host.Log.LogError(WikiTopics.MissingWikiTopic, "ExplorableGuesser", " Exception occurred while constructing factory from suggested sequence " + ex.Message);
                        continue;
                    }

                    //no method are being added to this sequence. This can be of no use.
                    if (!bRecommendThisFactoryMethod)
                        continue;

                    IPexExplorable originalExplorable = pexmeExplorableFactory.CreateExplorable();
                    CodeUpdate.AddMethodCodeUpdate originalPreviewUpdate;
                    CodeUpdate originalUpdate = pexmeExplorableFactory.CreateExplorableFactoryUpdate(out originalPreviewUpdate);
                    this.WriteOutMethodBody(explorableType, originalPreviewUpdate);
                    yield return new PexExplorableCandidate(originalExplorable, false, originalUpdate);
                }
            }

            

            #endregion
            yield break;
        }

        private void ExtractMethodsAndProperties(TypeEx explorableType, SafeDictionary<string, Method> methodNameToMethodMapper,
            SafeDictionary<string, Property> propertyNameToPropertyMapper)
        {
            for (TypeEx type = explorableType; type != null; type = type.BaseType)
            {
                if (type.ToString() == "System.Object")
                    continue;

                var typedef = type.Definition;
                foreach (var constructor in typedef.DeclaredInstanceConstructors)
                {
                    Method method = constructor.Instantiate(type.GenericTypeArguments, MethodOrFieldAnalyzer.GetGenericMethodParameters(host, constructor));
                    methodNameToMethodMapper[MethodOrFieldAnalyzer.GetMethodSignature(constructor)] = method;
                }

                foreach (var methodDefinition in typedef.DeclaredInstanceMethods)
                {
                    Method method = methodDefinition.Instantiate(type.GenericTypeArguments, MethodOrFieldAnalyzer.GetGenericMethodParameters(host, methodDefinition));
                    methodNameToMethodMapper[MethodOrFieldAnalyzer.GetMethodSignature(method)] = method;
                }

                foreach (var propDefinition in typedef.DeclaredProperties)
                {
                    Property prop = propDefinition.Instantiate(type.GenericTypeArguments);
                    if(prop.Setter != null)
                        propertyNameToPropertyMapper[MethodOrFieldAnalyzer.GetMethodSignature(prop.Setter)] = prop;
                }
            }
        }        

        private void WriteOutMethodBody(TypeEx explorableType, CodeUpdate.AddMethodCodeUpdate originalPreviewUpdate)
        {
            string originalPreview = originalPreviewUpdate.GetPreviewDescription(() => this.Host.Services.LanguageManager.DefaultLanguage);
            this.pmd.AddPexGeneratedFactoryMethod(explorableType.ToString(), originalPreview);
        }
        #endregion

        /// <summary>
        /// Given a uncovered code location and the associated terms, this
        /// method infers factory method for that code location. 
        /// 
        /// Assumes that the uncovered branch is mainly due to an object creating issue
        /// </summary>
        /// <returns></returns>
        public bool TryInferFactoryMethod(UncoveredCodeLocationStore ucls, out SafeSet<Method> suggestedMethods)
        {
            SafeDebug.AssumeNotNull(ucls, "ucls");

            if (ucls.AllFields.Count == 0)
            {
                this.host.Log.LogError(WikiTopics.MissingWikiTopic, "factoryguesser",
                    "No information about involving fields in the uncovered branch");
                suggestedMethods = null;
                return false;
            }
            
            //Check whether the feature is currently supported
            FieldModificationType fmt = this.GetRequiredFieldModificationType(ucls);
            if (!(fmt == FieldModificationType.NON_NULL_SET 
                || fmt == FieldModificationType.NULL_SET || fmt == FieldModificationType.INCREMENT || fmt == FieldModificationType.DECREMENT
                || fmt == FieldModificationType.FALSE_SET || fmt == FieldModificationType.TRUE_SET))
            {
                this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "factoryguesser",
                    "Format " + fmt.ToString() + " is not supported for suggesting factory methods");
                suggestedMethods = null;
                return false;
            }

            //Step 1: Get the exact type whose factory method is required for covering this branch.
            //Decided based on where there is a subsequent field which can be directly handled rather than the top field.
            //This step is moved to the place where the uncovered location is initially stored       
            
            //Step 2: Decide which methods of this type should be invoked. Use a bottomup approach
            //for inferrinfg the exact method            
            if (!this.GetTargetMethod(ucls, ucls.TargetField, out suggestedMethods))
            {
                this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "factoryguesser",
                    "Failed to retrieve the target method of field " + ucls.TargetField.FullName + " in type " + ucls.ExplorableType.FullName);
                suggestedMethods = null;
                return false;
            }

            var sb = new StringBuilder();
            foreach (var m in suggestedMethods)
                sb.AppendLine(MethodOrFieldAnalyzer.GetMethodSignature(m));                
            ucls.SuggestedMethodsforFactory = sb.ToString();                        

            //Create a factory suggestion store. This is expected by the rest of the code.
            FactorySuggestionStore fss;            
            if (!this.pmd.FactorySuggestionsDictionary.TryGetValue(ucls.ExplorableType.ToString(), out fss))
            {
                fss = new FactorySuggestionStore();
                fss.DeclaringType = ucls.ExplorableType.ToString();
                this.pmd.FactorySuggestionsDictionary[ucls.ExplorableType.ToString()] = fss;
            }
            
            return true;
        }

        /// <summary>
        /// Analyzes the currently implemented features and makes sure only relevant
        /// features that are implemented further are processed.
        /// </summary>
        /// <param name="ucls"></param>
        /// <returns></returns>
        private FieldModificationType GetRequiredFieldModificationType(UncoveredCodeLocationStore ucls)
        {
            return ucls.DesiredFieldModificationType;
        }        

        /// <summary>
        /// Gets the target method that need to be invoked for setting a field.
        /// Based on static analysis and later uses dynamic analysis for giving
        /// more priority to those methods that are identified through dynamic analysis also.
        /// </summary>
        /// <param name="ucls"></param>
        /// <param name="targetField"></param>
        /// <param name="declaringType"></param>
        /// <param name="targetMethods"></param>
        /// <returns></returns>
        private bool GetTargetMethod(UncoveredCodeLocationStore ucls, Field targetField,
            out SafeSet<Method> targetMethods)
        {
            targetMethods = new SafeSet<Method>();

            int numfields = ucls.AllFields.Count;
            for (int count = 0; count < numfields; count++)
            {
                var field = ucls.AllFields[count];

                //Get all write methods for this field                
                SafeSet<Method> writeMethods = null;
                
                //Get the declaring type of the field. There are two possibilities of choosing
                //a declaring type: from the next field or the enclosing type
                TypeEx declaringType1 = null, declaringType2 = null;                

                //If there is a parent field, the declaring type should
                //be upgraded to the type of the next field in the list, which could be 
                //a sub-class of the actual declaring type
                if (count < numfields - 1)
                {
                    var nextField = ucls.AllFields[count + 1];
                    declaringType1 = nextField.Type;
                }

                if (!MethodOrFieldAnalyzer.TryGetDeclaringTypeEx(this.host, field, out declaringType2))
                {
                    SafeDebug.AssumeNotNull(declaringType2, "declaringType");
                }

                var declaringType = declaringType2;
                if (declaringType1 != null && declaringType1 != declaringType2)
                    declaringType = this.ChooseADeclaringType(declaringType1, declaringType2);                            
           
                //Chosen declaringType should be a part of all field types stored
                //in UCLS. If not, there can be inheritance issues
                if (!ucls.AllFieldTypes.Contains(declaringType))
                {
                    //Find out the type to which declaringType is assignable and update it
                    foreach (var tex in ucls.AllFieldTypes)
                    {
                        if (tex.IsAssignableTo(declaringType))
                        {
                            declaringType = tex;
                            break;
                        }
                    }
                }

                //For the first field, get all the methods that modify the field
                //using static analysis
                if (targetMethods.Count == 0)
                {
                    //Try from static analysis store
                    if (!this.psd.TryGetFilteredWriteMethods(field, declaringType, ucls.DesiredFieldModificationType, out writeMethods))
                    {
                        this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "factoryguesser",
                            "Failed to get write methods for the field " + field.FullName);
                        return false;
                    }                    

                    //Try from dynamic analysis
                    //if (!this.pmd.FieldDictionary.TryGetValue(field.GlobalIndex, out fs))
                    targetMethods.AddRange(writeMethods);                    
                }
                else
                {
                    //Get the callers of all methods in targetmethods
                    SafeSet<Method> callerMethods = new SafeSet<Method>();
                    foreach (var tmw in targetMethods)
                    {                        
                        SafeSet<Method> callingMethods;
                        
                        //TODO: Performance improvements can be done here as we repeat the loops inside
                        //the method for each method in targetMethods
                        if (!this.psd.TryGetCallingMethodsInType(tmw, field, declaringType, out callingMethods))
                        {
                            this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "factoryguesser",
                                SafeString.Format("Failed to get calling methods of {0} through static and dynamic analysis", tmw));
                            continue;
                        }

                        //Filter the called methods based on field to avoid 
                        //unnecessary additional methods
                        SafeSet<Method> subcallingMethods = this.psd.FilterCallingMethodsBasedOnField(tmw, field, callingMethods);
                        if (callingMethods.Count > 0 && subcallingMethods.Count == 0)
                        {
                            this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "callingmethods",
                                "Failed to filter calling methods based on field, adding all methods");
                            subcallingMethods.AddRange(callingMethods);
                        }

                        //Check in dynamic analysis portion
                        //if (!this.pmd.MethodDictionary.TryGetValue(tmw.GlobalIndex, out mstore))
                        //{                                                        
                        //}
                        //else
                        //    callingMethods = mstore.CallingMethods[declaringType];                            

                        callerMethods.AddRange(subcallingMethods);
                    }

                    //All caller methods in the parent type
                    targetMethods = callerMethods;                    
                }

                //Our objective is to search for the methods that belong to our target field.
                //Stop traversal once the desired method is found
                if (field == targetField)
                    break;
            }
            
            return true;
        }        

        /// <summary>
        /// The parameters should be related through inheritance, which is a 
        /// pre-condition and both should not be interfaces. prefers declaringType2 over declaringType1, 
        /// incase conflict is not resolved, since declaringType2 is the actual fields declaring type
        /// </summary>
        /// <param name="declaringType1"></param>
        /// <param name="declaringType2"></param>
        /// <returns></returns>
        private TypeEx ChooseADeclaringType(TypeEx declaringType1, TypeEx declaringType2)
        {           
            //Ensure atleast one of them is not interface
            if (declaringType1.IsInterface && declaringType2.IsInterface)
                return null;

            //Ensure atleast one of them is not abstract
            if (declaringType1.IsAbstract && declaringType2.IsAbstract)
                return null;

            if (declaringType1.IsInterface || declaringType1.IsAbstract)
            {
                if(!declaringType2.IsAbstract)
                    return declaringType2;
            }

            if (declaringType2.IsInterface || declaringType2.IsAbstract)
            {
                if(!declaringType1.IsAbstract)
                    return declaringType1;
            }

            //If an object is assignable to another class, then the other class
            //is more abstract than the current class.
            if (declaringType2.IsAssignableTo(declaringType1))
                return declaringType2;
            else if (declaringType1.IsAssignableTo(declaringType2))
                return declaringType1;

            return declaringType2;            
        }

        /// <summary>
        /// Get an equivalent method of one type in another type such 
        /// as parent class or implementing interface
        /// </summary>
        /// <param name="tmw"></param>
        /// <param name="typeEx"></param>
        /// <returns></returns>
        private Method GetEquivalentMethodInOtherType(Method tmw, TypeEx typeEx)
        {
            //TODO: check whether there exist a relation (inheritance or implements)
            //relationship between the declaring type of tmw and typeEx
            Method equivalentMethod;
            typeEx.TryGetMethod(tmw.ShortName, tmw.ParameterTypes, out equivalentMethod);
            return equivalentMethod;
        }

        /// <summary>
        /// Get the exact type whose factory method is required for covering this branch.
        /// Decided based on where there is a subsequent field which can be directly handled 
        /// rather than the top field. The primary objective is to minimize the search space
        /// as much as possible.
        /// </summary>
        /// <param name="ucls"></param>
        /// <param name="targetField"></param>
        /// <returns></returns>
        public static bool GetTargetExplorableField(IPexComponent host, SafeList<Field> allFields, out Field targetField, out TypeEx declaringType)
        {
            targetField = null;
            var allInvolvedFields = new SafeList<Field>();
            allInvolvedFields.AddRange(allFields);

            int numFields = allInvolvedFields.Count;
            if (numFields == 1)
            {
                targetField = allInvolvedFields[0];
                if (!MethodOrFieldAnalyzer.TryGetDeclaringTypeEx(host, targetField, out declaringType))
                {
                    declaringType = null;
                    return false;
                }
                return true;
            }

            allInvolvedFields.Reverse();

            //Commenting the following functionality, which is actually intended
            //for reducing the enormous search space in the list of fields. However,
            //since our approach is not using the Pex's default factory method mechanism
            //this may not be of any use.
            //TypeEx prevFieldType = null;
            //for (int count = 0; count < allInvolvedFields.Count; count++)
            //{
            //    var currentField = allInvolvedFields[count];               

            //    if (!MethodOrFieldAnalyzer.TryGetDeclaringTypeDefinition(host, currentField, out declaringType))
            //    {
            //        host.Log.LogError(WikiTopics.MissingWikiTopic, "targetfield",
            //            "Failed to get the declaring type for the field " + currentField.FullName);
            //        return false;
            //    }

            //    SafeDebug.Assume(prevFieldType == null || prevFieldType == declaringType || prevFieldType.IsAssignableTo(declaringType)
            //        || declaringType.IsAssignableTo(prevFieldType),
            //        "The current field type (" + declaringType + ") should be the same as the previous field type (" + prevFieldType + ")");

            //    if (MethodOrFieldAnalyzer.IsFieldExternallyVisible(host, declaringType, currentField))
            //    {
            //        prevFieldType = currentField.Type;
            //        continue;
            //    }
            //    else
            //    {
            //        targetField = currentField;                    
            //        return true;
            //    }
            //}

            targetField = allInvolvedFields[0];
            if (!MethodOrFieldAnalyzer.TryGetDeclaringTypeEx(host, targetField, out declaringType))
            {
                host.Log.LogError(WikiTopics.MissingWikiTopic, "targetfield",
                        "Failed to get the declaring type for the field " + targetField.FullName);
                return false;
            }
            return true;
        }
    }
}
