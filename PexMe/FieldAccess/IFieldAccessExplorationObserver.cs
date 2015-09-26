using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.ComponentModel;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Interpretation;

namespace PexMe.FieldAccess
{
    /// <summary>
    /// Gets invoked for each exploration
    /// </summary>
    public interface IFieldAccessExplorationObserver
        : IService, IComponent
    {
        /// <summary>
        /// Debugging method for dumping the logging information
        /// </summary>
        void Dump();

        /// <summary>
        /// Rewrites the term and then stores the monitored field into the database
        /// </summary>
        /// <param name="method"></param>
        /// <param name="f"></param>
        /// <param name="indices"></param>
        /// <param name="fieldValue"></param>
        /// <param name="initialValue"></param>
        void HandleMonitoredField(Method method, Field f, Term[] indices, Term fieldValue, Term initialValue);
    }
}
