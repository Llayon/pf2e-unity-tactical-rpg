using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public class JumpAction : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        private readonly Dictionary<Vector3Int, int> strideReachabilityFeetBuffer = new();
        private readonly ConditionService conditionService = new();
        private readonly List<ConditionDelta> conditionDeltaBuffer = new();

        public void InjectDependencies(EntityManager entityManager, CombatEventBus eventBus)
        {
            if (entityManager != null)
                this.entityManager = entityManager;
            if (eventBus != null)
                this.eventBus = eventBus;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogWarning("[JumpAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[JumpAction] Missing CombatEventBus", this);
        }
#endif

        public bool TryPreviewJump(EntityHandle actor, Vector3Int landingCell, out JumpPreviewResult preview)
        {
            preview = JumpPreviewResult.Invalid(JumpFailureReason.InvalidState, landingCell);

            if (!actor.IsValid || entityManager == null || entityManager.Registry == null)
                return false;
            if (entityManager.GridData == null || entityManager.Occupancy == null || entityManager.Pathfinding == null)
                return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive)
                return false;

            BuildStrideReachability(actorData, strideReachabilityFeetBuffer);

            preview = JumpReachabilityResolver.ResolvePreview(
                actorData.GridPosition,
                actorData.Speed,
                landingCell,
                strideReachabilityFeetBuffer,
                cell => CanLand(actor, actorData.SizeCells, cell),
                heightStepFeet: GameConstants.CardinalCostFeet);

            return preview.isValid;
        }

        public bool TryExecuteJump(
            EntityHandle actor,
            Vector3Int landingCell,
            IRng rng,
            out JumpResolvedEvent resolved)
        {
            resolved = default;
            rng ??= UnityRng.Shared;

            if (!TryPreviewJump(actor, landingCell, out var preview))
                return false;
            if (entityManager == null || entityManager.Registry == null)
                return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive)
                return false;

            var fromCell = actorData.GridPosition;
            var currentCell = actorData.GridPosition;

            if (preview.takeoffCell != currentCell)
            {
                // Represents the Stride part of Long Jump / High Jump activity.
                if (!entityManager.TryMoveEntityImmediate(actor, preview.takeoffCell))
                    return false;
                currentCell = preview.takeoffCell;
            }

            bool hasCheck = preview.requiresCheck;
            CheckRoll checkRoll = default;
            DegreeOfSuccess degree = DegreeOfSuccess.Success;
            bool movedToLanding = false;
            bool becameProne = false;

            if (hasCheck)
            {
                var check = CheckResolver.RollSkillCheck(actorData, SkillType.Athletics, preview.dc, rng);
                checkRoll = check.roll;
                degree = check.degree;

                if (degree == DegreeOfSuccess.Success || degree == DegreeOfSuccess.CriticalSuccess)
                {
                    movedToLanding = entityManager.TryMoveEntityImmediate(actor, preview.landingCell);
                }
                else
                {
                    bool allowFallbackLeap = preview.jumpType != JumpType.HighJump
                        || degree != DegreeOfSuccess.CriticalFailure;
                    if (allowFallbackLeap)
                    {
                        Vector3Int fallback = ResolveFailureLeapFallbackCell(actorData, preview.takeoffCell, preview.landingCell);
                        if (fallback != preview.takeoffCell)
                            movedToLanding = entityManager.TryMoveEntityImmediate(actor, fallback);
                    }

                    if (degree == DegreeOfSuccess.CriticalFailure)
                    {
                        conditionDeltaBuffer.Clear();
                        conditionService.AddOrRefresh(actorData, ConditionType.Prone, value: 0, rounds: -1, conditionDeltaBuffer);
                        PublishConditionDeltas();
                        becameProne = true;
                    }
                }
            }
            else
            {
                movedToLanding = entityManager.TryMoveEntityImmediate(actor, preview.landingCell);
            }

            var finalData = entityManager.Registry.Get(actor);
            var finalCell = finalData != null ? finalData.GridPosition : currentCell;

            resolved = new JumpResolvedEvent(
                actor,
                preview.jumpType,
                fromCell,
                preview.takeoffCell,
                finalCell,
                preview.actionCost,
                preview.runUpFeet,
                preview.jumpDistanceFeet,
                hasCheck,
                in checkRoll,
                preview.dc,
                degree,
                movedToLanding,
                becameProne);

            eventBus?.PublishJumpResolved(in resolved);
            return true;
        }

        private void BuildStrideReachability(EntityData actorData, Dictionary<Vector3Int, int> outZone)
        {
            outZone.Clear();
            if (actorData == null || entityManager == null || entityManager.GridData == null || entityManager.Pathfinding == null)
                return;

            var profile = new MovementProfile
            {
                moveType = MovementType.Walk,
                speedFeet = actorData.Speed,
                creatureSizeCells = actorData.SizeCells,
                ignoresDifficultTerrain = false
            };

            entityManager.Pathfinding.GetMovementZone(
                entityManager.GridData,
                actorData.GridPosition,
                profile,
                actorData.Speed,
                actorData.Handle,
                entityManager.Occupancy,
                outZone);

            if (!outZone.ContainsKey(actorData.GridPosition))
                outZone[actorData.GridPosition] = 0;
        }

        private bool CanLand(EntityHandle actor, int sizeCells, Vector3Int anchorCell)
        {
            if (entityManager == null || entityManager.GridData == null || entityManager.Occupancy == null)
                return false;

            var footprint = OccupancyMap.GetFootprint(anchorCell, sizeCells);
            for (int i = 0; i < footprint.Count; i++)
            {
                if (!entityManager.GridData.IsCellPassable(footprint[i], MovementType.Walk))
                    return false;
            }

            return entityManager.Occupancy.CanOccupyFootprint(anchorCell, sizeCells, actor);
        }

        private Vector3Int ResolveFailureLeapFallbackCell(EntityData actorData, Vector3Int takeoffCell, Vector3Int intendedLandingCell)
        {
            if (actorData == null)
                return takeoffCell;

            int leapRangeFeet = JumpRules.GetLeapRangeFeet(actorData.Speed);
            int maxSteps = leapRangeFeet / GameConstants.CardinalCostFeet;
            if (maxSteps <= 0)
                return takeoffCell;

            int dirX = intendedLandingCell.x.CompareTo(takeoffCell.x);
            int dirZ = intendedLandingCell.z.CompareTo(takeoffCell.z);
            if (dirX == 0 && dirZ == 0)
                return takeoffCell;

            Vector3Int candidate = takeoffCell;
            Vector3Int best = takeoffCell;

            for (int i = 0; i < maxSteps; i++)
            {
                candidate += new Vector3Int(dirX, 0, dirZ);
                if (!CanLand(actorData.Handle, actorData.SizeCells, candidate))
                    break;
                best = candidate;
            }

            return best;
        }

        private void PublishConditionDeltas()
        {
            if (eventBus == null || conditionDeltaBuffer.Count <= 0)
                return;

            for (int i = 0; i < conditionDeltaBuffer.Count; i++)
            {
                var delta = conditionDeltaBuffer[i];
                eventBus.PublishConditionChanged(
                    delta.entity,
                    delta.type,
                    delta.changeType,
                    delta.oldValue,
                    delta.newValue,
                    delta.oldRemainingRounds,
                    delta.newRemainingRounds);
            }
        }
    }
}
