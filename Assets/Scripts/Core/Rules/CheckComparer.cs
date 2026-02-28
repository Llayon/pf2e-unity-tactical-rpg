using PF2e.TurnSystem;

namespace PF2e.Core
{
    public delegate int InitiativeTiePolicy(in InitiativeEntry left, in InitiativeEntry right);

    public static class CheckComparer
    {
        /// <summary>
        /// Descending by check total (higher total acts first).
        /// </summary>
        public static int CompareByTotal(in CheckRoll left, in CheckRoll right)
        {
            return right.total.CompareTo(left.total);
        }

        /// <summary>
        /// Initiative ordering comparator.
        /// 1) higher total first
        /// 2) custom tie policy if provided
        /// 3) deterministic fallback by handle id ascending
        /// </summary>
        public static int CompareInitiative(in InitiativeEntry left, in InitiativeEntry right, InitiativeTiePolicy tiePolicy = null)
        {
            int byTotal = CompareByTotal(left.Roll, right.Roll);
            if (byTotal != 0)
                return byTotal;

            if (tiePolicy != null)
            {
                int byTiePolicy = tiePolicy(in left, in right);
                if (byTiePolicy != 0)
                    return byTiePolicy;
            }

            return left.Handle.Id.CompareTo(right.Handle.Id);
        }
    }
}
