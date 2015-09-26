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
    /// Represents a list of method sequences
    /// </summary>
    [Serializable]
    public class MethodSignatureSequenceList
    {
        private List<MethodSignatureSequence> list = new List<MethodSignatureSequence>();
        public List<MethodSignatureSequence> SequenceList
        {
            get
            {
                return list;
            }
        }

        public void Add(MethodSignatureSequence seq)
        {
            if (!this.list.Contains(seq))
                this.list.Add(seq);
        }

        public void AddRange(List<MethodSignatureSequence> seqlist)
        {
            foreach (var seq in seqlist)
            {
                if (!this.list.Contains(seq))
                    this.list.Add(seq);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var seq in this.list)
            {
                sb.AppendLine(seq.ToString());
            }
            return sb.ToString();
        }

        /// <summary>
        /// Set to true, if this is the complete sequence list.
        /// No need of any further additions to this list. TODO: To set this flag to true later.
        /// </summary>
        public bool IsSequenceListComplete = false;
    }
}
