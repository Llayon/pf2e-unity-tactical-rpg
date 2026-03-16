using System;
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
        private TargetingController targetingController;
        private PlayerActionExecutor actionExecutor;
        private Action refreshAvailability;
        private SpellId selectedSpell = SpellId.ForceBarrage;
        private int forceBarrageActionCount = 1;

        public SpellId CurrentSelectedSpell => selectedSpell;
        public int CurrentForceBarrageActionCount => forceBarrageActionCount;

        public void Bind(
            TargetingController targetingController,
            PlayerActionExecutor actionExecutor,
            Action refreshAvailability)
        {
            this.targetingController = targetingController;
            this.actionExecutor = actionExecutor;
            this.refreshAvailability = refreshAvailability;
        }

        public void SyncSpellSelection(EntityData actorData, int actionsRemaining)
        {
            if (actorData == null)
            {
                selectedSpell = SpellId.ForceBarrage;
                forceBarrageActionCount = 1;
                return;
            }

            int maxForceBarrageActions = Math.Clamp(actionsRemaining, 1, 3);
            forceBarrageActionCount = Math.Clamp(forceBarrageActionCount, 1, maxForceBarrageActions);

            bool forceBarrageAvailable = CanSelectForceBarrage(actorData, actionsRemaining);
            bool electricArcAvailable = CanSelectElectricArc(actorData, actionsRemaining);
            bool snowballAvailable = CanSelectSnowball(actorData, actionsRemaining);

            bool selectedAvailable = selectedSpell switch
            {
                SpellId.ForceBarrage => forceBarrageAvailable,
                SpellId.ElectricArc => electricArcAvailable,
                SpellId.Snowball => snowballAvailable,
                _ => false
            };

            if (selectedAvailable)
                return;

            if (forceBarrageAvailable)
                selectedSpell = SpellId.ForceBarrage;
            else if (electricArcAvailable)
                selectedSpell = SpellId.ElectricArc;
            else if (snowballAvailable)
                selectedSpell = SpellId.Snowball;
        }

        public bool CanSelectForceBarrage(EntityData actorData, int actionsRemaining)
        {
            return actorData != null
                && actorData.IsAlive
                && actorData.KnowsForceBarrage
                && actionsRemaining >= SpellCatalog.Get(SpellId.ForceBarrage).minActionCost;
        }

        public bool CanSelectElectricArc(EntityData actorData, int actionsRemaining)
        {
            return actorData != null
                && actorData.IsAlive
                && actorData.KnowsElectricArc
                && actionsRemaining >= SpellCatalog.Get(SpellId.ElectricArc).minActionCost;
        }

        public bool CanSelectSnowball(EntityData actorData, int actionsRemaining)
        {
            return actorData != null
                && actorData.IsAlive
                && actorData.KnowsSnowball
                && actionsRemaining >= SpellCatalog.Get(SpellId.Snowball).minActionCost;
        }

        public bool HasAnyActionBarSpell(EntityData actorData)
        {
            return actorData != null
                && actorData.IsAlive
                && actorData.KnowsAnyActionBarSpell;
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

        public void OnJumpClicked()
        {
            if (targetingController == null || actionExecutor == null)
                return;

            if (targetingController.ActiveMode == TargetingMode.Jump)
            {
                targetingController.CancelTargeting();
                return;
            }

            targetingController.BeginCellTargeting(
                TargetingMode.Jump,
                onCellConfirmed: c => actionExecutor.TryExecuteJumpToCell(c));
        }

        public void OnRaiseShieldClicked()
        {
            if (actionExecutor == null)
                return;

            actionExecutor.TryExecuteRaiseShield();
        }

        public void OnCastSpellClicked()
        {
            if (targetingController == null || actionExecutor == null)
                return;

            if (targetingController.IsSpellTargetingActive)
            {
                targetingController.TryConfirmSpellTargeting();
                return;
            }

            switch (selectedSpell)
            {
                case SpellId.ForceBarrage:
                    if (!actionExecutor.TryBeginForceBarrage(forceBarrageActionCount))
                        return;

                    targetingController.BeginForceBarrageTargeting(
                        forceBarrageActionCount,
                        targets => actionExecutor.TryConfirmForceBarrage(targets, targets != null ? targets.Count : 0));
                    return;

                case SpellId.ElectricArc:
                    if (!actionExecutor.TryBeginElectricArc())
                        return;

                    targetingController.BeginElectricArcTargeting(
                        targets => actionExecutor.TryConfirmElectricArc(targets));
                    return;

                case SpellId.Snowball:
                    if (!actionExecutor.TryBeginSnowball())
                        return;

                    targetingController.BeginSnowballTargeting(
                        targets => targets != null
                            && targets.Count > 0
                            && actionExecutor.TryConfirmSnowball(targets[0]));
                    return;
            }
        }

        public bool TryBeginForceBarrage(int actionCount)
        {
            if (targetingController == null || actionExecutor == null)
                return false;

            forceBarrageActionCount = Math.Clamp(actionCount, 1, 3);
            selectedSpell = SpellId.ForceBarrage;

            if (!actionExecutor.TryBeginForceBarrage(forceBarrageActionCount))
                return false;

            targetingController.BeginForceBarrageTargeting(
                forceBarrageActionCount,
                targets => actionExecutor.TryConfirmForceBarrage(targets, targets != null ? targets.Count : 0));
            refreshAvailability?.Invoke();
            return true;
        }

        public bool TryBeginElectricArc()
        {
            if (targetingController == null || actionExecutor == null)
                return false;

            selectedSpell = SpellId.ElectricArc;

            if (!actionExecutor.TryBeginElectricArc())
                return false;

            targetingController.BeginElectricArcTargeting(
                targets => actionExecutor.TryConfirmElectricArc(targets));
            refreshAvailability?.Invoke();
            return true;
        }

        public bool TryBeginSnowball()
        {
            if (targetingController == null || actionExecutor == null)
                return false;

            selectedSpell = SpellId.Snowball;

            if (!actionExecutor.TryBeginSnowball())
                return false;

            targetingController.BeginSnowballTargeting(
                targets => targets != null
                    && targets.Count > 0
                    && actionExecutor.TryConfirmSnowball(targets[0]));
            refreshAvailability?.Invoke();
            return true;
        }

        public bool TryConfirmSpellTargeting()
        {
            return targetingController != null
                && targetingController.TryConfirmSpellTargeting();
        }

        public void CancelSpellTargeting()
        {
            targetingController?.CancelTargeting();
        }

        public void SelectSpell(SpellId spellId)
        {
            selectedSpell = spellId;
            refreshAvailability?.Invoke();
        }

        public void SetForceBarrageActionCount(int actionCount)
        {
            forceBarrageActionCount = Math.Clamp(actionCount, 1, 3);
            selectedSpell = SpellId.ForceBarrage;
            refreshAvailability?.Invoke();
        }

        public void OnCastSpellModeStandardClicked()
        {
            if (selectedSpell == SpellId.ForceBarrage)
                forceBarrageActionCount = forceBarrageActionCount >= 3 ? 1 : forceBarrageActionCount + 1;
            else
                selectedSpell = SpellId.ForceBarrage;

            refreshAvailability?.Invoke();
        }

        public void OnCastSpellModeGlassClicked()
        {
            selectedSpell = SpellId.ElectricArc;
            refreshAvailability?.Invoke();
        }

        public void OnCastSpellModeSnowballClicked()
        {
            selectedSpell = SpellId.Snowball;
            refreshAvailability?.Invoke();
        }

        public void OnStandClicked()
        {
            if (actionExecutor == null)
                return;

            actionExecutor.TryExecuteStand();
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
