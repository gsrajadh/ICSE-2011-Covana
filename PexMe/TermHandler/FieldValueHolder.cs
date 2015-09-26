using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PexMe.TermHandler
{
    public enum FieldValueType {INTEGER, SHORT, LONG, BOOLEAN, STRING, OBJECT};

    /**
     * Holds value for the field encountered. This class currently
     * supports INTEGER, BOOLEAN and STRING types
     **/
    public class FieldValueHolder
    {
        public FieldValueHolder(FieldValueType ftype)
        {
            this.ftype = ftype;
        }

        public FieldValueType ftype
        {
            set; get;
        }

        public int intValue
        {
            set;
            get;
        }

        public bool boolValue
        {
            set;
            get;
        }

        public string stringValue
        {
            set;
            get;
        }

        public short shortValue
        {
            get;
            set;
        }

        public long longValue
        {
            get;
            set;
        }

        public object objValue
        {
            get;
            set;
        }
    }
}
