using UnityEngine;
using PF2e.Core;

namespace PF2e.Presentation
{
    /// <summary>
    /// Converts typed stride events to string log entries.
    /// Typed stride events → string log.
    /// </summary>
    public class StrideLogForwarder : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private CombatEventBus eventBus;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null)
                Debug.LogError("[StrideLogForwarder] Missing CombatEventBus", this);
        }
#endif

        private void OnEnable()
        {
            if (eventBus == null)
            {
                Debug.LogError("[StrideLogForwarder] Missing eventBus. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnStrideStartedTyped += OnStrideStartedTyped;
            eventBus.OnStrideCompletedTyped += OnStrideCompletedTyped;
        }

        private void OnDisable()
        {
            if (eventBus == null) return;
            eventBus.OnStrideStartedTyped -= OnStrideStartedTyped;
            eventBus.OnStrideCompletedTyped -= OnStrideCompletedTyped;
        }

        private void OnStrideStartedTyped(in StrideStartedEvent e)
        {
            eventBus.Publish(e.actor, $"{CombatLogRichText.ActionCost(Mathf.Clamp(e.actionsCost, 1, 3))} {CombatLogRichText.Verb($"strides → {e.to}")}", CombatLogCategory.Movement);
        }

        private void OnStrideCompletedTyped(in StrideCompletedEvent e)
        {
            // Intentionally silent — stride destination already shown in StrideStarted line.
        }
    }
}
