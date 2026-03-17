using System.Collections.Generic;
using PF2e.Grid;
using UnityEngine;

namespace PF2e.Core
{
    public static class FleeingRules
    {
        public static bool TryBuildFleeZone(
            GridData grid,
            GridPathfinding pathfinding,
            OccupancyMap occupancy,
            EntityRegistry registry,
            EntityData actor,
            int availableActions,
            Dictionary<Vector3Int, int> outZone,
            out Vector3Int sourcePosition)
        {
            sourcePosition = default;

            if (outZone == null)
                return false;

            outZone.Clear();

            if (grid == null || pathfinding == null || occupancy == null || registry == null || actor == null)
                return false;

            availableActions = Mathf.Clamp(availableActions, 0, 3);
            if (availableActions <= 0 || !actor.HasCondition(ConditionType.Fleeing))
                return false;

            if (!TryResolveSourcePosition(actor, registry, out sourcePosition))
                return false;

            var profile = new MovementProfile
            {
                moveType = MovementType.Walk,
                speedFeet = actor.EffectiveSpeed,
                creatureSizeCells = actor.SizeCells,
                ignoresDifficultTerrain = false
            };

            pathfinding.GetMovementZoneByActions(
                grid,
                actor.GridPosition,
                profile,
                availableActions,
                actor.Handle,
                occupancy,
                outZone);

            return FilterToMaximumDistanceCells(actor.GridPosition, sourcePosition, outZone);
        }

        public static bool TryResolveSourcePosition(EntityData actor, EntityRegistry registry, out Vector3Int sourcePosition)
        {
            sourcePosition = default;

            if (actor == null || registry == null)
                return false;

            if (TryResolveStoredSource(actor, registry, out var source))
            {
                sourcePosition = source.GridPosition;
                return true;
            }

            EntityData best = null;
            int bestElevationBucket = int.MaxValue;
            int bestDistanceFeet = int.MaxValue;
            int bestHandleId = int.MaxValue;

            foreach (var candidate in registry.GetAll())
            {
                if (candidate == null || !candidate.IsAlive)
                    continue;
                if (candidate.Handle == actor.Handle)
                    continue;
                if (candidate.Team == actor.Team)
                    continue;

                int elevationBucket = candidate.GridPosition.y == actor.GridPosition.y ? 0 : 1;
                int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, candidate.GridPosition);
                int handleId = candidate.Handle.Id;

                if (best == null
                    || elevationBucket < bestElevationBucket
                    || (elevationBucket == bestElevationBucket && distanceFeet < bestDistanceFeet)
                    || (elevationBucket == bestElevationBucket && distanceFeet == bestDistanceFeet && handleId < bestHandleId))
                {
                    best = candidate;
                    bestElevationBucket = elevationBucket;
                    bestDistanceFeet = distanceFeet;
                    bestHandleId = handleId;
                }
            }

            if (best == null)
                return false;

            sourcePosition = best.GridPosition;
            return true;
        }

        public static bool FilterToMaximumDistanceCells(
            Vector3Int actorPosition,
            Vector3Int sourcePosition,
            Dictionary<Vector3Int, int> zoneByActions)
        {
            if (zoneByActions == null || zoneByActions.Count <= 0)
                return false;

            int currentDistanceFeet = GridDistancePF2e.DistanceFeetXZ(actorPosition, sourcePosition);
            int maximumDistanceFeet = currentDistanceFeet;

            foreach (var kvp in zoneByActions)
            {
                if (kvp.Key == actorPosition)
                    continue;

                int candidateDistanceFeet = GridDistancePF2e.DistanceFeetXZ(kvp.Key, sourcePosition);
                if (candidateDistanceFeet > maximumDistanceFeet)
                    maximumDistanceFeet = candidateDistanceFeet;
            }

            if (maximumDistanceFeet <= currentDistanceFeet)
            {
                zoneByActions.Clear();
                return false;
            }

            var removalBuffer = new List<Vector3Int>();
            foreach (var kvp in zoneByActions)
            {
                int candidateDistanceFeet = GridDistancePF2e.DistanceFeetXZ(kvp.Key, sourcePosition);
                if (candidateDistanceFeet != maximumDistanceFeet)
                    removalBuffer.Add(kvp.Key);
            }

            for (int i = 0; i < removalBuffer.Count; i++)
                zoneByActions.Remove(removalBuffer[i]);

            return zoneByActions.Count > 0;
        }

        public static bool TrySelectDeterministicCell(
            IReadOnlyDictionary<Vector3Int, int> zoneByActions,
            out Vector3Int bestCell)
        {
            bestCell = default;

            if (zoneByActions == null || zoneByActions.Count <= 0)
                return false;

            bool found = false;
            int bestActions = int.MinValue;

            foreach (var kvp in zoneByActions)
            {
                if (!found
                    || kvp.Value > bestActions
                    || (kvp.Value == bestActions && CompareCellOrder(kvp.Key, bestCell) < 0))
                {
                    bestCell = kvp.Key;
                    bestActions = kvp.Value;
                    found = true;
                }
            }

            return found;
        }

        private static bool TryResolveStoredSource(EntityData actor, EntityRegistry registry, out EntityData source)
        {
            source = null;

            if (actor == null || registry == null || !actor.FleeingSourceHandle.IsValid)
                return false;

            source = registry.Get(actor.FleeingSourceHandle);
            return source != null
                && source.IsAlive
                && source.Handle != actor.Handle
                && source.Team != actor.Team;
        }

        private static int CompareCellOrder(Vector3Int a, Vector3Int b)
        {
            int x = a.x.CompareTo(b.x);
            if (x != 0)
                return x;

            int z = a.z.CompareTo(b.z);
            if (z != 0)
                return z;

            return a.y.CompareTo(b.y);
        }
    }
}
