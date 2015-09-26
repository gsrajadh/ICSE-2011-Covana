using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Metadata;

namespace DUCover.Core
{
    /// <summary>
    /// Entry for the DUCover table
    /// </summary>
    public class DUCoverStoreEntry
    {
        internal Field Field;
        internal Method DefMethod;
        internal int DefOffset;
        internal Method UseMethod;
        internal int UseOffset;

        /// <summary>
        /// Represents that the Defintion part is not sure
        /// </summary>
        internal bool DefUnsure = false;
        internal Method Def_UnknownSideEffectMethod = null;

        /// <summary>
        /// Represents that the Usage part is not sure
        /// </summary>
        internal bool UseUnsure = false;
        internal Method Use_UnknownSideEffectMethod = null;
               

        public DUCoverStoreEntry(Field Field, Method DefMethod, int DefOffset,
            Method UseMethod, int UseOffset)
        {
            this.Field = Field;
            this.DefMethod = DefMethod;
            this.DefOffset = DefOffset;
            this.UseMethod = UseMethod;
            this.UseOffset = UseOffset;
        }

        public override bool Equals(object obj)
        {
            var dcse = obj as DUCoverStoreEntry;

            if (dcse == null)
                return false;

            if (!this.Field.Equals(dcse.Field))
                return false;

            if (!this.DefMethod.Equals(dcse.DefMethod))
                return false;

            if (this.DefOffset != dcse.DefOffset)
                return false;

            if (!this.UseMethod.Equals(dcse.UseMethod))
                return false;

            if (this.UseOffset != dcse.UseOffset)
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return this.Field.FullName.GetHashCode();
        }

        public override string ToString()
        {
            var dcs = DUCoverStore.GetInstance();
            var dicKey = this.DefMethod.FullName + this.DefOffset + "(" + dcs.GetLineNumberOfOffset(this.DefMethod, this.DefOffset) + ")"
                + "##" + this.UseMethod.FullName + this.UseOffset + "(" + dcs.GetLineNumberOfOffset(this.UseMethod, this.UseOffset) + ")";
            return dicKey;
        }
    }
}
