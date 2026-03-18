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
        private readonly List<Vector3Int> spellAreaCellBuffer = new(8);
        private readonly List<Vector3Int> controlDestinationBuffer = new(8);
        private readonly Queue<Vector3Int> controlCellQueue = new();
        private readonly Queue<int> controlDepthQueue = new();
        private readonly HashSet<Vector3Int> controlVisitedCells = new();

        private static readonly Vector3Int[] BurningHandsAimOffsets =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(1, 0, 1),
            new Vector3Int(0, 0, 1),
            new Vector3Int(-1, 0, 1),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(-1, 0, -1),
            new Vector3Int(0, 0, -1),
            new Vector3Int(1, 0, -1)
        };

        private const int DifficultTerrainPressure = 10;
        private const int GreaterDifficultTerrainPressure = 20;
        private const int HazardousTerrainPressure = 100;

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

        public bool TrySelectSpellDecision(EntityData actor, EntityData target, int availableActions, out AISpellDecision decision)
        {
            decision = default;

            if (actor == null || target == null)
                return false;
            if (availableActions <= 0)
                return false;
            if (entityManager == null || entityManager.Registry == null || entityManager.Occupancy == null)
                return false;
            if (gridManager == null || gridManager.Data == null)
                return false;
            if (!actor.IsAlive || !target.IsAlive)
                return false;

            if (TryBuildSupportSpellDecision(actor, availableActions, out decision))
                return true;

            bool inMelee = IsInMeleeRange(actor, target);
            if (ShouldPrioritizeMeleeFollowThrough(actor, target))
                return false;

            if (availableActions >= SpellCatalog.Get(SpellId.Fear).minActionCost
                && actor.KnowsFear
                && IsValidOffensiveSpellTarget(actor, target, SpellId.Fear)
                && !target.HasCondition(ConditionType.Frightened)
                && !target.HasCondition(ConditionType.Fleeing))
            {
                decision = AISpellDecision.SingleTarget(
                    SpellId.Fear,
                    SpellCatalog.Get(SpellId.Fear).minActionCost,
                    target.Handle);
                return true;
            }

            if (availableActions >= SpellCatalog.Get(SpellId.BurningHands).minActionCost
                && actor.KnowsBurningHands
                && TryBuildBurningHandsDecision(actor, target, out decision))
            {
                return true;
            }

            if (inMelee)
                return false;

            if (availableActions >= SpellCatalog.Get(SpellId.ElectricArc).minActionCost
                && actor.KnowsElectricArc
                && TryBuildElectricArcDecision(actor, target, out decision))
            {
                return true;
            }

            if (availableActions >= SpellCatalog.Get(SpellId.Snowball).minActionCost
                && actor.KnowsSnowball
                && IsValidOffensiveSpellTarget(actor, target, SpellId.Snowball))
            {
                decision = AISpellDecision.SingleTarget(
                    SpellId.Snowball,
                    SpellCatalog.Get(SpellId.Snowball).minActionCost,
                    target.Handle);
                return true;
            }

            if (actor.KnowsForceBarrage
                && IsValidOffensiveSpellTarget(actor, target, SpellId.ForceBarrage))
            {
                decision = AISpellDecision.MultiShard(
                    SpellId.ForceBarrage,
                    Mathf.Clamp(availableActions, 1, 3),
                    target.Handle);
                return true;
            }

            return false;
        }

        public bool TrySelectDefensiveDecision(EntityData actor, EntityData target, int availableActions, out AIDefensiveDecision decision)
        {
            decision = default;

            if (actor == null || target == null)
                return false;
            if (availableActions != 1)
                return false;
            if (!actor.IsAlive || !target.IsAlive)
                return false;
            if (!actor.Handle.IsValid || !target.Handle.IsValid)
                return false;
            if (actor.GridPosition.y != target.GridPosition.y)
                return false;
            if (actor.HasRaisedPhysicalShield || actor.StandardShieldRaised || actor.GlassShieldRaised)
                return false;
            if (ShouldPrioritizeMeleeFollowThrough(actor, target))
                return false;

            int targetDistance = GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, target.GridPosition);
            bool closeThreat = targetDistance <= Mathf.Max(10, actor.EquippedWeapon.ReachFeet);
            bool wounded = actor.CurrentHP * 2 <= actor.MaxHP;
            if (!closeThreat && !wounded)
                return false;

            if (actor.EquippedShield.IsEquipped && !actor.EquippedShield.IsBroken && !actor.EquippedShield.isRaised)
            {
                decision = AIDefensiveDecision.RaisePhysicalShield();
                return true;
            }

            if (actor.CanCastStandardShield)
            {
                decision = AIDefensiveDecision.CastShieldSpell(RaiseShieldSpellMode.Standard);
                return true;
            }

            if (actor.CanCastGlassShield)
            {
                decision = AIDefensiveDecision.CastShieldSpell(RaiseShieldSpellMode.Glass);
                return true;
            }

            return false;
        }

        public bool TrySelectSkillDecision(EntityData actor, EntityData target, int availableActions, out AISkillDecision decision)
        {
            decision = default;

            if (actor == null || target == null)
                return false;
            if (availableActions < 2)
                return false;
            if (!actor.IsAlive || !target.IsAlive)
                return false;
            if (!actor.Handle.IsValid || !target.Handle.IsValid)
                return false;
            if (target.Team == actor.Team || target.Team == Team.Neutral)
                return false;
            if (ShouldPrioritizeMeleeFollowThrough(actor, target))
                return false;

            if (TryBuildControlSkillDecision(actor, target, availableActions, out decision))
                return true;

            if (target.HasCondition(ConditionType.Frightened) || target.HasCondition(ConditionType.Fleeing))
                return false;
            if (actor.GetSkillModifier(SkillType.Intimidation) < 0)
                return false;

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, target.GridPosition);
            if (distanceFeet > 30)
                return false;

            decision = AISkillDecision.Demoralize(target.Handle);
            return true;
        }

        private bool TryBuildControlSkillDecision(EntityData actor, EntityData target, int availableActions, out AISkillDecision decision)
        {
            decision = default;

            if (availableActions < 2)
                return false;
            if (actor == null || target == null)
                return false;
            if (actor.MAPCount > 0)
                return false;
            if (actor.GetSkillModifier(SkillType.Athletics) < 0)
                return false;

            bool targetProne = target.HasCondition(ConditionType.Prone);
            bool targetGrabbed = target.HasCondition(ConditionType.Grabbed) || target.HasCondition(ConditionType.Restrained);
            bool canTrip = IsValidTripControlTarget(actor, target);
            bool canGrapple = IsValidGrappleControlTarget(actor, target);

            if (targetProne && canGrapple && !targetGrabbed)
            {
                decision = AISkillDecision.Grapple(target.Handle);
                return true;
            }

            if (canTrip && !targetProne)
            {
                decision = AISkillDecision.Trip(target.Handle);
                return true;
            }

            if (canGrapple && !targetGrabbed)
            {
                decision = AISkillDecision.Grapple(target.Handle);
                return true;
            }

            if (TryBuildRepositionSkillDecision(actor, target, out decision))
                return true;

            if (TryBuildShoveSkillDecision(actor, target, out decision))
                return true;

            return false;
        }

        private bool TryBuildSupportSpellDecision(EntityData actor, int availableActions, out AISpellDecision decision)
        {
            decision = default;

            bool hasHeal = TryBuildHealSupportDecision(actor, availableActions, out var healDecision, out int healUrgency);
            bool hasHarm = TryBuildHarmSupportDecision(actor, availableActions, out var harmDecision, out int harmUrgency);

            if (!hasHeal && !hasHarm)
                return false;

            if (hasHeal && (!hasHarm || healUrgency >= harmUrgency))
            {
                decision = healDecision;
                return true;
            }

            decision = harmDecision;
            return true;
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
            int bestSaferTerrainPressure = int.MaxValue;
            bool bestSaferInMelee = false;
            int bestSaferDistance = int.MaxValue;

            bool foundMelee = false;
            Vector3Int bestMeleeCell = default;
            int bestMeleeThreatCount = int.MaxValue;
            int bestMeleeTerrainPressure = int.MaxValue;
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
                int candidateTerrainPressure = GetTerrainPressureScore(candidate);

                if (candidateThreatCount < currentThreatCount && candidateDistance <= currentDistance)
                {
                    if (!foundSafer
                        || IsBetterSaferStepCandidate(
                            candidateThreatCount,
                            candidateTerrainPressure,
                            candidateInMelee,
                            candidateDistance,
                            candidate,
                            bestSaferThreatCount,
                            bestSaferTerrainPressure,
                            bestSaferInMelee,
                            bestSaferDistance,
                            bestSaferCell))
                    {
                        foundSafer = true;
                        bestSaferCell = candidate;
                        bestSaferThreatCount = candidateThreatCount;
                        bestSaferTerrainPressure = candidateTerrainPressure;
                        bestSaferInMelee = candidateInMelee;
                        bestSaferDistance = candidateDistance;
                    }
                }

                if (candidateInMelee)
                {
                    if (!foundMelee
                        || IsBetterMeleeStepCandidate(
                            candidateThreatCount,
                            candidateTerrainPressure,
                            candidateDistance,
                            candidate,
                            bestMeleeThreatCount,
                            bestMeleeTerrainPressure,
                            bestMeleeDistance,
                            bestMeleeCell))
                    {
                        foundMelee = true;
                        bestMeleeCell = candidate;
                        bestMeleeThreatCount = candidateThreatCount;
                        bestMeleeTerrainPressure = candidateTerrainPressure;
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

        private bool TryBuildElectricArcDecision(EntityData actor, EntityData preferredTarget, out AISpellDecision decision)
        {
            decision = default;

            if (actor == null || preferredTarget == null || entityManager == null || entityManager.Registry == null)
                return false;

            if (!IsValidOffensiveSpellTarget(actor, preferredTarget, SpellId.ElectricArc))
                return false;

            EntityData bestSecondary = null;
            foreach (var candidate in entityManager.Registry.GetAll())
            {
                if (candidate == null || candidate.Handle == preferredTarget.Handle)
                    continue;
                if (!IsValidOffensiveSpellTarget(actor, candidate, SpellId.ElectricArc))
                    continue;

                if (bestSecondary == null || IsBetterSecondaryArcTarget(actor, candidate, bestSecondary))
                    bestSecondary = candidate;
            }

            if (bestSecondary == null)
                return false;

            decision = AISpellDecision.ChainTwo(
                SpellId.ElectricArc,
                SpellCatalog.Get(SpellId.ElectricArc).minActionCost,
                preferredTarget.Handle,
                bestSecondary.Handle);
            return true;
        }

        private bool TryBuildHealSupportDecision(EntityData actor, int availableActions, out AISpellDecision decision, out int urgency)
        {
            decision = default;
            urgency = int.MinValue;

            if (actor == null || !actor.KnowsHeal || entityManager == null || entityManager.Registry == null)
                return false;

            if (availableActions >= 2
                && TryFindBestSupportTarget(actor, SpellId.Heal, actionCount: 2, out var rangedTarget, out int rangedUrgency))
            {
                decision = AISpellDecision.SingleTarget(SpellId.Heal, 2, rangedTarget.Handle);
                urgency = rangedUrgency;
                return true;
            }

            if (availableActions >= 1
                && TryFindBestSupportTarget(actor, SpellId.Heal, actionCount: 1, out var touchTarget, out int touchUrgency))
            {
                decision = AISpellDecision.SingleTarget(SpellId.Heal, 1, touchTarget.Handle);
                urgency = touchUrgency;
                return true;
            }

            return false;
        }

        private bool TryBuildHarmSupportDecision(EntityData actor, int availableActions, out AISpellDecision decision, out int urgency)
        {
            decision = default;
            urgency = int.MinValue;

            if (actor == null || !actor.KnowsHarm || entityManager == null || entityManager.Registry == null)
                return false;

            if (availableActions >= 2
                && TryFindBestSupportTarget(actor, SpellId.Harm, actionCount: 2, out var rangedTarget, out int rangedUrgency))
            {
                decision = AISpellDecision.SingleTarget(SpellId.Harm, 2, rangedTarget.Handle);
                urgency = rangedUrgency;
                return true;
            }

            if (availableActions >= 1
                && TryFindBestSupportTarget(actor, SpellId.Harm, actionCount: 1, out var touchTarget, out int touchUrgency))
            {
                decision = AISpellDecision.SingleTarget(SpellId.Harm, 1, touchTarget.Handle);
                urgency = touchUrgency;
                return true;
            }

            return false;
        }

        private bool TryFindBestSupportTarget(
            EntityData actor,
            SpellId spellId,
            int actionCount,
            out EntityData bestTarget,
            out int urgency)
        {
            bestTarget = null;
            urgency = int.MinValue;

            if (actor == null || entityManager == null || entityManager.Registry == null)
                return false;

            foreach (var candidate in entityManager.Registry.GetAll())
            {
                if (!IsValidSupportSpellTarget(actor, candidate, spellId, actionCount))
                    continue;
                if (!IsUrgentSupportTarget(candidate))
                    continue;

                int candidateUrgency = ComputeSupportUrgency(actor, candidate);
                if (bestTarget == null || candidateUrgency > urgency || IsBetterSupportCandidate(actor, candidate, bestTarget, candidateUrgency, urgency))
                {
                    bestTarget = candidate;
                    urgency = candidateUrgency;
                }
            }

            return bestTarget != null;
        }

        private bool TryBuildBurningHandsDecision(EntityData actor, EntityData preferredTarget, out AISpellDecision decision)
        {
            decision = default;

            if (actor == null || preferredTarget == null)
                return false;
            if (gridManager == null || gridManager.Data == null || entityManager == null || entityManager.Registry == null)
                return false;
            if (!preferredTarget.IsAlive || preferredTarget.Team == actor.Team || preferredTarget.Team == Team.Neutral)
                return false;
            if (actor.GridPosition.y != preferredTarget.GridPosition.y)
                return false;

            int bestHostileCount = 0;
            int bestTotalHostileHp = int.MaxValue;
            int bestDirectionIndex = int.MaxValue;
            Vector3Int bestAimCell = default;

            for (int i = 0; i < BurningHandsAimOffsets.Length; i++)
            {
                Vector3Int aimCell = actor.GridPosition + BurningHandsAimOffsets[i];
                if (!BurningHandsConeResolver.TryResolve(actor.GridPosition, aimCell, spellAreaCellBuffer, out int directionIndex))
                    continue;

                int hostileCount = 0;
                int allyCount = 0;
                int totalHostileHp = 0;
                bool hitsPreferred = false;

                foreach (var candidate in entityManager.Registry.GetAll())
                {
                    if (candidate == null || !candidate.IsAlive)
                        continue;
                    if (!spellAreaCellBuffer.Contains(candidate.GridPosition))
                        continue;

                    if (candidate.Team == actor.Team)
                    {
                        allyCount++;
                        continue;
                    }

                    if (candidate.Team == Team.Neutral)
                        continue;

                    hostileCount++;
                    totalHostileHp += candidate.CurrentHP;
                    if (candidate.Handle == preferredTarget.Handle)
                        hitsPreferred = true;
                }

                if (!hitsPreferred || allyCount > 0 || hostileCount < 2)
                    continue;

                if (hostileCount > bestHostileCount
                    || (hostileCount == bestHostileCount && totalHostileHp < bestTotalHostileHp)
                    || (hostileCount == bestHostileCount && totalHostileHp == bestTotalHostileHp && directionIndex < bestDirectionIndex))
                {
                    bestHostileCount = hostileCount;
                    bestTotalHostileHp = totalHostileHp;
                    bestDirectionIndex = directionIndex;
                    bestAimCell = aimCell;
                }
            }

            if (bestHostileCount < 2)
                return false;

            decision = AISpellDecision.AreaAimCell(
                SpellId.BurningHands,
                SpellCatalog.Get(SpellId.BurningHands).minActionCost,
                preferredTarget.Handle,
                bestAimCell);
            return true;
        }

        private bool ShouldPrioritizeMeleeFollowThrough(EntityData actor, EntityData target)
        {
            if (actor == null || target == null)
                return false;
            if (!actor.IsAlive || !target.IsAlive)
                return false;
            if (actor.EquippedWeapon.IsRanged)
                return false;
            if (actor.MAPCount <= 0)
                return false;
            if (!IsInMeleeRange(actor, target))
                return false;

            return target.HasCondition(ConditionType.Prone)
                || target.HasCondition(ConditionType.Grabbed)
                || target.HasCondition(ConditionType.Restrained);
        }

        private bool TryBuildShoveSkillDecision(EntityData actor, EntityData target, out AISkillDecision decision)
        {
            decision = default;

            if (!IsValidShoveControlTarget(actor, target))
                return false;
            if (target.HasCondition(ConditionType.Prone)
                || target.HasCondition(ConditionType.Grabbed)
                || target.HasCondition(ConditionType.Restrained))
            {
                return false;
            }
            if (!TryGetShoveSuccessDestination(actor, target, out var pushedCell))
                return false;

            int currentDistance = GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, target.GridPosition);
            int pushedDistance = GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, pushedCell);
            int terrainPressure = GetTerrainPressureScore(pushedCell);
            bool keepsTargetInReach = pushedDistance > currentDistance
                && pushedDistance <= actor.EquippedWeapon.ReachFeet;

            if (!keepsTargetInReach && terrainPressure < HazardousTerrainPressure)
                return false;

            decision = AISkillDecision.Shove(target.Handle);
            return true;
        }

        private bool TryBuildRepositionSkillDecision(EntityData actor, EntityData target, out AISkillDecision decision)
        {
            decision = default;

            if (!IsValidRepositionControlTarget(actor, target))
                return false;
            if (!TryGetValidRepositionDestinations(actor, target, maxMoveFeet: 5, controlDestinationBuffer))
                return false;

            int currentThreatCount = CountMeleeThreatsToCell(actor.Team, target.GridPosition);
            int currentTerrainPressure = GetTerrainPressureScore(target.GridPosition);
            int bestThreatCount = currentThreatCount;
            int bestTerrainPressure = currentTerrainPressure;
            int bestSupportCount = int.MaxValue;
            Vector3Int bestCell = default;
            bool found = false;

            for (int i = 0; i < controlDestinationBuffer.Count; i++)
            {
                Vector3Int candidateCell = controlDestinationBuffer[i];
                int candidateThreatCount = CountMeleeThreatsToCell(actor.Team, candidateCell);
                int candidateTerrainPressure = GetTerrainPressureScore(candidateCell);
                int candidateSupportCount = CountMeleeThreatsToCell(target.Team, candidateCell);

                if (!found
                    || candidateThreatCount > bestThreatCount
                    || (candidateThreatCount == bestThreatCount && candidateTerrainPressure > bestTerrainPressure)
                    || (candidateThreatCount == bestThreatCount
                        && candidateTerrainPressure == bestTerrainPressure
                        && candidateSupportCount < bestSupportCount)
                    || (candidateThreatCount == bestThreatCount
                        && candidateTerrainPressure == bestTerrainPressure
                        && candidateSupportCount == bestSupportCount
                        && CompareCells(candidateCell, bestCell) < 0))
                {
                    found = true;
                    bestThreatCount = candidateThreatCount;
                    bestTerrainPressure = candidateTerrainPressure;
                    bestSupportCount = candidateSupportCount;
                    bestCell = candidateCell;
                }
            }

            if (!found || (bestThreatCount <= currentThreatCount && bestTerrainPressure <= currentTerrainPressure))
                return false;

            decision = AISkillDecision.Reposition(target.Handle, bestCell);
            return true;
        }

        private static bool IsValidTripControlTarget(EntityData actor, EntityData target)
        {
            return IsValidAthleticsControlTarget(actor, target, WeaponTraitFlags.Trip);
        }

        private static bool IsValidGrappleControlTarget(EntityData actor, EntityData target)
        {
            return IsValidAthleticsControlTarget(actor, target, WeaponTraitFlags.Grapple);
        }

        private static bool IsValidShoveControlTarget(EntityData actor, EntityData target)
        {
            return IsValidAthleticsControlTarget(actor, target, WeaponTraitFlags.Shove);
        }

        private static bool IsValidRepositionControlTarget(EntityData actor, EntityData target)
        {
            return IsValidAthleticsControlTarget(actor, target, WeaponTraitFlags.Reposition);
        }

        private static bool IsValidAthleticsControlTarget(EntityData actor, EntityData target, WeaponTraitFlags requiredTrait)
        {
            if (actor == null || target == null)
                return false;
            if (!actor.IsAlive || !target.IsAlive)
                return false;
            if (!actor.Handle.IsValid || !target.Handle.IsValid)
                return false;
            if (actor.Handle == target.Handle)
                return false;
            if (target.Team == actor.Team || target.Team == Team.Neutral)
                return false;
            if (actor.EquippedWeapon.IsRanged)
                return false;
            if ((actor.EquippedWeapon.Traits & requiredTrait) == 0)
                return false;
            if (actor.GridPosition.y != target.GridPosition.y)
                return false;

            int sizeDelta = (int)target.Size - (int)actor.Size;
            if (sizeDelta > 1)
                return false;

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, target.GridPosition);
            return distanceFeet <= actor.EquippedWeapon.ReachFeet;
        }

        private bool TryGetShoveSuccessDestination(EntityData actor, EntityData target, out Vector3Int destinationCell)
        {
            destinationCell = default;

            if (actor == null || target == null)
                return false;
            if (gridManager == null || gridManager.Data == null || entityManager == null || entityManager.Occupancy == null)
                return false;

            Vector3Int step = GetNormalizedPushDirection(actor.GridPosition, target.GridPosition);
            if (step.x == 0 && step.z == 0)
                return false;

            Vector3Int candidate = target.GridPosition + step;
            if (!gridManager.Data.IsCellPassable(candidate, MovementType.Walk))
                return false;
            if (!entityManager.Occupancy.CanOccupyFootprint(candidate, target.SizeCells, target.Handle))
                return false;

            destinationCell = candidate;
            return true;
        }

        private bool TryGetValidRepositionDestinations(EntityData actor, EntityData target, int maxMoveFeet, List<Vector3Int> outCells)
        {
            outCells.Clear();

            if (actor == null || target == null)
                return false;
            if (entityManager == null || entityManager.Occupancy == null || gridManager == null || gridManager.Data == null)
                return false;
            if (maxMoveFeet <= 0)
                return false;

            int maxSteps = Mathf.Max(0, maxMoveFeet / 5);
            if (maxSteps <= 0)
                return false;

            int reachFeet = actor.EquippedWeapon.ReachFeet > 0 ? actor.EquippedWeapon.ReachFeet : 5;
            Vector3Int start = target.GridPosition;
            int fixedElevation = start.y;

            controlVisitedCells.Clear();
            controlCellQueue.Clear();
            controlDepthQueue.Clear();

            controlVisitedCells.Add(start);
            controlCellQueue.Enqueue(start);
            controlDepthQueue.Enqueue(0);

            while (controlCellQueue.Count > 0)
            {
                Vector3Int current = controlCellQueue.Dequeue();
                int depth = controlDepthQueue.Dequeue();

                if (depth >= maxSteps)
                    continue;

                gridManager.Data.GetNeighbors(current, MovementType.Walk, neighborBuffer);
                for (int i = 0; i < neighborBuffer.Count; i++)
                {
                    Vector3Int next = neighborBuffer[i].pos;
                    if (next.y != fixedElevation)
                        continue;
                    if (!controlVisitedCells.Add(next))
                        continue;
                    if (!gridManager.Data.IsCellPassable(next, MovementType.Walk))
                        continue;
                    if (!entityManager.Occupancy.CanOccupyFootprint(next, target.SizeCells, target.Handle))
                        continue;

                    int distanceToActorFeet = GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, next);
                    if (distanceToActorFeet > reachFeet)
                        continue;

                    controlCellQueue.Enqueue(next);
                    controlDepthQueue.Enqueue(depth + 1);

                    if (next != start)
                        outCells.Add(next);
                }
            }

            outCells.Sort(CompareCells);
            return outCells.Count > 0;
        }

        private int CountMeleeThreatsToCell(Team threateningTeam, Vector3Int targetCell)
        {
            if (entityManager == null || entityManager.Registry == null)
                return 0;

            int threatCount = 0;
            foreach (var candidate in entityManager.Registry.GetAll())
            {
                if (candidate == null || !candidate.IsAlive)
                    continue;
                if (candidate.Team != threateningTeam)
                    continue;
                if (!candidate.Handle.IsValid)
                    continue;
                if (candidate.EquippedWeapon.IsRanged)
                    continue;
                if (candidate.GridPosition.y != targetCell.y)
                    continue;

                int reachFeet = candidate.EquippedWeapon.ReachFeet > 0 ? candidate.EquippedWeapon.ReachFeet : 5;
                int distanceFeet = GridDistancePF2e.DistanceFeetXZ(candidate.GridPosition, targetCell);
                if (distanceFeet <= reachFeet)
                    threatCount++;
            }

            return threatCount;
        }

        private int GetTerrainPressureScore(Vector3Int cell)
        {
            if (gridManager == null || gridManager.Data == null)
                return 0;
            if (!gridManager.Data.TryGetCell(cell, out var cellData))
                return 0;

            return cellData.terrain switch
            {
                CellTerrain.Hazardous => HazardousTerrainPressure,
                CellTerrain.GreaterDifficult => GreaterDifficultTerrainPressure,
                CellTerrain.Difficult => DifficultTerrainPressure,
                _ => 0
            };
        }

        private static Vector3Int GetNormalizedPushDirection(Vector3Int actorCell, Vector3Int targetCell)
        {
            int dx = targetCell.x - actorCell.x;
            int dz = targetCell.z - actorCell.z;

            int dirX = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int dirZ = dz == 0 ? 0 : (dz > 0 ? 1 : -1);
            return new Vector3Int(dirX, 0, dirZ);
        }

        private static int CompareCells(Vector3Int a, Vector3Int b)
        {
            int x = a.x.CompareTo(b.x);
            if (x != 0)
                return x;

            int y = a.y.CompareTo(b.y);
            if (y != 0)
                return y;

            return a.z.CompareTo(b.z);
        }

        private bool IsValidOffensiveSpellTarget(EntityData actor, EntityData target, SpellId spellId)
        {
            if (actor == null || target == null)
                return false;
            if (!actor.IsAlive || !target.IsAlive)
                return false;
            if (!actor.Handle.IsValid || !target.Handle.IsValid)
                return false;
            if (actor.Handle == target.Handle)
                return false;
            if (target.Team == actor.Team || target.Team == Team.Neutral)
                return false;

            var definition = SpellCatalog.Get(spellId);
            if (actor.GridPosition.y != target.GridPosition.y)
                return false;

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, target.GridPosition);
            if (distanceFeet > definition.rangeFeet)
                return false;

            if (!definition.requiresLineOfSight)
                return true;

            var line = StrikeLineResolver.ResolveSameElevation(
                gridManager.Data,
                entityManager.Occupancy,
                actor.GridPosition,
                target.GridPosition,
                actor.Handle,
                target.Handle);

            return line.hasLineOfSight;
        }

        private bool IsValidSupportSpellTarget(EntityData actor, EntityData target, SpellId spellId, int actionCount)
        {
            if (actor == null || target == null)
                return false;
            if (!actor.IsAlive || !target.IsAlive)
                return false;
            if (!actor.Handle.IsValid || !target.Handle.IsValid)
                return false;
            if (actor.GridPosition.y != target.GridPosition.y)
                return false;

            bool isSelf = actor.Handle == target.Handle;
            bool isFriendly = target.Team == actor.Team;

            switch (spellId)
            {
                case SpellId.Heal:
                    if (target.VitalityAffinity == VitalityAffinity.Undead)
                        return false;
                    if (!isSelf && !isFriendly)
                        return false;
                    break;

                case SpellId.Harm:
                    if (target.VitalityAffinity != VitalityAffinity.Undead)
                        return false;
                    if (!isSelf && !isFriendly)
                        return false;
                    break;

                default:
                    return false;
            }

            int allowedRangeFeet = Mathf.Clamp(actionCount, 1, 3) >= 2 ? 30 : 5;
            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, target.GridPosition);
            if (distanceFeet > allowedRangeFeet)
                return false;

            if (isSelf || gridManager == null || gridManager.Data == null)
                return true;

            var line = StrikeLineResolver.ResolveSameElevation(
                gridManager.Data,
                entityManager.Occupancy,
                actor.GridPosition,
                target.GridPosition,
                actor.Handle,
                target.Handle);

            return line.hasLineOfSight;
        }

        private static bool IsUrgentSupportTarget(EntityData target)
        {
            if (target == null || !target.IsAlive)
                return false;

            int missingHp = Mathf.Max(0, target.MaxHP - target.CurrentHP);
            if (missingHp <= 0)
                return false;

            return target.CurrentHP * 2 <= target.MaxHP || missingHp >= 8;
        }

        private static int ComputeSupportUrgency(EntityData actor, EntityData target)
        {
            if (target == null)
                return int.MinValue;

            int missingHp = Mathf.Max(0, target.MaxHP - target.CurrentHP);
            int missingPercent = target.MaxHP > 0
                ? (missingHp * 100) / target.MaxHP
                : 0;

            int score = missingPercent * 100 + missingHp;
            if (actor != null && actor.Handle == target.Handle)
                score += 25;

            return score;
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

        private static bool IsBetterSecondaryArcTarget(EntityData actor, EntityData candidate, EntityData best)
        {
            if (candidate == null)
                return false;
            if (best == null)
                return true;

            int candidateHp = candidate.CurrentHP;
            int bestHp = best.CurrentHP;
            if (candidateHp < bestHp) return true;
            if (candidateHp > bestHp) return false;

            int candidateDistance = GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, candidate.GridPosition);
            int bestDistance = GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, best.GridPosition);
            if (candidateDistance < bestDistance) return true;
            if (candidateDistance > bestDistance) return false;

            return candidate.Handle.Id < best.Handle.Id;
        }

        private static bool IsBetterSupportCandidate(
            EntityData actor,
            EntityData candidate,
            EntityData best,
            int candidateUrgency,
            int bestUrgency)
        {
            if (candidate == null)
                return false;
            if (best == null)
                return true;
            if (candidateUrgency > bestUrgency)
                return true;
            if (candidateUrgency < bestUrgency)
                return false;

            bool candidateIsSelf = actor != null && candidate.Handle == actor.Handle;
            bool bestIsSelf = actor != null && best.Handle == actor.Handle;
            if (candidateIsSelf && !bestIsSelf)
                return true;
            if (!candidateIsSelf && bestIsSelf)
                return false;

            int candidateDistance = actor != null
                ? GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, candidate.GridPosition)
                : int.MaxValue;
            int bestDistance = actor != null
                ? GridDistancePF2e.DistanceFeetXZ(actor.GridPosition, best.GridPosition)
                : int.MaxValue;
            if (candidateDistance < bestDistance)
                return true;
            if (candidateDistance > bestDistance)
                return false;

            return candidate.Handle.Id < best.Handle.Id;
        }

        private static bool IsBetterSaferStepCandidate(
            int threatCount,
            int terrainPressure,
            bool inMelee,
            int distance,
            Vector3Int cell,
            int bestThreatCount,
            int bestTerrainPressure,
            bool bestInMelee,
            int bestDistance,
            Vector3Int bestCell)
        {
            if (threatCount < bestThreatCount) return true;
            if (threatCount > bestThreatCount) return false;
            if (terrainPressure < bestTerrainPressure) return true;
            if (terrainPressure > bestTerrainPressure) return false;
            if (inMelee && !bestInMelee) return true;
            if (!inMelee && bestInMelee) return false;
            if (distance < bestDistance) return true;
            if (distance > bestDistance) return false;
            return IsDeterministicallyEarlier(cell, bestCell);
        }

        private static bool IsBetterMeleeStepCandidate(
            int threatCount,
            int terrainPressure,
            int distance,
            Vector3Int cell,
            int bestThreatCount,
            int bestTerrainPressure,
            int bestDistance,
            Vector3Int bestCell)
        {
            if (threatCount < bestThreatCount) return true;
            if (threatCount > bestThreatCount) return false;
            if (terrainPressure < bestTerrainPressure) return true;
            if (terrainPressure > bestTerrainPressure) return false;
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
