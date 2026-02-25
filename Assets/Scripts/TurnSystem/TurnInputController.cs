using UnityEngine;
using UnityEngine.InputSystem;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Thin router between input and TargetingController / TurnManager.
    /// - Space → EndTurn (PlayerTurn only, not while executor busy)
    /// - Escape → CancelTargeting
    /// - T / H / J / K / Y / V → Begin explicit targeting for Trip / Shove / Grapple / Escape / Demoralize / Reposition
    /// - Cell click → targetingController.TryConfirmCell (after guards)
    /// - Entity click → targetingController.TryConfirmEntity (after guards)
    ///
    /// All target-selection logic lives in TargetingController, not here.
    /// Subscriptions: OnEnable/OnDisable for OnCellClicked + OnEntityClicked.
    /// Inspector-only wiring.
    /// </summary>
    public class TurnInputController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private PlayerActionExecutor actionExecutor;
        [SerializeField] private TargetingController targetingController;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (turnManager         == null) Debug.LogError("[TurnInput] Missing TurnManager", this);
            if (gridManager         == null) Debug.LogError("[TurnInput] Missing GridManager", this);
            if (actionExecutor      == null) Debug.LogError("[TurnInput] Missing PlayerActionExecutor", this);
            if (targetingController == null) Debug.LogError("[TurnInput] Missing TargetingController", this);
        }
#endif

        private void OnEnable()
        {
            if (gridManager != null)
            {
                gridManager.OnCellClicked   += HandleCellClicked;
                gridManager.OnEntityClicked += HandleEntityClicked;
            }
        }

        private void OnDisable()
        {
            if (gridManager != null)
            {
                gridManager.OnCellClicked   -= HandleCellClicked;
                gridManager.OnEntityClicked -= HandleEntityClicked;
            }
        }

        public bool CanEndTurn()
        {
            if (turnManager == null || actionExecutor == null) return false;
            if (!turnManager.IsPlayerTurn) return false;
            if (turnManager.State == TurnState.ExecutingAction) return false;
            if (actionExecutor.IsBusy) return false;
            return true;
        }

        public void RequestEndTurn()
        {
            if (!CanEndTurn()) return;
            turnManager.EndTurn();
        }

        private void HandleCellClicked(Vector3Int cell)
        {
            Debug.Log($"[TurnInput] HandleCellClicked: {cell}, mode={targetingController?.ActiveMode}, isRepoSelectCell={targetingController?.IsRepositionSelectingCell}, isBusy={actionExecutor?.IsBusy}, isPlayerTurn={turnManager?.IsPlayerTurn}");
            if (!CanProcessTargetingClick())
                return;
            if (actionExecutor.IsBusy && (targetingController == null || !targetingController.IsRepositionSelectingCell))
                return;
            targetingController.TryConfirmCell(cell);
        }

        private void HandleEntityClicked(EntityHandle handle)
        {
            Debug.Log($"[TurnInput] HandleEntityClicked: handle={handle.Id}, mode={targetingController?.ActiveMode}, isRepoSelectCell={targetingController?.IsRepositionSelectingCell}, isBusy={actionExecutor?.IsBusy}");
            if (!CanProcessTargetingClick())
                return;

            // During Reposition cell selection, convert entity click to cell click
            // so player can select a destination cell occupied by the target itself.
            if (targetingController != null && targetingController.IsRepositionSelectingCell)
            {
                var data = entityManager?.Registry?.Get(handle);
                Debug.Log($"[TurnInput] EntityClick→CellClick: handle={handle.Id}, gridPos={data?.GridPosition}");
                if (data != null)
                    targetingController.TryConfirmCell(data.GridPosition);
                return;
            }

            if (actionExecutor.IsBusy) return;
            targetingController.TryConfirmEntity(handle);
        }

        private bool CanProcessTargetingClick()
        {
            if (turnManager == null)
                return false;

            if (turnManager.IsPlayerTurn)
                return true;

            // Reposition target selection is a two-step interaction that intentionally keeps the turn
            // in ExecutingAction while waiting for a destination click. Allow that follow-up click path.
            return turnManager.State == TurnState.ExecutingAction
                && targetingController != null
                && targetingController.IsRepositionSelectingCell;
        }

        private void Update()
        {
            if (turnManager == null) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.spaceKey.wasPressedThisFrame)
                RequestEndTurn();

            if (kb.escapeKey.wasPressedThisFrame)
                targetingController.CancelTargeting();

            if (kb.rKey.wasPressedThisFrame)
                actionExecutor.TryExecuteRaiseShield();

            if (kb.tKey.wasPressedThisFrame)
            {
                if (turnManager.IsPlayerTurn && !actionExecutor.IsBusy)
                    targetingController.BeginTargeting(TargetingMode.Trip, h => actionExecutor.TryExecuteTrip(h));
            }

            if (kb.hKey.wasPressedThisFrame)
            {
                if (turnManager.IsPlayerTurn && !actionExecutor.IsBusy)
                    targetingController.BeginTargeting(TargetingMode.Shove, h => actionExecutor.TryExecuteShove(h));
            }

            if (kb.jKey.wasPressedThisFrame)
            {
                if (turnManager.IsPlayerTurn && !actionExecutor.IsBusy)
                    targetingController.BeginTargeting(TargetingMode.Grapple, h => actionExecutor.TryExecuteGrapple(h));
            }

            if (kb.kKey.wasPressedThisFrame)
            {
                if (turnManager.IsPlayerTurn && !actionExecutor.IsBusy)
                    targetingController.BeginTargeting(TargetingMode.Escape, h => actionExecutor.TryExecuteEscape(h));
            }

            if (kb.vKey.wasPressedThisFrame)
            {
                if (targetingController.ActiveMode == TargetingMode.Reposition)
                {
                    targetingController.CancelTargeting();
                }
                else if (turnManager.IsPlayerTurn && !actionExecutor.IsBusy)
                {
                    targetingController.BeginRepositionTargeting(
                        actionExecutor.TryBeginRepositionTargetSelection,
                        actionExecutor.TryConfirmRepositionDestination,
                        onCancelled: null,
                        onCellPhaseCancelled: actionExecutor.CancelPendingRepositionSelection);
                }
            }

            if (kb.yKey.wasPressedThisFrame)
            {
                if (turnManager.IsPlayerTurn && !actionExecutor.IsBusy)
                    targetingController.BeginTargeting(TargetingMode.Demoralize, h => actionExecutor.TryExecuteDemoralize(h));
            }
        }
    }
}
