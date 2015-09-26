using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Pex.Engine.ComponentModel;
using PexMe.Common;
using Microsoft.Pex.Engine.Drivers;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.ExtendedReflection.Utilities.Safe;
using PexMe.Core;
using Microsoft.ExtendedReflection.Metadata;

namespace PexMe.FieldAccess
{
    internal class FieldAccessPathObserver
        : PexPathComponentBase, IFieldAccessPathObserver
    {
        IFieldAccessExplorationObserver explorationObserver;
        PexMeDynamicDatabase pmd;
        PexMeStaticDatabase psd;
                
        #region IFieldAccessPathObserver Members
        public void Analyze()
        {
            //this.Log.LogMessage(PexMeLogCategories.MethodBegin, "Begin of FieldAccessPathObserver.Analyze() method");

            if(this.explorationObserver == null)
                this.explorationObserver = this.GetService<IFieldAccessExplorationObserver>();

            if (this.pmd == null)
                this.pmd = this.GetService<IPexMeDynamicDatabase>() as PexMeDynamicDatabase;

            if (this.psd == null)
                this.psd = this.GetService<IPexMeStaticDatabase>() as PexMeStaticDatabase;

            int framesCount = 0;
            int framesHandled = 0;
            int maxLevelCount = 16; //TODO: Why 16?

            pmd.LastExecutedFactoryMethodCallSequence = null;

            for (int level = 0; level < maxLevelCount; level++)
            {
                FieldAccessCollector controller = new FieldAccessCollector(this, 
                    this.explorationObserver, this.pmd, this.psd, level);

                this.pmd.DefectDetectingSequence = false;
                try
                {
                    using (IEngine trackingEngine = this.PathServices.TrackingEngineFactory.CreateTrackingEngine(controller))
                    {
                        IPexTrackingDriver driver = trackingEngine.GetService<IPexTrackingDriver>();
                        if (!driver.Run())
                            break;
                    }
                }
                catch (Exception ex)
                {
                    this.pmd.DefectDetectingSequence = true;
                }

                pmd.LastExecutedFactoryMethodCallSequence = controller.FactoryMethodCallSequence;
                pmd.LastExecutedCUTMethodCallSequence = controller.CUTMethodCallSequence;

                //StringBuilder sb = new StringBuilder();
                //foreach(Method m in pmd.LastExecutedMethodCallSequence)
                //{
                //    sb.Append(m.ToString() + "\n");
                //}
                //this.pmd.Log.LogMessage("debug", "Executed method call sequence " + sb.ToString());

                framesCount = SafeMath.Max(framesCount, controller.FramesCount);
                framesHandled += controller.FramesHandled;
                if (framesHandled > framesCount) framesHandled = framesCount;
                //this.Log.LogMessage(
                //    "FieldAccessObserver",
                //    "collecting data, {0:P} of all frames up to level {1} / {2}",
                //    ((double)framesHandled / framesCount), level, maxLevelCount);
                if (controller.FramesHandled == 0 || // did we make any progress?
                    framesHandled >= framesCount) // or have we processed all frames there are?
                    break;
            }

            //Gather the information of last visited term here to use it later in 
            //InsufficientObjectFactoryObserver.LogExplorableInsufficiency
            


            //this.Log.LogMessage(PexMeLogCategories.MethodEnd, "End of FieldAccessPathObserver.Analyze() method");
        }
        #endregion
    }
}
