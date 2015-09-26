using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using Microsoft.ExtendedReflection.Collections;
using __Substitutions.__Auxiliary;

namespace PexMe.Common
{
    /// <summary>
    /// All kinds of current filter used in our code
    /// </summary>
    public static class PexMeFilter
    {
        public static readonly IFiniteSet<TypeEx> PrimitiveTypes = Set.Enumerable<TypeEx>(null, new TypeEx[] {
            Metadata<System.Int16>.Type, Metadata<System.Int32>.Type, Metadata<System.Int64>.Type, Metadata<System.Boolean>.Type
        });

        /// <summary>
        /// Includes what are the types supported
        /// </summary>
        /// <returns></returns>
        public static bool IsTypeSupported(TypeEx type)
        {
            if (type.Spec == TypeSpec.SzArray || type.IsPrimitiveImmutable || PrimitiveTypes.Contains(type) ||
                type.IsStringType || type.Spec == TypeSpec.Class)
                return true;

            return false;
        }

        /// <summary>
        /// Includes what are the types supported
        /// </summary>
        /// <returns></returns>
        public static bool IsPrimitiveType(TypeEx type)
        {
            if (type.IsPrimitiveImmutable || PrimitiveTypes.Contains(type) || type.IsStringType)
                return true;

            return false;
        }
    }
}
