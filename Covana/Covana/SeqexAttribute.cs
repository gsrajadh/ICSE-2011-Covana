using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Pex.Framework.Packages;
using Microsoft.Pex.Engine.Packages;

namespace Covana
{
    //adding [assembly: Seqex] to the beginning of a PUT class can allow this Load() method is invoked 
    //and therefore we can have an object of SeqexDatabase to live across PUTs/paths
    public class SeqexAttribute
        : PexPackageAttributeBase
          , IPexExecutionPackage
    {
        protected override void Load(Microsoft.ExtendedReflection.ComponentModel.IContainer engineContainer)
        {
            engineContainer.AddComponent(null, new SeqexDatabase());
            base.Load(engineContainer);
        }

        protected override void Initialize(Microsoft.ExtendedReflection.ComponentModel.IEngine engine)
        {
            engine.GetService<SeqexDatabase>();
            base.Initialize(engine);
        }

        #region IPexExecutionPackage Members

        public void AfterExecution(Microsoft.Pex.Engine.ComponentModel.IPexComponent host, object data)
        {
            host.GetService<SeqexDatabase>().AfterExecution();
        }

        public object BeforeExecution(Microsoft.Pex.Engine.ComponentModel.IPexComponent host)
        {
            return null;
        }

        #endregion
    }
}