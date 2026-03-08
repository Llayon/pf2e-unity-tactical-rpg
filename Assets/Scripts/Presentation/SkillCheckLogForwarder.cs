using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.Presentation
{
    /// <summary>
    /// Converts typed skill-check events (Trip, Grapple, etc.) into combat log lines.
    /// </summary>
    public class SkillCheckLogForwarder : MonoBehaviour
    {
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null) Debug.LogError("[SkillCheckLogForwarder] Missing CombatEventBus", this);
            if (entityManager == null) Debug.LogError("[SkillCheckLogForwarder] Missing EntityManager", this);
        }
#endif

        private void OnEnable()
        {
            if (eventBus == null || entityManager == null)
            {
                Debug.LogError("[SkillCheckLogForwarder] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnSkillCheckResolvedTyped += HandleSkillCheckResolved;
        }

        private void OnDisable()
        {
            if (eventBus != null)
                eventBus.OnSkillCheckResolvedTyped -= HandleSkillCheckResolved;
        }

        private void HandleSkillCheckResolved(in SkillCheckResolvedEvent e)
        {
            var targetData = entityManager.Registry != null ? entityManager.Registry.Get(e.target) : null;
            string rawTargetName = targetData?.Name ?? "Unknown";
            var targetTeam = targetData?.Team ?? Team.Neutral;
            string targetName = CombatLogRichText.EntityName(rawTargetName, targetTeam);
            string actionLabel = string.IsNullOrEmpty(e.actionName) ? e.skill.ToString() : e.actionName;
            string projectionToken = e.hasOpposedProjection
                ? CombatLogRichText.Verb($" (cmp {RollBreakdownFormatter.FormatSigned(e.opposedProjection.margin)})")
                : string.Empty;
            string degreeLabel = TooltipTextBuilder.FormatDegreeLabel(e.degree);
            string signedModifier = RollBreakdownFormatter.FormatSigned(e.roll.modifier);
            string rollTotalLink = CombatLogLinkHelper.Link(CombatLogLinkTokens.Result, $"{e.roll.total} - {degreeLabel}");
            var tooltipPayload = new CombatLogTooltipPayload(new[]
            {
                new TooltipEntry(
                    CombatLogLinkTokens.Result,
                    $"{e.roll.total} vs {e.defenseSource.ToShortLabel()} DC {e.dc} - {degreeLabel}",
                    TooltipTextBuilder.SkillCheckResultBreakdown(
                        e.roll,
                        e.defenseSource,
                        e.dc,
                        e.degree,
                        e.aidCircumstanceBonus))
            });

            eventBus.Publish(
                e.actor,
                $"{CombatLogRichText.ActionCost(1)} {CombatLogRichText.Verb("uses")} {CombatLogRichText.Weapon(actionLabel)} {CombatLogRichText.Verb("on")} {targetName} {CombatLogRichText.Verb("—")} " +
                $"{CombatLogRichText.Verb($"rolls {e.roll.naturalRoll}{signedModifier}")} {CombatLogRichText.Verb("=")} {rollTotalLink}{projectionToken}",
                CombatLogCategory.Attack,
                tooltipPayload);
        }
    }
}
