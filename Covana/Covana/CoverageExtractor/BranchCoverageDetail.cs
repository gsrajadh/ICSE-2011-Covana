using System;
using Microsoft.ExtendedReflection.Metadata;

namespace Covana.CoverageExtractor
{
    public class BranchCoverageDetail
    {
        public BranchInfo BranchInfo { get; set; }
        public int CoveredTimes { get; set; }
        public BranchInfo TargetLocation { get; set; }
        public int targetCoveredTimes { get; set; }
        public string Type { get; set; }
        public bool IsBranch { get; set; }
        public bool IsCheck { get; set; }
        public bool IsContinue { get; set; }
        public bool IsFailedCheck { get; set; }
        public bool IsStartMethod { get; set; }
        public bool IsSwitch { get; set; }
        public bool IsTarget { get; set; }
        public int BranchLabel { get; set; }
        public int OutgoingLabel { get; set; }

        public BranchCoverageDetail(BranchInfo branchInfo, int coveredTimes, BranchInfo targetLocation, int targetCoveredTimes, string type)
        {
            BranchInfo = branchInfo;
            CoveredTimes = coveredTimes;
            TargetLocation = targetLocation;
            this.targetCoveredTimes = targetCoveredTimes;
            Type = type;
        }

        public BranchCoverageDetail()
        {
        }

        public bool Equals(BranchCoverageDetail other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.BranchInfo, BranchInfo) && Equals(other.TargetLocation, TargetLocation) && Equals(other.Type, Type) && other.IsBranch.Equals(IsBranch) && other.IsCheck.Equals(IsCheck) && other.IsContinue.Equals(IsContinue) && other.IsFailedCheck.Equals(IsFailedCheck) && other.IsStartMethod.Equals(IsStartMethod) && other.IsSwitch.Equals(IsSwitch) && other.IsTarget.Equals(IsTarget);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (BranchCoverageDetail)) return false;
            return Equals((BranchCoverageDetail) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (BranchInfo != null ? BranchInfo.GetHashCode() : 0);
                result = (result*397) ^ (TargetLocation != null ? TargetLocation.GetHashCode() : 0);
                result = (result*397) ^ (Type != null ? Type.GetHashCode() : 0);
                result = (result*397) ^ IsBranch.GetHashCode();
                result = (result*397) ^ IsCheck.GetHashCode();
                result = (result*397) ^ IsContinue.GetHashCode();
                result = (result*397) ^ IsFailedCheck.GetHashCode();
                result = (result*397) ^ IsStartMethod.GetHashCode();
                result = (result*397) ^ IsSwitch.GetHashCode();
                result = (result*397) ^ IsTarget.GetHashCode();
                return result;
            }
        }

        public void CopyBranchProperties(CodeBranch branch)
        {
            IsBranch = branch.IsBranch;
            IsCheck = branch.IsCheck;
            IsContinue = branch.IsContinue;
            IsFailedCheck = branch.IsFailedCheck;
            IsStartMethod = branch.IsStartMethod;
            IsSwitch = branch.IsSwitch;
            IsTarget = branch.IsTarget;
        }
    }
}