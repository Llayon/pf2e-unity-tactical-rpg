using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Minimal combat HUD (UGUI + TMP).
    /// Bus-driven: subscribes to CombatEventBus typed events only.
    /// No direct TurnManager dependency.
    /// Inspector-only wiring (no Find*).
    /// </summary>
    public class TurnUIController : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private TurnInputController turnInputController;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI roundText;
        [SerializeField] private TextMeshProUGUI actorText;
        [SerializeField] private TextMeshProUGUI actionsText;
        [SerializeField] private Button endTurnButton;

        [Header("Visibility")]
        [SerializeField] private bool hideWhenNotInCombat = true;
        [SerializeField] private CanvasGroup canvasGroup;

        private static readonly string[] Pips = { "○○○", "●○○", "●●○", "●●●" };

        // Local state (driven by typed events)
        private bool inCombat;
        private int currentRound;
        private EntityHandle currentActor;
        private int actionsRemaining;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null) Debug.LogError("[TurnUI] Missing reference: CombatEventBus", this);
            if (entityManager == null) Debug.LogError("[TurnUI] Missing reference: EntityManager", this);
            if (turnInputController == null) Debug.LogError("[TurnUI] Missing reference: TurnInputController", this);

            if (roundText == null) Debug.LogWarning("[TurnUI] roundText not assigned", this);
            if (actorText == null) Debug.LogWarning("[TurnUI] actorText not assigned", this);
            if (actionsText == null) Debug.LogWarning("[TurnUI] actionsText not assigned", this);
            if (endTurnButton == null) Debug.LogWarning("[TurnUI] endTurnButton not assigned", this);
        }
#endif

        private void OnEnable()
        {
            if (eventBus == null || entityManager == null || turnInputController == null)
            {
                Debug.LogError("[TurnUI] Missing core dependencies (eventBus/entityManager/turnInputController). Disabling.", this);
                enabled = false;
                return;
            }

            if (hideWhenNotInCombat && canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    Debug.LogWarning("[TurnUI] hideWhenNotInCombat is enabled but CanvasGroup is not assigned. UI will remain visible.", this);
            }

            if (endTurnButton != null)
                endTurnButton.onClick.AddListener(OnEndTurnClicked);

            eventBus.OnCombatStartedTyped += HandleCombatStarted;
            eventBus.OnCombatEndedTyped += HandleCombatEnded;
            eventBus.OnRoundStartedTyped += HandleRoundStarted;
            eventBus.OnTurnStartedTyped += HandleTurnStarted;
            eventBus.OnTurnEndedTyped += HandleTurnEnded;
            eventBus.OnActionsChangedTyped += HandleActionsChanged;

            RefreshAll();
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                eventBus.OnCombatStartedTyped -= HandleCombatStarted;
                eventBus.OnCombatEndedTyped -= HandleCombatEnded;
                eventBus.OnRoundStartedTyped -= HandleRoundStarted;
                eventBus.OnTurnStartedTyped -= HandleTurnStarted;
                eventBus.OnTurnEndedTyped -= HandleTurnEnded;
                eventBus.OnActionsChangedTyped -= HandleActionsChanged;
            }

            if (endTurnButton != null)
                endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        }

        // ─── Typed event handlers ────────────────────────────────────────

        private void HandleCombatStarted(in CombatStartedEvent e)
        {
            inCombat = true;
            RefreshVisibility();
            RefreshAll();
        }

        private void HandleCombatEnded(in CombatEndedEvent e)
        {
            inCombat = false;
            currentActor = default;
            actionsRemaining = 0;
            RefreshVisibility();
            RefreshAll();
        }

        private void HandleRoundStarted(in RoundStartedEvent e)
        {
            currentRound = e.round;
            RefreshRound();
        }

        private void HandleTurnStarted(in TurnStartedEvent e)
        {
            currentActor = e.actor;
            actionsRemaining = e.actionsAtStart;
            RefreshActor();
            RefreshActions();
            RefreshEndTurnButton();
        }

        private void HandleTurnEnded(in TurnEndedEvent e)
        {
            currentActor = default;
            RefreshActor();
            RefreshActions();
            RefreshEndTurnButton();
        }

        private void HandleActionsChanged(in ActionsChangedEvent e)
        {
            actionsRemaining = e.remaining;
            RefreshActions();
            RefreshEndTurnButton();
        }

        private void OnEndTurnClicked()
        {
            if (turnInputController == null) return;
            turnInputController.RequestEndTurn();
        }

        // ─── Refresh API ────────────────────────────────────────────────

        private void RefreshAll()
        {
            RefreshVisibility();
            RefreshRound();
            RefreshActor();
            RefreshActions();
            RefreshEndTurnButton();
        }

        private void RefreshVisibility()
        {
            if (!hideWhenNotInCombat || canvasGroup == null)
                return;

            canvasGroup.alpha = inCombat ? 1f : 0f;
            canvasGroup.blocksRaycasts = inCombat;
            canvasGroup.interactable = inCombat;
        }

        private void RefreshRound()
        {
            if (roundText == null) return;
            roundText.text = inCombat ? $"Round: {currentRound}" : "Round: -";
        }

        private void RefreshActor()
        {
            if (actorText == null || entityManager == null || entityManager.Registry == null)
                return;

            if (!inCombat || !currentActor.IsValid)
            {
                actorText.text = "Actor: -";
                return;
            }

            var data = entityManager.Registry.Get(currentActor);
            string name = data?.Name ?? currentActor.ToString();
            string team = data != null ? data.Team.ToString() : "-";
            actorText.text = $"Actor: {name} ({team})";
        }

        private void RefreshActions()
        {
            if (actionsText == null) return;

            if (!inCombat)
            {
                actionsText.text = "Actions: ---";
                return;
            }

            int r = Mathf.Clamp(actionsRemaining, 0, 3);
            actionsText.text = $"Actions: {Pips[r]}";
        }

        private void RefreshEndTurnButton()
        {
            if (endTurnButton == null) return;
            bool can = (turnInputController != null) && turnInputController.CanEndTurn();
            endTurnButton.interactable = can;
        }
    }
}
