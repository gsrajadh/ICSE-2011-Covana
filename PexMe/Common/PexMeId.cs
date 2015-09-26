using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ExtendedReflection.Interpretation;

namespace PexMe.Common
{
    class PexMeId : IObjectId
    {
        int index;

        public PexMeId(int index)
        {
            this.index = index;
        }

        public override bool Equals(object obj)
        {
            PexMeId someId = obj as PexMeId;
            return someId != null && someId.index == this.index;
        }

        public override int GetHashCode()
        {
            return this.index;
        }

        #region IObjectId Members

        public string Description
        {
            get { return "some." + this.index.ToString(); }
        }

        public bool TrackFieldAccesses
        {
            get { return false; }
        }

        public ObjectCreationTime CreationTime
        {
            get { return ObjectCreationTime.Unknown; }
        }

        public bool IsFullyDefined
        {
            get { return false; }
        }

        public Int64 GetPersistentHashCode()
        {
            return 0; // TODO
        }
        #endregion
    }
}
