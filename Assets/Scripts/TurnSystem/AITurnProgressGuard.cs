using UnityEngine;
using PF2e.Core;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Detects repeated AI loop snapshots with no progress and requests turn abort.
    /// </summary>
    public sealed class AITurnProgressGuard
    {
        private readonly int maxNoProgressLoops;

        private bool hasSnapshot;
        private Vector3Int lastPosition;
        private int lastActionsRemaining;
        private EntityHandle lastTarget;
        private int noProgressStreak;

        public AITurnProgressGuard(int maxNoProgressLoops = 2)
        {
            this.maxNoProgressLoops = Mathf.Max(1, maxNoProgressLoops);
            Reset();
        }

        /// <summary>
        /// Returns true when no-progress streak reaches the configured threshold.
        /// </summary>
        public bool RegisterStep(Vector3Int actorPosition, int actionsRemaining, EntityHandle target)
        {
            if (!hasSnapshot)
            {
                hasSnapshot = true;
                lastPosition = actorPosition;
                lastActionsRemaining = actionsRemaining;
                lastTarget = target;
                noProgressStreak = 0;
                return false;
            }

            bool sameSnapshot = lastPosition == actorPosition
                                && lastActionsRemaining == actionsRemaining
                                && lastTarget == target;

            if (sameSnapshot)
            {
                noProgressStreak++;
                return noProgressStreak >= maxNoProgressLoops;
            }

            lastPosition = actorPosition;
            lastActionsRemaining = actionsRemaining;
            lastTarget = target;
            noProgressStreak = 0;
            return false;
        }

        public void Reset()
        {
            hasSnapshot = false;
            lastPosition = default;
            lastActionsRemaining = 0;
            lastTarget = EntityHandle.None;
            noProgressStreak = 0;
        }
    }
}
