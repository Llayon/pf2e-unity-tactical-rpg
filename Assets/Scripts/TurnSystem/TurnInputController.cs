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
    /// - T / H / Y → Begin explicit targeting for Trip / Shove / Demoralize
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
            if (!turnManager.IsPlayerTurn) return;
            if (actionExecutor.IsBusy) return;
            // TargetingResult ignored until UI (Phase 15 adds tooltip/flash)
            targetingController.TryConfirmCell(cell);
        }

        private void HandleEntityClicked(EntityHandle handle)
        {
            if (!turnManager.IsPlayerTurn) return;
            if (actionExecutor.IsBusy) return;
            // TargetingResult ignored until UI (Phase 15 adds tooltip/flash)
            targetingController.TryConfirmEntity(handle);
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

            if (kb.yKey.wasPressedThisFrame)
            {
                if (turnManager.IsPlayerTurn && !actionExecutor.IsBusy)
                    targetingController.BeginTargeting(TargetingMode.Demoralize, h => actionExecutor.TryExecuteDemoralize(h));
            }
        }
    }
}
