using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.Presentation
{
    /// <summary>
    /// Converts typed Aid events (prepare/resolve/clear lifecycle) into combat log lines.
    /// </summary>
    public class AidResolvedLogForwarder : MonoBehaviour
    {
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null) Debug.LogError("[AidResolvedLogForwarder] Missing CombatEventBus", this);
            if (entityManager == null) Debug.LogError("[AidResolvedLogForwarder] Missing EntityManager", this);
        }
#endif

        private void OnEnable()
        {
            if (eventBus == null || entityManager == null)
            {
                Debug.LogError("[AidResolvedLogForwarder] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnAidPreparedTyped += HandleAidPrepared;
            eventBus.OnAidResolvedTyped += HandleAidResolved;
            eventBus.OnAidClearedTyped += HandleAidCleared;
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                eventBus.OnAidPreparedTyped -= HandleAidPrepared;
                eventBus.OnAidResolvedTyped -= HandleAidResolved;
                eventBus.OnAidClearedTyped -= HandleAidCleared;
            }
        }

        private void HandleAidPrepared(in AidPreparedEvent e)
        {
            var allyData = entityManager.Registry != null ? entityManager.Registry.Get(e.ally) : null;
            string allyName = allyData?.Name ?? "Unknown";

            eventBus.Publish(
                e.helper,
                $"prepares Aid for {allyName}",
                CombatLogCategory.Turn);
        }

        private void HandleAidResolved(in AidResolvedEvent e)
        {
            var allyData = entityManager.Registry != null ? entityManager.Registry.Get(e.ally) : null;
            string allyName = allyData?.Name ?? "Unknown";
            string actionLabel = string.IsNullOrWhiteSpace(e.triggeringActionName)
                ? "check"
                : e.triggeringActionName;

            string modifierText = FormatModifierText(e.modifierApplied);
            string reactionToken = e.reactionConsumed ? string.Empty : " (reaction not consumed)";

            eventBus.Publish(
                e.helper,
                $"aids {allyName} for {actionLabel} — {RollBreakdownFormatter.FormatRoll(e.roll)} vs DC {e.dc} → {e.degree} ({modifierText}){reactionToken}",
                CombatLogCategory.Attack);
        }

        private void HandleAidCleared(in AidClearedEvent e)
        {
            string message = BuildClearMessage(in e);
            if (string.IsNullOrEmpty(message))
                return;

            eventBus.Publish(e.helper, message, CombatLogCategory.Turn);
        }

        private string BuildClearMessage(in AidClearedEvent e)
        {
            if (e.reason == AidClearReason.Consumed || e.reason == AidClearReason.CombatEnded || e.reason == AidClearReason.None)
                return null;

            var allyData = entityManager.Registry != null ? entityManager.Registry.Get(e.ally) : null;
            string allyName = allyData?.Name ?? "Unknown";

            return e.reason switch
            {
                AidClearReason.ExpiredOnHelperTurnStart => $"Aid for {allyName} expires.",
                AidClearReason.OverwrittenByNewPreparation => $"Aid for {allyName} is replaced by a new preparation.",
                _ => null
            };
        }

        private static string FormatModifierText(int modifierApplied)
        {
            if (modifierApplied > 0)
                return $"{RollBreakdownFormatter.FormatSigned(modifierApplied)} circumstance";

            if (modifierApplied < 0)
                return $"{modifierApplied} circumstance penalty";

            return "no modifier";
        }
    }
}
