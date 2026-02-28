using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// PF2e Escape (MVP):
    /// - Attack trait (MAP applies and increments)
    /// - Uses best of Athletics/Acrobatics (auto-choice, no UI yet)
    /// - Escapes only from source-scoped grapple relations managed by GrappleService
    /// Known gaps: explicit player skill choice, unarmed attack escape option, broader immobilize sources.
    /// </summary>
    public class EscapeAction : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private GrappleLifecycleController grappleLifecycle;

        private readonly List<ConditionDelta> conditionDeltaBuffer = new();

        public const int ActionCost = 1;
        private const string ActionName = "Escape";

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[EscapeAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[EscapeAction] Missing CombatEventBus", this);
            if (grappleLifecycle == null) Debug.LogWarning("[EscapeAction] Missing GrappleLifecycleController", this);
        }
#endif

        public TargetingFailureReason GetEscapeTargetFailure(EntityHandle actor, EntityHandle grappler)
        {
            if (!actor.IsValid || !grappler.IsValid) return TargetingFailureReason.InvalidTarget;
            if (actor == grappler) return TargetingFailureReason.SelfTarget;
            if (entityManager == null || entityManager.Registry == null) return TargetingFailureReason.InvalidState;
            if (grappleLifecycle == null || grappleLifecycle.Service == null) return TargetingFailureReason.InvalidState;

            var actorData = entityManager.Registry.Get(actor);
            var grapplerData = entityManager.Registry.Get(grappler);
            if (actorData == null || grapplerData == null) return TargetingFailureReason.InvalidTarget;
            if (!actorData.IsAlive || !grapplerData.IsAlive) return TargetingFailureReason.NotAlive;

            if (!grappleLifecycle.Service.HasExactRelation(grappler, actor)) return TargetingFailureReason.NoGrappleRelation;
            if (!actorData.HasCondition(ConditionType.Grabbed) && !actorData.HasCondition(ConditionType.Restrained)) return TargetingFailureReason.NoGrappleRelation;

            return TargetingFailureReason.None;
        }

        public bool CanEscape(EntityHandle actor, EntityHandle grappler)
        {
            return GetEscapeTargetFailure(actor, grappler) == TargetingFailureReason.None;
        }

        public DegreeOfSuccess? TryEscape(EntityHandle actor, EntityHandle grappler, IRng rng = null)
        {
            if (!CanEscape(actor, grappler)) return null;
            if (entityManager == null || entityManager.Registry == null) return null;
            if (grappleLifecycle == null || grappleLifecycle.Service == null) return null;

            var actorData = entityManager.Registry.Get(actor);
            var grapplerData = entityManager.Registry.Get(grappler);
            if (actorData == null || grapplerData == null) return null;

            rng ??= UnityRng.Shared;

            int athleticsModifier = actorData.GetSkillModifier(SkillType.Athletics);
            int acrobaticsModifier = actorData.GetSkillModifier(SkillType.Acrobatics);
            SkillType usedSkill = acrobaticsModifier > athleticsModifier ? SkillType.Acrobatics : SkillType.Athletics;
            int baseModifier = usedSkill == SkillType.Acrobatics ? acrobaticsModifier : athleticsModifier;

            int mapPenalty = actorData.GetMAPPenalty(actorData.EquippedWeapon);
            int effectiveModifier = baseModifier + mapPenalty;

            // Escape has the Attack trait and increases MAP after the attempt is declared.
            actorData.MAPCount++;

            int dc = 10 + grapplerData.GetSkillModifier(SkillType.Athletics);
            var result = CheckResolver.RollCheck(effectiveModifier, dc, CheckSource.Skill(usedSkill), rng);

            conditionDeltaBuffer.Clear();
            if (result.degree == DegreeOfSuccess.Success || result.degree == DegreeOfSuccess.CriticalSuccess)
                grappleLifecycle.Service.ReleaseExact(grappler, entityManager.Registry, conditionDeltaBuffer, expectedTarget: actor);

            PublishConditionDeltas();

            if (eventBus != null)
            {
                var ev = new SkillCheckResolvedEvent(
                    actor,
                    grappler,
                    usedSkill,
                    result.roll,
                    CheckSource.Skill(SkillType.Athletics),
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
