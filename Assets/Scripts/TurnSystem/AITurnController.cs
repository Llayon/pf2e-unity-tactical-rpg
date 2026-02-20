using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Phase 16: Simple melee AI for enemy turns.
    /// Reuses StrideAction/StrikeAction/StandAction with the same action lock contract as player execution.
    /// Inspector-only wiring.
    /// </summary>
    public class AITurnController : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private StrideAction strideAction;
        [SerializeField] private StrikeAction strikeAction;
        [SerializeField] private StandAction standAction;

        [Header("Timing")]
        [SerializeField] private float thinkDelay = 0.6f;
        [SerializeField] private float actionDelay = 0.4f;

        private const int MaxActionAttemptsPerTurn = 6;
        private const float StrideTimeoutSeconds = 30f;

        private Coroutine activeCoroutine;
        private int runId;
        private bool usingTypedTurnStartSubscription;

        // Async stride state
        private bool waitingForStride;
        private int lastStrideCost;

        // Tracks lock ownership for safe cleanup on abort/disable.
        private bool ownsExecutionLock;

        // Reusable buffers (avoid per-turn allocations)
        private readonly List<Vector3Int> pathBuffer = new(32);
        private readonly Dictionary<Vector3Int, int> zoneBuffer = new();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (turnManager == null) Debug.LogError("[AITurnController] Missing TurnManager", this);
            // eventBus is optional for backward compatibility; OnEnable auto-resolves it.
            if (entityManager == null) Debug.LogError("[AITurnController] Missing EntityManager", this);
            if (gridManager == null) Debug.LogError("[AITurnController] Missing GridManager", this);
            if (strideAction == null) Debug.LogError("[AITurnController] Missing StrideAction", this);
            if (strikeAction == null) Debug.LogError("[AITurnController] Missing StrikeAction", this);
            if (standAction == null) Debug.LogError("[AITurnController] Missing StandAction", this);
        }
