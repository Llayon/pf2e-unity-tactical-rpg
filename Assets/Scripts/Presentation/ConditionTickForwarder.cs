using UnityEngine;
using System.Collections.Generic;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    public class ConditionTickForwarder : MonoBehaviour
    {
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private CombatEventBus eventBus;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (turnManager == null) Debug.LogError("[ConditionTickForwarder] Missing TurnManager", this);
            if (eventBus == null) Debug.LogError("[ConditionTickForwarder] Missing CombatEventBus", this);
        }
#endif

        private void OnEnable()
        {
            if (turnManager == null || eventBus == null)
            {
                Debug.LogError("[ConditionTickForwarder] Missing deps. Disabling.", this);
                enabled = false;
                return;
            }
            turnManager.OnConditionsTicked += HandleTicks;
        }

        private void OnDisable()
        {
            if (turnManager != null)
                turnManager.OnConditionsTicked -= HandleTicks;
        }

        private void HandleTicks(EntityHandle actor, IReadOnlyList<ConditionTick> ticks)
        {
            for (int i = 0; i < ticks.Count; i++)
            {
                var t = ticks[i];
                eventBus.PublishConditionChanged(
                    actor, t.type,
                    t.removed ? ConditionChangeType.Removed : ConditionChangeType.ValueChanged,
                    t.oldValue, t.newValue);
            }
        }
    }
}
