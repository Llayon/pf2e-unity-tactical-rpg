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

        public bool CanTrip(EntityHandle actor, EntityHandle target)
        {
            if (!actor.IsValid || !target.IsValid) return false;
            if (entityManager == null || entityManager.Registry == null) return false;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null) return false;
            if (!actorData.IsAlive || !targetData.IsAlive) return false;
            if (actorData.Team == targetData.Team) return false;
            if (actorData.EquippedWeapon.IsRanged) return false;
            if (requireSameElevation && actorData.GridPosition.y != targetData.GridPosition.y) return false;

            int sizeDelta = (int)targetData.Size - (int)actorData.Size;
            if (sizeDelta > 1) return false;

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, targetData.GridPosition);
            if (distanceFeet > actorData.EquippedWeapon.ReachFeet) return false;

            // MVP rule gap: free-hand model is not implemented yet, so only Trip-trait weapons are eligible.
            bool hasTripTrait = (actorData.EquippedWeapon.Traits & WeaponTraitFlags.Trip) != 0;
            if (!hasTripTrait) return false;

            return true;
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
            var result = CheckResolver.RollCheck(effectiveModifier, dc, rng);

            conditionDeltaBuffer.Clear();

            switch (result.degree)
            {
                case DegreeOfSuccess.CriticalSuccess:
                    conditionService.AddOrRefresh(targetData, ConditionType.Prone, value: 0, rounds: -1, conditionDeltaBuffer);
                    ApplyTripCritDamage(target, targetData, rng);
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

        private void ApplyTripCritDamage(EntityHandle targetHandle, EntityData targetData, IRng rng)
        {
            if (targetData == null || !targetData.IsAlive) return;

            int damage = Mathf.Max(0, rng.RollDie(6));
            if (damage <= 0) return;

            targetData.CurrentHP -= damage;
            if (targetData.CurrentHP < 0)
                targetData.CurrentHP = 0;

            if (targetData.CurrentHP <= 0)
                entityManager.HandleDeath(targetHandle);
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
