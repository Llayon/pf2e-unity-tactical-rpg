using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        [SerializeField] private Button aidButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonLabel;
        [SerializeField] private RectTransform readyModeSelectorRoot;
        [SerializeField] private Button readyModeMoveButton;
        [SerializeField] private Button readyModeAttackButton;
        [SerializeField] private Button readyModeAnyButton;
        [SerializeField] private Button raiseShieldButton;
        [SerializeField] private TMP_Text raiseShieldButtonLabel;
        [SerializeField] private RectTransform raiseShieldModeSelectorRoot;
        [SerializeField] private Button raiseShieldModeStandardButton;
        [SerializeField] private Button raiseShieldModeGlassButton;
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
        [SerializeField] private Image aidHighlight;
        [SerializeField] private Image readyHighlight;
        [SerializeField] private Image raiseShieldHighlight;
        [SerializeField] private Image standHighlight;

        [Header("Aid Prepared Indicator (optional)")]
        [SerializeField] private GameObject aidPreparedIndicatorRoot;
        [SerializeField] private TMP_Text aidPreparedIndicatorLabel;
        [SerializeField] private Color aidPreparedIndicatorFillColor = new Color(0.98f, 0.82f, 0.22f, 0.95f);
        [SerializeField] private Color aidPreparedIndicatorLabelColor = Color.black;
        [SerializeField] private string aidPreparedSingleText = string.Empty;
        [SerializeField] private string aidPreparedCountFormat = "{0}";
        [SerializeField] private Color readyModeSelectedColor = new Color(0.95f, 0.78f, 0.18f, 0.95f);
        [SerializeField] private Color readyModeUnselectedColor = new Color(0.18f, 0.23f, 0.30f, 0.92f);
        [SerializeField] private Color readyModeTextColor = new Color(0.92f, 0.92f, 0.95f, 1f);
        [SerializeField] private Color raiseShieldModeSelectedColor = new Color(0.95f, 0.78f, 0.18f, 0.95f);
        [SerializeField] private Color raiseShieldModeUnselectedColor = new Color(0.18f, 0.23f, 0.30f, 0.92f);
        [SerializeField] private Color raiseShieldModeTextColor = new Color(0.92f, 0.92f, 0.95f, 1f);

        private bool buttonListenersBound;
        private bool delayEventsSubscribedInternally;
        private readonly ActionBarAvailabilityPolicy actionBarAvailabilityPolicy = new();
        private readonly AidPreparedIndicatorPresenter aidPreparedIndicatorPresenter = new();
        private readonly DelayActionBarStatePresenter delayActionBarStatePresenter = new();
        private readonly ActionBarCommandCoordinator actionBarCommandCoordinator = new();
        private bool aidUiWiringWarned;
        private bool readyUiWiringWarned;
        private bool readyModeWiringWarned;
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
            if (aidButton == null) Debug.LogWarning("[ActionBar] aidButton not assigned", this);
            if (aidHighlight == null) Debug.LogWarning("[ActionBar] aidHighlight not assigned", this);
            if (aidPreparedIndicatorRoot == null) Debug.LogWarning("[ActionBar] aidPreparedIndicatorRoot not assigned", this);
            if (aidPreparedIndicatorLabel == null) Debug.LogWarning("[ActionBar] aidPreparedIndicatorLabel not assigned", this);
            if (readyButton == null) Debug.LogWarning("[ActionBar] readyButton not assigned", this);
            if (readyButtonLabel == null) Debug.LogWarning("[ActionBar] readyButtonLabel not assigned", this);
            if (readyHighlight == null) Debug.LogWarning("[ActionBar] readyHighlight not assigned", this);
            if (readyModeSelectorRoot == null) Debug.LogWarning("[ActionBar] readyModeSelectorRoot not assigned", this);
            if (readyModeMoveButton == null) Debug.LogWarning("[ActionBar] readyModeMoveButton not assigned", this);
            if (readyModeAttackButton == null) Debug.LogWarning("[ActionBar] readyModeAttackButton not assigned", this);
            if (readyModeAnyButton == null) Debug.LogWarning("[ActionBar] readyModeAnyButton not assigned", this);
            if (raiseShieldButton == null) Debug.LogWarning("[ActionBar] raiseShieldButton not assigned", this);
            if (raiseShieldButtonLabel == null) Debug.LogWarning("[ActionBar] raiseShieldButtonLabel not assigned", this);
            if (standButton == null) Debug.LogWarning("[ActionBar] standButton not assigned", this);
            // delay/return/skip buttons are optional in older scenes; no warning spam.
        }
