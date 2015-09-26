using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.ComponentModel;

namespace PexMe.FieldAccess
{
    /// <summary>
    /// Gets invoked for each path within the exploration
    /// </summary>
    public interface IFieldAccessPathObserver
        : IService, IComponent
    {
        /// <summary>
        /// Method that analyzes the path executed and stores the data
        /// </summary>
        void Analyze();
    }
}
