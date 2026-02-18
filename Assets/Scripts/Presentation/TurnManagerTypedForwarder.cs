using UnityEngine;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Forwards TurnManager events to typed CombatEventBus channels.
    /// TurnManager → typed bus (no strings).
    /// </summary>
    public class TurnManagerTypedForwarder : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private CombatEventBus eventBus;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (turnManager == null)
                Debug.LogError("[TurnManagerTypedForwarder] Missing TurnManager", this);
            if (eventBus == null)
                Debug.LogError("[TurnManagerTypedForwarder] Missing CombatEventBus", this);
        }
#endif

        private void OnEnable()
        {
            if (turnManager == null || eventBus == null)
            {
                Debug.LogError("[TurnManagerTypedForwarder] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            turnManager.OnCombatStarted += HandleCombatStarted;
            turnManager.OnCombatEnded += HandleCombatEnded;
            turnManager.OnRoundStarted += HandleRoundStarted;
            turnManager.OnTurnStarted += HandleTurnStarted;
            turnManager.OnTurnEnded += HandleTurnEnded;
            turnManager.OnActionsChanged += HandleActionsChanged;
        }

        private void OnDisable()
        {
            if (turnManager == null) return;
            turnManager.OnCombatStarted -= HandleCombatStarted;
            turnManager.OnCombatEnded -= HandleCombatEnded;
            turnManager.OnRoundStarted -= HandleRoundStarted;
            turnManager.OnTurnStarted -= HandleTurnStarted;
            turnManager.OnTurnEnded -= HandleTurnEnded;
            turnManager.OnActionsChanged -= HandleActionsChanged;
        }

        private void HandleCombatStarted() => eventBus.PublishCombatStarted();
        private void HandleCombatEnded() => eventBus.PublishCombatEnded();
        private void HandleRoundStarted(int round) => eventBus.PublishRoundStarted(round);

        private void HandleTurnStarted(EntityHandle actor)
        {
            int actionsAtStart = turnManager.ActionsRemaining;
            eventBus.PublishTurnStarted(actor, actionsAtStart);
        }

        private void HandleTurnEnded(EntityHandle actor) => eventBus.PublishTurnEnded(actor);

        private void HandleActionsChanged(int remaining)
        {
            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return;
            eventBus.PublishActionsChanged(actor, remaining);
        }
    }
}
