using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Bottom-right turn economy HUD: 3 action pips, 1 reaction pip, End Turn button.
    /// Event-driven: no Update polling.
    /// </summary>
    public sealed class TurnEconomyController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private TurnInputController turnInputController;

        [Header("UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image actionPip1;
        [SerializeField] private Image actionPip2;
        [SerializeField] private Image actionPip3;
        [SerializeField] private Image reactionPip;
        [SerializeField] private Button endTurnButton;

        [Header("Colors")]
        [SerializeField] private Color actionAvailableColor = new(0.96f, 0.82f, 0.28f, 1f);
        [SerializeField] private Color actionSpentColor = new(0.16f, 0.17f, 0.2f, 0.95f);
        [SerializeField] private Color reactionAvailableColor = new(0.32f, 0.86f, 0.48f, 1f);
        [SerializeField] private Color reactionSpentColor = new(0.16f, 0.17f, 0.2f, 0.95f);

        [Header("Behavior")]
        [SerializeField] private bool hideWhenNotInCombat = true;

        private bool inCombat;
        private EntityHandle currentActor;
        private int actionsRemaining;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (eventBus == null) Debug.LogWarning("[TurnEconomy] CombatEventBus not assigned.", this);
            if (entityManager == null) Debug.LogWarning("[TurnEconomy] EntityManager not assigned.", this);
            if (turnManager == null) Debug.LogWarning("[TurnEconomy] TurnManager not assigned.", this);
            if (turnInputController == null) Debug.LogWarning("[TurnEconomy] TurnInputController not assigned.", this);
            if (canvasGroup == null) Debug.LogWarning("[TurnEconomy] CanvasGroup not assigned.", this);
            if (actionPip1 == null || actionPip2 == null || actionPip3 == null)
                Debug.LogWarning("[TurnEconomy] Action pip images are not fully assigned.", this);
            if (reactionPip == null) Debug.LogWarning("[TurnEconomy] Reaction pip image not assigned.", this);
            if (endTurnButton == null) Debug.LogWarning("[TurnEconomy] End Turn button not assigned.", this);
        }
#endif

        private void Awake()
        {
            ResolveDependencies();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            EnsureUiFallback();
            HidePanel();
            RefreshVisuals();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            EnsureUiFallback();

            if (eventBus == null || entityManager == null || turnManager == null || turnInputController == null)
            {
                Debug.LogError("[TurnEconomy] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnCombatStartedTyped += HandleCombatStarted;
            eventBus.OnCombatEndedTyped += HandleCombatEnded;
            eventBus.OnTurnStartedTyped += HandleTurnStarted;
            eventBus.OnTurnEndedTyped += HandleTurnEnded;
            eventBus.OnActionsChangedTyped += HandleActionsChanged;

            // Reaction changes can happen outside action economy events.
            eventBus.OnShieldBlockResolvedTyped += HandleShieldBlockResolved;
            eventBus.OnAidResolvedTyped += HandleAidResolved;
            eventBus.OnStrikeResolved += HandleStrikeResolved;

            if (endTurnButton != null)
                endTurnButton.onClick.AddListener(OnEndTurnClicked);

            BootstrapFromTurnManager();
            RefreshVisuals();
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
                eventBus.OnShieldBlockResolvedTyped -= HandleShieldBlockResolved;
                eventBus.OnAidResolvedTyped -= HandleAidResolved;
                eventBus.OnStrikeResolved -= HandleStrikeResolved;
            }

            if (endTurnButton != null)
                endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        }

        private void ResolveDependencies()
        {
            if (eventBus == null)
                eventBus = FindFirstObjectByType<CombatEventBus>();
            if (entityManager == null)
                entityManager = FindFirstObjectByType<EntityManager>();
            if (turnManager == null)
                turnManager = FindFirstObjectByType<TurnManager>();
            if (turnInputController == null)
                turnInputController = FindFirstObjectByType<TurnInputController>();
        }

        private void EnsureUiFallback()
        {
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            var rootRect = GetComponent<RectTransform>();
            if (rootRect != null && rootRect.sizeDelta.sqrMagnitude < 1f)
                rootRect.sizeDelta = new Vector2(220f, 84f);

            var background = GetComponent<Image>();
            if (background == null)
                background = gameObject.AddComponent<Image>();
            background.raycastTarget = false;
            if (background.color.a <= 0.01f)
                background.color = new Color(0.08f, 0.09f, 0.12f, 0.92f);

            if (actionPip1 == null)
                actionPip1 = GetOrCreatePip("ActionPip1", 14f);
            if (actionPip2 == null)
                actionPip2 = GetOrCreatePip("ActionPip2", 36f);
            if (actionPip3 == null)
                actionPip3 = GetOrCreatePip("ActionPip3", 58f);
            if (reactionPip == null)
                reactionPip = GetOrCreatePip("ReactionPip", 86f);

            if (endTurnButton == null)
                endTurnButton = GetOrCreateEndTurnButton();
        }

        private Image GetOrCreatePip(string name, float x)
        {
            var existing = transform.Find(name);
            GameObject go;
            RectTransform rect;
            Image image;

            if (existing != null)
            {
                go = existing.gameObject;
                rect = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
                image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            }
            else
            {
                go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                rect = go.GetComponent<RectTransform>();
                image = go.GetComponent<Image>();
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -18f);
            rect.sizeDelta = new Vector2(16f, 16f);

            image.raycastTarget = false;
            image.color = actionSpentColor;
            return image;
        }

        private Button GetOrCreateEndTurnButton()
        {
            var existing = transform.Find("EndTurnButton");
            GameObject go;
            RectTransform rect;
            Image image;
            Button button;

            if (existing != null)
            {
                go = existing.gameObject;
                rect = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
                image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
                button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            }
            else
            {
                go = new GameObject("EndTurnButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(transform, false);
                rect = go.GetComponent<RectTransform>();
                image = go.GetComponent<Image>();
                button = go.GetComponent<Button>();
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(112f, -12f);
            rect.sizeDelta = new Vector2(96f, 30f);

            image.color = new Color(0.18f, 0.24f, 0.34f, 0.96f);

            var label = go.transform.Find("Label");
            TextMeshProUGUI tmp;
            if (label != null)
            {
                tmp = label.GetComponent<TextMeshProUGUI>() ?? label.gameObject.AddComponent<TextMeshProUGUI>();
            }
            else
            {
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(go.transform, false);
                tmp = labelGo.AddComponent<TextMeshProUGUI>();
            }

            var labelRect = tmp.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            tmp.text = "End Turn";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 20f;
            tmp.characterSpacing = 0.35f;
            tmp.color = new Color(0.93f, 0.95f, 0.99f, 1f);
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.enableKerning = true;

            return button;
        }

        private void BootstrapFromTurnManager()
        {
            if (turnManager == null)
                return;

            var state = turnManager.State;
            inCombat = state != TurnState.Inactive && state != TurnState.CombatOver;
            currentActor = turnManager.CurrentEntity;
            actionsRemaining = turnManager.ActionsRemaining;
        }

        private void HandleCombatStarted(in CombatStartedEvent e)
        {
            _ = e;
            inCombat = true;
            currentActor = turnManager != null ? turnManager.CurrentEntity : EntityHandle.None;
            actionsRemaining = turnManager != null ? turnManager.ActionsRemaining : 0;
            RefreshVisuals();
        }

        private void HandleCombatEnded(in CombatEndedEvent e)
        {
            _ = e;
            inCombat = false;
            currentActor = EntityHandle.None;
            actionsRemaining = 0;
            RefreshVisuals();
        }

        private void HandleTurnStarted(in TurnStartedEvent e)
        {
            currentActor = e.actor;
            actionsRemaining = e.actionsAtStart;
            RefreshVisuals();
        }

        private void HandleTurnEnded(in TurnEndedEvent e)
        {
            if (currentActor == e.actor)
            {
                currentActor = EntityHandle.None;
                actionsRemaining = 0;
            }

            RefreshVisuals();
        }

        private void HandleActionsChanged(in ActionsChangedEvent e)
        {
            if (!currentActor.IsValid || currentActor == e.actor)
            {
                currentActor = e.actor;
                actionsRemaining = e.remaining;
            }

            RefreshActionPips();
            RefreshEndTurnButton();
        }

        private void HandleShieldBlockResolved(in ShieldBlockResolvedEvent e)
        {
            if (currentActor == e.reactor)
                RefreshReactionPip();
        }

        private void HandleAidResolved(in AidResolvedEvent e)
        {
            if (e.reactionConsumed && currentActor == e.helper)
                RefreshReactionPip();
        }

        private void HandleStrikeResolved(in StrikeResolvedEvent e)
        {
            // Covers Ready-triggered strikes consuming reaction.
            if (currentActor == e.attacker)
                RefreshReactionPip();
        }

        private void OnEndTurnClicked()
        {
            if (turnInputController == null)
                return;

            turnInputController.RequestEndTurn();
            RefreshEndTurnButton();
        }

        private void RefreshVisuals()
        {
            ApplyVisibility();
            RefreshActionPips();
            RefreshReactionPip();
            RefreshEndTurnButton();
        }

        private void ApplyVisibility()
        {
            if (canvasGroup == null)
                return;

            bool visible = !hideWhenNotInCombat || inCombat;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        private void RefreshActionPips()
        {
            int remaining = inCombat ? Mathf.Clamp(actionsRemaining, 0, 3) : 0;
            SetPipState(actionPip1, remaining >= 1, actionAvailableColor, actionSpentColor);
            SetPipState(actionPip2, remaining >= 2, actionAvailableColor, actionSpentColor);
            SetPipState(actionPip3, remaining >= 3, actionAvailableColor, actionSpentColor);
        }

        private void RefreshReactionPip()
        {
            bool available = inCombat && ResolveCurrentActorReactionAvailable();
            SetPipState(reactionPip, available, reactionAvailableColor, reactionSpentColor);
        }

        private bool ResolveCurrentActorReactionAvailable()
        {
            if (!currentActor.IsValid || entityManager == null || entityManager.Registry == null)
                return false;

            var data = entityManager.Registry.Get(currentActor);
            return data != null && data.IsAlive && data.ReactionAvailable;
        }

        private void RefreshEndTurnButton()
        {
            if (endTurnButton == null)
                return;

            bool canEndTurn = inCombat
                && turnInputController != null
                && turnInputController.CanEndTurn();

            endTurnButton.interactable = canEndTurn;
        }

        private static void SetPipState(Image image, bool active, Color activeColor, Color inactiveColor)
        {
            if (image == null)
                return;

            image.color = active ? activeColor : inactiveColor;
        }

        private void HidePanel()
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}
