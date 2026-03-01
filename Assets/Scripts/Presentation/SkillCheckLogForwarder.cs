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
            string targetName = targetData?.Name ?? "Unknown";
            string actionLabel = string.IsNullOrEmpty(e.actionName) ? e.skill.ToString() : e.actionName;
            string projectionToken = e.hasOpposedProjection
                ? $" (cmp {RollBreakdownFormatter.FormatSigned(e.opposedProjection.margin)})"
                : string.Empty;
            string aidToken = e.aidCircumstanceBonus != 0
                ? $" + AID({RollBreakdownFormatter.FormatSigned(e.aidCircumstanceBonus)})"
                : string.Empty;

            eventBus.Publish(
                e.actor,
                $"uses {actionLabel} on {targetName} — {RollBreakdownFormatter.FormatVsDc(e.roll, e.defenseSource, e.dc)}{aidToken} → {e.degree}{projectionToken}",
                CombatLogCategory.Attack);
        }
    }
}
