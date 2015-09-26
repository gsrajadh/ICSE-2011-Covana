using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;
using PexMe.Core;

namespace PexMe.ComponentModel.Hardcoded
{
    /// <summary>
    /// Stores all predefined effects
    /// </summary>
    public class PreDefinedMethodEffectsStore
    {        
        /// <summary>
        /// Field
        /// </summary>
        Field field;

        /// <summary>
        /// Desired modification type
        /// </summary>
        FieldModificationType fmt;

        /// <summary>
        /// Suggested method
        /// </summary>
        public List<Method> suggestedmethodList
        {
            get;
            set;
        }

        /// <summary>
        /// public constructor
        /// </summary>
        /// <param name="type"></param>
        /// <param name="field"></param>
        /// <param name="fmt"></param>
        public PreDefinedMethodEffectsStore(Field field, FieldModificationType fmt)
        {            
            this.field = field;
            this.fmt = fmt;
        }

        public override int GetHashCode()
        {
            return field.GlobalIndex + fmt.ToString().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            PreDefinedMethodEffectsStore other = obj as PreDefinedMethodEffectsStore;
            if (other == null)
                return false;

            if (this.field != other.field)
                return false;

            if (this.fmt != other.fmt)
                return false;

            return true;
        }
    }
}
