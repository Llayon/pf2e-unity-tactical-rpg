using System.Collections;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.Presentation;

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
        [SerializeField] private ShieldBlockAction shieldBlockAction;
        [SerializeField] private ReactionPromptController reactionPromptController;

        [Header("Timing")]
        [SerializeField] private float thinkDelay = 0.6f;
        [SerializeField] private float actionDelay = 0.4f;

        private const int MaxActionAttemptsPerTurn = 6;
        private const int NoProgressLoopThreshold = 2;
        private const float StrideTimeoutSeconds = 30f;
        private const float ReactionTimeoutSeconds = 10f;

        private Coroutine activeCoroutine;
        private int runId;
        private IAIDecisionPolicy decisionPolicy;
        private IReactionDecisionPolicy reactionPolicy;
        private readonly System.Collections.Generic.List<ReactionOption> reactionBuffer = new(2);

        // Async stride state
        private bool waitingForStride;
        private int lastStrideCost;

        // Tracks lock ownership for safe cleanup on abort/disable.
        private bool ownsExecutionLock;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (turnManager == null) Debug.LogError("[AITurnController] Missing TurnManager", this);
            if (eventBus == null) Debug.LogError("[AITurnController] Missing CombatEventBus", this);
            if (entityManager == null) Debug.LogError("[AITurnController] Missing EntityManager", this);
            if (gridManager == null) Debug.LogError("[AITurnController] Missing GridManager", this);
            if (strideAction == null) Debug.LogError("[AITurnController] Missing StrideAction", this);
            if (strikeAction == null) Debug.LogError("[AITurnController] Missing StrikeAction", this);
            if (standAction == null) Debug.LogError("[AITurnController] Missing StandAction", this);
            if (shieldBlockAction == null) Debug.LogWarning("[AITurnController] Missing ShieldBlockAction", this);
            if (reactionPromptController == null) Debug.LogWarning("[AITurnController] Missing ReactionPromptController", this);
        }
