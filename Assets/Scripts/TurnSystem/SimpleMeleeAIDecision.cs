using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Pure decision helpers for simple melee AI.
    /// No scene/state mutation and no MonoBehaviour lifecycle.
    /// </summary>
    public static class SimpleMeleeAIDecision
    {
        /// <summary>
        /// Select nearest alive player on the same elevation.
        /// </summary>
        public static EntityHandle FindBestTarget(EntityData actor, IEnumerable<EntityData> allEntities)
        {
            if (actor == null || allEntities == null)
                return EntityHandle.None;

            EntityHandle best = EntityHandle.None;
            int bestDistFeet = int.MaxValue;

            foreach (var data in allEntities)
            {
                if (data == null) continue;
                if (!data.IsAlive) continue;
                if (data.Team != Team.Player) continue;
                if (data.GridPosition.y != actor.GridPosition.y) continue;

                int distFeet = GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, data.GridPosition);
                if (distFeet < bestDistFeet)
                {
                    bestDistFeet = distFeet;
                    best = data.Handle;
                }
            }

            return best;
        }

        public static bool IsInMeleeRange(EntityData attacker, EntityData target)
        {
            if (attacker == null || target == null) return false;
            if (attacker.GridPosition.y != target.GridPosition.y) return false;

            int distFeet = GridDistancePF2e.DistanceFeetXZ(attacker.GridPosition, target.GridPosition);
            return distFeet <= attacker.EquippedWeapon.ReachFeet;
        }

        /// <summary>
        /// Find cell to stride toward target.
        /// Priority:
        /// 1) Reachable adjacent cell around target with lowest action cost.
        /// 2) Closest reachable cell by zone search.
        /// </summary>
        public static Vector3Int? FindBestMoveCell(
            GridData gridData,
            GridPathfinding pathfinding,
            OccupancyMap occupancy,
            EntityData actor,
            EntityData target,
            int availableActions,
            List<Vector3Int> pathBuffer,
            Dictionary<Vector3Int, int> zoneBuffer)
        {
            if (gridData == null || pathfinding == null || occupancy == null) return null;
            if (actor == null || target == null) return null;
            if (pathBuffer == null || zoneBuffer == null) return null;

            availableActions = Mathf.Clamp(availableActions, 0, 3);
            if (availableActions <= 0) return null;

            var profile = new MovementProfile
            {
                moveType = MovementType.Walk,
                speedFeet = actor.Speed,
                creatureSizeCells = actor.SizeCells,
                ignoresDifficultTerrain = false
            };

            var actorPos = actor.GridPosition;
            var targetPos = target.GridPosition;

            Vector3Int bestAdjacentCell = default;
            int bestAdjacentActions = int.MaxValue;
            bool foundAdjacent = false;

            // Candidate cells around target (8 directions on same elevation).
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;

                    var candidate = new Vector3Int(targetPos.x + dx, targetPos.y, targetPos.z + dz);
                    if (candidate == actorPos)
                        return null; // already adjacent (caller should strike, not stride)

                    if (!gridData.HasCell(candidate)) continue;
                    if (!occupancy.CanOccupy(candidate, actor.Handle)) continue;

                    pathBuffer.Clear();
                    bool found = pathfinding.FindPathByActions(
                        gridData,
                        actorPos,
                        candidate,
                        profile,
                        actor.Handle,
                        occupancy,
                        availableActions,
                        pathBuffer,
                        out int actionsCost,
                        out int _);

                    if (!found) continue;
                    if (actionsCost < bestAdjacentActions)
                    {
                        bestAdjacentActions = actionsCost;
                        bestAdjacentCell = candidate;
                        foundAdjacent = true;
                    }
                }
            }

            if (foundAdjacent)
                return bestAdjacentCell;

            // Fallback: move to the closest reachable cell by remaining distance to target.
            zoneBuffer.Clear();
            pathfinding.GetMovementZoneByActions(
                gridData,
                actorPos,
                profile,
                availableActions,
                actor.Handle,
                occupancy,
                zoneBuffer);

            Vector3Int bestFallbackCell = actorPos;
            int bestFallbackDist = GridDistancePF2e.DistanceFeetXZ(actorPos, targetPos);

            foreach (var kvp in zoneBuffer)
            {
                int dist = GridDistancePF2e.DistanceFeetXZ(kvp.Key, targetPos);
                if (dist < bestFallbackDist)
                {
                    bestFallbackDist = dist;
                    bestFallbackCell = kvp.Key;
                }
            }

            return bestFallbackCell != actorPos ? bestFallbackCell : (Vector3Int?)null;
        }
    }
}
