using UnityEngine;
using PF2e.Core;

namespace PF2e.Presentation
{
    public class ConditionTickForwarder : MonoBehaviour
    {
        [SerializeField] private CombatEventBus eventBus;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null) Debug.LogError("[ConditionTickForwarder] Missing CombatEventBus", this);
        }
#endif

        private void OnEnable()
        {
            if (eventBus == null)
            {
                Debug.LogError("[ConditionTickForwarder] Missing deps. Disabling.", this);
                enabled = false;
                return;
            }
            eventBus.OnConditionsTickedTyped += HandleTicks;
        }

        private void OnDisable()
        {
            if (eventBus != null)
                eventBus.OnConditionsTickedTyped -= HandleTicks;
        }

        private void HandleTicks(in ConditionsTickedEvent e)
        {
            for (int i = 0; i < e.ticks.Count; i++)
            {
                var t = e.ticks[i];
                eventBus.PublishConditionChanged(
                    e.actor, t.type,
                    t.removed ? ConditionChangeType.Removed : ConditionChangeType.ValueChanged,
                    t.oldValue, t.newValue);
            }
        }
    }
}
