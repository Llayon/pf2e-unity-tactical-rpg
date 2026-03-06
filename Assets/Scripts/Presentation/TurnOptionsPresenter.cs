using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Initiative-anchored turn-options launcher.
    /// Normal mode: Ready (Move/Attack/Any) + Delay.
    /// Delay window mode: Return Now + Skip.
    /// </summary>
    public sealed class TurnOptionsPresenter : MonoBehaviour
    {
        private enum TurnOptionsMode : byte
        {
            None = 0,
            Normal = 1,
            DelayReturnWindow = 2,
        }

        [Header("Dependencies")]
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private PlayerActionExecutor actionExecutor;
        [SerializeField] private TargetingController targetingController;
        [SerializeField] private InitiativeBarController initiativeBarController;

        [Header("UI")]
        [SerializeField] private CanvasGroup launcherCanvasGroup;
        [SerializeField] private RectTransform launcherRoot;
        [SerializeField] private Button launcherButton;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Button readyMoveButton;
        [SerializeField] private Button readyAttackButton;
        [SerializeField] private Button readyAnyButton;
        [SerializeField] private Button delayButton;
        [SerializeField] private Button returnNowButton;
        [SerializeField] private Button skipButton;

        [Header("Layout")]
        [SerializeField] private Vector2 launcherOffset = new(-10f, -10f);
        [SerializeField] private Vector2 panelOffset = new(0f, 30f);

        [Header("Visual")]
        [SerializeField] private Color selectedReadyColor = new(0.95f, 0.78f, 0.18f, 0.95f);
        [SerializeField] private Color unselectedReadyColor = new(0.18f, 0.23f, 0.30f, 0.92f);
        [SerializeField] private Color buttonTextColor = new(0.92f, 0.92f, 0.95f, 1f);

        private bool inCombat;
        private bool panelOpen;

        private void Awake()
        {
            ResolveDependencies();
            EnsureUiFallback();
            SetLauncherVisible(false);
            SetPanelVisible(false);
        }

        private void OnEnable()
        {
            ResolveDependencies();
            EnsureUiFallback();

            if (eventBus == null || turnManager == null || entityManager == null || actionExecutor == null || initiativeBarController == null)
            {
                Debug.LogError("[TurnOptions] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnCombatStartedTyped += HandleCombatStarted;
            eventBus.OnCombatEndedTyped += HandleCombatEnded;
            eventBus.OnTurnStartedTyped += HandleTurnStarted;
            eventBus.OnTurnEndedTyped += HandleTurnEnded;
            eventBus.OnActionsChangedTyped += HandleActionsChanged;
            eventBus.OnConditionChangedTyped += HandleConditionChanged;
            eventBus.OnReadyTriggerModeChangedTyped += HandleReadyModeChanged;
            eventBus.OnDelayTurnBeginTriggerChangedTyped += HandleDelayStateChanged;
            eventBus.OnDelayPlacementSelectionChangedTyped += HandleDelayPlacementSelectionChanged;
            eventBus.OnDelayReturnWindowOpenedTyped += HandleDelayReturnWindowOpened;
            eventBus.OnDelayReturnWindowClosedTyped += HandleDelayReturnWindowClosed;
            eventBus.OnDelayedTurnEnteredTyped += HandleDelayedTurnChanged;
            eventBus.OnDelayedTurnResumedTyped += HandleDelayedTurnChanged;
            eventBus.OnDelayedTurnExpiredTyped += HandleDelayedTurnChanged;

            BindButtons();
            RefreshUiState();
        }

        private void OnDisable()
        {
            UnbindButtons();

            if (eventBus != null)
            {
                eventBus.OnCombatStartedTyped -= HandleCombatStarted;
                eventBus.OnCombatEndedTyped -= HandleCombatEnded;
                eventBus.OnTurnStartedTyped -= HandleTurnStarted;
                eventBus.OnTurnEndedTyped -= HandleTurnEnded;
                eventBus.OnActionsChangedTyped -= HandleActionsChanged;
                eventBus.OnConditionChangedTyped -= HandleConditionChanged;
                eventBus.OnReadyTriggerModeChangedTyped -= HandleReadyModeChanged;
                eventBus.OnDelayTurnBeginTriggerChangedTyped -= HandleDelayStateChanged;
                eventBus.OnDelayPlacementSelectionChangedTyped -= HandleDelayPlacementSelectionChanged;
                eventBus.OnDelayReturnWindowOpenedTyped -= HandleDelayReturnWindowOpened;
                eventBus.OnDelayReturnWindowClosedTyped -= HandleDelayReturnWindowClosed;
                eventBus.OnDelayedTurnEnteredTyped -= HandleDelayedTurnChanged;
                eventBus.OnDelayedTurnResumedTyped -= HandleDelayedTurnChanged;
                eventBus.OnDelayedTurnExpiredTyped -= HandleDelayedTurnChanged;
            }

            panelOpen = false;
            SetPanelVisible(false);
            SetLauncherVisible(false);
        }

        private void Update()
        {
            if (!panelOpen)
                return;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                ClosePanel();
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 screen = mouse.position.ReadValue();
                bool overLauncher = RectTransformUtility.RectangleContainsScreenPoint(launcherRoot, screen, null);
                bool overPanel = RectTransformUtility.RectangleContainsScreenPoint(panelRoot, screen, null);
                if (!overLauncher && !overPanel)
                    ClosePanel();
            }
        }

        private void LateUpdate()
        {
            RepositionUi();
        }

        private void HandleCombatStarted(in CombatStartedEvent e)
        {
            _ = e;
            inCombat = true;
            RefreshUiState();
        }

        private void HandleCombatEnded(in CombatEndedEvent e)
        {
            _ = e;
            inCombat = false;
            ClosePanel();
            RefreshUiState();
        }

        private void HandleTurnStarted(in TurnStartedEvent e)
        {
            _ = e;
            RefreshUiState();
        }

        private void HandleTurnEnded(in TurnEndedEvent e)
        {
            _ = e;
            RefreshUiState();
        }

        private void HandleActionsChanged(in ActionsChangedEvent e)
        {
            _ = e;
            RefreshUiState();
        }

        private void HandleConditionChanged(in ConditionChangedEvent e)
        {
            _ = e;
            RefreshUiState();
        }

        private void HandleReadyModeChanged(in ReadyTriggerModeChangedEvent e)
        {
            _ = e;
            RefreshUiState();
        }

        private void HandleDelayStateChanged(in DelayTurnBeginTriggerChangedEvent e)
        {
            _ = e;
            RefreshUiState();
        }

        private void HandleDelayPlacementSelectionChanged(in DelayPlacementSelectionChangedEvent e)
        {
            _ = e;
            RefreshUiState();
        }

        private void HandleDelayReturnWindowOpened(in DelayReturnWindowOpenedEvent e)
        {
            _ = e;
            RefreshUiState();
        }

        private void HandleDelayReturnWindowClosed(in DelayReturnWindowClosedEvent e)
        {
            _ = e;
            RefreshUiState();
        }

        private void HandleDelayedTurnChanged(in DelayedTurnEnteredEvent e)
        {
            _ = e;
            RefreshUiState();
        }

        private void HandleDelayedTurnChanged(in DelayedTurnResumedEvent e)
        {
            _ = e;
            RefreshUiState();
        }

        private void HandleDelayedTurnChanged(in DelayedTurnExpiredEvent e)
        {
            _ = e;
            RefreshUiState();
        }

        private void BindButtons()
        {
            if (launcherButton != null)
                launcherButton.onClick.AddListener(TogglePanel);
            if (readyMoveButton != null)
                readyMoveButton.onClick.AddListener(HandleReadyMoveClicked);
            if (readyAttackButton != null)
                readyAttackButton.onClick.AddListener(HandleReadyAttackClicked);
            if (readyAnyButton != null)
                readyAnyButton.onClick.AddListener(HandleReadyAnyClicked);
            if (delayButton != null)
                delayButton.onClick.AddListener(HandleDelayClicked);
            if (returnNowButton != null)
                returnNowButton.onClick.AddListener(HandleReturnNowClicked);
            if (skipButton != null)
                skipButton.onClick.AddListener(HandleSkipClicked);
        }

        private void UnbindButtons()
        {
            if (launcherButton != null)
                launcherButton.onClick.RemoveListener(TogglePanel);
            if (readyMoveButton != null)
                readyMoveButton.onClick.RemoveListener(HandleReadyMoveClicked);
            if (readyAttackButton != null)
                readyAttackButton.onClick.RemoveListener(HandleReadyAttackClicked);
            if (readyAnyButton != null)
                readyAnyButton.onClick.RemoveListener(HandleReadyAnyClicked);
            if (delayButton != null)
                delayButton.onClick.RemoveListener(HandleDelayClicked);
            if (returnNowButton != null)
                returnNowButton.onClick.RemoveListener(HandleReturnNowClicked);
            if (skipButton != null)
                skipButton.onClick.RemoveListener(HandleSkipClicked);
        }

        private void TogglePanel()
        {
            panelOpen = !panelOpen;
            SetPanelVisible(panelOpen);
            RefreshUiState();
        }

        private void ClosePanel()
        {
            panelOpen = false;
            SetPanelVisible(false);
        }

        private void HandleReadyMoveClicked() => ExecuteReady(ReadyTriggerMode.Movement);
        private void HandleReadyAttackClicked() => ExecuteReady(ReadyTriggerMode.Attack);
        private void HandleReadyAnyClicked() => ExecuteReady(ReadyTriggerMode.Any);

        private void ExecuteReady(ReadyTriggerMode mode)
        {
            if (turnManager == null || actionExecutor == null)
                return;

            turnManager.SetReadyTriggerMode(mode);
            actionExecutor.TryExecuteReadyStrike();
            ClosePanel();
            RefreshUiState();
        }

        private void HandleDelayClicked()
        {
            if (turnManager == null)
                return;

            if (targetingController != null && targetingController.ActiveMode != TargetingMode.None)
                targetingController.CancelTargeting();

            if (turnManager.IsDelayPlacementSelectionOpen)
                turnManager.CancelDelayPlacementSelection();
            else
                turnManager.TryBeginDelayPlacementSelection();

            ClosePanel();
            RefreshUiState();
        }

        private void HandleReturnNowClicked()
        {
            if (turnManager == null)
                return;

            if (turnManager.TryGetFirstDelayedPlayerActor(out var actor))
                turnManager.TryReturnDelayedActor(actor);

            ClosePanel();
            RefreshUiState();
        }

        private void HandleSkipClicked()
        {
            if (turnManager != null && turnManager.IsDelayReturnWindowOpen)
                turnManager.SkipDelayReturnWindow();

            ClosePanel();
            RefreshUiState();
        }

        private void RefreshUiState()
        {
            var mode = ResolveMode(out var actor);
            bool showLauncher = mode != TurnOptionsMode.None;

            SetLauncherVisible(showLauncher);
            if (!showLauncher)
            {
                ClosePanel();
                return;
            }

            if (panelOpen)
                SetPanelVisible(true);

            if (mode == TurnOptionsMode.Normal)
                ConfigureNormalMode(actor);
            else
                ConfigureDelayReturnMode();

            RepositionUi();
        }

        private TurnOptionsMode ResolveMode(out EntityHandle actor)
        {
            actor = EntityHandle.None;
            if (!inCombat || turnManager == null || entityManager == null || entityManager.Registry == null)
                return TurnOptionsMode.None;

            if (turnManager.IsDelayReturnWindowOpen)
                return TurnOptionsMode.DelayReturnWindow;

            if (!turnManager.IsPlayerTurn)
                return TurnOptionsMode.None;

            actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return TurnOptionsMode.None;

            var data = entityManager.Registry.Get(actor);
            if (data == null || !data.IsAlive || data.Team != Team.Player)
                return TurnOptionsMode.None;

            return TurnOptionsMode.Normal;
        }

        private void ConfigureNormalMode(EntityHandle actor)
        {
            SetButtonVisible(readyMoveButton, true);
            SetButtonVisible(readyAttackButton, true);
            SetButtonVisible(readyAnyButton, true);
            SetButtonVisible(delayButton, true);
            SetButtonVisible(returnNowButton, false);
            SetButtonVisible(skipButton, false);

            bool canPrepareReady =
                actor.IsValid
                && turnManager.IsPlayerTurn
                && !actionExecutor.IsBusy
                && !turnManager.IsDelayPlacementSelectionOpen
                && !turnManager.IsDelayReturnWindowOpen
                && !turnManager.HasReadiedStrike(actor)
                && turnManager.ActionsRemaining >= ReadyStrikeAction.ActionCost;

            SetInteractable(readyMoveButton, canPrepareReady);
            SetInteractable(readyAttackButton, canPrepareReady);
            SetInteractable(readyAnyButton, canPrepareReady);

            bool canDelay = turnManager.IsDelayPlacementSelectionOpen || turnManager.CanDelayCurrentTurn();
            SetInteractable(delayButton, canDelay);
            SetButtonText(delayButton, turnManager.IsDelayPlacementSelectionOpen ? "Cancel Delay [—]" : "Delay [—]");

            var currentMode = turnManager.CurrentReadyTriggerMode;
            ApplyReadyModeVisual(readyMoveButton, currentMode == ReadyTriggerMode.Movement);
            ApplyReadyModeVisual(readyAttackButton, currentMode == ReadyTriggerMode.Attack);
            ApplyReadyModeVisual(readyAnyButton, currentMode == ReadyTriggerMode.Any);

            SetButtonText(readyMoveButton, "Ready: Move [2]");
            SetButtonText(readyAttackButton, "Ready: Attack [2]");
            SetButtonText(readyAnyButton, "Ready: Any [2]");
        }

        private void ConfigureDelayReturnMode()
        {
            SetButtonVisible(readyMoveButton, false);
            SetButtonVisible(readyAttackButton, false);
            SetButtonVisible(readyAnyButton, false);
            SetButtonVisible(delayButton, false);
            SetButtonVisible(returnNowButton, true);
            SetButtonVisible(skipButton, true);

            bool canReturnNow = turnManager.TryGetFirstDelayedPlayerActor(out _);
            SetInteractable(returnNowButton, canReturnNow);
            SetInteractable(skipButton, true);

            SetButtonText(returnNowButton, "Return Now [R]");
            SetButtonText(skipButton, "Skip [—]");
        }

        private void RepositionUi()
        {
            if (launcherRoot == null)
                return;
            if (!launcherRoot.gameObject.activeInHierarchy)
                return;
            if (!initiativeBarController.TryGetTurnOptionsAnchorRect(out var anchorRect) || anchorRect == null)
                return;

            var corners = new Vector3[4];
            anchorRect.GetWorldCorners(corners);
            Vector3 topRight = corners[2];

            launcherRoot.position = topRight + new Vector3(launcherOffset.x, launcherOffset.y, 0f);
            if (panelRoot != null)
                panelRoot.position = launcherRoot.position + new Vector3(panelOffset.x, panelOffset.y, 0f);
        }

        private void ResolveDependencies()
        {
            if (eventBus == null)
                eventBus = FindFirstObjectByType<CombatEventBus>();
            if (turnManager == null)
                turnManager = FindFirstObjectByType<TurnManager>();
            if (entityManager == null)
                entityManager = FindFirstObjectByType<EntityManager>();
            if (actionExecutor == null)
                actionExecutor = FindFirstObjectByType<PlayerActionExecutor>();
            if (targetingController == null)
                targetingController = FindFirstObjectByType<TargetingController>();
            if (initiativeBarController == null)
                initiativeBarController = FindFirstObjectByType<InitiativeBarController>();
        }

        private void EnsureUiFallback()
        {
            if (launcherCanvasGroup == null)
            {
                launcherCanvasGroup = GetComponent<CanvasGroup>();
                if (launcherCanvasGroup == null)
                    launcherCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (launcherRoot == null || launcherButton == null)
            {
                var launcher = transform.Find("TurnOptionsLauncher") as RectTransform;
                if (launcher == null)
                    launcher = CreateLauncherRoot();

                launcherRoot = launcher;
                launcherButton = launcherRoot.GetComponent<Button>();
            }

            if (panelRoot == null)
            {
                var panel = transform.Find("TurnOptionsPanel") as RectTransform;
                if (panel == null)
                    panel = CreatePanelRoot();
                panelRoot = panel;
            }

            if (readyMoveButton == null)
                readyMoveButton = EnsureButton(panelRoot, "ReadyMoveButton");
            if (readyAttackButton == null)
                readyAttackButton = EnsureButton(panelRoot, "ReadyAttackButton");
            if (readyAnyButton == null)
                readyAnyButton = EnsureButton(panelRoot, "ReadyAnyButton");
            if (delayButton == null)
                delayButton = EnsureButton(panelRoot, "DelayButton");
            if (returnNowButton == null)
                returnNowButton = EnsureButton(panelRoot, "ReturnNowButton");
            if (skipButton == null)
                skipButton = EnsureButton(panelRoot, "SkipButton");
        }

        private RectTransform CreateLauncherRoot()
        {
            var go = new GameObject("TurnOptionsLauncher", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(20f, 20f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.18f, 0.23f, 0.30f, 0.96f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "...";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 15f;
            label.color = buttonTextColor;
            label.raycastTarget = false;
            label.enableWordWrapping = false;

            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return rect;
        }

        private RectTransform CreatePanelRoot()
        {
            var go = new GameObject(
                "TurnOptionsPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter));
            go.transform.SetParent(transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 28f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 4, 4);
            layout.spacing = 4f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rect;
        }

        private Button EnsureButton(RectTransform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                var existingButton = existing.GetComponent<Button>();
                if (existingButton != null)
                    return existingButton;
            }

            var go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(104f, 22f);

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredWidth = 104f;
            layout.preferredHeight = 22f;
            layout.minWidth = 90f;
            layout.minHeight = 22f;

            var image = go.GetComponent<Image>();
            image.color = unselectedReadyColor;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = name;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 12f;
            label.color = buttonTextColor;
            label.raycastTarget = false;
            label.enableWordWrapping = false;

            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button == null)
                return;
            if (button.gameObject.activeSelf != visible)
                button.gameObject.SetActive(visible);
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        private static void SetButtonText(Button button, string text)
        {
            if (button == null)
                return;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = text;
        }

        private void ApplyReadyModeVisual(Button button, bool selected)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? selectedReadyColor : unselectedReadyColor;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.color = selected ? Color.black : buttonTextColor;
        }

        private void SetLauncherVisible(bool visible)
        {
            if (launcherRoot != null && launcherRoot.gameObject.activeSelf != visible)
                launcherRoot.gameObject.SetActive(visible);

            if (launcherCanvasGroup == null)
                return;

            launcherCanvasGroup.alpha = visible ? 1f : 0f;
            launcherCanvasGroup.blocksRaycasts = visible;
            launcherCanvasGroup.interactable = visible;
        }

        private void SetPanelVisible(bool visible)
        {
            if (panelRoot != null && panelRoot.gameObject.activeSelf != visible)
                panelRoot.gameObject.SetActive(visible);
        }

        /// <summary>
        /// External refresh hook for Delay UI orchestration.
        /// Used by DelayUiOrchestrator to synchronize turn-management UI after delay-state events.
        /// </summary>
        public void RefreshFromDelayUiOrchestrator()
        {
            RefreshUiState();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (eventBus == null) Debug.LogWarning("[TurnOptions] CombatEventBus not assigned.", this);
            if (turnManager == null) Debug.LogWarning("[TurnOptions] TurnManager not assigned.", this);
            if (entityManager == null) Debug.LogWarning("[TurnOptions] EntityManager not assigned.", this);
            if (actionExecutor == null) Debug.LogWarning("[TurnOptions] PlayerActionExecutor not assigned.", this);
            if (initiativeBarController == null) Debug.LogWarning("[TurnOptions] InitiativeBarController not assigned.", this);
        }
#endif
    }
}
