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

        private bool buttonListenersBound;
        private bool delayEventsSubscribedInternally;
        private readonly ActionBarAvailabilityPolicy actionBarAvailabilityPolicy = new();
        private readonly AidPreparedIndicatorPresenter aidPreparedIndicatorPresenter = new();
        private readonly DelayActionBarStatePresenter delayActionBarStatePresenter = new();
        private readonly ActionBarCommandCoordinator actionBarCommandCoordinator = new();
        private readonly AidActionBarUiBootstrapper aidActionBarUiBootstrapper = new();
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
            // aid button is optional in older scenes; no warning spam.
            if (raiseShieldButton == null) Debug.LogWarning("[ActionBar] raiseShieldButton not assigned", this);
            if (standButton == null) Debug.LogWarning("[ActionBar] standButton not assigned", this);
            // delay/return/skip buttons are optional in older scenes; no warning spam.
        }
#endif

        private void Awake()
        {
            ResolveOptionalAidUiReferences();
            EnsureButtonListenersBound();

            SetCombatVisible(false);
            SetAllInteractable(false);
            ApplyDelayControls(delayActionBarStatePresenter.BuildInactiveState());
            RefreshReadyButtonLabel();
            ClearAllHighlights();
        }

        private void ResolveOptionalAidUiReferences()
        {
            aidActionBarUiBootstrapper.ResolveOptionalReferences(
                this,
                escapeButton,
                demoralizeButton,
                strikeButton,
                ref aidButton,
                ref aidHighlight,
                ref aidPreparedIndicatorRoot,
                ref aidPreparedIndicatorLabel,
                aidPreparedIndicatorFillColor,
                aidPreparedIndicatorLabelColor);

            if (readyButton == null)
            {
                var ready = transform.Find("ReadyButton");
                if (ready != null)
                    readyButton = ready.GetComponent<Button>();

                if (readyButton == null)
                {
                    var readyButtons = GetComponentsInChildren<Button>(true);
                    for (int i = 0; i < readyButtons.Length; i++)
                    {
                        var button = readyButtons[i];
                        if (button != null && string.Equals(button.name, "ReadyButton", System.StringComparison.Ordinal))
                        {
                            readyButton = button;
                            break;
                        }
                    }
                }
            }

            if (readyHighlight == null && readyButton != null)
            {
                var highlight = readyButton.transform.Find("ActiveHighlight");
                if (highlight != null)
                    readyHighlight = highlight.GetComponent<Image>();
            }

            if (readyButtonLabel == null && readyButton != null)
                readyButtonLabel = readyButton.GetComponentInChildren<TMP_Text>(true);

            aidPreparedIndicatorPresenter.Clear();
            RefreshAidPreparedIndicator();
        }

        private void ResolveAidPreparedIndicatorReferences()
        {
            aidActionBarUiBootstrapper.ResolveAidPreparedIndicatorReferences(
                aidButton,
                ref aidPreparedIndicatorRoot,
                ref aidPreparedIndicatorLabel,
                aidPreparedIndicatorFillColor,
                aidPreparedIndicatorLabelColor);
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
            boundCount += BindButton(raiseShieldButton, actionBarCommandCoordinator.OnRaiseShieldClicked);
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
            RefreshReadyButtonLabel();
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
                RefreshAidPreparedIndicator();
                return;
            }

            if (turnManager.IsDelayReturnWindowOpen)
            {
                SetAllInteractable(false);

                bool canReturnNow = turnManager.TryGetFirstDelayedPlayerActor(out _);
                ApplyDelayControls(delayActionBarStatePresenter.BuildReturnWindowState(canReturnNow));
                RefreshReadyButtonLabel();
                RefreshAidPreparedIndicator();
                return;
            }

            if (turnManager.IsDelayPlacementSelectionOpen)
            {
                SetAllInteractable(false);
                ApplyDelayControls(delayActionBarStatePresenter.BuildPlacementSelectionState());
                RefreshReadyButtonLabel();
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
                RefreshReadyButtonLabel();
                RefreshAidPreparedIndicator();
                return;
            }

            ApplyActionAvailability(in availability);
            ApplyDelayControls(delayActionBarStatePresenter.BuildNormalState(turnManager.CanDelayCurrentTurn()));
            RefreshReadyButtonLabel();
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
            SetInteractable(raiseShieldButton, enabled);
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

        private static bool IsExternalDelayOrchestratorPresent()
        {
            var orchestrator = UnityEngine.Object.FindFirstObjectByType<DelayUiOrchestrator>();
            return orchestrator != null && orchestrator.isActiveAndEnabled;
        }
    }
}
