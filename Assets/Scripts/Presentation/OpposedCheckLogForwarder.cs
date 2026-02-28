using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.Presentation
{
    /// <summary>
    /// Converts typed opposed-check events into combat log lines.
    /// </summary>
    public class OpposedCheckLogForwarder : MonoBehaviour
    {
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null) Debug.LogError("[OpposedCheckLogForwarder] Missing CombatEventBus", this);
            if (entityManager == null) Debug.LogError("[OpposedCheckLogForwarder] Missing EntityManager", this);
        }
#endif

        private void OnEnable()
        {
            if (eventBus == null || entityManager == null)
            {
                Debug.LogError("[OpposedCheckLogForwarder] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnOpposedCheckResolvedTyped += HandleOpposedCheckResolved;
        }

        private void OnDisable()
        {
            if (eventBus != null)
                eventBus.OnOpposedCheckResolvedTyped -= HandleOpposedCheckResolved;
        }

        private void HandleOpposedCheckResolved(in OpposedCheckResolvedEvent e)
        {
            var targetData = entityManager.Registry != null ? entityManager.Registry.Get(e.target) : null;
            string targetName = targetData?.Name ?? "Unknown";
            string actionLabel = string.IsNullOrEmpty(e.actionName) ? "Opposed Check" : e.actionName;

            eventBus.Publish(
                e.actor,
                $"uses {actionLabel} on {targetName} — {RollBreakdownFormatter.FormatRoll(e.attackerRoll)} vs {RollBreakdownFormatter.FormatRoll(e.defenderRoll)} → {e.winner} ({RollBreakdownFormatter.FormatSigned(e.margin)})",
                CombatLogCategory.Attack);
        }
    }
}
