using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.Pex.Engine;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.Pex.Engine.Coverage;
using Microsoft.Pex.Engine.Packages;
using Microsoft.Pex.Framework.Packages;

namespace Covana
{
    public class ProblemTrackAttribute : PexExecutionPackageAttributeBase
                                       , IPexExecutionPackage
    {
        private AssemblyEx assemblyUnderTest;

        protected override void Load(IContainer engineContainer)
        {
            engineContainer.AddComponent(null, new ProblemTrackDatabase());
            base.Load(engineContainer);
        }

        protected override object BeforeExecution(IPexComponent host)
        {
            assemblyUnderTest = host.Services.CurrentAssembly.Assembly.Assembly;
            return null;
        }

        public void AfterExecution(IPexComponent host, object data)
        {
            var problemTrackDatabase = host.GetService<ProblemTrackDatabase>();
            problemTrackDatabase.ReportPath = host.Services.ReportManager.ReportPath;
           // host.Services.ReportManager.GeneratePexReport
            problemTrackDatabase.RelativePath = host.Services.ReportManager.RelativeRootPath;
            problemTrackDatabase.AssemblyUnderTest = assemblyUnderTest;
            problemTrackDatabase.AfterExecution();
        }
    }
}