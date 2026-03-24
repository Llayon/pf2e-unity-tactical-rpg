using System;
using System.Collections.Generic;
using PF2e.Core;
using PF2e.Grid;
using UnityEngine;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Pure decision helpers for simple melee AI.
    /// No scene/state mutation and no MonoBehaviour lifecycle.
    /// </summary>
    public static class SimpleMeleeAIDecision
    {
        /// <summary>
        /// Select nearest alive player.
        /// Priority:
        /// 1) Same elevation as actor (distance -> HP -> handle id).
        /// 2) If none exist, fallback to any elevation with the same tie-break order.
        /// </summary>
        public static EntityHandle FindBestTarget(EntityData actor, IEnumerable<EntityData> allEntities)
        {
            if (actor == null || allEntities == null)
                return EntityHandle.None;

            EntityHandle best = FindBestTargetWithElevationFilter(actor, allEntities, sameElevationOnly: true);
            if (best.IsValid)
                return best;

            return FindBestTargetWithElevationFilter(actor, allEntities, sameElevationOnly: false);
        }

        private static EntityHandle FindBestTargetWithElevationFilter(
            EntityData actor,
            IEnumerable<EntityData> allEntities,
            bool sameElevationOnly)
        {
            EntityHandle best = EntityHandle.None;
            int bestDistFeet = int.MaxValue;
            int bestHp = int.MaxValue;
            int bestHandleId = int.MaxValue;

            foreach (var data in allEntities)
            {
                if (data == null) continue;
                if (!data.IsAlive) continue;
                if (data.Team != Team.Player) continue;
                if (sameElevationOnly && data.GridPosition.y != actor.GridPosition.y) continue;

                int distFeet = GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, data.GridPosition);
                int hp = data.CurrentHP;
                int handleId = data.Handle.Id;

                if (IsBetterTarget(distFeet, hp, handleId, bestDistFeet, bestHp, bestHandleId))
                {
                    bestDistFeet = distFeet;
                    bestHp = hp;
                    bestHandleId = handleId;
                    best = data.Handle;
                }
            }

            return best;
        }

        private static bool IsBetterTarget(
            int distFeet,
            int hp,
            int handleId,
            int bestDistFeet,
            int bestHp,
            int bestHandleId)
        {
            if (distFeet < bestDistFeet) return true;
            if (distFeet > bestDistFeet) return false;
            if (hp < bestHp) return true;
            if (hp > bestHp) return false;
            return handleId < bestHandleId;
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
            Dictionary<Vector3Int, int> zoneBuffer,
            Func<Vector3Int, int> terrainPressureEvaluator = null)
        {
            if (gridData == null || pathfinding == null || occupancy == null) return null;
            if (actor == null || target == null) return null;
            if (pathBuffer == null || zoneBuffer == null) return null;

            availableActions = Mathf.Clamp(availableActions, 0, 3);
            if (availableActions <= 0) return null;

            var profile = new MovementProfile
            {
                moveType = MovementType.Walk,
                speedFeet = actor.EffectiveSpeed,
                creatureSizeCells = actor.SizeCells,
                ignoresDifficultTerrain = false
            };

            var actorPos = actor.GridPosition;
            var targetPos = target.GridPosition;

            AIMovementCellScore bestAdjacentScore = default;
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

                    AIMovementCellScore candidateScore = new(
                        candidate,
                        actionsCost,
                        GridDistancePF2e.DistanceFeetXZ(candidate, targetPos),
                        GetTerrainPressure(terrainPressureEvaluator, candidate));

                    if (!foundAdjacent || AIMovementScoring.IsBetterAdjacentApproach(in candidateScore, in bestAdjacentScore))
                    {
                        bestAdjacentScore = candidateScore;
                        foundAdjacent = true;
                    }
                }
            }

            if (foundAdjacent)
                return bestAdjacentScore.cell;

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

            AIMovementCellScore bestFallbackScore = default;
            bool foundFallback = false;

            foreach (var kvp in zoneBuffer)
            {
                if (kvp.Key == actorPos)
                    continue;

                AIMovementCellScore candidateScore = new(
                    kvp.Key,
                    kvp.Value,
                    GridDistancePF2e.DistanceFeetXZ(kvp.Key, targetPos),
                    GetTerrainPressure(terrainPressureEvaluator, kvp.Key));

                if (!foundFallback || AIMovementScoring.IsBetterFallbackApproach(in candidateScore, in bestFallbackScore))
                {
                    bestFallbackScore = candidateScore;
                    foundFallback = true;
                }
            }

            return foundFallback ? bestFallbackScore.cell : (Vector3Int?)null;
        }

        private static int GetTerrainPressure(Func<Vector3Int, int> terrainPressureEvaluator, Vector3Int cell)
        {
            return terrainPressureEvaluator != null
                ? Mathf.Max(0, terrainPressureEvaluator(cell))
                : 0;
        }

    }
}
