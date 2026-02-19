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
            string name = ConditionRules.DisplayName(e.conditionType);
            bool valued = ConditionRules.IsValued(e.conditionType);

            string msg = e.changeType switch
            {
                ConditionChangeType.Added => valued && e.newValue > 0
                    ? $"gains {name} {e.newValue}" : $"gains {name}",
                ConditionChangeType.Removed => $"loses {name}",
                ConditionChangeType.ValueChanged => e.newValue < e.oldValue
                    ? $"{name} decreases to {e.newValue}"
                    : $"{name} increases to {e.newValue}",
                _ => $"{name} changed"
            };

            eventBus.Publish(e.entity, msg, CombatLogCategory.Condition);
        }
    }
}
