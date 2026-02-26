using UnityEngine;
using UnityEngine.UI;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Bottom-center combat action bar (MVP fixed slots).
    /// Event-driven via CombatEventBus + TargetingController.OnModeChanged.
    /// Reads current state from TurnManager / EntityManager / PlayerActionExecutor on refresh.
    /// </summary>
    public class ActionBarController : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private PlayerActionExecutor actionExecutor;
        [SerializeField] private TargetingController targetingController;

        [Header("UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button strikeButton;
        [SerializeField] private Button tripButton;
        [SerializeField] private Button shoveButton;
        [SerializeField] private Button grappleButton;
        [SerializeField] private Button repositionButton;
        [SerializeField] private Button demoralizeButton;
        [SerializeField] private Button escapeButton;
        [SerializeField] private Button raiseShieldButton;
        [SerializeField] private Button standButton;
        [SerializeField] private Button delayButton;
        [SerializeField] private Button returnNowButton;
        [SerializeField] private Button skipDelayWindowButton;

        [Header("Highlights (optional overlays)")]
        [SerializeField] private Image strikeHighlight;
        [SerializeField] private Image tripHighlight;
        [SerializeField] private Image shoveHighlight;
        [SerializeField] private Image grappleHighlight;
        [SerializeField] private Image repositionHighlight;
        [SerializeField] private Image demoralizeHighlight;
        [SerializeField] private Image escapeHighlight;
        [SerializeField] private Image raiseShieldHighlight;
        [SerializeField] private Image standHighlight;

        private bool buttonListenersBound;
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null) Debug.LogError("[ActionBar] Missing CombatEventBus", this);
            if (entityManager == null) Debug.LogError("[ActionBar] Missing EntityManager", this);
            if (turnManager == null) Debug.LogError("[ActionBar] Missing TurnManager", this);
            if (actionExecutor == null) Debug.LogError("[ActionBar] Missing PlayerActionExecutor", this);
            if (targetingController == null) Debug.LogError("[ActionBar] Missing TargetingController", this);

            if (canvasGroup == null) Debug.LogWarning("[ActionBar] Missing CanvasGroup", this);

            if (strikeButton == null) Debug.LogWarning("[ActionBar] strikeButton not assigned", this);
            if (tripButton == null) Debug.LogWarning("[ActionBar] tripButton not assigned", this);
            if (shoveButton == null) Debug.LogWarning("[ActionBar] shoveButton not assigned", this);
            if (grappleButton == null) Debug.LogWarning("[ActionBar] grappleButton not assigned", this);
            if (repositionButton == null) Debug.LogWarning("[ActionBar] repositionButton not assigned", this);
            if (demoralizeButton == null) Debug.LogWarning("[ActionBar] demoralizeButton not assigned", this);
            if (escapeButton == null) Debug.LogWarning("[ActionBar] escapeButton not assigned", this);
            if (raiseShieldButton == null) Debug.LogWarning("[ActionBar] raiseShieldButton not assigned", this);
            if (standButton == null) Debug.LogWarning("[ActionBar] standButton not assigned", this);
            // delay/return/skip buttons are optional in older scenes; no warning spam.
        }
