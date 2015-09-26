using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using System.Collections;

namespace PexMe.FactoryRecommender
{
    /// <summary>
    /// Stores entire information about the factory methods that
    /// are constructed
    /// </summary>
    internal class FactoryMethodStore
    {
        /// <summary>
        /// Object type associated with the factory method
        /// </summary>
        public TypeEx objectType = null;

        /// <summary>
        /// Each string represents a factory method
        /// </summary>
        public IList<string> fmethods = new List<string>();        
    }
}
