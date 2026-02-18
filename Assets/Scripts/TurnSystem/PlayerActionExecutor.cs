using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Phase 10.X FINAL: Centralizes player action execution.
    /// Owns BeginActionExecution/CompleteActionWithCost logic (not StrideAction).
    /// Provides dev watchdog for stuck actions.
    /// Inspector-only wiring.
    /// </summary>
    public class PlayerActionExecutor : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private StrideAction strideAction;
        [SerializeField] private StrikeAction strikeAction;

        private EntityHandle executingActor = EntityHandle.None;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private float executionStartTime = -1f;
        private const float StuckTimeoutSeconds = 30f;
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (turnManager == null) Debug.LogError("[Executor] Missing TurnManager", this);
            if (entityManager == null) Debug.LogError("[Executor] Missing EntityManager", this);
            if (strideAction == null) Debug.LogError("[Executor] Missing StrideAction", this);
            if (strikeAction == null) Debug.LogError("[Executor] Missing StrikeAction", this);
        }
#endif

        public bool IsBusy
        {
            get
            {
                if (turnManager != null && turnManager.State == TurnState.ExecutingAction) return true;
                if (strideAction != null && strideAction.StrideInProgress) return true;
                return false;
            }
        }

        public bool CanActNow()
        {
            if (turnManager == null || entityManager == null) return false;
            if (!turnManager.IsPlayerTurn) return false;
            if (turnManager.State == TurnState.ExecutingAction) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;

            if (!turnManager.CanAct(actor)) return false;
            if (strideAction != null && strideAction.StrideInProgress) return false;

            return true;
        }

        public bool TryExecuteStrideToCell(Vector3Int targetCell)
        {
            if (turnManager == null || entityManager == null || strideAction == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            int availableActions = Mathf.Clamp(turnManager.ActionsRemaining, 0, 3);
            if (availableActions <= 0) return false;

            // Lock input first; rollback if stride fails
            executingActor = actor;
            turnManager.BeginActionExecution();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            bool started = strideAction.TryExecuteStride(actor, targetCell, availableActions, HandleStrideComplete);

            if (!started)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted(); // rollback ExecutingAction
                return false;
            }

            return true;
        }

        private void HandleStrideComplete(int actionCost)
        {
            // Called after animation completes
            if (turnManager == null) return;

            // Defensive: ensure same actor (should always be true)
            if (executingActor.IsValid && turnManager.CurrentEntity != executingActor)
            {
                Debug.LogError($"[Executor] Stride complete but current actor changed. Was {executingActor}, now {turnManager.CurrentEntity}. Forcing ActionCompleted().", this);
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted();
                return;
            }

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(Mathf.Max(1, actionCost));
        }

        public bool TryExecuteStrike(EntityHandle target)
        {
            if (turnManager == null || entityManager == null || strikeAction == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;

            executingActor = actor;
            turnManager.BeginActionExecution();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            bool performed = strikeAction.TryStrike(actor, target);

            if (!performed)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted(); // rollback (no action spent)
                return false;
            }

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(1); // miss still spends
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (turnManager == null) return;

            if (turnManager.State == TurnState.ExecutingAction && executionStartTime > 0f)
            {
                if (Time.time - executionStartTime > StuckTimeoutSeconds)
                {
                    Debug.LogError("[Executor] Action stuck for 30s! Forcing ActionCompleted().", this);
                    executionStartTime = float.MaxValue; // prevent spam
                    executingActor = EntityHandle.None;
                    turnManager.ActionCompleted();
                }
            }
        }
#endif
    }
}