#endif

        private void Awake()
        {
            EnsureButtonListenersBound();

            SetCombatVisible(false);
            SetAllInteractable(false);
            SetDelayWindowControlsVisible(false);
            SetDelayControlInteractable(false);
            SetDelayReturnControlsInteractable(false, false);
            ClearAllHighlights();
        }

        private void OnEnable()
        {
            EnsureButtonListenersBound();

            if (eventBus == null || entityManager == null || turnManager == null || actionExecutor == null || targetingController == null)
            {
                Debug.LogError("[ActionBar] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnCombatStartedTyped += HandleCombatStarted;
            eventBus.OnCombatEndedTyped += HandleCombatEnded;
            eventBus.OnTurnStartedTyped += HandleTurnStarted;
            eventBus.OnTurnEndedTyped += HandleTurnEnded;
            eventBus.OnActionsChangedTyped += HandleActionsChanged;
            eventBus.OnConditionChangedTyped += HandleConditionChanged;
            eventBus.OnShieldRaisedTyped += HandleShieldRaised;
            eventBus.OnDelayTurnBeginTriggerChangedTyped += HandleDelayTurnBeginTriggerChanged;
            eventBus.OnDelayPlacementSelectionChangedTyped += HandleDelayPlacementSelectionChanged;
            eventBus.OnDelayReturnWindowOpenedTyped += HandleDelayReturnWindowOpened;
            eventBus.OnDelayReturnWindowClosedTyped += HandleDelayReturnWindowClosed;
            eventBus.OnDelayedTurnEnteredTyped += HandleDelayedTurnEntered;
            eventBus.OnDelayedTurnResumedTyped += HandleDelayedTurnResumed;
            eventBus.OnDelayedTurnExpiredTyped += HandleDelayedTurnExpired;
            targetingController.OnModeChanged += HandleModeChanged;

            HandleModeChanged(targetingController.ActiveMode);
            RefreshAvailability();
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                eventBus.OnCombatStartedTyped -= HandleCombatStarted;
                eventBus.OnCombatEndedTyped -= HandleCombatEnded;
                eventBus.OnTurnStartedTyped -= HandleTurnStarted;
                eventBus.OnTurnEndedTyped -= HandleTurnEnded;
                eventBus.OnActionsChangedTyped -= HandleActionsChanged;
                eventBus.OnConditionChangedTyped -= HandleConditionChanged;
                eventBus.OnShieldRaisedTyped -= HandleShieldRaised;
                eventBus.OnDelayTurnBeginTriggerChangedTyped -= HandleDelayTurnBeginTriggerChanged;
                eventBus.OnDelayPlacementSelectionChangedTyped -= HandleDelayPlacementSelectionChanged;
                eventBus.OnDelayReturnWindowOpenedTyped -= HandleDelayReturnWindowOpened;
                eventBus.OnDelayReturnWindowClosedTyped -= HandleDelayReturnWindowClosed;
                eventBus.OnDelayedTurnEnteredTyped -= HandleDelayedTurnEntered;
                eventBus.OnDelayedTurnResumedTyped -= HandleDelayedTurnResumed;
                eventBus.OnDelayedTurnExpiredTyped -= HandleDelayedTurnExpired;
            }

            if (targetingController != null)
                targetingController.OnModeChanged -= HandleModeChanged;
        }

        private void EnsureButtonListenersBound()
        {
            if (buttonListenersBound) return;

            int boundCount = 0;
            boundCount += BindButton(strikeButton, OnStrikeClicked);
            boundCount += BindButton(tripButton, OnTripClicked);
            boundCount += BindButton(shoveButton, OnShoveClicked);
            boundCount += BindButton(grappleButton, OnGrappleClicked);
            boundCount += BindButton(repositionButton, OnRepositionClicked);
            boundCount += BindButton(demoralizeButton, OnDemoralizeClicked);
            boundCount += BindButton(escapeButton, OnEscapeClicked);
            boundCount += BindButton(raiseShieldButton, OnRaiseShieldClicked);
            boundCount += BindButton(standButton, OnStandClicked);
            boundCount += BindButton(delayButton, OnDelayClicked);
            boundCount += BindButton(returnNowButton, OnReturnNowClicked);
            boundCount += BindButton(skipDelayWindowButton, OnSkipDelayWindowClicked);

            if (boundCount > 0)
                buttonListenersBound = true;
        }

        private int BindButton(Button button, UnityEngine.Events.UnityAction handler)
        {
            if (button == null || handler == null) return 0;
            button.onClick.AddListener(handler);
            return 1;
        }

        private void HandleCombatStarted(in CombatStartedEvent e)
        {
            SetCombatVisible(true);
            RefreshAvailability();
            HandleModeChanged(targetingController != null ? targetingController.ActiveMode : TargetingMode.None);
        }

        private void HandleCombatEnded(in CombatEndedEvent e)
        {
            if (targetingController != null && targetingController.ActiveMode != TargetingMode.None)
                targetingController.CancelTargeting();

            SetCombatVisible(false);
            SetAllInteractable(false);
            SetDelayControlInteractable(false);
            SetDelayWindowControlsVisible(false);
            SetDelayReturnControlsInteractable(false, false);
            ClearAllHighlights();
        }

        private void HandleTurnStarted(in TurnStartedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleTurnEnded(in TurnEndedEvent e)
        {
            if (targetingController != null && targetingController.ActiveMode != TargetingMode.None)
                targetingController.CancelTargeting();

            SetAllInteractable(false);
            SetDelayControlInteractable(false);
            ClearAllHighlights();
        }

        private void HandleActionsChanged(in ActionsChangedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleConditionChanged(in ConditionChangedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleShieldRaised(in ShieldRaisedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleDelayTurnBeginTriggerChanged(in DelayTurnBeginTriggerChangedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleDelayPlacementSelectionChanged(in DelayPlacementSelectionChangedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleDelayReturnWindowOpened(in DelayReturnWindowOpenedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleDelayReturnWindowClosed(in DelayReturnWindowClosedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleDelayedTurnEntered(in DelayedTurnEnteredEvent e)
        {
            RefreshAvailability();
        }

        private void HandleDelayedTurnResumed(in DelayedTurnResumedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleDelayedTurnExpired(in DelayedTurnExpiredEvent e)
        {
            RefreshAvailability();
        }

        private void HandleModeChanged(TargetingMode mode)
        {
            SetHighlight(strikeHighlight, mode == TargetingMode.Strike);
            SetHighlight(tripHighlight, mode == TargetingMode.Trip);
            SetHighlight(shoveHighlight, mode == TargetingMode.Shove);
            SetHighlight(grappleHighlight, mode == TargetingMode.Grapple);
            SetHighlight(repositionHighlight, mode == TargetingMode.Reposition);
            SetHighlight(demoralizeHighlight, mode == TargetingMode.Demoralize);
            SetHighlight(escapeHighlight, mode == TargetingMode.Escape);
            SetHighlight(raiseShieldHighlight, false);
            SetHighlight(standHighlight, false);

            RefreshAvailability();
        }

        private void RefreshAvailability()
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null || actionExecutor == null)
            {
                SetAllInteractable(false);
                SetDelayControlInteractable(false);
                SetDelayWindowControlsVisible(false);
                SetDelayReturnControlsInteractable(false, false);
                return;
            }

            if (turnManager.IsDelayReturnWindowOpen)
            {
                SetAllInteractable(false);
                SetDelayControlInteractable(false);
                SetDelayWindowControlsVisible(true);

                bool canReturnNow = turnManager.TryGetFirstDelayedPlayerActor(out _);
                SetDelayReturnControlsInteractable(canReturnNow, true);
                return;
            }

            SetDelayWindowControlsVisible(false);
            SetDelayReturnControlsInteractable(false, false);

            if (turnManager.IsDelayPlacementSelectionOpen)
            {
                SetAllInteractable(false);
                // Keep Delay enabled as a cancel toggle while choosing initiative slot.
                SetDelayControlInteractable(true);
                return;
            }

            bool canAct = turnManager.IsPlayerTurn
                       && !actionExecutor.IsBusy
                       && turnManager.ActionsRemaining > 0;

            if (!canAct)
            {
                SetAllInteractable(false);
                SetDelayControlInteractable(false);
                return;
            }

            var actor = turnManager.CurrentEntity;
            var data = actor.IsValid ? entityManager.Registry.Get(actor) : null;
            if (data == null || !data.IsAlive)
            {
                SetAllInteractable(false);
                SetDelayControlInteractable(false);
                return;
            }

            SetInteractable(strikeButton, true);
            SetInteractable(tripButton, HasWeaponTrait(data, WeaponTraitFlags.Trip));
            SetInteractable(shoveButton, HasWeaponTrait(data, WeaponTraitFlags.Shove));
            SetInteractable(grappleButton, HasWeaponTrait(data, WeaponTraitFlags.Grapple));
            // Reposition can also be enabled via active grapple relation (RAW) which is not cheaply visible from EntityData.
            // Use broad pre-target gate; action/preview will enforce exact legality.
            SetInteractable(repositionButton, true);
            SetInteractable(demoralizeButton, true);
            SetInteractable(escapeButton, IsGrabbedOrRestrained(data));
            SetInteractable(raiseShieldButton, CanRaiseShield(data));
            SetInteractable(standButton, HasCondition(data, ConditionType.Prone));
            SetDelayControlInteractable(turnManager.CanDelayCurrentTurn());
        }

        private static bool HasWeaponTrait(EntityData data, WeaponTraitFlags trait)
        {
            if (data == null) return false;
            return (data.EquippedWeapon.Traits & trait) != 0;
        }

        private static bool IsGrabbedOrRestrained(EntityData data)
        {
            if (data == null) return false;
            return data.HasCondition(ConditionType.Grabbed) || data.HasCondition(ConditionType.Restrained);
        }

        private static bool CanRaiseShield(EntityData data)
        {
            if (data == null) return false;
            var shield = data.EquippedShield;
            if (!shield.IsEquipped) return false;
            if (shield.IsBroken) return false;
            if (shield.isRaised) return false;
            return true;
        }

        private static bool HasCondition(EntityData data, ConditionType type)
        {
            return data != null && data.HasCondition(type);
        }

        private void SetCombatVisible(bool visible)
        {
            if (canvasGroup == null) return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        private void SetAllInteractable(bool enabled)
        {
            SetInteractable(strikeButton, enabled);
            SetInteractable(tripButton, enabled);
            SetInteractable(shoveButton, enabled);
            SetInteractable(grappleButton, enabled);
            SetInteractable(repositionButton, enabled);
            SetInteractable(demoralizeButton, enabled);
            SetInteractable(escapeButton, enabled);
            SetInteractable(raiseShieldButton, enabled);
            SetInteractable(standButton, enabled);
        }

        private void SetDelayControlInteractable(bool enabled)
        {
            SetInteractable(delayButton, enabled);
        }

        private void SetDelayReturnControlsInteractable(bool canReturnNow, bool canSkip)
        {
            SetInteractable(returnNowButton, canReturnNow);
            SetInteractable(skipDelayWindowButton, canSkip);
        }

        private void SetDelayWindowControlsVisible(bool visible)
        {
            SetButtonVisible(returnNowButton, visible);
            SetButtonVisible(skipDelayWindowButton, visible);
        }

        private static void SetInteractable(Button button, bool enabled)
        {
            if (button != null) button.interactable = enabled;
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button == null) return;
            if (button.gameObject.activeSelf != visible)
                button.gameObject.SetActive(visible);
        }

        private void ClearAllHighlights()
        {
            SetHighlight(strikeHighlight, false);
            SetHighlight(tripHighlight, false);
            SetHighlight(shoveHighlight, false);
            SetHighlight(grappleHighlight, false);
            SetHighlight(repositionHighlight, false);
            SetHighlight(demoralizeHighlight, false);
            SetHighlight(escapeHighlight, false);
            SetHighlight(raiseShieldHighlight, false);
            SetHighlight(standHighlight, false);
        }

        private static void SetHighlight(Image image, bool active)
        {
            if (image == null) return;
            if (image.gameObject.activeSelf != active)
                image.gameObject.SetActive(active);
        }

        private void OnStrikeClicked()
        {
            ToggleOrBeginTargeting(TargetingMode.Strike, h => actionExecutor.TryExecuteStrike(h));
        }

        private void OnTripClicked()
        {
            ToggleOrBeginTargeting(TargetingMode.Trip, h => actionExecutor.TryExecuteTrip(h));
        }

        private void OnShoveClicked()
        {
            ToggleOrBeginTargeting(TargetingMode.Shove, h => actionExecutor.TryExecuteShove(h));
        }

        private void OnGrappleClicked()
        {
            ToggleOrBeginTargeting(TargetingMode.Grapple, h => actionExecutor.TryExecuteGrapple(h));
        }

        private void OnDemoralizeClicked()
        {
            ToggleOrBeginTargeting(TargetingMode.Demoralize, h => actionExecutor.TryExecuteDemoralize(h));
        }

        private void OnRepositionClicked()
        {
            ToggleOrBeginRepositionTargeting();
        }

        private void OnEscapeClicked()
        {
            ToggleOrBeginTargeting(TargetingMode.Escape, h => actionExecutor.TryExecuteEscape(h));
        }

        private void OnRaiseShieldClicked()
        {
            if (actionExecutor == null) return;
            actionExecutor.TryExecuteRaiseShield();
        }

        private void OnStandClicked()
        {
            if (actionExecutor == null) return;
            actionExecutor.TryExecuteStand();
        }

        private void OnDelayClicked()
        {
            if (turnManager == null) return;

            if (targetingController != null && targetingController.ActiveMode != TargetingMode.None)
                targetingController.CancelTargeting();

            if (turnManager.IsDelayPlacementSelectionOpen)
            {
                turnManager.CancelDelayPlacementSelection();
                RefreshAvailability();
                return;
            }

            if (turnManager.TryBeginDelayPlacementSelection())
                RefreshAvailability();
        }

        private void OnReturnNowClicked()
        {
            if (turnManager == null) return;
            if (!turnManager.TryGetFirstDelayedPlayerActor(out var actor)) return;

            if (turnManager.TryReturnDelayedActor(actor))
                RefreshAvailability();
        }

        private void OnSkipDelayWindowClicked()
        {
            if (turnManager == null) return;

            if (turnManager.IsDelayReturnWindowOpen)
            {
                turnManager.SkipDelayReturnWindow();
                RefreshAvailability();
            }
        }

        private void ToggleOrBeginTargeting(TargetingMode mode, System.Action<EntityHandle> onConfirm)
        {
            if (targetingController == null || actionExecutor == null) return;

            if (targetingController.ActiveMode == mode)
            {
                targetingController.CancelTargeting();
                return;
            }

            targetingController.BeginTargeting(mode, onConfirm);
        }

        private void ToggleOrBeginRepositionTargeting()
        {
            if (targetingController == null || actionExecutor == null) return;

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
