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
            string rawName = ConditionRules.DisplayName(e.conditionType);
            bool valued = ConditionRules.IsValued(e.conditionType);

            string msg = e.changeType switch
            {
                ConditionChangeType.Added => valued && e.newValue > 0
                    ? $"gains {rawName} {e.newValue}"
                    : $"gains {rawName}",
                ConditionChangeType.Removed =>
                    $"loses {rawName}",
                ConditionChangeType.ValueChanged => e.newValue < e.oldValue
                    ? $"{rawName} decreases to {e.newValue}"
                    : $"{rawName} increases to {e.newValue}",
                ConditionChangeType.DurationChanged => e.newRemainingRounds >= 0
                    ? $"{rawName} duration decreases to {e.newRemainingRounds}"
                    : $"{rawName} duration changed",
                _ => $"{rawName} changed"
            };

            eventBus.Publish(e.entity, msg, CombatLogCategory.Condition);
        }
    }
}
