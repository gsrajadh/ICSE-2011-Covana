using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Metadata;

namespace Covana.Analyzer
{
    [Serializable]
    public class FieldInfo
    {
        public bool Equals(FieldInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.name, name) && Equals(other.type, type) && Equals(other.declaringType, declaringType);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (FieldInfo)) return false;
            return Equals((FieldInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (name != null ? name.GetHashCode() : 0);
                result = (result*397) ^ (type != null ? type.GetHashCode() : 0);
                result = (result*397) ^ (declaringType != null ? declaringType.GetHashCode() : 0);
                return result;
            }
        }

        private string name;

        public string Name
        {
            get { return name; }
        }

        public string Type
        {
            get { return type; }
        }

        public string DeclaringType
        {
            get { return declaringType; }
        }

        private string type;
        private string declaringType;

        public FieldInfo(string name, string type, string declaringType)
        {
            this.name = name;
            this.type = type;
            this.declaringType = declaringType;

        }

        public override string ToString()
        {
            return "Field: " + Name + ", type: " + Type + ", declaring type: " + DeclaringType;
        }
    }

    [Serializable]
    public class CandidateObjectCreationProblem
    {
        private HashSet<FieldInfo> _involvedFields;
        public HashSet<FieldInfo> InvolvedFields
        {
            get { return _involvedFields; }
            set { _involvedFields = value; }
        }

        private FieldInfo _targetField;
        public FieldInfo TargetField
        {
            get { return _targetField; }
            set { _targetField = value; }
        }

        private string _detailDescription;
        public string DetailDescription
        {
            get { return _detailDescription; }
            set { _detailDescription = value; }
        }

        private BranchInfo _branchLocation;
        public BranchInfo BranchLocation
        {
            get { return _branchLocation; }
            set { _branchLocation = value; }
        }

        private string _targetType;
        

        public string TargetType
        {
            get { return _targetType; }
            set { _targetType = value; }
        }

        private string _targetObjectType;
        public string TargetObjectType
        {
            get { return _targetObjectType; }
            set { _targetObjectType = value; }
        }

        public bool Equals(CandidateObjectCreationProblem other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other._involvedFields, _involvedFields) && Equals(other._targetField, _targetField) && Equals(other._detailDescription, _detailDescription) && Equals(other._branchLocation, _branchLocation) && Equals(other._targetType, _targetType);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (CandidateObjectCreationProblem)) return false;
            return Equals((CandidateObjectCreationProblem) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (_involvedFields != null ? _involvedFields.GetHashCode() : 0);
                result = (result*397) ^ (_targetField != null ? _targetField.GetHashCode() : 0);
                result = (result*397) ^ (_detailDescription != null ? _detailDescription.GetHashCode() : 0);
                result = (result*397) ^ (_branchLocation != null ? _branchLocation.GetHashCode() : 0);
                result = (result*397) ^ (_targetType != null ? _targetType.GetHashCode() : 0);
                return result;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("============Candidate Obejct Creation Problem===============");
            try
            {
                sb.AppendLine("Description: " + DetailDescription);
                sb.AppendLine("Branch: " + BranchLocation);
                sb.AppendLine("InvolvedFields: ");
                if (InvolvedFields != null && InvolvedFields.Count > 0)
                {
                    foreach (var field in InvolvedFields)
                    {
                        sb.AppendLine(field.ToString());
                    }
                    sb.AppendLine("TargetField: " + TargetField);
                    sb.AppendLine("TargetType: " + TargetType);
                }
                else
                {
                    sb.AppendLine("No involved fields.");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("Ex:" + ex);
            }

            sb.AppendLine("==========================================================");

            return sb.ToString();
        }

        public string ShortDescription
        {
            get
            {
                if (TargetField == null)
                {
                    return "No related Field";
                }

                return "Object Creation: type: " + TargetType + " related field: " + TargetField.Name + " of type " +
                       TargetField.Type;
            }
        }
    }
}