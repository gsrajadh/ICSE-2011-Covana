using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Pex.Framework.ComponentModel;
using Microsoft.Pex.Engine.Packages;
using Microsoft.ExtendedReflection.Metadata.Names;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.ComponentModel;
using PexMe.FieldAccess;
using PexMe.Core;
using PexMe.MSeqGen;
using PexMe.Common;

namespace PexMe.Attribute
{
    /// <summary>
    /// Provides attribute "FieldAccessObserver" that can be used to annotate the PUTs
    /// </summary>
    public sealed class FieldAccessObserverAttribute
            : PexComponentElementDecoratorAttributeBase
            , IPexExplorationPackage, IPexPathPackage
    {
        IFieldAccessExplorationObserver explorationObserver;
        IFieldAccessPathObserver pathObserver;
        PexMeDynamicDatabase pmd;
        

        /// <summary>
        /// Gets the name of this package.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return "FieldAccessObserver"; }
        }

        protected sealed override void Decorate(Name location, IPexDecoratedComponentElement host)
        {
            host.AddExplorationPackage(location, this);
            host.AddPathPackage(location, this);
        }
                
        #region IPexExplorationPackage Members Gets executed for each exploration
        void IPexExplorationPackage.Load(IContainer explorationContainer)
        {
            explorationObserver = new FieldAccessExplorationObserver();
            explorationContainer.AddComponent("", explorationObserver);            
        }

        void IPexExplorationPackage.Initialize(IPexExplorationEngine host) 
        {
            //This is required to invoke initialize method of FieldAccessExplorationObserver
            var explorationObserver = ServiceProviderHelper.GetService<IFieldAccessExplorationObserver>(host);
            this.pmd = host.GetService<IPexMeDynamicDatabase>() as PexMeDynamicDatabase;
        }

        /// <summary>
        /// Gets invoked befor the state of the exploration
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>
        object IPexExplorationPackage.BeforeExploration(IPexExplorationComponent host)
        {           
            this.pmd.CurrentPUTMethod = host.ExplorationServices.CurrentExploration.Exploration.Method;
            var currPUTSignature = MethodOrFieldAnalyzer.GetMethodSignature(pmd.CurrentPUTMethod);

            //Activate the methods of the current PUT from dormant
            foreach (var fss in pmd.FactorySuggestionsDictionary.Values)
            {
                foreach (var pucls in fss.locationStoreSpecificSequences.Values)
                {
                    if(pucls.IsDormat())
                        pucls.ActivateFromDormant(currPUTSignature);
                }
            }
            pmd.PendingExplorationMethods.Add(currPUTSignature);                         

            //This code includes MSeqGen related stuff, primarily for evaluating the combined approach
            //if (PexMeConstants.ENABLE_MSEQGEN_RECOMMENDER)
            //{
            //    var explorableManager = host.ExplorationServices.ExplorableManager;
            //    int numMethodsRecommended = 0;
            //    foreach (var explorableCandidate in mseqgen.GetExplorableCandidates())
            //    {
            //        explorableManager.AddExplorableCandidate(explorableCandidate);
            //        numMethodsRecommended++;
            //    }
            //
            //    host.Log.LogMessage("MSeqGenRecommender", "Recommended " + numMethodsRecommended + " factory methods");
            //}

            return null; 
        }

        /// <summary>
        /// Gets invoked after the exploration
        /// </summary>
        /// <param name="host"></param>
        /// <param name="data"></param>
        void IPexExplorationPackage.AfterExploration(IPexExplorationComponent host, object data)
        {
            if (this.explorationObserver == null)
                this.explorationObserver = host.GetService<IFieldAccessExplorationObserver>();

            this.explorationObserver.Dump();
        }
        #endregion

        #region IPexPathPackage Members Gets executed for each path within the exploration
        void IPexPathPackage.Load(IContainer pathContainer)
        {
            pathObserver = new FieldAccessPathObserver();
            pathContainer.AddComponent("", pathObserver);
        }

        /// <summary>
        /// Gets invoked before each run, which happens for each path in the exploration
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>
        object IPexPathPackage.BeforeRun(IPexPathComponent host)
        {
            return null;
        }

        /// <summary>
        /// Gets invoked after each run, which happens for each path in the exploration
        /// </summary>
        /// <param name="host"></param>
        /// <param name="data"></param>
        void IPexPathPackage.AfterRun(IPexPathComponent host, object data)
        {                      
            if(pathObserver != null)
                pathObserver = host.GetService<IFieldAccessPathObserver>();
            pathObserver.Analyze();
            

            //aggregate the coverage 
            this.pmd.AccumulateMaxCoverage(host.ExplorationServices.Driver.TotalCoverageBuilder);

            //Check whether any uncovered locations are covered and then store the associated sequence
            this.pmd.CheckForNewlyCoveredLocations();
        }
        #endregion
    }
}
