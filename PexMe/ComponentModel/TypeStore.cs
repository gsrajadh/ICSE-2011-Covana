using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;

namespace PexMe.ComponentModel
{
    /// <summary>
    /// Represents a persistent form of the type
    /// </summary>
    [Serializable]
    public class TypeStore
    {
        /// <summary>
        /// Actual types in this store
        /// </summary>
        TypeDefinition type;
        public TypeDefinition Type
        {
            get
            {
                return this.type;
            }
        }

        /// <summary>
        /// All those types that are either extending or implementing current type
        /// </summary>
        HashSet<TypeStore> extendingTypes = new HashSet<TypeStore>();

        public HashSet<TypeStore> ExtendingTypes
        {
            get
            {
                return this.extendingTypes;
            }
        }

        /// <summary>
        /// public constructor
        /// </summary>
        /// <param name="type"></param>
        public TypeStore(TypeDefinition type)
        {
            this.type = type;
        }

        public override int GetHashCode()
        {
            return this.type.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            TypeStore other = obj as TypeStore;
            if (other == null)
                return false;

            return this.type.Equals(other.type);
        }
    }
}
