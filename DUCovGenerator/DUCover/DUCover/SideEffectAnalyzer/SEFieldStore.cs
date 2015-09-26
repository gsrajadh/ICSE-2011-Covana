using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;

namespace DUCover.SideEffectAnalyzer
{
    /// <summary>
    /// Represents a persistent field store for storing the side effects
    /// </summary>
    [Serializable]
    [__DoNotInstrument]
    public class SEFieldStore
    {        
        string fullname;

        /// <summary>
        /// Stores all the offsets where the field is defined or used
        /// </summary>
        HashSet<int> offsets = new HashSet<int>();
        public HashSet<int> AllOffsets
        {
            get { return this.offsets; }
        }

        /// <summary>
        /// Only an optional element, used ocasionally
        /// </summary>
        public Field OptionalField
        {
            get;
            set;
        }

        public string FullName
        {
            get { return this.fullname;  }
        }

        public SEFieldStore(string fullname)
        {            
            this.fullname = fullname;
        }
                
        public override int GetHashCode()
        {
            return this.fullname.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            SEFieldStore other = obj as SEFieldStore;
            if (other == null)
                return false;

            return this.fullname == other.fullname;
        }

        public override string ToString()
        {
            return this.fullname;
        }
    }
}
