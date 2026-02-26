using System;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Orchestrates Delay placement interactions (marker click/hover -> TurnManager actions + prompt text updates).
    /// Keeps InitiativeBarController focused on rendering and event wiring.
    /// </summary>
    internal sealed class DelayPlacementInteractionCoordinator
    {
        private TurnManager turnManager;
        private EntityManager entityManager;
        private DelayPlacementPromptPresenter promptPresenter;

        public event Action OnDelayPlacementCommitted;

        public void Bind(
            TurnManager turnManager,
            EntityManager entityManager,
            DelayPlacementPromptPresenter promptPresenter)
        {
            this.turnManager = turnManager;
            this.entityManager = entityManager;
            this.promptPresenter = promptPresenter;
        }

        public void HandleMarkerClicked(EntityHandle anchorHandle)
        {
            if (!anchorHandle.IsValid || turnManager == null)
                return;
            if (!turnManager.IsDelayPlacementSelectionOpen)
                return;
            if (!turnManager.TryDelayCurrentTurnAfterActor(anchorHandle))
                return;

            OnDelayPlacementCommitted?.Invoke();
        }

        public void HandleMarkerHoverEntered(EntityHandle anchorHandle)
        {
            if (!anchorHandle.IsValid)
                return;
            if (turnManager == null || !turnManager.IsDelayPlacementSelectionOpen)
                return;
            if (promptPresenter == null)
                return;

            string anchorName = anchorHandle.Id.ToString();
            if (entityManager != null && entityManager.Registry != null)
            {
                var data = entityManager.Registry.Get(anchorHandle);
                if (data != null && !string.IsNullOrEmpty(data.Name))
                    anchorName = data.Name;
            }

            promptPresenter.ShowAnchorPrompt(anchorName);
        }

        public void HandleMarkerHoverExited(EntityHandle _)
        {
            if (promptPresenter == null)
                return;

            promptPresenter.ClearHoverState();
            RefreshPromptForCurrentState();
        }

        public void RefreshPromptForCurrentState()
        {
            if (turnManager == null)
                return;

            if (!turnManager.IsDelayPlacementSelectionOpen)
            {
                HidePrompt();
                return;
            }

            if (promptPresenter == null || promptPresenter.IsHoverActive)
                return;

            promptPresenter.ShowDefaultPrompt();
        }

        public void HidePrompt()
        {
            promptPresenter?.Hide();
        }

        public void ClearHoverState()
        {
            promptPresenter?.ClearHoverState();
        }
    }
}