#endif

        private void Awake()
        {
            ValidateAndApplyUiWiring();
            EnsureButtonListenersBound();

            SetCombatVisible(false);
            SetAllInteractable(false);
            ApplyDelayControls(delayActionBarStatePresenter.BuildInactiveState());
            SetReadyModeButtonsInteractable(false);
            SetRaiseShieldModeButtonsInteractable(false);
            RefreshReadyModeButtonsVisual();
            RefreshRaiseShieldModeButtonsVisual();
            RefreshReadyButtonLabel();
            RefreshRaiseShieldButtonLabel();
            ClearAllHighlights();
        }

        private void ValidateAndApplyUiWiring()
        {
            ValidateAidUiReferences();
            ApplyAidPreparedIndicatorStyle();
            ValidateReadyUiReferences();
            ResolveReadyModeSelectorReferences();
            ResolveRaiseShieldUiReferences();

            aidPreparedIndicatorPresenter.Clear();
            RefreshAidPreparedIndicator();
        }

        private void ValidateAidUiReferences()
        {
            if (aidButton != null && aidHighlight != null && aidPreparedIndicatorRoot != null && aidPreparedIndicatorLabel != null)
                return;

            if (aidUiWiringWarned)
                return;

            aidUiWiringWarned = true;
            Debug.LogWarning(
                "[ActionBar] Aid UI is not fully wired (aidButton/aidHighlight/aidPreparedIndicatorRoot/aidPreparedIndicatorLabel). " +
                "Assign references in scene or run scene validator autofix.",
                this);
        }

        private void ApplyAidPreparedIndicatorStyle()
        {
            if (aidPreparedIndicatorRoot != null)
            {
                var indicatorImage = aidPreparedIndicatorRoot.GetComponent<Image>();
                if (indicatorImage != null)
                    indicatorImage.color = aidPreparedIndicatorFillColor;
            }

            if (aidPreparedIndicatorLabel != null)
                aidPreparedIndicatorLabel.color = aidPreparedIndicatorLabelColor;
        }

        private void ValidateReadyUiReferences()
        {
            if (readyButton != null && readyButtonLabel != null && readyHighlight != null)
                return;

            if (readyUiWiringWarned)
                return;

            readyUiWiringWarned = true;
            Debug.LogWarning(
                "[ActionBar] Ready UI is not fully wired (readyButton/readyButtonLabel/readyHighlight). " +
                "Assign references in scene or run scene validator autofix.",
                this);
        }

        private void ResolveReadyModeSelectorReferences()
        {
            if (readyButton == null)
            {
                WarnMissingReadyModeWiring();
                return;
            }

            if (readyModeSelectorRoot == null
                || readyModeMoveButton == null
                || readyModeAttackButton == null
                || readyModeAnyButton == null)
                WarnMissingReadyModeWiring();
        }

        private void WarnMissingReadyModeWiring()
        {
            if (readyModeWiringWarned)
                return;

            readyModeWiringWarned = true;
            Debug.LogWarning(
                "[ActionBar] Ready mode selector is not fully wired (root/mode buttons missing). " +
                "Run scene validator autofix or assign references in scene.",
                this);
        }

        private void ResolveRaiseShieldUiReferences()
        {
            if (raiseShieldButton != null && raiseShieldButtonLabel == null)
                raiseShieldButtonLabel = raiseShieldButton.GetComponentInChildren<TMP_Text>(true);
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

            actionBarCommandCoordinator.Bind(turnManager, targetingController, actionExecutor, RefreshAvailability);
            SubscribeCoreEvents();
            SubscribeDelayEventsIfNeeded();

            targetingController.OnModeChanged += HandleModeChanged;

            HandleModeChanged(targetingController.ActiveMode);
            RebuildAidPreparedCountsFromService();
            RefreshAvailability();
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                UnsubscribeCoreEvents();
                UnsubscribeDelayEvents();
            }

            if (targetingController != null)
                targetingController.OnModeChanged -= HandleModeChanged;
        }

        private void EnsureButtonListenersBound()
        {
            if (buttonListenersBound) return;

            int boundCount = 0;
            boundCount += BindButton(strikeButton, actionBarCommandCoordinator.OnStrikeClicked);
            boundCount += BindButton(tripButton, actionBarCommandCoordinator.OnTripClicked);
            boundCount += BindButton(shoveButton, actionBarCommandCoordinator.OnShoveClicked);
            boundCount += BindButton(grappleButton, actionBarCommandCoordinator.OnGrappleClicked);
            boundCount += BindButton(repositionButton, actionBarCommandCoordinator.OnRepositionClicked);
            boundCount += BindButton(demoralizeButton, actionBarCommandCoordinator.OnDemoralizeClicked);
            boundCount += BindButton(escapeButton, actionBarCommandCoordinator.OnEscapeClicked);
            boundCount += BindButton(aidButton, actionBarCommandCoordinator.OnAidClicked);
            boundCount += BindButton(readyButton, actionBarCommandCoordinator.OnReadyClicked);
            boundCount += BindButton(readyModeMoveButton, actionBarCommandCoordinator.OnReadyModeMoveClicked);
            boundCount += BindButton(readyModeAttackButton, actionBarCommandCoordinator.OnReadyModeAttackClicked);
            boundCount += BindButton(readyModeAnyButton, actionBarCommandCoordinator.OnReadyModeAnyClicked);
            boundCount += BindButton(raiseShieldButton, actionBarCommandCoordinator.OnRaiseShieldClicked);
            boundCount += BindButton(raiseShieldModeStandardButton, actionBarCommandCoordinator.OnRaiseShieldModeStandardClicked);
            boundCount += BindButton(raiseShieldModeGlassButton, actionBarCommandCoordinator.OnRaiseShieldModeGlassClicked);
            boundCount += BindButton(standButton, actionBarCommandCoordinator.OnStandClicked);
            boundCount += BindButton(delayButton, actionBarCommandCoordinator.OnDelayClicked);
            boundCount += BindButton(returnNowButton, actionBarCommandCoordinator.OnReturnNowClicked);
            boundCount += BindButton(skipDelayWindowButton, actionBarCommandCoordinator.OnSkipDelayWindowClicked);

            if (boundCount > 0)
                buttonListenersBound = true;
        }

        private int BindButton(Button button, UnityEngine.Events.UnityAction handler)
        {
            if (button == null || handler == null) return 0;
            button.onClick.AddListener(handler);
            return 1;
        }

        private void SubscribeCoreEvents()
        {
            eventBus.OnCombatStartedTyped += HandleCombatStarted;
            eventBus.OnCombatEndedTyped += HandleCombatEnded;
            eventBus.OnTurnStartedTyped += HandleTurnStarted;
            eventBus.OnTurnEndedTyped += HandleTurnEnded;
            eventBus.OnActionsChangedTyped += HandleActionsChanged;
            eventBus.OnConditionChangedTyped += HandleConditionChanged;
            eventBus.OnShieldRaisedTyped += HandleShieldRaised;
            eventBus.OnAidPreparedTyped += HandleAidPrepared;
            eventBus.OnAidClearedTyped += HandleAidCleared;
            eventBus.OnReadyTriggerModeChangedTyped += HandleReadyTriggerModeChanged;
        }

        private void UnsubscribeCoreEvents()
        {
            eventBus.OnCombatStartedTyped -= HandleCombatStarted;
            eventBus.OnCombatEndedTyped -= HandleCombatEnded;
            eventBus.OnTurnStartedTyped -= HandleTurnStarted;
            eventBus.OnTurnEndedTyped -= HandleTurnEnded;
            eventBus.OnActionsChangedTyped -= HandleActionsChanged;
            eventBus.OnConditionChangedTyped -= HandleConditionChanged;
            eventBus.OnShieldRaisedTyped -= HandleShieldRaised;
            eventBus.OnAidPreparedTyped -= HandleAidPrepared;
            eventBus.OnAidClearedTyped -= HandleAidCleared;
            eventBus.OnReadyTriggerModeChangedTyped -= HandleReadyTriggerModeChanged;
        }

        private void SubscribeDelayEventsIfNeeded()
        {
            if (IsExternalDelayOrchestratorPresent())
            {
                delayEventsSubscribedInternally = false;
                return;
            }

            eventBus.OnDelayTurnBeginTriggerChangedTyped += HandleDelayTurnBeginTriggerChanged;
            eventBus.OnDelayPlacementSelectionChangedTyped += HandleDelayPlacementSelectionChanged;
            eventBus.OnDelayReturnWindowOpenedTyped += HandleDelayReturnWindowOpened;
            eventBus.OnDelayReturnWindowClosedTyped += HandleDelayReturnWindowClosed;
            eventBus.OnDelayedTurnEnteredTyped += HandleDelayedTurnEntered;
            eventBus.OnDelayedTurnResumedTyped += HandleDelayedTurnResumed;
            eventBus.OnDelayedTurnExpiredTyped += HandleDelayedTurnExpired;
            delayEventsSubscribedInternally = true;
        }

        private void UnsubscribeDelayEvents()
        {
            if (!delayEventsSubscribedInternally)
                return;

            eventBus.OnDelayTurnBeginTriggerChangedTyped -= HandleDelayTurnBeginTriggerChanged;
            eventBus.OnDelayPlacementSelectionChangedTyped -= HandleDelayPlacementSelectionChanged;
            eventBus.OnDelayReturnWindowOpenedTyped -= HandleDelayReturnWindowOpened;
            eventBus.OnDelayReturnWindowClosedTyped -= HandleDelayReturnWindowClosed;
            eventBus.OnDelayedTurnEnteredTyped -= HandleDelayedTurnEntered;
            eventBus.OnDelayedTurnResumedTyped -= HandleDelayedTurnResumed;
            eventBus.OnDelayedTurnExpiredTyped -= HandleDelayedTurnExpired;
            delayEventsSubscribedInternally = false;
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
            ApplyDelayControls(delayActionBarStatePresenter.BuildInactiveState());
            SetReadyModeButtonsInteractable(false);
            SetRaiseShieldModeButtonsInteractable(false);
            RefreshReadyModeButtonsVisual();
            RefreshRaiseShieldModeButtonsVisual();
            RefreshReadyButtonLabel();
            RefreshRaiseShieldButtonLabel();
            ClearAllHighlights();
            aidPreparedIndicatorPresenter.Clear();
            RefreshAidPreparedIndicator();
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
            ApplyDelayControls(delayActionBarStatePresenter.BuildInactiveState());
            SetReadyModeButtonsInteractable(false);
            RefreshReadyModeButtonsVisual();
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

        private void HandleAidPrepared(in AidPreparedEvent e)
        {
            aidPreparedIndicatorPresenter.HandleAidPrepared(in e);
            RefreshAvailability();
        }

        private void HandleAidCleared(in AidClearedEvent e)
        {
            aidPreparedIndicatorPresenter.HandleAidCleared(in e);
            RefreshAvailability();
        }

        private void HandleReadyTriggerModeChanged(in ReadyTriggerModeChangedEvent e)
        {
            _ = e;
            RefreshAvailability();
        }

        private void HandleDelayTurnBeginTriggerChanged(in DelayTurnBeginTriggerChangedEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        private void HandleDelayPlacementSelectionChanged(in DelayPlacementSelectionChangedEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        private void HandleDelayReturnWindowOpened(in DelayReturnWindowOpenedEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        private void HandleDelayReturnWindowClosed(in DelayReturnWindowClosedEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        private void HandleDelayedTurnEntered(in DelayedTurnEnteredEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        private void HandleDelayedTurnResumed(in DelayedTurnResumedEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        private void HandleDelayedTurnExpired(in DelayedTurnExpiredEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        public void RefreshDelayUiFromOrchestrator()
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
            SetHighlight(aidHighlight, mode == TargetingMode.Aid);
            SetHighlight(readyHighlight, mode == TargetingMode.ReadyStrike);
            SetHighlight(raiseShieldHighlight, false);
            SetHighlight(standHighlight, false);

            RefreshAvailability();
        }

        private void RefreshAvailability()
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null || actionExecutor == null)
            {
                SetAllInteractable(false);
                ApplyDelayControls(delayActionBarStatePresenter.BuildInactiveState());
                aidPreparedIndicatorPresenter.Clear();
                SetReadyModeButtonsInteractable(false);
                SetRaiseShieldModeButtonsInteractable(false);
                RefreshReadyModeButtonsVisual();
                RefreshRaiseShieldModeButtonsVisual();
                RefreshRaiseShieldButtonLabel();
                RefreshAidPreparedIndicator();
                return;
            }

            if (turnManager.IsDelayReturnWindowOpen)
            {
                SetAllInteractable(false);

                bool canReturnNow = turnManager.TryGetFirstDelayedPlayerActor(out _);
                ApplyDelayControls(delayActionBarStatePresenter.BuildReturnWindowState(canReturnNow));
                SetReadyModeButtonsInteractable(false);
                SetRaiseShieldModeButtonsInteractable(false);
                RefreshReadyModeButtonsVisual();
                RefreshRaiseShieldModeButtonsVisual();
                RefreshReadyButtonLabel();
                RefreshRaiseShieldButtonLabel();
                RefreshAidPreparedIndicator();
                return;
            }

            if (turnManager.IsDelayPlacementSelectionOpen)
            {
                SetAllInteractable(false);
                ApplyDelayControls(delayActionBarStatePresenter.BuildPlacementSelectionState());
                SetReadyModeButtonsInteractable(false);
                SetRaiseShieldModeButtonsInteractable(false);
                RefreshReadyModeButtonsVisual();
                RefreshRaiseShieldModeButtonsVisual();
                RefreshReadyButtonLabel();
                RefreshRaiseShieldButtonLabel();
                RefreshAidPreparedIndicator();
                return;
            }

            if (!actionBarAvailabilityPolicy.TryEvaluate(
                turnManager,
                actionExecutor,
                entityManager.Registry,
                out var availability))
            {
                SetAllInteractable(false);
                ApplyDelayControls(delayActionBarStatePresenter.BuildInactiveState());
                SetReadyModeButtonsInteractable(false);
                SetRaiseShieldModeButtonsInteractable(false);
                RefreshReadyModeButtonsVisual();
                RefreshRaiseShieldModeButtonsVisual();
                RefreshReadyButtonLabel();
                RefreshRaiseShieldButtonLabel();
                RefreshAidPreparedIndicator();
                return;
            }

            ApplyActionAvailability(in availability);
            ApplyDelayControls(delayActionBarStatePresenter.BuildNormalState(turnManager.CanDelayCurrentTurn()));

            var actor = turnManager.CurrentEntity;
            bool canAdjustReadyMode =
                actor.IsValid &&
                turnManager.IsPlayerTurn &&
                !actionExecutor.IsBusy &&
                !turnManager.IsDelayPlacementSelectionOpen &&
                !turnManager.IsDelayReturnWindowOpen &&
                !turnManager.HasReadiedStrike(actor);
            SetReadyModeButtonsInteractable(canAdjustReadyMode);
            RefreshReadyModeButtonsVisual();

            var actorData = entityManager.Registry.Get(actor);
            bool canAdjustShieldMode =
                actorData != null &&
                actorData.CanCastStandardShield &&
                actorData.CanCastGlassShield &&
                !actionExecutor.IsBusy &&
                turnManager.IsPlayerTurn &&
                !turnManager.IsDelayPlacementSelectionOpen &&
                !turnManager.IsDelayReturnWindowOpen;
            SetRaiseShieldModeButtonsInteractable(canAdjustShieldMode);
            RefreshRaiseShieldModeButtonsVisual();

            RefreshReadyButtonLabel();
            RefreshRaiseShieldButtonLabel();
            RefreshAidPreparedIndicator();
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
            SetInteractable(aidButton, enabled);
            SetInteractable(readyButton, enabled);
            SetInteractable(readyModeMoveButton, enabled);
            SetInteractable(readyModeAttackButton, enabled);
            SetInteractable(readyModeAnyButton, enabled);
            SetInteractable(raiseShieldButton, enabled);
            SetInteractable(raiseShieldModeStandardButton, enabled);
            SetInteractable(raiseShieldModeGlassButton, enabled);
            SetInteractable(standButton, enabled);
        }

        private void ApplyActionAvailability(in ActionBarAvailabilityState availability)
        {
            SetInteractable(strikeButton, availability.strikeInteractable);
            SetInteractable(tripButton, availability.tripInteractable);
            SetInteractable(shoveButton, availability.shoveInteractable);
            SetInteractable(grappleButton, availability.grappleInteractable);
            SetInteractable(repositionButton, availability.repositionInteractable);
            SetInteractable(demoralizeButton, availability.demoralizeInteractable);
            SetInteractable(escapeButton, availability.escapeInteractable);
            SetInteractable(aidButton, availability.aidInteractable);
            SetInteractable(readyButton, availability.readyInteractable);
            SetInteractable(raiseShieldButton, availability.raiseShieldInteractable);
            SetInteractable(standButton, availability.standInteractable);
        }

        private void ApplyDelayControls(in DelayActionBarState state)
        {
            delayActionBarStatePresenter.Apply(in state, delayButton, returnNowButton, skipDelayWindowButton);
        }

        private static void SetInteractable(Button button, bool enabled)
        {
            if (button != null) button.interactable = enabled;
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
            SetHighlight(aidHighlight, false);
            SetHighlight(readyHighlight, false);
            SetHighlight(raiseShieldHighlight, false);
            SetHighlight(standHighlight, false);
        }

        private static void SetHighlight(Image image, bool active)
        {
            if (image == null) return;
            if (image.gameObject.activeSelf != active)
                image.gameObject.SetActive(active);
        }

        private void RebuildAidPreparedCountsFromService()
        {
            aidPreparedIndicatorPresenter.RebuildFromService(turnManager != null ? turnManager.AidService : null);
        }

        private void RefreshAidPreparedIndicator()
        {
            var actor = turnManager != null ? turnManager.CurrentEntity : default;
            aidPreparedIndicatorPresenter.RefreshForActor(
                actor,
                aidPreparedIndicatorRoot,
                aidPreparedIndicatorLabel,
                aidPreparedSingleText,
                aidPreparedCountFormat);
        }

        private void RefreshReadyButtonLabel()
        {
            if (readyButtonLabel == null)
                return;

            var mode = turnManager != null ? turnManager.CurrentReadyTriggerMode : ReadyTriggerMode.Any;
            readyButtonLabel.text = $"Ready [{mode.ToShortToken()}]";
        }

        private void SetReadyModeButtonsInteractable(bool enabled)
        {
            if (readyModeSelectorRoot != null)
                readyModeSelectorRoot.gameObject.SetActive(readyButton != null && readyButton.gameObject.activeInHierarchy);

            SetInteractable(readyModeMoveButton, enabled);
            SetInteractable(readyModeAttackButton, enabled);
            SetInteractable(readyModeAnyButton, enabled);
        }

        private void SetRaiseShieldModeButtonsInteractable(bool enabled)
        {
            if (raiseShieldModeSelectorRoot != null)
                raiseShieldModeSelectorRoot.gameObject.SetActive(raiseShieldButton != null && raiseShieldButton.gameObject.activeInHierarchy);

            SetInteractable(raiseShieldModeStandardButton, enabled);
            SetInteractable(raiseShieldModeGlassButton, enabled);
        }

        private void RefreshReadyModeButtonsVisual()
        {
            var mode = turnManager != null ? turnManager.CurrentReadyTriggerMode : ReadyTriggerMode.Any;
            ApplyReadyModeButtonVisual(readyModeMoveButton, mode == ReadyTriggerMode.Movement);
            ApplyReadyModeButtonVisual(readyModeAttackButton, mode == ReadyTriggerMode.Attack);
            ApplyReadyModeButtonVisual(readyModeAnyButton, mode == ReadyTriggerMode.Any);
        }

        private void RefreshRaiseShieldModeButtonsVisual()
        {
            var mode = actionBarCommandCoordinator.CurrentRaiseShieldSpellMode;
            ApplyRaiseShieldModeButtonVisual(raiseShieldModeStandardButton, mode == RaiseShieldSpellMode.Standard);
            ApplyRaiseShieldModeButtonVisual(raiseShieldModeGlassButton, mode == RaiseShieldSpellMode.Glass);
        }

        private void ApplyReadyModeButtonVisual(Button button, bool selected)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? readyModeSelectedColor : readyModeUnselectedColor;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.color = selected ? Color.black : readyModeTextColor;
        }

        private void ApplyRaiseShieldModeButtonVisual(Button button, bool selected)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? raiseShieldModeSelectedColor : raiseShieldModeUnselectedColor;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.color = selected ? Color.black : raiseShieldModeTextColor;
        }

        private void RefreshRaiseShieldButtonLabel()
        {
            if (raiseShieldButtonLabel == null)
                return;

            string token = actionBarCommandCoordinator.CurrentRaiseShieldSpellMode.ToShortToken();
            raiseShieldButtonLabel.text = $"Shield [{token}]";
        }

        private static bool IsExternalDelayOrchestratorPresent()
        {
            var orchestrator = UnityEngine.Object.FindFirstObjectByType<DelayUiOrchestrator>();
            return orchestrator != null && orchestrator.isActiveAndEnabled;
        }
    }
}
