using System;
using UnityEngine.InputSystem;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Coordinates Action Bar command routing and targeting toggles.
    /// Keeps ActionBarController focused on view state and event subscriptions.
    /// </summary>
    public sealed class ActionBarCommandCoordinator
    {
        private TurnManager turnManager;
        private TargetingController targetingController;
        private PlayerActionExecutor actionExecutor;
        private Action refreshAvailability;
        private RaiseShieldSpellMode castShieldSpellMode = RaiseShieldSpellMode.Standard;

        public RaiseShieldSpellMode CurrentCastShieldSpellMode => castShieldSpellMode;

        public void Bind(
            TurnManager turnManager,
            TargetingController targetingController,
            PlayerActionExecutor actionExecutor,
            Action refreshAvailability)
        {
            this.turnManager = turnManager;
            this.targetingController = targetingController;
            this.actionExecutor = actionExecutor;
            this.refreshAvailability = refreshAvailability;
        }

        public void OnStrikeClicked()
        {
            ToggleOrBeginTargeting(TargetingMode.Strike, h => actionExecutor.TryExecuteStrike(h));
        }

        public void OnTripClicked()
        {
            ToggleOrBeginTargeting(TargetingMode.Trip, h => actionExecutor.TryExecuteTrip(h));
        }

        public void OnShoveClicked()
        {
            ToggleOrBeginTargeting(TargetingMode.Shove, h => actionExecutor.TryExecuteShove(h));
        }

        public void OnGrappleClicked()
        {
            ToggleOrBeginTargeting(TargetingMode.Grapple, h => actionExecutor.TryExecuteGrapple(h));
        }

        public void OnDemoralizeClicked()
        {
            ToggleOrBeginTargeting(TargetingMode.Demoralize, h => actionExecutor.TryExecuteDemoralize(h));
        }

        public void OnRepositionClicked()
        {
            ToggleOrBeginRepositionTargeting();
        }

        public void OnEscapeClicked()
        {
            ToggleOrBeginTargeting(TargetingMode.Escape, h => actionExecutor.TryExecuteEscape(h));
        }

        public void OnAidClicked()
        {
            ToggleOrBeginTargeting(TargetingMode.Aid, h => actionExecutor.TryExecuteAid(h));
        }

        public void OnReadyClicked()
        {
            if (turnManager == null || actionExecutor == null)
                return;

            var kb = Keyboard.current;
            bool shiftPressed = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            if (shiftPressed)
            {
                turnManager.CycleReadyTriggerMode();
                refreshAvailability?.Invoke();
                return;
            }

            actionExecutor.TryExecuteReadyStrike();
        }

        public void OnReadyModeMoveClicked()
        {
            if (turnManager == null)
                return;

            if (turnManager.SetReadyTriggerMode(ReadyTriggerMode.Movement))
                refreshAvailability?.Invoke();
        }

        public void OnReadyModeAttackClicked()
        {
            if (turnManager == null)
                return;

            if (turnManager.SetReadyTriggerMode(ReadyTriggerMode.Attack))
                refreshAvailability?.Invoke();
        }

        public void OnReadyModeAnyClicked()
        {
            if (turnManager == null)
                return;

            if (turnManager.SetReadyTriggerMode(ReadyTriggerMode.Any))
                refreshAvailability?.Invoke();
        }

        public void OnRaiseShieldClicked()
        {
            if (actionExecutor == null)
                return;

            actionExecutor.TryExecuteRaiseShield();
        }

        public void OnCastSpellClicked()
        {
            if (actionExecutor == null)
                return;

            var kb = Keyboard.current;
            bool shiftPressed = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            if (shiftPressed)
            {
                ToggleCastShieldSpellMode();
                refreshAvailability?.Invoke();
                return;
            }

            actionExecutor.TryExecuteCastShieldSpell(castShieldSpellMode);
        }

        public void OnCastSpellModeStandardClicked()
        {
            if (castShieldSpellMode == RaiseShieldSpellMode.Standard)
                return;

            castShieldSpellMode = RaiseShieldSpellMode.Standard;
            refreshAvailability?.Invoke();
        }

        public void OnCastSpellModeGlassClicked()
        {
            if (castShieldSpellMode == RaiseShieldSpellMode.Glass)
                return;

            castShieldSpellMode = RaiseShieldSpellMode.Glass;
            refreshAvailability?.Invoke();
        }

        private void ToggleCastShieldSpellMode()
        {
            castShieldSpellMode = castShieldSpellMode == RaiseShieldSpellMode.Standard
                ? RaiseShieldSpellMode.Glass
                : RaiseShieldSpellMode.Standard;
        }

        public void OnStandClicked()
        {
            if (actionExecutor == null)
                return;

            actionExecutor.TryExecuteStand();
        }

        public void OnDelayClicked()
        {
            if (turnManager == null)
                return;

            if (targetingController != null && targetingController.ActiveMode != TargetingMode.None)
                targetingController.CancelTargeting();

            if (turnManager.IsDelayPlacementSelectionOpen)
            {
                turnManager.CancelDelayPlacementSelection();
                refreshAvailability?.Invoke();
                return;
            }

            if (turnManager.TryBeginDelayPlacementSelection())
                refreshAvailability?.Invoke();
        }

        public void OnReturnNowClicked()
        {
            if (turnManager == null)
                return;
            if (!turnManager.TryGetFirstDelayedPlayerActor(out var actor))
                return;

            if (turnManager.TryReturnDelayedActor(actor))
                refreshAvailability?.Invoke();
        }

        public void OnSkipDelayWindowClicked()
        {
            if (turnManager == null)
                return;

            if (turnManager.IsDelayReturnWindowOpen)
            {
                turnManager.SkipDelayReturnWindow();
                refreshAvailability?.Invoke();
            }
        }

        private void ToggleOrBeginTargeting(TargetingMode mode, Action<EntityHandle> onConfirm)
        {
            if (targetingController == null || actionExecutor == null)
                return;

            if (targetingController.ActiveMode == mode)
            {
                targetingController.CancelTargeting();
                return;
            }

            targetingController.BeginTargeting(mode, onConfirm);
        }

        private void ToggleOrBeginRepositionTargeting()
        {
            if (targetingController == null || actionExecutor == null)
                return;

            if (targetingController.ActiveMode == TargetingMode.Reposition)
            {
                targetingController.CancelTargeting();
                return;
            }

            targetingController.BeginRepositionTargeting(
                actionExecutor.TryBeginRepositionTargetSelection,
                actionExecutor.TryConfirmRepositionDestination,
                onCancelled: null,
                onCellPhaseCancelled: actionExecutor.CancelPendingRepositionSelection);
        }
    }
}
