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
            string aidToken = e.aidCircumstanceBonus != 0
                ? CombatLogRichText.Verb($" + AID({RollBreakdownFormatter.FormatSigned(e.aidCircumstanceBonus)})")
                : string.Empty;

            eventBus.Publish(
                e.actor,
                $"{CombatLogRichText.Verb("uses")} {CombatLogRichText.Weapon(actionLabel)} {CombatLogRichText.Verb("on")} {targetName} {CombatLogRichText.Verb("—")} " +
                $"{CombatLogRichText.Verb(RollBreakdownFormatter.FormatVsDc(e.roll, e.defenseSource, e.dc))}{aidToken}" +
                $" → {CombatLogRichText.Degree(e.degree)}{projectionToken}",
                CombatLogCategory.Attack);
        }
    }
}
