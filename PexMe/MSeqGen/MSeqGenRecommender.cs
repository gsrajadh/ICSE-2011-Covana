using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.Pex.Engine.Explorable;
using PexMe.Core;
using Microsoft.ExtendedReflection.Logging;
using Microsoft.ExtendedReflection.Feedback;
using Microsoft.ExtendedReflection.Utilities;
using Microsoft.ExtendedReflection.Collections;

namespace PexMe.MSeqGen
{
    /// <summary>
    /// Recommends MSeqGen extracted factories
    /// </summary>
    public sealed class MSeqGenRecommender
        : PexExplorationComponentBase, IService
    {
        AssemblyEx currAssembly;        
        SafeDictionary<string, SafeList<PexExplorableCandidate>> recommendedFactories = new SafeDictionary<string, SafeList<PexExplorableCandidate>>();
             
        protected override void Initialize()
        {
            base.Initialize();
            this.currAssembly = this.Services.CurrentAssembly.Assembly.Assembly;
            this.Log.LogMessage("MSeqGenRecommender", "MSeqGenRecommender initialized");
            this.LoadExplorableCandidates();
        }

        /// <summary>
        /// scans the assembly and identifies all MSeqGen generated factory methods
        /// </summary>
        /// <returns></returns>
        public void LoadExplorableCandidates()
        {
            var pmd = this.GetService<PexMeDynamicDatabase>();
            var fss = pmd.FactorySuggestionsDictionary;

            foreach (var tdef in this.currAssembly.TypeDefinitions)
            {
                if (!tdef.ShortName.EndsWith(MSeqGenConstants.FACTORY_CLASS_SUFFIX))
                    continue;

                foreach (var mdef in tdef.DeclaredStaticMethods)
                {
                    PexExplorableFactory originalExplorableFactory = null;
                    TypeEx retTypeEx = null;
                    try
                    {
                        var retType = mdef.ResultType;
                        
                        if (!MethodOrFieldAnalyzer.TryGetTypeExFromName(this, this.currAssembly, retType.ToString(), out retTypeEx))
                        {
                            this.Log.LogWarning(WikiTopics.MissingWikiTopic, "MSeqGenRecommender", "Failed to set typeex for " + retType.ToString());
                            continue;
                        }

                        //var retTypeEx = MetadataFromReflection.GetType(retType.GetType());
                        var methodEx = mdef.Instantiate(MethodOrFieldAnalyzer.GetGenericTypeParameters(this, retTypeEx.Definition), 
                            MethodOrFieldAnalyzer.GetGenericMethodParameters(this, mdef));
                                                
                        var result = PexExplorableFactory.TryGetExplorableFactory(this, retTypeEx, out originalExplorableFactory);
                        if (result == false)
                        {
                            this.Log.LogWarning(WikiTopics.MissingWikiTopic, "MSeqGenRecommender", "Failed to set create explorable for " + retTypeEx.FullName);
                            continue;
                        }

                        //add constructor
                        if (!originalExplorableFactory.TrySetFactoryMethod(methodEx))
                        {
                            this.Log.LogWarning(WikiTopics.MissingWikiTopic, "MSeqGenRecommender", "Failed to set factory method for " + mdef.FullName);
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.Log.LogError(WikiTopics.MissingWikiTopic, "MSeqGenRecommender", "Error occurred while parsing MSeqGen factories " + ex.Message);
                    }

                    if (originalExplorableFactory == null)
                        continue;

                    IPexExplorable originalExplorable1 = originalExplorableFactory.CreateExplorable();
                    CodeUpdate.AddMethodCodeUpdate originalPreviewUpdate1;
                    CodeUpdate originalUpdate1 = originalExplorableFactory.CreateExplorableFactoryUpdate(out originalPreviewUpdate1);
                    this.AddToRecommendedFactories(retTypeEx.FullName, new PexExplorableCandidate(originalExplorable1, false, originalUpdate1));
                }
            }            
        }

        public void AddToRecommendedFactories(string typeEx, PexExplorableCandidate factory)
        {
            SafeList<PexExplorableCandidate> existingList;
            if (!this.recommendedFactories.TryGetValue(typeEx, out existingList))
            {
                existingList = new SafeList<PexExplorableCandidate>();
                this.recommendedFactories.Add(typeEx, existingList);
            }

            existingList.Add(factory);
        }

        /// <summary>
        /// Returns the loaded factory methods
        /// </summary>
        /// <param name="typeEx"></param>
        /// <returns></returns>
        public IEnumerable<PexExplorableCandidate> GetMSeqGenFactories(TypeEx typeEx)
        {
            SafeList<PexExplorableCandidate> existingList;
            if (!this.recommendedFactories.TryGetValue(typeEx.FullName, out existingList))
                yield break;

            foreach (var factory in existingList)
                yield return factory;
        }

        /// <summary>
        /// Checks whether the factory methods of a type can be ignored. If there is ever an entry in FSS for a type,
        /// related factory method is always ignored
        /// </summary>
        /// <param name="retTypeEx"></param>
        /// <param name="fss"></param>
        /// <returns></returns>
        private bool IgnoreFMethodsOfType(TypeEx retTypeEx, Dictionary<string, ObjectFactoryObserver.FactorySuggestionStore> fss)
        {
            if (fss.Keys.Contains(retTypeEx.FullName))
                return true;

            return false;
        }
    }
}
