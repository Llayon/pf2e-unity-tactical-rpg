using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Ready Strike basic action (trigger-model MVP):
    /// - spends 2 actions now,
    /// - stores a readied strike state for actor,
    /// - strike is attempted later as a reaction based on selected trigger mode.
    /// </summary>
    public class ReadyStrikeAction : MonoBehaviour
    {
        public const int ActionCost = 2;

        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private CombatEventBus eventBus;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (turnManager == null) Debug.LogWarning("[ReadyStrikeAction] Missing TurnManager", this);
            if (eventBus == null) Debug.LogWarning("[ReadyStrikeAction] Missing CombatEventBus", this);
        }
#endif

        public void InjectDependencies(
            TurnManager turnManager,
            EntityManager entityManager,
            StrikeAction strikeAction,
            CombatEventBus eventBus)
        {
            if (turnManager != null) this.turnManager = turnManager;
            if (eventBus != null) this.eventBus = eventBus;
        }

        public bool TryPrepareReadiedStrike(
            EntityHandle actor,
            int preparedRound,
            ReadyTriggerMode triggerMode = ReadyTriggerMode.Any)
        {
            if (!actor.IsValid)
                return false;
            if (turnManager == null)
                return false;
            if (turnManager.HasReadiedStrike(actor))
                return false;

            if (!turnManager.TryPrepareReadiedStrike(actor, preparedRound, triggerMode))
                return false;

            eventBus?.Publish(
                actor,
                $"readies Strike (trigger: {triggerMode.ToPrepareDescription()}).",
                CombatLogCategory.Turn);
            return true;
        }
    }
}
