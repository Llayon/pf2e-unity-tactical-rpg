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
    /// Known gaps: free-hand model (weapon Grapple trait only), source-scoped grapple state,
    /// Escape interaction, auto-end on grappler movement, RAW crit-failure branch.
    /// </summary>
    public class GrappleAction : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

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
        }
#endif

        public bool CanGrapple(EntityHandle actor, EntityHandle target)
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

            // MVP rule gap: free-hand model is not implemented yet, so only Grapple-trait weapons are eligible.
            bool hasGrappleTrait = (actorData.EquippedWeapon.Traits & WeaponTraitFlags.Grapple) != 0;
            if (!hasGrappleTrait) return false;

            return true;
        }

        public DegreeOfSuccess? TryGrapple(EntityHandle actor, EntityHandle target, IRng rng = null)
        {
            if (!CanGrapple(actor, target)) return null;
            if (entityManager == null || entityManager.Registry == null) return null;

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
            var result = CheckResolver.RollCheck(effectiveModifier, dc, rng);

            conditionDeltaBuffer.Clear();
            switch (result.degree)
            {
                case DegreeOfSuccess.CriticalSuccess:
                    // Clean up overlapping state for readability; Restrained supersedes Grabbed in the MVP slice.
                    conditionService.Remove(targetData, ConditionType.Grabbed, conditionDeltaBuffer);
                    // MVP no source-scoped duration tracking: use indefinite duration until Grapple relation exists.
                    conditionService.AddOrRefresh(targetData, ConditionType.Restrained, value: 0, rounds: -1, conditionDeltaBuffer);
                    break;

                case DegreeOfSuccess.Success:
                    // MVP no source-scoped duration tracking: use indefinite duration until Grapple relation exists.
                    conditionService.AddOrRefresh(targetData, ConditionType.Grabbed, value: 0, rounds: -1, conditionDeltaBuffer);
                    break;

                case DegreeOfSuccess.CriticalFailure:
                    // MVP simplification: RAW target-choice branch requires source-scoped grapple state.
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
