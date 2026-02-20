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
            turnManager.OnCombatEndedWithResult += HandleCombatEnded;
            turnManager.OnRoundStarted += HandleRoundStarted;
            turnManager.OnTurnStarted += HandleTurnStarted;
            turnManager.OnTurnEnded += HandleTurnEnded;
            turnManager.OnActionsChanged += HandleActionsChanged;
            turnManager.OnConditionsTicked += HandleConditionsTicked;
            turnManager.OnInitiativeRolled += HandleInitiativeRolled;
        }

        private void OnDisable()
        {
            if (turnManager == null) return;
            turnManager.OnCombatStarted -= HandleCombatStarted;
            turnManager.OnCombatEndedWithResult -= HandleCombatEnded;
            turnManager.OnRoundStarted -= HandleRoundStarted;
            turnManager.OnTurnStarted -= HandleTurnStarted;
            turnManager.OnTurnEnded -= HandleTurnEnded;
            turnManager.OnActionsChanged -= HandleActionsChanged;
            turnManager.OnConditionsTicked -= HandleConditionsTicked;
            turnManager.OnInitiativeRolled -= HandleInitiativeRolled;
        }

        private void HandleCombatStarted(CombatStartedEvent _) => eventBus.PublishCombatStarted();
        private void HandleCombatEnded(CombatEndedEvent e) => eventBus.PublishCombatEnded(e.result);
        private void HandleRoundStarted(RoundStartedEvent e) => eventBus.PublishRoundStarted(e.round);

        private void HandleTurnStarted(TurnStartedEvent e)
        {
            eventBus.PublishTurnStarted(e.actor, e.actionsAtStart);
        }

        private void HandleTurnEnded(TurnEndedEvent e) => eventBus.PublishTurnEnded(e.actor);

        private void HandleActionsChanged(ActionsChangedEvent e)
        {
            if (!e.actor.IsValid) return;
            eventBus.PublishActionsChanged(e.actor, e.remaining);
        }

        private void HandleConditionsTicked(ConditionsTickedEvent e)
            => eventBus.PublishConditionsTicked(e.actor, e.ticks);

        private void HandleInitiativeRolled(InitiativeRolledEvent e)
            => eventBus.PublishInitiativeRolled(e.order);
    }
}
