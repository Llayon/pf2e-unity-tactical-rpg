using UnityEngine;
using PF2e.Core;

namespace PF2e.Presentation
{
    public class ConditionLogForwarder : MonoBehaviour
    {
        [SerializeField] private CombatEventBus eventBus;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null) Debug.LogError("[ConditionLogForwarder] Missing CombatEventBus", this);
        }
#endif

        private void OnEnable()
        {
            if (eventBus == null) { enabled = false; return; }
            eventBus.OnConditionChangedTyped += Handle;
        }

        private void OnDisable()
        {
            if (eventBus != null)
                eventBus.OnConditionChangedTyped -= Handle;
        }

        private void Handle(in ConditionChangedEvent e)
        {
            string conditionName = ConditionRules.DisplayName(e.conditionType);
            bool valued = ConditionRules.IsValued(e.conditionType);

            string msg = e.changeType switch
            {
                ConditionChangeType.Added => CombatLogRichText.StatusAppliedSuffix(
                    BuildConditionLabel(conditionName, valued, e.newValue)),
                ConditionChangeType.Removed =>
                    CombatLogRichText.StatusRemovedSuffix(conditionName),
                ConditionChangeType.ValueChanged => CombatLogRichText.StatusAppliedSuffix(
                    BuildConditionLabel(conditionName, valued, e.newValue)),
                ConditionChangeType.DurationChanged => e.newRemainingRounds >= 0
                    ? $"{conditionName} duration decreases to {e.newRemainingRounds}"
                    : $"{conditionName} duration changed",
                _ => $"{conditionName} changed"
            };

            eventBus.Publish(e.entity, msg, CombatLogCategory.Condition);
        }

        private static string BuildConditionLabel(string conditionName, bool valued, int value)
        {
            if (valued && value > 0)
            {
                return $"{conditionName} {value}";
            }

            return conditionName;
        }
    }
}
