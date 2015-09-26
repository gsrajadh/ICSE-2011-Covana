using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PexMe.Core;
using Microsoft.ExtendedReflection.Interpretation;
using Microsoft.Pex.Engine.ComponentModel;
using Microsoft.ExtendedReflection.Logging;
using PexMe.Common;
using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;

namespace PexMe.ObjectFactoryObserver
{
    /// <summary>
    /// Represents a method sequence, stored as sequence as ints.
    /// </summary>
    [Serializable]
    public class MethodSignatureSequence
        : IComparable<MethodSignatureSequence>
    {
        public List<string> Sequence = new List<string>();

        #region IComparable<MethodSignatureSequence> Members
        public int CompareTo(MethodSignatureSequence otherobj)
        {
            return otherobj.Sequence.Count - this.Sequence.Count;
        }
        #endregion

        public override bool Equals(object obj)
        {
            var otherobj = obj as MethodSignatureSequence;
            if (otherobj == null)
                return false;

            if (this.Sequence.Count == 0 || otherobj.Sequence.Count == 0)
                return false;

            if (this.Sequence.Count != otherobj.Sequence.Count)
                return false;

            IEnumerator<string> otheriter = otherobj.Sequence.GetEnumerator();
            otheriter.MoveNext();
            var otherelem = otheriter.Current;
            foreach (var thiselem in this.Sequence)
            {
                if (thiselem != otherelem)
                    return false;

                if (otheriter.MoveNext())
                    otherelem = otheriter.Current;
            }

            return true;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (Sequence.Count == 0)
                sb.AppendLine("Empty Sequence");
            else
            {
                foreach (var m in Sequence)
                    sb.AppendLine(m);
            }

            return sb.ToString();
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
