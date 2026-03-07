using System;
using System.Collections.Generic;
using UnityEngine;

namespace PF2e.Core
{
    public static class JumpReachabilityResolver
    {
        private readonly struct Candidate
        {
            public readonly Vector3Int takeoffCell;
            public readonly int runUpFeet;
            public readonly int jumpDistanceFeet;
            public readonly JumpType jumpType;
            public readonly int dc;

            public Candidate(
                Vector3Int takeoffCell,
                int runUpFeet,
                int jumpDistanceFeet,
                JumpType jumpType,
                int dc)
            {
                this.takeoffCell = takeoffCell;
                this.runUpFeet = runUpFeet;
                this.jumpDistanceFeet = jumpDistanceFeet;
                this.jumpType = jumpType;
                this.dc = dc;
            }
        }

        /// <summary>
        /// Resolves the best jump preview for a target landing cell.
        /// strideReachabilityFeet contains cells reachable by the actor with one Stride and
        /// their stride cost in feet from current position.
        /// </summary>
        public static JumpPreviewResult ResolvePreview(
            Vector3Int actorCell,
            int speedFeet,
            Vector3Int landingCell,
            IReadOnlyDictionary<Vector3Int, int> strideReachabilityFeet,
            Func<Vector3Int, bool> isLandingCellValid,
            float heightStepFeet = GameConstants.CardinalCostFeet)
        {
            if (isLandingCellValid == null)
                return JumpPreviewResult.Invalid(JumpFailureReason.InvalidState, landingCell);

            if (!isLandingCellValid(landingCell))
                return JumpPreviewResult.Invalid(JumpFailureReason.InvalidLanding, landingCell);

            if (JumpRules.IsLeapPossible(actorCell, landingCell, speedFeet, heightStepFeet))
            {
                int directDistance = GridDistancePF2e.DistanceFeetXZ(actorCell, landingCell);
                return JumpPreviewResult.Valid(
                    JumpType.Leap,
                    JumpRules.LeapActionCost,
                    requiresCheck: false,
                    dc: 0,
                    requiresRunUp: false,
                    runUpFeet: 0,
                    jumpDistanceFeet: directDistance,
                    takeoffCell: actorCell,
                    landingCell: landingCell);
            }

            if (strideReachabilityFeet == null || strideReachabilityFeet.Count <= 0)
                return JumpPreviewResult.Invalid(JumpFailureReason.Unreachable, landingCell);

            bool found = false;
            Candidate best = default;

            foreach (var kvp in strideReachabilityFeet)
            {
                Vector3Int takeoffCell = kvp.Key;
                int strideCostFeet = Mathf.Max(0, kvp.Value);

                if (strideCostFeet < JumpRules.MinimumRunUpFeet || strideCostFeet > speedFeet)
                    continue;

                var jumpType = JumpRules.ClassifyJump(takeoffCell, landingCell, speedFeet, heightStepFeet);
                if (jumpType == JumpType.Leap)
                    continue;

                int jumpDistanceFeet = GridDistancePF2e.DistanceFeetXZ(takeoffCell, landingCell);
                int dc = jumpType == JumpType.LongJump
                    ? JumpRules.GetLongJumpDc(jumpDistanceFeet)
                    : JumpRules.HighJumpDc;

                var current = new Candidate(
                    takeoffCell,
                    strideCostFeet,
                    jumpDistanceFeet,
                    jumpType,
                    dc);

                if (!found || IsBetter(in current, in best))
                {
                    best = current;
                    found = true;
                }
            }

            if (!found)
                return JumpPreviewResult.Invalid(JumpFailureReason.MissingRunUp, landingCell);

            return JumpPreviewResult.Valid(
                best.jumpType,
                JumpRules.GetActionCost(best.jumpType),
                requiresCheck: true,
                dc: best.dc,
                requiresRunUp: true,
                runUpFeet: best.runUpFeet,
                jumpDistanceFeet: best.jumpDistanceFeet,
                takeoffCell: best.takeoffCell,
                landingCell: landingCell);
        }

        private static bool IsBetter(in Candidate current, in Candidate best)
        {
            // Prefer lower DC, then shorter run-up, then deterministic position order.
            if (current.dc != best.dc)
                return current.dc < best.dc;

            if (current.runUpFeet != best.runUpFeet)
                return current.runUpFeet < best.runUpFeet;

            if (current.takeoffCell.x != best.takeoffCell.x)
                return current.takeoffCell.x < best.takeoffCell.x;

            if (current.takeoffCell.y != best.takeoffCell.y)
                return current.takeoffCell.y < best.takeoffCell.y;

            return current.takeoffCell.z < best.takeoffCell.z;
        }
    }
}
