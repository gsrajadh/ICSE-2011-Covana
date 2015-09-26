using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Interpretation;
using PexMe.Core;
using PexMe.Common;
using Microsoft.ExtendedReflection.Interpretation.Visitors;

namespace PexMe.FieldAccess
{
    internal class FieldAccessExplorationObserver
        : PexExplorationComponentBase, IFieldAccessExplorationObserver
    {
        IPexMeDynamicDatabase pmd;
        TermManager termManager;

        protected override void Initialize()
        {
            base.Initialize();
            this.pmd = this.GetService<IPexMeDynamicDatabase>();
            this.termManager = this.ExplorationServices.TermManager;
        }

        #region IFieldAccessExplorationObserver Members
        public void Dump()
        {
           
        }

        /// <summary>
        /// Rewrites the term and then stores the monitored field into the database
        /// </summary>
        /// <param name="method"></param>
        /// <param name="f"></param>
        /// <param name="indices"></param>
        /// <param name="fieldValue"></param>
        public void HandleMonitoredField(Method method, Field f, Term[] indices, Term fieldValue, Term initialValue)
        {
            pmd.AddMonitoredField(this.termManager, method, f, indices, fieldValue, initialValue);
        }
        #endregion
    }
}
