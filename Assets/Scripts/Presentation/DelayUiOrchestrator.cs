using UnityEngine;
using PF2e.Core;

namespace PF2e.Presentation
{
    /// <summary>
    /// Centralized Delay UI mediator for ActionBar + InitiativeBar.
    /// Owns Delay typed-event subscriptions and fans out refresh calls to presentation controllers.
    /// </summary>
    public class DelayUiOrchestrator : MonoBehaviour
    {
        [Header("Dependencies (Inspector-first)")]
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private ActionBarController actionBarController;
        [SerializeField] private InitiativeBarController initiativeBarController;

        private bool subscribed;

        private void Awake()
        {
            ResolveDependenciesIfMissing();
        }

        private void OnEnable()
        {
            ResolveDependenciesIfMissing();

            if (eventBus == null)
            {
                Debug.LogWarning("[DelayUiOrchestrator] CombatEventBus missing. Delay UI orchestration disabled.", this);
                return;
            }

            eventBus.OnDelayTurnBeginTriggerChangedTyped += HandleDelayTurnBeginTriggerChanged;
            eventBus.OnDelayPlacementSelectionChangedTyped += HandleDelayPlacementSelectionChanged;
            eventBus.OnDelayReturnWindowOpenedTyped += HandleDelayReturnWindowOpened;
            eventBus.OnDelayReturnWindowClosedTyped += HandleDelayReturnWindowClosed;
            eventBus.OnDelayedTurnEnteredTyped += HandleDelayedTurnEntered;
            eventBus.OnDelayedTurnResumedTyped += HandleDelayedTurnResumed;
            eventBus.OnDelayedTurnExpiredTyped += HandleDelayedTurnExpired;
            subscribed = true;

            RefreshAllDelayUi();
        }

        private void OnDisable()
        {
            if (!subscribed || eventBus == null)
                return;

            eventBus.OnDelayTurnBeginTriggerChangedTyped -= HandleDelayTurnBeginTriggerChanged;
            eventBus.OnDelayPlacementSelectionChangedTyped -= HandleDelayPlacementSelectionChanged;
            eventBus.OnDelayReturnWindowOpenedTyped -= HandleDelayReturnWindowOpened;
            eventBus.OnDelayReturnWindowClosedTyped -= HandleDelayReturnWindowClosed;
            eventBus.OnDelayedTurnEnteredTyped -= HandleDelayedTurnEntered;
            eventBus.OnDelayedTurnResumedTyped -= HandleDelayedTurnResumed;
            eventBus.OnDelayedTurnExpiredTyped -= HandleDelayedTurnExpired;
            subscribed = false;
        }

        private void HandleDelayTurnBeginTriggerChanged(in DelayTurnBeginTriggerChangedEvent _)
        {
            actionBarController?.RefreshDelayUiFromOrchestrator();
        }

        private void HandleDelayPlacementSelectionChanged(in DelayPlacementSelectionChangedEvent _)
        {
            actionBarController?.RefreshDelayUiFromOrchestrator();
            initiativeBarController?.RefreshDelayPlacementUiFromOrchestrator();
        }

        private void HandleDelayReturnWindowOpened(in DelayReturnWindowOpenedEvent _)
        {
            actionBarController?.RefreshDelayUiFromOrchestrator();
            initiativeBarController?.RefreshDelayReturnWindowUiFromOrchestrator();
        }

        private void HandleDelayReturnWindowClosed(in DelayReturnWindowClosedEvent _)
        {
            actionBarController?.RefreshDelayUiFromOrchestrator();
            initiativeBarController?.RefreshDelayReturnWindowUiFromOrchestrator();
        }

        private void HandleDelayedTurnEntered(in DelayedTurnEnteredEvent _)
        {
            actionBarController?.RefreshDelayUiFromOrchestrator();
            initiativeBarController?.RefreshDelayedActorsUiFromOrchestrator();
        }

        private void HandleDelayedTurnResumed(in DelayedTurnResumedEvent _)
        {
            actionBarController?.RefreshDelayUiFromOrchestrator();
            initiativeBarController?.RefreshDelayedActorsUiFromOrchestrator();
        }

        private void HandleDelayedTurnExpired(in DelayedTurnExpiredEvent _)
        {
            actionBarController?.RefreshDelayUiFromOrchestrator();
            initiativeBarController?.RefreshDelayedActorsUiFromOrchestrator();
        }

        private void RefreshAllDelayUi()
        {
            actionBarController?.RefreshDelayUiFromOrchestrator();
            initiativeBarController?.RefreshDelayedActorsUiFromOrchestrator();
            initiativeBarController?.RefreshDelayReturnWindowUiFromOrchestrator();
        }

        private void ResolveDependenciesIfMissing()
        {
            if (eventBus == null)
                eventBus = FindFirstObjectByType<CombatEventBus>();
            if (actionBarController == null)
                actionBarController = FindFirstObjectByType<ActionBarController>();
            if (initiativeBarController == null)
                initiativeBarController = FindFirstObjectByType<InitiativeBarController>();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null)
                Debug.LogWarning("[DelayUiOrchestrator] CombatEventBus not assigned.", this);
            if (actionBarController == null)
                Debug.LogWarning("[DelayUiOrchestrator] ActionBarController not assigned.", this);
            if (initiativeBarController == null)
                Debug.LogWarning("[DelayUiOrchestrator] InitiativeBarController not assigned.", this);
        }
#endif
    }
}
