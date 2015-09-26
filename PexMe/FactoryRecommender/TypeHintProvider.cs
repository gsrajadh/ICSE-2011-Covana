using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Pex.Engine.Domains;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;
using PexMe.Core;
using Microsoft.ExtendedReflection.Logging;
using PexMe.ComponentModel;
using PexMe.Common;

namespace PexMe.FactoryRecommender
{
    /// <summary>
    /// Class that provides hints for interfaces 
    /// </summary>
    public class TypeHintProvider
        : IPexTypeHintProvider
    {
        PexMeDynamicDatabase pmd;
        PexMeStaticDatabase psd;

        public TypeHintProvider(PexMeDynamicDatabase pmd, PexMeStaticDatabase psd)
        {
            this.pmd = pmd;
            this.psd = psd;
        }
        
        #region IPexTypeHintProvider Members

        public bool TryGetTypeHints(TypeEx type, 
            out IIndexable<TypeDefinition> hints)
        {
            hints = null;
            if (!PexMeConstants.ENABLE_TYPE_HINT_PROVIDER)
                return false;

            hints = null;
            this.pmd.Log.LogMessage("Hint provider", "Requested for types of interface or class: " + type.FullName.ToString());
                        
            if (TypeAnalyzer.TryGetExtendingClasses(this.psd, type, out hints))
                return true;
            
            return false;
        }

        #endregion
    }
}
