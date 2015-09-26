using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Pex.Framework.ComponentModel;
using Microsoft.Pex.Engine.Packages;
using System.ComponentModel;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Metadata.Names;
using PexMe.ObjectFactoryObserver;
using Microsoft.ExtendedReflection.ComponentModel;

namespace PexMe.Attribute
{
    /// <summary>
    /// Provides the attribute "InsufficientObjectFactoryObserver" that can be annotated for the PUT
    /// </summary>
    public sealed class InsufficientObjectFactoryObserverAttribute
                : PexComponentElementDecoratorAttributeBase
                , IPexExplorationPackage
    {
        InsufficientObjectFactoryObserver iofo;

        /// <summary>
        /// Gets the name of this package.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return "InsufficientObjectFactoryObserver"; }
        }

        protected sealed override void Decorate(Name location, IPexDecoratedComponentElement host)
        {
            host.AddExplorationPackage(location, this);
        }

        #region IPexExplorationPackage Members
        void IPexExplorationPackage.Load(Microsoft.ExtendedReflection.ComponentModel.IContainer explorationContainer)
        {
            this.iofo = new InsufficientObjectFactoryObserver();
            explorationContainer.AddComponent("InsufficientObjectFactoryObserver", this.iofo);
        }

        void IPexExplorationPackage.Initialize(IPexExplorationEngine host)
        {
            //This is required to invoke the initialize() method of InsufficientObjectFactoryObserver
            var observer = ServiceProviderHelper.GetService<InsufficientObjectFactoryObserver>(host);          
        }

        object IPexExplorationPackage.BeforeExploration(IPexExplorationComponent host)
        { 
            return null; 
        }

        void IPexExplorationPackage.AfterExploration(IPexExplorationComponent host, object data)
        {
            if(this.iofo == null)
                this.iofo = ServiceProviderHelper.GetService<InsufficientObjectFactoryObserver>(host.Site);
            this.iofo.AfterExecution();
        }
        #endregion        
    }
}
