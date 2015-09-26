using System;

namespace Covana
{
    [Serializable]
    public class BranchInfo
    {
        public string Document { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public int EndColumn { get; set; }
        public string Method { get; set; }
        public int ILOffset { get; set; }

        public BranchInfo(string document, int line, int column, int endColumn, string method, int ilOffset)
        {
            Document = document;
            Line = line;
            Column = column;
            EndColumn = endColumn;
            Method = method;
            ILOffset = ilOffset;
        }

        public BranchInfo ToLocation
        {
            get
            {
                return new BranchInfo(Document, Line, Column, EndColumn,
                                      Method, 0);
            }
        }

        public override string ToString()
        {
            return Document + "," + Line + ",[" + Column + "," + EndColumn + "],IL Offset: " + ILOffset.ToString("x") + "\n " + Method;
        }

        public bool Equals(BranchInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Document, Document) && other.Line == Line && other.Column == Column && other.EndColumn == EndColumn && Equals(other.Method, Method) && other.ILOffset == ILOffset;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (BranchInfo)) return false;
            return Equals((BranchInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (Document != null ? Document.GetHashCode() : 0);
                result = (result*397) ^ Line;
                result = (result*397) ^ Column;
                result = (result*397) ^ EndColumn;
                result = (result*397) ^ (Method != null ? Method.GetHashCode() : 0);
                result = (result*397) ^ ILOffset;
                return result;
            }
        }
    }
}