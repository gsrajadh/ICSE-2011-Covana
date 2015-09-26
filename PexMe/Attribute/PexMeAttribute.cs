using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Pex.Engine.Packages;
using Microsoft.Pex.Framework.Packages;
using System.ComponentModel;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.Pex.Engine.ComponentModel;
using PexMe.Core;
using PexMe.Common;
using Microsoft.Pex.Engine.Logging;
using PexMe.FactoryRecommender;
using PexMe.PersistentStore;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using Microsoft.ExtendedReflection.Logging;
using System.IO;
using PexMe.MSeqGen;

namespace PexMe.Attribute
{
    /// <summary>
    /// Enables adding attribute [assembly:PexMe] to the assembly.
    /// Adding the attribute 
    /// </summary>
    public class PexMeAttribute
        : PexPackageAttributeBase
        , IPexExecutionPackage
    {
        PexMeDynamicDatabase pmd = null;
        IPexMeStaticDatabase psd = null;
        MSeqGenRecommender mseqgen = null;
        
        IPexComponent host;

        public static string AssemblyUnderAnalysis = "";

        protected override void Load(Microsoft.ExtendedReflection.ComponentModel.IContainer container)
        {           
            this.pmd = new PexMeDynamicDatabase();
            container.AddComponent(null, pmd);

            this.psd = new PexMeStaticDatabase();            
            container.AddComponent(null, psd);

            if (PexMeConstants.ENABLE_MSEQGEN_RECOMMENDER)
            {
                mseqgen = new MSeqGenRecommender();
                container.AddComponent(null, mseqgen);
            }
            
            base.Load(container);          
        }

        protected override void Initialize(IEngine engine)
        {
            engine.Log.LogMessage(PexMeLogCategories.MethodBegin, "Begin of PexMeAttribute.Initialize() method");

            if (PexMeConstants.PexMeStorageDirectory == null || PexMeConstants.PexMeStorageDirectory.Length == 0)
            {
                engine.Log.LogError(WikiTopics.MissingWikiTopic, "Environment", "Environment variable SEEKER_REPOSITORY is not set. Please set to a valid directory");
                Environment.Exit(0);
            }

            base.Initialize(engine);
        }

        #region IPexExecutionPackage Members
        public object BeforeExecution(IPexComponent host)
        {         
            this.host = host;
            AssemblyUnderAnalysis = this.host.Services.CurrentAssembly.Assembly.Assembly.ShortName;         
            return null;
        }

        public void AfterExecution(IPexComponent host, object data)
        {
            if (this.pmd.CurrentPUTMethod == null)
            {
                this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "PUTExploration",
                    "Return, not current PUT method is set");
                WriteStopStatus();
                return;
            }

            SafeDebug.AssertNotNull(this.pmd.CurrentPUTMethod, "CurrentPUTMethod should be set by this time");
            var currPUTSignature = MethodOrFieldAnalyzer.GetMethodSignature(this.pmd.CurrentPUTMethod);            
            if (this.pmd.AllExploredMethods.Contains(currPUTSignature))
            {
                this.host.Log.LogWarning(WikiTopics.MissingWikiTopic, "PUTExploration",
                    "Ignoring the post processing of the PUT " + currPUTSignature + " since it is explored earlier!!!");
                WriteStopStatus();
                return;
            }

            //Add this to pending methods                  
            PexMePostProcessor ppp = new PexMePostProcessor(host);
            ppp.AfterExecution();
        }

        private static void WriteStopStatus()
        {
            //Dumping STOP status to external Perl script
            var filename = Path.Combine(PexMeConstants.PexMeStorageDirectory, PexMeConstants.ReExecutionStatusFile);
            using (StreamWriter sw = new StreamWriter(filename))
            {
                sw.WriteLine("STOP");
            }
        }
        #endregion
    }
}
