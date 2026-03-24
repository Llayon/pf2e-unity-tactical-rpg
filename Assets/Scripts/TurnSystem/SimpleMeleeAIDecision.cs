using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using System;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Pure decision helpers for simple melee AI.
    /// No scene/state mutation and no MonoBehaviour lifecycle.
    /// </summary>
    public static class SimpleMeleeAIDecision
    {
        private const int TrapAvoidanceDistanceToleranceFeet = 5;

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

            Vector3Int bestAdjacentCell = default;
            int bestAdjacentActions = int.MaxValue;
            int bestAdjacentPressure = int.MaxValue;
            int bestAdjacentDistance = int.MaxValue;
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

                    int candidatePressure = GetTerrainPressure(terrainPressureEvaluator, candidate);
                    int candidateDistance = GridDistancePF2e.DistanceFeetXZ(candidate, targetPos);
                    if (!foundAdjacent || IsBetterAdjacentMoveCandidate(
                        actionsCost,
                        candidatePressure,
                        candidateDistance,
                        candidate,
                        bestAdjacentActions,
                        bestAdjacentPressure,
                        bestAdjacentDistance,
                        bestAdjacentCell))
                    {
                        bestAdjacentActions = actionsCost;
                        bestAdjacentPressure = candidatePressure;
                        bestAdjacentDistance = candidateDistance;
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

            Vector3Int bestFallbackCell = default;
            int bestFallbackDist = int.MaxValue;
            int bestFallbackPressure = int.MaxValue;
            int bestFallbackActions = int.MaxValue;
            bool foundFallback = false;

            foreach (var kvp in zoneBuffer)
            {
                if (kvp.Key == actorPos)
                    continue;

                int dist = GridDistancePF2e.DistanceFeetXZ(kvp.Key, targetPos);
                int pressure = GetTerrainPressure(terrainPressureEvaluator, kvp.Key);
                int actions = kvp.Value;

                if (!foundFallback || IsBetterFallbackMoveCandidate(
                    dist,
                    pressure,
                    actions,
                    kvp.Key,
                    bestFallbackDist,
                    bestFallbackPressure,
                    bestFallbackActions,
                    bestFallbackCell))
                {
                    bestFallbackDist = dist;
                    bestFallbackPressure = pressure;
                    bestFallbackActions = actions;
                    bestFallbackCell = kvp.Key;
                    foundFallback = true;
                }
            }

            return foundFallback ? bestFallbackCell : (Vector3Int?)null;
        }

        private static int GetTerrainPressure(Func<Vector3Int, int> terrainPressureEvaluator, Vector3Int cell)
        {
            return terrainPressureEvaluator != null
                ? Mathf.Max(0, terrainPressureEvaluator(cell))
                : 0;
        }

        private static bool IsBetterAdjacentMoveCandidate(
            int actions,
            int pressure,
            int distance,
            Vector3Int cell,
            int bestActions,
            int bestPressure,
            int bestDistance,
            Vector3Int bestCell)
        {
            if (actions < bestActions) return true;
            if (actions > bestActions) return false;
            if (pressure < bestPressure) return true;
            if (pressure > bestPressure) return false;
            if (distance < bestDistance) return true;
            if (distance > bestDistance) return false;
            return IsDeterministicallyEarlier(cell, bestCell);
        }

        private static bool IsBetterFallbackMoveCandidate(
            int distance,
            int pressure,
            int actions,
            Vector3Int cell,
            int bestDistance,
            int bestPressure,
            int bestActions,
            Vector3Int bestCell)
        {
            bool candidateMuchSafer = pressure + HazardousTerrainRules.DefaultHazardousTerrainPressure <= bestPressure
                && distance <= bestDistance + TrapAvoidanceDistanceToleranceFeet;
            if (candidateMuchSafer) return true;

            bool bestMuchSafer = bestPressure + HazardousTerrainRules.DefaultHazardousTerrainPressure <= pressure
                && bestDistance <= distance + TrapAvoidanceDistanceToleranceFeet;
            if (bestMuchSafer) return false;

            if (distance < bestDistance) return true;
            if (distance > bestDistance) return false;
            if (pressure < bestPressure) return true;
            if (pressure > bestPressure) return false;
            if (actions < bestActions) return true;
            if (actions > bestActions) return false;
            return IsDeterministicallyEarlier(cell, bestCell);
        }

        private static bool IsDeterministicallyEarlier(Vector3Int cell, Vector3Int bestCell)
        {
            if (cell.x != bestCell.x) return cell.x < bestCell.x;
            if (cell.y != bestCell.y) return cell.y < bestCell.y;
            return cell.z < bestCell.z;
        }
    }
}
