using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// PF2e Reposition (MVP core):
    /// - Athletics (untrained) vs Fortitude DC
    /// - Attack trait (MAP applies and increments)
    /// - Roll resolves before destination-cell selection (two-step UX handled elsewhere)
    /// - Forced movement (future movement-trigger reactions should not trigger)
    ///
    /// Known gaps:
    /// - Free-hand model is not implemented; eligibility uses Reposition weapon trait OR exact grapple relation.
    /// - CritFailure target choice uses deterministic auto-selection (no UI choice).
    /// </summary>
    public class RepositionAction : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private GrappleLifecycleController grappleLifecycle;

        [Header("Rules")]
        [SerializeField] private bool requireSameElevation = true;

        private readonly List<NeighborInfo> neighborBuffer = new();
        private readonly Queue<Vector3Int> bfsCellQueue = new();
        private readonly Queue<int> bfsDepthQueue = new();
        private readonly HashSet<Vector3Int> visitedCells = new();
        private readonly List<Vector3Int> tempDestinationBuffer = new();

        public const int ActionCost = 1;
        private const string ActionName = "Reposition";
        private const int SuccessMoveFeet = 5;
        private const int CritSuccessMoveFeet = 10;
        private const int CritFailureCounterMoveFeet = 5;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[RepositionAction] Missing EntityManager", this);
            if (gridManager == null) Debug.LogError("[RepositionAction] Missing GridManager", this);
            if (eventBus == null) Debug.LogWarning("[RepositionAction] Missing CombatEventBus", this);
            if (grappleLifecycle == null) Debug.LogWarning("[RepositionAction] Missing GrappleLifecycleController", this);
        }
