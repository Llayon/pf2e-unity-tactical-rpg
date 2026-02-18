using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Phase 9 Step 6: Minimal combat HUD (UGUI + TMP).
    /// Presentation-only: listens to TurnManager events, uses TurnInputController as command gateway.
    /// Inspector-only wiring (no Find*).
    /// </summary>
    public class TurnUIController : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private TurnManager turnManager;
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (turnManager == null) Debug.LogError("[TurnUI] Missing reference: TurnManager", this);
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
            // Fail-fast on core dependencies (inspector-only refs)
            if (turnManager == null || entityManager == null || turnInputController == null)
            {
                Debug.LogError("[TurnUI] Missing core dependencies (turnManager/entityManager/turnInputController). Disabling TurnUIController.", this);
                enabled = false;
                return;
            }

            if (hideWhenNotInCombat && canvasGroup == null)
            {
                // Allowed local fallback: same GameObject only
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    Debug.LogWarning("[TurnUI] hideWhenNotInCombat is enabled but CanvasGroup is not assigned. UI will remain visible.", this);
            }

            if (endTurnButton != null)
                endTurnButton.onClick.AddListener(OnEndTurnClicked);

            // Subscribe (named handlers only)
            turnManager.OnCombatStarted += HandleCombatStarted;
            turnManager.OnCombatEnded += HandleCombatEnded;
            turnManager.OnRoundStarted += HandleRoundStarted;
            turnManager.OnTurnStarted += HandleTurnStarted;
            turnManager.OnTurnEnded += HandleTurnEnded;
            turnManager.OnActionsChanged += HandleActionsChanged;

            RefreshAll();
        }

        private void OnDisable()
        {
            if (turnManager != null)
            {
                turnManager.OnCombatStarted -= HandleCombatStarted;
                turnManager.OnCombatEnded -= HandleCombatEnded;
                turnManager.OnRoundStarted -= HandleRoundStarted;
                turnManager.OnTurnStarted -= HandleTurnStarted;
                turnManager.OnTurnEnded -= HandleTurnEnded;
                turnManager.OnActionsChanged -= HandleActionsChanged;
            }

            if (endTurnButton != null)
                endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        }

        // ─── Event handlers ─────────────────────────────────────────────

        private void HandleCombatStarted()
        {
            RefreshVisibility();
            RefreshAll();
        }

        private void HandleCombatEnded()
        {
            RefreshVisibility();
            RefreshAll();
        }

        private void HandleRoundStarted(int _) => RefreshRound();

        private void HandleTurnStarted(EntityHandle _)
        {
            RefreshActor();
            RefreshActions();
            RefreshEndTurnButton();
        }

        private void HandleTurnEnded(EntityHandle _)
        {
            RefreshActor();
            RefreshActions();
            RefreshEndTurnButton();
        }

        private void HandleActionsChanged(int remaining)
        {
            RefreshActions(remaining);
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
            if (!hideWhenNotInCombat || canvasGroup == null || turnManager == null)
                return;

            bool inCombat = turnManager.State != TurnState.Inactive && turnManager.State != TurnState.CombatOver;
            canvasGroup.alpha = inCombat ? 1f : 0f;
            canvasGroup.blocksRaycasts = inCombat;
            canvasGroup.interactable = inCombat;
        }

        private void RefreshRound()
        {
            if (roundText == null || turnManager == null) return;

            roundText.text = (turnManager.State == TurnState.Inactive)
                ? "Round: -"
                : $"Round: {turnManager.RoundNumber}";
        }

        private void RefreshActor()
        {
            if (actorText == null || turnManager == null || entityManager == null || entityManager.Registry == null)
                return;

            if (turnManager.State == TurnState.Inactive)
            {
                actorText.text = "Actor: -";
                return;
            }

            EntityHandle h = turnManager.CurrentEntity;
            var data = h.IsValid ? entityManager.Registry.Get(h) : null;

            string name = data?.Name ?? (h.IsValid ? h.ToString() : "-");
            string team = data != null ? data.Team.ToString() : "-";
            actorText.text = $"Actor: {name} ({team})";
        }

        private void RefreshActions() => RefreshActions(turnManager != null ? turnManager.ActionsRemaining : 0);

        private void RefreshActions(int remaining)
        {
            if (actionsText == null || turnManager == null) return;

            if (turnManager.State == TurnState.Inactive)
            {
                actionsText.text = "Actions: ---";
                return;
            }

            int r = Mathf.Clamp(remaining, 0, 3);
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
