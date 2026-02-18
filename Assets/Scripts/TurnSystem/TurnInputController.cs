using UnityEngine;
using UnityEngine.InputSystem;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Phase 10.X FINAL: Thin wiring between input and PlayerActionExecutor.
    /// - Space → EndTurn (PlayerTurn only, not while executor busy)
    /// - Cell click → executor.TryExecuteStrideToCell
    /// - Entity click → executor.TryExecuteStrike (if enemy) or do nothing (future inspect/heal)
    ///
    /// Subscriptions: OnEnable/OnDisable for OnCellClicked + OnEntityClicked.
    /// Inspector-only wiring.
    /// </summary>
    public class TurnInputController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private PlayerActionExecutor actionExecutor;
        [SerializeField] private EntityManager entityManager;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (turnManager == null) Debug.LogError("[TurnInput] Missing TurnManager", this);
            if (gridManager == null) Debug.LogError("[TurnInput] Missing GridManager", this);
            if (actionExecutor == null) Debug.LogError("[TurnInput] Missing PlayerActionExecutor", this);
            if (entityManager == null) Debug.LogError("[TurnInput] Missing EntityManager", this);
        }
#endif

        private void OnEnable()
        {
            if (gridManager != null)
            {
                gridManager.OnCellClicked += HandleCellClicked;
                gridManager.OnEntityClicked += HandleEntityClicked;
            }
        }

        private void OnDisable()
        {
            if (gridManager != null)
            {
                gridManager.OnCellClicked -= HandleCellClicked;
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
            if (actionExecutor != null)
                actionExecutor.TryExecuteStrideToCell(cell);
        }

        private void HandleEntityClicked(EntityHandle handle)
        {
            if (turnManager == null || entityManager == null || actionExecutor == null) return;
            if (!turnManager.IsPlayerTurn) return;
            if (turnManager.State == TurnState.ExecutingAction) return;

            var currentActor = turnManager.CurrentEntity;
            if (!currentActor.IsValid) return;

            // Ignore self click
            if (handle == currentActor) return;

            // Get target data
            if (entityManager.Registry == null) return;
            var targetData = entityManager.Registry.Get(handle);
            if (targetData == null) return;

            // If enemy → attack (Phase 11 placeholder)
            if (targetData.Team == Team.Enemy)
            {
                actionExecutor.TryExecuteStrike(handle);
            }
            // else: do nothing (future: inspect/heal/buff)
        }

        private void Update()
        {
            if (turnManager == null) return;

            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                RequestEndTurn();
            }
        }
    }
}
