using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;
using Microsoft.ExtendedReflection.Metadata;

namespace Covana.Analyzer
{

    ///<summary>
    ///Constants that describe how the field is modified
    ///</summary>
    public enum FieldModificationType { INCREMENT, DECREMENT, NON_NULL_SET, NULL_SET, UNKNOWN, TRUE_SET, FALSE_SET };

    /// <summary>
    /// Stores the fields modified by methods. Related to static analysis
    /// </summary>
    [Serializable]
    public class MethodEffects
    {
        /// <summary>
        /// Fields that are modified by the method
        /// </summary>
        public readonly IFiniteSet<Field> WrittenInstanceFields;

        /// <summary>
        /// Fields that are directly set by the method. 
        /// </summary>
        public readonly IFiniteSet<Field> DirectSetterFields;

        /// <summary>
        /// All fields returned by this method
        /// </summary>
        public readonly IFiniteSet<Field> ReturnFields;

        /// <summary>
        /// All directly called methods from the current method
        /// </summary>
        public readonly IFiniteSet<Method> DirectCalledMethods;

        /// <summary>
        /// Stores how the field is modified
        /// </summary>
        public readonly SafeDictionary<Field, FieldModificationType> ModificationTypeDictionary;

        public readonly int CallDepth;

        public MethodEffects(IFiniteSet<Field> writtenInstanceFields,
                             IFiniteSet<Field> directSetFields, IFiniteSet<Method> directCalledMethods, IFiniteSet<Field> returnFields,
                             SafeDictionary<Field, FieldModificationType> modificationTypeDic,
                             int callDepth)
        {
            SafeDebug.AssumeNotNull(writtenInstanceFields, "writtenInstanceFields");
            SafeDebug.Assume(callDepth >= 0, "callDepth>=0");

            this.WrittenInstanceFields = writtenInstanceFields;
            this.DirectSetterFields = directSetFields;
            this.DirectCalledMethods = directCalledMethods;
            this.ReturnFields = returnFields;
            this.ModificationTypeDictionary = modificationTypeDic;
            this.CallDepth = callDepth;
        }
    }
}