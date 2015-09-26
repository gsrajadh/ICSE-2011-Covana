using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;

namespace DUCover.SideEffectAnalyzer
{
    /// <summary>
    /// Represents a persistent method store
    /// </summary>
    [Serializable]
    [__DoNotInstrument]
    public class SEMethodStore
    {
        /// <summary>
        /// Name of the method
        /// </summary>
        string methodName;
        public string MethodName
        {
            get { return this.methodName; }
        }        
        
        /// <summary>
        /// Set of fields defined by this method
        /// </summary>
        Dictionary<string, SEFieldStore> defFieldSet = new Dictionary<string, SEFieldStore>();
        public Dictionary<string, SEFieldStore> DefinedFieldSet
        {
            get { return this.defFieldSet; }
        }

        /// <summary>
        /// Set of fields used by this method
        /// </summary>
        Dictionary<string, SEFieldStore> useFieldSet = new Dictionary<string, SEFieldStore>();
        public Dictionary<string, SEFieldStore> UsedFieldSet
        {
            get { return this.useFieldSet; }
        }

        public Method OptionalMethod
        {
            get;
            set;
        }

        /// <summary>
        /// public constructor
        /// </summary>
        /// <param name="globalindex"></param>
        /// <param name="methodname"></param>
        public SEMethodStore(string methodname)
        {            
            this.methodName = methodname;
        }

        public override int GetHashCode()
        {
            return this.methodName.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            SEMethodStore other = obj as SEMethodStore;
            if (other == null)
                return false;

            return this.methodName == other.methodName;
        }

        public override string ToString()
        {
            return this.methodName;
        }

        /// <summary>
        /// Adds a field to defined list
        /// </summary>
        /// <param name="field"></param>
        public void AddToDefinedList(Field field, int offset)
        {
            SEFieldStore sef;
            if(!this.defFieldSet.TryGetValue(field.FullName, out sef))
            {
                sef = new SEFieldStore(field.FullName);
                sef.OptionalField = field;
                this.defFieldSet[field.FullName] = sef;
            }

            sef.AllOffsets.Add(offset);
        }

        /// <summary>
        /// Adds a field to loaded list
        /// </summary>
        public void AddToUsedList(Field field, int offset)
        {
            SEFieldStore sef;
            if (!this.useFieldSet.TryGetValue(field.FullName, out sef))
            {
                sef = new SEFieldStore(field.FullName);
                sef.OptionalField = field;
                this.useFieldSet[field.FullName] = sef;
            }

            sef.AllOffsets.Add(offset);
        }

        /// <summary>
        /// Appends a method store to the current one
        /// </summary>
        /// <param name="sem"></param>
        public void AppendMethodStore(SEMethodStore sem, int offset)
        {
            foreach (var defelem in sem.DefinedFieldSet.Values)
            {
                SEFieldStore sef;
                if (!this.defFieldSet.TryGetValue(defelem.FullName, out sef))
                {
                    sef = new SEFieldStore(defelem.FullName);
                    sef.OptionalField = defelem.OptionalField;
                    this.defFieldSet[defelem.FullName] = sef;
                }

                sef.AllOffsets.Add(offset);
            }

            foreach (var useelem in sem.UsedFieldSet.Values)
            {
                SEFieldStore sef;
                if (!this.useFieldSet.TryGetValue(useelem.FullName, out sef))
                {
                    sef = new SEFieldStore(useelem.FullName);
                    sef.OptionalField = useelem.OptionalField;
                    this.useFieldSet[useelem.FullName] = sef;
                }

                sef.AllOffsets.Add(offset);
            }
        }
    }
}
