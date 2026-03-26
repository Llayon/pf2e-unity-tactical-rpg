using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private StepAction stepAction;
        [SerializeField] private PlayerActionExecutor actionExecutor;
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
        private readonly List<EntityHandle> aiSpellTargetBuffer = new(3);
        private readonly List<Vector3Int> aiCellBuffer = new(8);
        private readonly Dictionary<Vector3Int, int> fleeingZoneBuffer = new(64);

        // Async stride state
        private bool waitingForStride;
        private int lastStrideCost;
        private bool warnedAboutMissingStepAction;

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
            if (stepAction == null) Debug.LogWarning("[AITurnController] Missing StepAction", this);
            if (actionExecutor == null) Debug.LogWarning("[AITurnController] Missing PlayerActionExecutor", this);
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

                actorData = entityManager.Registry.Get(actor);
                if (actorData == null || !actorData.IsAlive)
                    yield break;

                if (actorData.HasCondition(ConditionType.Fleeing))
                {
                    if (TrySelectFleeCell(actorData, Mathf.Clamp(actorData.ActionsRemaining, 0, 3), out var fleeCell))
                    {
                        bool moved = false;
                        yield return DoStride(actor, fleeCell, token, success => moved = success);
                        if (!IsCurrentRun(token) || !IsMyTurn(actor))
                            yield break;
                        if (moved)
                            yield return new WaitForSeconds(actionDelay);
                    }

                    ForceEndTurn(actor);
                    yield break;
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

                    if (!decisionPolicy.TrySelectActionCandidate(
                            actorData,
                            targetData,
                            actorData.ActionsRemaining,
                            out var candidate))
                        break;

                    bool acted = false;
                    yield return ExecuteActionCandidate(actor, candidate, token, success => acted = success);

                    if (!IsCurrentRun(token))
                        yield break;

                    actorData = entityManager.Registry.Get(actor);
                    if (actorData == null || !actorData.IsAlive)
                        yield break;

                    if (!IsMyTurn(actor) && acted)
                        yield break;

                    if (!acted)
                    {
                        if (!IsMyTurn(actor))
                            yield break;

                        continue;
                    }

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

        private IEnumerator DoStep(EntityHandle actor, Vector3Int targetCell, int token, System.Action<bool> setResult)
        {
            bool completed = false;
            setResult?.Invoke(false);

            try
            {
                if (stepAction == null || turnManager == null)
                    yield break;

                var data = entityManager != null && entityManager.Registry != null
                    ? entityManager.Registry.Get(actor)
                    : null;
                if (data == null || data.ActionsRemaining <= 0)
                    yield break;

                if (!TryBeginExecution(actor, "AI.Step"))
                    yield break;

                bool performed = stepAction.TryExecuteStep(actor, targetCell);
                if (!performed)
                    yield break;

                if (!IsCurrentRun(token))
                    yield break;

                CompleteExecutionWithCost(StepAction.ActionCost);
                completed = true;
                setResult?.Invoke(true);
            }
            finally
            {
                if (!completed)
                    TryRollbackExecutionLock();
            }
        }

        private bool TrySelectFleeCell(EntityData actorData, int availableActions, out Vector3Int fleeCell)
        {
            fleeCell = default;

            if (actorData == null
                || entityManager == null
                || entityManager.Registry == null
                || entityManager.GridData == null
                || entityManager.Pathfinding == null
                || entityManager.Occupancy == null)
            {
                return false;
            }

            fleeingZoneBuffer.Clear();
            if (!FleeingRules.TryBuildFleeZone(
                    entityManager.GridData,
                    entityManager.Pathfinding,
                    entityManager.Occupancy,
                    entityManager.Registry,
                    actorData,
                    availableActions,
                    fleeingZoneBuffer,
                    out _))
            {
                return false;
            }

            return FleeingRules.TrySelectDeterministicCell(fleeingZoneBuffer, out fleeCell);
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

        private IEnumerator ExecuteActionCandidate(EntityHandle actor, AIActionCandidate candidate, int token, System.Action<bool> setResult)
        {
            setResult?.Invoke(false);

            switch (candidate.kind)
            {
                case AIActionCandidateKind.Spell:
                    setResult?.Invoke(TryExecuteSpellDecision(actor, in candidate.spellDecision));
                    yield break;

                case AIActionCandidateKind.Skill:
                    setResult?.Invoke(TryExecuteSkillDecision(in candidate.skillDecision));
                    yield break;

                case AIActionCandidateKind.Defensive:
                    setResult?.Invoke(TryExecuteDefensiveDecision(in candidate.defensiveDecision));
                    yield break;

                case AIActionCandidateKind.Strike:
                    if (!candidate.target.IsValid)
                        yield break;

                    yield return DoStrike(actor, candidate.target, token, setResult);
                    yield break;

                case AIActionCandidateKind.Step:
                    if (!candidate.HasCell)
                        yield break;
                    if (!EnsureStepAction())
                        yield break;

                    yield return DoStep(actor, candidate.cell, token, setResult);
                    yield break;

                case AIActionCandidateKind.Stride:
                    if (!candidate.HasCell)
                        yield break;

                    yield return DoStride(actor, candidate.cell, token, setResult);
                    yield break;
            }
        }

        private IEnumerator ResolvePostHitReactionReductionCoroutine(
            StrikePhaseResult resolved, int token, System.Action<int> setResult)
        {
            if (entityManager == null || entityManager.Registry == null || turnManager == null)
            {
                setResult?.Invoke(0);
                yield break;
            }

            var ledger = turnManager.ReactionTriggerWindowLedger;
            var triggerWindowToken = ledger != null
                ? ledger.OpenWindow(
                    TriggerWindowType.PostHitDamage,
                    source: resolved.attacker,
                    target: resolved.target)
                : default;

            try
            {
                yield return ReactionBroker.ResolvePostHitReductionAsync(
                    resolved,
                    turnManager.InitiativeOrder,
                    handle => entityManager.Registry.Get(handle),
                    handle => turnManager.CanUseReaction(handle),
                    reactionPolicy,
                    shieldBlockAction,
                    reactionBuffer,
                    shouldAbortWaiting: () => !IsCurrentRun(token),
                    timeoutSeconds: ReactionTimeoutSeconds,
                    forceClosePrompt: () =>
                    {
                        if (reactionPromptController != null)
                            reactionPromptController.ForceCloseAsDecline();
                    },
                    setResult: setResult,
                    ownerTag: "AITurnController",
                    triggerWindowLedger: ledger,
                    triggerWindowToken: triggerWindowToken);
            }
            finally
            {
                if (ledger != null && triggerWindowToken.IsValid)
                    ledger.CloseWindow(triggerWindowToken);
            }
        }

        private bool TryExecuteStand(EntityHandle actor)
        {
            if (standAction == null || turnManager == null) return false;
            if (!standAction.CanStand(actor)) return false;
            if (!TryBeginExecution(actor, "AI.Stand")) return false;

            if (!TryContinueAfterActionStartReactions(
                    actor,
                    actionName: "Stand",
                    actionKind: CombatActionKind.Stand,
                    traits: CombatActionTraitFlags.None,
                    actionCost: StandAction.ActionCost))
            {
                return false;
            }

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

        private bool TryContinueAfterActionStartReactions(
            EntityHandle actor,
            string actionName,
            CombatActionKind actionKind,
            CombatActionTraitFlags traits,
            int actionCost)
        {
            eventBus?.PublishCombatActionStarted(actor, actionName, actionKind, traits, actionCost);

            bool actionInterrupted = turnManager != null && turnManager.ConsumeLastActionStartInterrupted(actor);

            if (entityManager == null || entityManager.Registry == null)
                return !actionInterrupted;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData != null && actorData.IsAlive && !actionInterrupted)
                return true;

            TryRollbackExecutionLock();

            if (turnManager != null
                && (actorData == null || !actorData.IsAlive)
                && actor == turnManager.CurrentEntity
                && (turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn))
            {
                turnManager.EndTurn();
            }

            return false;
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

        private bool EnsureStepAction()
        {
            if (stepAction != null)
                return true;

            stepAction = GetComponent<StepAction>();
            if (stepAction != null)
                return true;

            if (!warnedAboutMissingStepAction)
            {
                Debug.LogWarning("[AITurnController] Missing StepAction. AI will skip Step decisions.", this);
                warnedAboutMissingStepAction = true;
            }

            return false;
        }

        private bool EnsureActionExecutor()
        {
            if (actionExecutor != null)
                return true;

            actionExecutor = GetComponent<PlayerActionExecutor>();
            if (actionExecutor == null)
            {
                Debug.LogWarning("[AITurnController] Missing PlayerActionExecutor. AI spellcasting will be skipped.", this);
                return false;
            }

            return true;
        }

        private bool TryExecuteSpellDecision(EntityHandle actor, in AISpellDecision decision)
        {
            if (!actor.IsValid)
                return false;
            if (!EnsureActionExecutor())
                return false;

            return decision.spellId switch
            {
                SpellId.Heal => TryExecuteHealDecision(in decision),
                SpellId.Harm => TryExecuteHarmDecision(in decision),
                SpellId.Fear => actionExecutor.TryConfirmFear(decision.primaryTarget),
                SpellId.Snowball => actionExecutor.TryConfirmSnowball(decision.primaryTarget),
                SpellId.BurningHands => decision.HasAimCell && actionExecutor.TryConfirmBurningHands(decision.aimCell),
                SpellId.ElectricArc => TryExecuteElectricArcDecision(in decision),
                SpellId.ForceBarrage => TryExecuteForceBarrageDecision(in decision),
                _ => false
            };
        }

        private bool TryExecuteDefensiveDecision(in AIDefensiveDecision decision)
        {
            if (!EnsureActionExecutor())
                return false;

            return decision.actionKind switch
            {
                AIDefensiveActionKind.RaisePhysicalShield => actionExecutor.TryExecuteRaiseShield(),
                AIDefensiveActionKind.CastShieldSpell => actionExecutor.TryExecuteCastShieldSpell(decision.shieldSpellMode),
                _ => false
            };
        }

        private bool TryExecuteSkillDecision(in AISkillDecision decision)
        {
            if (!EnsureActionExecutor())
                return false;

            return decision.actionKind switch
            {
                AISkillActionKind.Demoralize => actionExecutor.TryExecuteDemoralize(decision.primaryTarget),
                AISkillActionKind.Trip => actionExecutor.TryExecuteTrip(decision.primaryTarget),
                AISkillActionKind.Grapple => actionExecutor.TryExecuteGrapple(decision.primaryTarget),
                AISkillActionKind.Shove => actionExecutor.TryExecuteShove(decision.primaryTarget),
                AISkillActionKind.Reposition => TryExecuteRepositionDecision(in decision),
                _ => false
            };
        }

        private bool TryExecuteRepositionDecision(in AISkillDecision decision)
        {
            if (!decision.primaryTarget.IsValid || !decision.HasDestinationCell)
                return false;

            RepositionTargetSelectionResult result = actionExecutor.TryBeginRepositionTargetSelection(decision.primaryTarget);
            switch (result)
            {
                case RepositionTargetSelectionResult.ResolvedAndClear:
                    return true;

                case RepositionTargetSelectionResult.EnterCellSelection:
                    aiCellBuffer.Clear();
                    if (!actionExecutor.TryGetPendingRepositionDestinations(aiCellBuffer) || aiCellBuffer.Count <= 0)
                    {
                        actionExecutor.CancelPendingRepositionSelection();
                        return true;
                    }

                    Vector3Int chosenCell = aiCellBuffer[0];
                    for (int i = 0; i < aiCellBuffer.Count; i++)
                    {
                        if (aiCellBuffer[i] == decision.destinationCell)
                        {
                            chosenCell = decision.destinationCell;
                            break;
                        }
                    }

                    if (actionExecutor.TryConfirmRepositionDestination(chosenCell))
                        return true;

                    actionExecutor.CancelPendingRepositionSelection();
                    return true;

                default:
                    return false;
            }
        }

        private bool TryExecuteElectricArcDecision(in AISpellDecision decision)
        {
            aiSpellTargetBuffer.Clear();
            if (decision.primaryTarget.IsValid)
                aiSpellTargetBuffer.Add(decision.primaryTarget);
            if (decision.secondaryTarget.IsValid && decision.secondaryTarget != decision.primaryTarget)
                aiSpellTargetBuffer.Add(decision.secondaryTarget);

            return aiSpellTargetBuffer.Count > 0
                && actionExecutor.TryConfirmElectricArc(aiSpellTargetBuffer);
        }

        private bool TryExecuteHealDecision(in AISpellDecision decision)
        {
            if (!decision.primaryTarget.IsValid)
                return false;

            int actionCount = Mathf.Clamp(decision.actionCount, 1, 2);
            return actionExecutor.TryConfirmHeal(decision.primaryTarget, actionCount);
        }

        private bool TryExecuteHarmDecision(in AISpellDecision decision)
        {
            if (!decision.primaryTarget.IsValid)
                return false;

            int actionCount = Mathf.Clamp(decision.actionCount, 1, 2);
            return actionExecutor.TryConfirmHarm(decision.primaryTarget, actionCount);
        }

        private bool TryExecuteForceBarrageDecision(in AISpellDecision decision)
        {
            aiSpellTargetBuffer.Clear();
            if (!decision.primaryTarget.IsValid)
                return false;

            int shardCount = Mathf.Clamp(decision.actionCount, 1, 3);
            for (int i = 0; i < shardCount; i++)
                aiSpellTargetBuffer.Add(decision.primaryTarget);

            return actionExecutor.TryConfirmForceBarrage(aiSpellTargetBuffer, shardCount);
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
