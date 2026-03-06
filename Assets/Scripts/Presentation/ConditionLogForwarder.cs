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
                    ? $"{CombatLogRichText.Verb("gains")} {CombatLogRichText.ConditionGain(rawName)} {e.newValue}"
                    : $"{CombatLogRichText.Verb("gains")} {CombatLogRichText.ConditionGain(rawName)}",
                ConditionChangeType.Removed =>
                    $"{CombatLogRichText.Verb("loses")} {CombatLogRichText.ConditionLose(rawName)}",
                ConditionChangeType.ValueChanged => e.newValue < e.oldValue
                    ? $"{CombatLogRichText.ConditionGain(rawName)} {CombatLogRichText.Verb("decreases to")} {e.newValue}"
                    : $"{CombatLogRichText.ConditionGain(rawName)} {CombatLogRichText.Verb("increases to")} {e.newValue}",
                ConditionChangeType.DurationChanged => e.newRemainingRounds >= 0
                    ? $"{CombatLogRichText.ConditionGain(rawName)} {CombatLogRichText.Verb($"duration decreases to {e.newRemainingRounds}")}"
                    : $"{CombatLogRichText.ConditionGain(rawName)} {CombatLogRichText.Verb("duration changed")}",
                _ => $"{CombatLogRichText.ConditionGain(rawName)} {CombatLogRichText.Verb("changed")}"
            };

            eventBus.Publish(e.entity, msg, CombatLogCategory.Condition);
        }
    }
}
