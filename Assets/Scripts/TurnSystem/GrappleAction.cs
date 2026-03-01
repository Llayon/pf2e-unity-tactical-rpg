using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// PF2e Grapple (MVP):
    /// - Athletics (untrained) vs Fortitude DC
    /// - Attack trait (MAP applies and increments)
    /// - Success applies Grabbed, CritSuccess applies Restrained
        /// Known gaps: free-hand model (weapon Grapple trait only), full RAW crit-failure branch.
    /// </summary>
    public class GrappleAction : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private GrappleLifecycleController grappleLifecycle;

        [Header("Rules")]
        [SerializeField] private bool requireSameElevation = true;

        private readonly ConditionService conditionService = new();
        private readonly List<ConditionDelta> conditionDeltaBuffer = new();

        public const int ActionCost = 1;
        private const string ActionName = "Grapple";

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[GrappleAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[GrappleAction] Missing CombatEventBus", this);
            if (grappleLifecycle == null) Debug.LogWarning("[GrappleAction] Missing GrappleLifecycleController", this);
        }
#endif

        public TargetingFailureReason GetGrappleTargetFailure(EntityHandle actor, EntityHandle target)
        {
            if (!actor.IsValid || !target.IsValid) return TargetingFailureReason.InvalidTarget;
            if (entityManager == null || entityManager.Registry == null) return TargetingFailureReason.InvalidState;
            if (grappleLifecycle == null || grappleLifecycle.Service == null) return TargetingFailureReason.InvalidState;

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

            // MVP rule gap: free-hand model is not implemented yet, so only Grapple-trait weapons are eligible.
            bool hasGrappleTrait = (actorData.EquippedWeapon.Traits & WeaponTraitFlags.Grapple) != 0;
            if (!hasGrappleTrait) return TargetingFailureReason.MissingRequiredWeaponTrait;

            return TargetingFailureReason.None;
        }

        public bool CanGrapple(EntityHandle actor, EntityHandle target)
        {
            return GetGrappleTargetFailure(actor, target) == TargetingFailureReason.None;
        }

        public DegreeOfSuccess? TryGrapple(EntityHandle actor, EntityHandle target, IRng rng = null)
        {
            if (!CanGrapple(actor, target)) return null;
            if (entityManager == null || entityManager.Registry == null) return null;
            if (grappleLifecycle == null || grappleLifecycle.Service == null) return null;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null) return null;

            rng ??= UnityRng.Shared;

            int skillModifier = actorData.GetSkillModifier(SkillType.Athletics);
            int mapPenalty = actorData.GetMAPPenalty(actorData.EquippedWeapon);
            int effectiveModifier = skillModifier + mapPenalty;

            // Grapple has the Attack trait and increases MAP after the attempt is declared.
            actorData.MAPCount++;

            int dc = targetData.GetSaveDC(SaveType.Fortitude);
            var result = CheckResolver.RollCheck(effectiveModifier, dc, CheckSource.Skill(SkillType.Athletics), rng);

            conditionDeltaBuffer.Clear();
            var grappleService = grappleLifecycle.Service;
            switch (result.degree)
            {
                case DegreeOfSuccess.CriticalSuccess:
                    grappleService.ApplyOrRefresh(
                        actorData,
                        targetData,
                        GrappleHoldState.Restrained,
                        entityManager.Registry,
                        conditionDeltaBuffer);
                    break;

                case DegreeOfSuccess.Success:
                    grappleService.ApplyOrRefresh(
                        actorData,
                        targetData,
                        GrappleHoldState.Grabbed,
                        entityManager.Registry,
                        conditionDeltaBuffer);
                    break;

                case DegreeOfSuccess.Failure:
                    grappleService.ReleaseExact(actor, entityManager.Registry, conditionDeltaBuffer, expectedTarget: target);
                    break;

                case DegreeOfSuccess.CriticalFailure:
                    grappleService.ReleaseExact(actor, entityManager.Registry, conditionDeltaBuffer, expectedTarget: target);
                    // MVP simplification: RAW target-choice branch requires source-scoped grapple state.
                    conditionService.AddOrRefresh(actorData, ConditionType.Prone, value: 0, rounds: -1, conditionDeltaBuffer);
                    break;
            }

            PublishConditionDeltas();

            if (eventBus != null)
            {
                var opposedProjection = OpposedCheckResult.FromRollVsDc(
                    result.roll,
                    result.dc,
                    CheckSource.Save(SaveType.Fortitude));

                var ev = new SkillCheckResolvedEvent(
                    actor,
                    target,
                    SkillType.Athletics,
                    result.roll,
                    CheckSource.Save(SaveType.Fortitude),
                    result.dc,
                    result.degree,
                    ActionName,
                    opposedProjection);
                eventBus.PublishSkillCheckResolved(in ev);
            }

            return result.degree;
        }

        private void PublishConditionDeltas()
        {
            if (eventBus == null || conditionDeltaBuffer.Count == 0) return;

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