#endif

        public TargetingFailureReason GetRepositionTargetFailure(EntityHandle actor, EntityHandle target)
        {
            if (!actor.IsValid || !target.IsValid) return TargetingFailureReason.InvalidTarget;
            if (entityManager == null || entityManager.Registry == null) return TargetingFailureReason.InvalidState;
            if (gridManager == null || gridManager.Data == null) return TargetingFailureReason.InvalidState;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null) return TargetingFailureReason.InvalidTarget;
            if (!actorData.IsAlive || !targetData.IsAlive) return TargetingFailureReason.NotAlive;
            if (actor == target) return TargetingFailureReason.SelfTarget;
            if (actorData.Team == targetData.Team) return TargetingFailureReason.WrongTeam;
            if (actorData.EquippedWeapon.IsRanged) return TargetingFailureReason.RequiresMeleeWeapon;
            if (requireSameElevation && actorData.GridPosition.y != targetData.GridPosition.y) return TargetingFailureReason.WrongElevation;

            int sizeDelta = (int)targetData.Size - (int)actorData.Size;
            if (sizeDelta > 1) return TargetingFailureReason.TargetTooLarge;

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, targetData.GridPosition);
            if (distanceFeet > actorData.EquippedWeapon.ReachFeet) return TargetingFailureReason.OutOfRange;

            bool hasRepositionTrait = (actorData.EquippedWeapon.Traits & WeaponTraitFlags.Reposition) != 0;
            bool hasExactGrappleRelation = grappleLifecycle != null
                && grappleLifecycle.Service != null
                && grappleLifecycle.Service.HasExactRelation(actor, target);

            if (!hasRepositionTrait && !hasExactGrappleRelation)
                return TargetingFailureReason.MissingRequiredWeaponTrait;

            return TargetingFailureReason.None;
        }

        public bool CanRepositionTarget(EntityHandle actor, EntityHandle target)
        {
            return GetRepositionTargetFailure(actor, target) == TargetingFailureReason.None;
        }

        public DegreeOfSuccess? ResolveRepositionCheck(
            EntityHandle actor,
            EntityHandle target,
            out RepositionCheckContext context,
            IRng rng = null)
        {
            context = default;

            if (!CanRepositionTarget(actor, target)) return null;
            if (entityManager == null || entityManager.Registry == null) return null;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null) return null;

            rng ??= UnityRng.Shared;

            int skillModifier = actorData.GetSkillModifier(SkillType.Athletics);
            int mapPenalty = actorData.GetMAPPenalty(actorData.EquippedWeapon);
            int effectiveModifier = skillModifier + mapPenalty;

            // Reposition has the Attack trait and increases MAP after the attempt is declared.
            actorData.MAPCount++;

            int dc = targetData.GetSaveDC(SaveType.Fortitude);
            var result = CheckResolver.RollCheck(effectiveModifier, dc, rng);

            int maxMoveFeet = result.degree switch
            {
                DegreeOfSuccess.Success => SuccessMoveFeet,
                DegreeOfSuccess.CriticalSuccess => CritSuccessMoveFeet,
                _ => 0
            };

            context = new RepositionCheckContext(
                actor,
                target,
                result.naturalRoll,
                result.modifier,
                result.total,
                result.dc,
                result.degree,
                maxMoveFeet);

            if (result.degree == DegreeOfSuccess.CriticalFailure)
            {
                // RAW: the target can Reposition you up to 5 feet as though it succeeded.
                // MVP: deterministic auto-choice (first valid destination in stable order), no UI prompt.
                TryApplyCritFailureCounterReposition(targetData, actorData);
            }

            if (eventBus != null)
            {
                var ev = new SkillCheckResolvedEvent(
                    actor,
                    target,
                    SkillType.Athletics,
                    result.naturalRoll,
                    result.modifier,
                    result.total,
                    result.dc,
                    result.degree,
                    ActionName);
                eventBus.PublishSkillCheckResolved(in ev);
            }

            return result.degree;
        }

        public bool TryGetValidRepositionDestinations(
            EntityHandle actor,
            EntityHandle target,
            int maxMoveFeet,
            List<Vector3Int> outCells)
        {
            if (outCells == null) return false;
            outCells.Clear();

            if (!CanRepositionTarget(actor, target)) return false;
            if (entityManager == null || entityManager.Registry == null) return false;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null) return false;

            return TryGetValidDestinationsInternal(actorData, targetData, maxMoveFeet, outCells);
        }

        public bool TryApplyRepositionMove(
            EntityHandle actor,
            EntityHandle target,
            Vector3Int destinationCell,
            in RepositionCheckContext context)
        {
            if (entityManager == null || entityManager.Registry == null) return false;
            if (context.degree != DegreeOfSuccess.Success && context.degree != DegreeOfSuccess.CriticalSuccess) return false;
            if (context.actor != actor || context.target != target) return false;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null || !actorData.IsAlive || !targetData.IsAlive) return false;

            tempDestinationBuffer.Clear();
            if (!TryGetValidDestinationsInternal(actorData, targetData, context.maxMoveFeet, tempDestinationBuffer))
                return false;

            bool allowed = false;
            for (int i = 0; i < tempDestinationBuffer.Count; i++)
            {
                if (tempDestinationBuffer[i] == destinationCell)
                {
                    allowed = true;
                    break;
                }
            }
            if (!allowed) return false;

            // Forced movement path: EntityManager publishes EntityMovedEvent (forced=true) on successful move.
            return entityManager.TryMoveEntityImmediate(target, destinationCell);
        }

        private void TryApplyCritFailureCounterReposition(EntityData controllerData, EntityData movedData)
        {
            if (controllerData == null || movedData == null) return;
            if (entityManager == null) return;

            tempDestinationBuffer.Clear();
            if (!TryGetValidDestinationsInternal(controllerData, movedData, CritFailureCounterMoveFeet, tempDestinationBuffer))
                return;

            if (tempDestinationBuffer.Count <= 0)
                return;

            // Deterministic MVP choice to keep behavior testable.
            entityManager.TryMoveEntityImmediate(movedData.Handle, tempDestinationBuffer[0]);
        }

        private bool TryGetValidDestinationsInternal(
            EntityData controllerData,
            EntityData movedData,
            int maxMoveFeet,
            List<Vector3Int> outCells)
        {
            outCells.Clear();

            if (controllerData == null || movedData == null) return false;
            if (entityManager == null || entityManager.Occupancy == null) return false;
            if (gridManager == null || gridManager.Data == null) return false;
            if (maxMoveFeet <= 0) return false;

            int maxSteps = Mathf.Max(0, maxMoveFeet / 5);
            if (maxSteps <= 0) return false;

            int reachFeet = controllerData.EquippedWeapon.ReachFeet;
            if (reachFeet <= 0) reachFeet = 5;

            Vector3Int start = movedData.GridPosition;
            int fixedElevation = start.y;

            visitedCells.Clear();
            bfsCellQueue.Clear();
            bfsDepthQueue.Clear();

            visitedCells.Add(start);
            bfsCellQueue.Enqueue(start);
            bfsDepthQueue.Enqueue(0);

            while (bfsCellQueue.Count > 0)
            {
                Vector3Int current = bfsCellQueue.Dequeue();
                int depth = bfsDepthQueue.Dequeue();

                if (depth >= maxSteps)
                    continue;

                gridManager.Data.GetNeighbors(current, MovementType.Walk, neighborBuffer);
                for (int i = 0; i < neighborBuffer.Count; i++)
                {
                    Vector3Int next = neighborBuffer[i].pos;

                    // Reposition in current slice stays on the same floor.
                    if (next.y != fixedElevation) continue;
                    if (!visitedCells.Add(next)) continue;

                    // Can't move into occupied spaces (except own current footprint handled by CanOccupy(..., handle)).
                    if (!entityManager.Occupancy.CanOccupy(next, movedData.Handle))
                        continue;

                    // RAW: target must remain within controller's reach during the forced movement.
                    int distanceToControllerFeet = GridDistancePF2e.DistanceFeetXZ(controllerData.GridPosition, next);
                    if (distanceToControllerFeet > reachFeet)
                        continue;

                    bfsCellQueue.Enqueue(next);
                    bfsDepthQueue.Enqueue(depth + 1);

                    if (next != start)
                        outCells.Add(next);
                }
            }

            outCells.Sort(CompareCells);
            return outCells.Count > 0;
        }

        private static int CompareCells(Vector3Int a, Vector3Int b)
        {
            int x = a.x.CompareTo(b.x);
            if (x != 0) return x;
            int y = a.y.CompareTo(b.y);
            if (y != 0) return y;
            return a.z.CompareTo(b.z);
        }
    }
}
