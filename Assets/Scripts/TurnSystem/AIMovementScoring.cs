using UnityEngine;

namespace PF2e.TurnSystem
{
    public readonly struct AIMovementCellScore
    {
        public readonly Vector3Int cell;
        public readonly int actionCost;
        public readonly int distanceToTargetFeet;
        public readonly int terrainPressure;
        public readonly int hostileThreatCount;
        public readonly bool inMeleeRange;

        public AIMovementCellScore(
            Vector3Int cell,
            int actionCost,
            int distanceToTargetFeet,
            int terrainPressure,
            int hostileThreatCount = 0,
            bool inMeleeRange = false)
        {
            this.cell = cell;
            this.actionCost = actionCost;
            this.distanceToTargetFeet = distanceToTargetFeet;
            this.terrainPressure = Mathf.Max(0, terrainPressure);
            this.hostileThreatCount = Mathf.Max(0, hostileThreatCount);
            this.inMeleeRange = inMeleeRange;
        }
    }

    public static class AIMovementScoring
    {
        public const int TrapAvoidanceDistanceToleranceFeet = 5;

        public static bool IsBetterAdjacentApproach(in AIMovementCellScore candidate, in AIMovementCellScore best)
        {
            if (candidate.actionCost < best.actionCost) return true;
            if (candidate.actionCost > best.actionCost) return false;
            if (candidate.terrainPressure < best.terrainPressure) return true;
            if (candidate.terrainPressure > best.terrainPressure) return false;
            if (candidate.distanceToTargetFeet < best.distanceToTargetFeet) return true;
            if (candidate.distanceToTargetFeet > best.distanceToTargetFeet) return false;
            return IsDeterministicallyEarlier(candidate.cell, best.cell);
        }

        public static bool IsBetterFallbackApproach(in AIMovementCellScore candidate, in AIMovementCellScore best)
        {
            bool candidateMuchSafer = candidate.terrainPressure + HazardousTerrainRules.DefaultHazardousTerrainPressure <= best.terrainPressure
                && candidate.distanceToTargetFeet <= best.distanceToTargetFeet + TrapAvoidanceDistanceToleranceFeet;
            if (candidateMuchSafer) return true;

            bool bestMuchSafer = best.terrainPressure + HazardousTerrainRules.DefaultHazardousTerrainPressure <= candidate.terrainPressure
                && best.distanceToTargetFeet <= candidate.distanceToTargetFeet + TrapAvoidanceDistanceToleranceFeet;
            if (bestMuchSafer) return false;

            if (candidate.distanceToTargetFeet < best.distanceToTargetFeet) return true;
            if (candidate.distanceToTargetFeet > best.distanceToTargetFeet) return false;
            if (candidate.terrainPressure < best.terrainPressure) return true;
            if (candidate.terrainPressure > best.terrainPressure) return false;
            if (candidate.actionCost < best.actionCost) return true;
            if (candidate.actionCost > best.actionCost) return false;
            return IsDeterministicallyEarlier(candidate.cell, best.cell);
        }

        public static bool IsBetterThreatEscape(in AIMovementCellScore candidate, in AIMovementCellScore best)
        {
            if (candidate.hostileThreatCount < best.hostileThreatCount) return true;
            if (candidate.hostileThreatCount > best.hostileThreatCount) return false;
            if (candidate.terrainPressure < best.terrainPressure) return true;
            if (candidate.terrainPressure > best.terrainPressure) return false;
            if (candidate.inMeleeRange && !best.inMeleeRange) return true;
            if (!candidate.inMeleeRange && best.inMeleeRange) return false;
            if (candidate.distanceToTargetFeet < best.distanceToTargetFeet) return true;
            if (candidate.distanceToTargetFeet > best.distanceToTargetFeet) return false;
            return IsDeterministicallyEarlier(candidate.cell, best.cell);
        }

        public static bool IsBetterThreatenedMeleeSetup(in AIMovementCellScore candidate, in AIMovementCellScore best)
        {
            if (candidate.hostileThreatCount < best.hostileThreatCount) return true;
            if (candidate.hostileThreatCount > best.hostileThreatCount) return false;
            if (candidate.terrainPressure < best.terrainPressure) return true;
            if (candidate.terrainPressure > best.terrainPressure) return false;
            if (candidate.distanceToTargetFeet < best.distanceToTargetFeet) return true;
            if (candidate.distanceToTargetFeet > best.distanceToTargetFeet) return false;
            return IsDeterministicallyEarlier(candidate.cell, best.cell);
        }

        private static bool IsDeterministicallyEarlier(Vector3Int cell, Vector3Int bestCell)
        {
            if (cell.x != bestCell.x) return cell.x < bestCell.x;
            if (cell.y != bestCell.y) return cell.y < bestCell.y;
            return cell.z < bestCell.z;
        }
    }
}
