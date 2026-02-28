using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public class TripAction : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        [Header("Rules")]
        [SerializeField] private bool requireSameElevation = true;

        private readonly ConditionService conditionService = new();
        private readonly List<ConditionDelta> conditionDeltaBuffer = new();

        public const int ActionCost = 1;
        private const string ActionName = "Trip";

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[TripAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[TripAction] Missing CombatEventBus", this);
        }
#endif

        public TargetingFailureReason GetTripTargetFailure(EntityHandle actor, EntityHandle target)
        {
            if (!actor.IsValid || !target.IsValid) return TargetingFailureReason.InvalidTarget;
            if (entityManager == null || entityManager.Registry == null) return TargetingFailureReason.InvalidState;

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

            // MVP rule gap: free-hand model is not implemented yet, so only Trip-trait weapons are eligible.
            bool hasTripTrait = (actorData.EquippedWeapon.Traits & WeaponTraitFlags.Trip) != 0;
            if (!hasTripTrait) return TargetingFailureReason.MissingRequiredWeaponTrait;

            return TargetingFailureReason.None;
        }

        public bool CanTrip(EntityHandle actor, EntityHandle target)
        {
            return GetTripTargetFailure(actor, target) == TargetingFailureReason.None;
        }

        public DegreeOfSuccess? TryTrip(EntityHandle actor, EntityHandle target, IRng rng = null)
        {
            if (!CanTrip(actor, target)) return null;
            if (entityManager == null || entityManager.Registry == null) return null;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null) return null;

            rng ??= UnityRng.Shared;

            int skillModifier = actorData.GetSkillModifier(SkillType.Athletics);
            int mapPenalty = actorData.GetMAPPenalty(actorData.EquippedWeapon);
            int effectiveModifier = skillModifier + mapPenalty;

            // Trip has the Attack trait and increases MAP after the attempt is declared.
            actorData.MAPCount++;

            int dc = targetData.GetSaveDC(SaveType.Reflex);
            var result = CheckResolver.RollCheck(effectiveModifier, dc, CheckSource.Skill(SkillType.Athletics), rng);

            conditionDeltaBuffer.Clear();

            switch (result.degree)
            {
                case DegreeOfSuccess.CriticalSuccess:
                    conditionService.AddOrRefresh(targetData, ConditionType.Prone, value: 0, rounds: -1, conditionDeltaBuffer);
                    ApplyTripCritDamage(actor, target, rng);
                    break;

                case DegreeOfSuccess.Success:
                    conditionService.AddOrRefresh(targetData, ConditionType.Prone, value: 0, rounds: -1, conditionDeltaBuffer);
                    break;

                case DegreeOfSuccess.CriticalFailure:
                    conditionService.AddOrRefresh(actorData, ConditionType.Prone, value: 0, rounds: -1, conditionDeltaBuffer);
                    break;
            }

            PublishConditionDeltas();

            if (eventBus != null)
            {
                var ev = new SkillCheckResolvedEvent(
                    actor,
                    target,
                    SkillType.Athletics,
                    result.roll,
                    CheckSource.Save(SaveType.Reflex),
                    result.dc,
                    result.degree,
                    ActionName);
                eventBus.PublishSkillCheckResolved(in ev);
            }

            return result.degree;
        }

        private void ApplyTripCritDamage(EntityHandle sourceHandle, EntityHandle targetHandle, IRng rng)
        {
            int damage = Mathf.Max(0, rng.RollDie(6));
            DamageApplicationService.ApplyDamage(
                sourceHandle,
                targetHandle,
                damage,
                DamageType.Bludgeoning,
                ActionName,
                isCritical: true,
                entityManager,
                eventBus);
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
