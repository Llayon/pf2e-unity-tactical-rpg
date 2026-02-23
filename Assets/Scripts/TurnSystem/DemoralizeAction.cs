using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// PF2e Demoralize (MVP):
    /// - Intimidation (untrained) vs Will DC
    /// - range 30 ft
    /// - no MAP (not an Attack action)
    /// Known gaps (deferred): language penalty, awareness checks, temporary immunity.
    /// </summary>
    public class DemoralizeAction : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        private readonly ConditionService conditionService = new();
        private readonly List<ConditionDelta> conditionDeltaBuffer = new();

        public const int ActionCost = 1;
        private const string ActionName = "Demoralize";
        private const int RangeFeet = 30;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[DemoralizeAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[DemoralizeAction] Missing CombatEventBus", this);
        }
#endif

        public bool CanDemoralize(EntityHandle actor, EntityHandle target)
        {
            if (!actor.IsValid || !target.IsValid) return false;
            if (entityManager == null || entityManager.Registry == null) return false;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null) return false;
            if (!actorData.IsAlive || !targetData.IsAlive) return false;
            if (actorData.Team == targetData.Team) return false;

            // MVP: awareness/LoS/language subsystems are not modeled yet.
            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, targetData.GridPosition);
            if (distanceFeet > RangeFeet) return false;

            return true;
        }

        public DegreeOfSuccess? TryDemoralize(EntityHandle actor, EntityHandle target, IRng rng = null)
        {
            if (!CanDemoralize(actor, target)) return null;
            if (entityManager == null || entityManager.Registry == null) return null;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null) return null;

            rng ??= UnityRng.Shared;

            int modifier = actorData.GetSkillModifier(SkillType.Intimidation);
            int dc = targetData.GetSaveDC(SaveType.Will);
            var result = CheckResolver.RollCheck(modifier, dc, rng);

            conditionDeltaBuffer.Clear();
            switch (result.degree)
            {
                case DegreeOfSuccess.CriticalSuccess:
                    conditionService.AddOrRefresh(targetData, ConditionType.Frightened, value: 2, rounds: -1, conditionDeltaBuffer);
                    break;

                case DegreeOfSuccess.Success:
                    conditionService.AddOrRefresh(targetData, ConditionType.Frightened, value: 1, rounds: -1, conditionDeltaBuffer);
                    break;
            }

            PublishConditionDeltas();

            if (eventBus != null)
            {
                var ev = new SkillCheckResolvedEvent(
                    actor,
                    target,
                    SkillType.Intimidation,
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
