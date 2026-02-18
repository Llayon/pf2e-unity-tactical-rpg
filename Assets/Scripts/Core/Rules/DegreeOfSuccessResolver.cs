using UnityEngine;

namespace PF2e.Core
{
    public enum DegreeOfSuccess : byte
    {
        CriticalFailure = 0,
        Failure = 1,
        Success = 2,
        CriticalSuccess = 3
    }

    public static class DegreeOfSuccessResolver
    {
        public static DegreeOfSuccess Resolve(int total, int naturalRoll, int dc)
        {
            DegreeOfSuccess baseDegree;

            if (total >= dc + 10) baseDegree = DegreeOfSuccess.CriticalSuccess;
            else if (total >= dc) baseDegree = DegreeOfSuccess.Success;
            else if (total <= dc - 10) baseDegree = DegreeOfSuccess.CriticalFailure;
            else baseDegree = DegreeOfSuccess.Failure;

            if (naturalRoll == 20) baseDegree = Upgrade(baseDegree);
            else if (naturalRoll == 1) baseDegree = Downgrade(baseDegree);

            return baseDegree;
        }

        private static DegreeOfSuccess Upgrade(DegreeOfSuccess d)
            => (DegreeOfSuccess)Mathf.Min((int)DegreeOfSuccess.CriticalSuccess, (int)d + 1);

        private static DegreeOfSuccess Downgrade(DegreeOfSuccess d)
            => (DegreeOfSuccess)Mathf.Max((int)DegreeOfSuccess.CriticalFailure, (int)d - 1);
    }
}
