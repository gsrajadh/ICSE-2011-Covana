using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Metadata;

namespace PexMe.Core
{
    /// <summary>
    /// Represents a method store that stores all details of methods observed so far
    /// Same class is used to store information for both static and dynamic databases
    /// </summary>
    [Serializable]
    public class MethodStore
    {
        /// <summary>
        /// Name of the method
        /// </summary>
        public Method methodName;

        /// <summary>
        /// Field read by the current method
        /// </summary>
        public SafeSet<Field> ReadFields = new SafeSet<Field>();

        /// <summary>
        /// Fields written by the current method
        /// </summary>
        public SafeSet<Field> WriteFields = new SafeSet<Field>();

        /// <summary>
        /// Methods invoking the current method. grouped as its type
        /// </summary>
        public SafeDictionary<TypeEx, SafeSet<Method>> CallingMethods = new SafeDictionary<TypeEx, SafeSet<Method>>();        

        /// <summary>
        /// Methods invoked by the current method
        /// </summary>
        public SafeSet<Method> CalledMethods = new SafeSet<Method>();        
    }
}