#endif

        private void OnEnable()
        {
            if (turnManager == null)
            {
                Debug.LogError("[AITurnController] Missing TurnManager. Disabling.", this);
                enabled = false;
                return;
            }

            // Backward-compatible self-heal for existing scenes: TurnManager and CombatEventBus
            // live on the same CombatController in current setup.
            if (eventBus == null)
                eventBus = turnManager.GetComponent<CombatEventBus>();

            if (eventBus == null)
                eventBus = FindFirstObjectByType<CombatEventBus>();

            if (eventBus != null)
            {
                eventBus.OnTurnStartedTyped += HandleTurnStartedTyped;
                usingTypedTurnStartSubscription = true;
                return;
            }

            // Legacy fallback for scenes/tests that do not yet wire CombatEventBus.
            turnManager.OnTurnStarted += HandleTurnStartedLegacy;
            usingTypedTurnStartSubscription = false;
        }

        private void OnDisable()
        {
            if (usingTypedTurnStartSubscription)
            {
                if (eventBus != null)
                    eventBus.OnTurnStartedTyped -= HandleTurnStartedTyped;
            }
            else if (turnManager != null)
            {
                turnManager.OnTurnStarted -= HandleTurnStartedLegacy;
            }

            usingTypedTurnStartSubscription = false;

            runId++; // invalidate any in-flight coroutine work

            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
                activeCoroutine = null;
            }

            waitingForStride = false;
            TryRollbackExecutionLock();
        }

        private void HandleTurnStartedTyped(in TurnStartedEvent e)
        {
            HandleTurnStarted(e.actor);
        }

        private void HandleTurnStartedLegacy(TurnStartedEvent e)
        {
            HandleTurnStarted(e.actor);
        }

        private void HandleTurnStarted(EntityHandle actor)
        {
            if (turnManager == null || !enabled) return;
            if (turnManager.State != TurnState.EnemyTurn) return;

            var data = entityManager != null && entityManager.Registry != null
                ? entityManager.Registry.Get(actor)
                : null;
            if (data == null || data.Team != Team.Enemy) return;

            // Recover if a previous run crashed/aborted while still holding the action lock.
            TryRollbackExecutionLock();

            // Defensive: stop stale coroutine if still alive from previous actor.
            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
                activeCoroutine = null;
            }

            runId++;
            activeCoroutine = StartCoroutine(ExecuteAITurn(actor, runId));
        }

        private IEnumerator ExecuteAITurn(EntityHandle actor, int token)
        {
            try
            {
                yield return new WaitForSeconds(thinkDelay);

                if (!IsCurrentRun(token) || !IsMyTurn(actor))
                    yield break;

                var actorData = entityManager.Registry.Get(actor);
                if (actorData == null || !actorData.IsAlive)
                    yield break;

                // Phase 1: Stand if prone
                if (actorData.HasCondition(ConditionType.Prone) && actorData.ActionsRemaining > 0)
                {
                    bool stood = TryExecuteStand(actor);
                    if (!IsCurrentRun(token) || !IsMyTurn(actor))
                        yield break;

                    if (stood)
                        yield return new WaitForSeconds(actionDelay);
                }

                int attempts = 0;
                while (IsCurrentRun(token) && IsMyTurn(actor) && attempts < MaxActionAttemptsPerTurn)
                {
                    attempts++;

                    actorData = entityManager.Registry.Get(actor);
                    if (actorData == null || !actorData.IsAlive) break;

                    EntityHandle target = SimpleMeleeAIDecision.FindBestTarget(actorData, entityManager.Registry.GetAll());
                    if (!target.IsValid) break;

                    var targetData = entityManager.Registry.Get(target);
                    if (targetData == null || !targetData.IsAlive) break;

                    if (SimpleMeleeAIDecision.IsInMeleeRange(actorData, targetData))
                    {
                        bool struck = TryExecuteStrike(actor, target);
                        if (!IsCurrentRun(token) || !IsMyTurn(actor))
                            yield break;
                        if (!struck) break;

                        yield return new WaitForSeconds(actionDelay);
                        continue;
                    }

                    if (actorData.ActionsRemaining <= 0) break;

                    var moveCell = SimpleMeleeAIDecision.FindBestMoveCell(
                        gridManager.Data,
                        entityManager.Pathfinding,
                        entityManager.Occupancy,
                        actorData,
                        targetData,
                        Mathf.Clamp(actorData.ActionsRemaining, 0, 3),
                        pathBuffer,
                        zoneBuffer);

                    if (!moveCell.HasValue || moveCell.Value == actorData.GridPosition)
                        break;

                    bool moved = false;
                    yield return DoStride(actor, moveCell.Value, token, success => moved = success);

                    if (!IsCurrentRun(token) || !IsMyTurn(actor))
                        yield break;
                    if (!moved)
                        break;

                    yield return new WaitForSeconds(actionDelay);
                }

                ForceEndTurn(actor);
            }
            finally
            {
                // StopCoroutine may bypass coroutine body; this guard keeps action state recoverable.
                TryRollbackExecutionLock();

                if (IsCurrentRun(token))
                    activeCoroutine = null;
            }
        }

        private IEnumerator DoStride(EntityHandle actor, Vector3Int targetCell, int token, System.Action<bool> setResult)
        {
            bool completed = false;
            setResult?.Invoke(false);

            try
            {
                if (strideAction == null || turnManager == null)
                    yield break;

                var data = entityManager.Registry.Get(actor);
                if (data == null)
                    yield break;

                int availableActions = Mathf.Clamp(data.ActionsRemaining, 0, 3);
                if (availableActions <= 0)
                    yield break;

                if (!TryBeginExecution(actor, "AI.Stride"))
                    yield break;

                waitingForStride = true;
                lastStrideCost = 0;

                bool started = strideAction.TryExecuteStride(actor, targetCell, availableActions, HandleStrideComplete);
                if (!started)
                {
                    waitingForStride = false;
                    yield break;
                }

                float strideStart = Time.time;
                while (waitingForStride)
                {
                    if (!IsCurrentRun(token))
                    {
                        waitingForStride = false;
                        yield break;
                    }

                    if (Time.time - strideStart > StrideTimeoutSeconds)
                    {
                        Debug.LogError("[AITurnController] Stride timeout (30s). Rolling back action execution lock.", this);
                        waitingForStride = false;
                        yield break;
                    }

                    yield return null;
                }

                if (!IsCurrentRun(token))
                    yield break;

                CompleteExecutionWithCost(Mathf.Max(1, lastStrideCost));
                completed = true;
                setResult?.Invoke(true);
            }
            finally
            {
                if (!completed)
                    TryRollbackExecutionLock();
            }
        }

        private void HandleStrideComplete(int cost)
        {
            lastStrideCost = cost;
            waitingForStride = false;
        }

        private bool TryExecuteStrike(EntityHandle actor, EntityHandle target)
        {
            if (strikeAction == null || turnManager == null) return false;
            if (!TryBeginExecution(actor, "AI.Strike")) return false;

            bool completed = false;
            try
            {
                bool performed = strikeAction.TryStrike(actor, target);
                if (!performed)
                    return false;

                CompleteExecutionWithCost(1);
                completed = true;
                return true;
            }
            finally
            {
                if (!completed)
                    TryRollbackExecutionLock();
            }
        }

        private bool TryExecuteStand(EntityHandle actor)
        {
            if (standAction == null || turnManager == null) return false;
            if (!standAction.CanStand(actor)) return false;
            if (!TryBeginExecution(actor, "AI.Stand")) return false;

            bool completed = false;
            try
            {
                bool stood = standAction.TryStand(actor);
                if (!stood)
                    return false;

                CompleteExecutionWithCost(StandAction.ActionCost);
                completed = true;
                return true;
            }
            finally
            {
                if (!completed)
                    TryRollbackExecutionLock();
            }
        }

        private bool TryBeginExecution(EntityHandle actor, string source)
        {
            if (turnManager == null) return false;
            if (!IsMyTurn(actor)) return false;
            if (turnManager.State == TurnState.ExecutingAction) return false;

            turnManager.BeginActionExecution(actor, source);
            if (turnManager.State != TurnState.ExecutingAction || turnManager.ExecutingActor != actor)
                return false;

            ownsExecutionLock = true;
            return true;
        }

        private void CompleteExecutionWithCost(int actionCost)
        {
            if (turnManager == null)
            {
                ownsExecutionLock = false;
                return;
            }

            turnManager.CompleteActionWithCost(Mathf.Max(0, actionCost));
            ownsExecutionLock = false;
        }

        private void TryRollbackExecutionLock()
        {
            if (!ownsExecutionLock || turnManager == null) return;

            if (turnManager.State == TurnState.ExecutingAction)
                turnManager.ActionCompleted();

            ownsExecutionLock = false;
        }

        private bool IsCurrentRun(int token) => token == runId;

        private bool IsMyTurn(EntityHandle actor)
        {
            return turnManager != null
                && turnManager.CurrentEntity == actor
                && turnManager.State == TurnState.EnemyTurn
                && turnManager.ActionsRemaining > 0;
        }

        private void ForceEndTurn(EntityHandle actor)
        {
            if (turnManager == null) return;

            if (turnManager.CurrentEntity == actor
                && turnManager.State == TurnState.EnemyTurn
                && turnManager.ActionsRemaining > 0)
            {
                turnManager.EndTurn();
            }
        }
    }
}
