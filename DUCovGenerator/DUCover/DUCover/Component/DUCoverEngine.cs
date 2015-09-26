using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.ExtendedReflection.Metadata;
using PexMe.Core;
using Microsoft.Pex.Engine.Logging;
using DUCover.Core;

namespace DUCover.Component
{
    public class DUCoverEngine
        : Engine
    {
        public DUCoverEngine()
            : base(new Container(new TypeEx[] { Microsoft.ExtendedReflection.Metadata.Metadata<IEngineOptions>.Type }), new IComponent[] { })
        {
            EngineOptions options = new EngineOptions();
            this.AddComponent("options", options);
            this.AddComponents();

            var pmd = new PexMeDynamicDatabase();
            pmd.AssemblyName = System.Environment.GetEnvironmentVariable(DUCoverConstants.DUCoverAssemblyVar);
            this.AddComponent("pmd", pmd);

            var psd = new PexMeStaticDatabase();
            psd.AssemblyName = System.Environment.GetEnvironmentVariable(DUCoverConstants.DUCoverAssemblyVar);
            this.AddComponent("psd", psd);
        }

        protected override void AddComponents()
        {
            base.AddComponents();
            base.AddSourceManager();
            base.AddSymbolManager();
            base.AddLog();                        
        }
    }
}
