using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Default MVP AI policy that mirrors existing simple melee behavior.
    /// </summary>
    public sealed class SimpleMeleeDecisionPolicy : IAIDecisionPolicy
    {
        private readonly EntityManager entityManager;
        private readonly GridManager gridManager;

        // Reused to avoid per-decision allocations.
        private readonly List<Vector3Int> pathBuffer = new(32);
        private readonly Dictionary<Vector3Int, int> zoneBuffer = new();
        private readonly List<NeighborInfo> neighborBuffer = new(8);

        public SimpleMeleeDecisionPolicy(EntityManager entityManager, GridManager gridManager)
        {
            this.entityManager = entityManager;
            this.gridManager = gridManager;
        }

        public EntityHandle SelectTarget(EntityData actor)
        {
            if (actor == null || entityManager == null || entityManager.Registry == null)
                return EntityHandle.None;

            return SimpleMeleeAIDecision.FindBestTarget(actor, entityManager.Registry.GetAll());
        }

        public bool IsInMeleeRange(EntityData actor, EntityData target)
        {
            return SimpleMeleeAIDecision.IsInMeleeRange(actor, target);
        }

        public Vector3Int? SelectStepCell(EntityData actor, EntityData target, int availableActions)
        {
            if (actor == null || target == null)
                return null;
            if (availableActions <= 0)
                return null;
            if (gridManager == null || gridManager.Data == null)
                return null;
            if (entityManager == null || entityManager.Registry == null || entityManager.Occupancy == null)
                return null;
            if (!actor.IsAlive || actor.EffectiveSpeed <= 0)
                return null;
            if (actor.HasCondition(ConditionType.Prone))
                return null;

            int currentThreatCount = CountHostileReactiveStrikeThreats(actor, actor.GridPosition);
            if (currentThreatCount <= 0)
                return null;

            var actorPos = actor.GridPosition;
            var targetPos = target.GridPosition;
            int currentDistance = GridDistancePF2e.DistanceFeetXZ(actorPos, targetPos);

            bool foundSafer = false;
            Vector3Int bestSaferCell = default;
            int bestSaferThreatCount = int.MaxValue;
            bool bestSaferInMelee = false;
            int bestSaferDistance = int.MaxValue;

            bool foundMelee = false;
            Vector3Int bestMeleeCell = default;
            int bestMeleeThreatCount = int.MaxValue;
            int bestMeleeDistance = int.MaxValue;

            gridManager.Data.GetNeighbors(actorPos, MovementType.Walk, neighborBuffer);
            for (int i = 0; i < neighborBuffer.Count; i++)
            {
                var neighbor = neighborBuffer[i];
                var candidate = neighbor.pos;

                if (!IsValidStepDestination(actor, in neighbor))
                    continue;

                int candidateDistance = GridDistancePF2e.DistanceFeetXZ(candidate, targetPos);
                bool candidateInMelee = candidate.y == targetPos.y
                    && candidateDistance <= actor.EquippedWeapon.ReachFeet;

                int candidateThreatCount = CountHostileReactiveStrikeThreats(actor, candidate);

                if (candidateThreatCount < currentThreatCount && candidateDistance <= currentDistance)
                {
                    if (!foundSafer
                        || IsBetterSaferStepCandidate(
                            candidateThreatCount,
                            candidateInMelee,
                            candidateDistance,
                            candidate,
                            bestSaferThreatCount,
                            bestSaferInMelee,
                            bestSaferDistance,
                            bestSaferCell))
                    {
                        foundSafer = true;
                        bestSaferCell = candidate;
                        bestSaferThreatCount = candidateThreatCount;
                        bestSaferInMelee = candidateInMelee;
                        bestSaferDistance = candidateDistance;
                    }
                }

                if (candidateInMelee)
                {
                    if (!foundMelee
                        || IsBetterMeleeStepCandidate(
                            candidateThreatCount,
                            candidateDistance,
                            candidate,
                            bestMeleeThreatCount,
                            bestMeleeDistance,
                            bestMeleeCell))
                    {
                        foundMelee = true;
                        bestMeleeCell = candidate;
                        bestMeleeThreatCount = candidateThreatCount;
                        bestMeleeDistance = candidateDistance;
                    }
                }
            }

            if (foundSafer)
                return bestSaferCell;

            return foundMelee ? bestMeleeCell : (Vector3Int?)null;
        }

        public Vector3Int? SelectStrideCell(EntityData actor, EntityData target, int availableActions)
        {
            if (actor == null || target == null)
                return null;
            if (gridManager == null || gridManager.Data == null)
                return null;
            if (entityManager == null || entityManager.Pathfinding == null || entityManager.Occupancy == null)
                return null;

            return SimpleMeleeAIDecision.FindBestMoveCell(
                gridManager.Data,
                entityManager.Pathfinding,
                entityManager.Occupancy,
                actor,
                target,
                availableActions,
                pathBuffer,
                zoneBuffer);
        }

        private bool IsValidStepDestination(EntityData actor, in NeighborInfo neighbor)
        {
            if (actor == null)
                return false;
            if (gridManager == null || gridManager.Data == null || entityManager == null || entityManager.Occupancy == null)
                return false;
            if (!gridManager.Data.TryGetCell(neighbor.pos, out var targetCellData))
                return false;
            if (!gridManager.Data.IsCellPassable(neighbor.pos, MovementType.Walk))
                return false;

            var profile = new MovementProfile
            {
                moveType = MovementType.Walk,
                speedFeet = actor.EffectiveSpeed,
                creatureSizeCells = actor.SizeCells,
                ignoresDifficultTerrain = false
            };

            int stepCost = MovementCostEvaluator.GetStepCost(
                targetCellData,
                neighbor,
                diagonalParity: false,
                profile);

            if (stepCost > GameConstants.CardinalCostFeet)
                return false;

            return entityManager.Occupancy.CanOccupyFootprint(neighbor.pos, actor.SizeCells, actor.Handle);
        }

        private int CountHostileReactiveStrikeThreats(EntityData actor, Vector3Int cell)
        {
            if (actor == null || entityManager == null || entityManager.Registry == null)
                return 0;

            int count = 0;
            foreach (var other in entityManager.Registry.GetAll())
            {
                if (other == null || !other.IsAlive)
                    continue;
                if (other.Team == actor.Team || other.Team == Team.Neutral)
                    continue;
                if (!other.HasReactiveStrike || !other.ReactionAvailable)
                    continue;
                if (other.EquippedWeapon.IsRanged)
                    continue;

                int distanceFeet = GridDistancePF2e.DistanceFeetXZ(other.GridPosition, cell);
                if (distanceFeet <= other.EquippedWeapon.ReachFeet)
                    count++;
            }

            return count;
        }

        private static bool IsBetterSaferStepCandidate(
            int threatCount,
            bool inMelee,
            int distance,
            Vector3Int cell,
            int bestThreatCount,
            bool bestInMelee,
            int bestDistance,
            Vector3Int bestCell)
        {
            if (threatCount < bestThreatCount) return true;
            if (threatCount > bestThreatCount) return false;
            if (inMelee && !bestInMelee) return true;
            if (!inMelee && bestInMelee) return false;
            if (distance < bestDistance) return true;
            if (distance > bestDistance) return false;
            return IsDeterministicallyEarlier(cell, bestCell);
        }

        private static bool IsBetterMeleeStepCandidate(
            int threatCount,
            int distance,
            Vector3Int cell,
            int bestThreatCount,
            int bestDistance,
            Vector3Int bestCell)
        {
            if (threatCount < bestThreatCount) return true;
            if (threatCount > bestThreatCount) return false;
            if (distance < bestDistance) return true;
            if (distance > bestDistance) return false;
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
