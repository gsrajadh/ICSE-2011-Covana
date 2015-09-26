namespace CoverageAnalysisForm
{
    public class Problem
    {
        public ProblemKind Kind { get; set;}
        public string Description { get; set;}
        public string Type { get; set; }

        public Problem(ProblemKind kind, string type, string description)
        {
            Kind = kind;
            Type = type;
            Description = description;
        }

        public bool Equals(Problem other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Kind, Kind) && Equals(other.Type, Type);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (Problem)) return false;
            return Equals((Problem) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Kind.GetHashCode()*397) ^ (Type != null ? Type.GetHashCode() : 0);
            }
        }
    }
}