#endif

        private void OnEnable()
        {
            if (turnManager == null || eventBus == null)
            {
                Debug.LogError("[AITurnController] Missing TurnManager/CombatEventBus. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnTurnStartedTyped += HandleTurnStartedTyped;
        }

        private void OnDisable()
        {
            if (eventBus != null)
                eventBus.OnTurnStartedTyped -= HandleTurnStartedTyped;

            runId++; // invalidate any in-flight coroutine work

            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
                activeCoroutine = null;
            }

            waitingForStride = false;
            TryRollbackExecutionLock();
            decisionPolicy = null;
            reactionPolicy = null;
        }

        private void HandleTurnStartedTyped(in TurnStartedEvent e)
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
            if (!EnsureDecisionPolicy() || !EnsureReactionPolicy()) return;

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
            var progressGuard = new AITurnProgressGuard(NoProgressLoopThreshold);
            EntityHandle lockedTarget = EntityHandle.None;
            try
            {
                yield return new WaitForSeconds(thinkDelay);

                if (!IsCurrentRun(token) || !IsMyTurn(actor))
                    yield break;
                if (!EnsureDecisionPolicy())
                    yield break;
                if (!EnsureReactionPolicy())
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

                    EntityHandle target = lockedTarget;
                    var targetData = target.IsValid ? entityManager.Registry.Get(target) : null;
                    if (!AITurnTargetLock.IsValidTarget(actorData, targetData))
                    {
                        lockedTarget = EntityHandle.None;
                        target = decisionPolicy.SelectTarget(actorData);
                        if (!target.IsValid) break;

                        targetData = entityManager.Registry.Get(target);
                        if (!AITurnTargetLock.IsValidTarget(actorData, targetData))
                            break;

                        lockedTarget = target;
                    }

                    if (progressGuard.RegisterStep(actorData.GridPosition, actorData.ActionsRemaining, target))
                    {
                        Debug.LogWarning(
                            $"[AITurnController] No-progress guard triggered for actor {actor.Id}. Ending turn early.",
                            this);
                        ForceEndTurn(actor);
                        yield break;
                    }

                    if (decisionPolicy.IsInMeleeRange(actorData, targetData))
                    {
                        bool struck = false;
                        yield return DoStrike(actor, target, token, v => struck = v);
                        if (!IsCurrentRun(token) || !IsMyTurn(actor))
                            yield break;
                        if (!struck) break;

                        yield return new WaitForSeconds(actionDelay);
                        continue;
                    }

                    if (actorData.ActionsRemaining <= 0) break;

                    var moveCell = decisionPolicy.SelectStrideCell(
                        actorData,
                        targetData,
                        Mathf.Clamp(actorData.ActionsRemaining, 0, 3));

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
                progressGuard.Reset();

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

        private IEnumerator DoStrike(EntityHandle actor, EntityHandle target, int token, System.Action<bool> setResult)
        {
            bool completed = false;
            setResult?.Invoke(false);

            try
            {
                if (strikeAction == null || turnManager == null)
                    yield break;
                if (!EnsureReactionPolicy())
                    yield break;
                if (!TryBeginExecution(actor, "AI.Strike"))
                    yield break;

                var phase = strikeAction.ResolveAttackRoll(actor, target, UnityRng.Shared);
                if (!phase.HasValue)
                    yield break;

                if (!IsCurrentRun(token))
                    yield break;

                var resolved = strikeAction.DetermineHitAndDamage(phase.Value, target, UnityRng.Shared);

                // Async reaction window.
                int damageReduction = 0;
                bool reactionResolved = false;
                yield return ResolvePostHitReactionReductionCoroutine(
                    resolved, token, reduction =>
                    {
                        damageReduction = reduction;
                        reactionResolved = true;
                    });

                if (!IsCurrentRun(token))
                    yield break;

                if (!reactionResolved)
                    damageReduction = 0;

                bool performed = strikeAction.ApplyStrikeDamage(resolved, damageReduction);
                if (!performed)
                    yield break;

                CompleteExecutionWithCost(1);
                completed = true;
                setResult?.Invoke(true);
            }
            finally
            {
                if (!completed)
                    TryRollbackExecutionLock();
            }
        }

        private IEnumerator ResolvePostHitReactionReductionCoroutine(
            StrikePhaseResult resolved, int token, System.Action<int> setResult)
        {
            if (!resolved.damageDealt || resolved.damageRolled <= 0)
            {
                setResult?.Invoke(0);
                yield break;
            }

            if (reactionPolicy == null || entityManager == null || entityManager.Registry == null || turnManager == null)
            {
                setResult?.Invoke(0);
                yield break;
            }

            reactionBuffer.Clear();
            ReactionService.CollectEligibleReactions(
                ReactionTriggerPhase.PostHit,
                resolved.attacker,
                resolved.target,
                turnManager.InitiativeOrder,
                handle => entityManager.Registry.Get(handle),
                reactionBuffer);

            if (reactionBuffer.Count <= 0)
            {
                setResult?.Invoke(0);
                yield break;
            }

            // MVP invariant: max 1 eligible option (target self-only Shield Block).
            var option = reactionBuffer[0];
            var reactorData = entityManager.Registry.Get(option.entity);
            if (reactorData == null || !reactorData.IsAlive)
            {
                setResult?.Invoke(0);
                yield break;
            }
            if (!turnManager.CanUseReaction(option.entity))
            {
                setResult?.Invoke(0);
                yield break;
            }

            bool? decided = null;
            reactionPolicy.DecideReaction(
                option,
                reactorData,
                resolved.damageRolled,
                result => decided = result);

            // If policy resolved synchronously (AI auto-block, Player AutoBlock/Never), skip yield.
            if (!decided.HasValue)
            {
                // Async path: wait for callback (modal prompt).
                float reactionStart = Time.time;
                while (!decided.HasValue)
                {
                    if (!IsCurrentRun(token))
                    {
                        // Abort: force-close any open prompt.
                        if (reactionPromptController != null)
                            reactionPromptController.ForceCloseAsDecline();
                        setResult?.Invoke(0);
                        yield break;
                    }

                    if (Time.time - reactionStart > ReactionTimeoutSeconds)
                    {
                        Debug.LogWarning("[AITurnController] Reaction decision timeout. Auto-declining.", this);
                        if (reactionPromptController != null)
                            reactionPromptController.ForceCloseAsDecline();
                        decided = false;
                        break;
                    }

                    yield return null;
                }
            }

            if (decided != true)
            {
                setResult?.Invoke(0);
                yield break;
            }

            var blockResult = ShieldBlockRules.Calculate(reactorData.EquippedShield, resolved.damageRolled);
            if (shieldBlockAction == null)
            {
                Debug.LogWarning("[AITurnController] ShieldBlockAction is missing. Skipping Shield Block reaction.", this);
                setResult?.Invoke(0);
                yield break;
            }

            int reduction = shieldBlockAction.Execute(option.entity, resolved.damageRolled, in blockResult)
                ? blockResult.targetDamageReduction
                : 0;

            setResult?.Invoke(reduction);
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

        private bool EnsureDecisionPolicy()
        {
            if (decisionPolicy != null) return true;

            if (entityManager == null || gridManager == null)
            {
                Debug.LogError("[AITurnController] Missing EntityManager/GridManager for AI policy. Disabling.", this);
                enabled = false;
                return false;
            }

            decisionPolicy = new SimpleMeleeDecisionPolicy(entityManager, gridManager);
            return true;
        }

        private bool EnsureReactionPolicy()
        {
            if (reactionPolicy != null) return true;
            reactionPolicy = new ModalReactionPolicy(reactionPromptController);
            return true;
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
