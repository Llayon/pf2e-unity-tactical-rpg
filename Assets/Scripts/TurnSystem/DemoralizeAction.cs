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

        public TargetingFailureReason GetDemoralizeTargetFailure(EntityHandle actor, EntityHandle target)
        {
            if (!actor.IsValid || !target.IsValid) return TargetingFailureReason.InvalidTarget;
            if (entityManager == null || entityManager.Registry == null) return TargetingFailureReason.InvalidState;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null) return TargetingFailureReason.InvalidTarget;
            if (!actorData.IsAlive || !targetData.IsAlive) return TargetingFailureReason.NotAlive;
            if (actor == target) return TargetingFailureReason.SelfTarget;
            if (actorData.Team == targetData.Team) return TargetingFailureReason.WrongTeam;

            // MVP: awareness/LoS/language subsystems are not modeled yet.
            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, targetData.GridPosition);
            if (distanceFeet > RangeFeet) return TargetingFailureReason.OutOfRange;

            return TargetingFailureReason.None;
        }

        public bool CanDemoralize(EntityHandle actor, EntityHandle target)
        {
            return GetDemoralizeTargetFailure(actor, target) == TargetingFailureReason.None;
        }

        public DegreeOfSuccess? TryDemoralize(EntityHandle actor, EntityHandle target, IRng rng = null, int aidCircumstanceBonus = 0)
        {
            if (!CanDemoralize(actor, target)) return null;
            if (entityManager == null || entityManager.Registry == null) return null;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null) return null;

            rng ??= UnityRng.Shared;

            int modifier = actorData.GetSkillModifier(SkillType.Intimidation) + aidCircumstanceBonus;
            int dc = targetData.GetSaveDC(SaveType.Will);
            var result = CheckResolver.RollCheck(modifier, dc, CheckSource.Skill(SkillType.Intimidation), rng);

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
                var opposedProjection = OpposedCheckResult.FromRollVsDc(
                    result.roll,
                    result.dc,
                    CheckSource.Save(SaveType.Will));

                var ev = new SkillCheckResolvedEvent(
                    actor,
                    target,
                    SkillType.Intimidation,
                    result.roll,
                    CheckSource.Save(SaveType.Will),
                    result.dc,
                    result.degree,
                    ActionName,
                    opposedProjection,
                    aidCircumstanceBonus);
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
