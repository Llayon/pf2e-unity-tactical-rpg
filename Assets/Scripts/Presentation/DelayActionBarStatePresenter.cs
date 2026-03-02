using UnityEngine.UI;

namespace PF2e.Presentation
{
    public readonly struct DelayActionBarState
    {
        public readonly bool blocksNormalActions;
        public readonly bool delayInteractable;
        public readonly bool returnControlsVisible;
        public readonly bool returnNowInteractable;
        public readonly bool skipInteractable;

        public DelayActionBarState(
            bool blocksNormalActions,
            bool delayInteractable,
            bool returnControlsVisible,
            bool returnNowInteractable,
            bool skipInteractable)
        {
            this.blocksNormalActions = blocksNormalActions;
            this.delayInteractable = delayInteractable;
            this.returnControlsVisible = returnControlsVisible;
            this.returnNowInteractable = returnNowInteractable;
            this.skipInteractable = skipInteractable;
        }
    }

    /// <summary>
    /// Maps TurnManager delay state to ActionBar Delay/Return/Skip controls.
    /// </summary>
    public sealed class DelayActionBarStatePresenter
    {
        public DelayActionBarState BuildInactiveState()
        {
            return new DelayActionBarState(
                blocksNormalActions: true,
                delayInteractable: false,
                returnControlsVisible: false,
                returnNowInteractable: false,
                skipInteractable: false);
        }

        public DelayActionBarState BuildReturnWindowState(bool canReturnNow)
        {
            return new DelayActionBarState(
                blocksNormalActions: true,
                delayInteractable: false,
                returnControlsVisible: true,
                returnNowInteractable: canReturnNow,
                skipInteractable: true);
        }

        public DelayActionBarState BuildPlacementSelectionState()
        {
            return new DelayActionBarState(
                blocksNormalActions: true,
                delayInteractable: true,
                returnControlsVisible: false,
                returnNowInteractable: false,
                skipInteractable: false);
        }

        public DelayActionBarState BuildNormalState(bool canDelayCurrentTurn)
        {
            return new DelayActionBarState(
                blocksNormalActions: false,
                delayInteractable: canDelayCurrentTurn,
                returnControlsVisible: false,
                returnNowInteractable: false,
                skipInteractable: false);
        }

        public void Apply(in DelayActionBarState state, Button delayButton, Button returnNowButton, Button skipDelayWindowButton)
        {
            SetInteractable(delayButton, state.delayInteractable);

            SetButtonVisible(returnNowButton, state.returnControlsVisible);
            SetButtonVisible(skipDelayWindowButton, state.returnControlsVisible);

            SetInteractable(returnNowButton, state.returnNowInteractable);
            SetInteractable(skipDelayWindowButton, state.skipInteractable);
        }

        private static void SetInteractable(Button button, bool enabled)
        {
            if (button != null)
                button.interactable = enabled;
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button == null)
                return;

            if (button.gameObject.activeSelf != visible)
                button.gameObject.SetActive(visible);
        }
    }
}
