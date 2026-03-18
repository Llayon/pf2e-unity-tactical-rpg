using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.Presentation;
using System.Collections.Generic;

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
        private enum FleeingActionAllowance : byte
        {
            Restricted = 0,
            Stride = 1,
            Escape = 2,
            Stand = 3
        }

        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private StrideAction strideAction;
        [SerializeField] private StepAction stepAction;
        [SerializeField] private JumpAction jumpAction;
        [SerializeField] private StrikeAction strikeAction;
        [SerializeField] private ReadyStrikeAction readyStrikeAction;
        [SerializeField] private StandAction standAction;
        [SerializeField] private TripAction tripAction;
        [SerializeField] private ShoveAction shoveAction;
        [SerializeField] private GrappleAction grappleAction;
        [SerializeField] private RepositionAction repositionAction;
        [SerializeField] private EscapeAction escapeAction;
        [SerializeField] private DemoralizeAction demoralizeAction;
        [SerializeField] private AidAction aidAction;
        [SerializeField] private RaiseShieldAction raiseShieldAction;
        [SerializeField] private StandardShieldAction standardShieldAction;
        [SerializeField] private GlassShieldAction glassShieldAction;
        [SerializeField] private ShieldBlockAction shieldBlockAction;
        [SerializeField] private ReactionPromptController reactionPromptController;

        private EntityHandle executingActor = EntityHandle.None;
        private bool hasPendingRepositionSelection;
        private RepositionCheckContext pendingRepositionContext;
        private readonly List<Vector3Int> pendingRepositionDestinations = new();
        private readonly List<ReactionOption> reactionBuffer = new(2);
        private readonly ConditionService conditionService = new();
        private readonly List<ConditionDelta> conditionDeltaBuffer = new(4);
        private readonly List<Vector3Int> spellAreaCellBuffer = new(8);
        private readonly List<EntityHandle> spellAreaTargetBuffer = new(8);
        private readonly Dictionary<Vector3Int, int> fleeingZoneBuffer = new(64);
        private IReactionDecisionPolicy reactionPolicy;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private float executionStartTime = -1f;
        private const float StuckTimeoutSeconds = 30f;
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (EditorValidationGuard.ShouldSkipMissingReferenceWarnings())
                return;

            if (turnManager == null) Debug.LogError("[Executor] Missing TurnManager", this);
            if (entityManager == null) Debug.LogError("[Executor] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[Executor] Missing CombatEventBus", this);
            if (strideAction == null) Debug.LogError("[Executor] Missing StrideAction", this);
            if (stepAction == null) Debug.LogWarning("[Executor] Missing StepAction", this);
            if (jumpAction == null) Debug.LogWarning("[Executor] Missing JumpAction", this);
            if (strikeAction == null) Debug.LogError("[Executor] Missing StrikeAction", this);
            if (readyStrikeAction == null) Debug.LogWarning("[Executor] Missing ReadyStrikeAction", this);
            if (tripAction == null) Debug.LogWarning("[Executor] Missing TripAction", this);
            if (shoveAction == null) Debug.LogWarning("[Executor] Missing ShoveAction", this);
            if (grappleAction == null) Debug.LogWarning("[Executor] Missing GrappleAction", this);
            if (repositionAction == null) Debug.LogWarning("[Executor] Missing RepositionAction", this);
            if (escapeAction == null) Debug.LogWarning("[Executor] Missing EscapeAction", this);
            if (demoralizeAction == null) Debug.LogWarning("[Executor] Missing DemoralizeAction", this);
            if (aidAction == null) Debug.LogWarning("[Executor] Missing AidAction", this);
            if (raiseShieldAction == null) Debug.LogWarning("[Executor] Missing RaiseShieldAction", this);
            if (standardShieldAction == null) Debug.LogWarning("[Executor] Missing StandardShieldAction", this);
            if (glassShieldAction == null) Debug.LogWarning("[Executor] Missing GlassShieldAction", this);
            if (shieldBlockAction == null) Debug.LogWarning("[Executor] Missing ShieldBlockAction", this);
            if (reactionPromptController == null) Debug.LogWarning("[Executor] Missing ReactionPromptController", this);
        }
#endif

        private void Awake()
        {
            ResolveOptionalReferences();
        }

        public bool IsBusy
        {
            get
            {
                if (turnManager != null && turnManager.State == TurnState.ExecutingAction) return true;
                if (strideAction != null && strideAction.StrideInProgress) return true;
                if (hasPendingRepositionSelection) return true;
                return false;
            }
        }

        private void ResolveOptionalReferences()
        {
            if (aidAction == null)
            {
                aidAction = GetComponent<AidAction>();
                if (aidAction == null && entityManager != null)
                    aidAction = gameObject.AddComponent<AidAction>();
            }

            if (readyStrikeAction == null)
            {
                readyStrikeAction = GetComponent<ReadyStrikeAction>();
                if (readyStrikeAction == null && entityManager != null)
                    readyStrikeAction = gameObject.AddComponent<ReadyStrikeAction>();
            }

            if (jumpAction == null)
            {
                jumpAction = GetComponent<JumpAction>();
                if (jumpAction == null && entityManager != null)
                    jumpAction = gameObject.AddComponent<JumpAction>();
            }

            if (stepAction == null)
            {
                stepAction = GetComponent<StepAction>();
                if (stepAction == null && entityManager != null)
                    stepAction = gameObject.AddComponent<StepAction>();
            }

            if (glassShieldAction == null)
            {
                glassShieldAction = GetComponent<GlassShieldAction>();
                if (glassShieldAction == null && entityManager != null)
                    glassShieldAction = gameObject.AddComponent<GlassShieldAction>();
            }

            if (standardShieldAction == null)
            {
                standardShieldAction = GetComponent<StandardShieldAction>();
                if (standardShieldAction == null && entityManager != null)
                    standardShieldAction = gameObject.AddComponent<StandardShieldAction>();
            }

            if (eventBus == null)
                eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();

            aidAction?.InjectDependencies(entityManager, eventBus);
            jumpAction?.InjectDependencies(entityManager, eventBus);
            stepAction?.InjectDependencies(entityManager, eventBus);
            readyStrikeAction?.InjectDependencies(turnManager, entityManager, strikeAction, eventBus);
            glassShieldAction?.InjectDependencies(entityManager, eventBus);
            standardShieldAction?.InjectDependencies(entityManager, eventBus);
        }

        public bool HasPendingRepositionSelection => hasPendingRepositionSelection;

        public bool TryGetPendingRepositionDestinations(List<Vector3Int> outCells)
        {
            if (outCells == null) return false;
            outCells.Clear();
            if (!hasPendingRepositionSelection) return false;
            outCells.AddRange(pendingRepositionDestinations);
            return outCells.Count > 0;
        }

        private bool CanActNow(FleeingActionAllowance fleeingAllowance = FleeingActionAllowance.Restricted)
        {
            if (turnManager == null || entityManager == null) return false;
            if (turnManager.State != TurnState.PlayerTurn && turnManager.State != TurnState.EnemyTurn) return false;
            if (turnManager.State == TurnState.ExecutingAction) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;

            var actorData = entityManager.Registry != null ? entityManager.Registry.Get(actor) : null;
            if (actorData == null || !actorData.IsAlive) return false;
            if (IsRestrictedByFleeing(actorData, fleeingAllowance)) return false;

            if (!turnManager.CanAct(actor)) return false;
            if (strideAction != null && strideAction.StrideInProgress) return false;

            return true;
        }

        private bool IsRestrictedByFleeing(EntityData actorData, FleeingActionAllowance allowance)
        {
            if (actorData == null || !actorData.HasCondition(ConditionType.Fleeing))
                return false;

            return allowance switch
            {
                FleeingActionAllowance.Stride => false,
                FleeingActionAllowance.Escape => false,
                FleeingActionAllowance.Stand => false,
                _ => true
            };
        }

        private bool TryBuildFleeZone(EntityData actorData, int availableActions, out Vector3Int sourcePosition)
        {
            sourcePosition = default;
            fleeingZoneBuffer.Clear();

            if (actorData == null
                || entityManager == null
                || entityManager.Registry == null
                || entityManager.GridData == null
                || entityManager.Pathfinding == null
                || entityManager.Occupancy == null)
            {
                return false;
            }

            return FleeingRules.TryBuildFleeZone(
                entityManager.GridData,
                entityManager.Pathfinding,
                entityManager.Occupancy,
                entityManager.Registry,
                actorData,
                availableActions,
                fleeingZoneBuffer,
                out sourcePosition);
        }

        /// <summary>
        /// Non-mutating target preview for TargetingController/UI hint systems.
        /// Evaluates action-specific pre-target rules for the current actor without spending actions or changing state.
        /// </summary>
        public TargetingEvaluationResult PreviewEntityTargetDetailed(TargetingMode mode, EntityHandle target)
        {
            if (!target.IsValid) return TargetingEvaluationResult.FromFailure(TargetingFailureReason.InvalidTarget);
            if (turnManager == null || entityManager == null) return TargetingEvaluationResult.FromFailure(TargetingFailureReason.InvalidState);

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return TargetingEvaluationResult.FromFailure(TargetingFailureReason.InvalidState);

            TargetingFailureReason reason = mode switch
            {
                TargetingMode.Strike => strikeAction == null
                    ? TargetingFailureReason.InvalidState
                    : strikeAction.GetStrikeTargetFailure(actor, target),

                TargetingMode.ReadyStrike => TargetingFailureReason.ModeNotSupported,

                TargetingMode.Trip => tripAction == null
                    ? TargetingFailureReason.InvalidState
                    : tripAction.GetTripTargetFailure(actor, target),

                TargetingMode.Shove => shoveAction == null
                    ? TargetingFailureReason.InvalidState
                    : shoveAction.GetShoveTargetFailure(actor, target),

                TargetingMode.Grapple => grappleAction == null
                    ? TargetingFailureReason.InvalidState
                    : grappleAction.GetGrappleTargetFailure(actor, target),

                TargetingMode.Reposition => repositionAction == null
                    ? TargetingFailureReason.InvalidState
                    : repositionAction.GetRepositionTargetFailure(actor, target),

                TargetingMode.Escape => escapeAction == null
                    ? TargetingFailureReason.InvalidState
                    : escapeAction.GetEscapeTargetFailure(actor, target),

                TargetingMode.Demoralize => demoralizeAction == null
                    ? TargetingFailureReason.InvalidState
                    : demoralizeAction.GetDemoralizeTargetFailure(actor, target),

                TargetingMode.Aid => aidAction == null
                    ? TargetingFailureReason.InvalidState
                    : aidAction.GetAidTargetFailure(actor, target),

                TargetingMode.ForceBarrage => GetSpellTargetFailure(
                    actor,
                    target,
                    SpellCatalog.Get(SpellId.ForceBarrage)),

                TargetingMode.ElectricArc => GetSpellTargetFailure(
                    actor,
                    target,
                    SpellCatalog.Get(SpellId.ElectricArc)),

                TargetingMode.Snowball => GetSpellTargetFailure(
                    actor,
                    target,
                    SpellCatalog.Get(SpellId.Snowball)),

                TargetingMode.Fear => GetSpellTargetFailure(
                    actor,
                    target,
                    SpellCatalog.Get(SpellId.Fear)),

                TargetingMode.HealSingle => PreviewHealTargetDetailed(
                    target,
                    Mathf.Clamp(Mathf.Min(turnManager.ActionsRemaining, 2), 1, 2)).failureReason,

                TargetingMode.HarmSingle => PreviewHarmTargetDetailed(
                    target,
                    Mathf.Clamp(Mathf.Min(turnManager.ActionsRemaining, 3), 1, 3)).failureReason,

                _ => TargetingFailureReason.ModeNotSupported
            };

            return reason == TargetingFailureReason.None
                ? TargetingEvaluationResult.Success()
                : TargetingEvaluationResult.FromFailure(reason);
        }

        public bool TryPreviewSpellAreaCell(SpellId spellId, Vector3Int aimCell, out SpellAreaPreview preview)
        {
            preview = spellId switch
            {
                SpellId.BurningHands => BuildBurningHandsPreview(aimCell),
                SpellId.Heal => BuildHealAreaPreview(aimCell),
                SpellId.Harm => BuildHarmAreaPreview(aimCell),
                _ => SpellAreaPreview.Invalid(spellId, aimCell, TargetingFailureReason.ModeNotSupported)
            };

            return preview.IsValid;
        }

        public bool TryGetCurrentActorGridPosition(out Vector3Int cell)
        {
            cell = default;
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null)
                return false;

            cell = actorData.GridPosition;
            return true;
        }

        public bool TryExecuteStrideToCell(Vector3Int targetCell)
        {
            if (turnManager == null || entityManager == null || strideAction == null) return false;
            if (!CanActNow(FleeingActionAllowance.Stride)) return false;

            var actor = turnManager.CurrentEntity;
            var actorData = entityManager.Registry != null ? entityManager.Registry.Get(actor) : null;
            if (actorData == null || !actorData.IsAlive) return false;

            int availableActions = Mathf.Clamp(turnManager.ActionsRemaining, 0, 3);
            if (availableActions <= 0) return false;

            if (actorData.HasCondition(ConditionType.Fleeing))
            {
                if (!TryBuildFleeZone(actorData, availableActions, out _))
                    return false;
                if (!fleeingZoneBuffer.ContainsKey(targetCell))
                    return false;
            }

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

        public bool TryExecuteStepToCell(Vector3Int targetCell)
        {
            if (turnManager == null || entityManager == null || stepAction == null)
                return false;
            if (!CanActNow())
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Step");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            bool performed = stepAction.TryExecuteStep(actor, targetCell);
            if (!performed)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted();
                return false;
            }

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(StepAction.ActionCost);
            return true;
        }

        public bool TryPreviewStepToCell(Vector3Int targetCell, out StepPreviewResult preview)
        {
            preview = StepPreviewResult.Invalid(StepFailureReason.InvalidState, targetCell);

            if (turnManager == null || stepAction == null)
                return false;
            if (!CanActNow())
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            return stepAction.TryPreviewStep(actor, targetCell, out preview);
        }

        public bool TryExecuteJumpToCell(Vector3Int targetCell)
        {
            if (turnManager == null || entityManager == null || jumpAction == null)
                return false;
            if (!CanActNow())
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            if (!jumpAction.TryPreviewJump(actor, targetCell, out var preview))
                return false;
            if (preview.actionCost <= 0 || preview.actionCost > turnManager.ActionsRemaining)
                return false;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Jump");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            bool performed = jumpAction.TryExecuteJump(actor, targetCell, UnityRng.Shared, out _);
            if (!performed)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted();
                return false;
            }

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(preview.actionCost);
            return true;
        }

        public bool TryPreviewJumpToCell(Vector3Int targetCell, out JumpPreviewResult preview)
        {
            preview = JumpPreviewResult.Invalid(JumpFailureReason.InvalidState, targetCell);

            if (turnManager == null || jumpAction == null)
                return false;
            if (!CanActNow())
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            if (!jumpAction.TryPreviewJump(actor, targetCell, out preview))
                return false;

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
            if (!EnsureReactionPolicy()) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;
            if (strikeAction.GetStrikeTargetFailure(actor, target) != TargetingFailureReason.None) return false;

            int aidCircumstanceBonus = ResolveAidBonusForStrike(actor);

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Strike");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            var phase = strikeAction.ResolveAttackRoll(actor, target, UnityRng.Shared, aidCircumstanceBonus);
            if (!phase.HasValue)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted();
                return false;
            }

            var resolved = strikeAction.DetermineHitAndDamage(phase.Value, target, UnityRng.Shared);
            int damageReduction = ResolvePostHitReactionReduction(resolved);

            bool performed = strikeAction.ApplyStrikeDamage(resolved, damageReduction);

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

        public bool TryExecuteReadyStrike()
        {
            if (turnManager == null || entityManager == null || readyStrikeAction == null) return false;
            if (!CanActNow()) return false;
            if (turnManager.ActionsRemaining < ReadyStrikeAction.ActionCost) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.ReadyStrike");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            bool prepared = readyStrikeAction.TryPrepareReadiedStrike(
                actor,
                turnManager.RoundNumber,
                turnManager.CurrentReadyTriggerMode);
            if (!prepared)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted();
                return false;
            }

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(ReadyStrikeAction.ActionCost);
            return true;
        }

        private int ResolvePostHitReactionReduction(StrikePhaseResult resolved)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return 0;

            var ledger = turnManager.ReactionTriggerWindowLedger;
            var token = ledger != null
                ? ledger.OpenWindow(
                    TriggerWindowType.PostHitDamage,
                    source: resolved.attacker,
                    target: resolved.target)
                : default;

            try
            {
                return ReactionBroker.ResolvePostHitReductionSync(
                    resolved,
                    turnManager.InitiativeOrder,
                    handle => entityManager.Registry.Get(handle),
                    handle => turnManager.CanUseReaction(handle),
                    reactionPolicy,
                    shieldBlockAction,
                    reactionBuffer,
                    "Executor",
                    triggerWindowLedger: ledger,
                    triggerWindowToken: token);
            }
            finally
            {
                if (ledger != null && token.IsValid)
                    ledger.CloseWindow(token);
            }
        }

        private bool EnsureReactionPolicy()
        {
            if (reactionPolicy != null) return true;
            reactionPolicy = new ModalReactionPolicy(reactionPromptController);
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

            AbortInterruptedExecutingAction(actor, endTurn: actorData == null || !actorData.IsAlive);
            return false;
        }

        private void AbortInterruptedExecutingAction(EntityHandle actor, bool endTurn = true)
        {
            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif

            if (turnManager == null)
                return;

            turnManager.ActionCompleted();

            if (endTurn
                && actor == turnManager.CurrentEntity
                && (turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn))
            {
                turnManager.EndTurn();
            }
        }

        public bool TryExecuteStand()
        {
            if (turnManager == null || standAction == null) return false;
            if (!CanActNow(FleeingActionAllowance.Stand)) return false;

            var actor = turnManager.CurrentEntity;
            if (!standAction.CanStand(actor)) return false;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Stand");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            if (!TryContinueAfterActionStartReactions(
                    actor,
                    actionName: "Stand",
                    actionKind: CombatActionKind.Stand,
                    traits: CombatActionTraitFlags.None,
                    actionCost: StandAction.ActionCost))
            {
                return false;
            }

            if (!standAction.TryStand(actor))
            {
                if (entityManager != null && entityManager.Registry != null)
                {
                    var actorData = entityManager.Registry.Get(actor);
                    if (actorData == null || !actorData.IsAlive)
                    {
                        AbortInterruptedExecutingAction(actor);
                        return false;
                    }
                }

                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted();
                return false;
            }

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(StandAction.ActionCost);
            return true;
        }

        public bool TryExecuteTrip(EntityHandle target)
        {
            if (turnManager == null || entityManager == null || tripAction == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;
            if (tripAction.GetTripTargetFailure(actor, target) != TargetingFailureReason.None) return false;
            int aidCircumstanceBonus = ResolveAidBonusForSkillCheck(actor, SkillType.Athletics, "Trip");

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Trip");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            var degree = tripAction.TryTrip(actor, target, UnityRng.Shared, aidCircumstanceBonus);
            if (!degree.HasValue)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted(); // rollback (invalid attempt)
                return false;
            }

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(TripAction.ActionCost);
            return true;
        }

        public bool TryExecuteDemoralize(EntityHandle target)
        {
            if (turnManager == null || entityManager == null || demoralizeAction == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;
            if (demoralizeAction.GetDemoralizeTargetFailure(actor, target) != TargetingFailureReason.None) return false;
            int aidCircumstanceBonus = ResolveAidBonusForSkillCheck(actor, SkillType.Intimidation, "Demoralize");

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Demoralize");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            var degree = demoralizeAction.TryDemoralize(actor, target, UnityRng.Shared, aidCircumstanceBonus);
            if (!degree.HasValue)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted(); // rollback (invalid attempt)
                return false;
            }

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(DemoralizeAction.ActionCost);
            return true;
        }

        public bool CanPrepareAid()
        {
            if (!CanActNow()) return false;
            if (entityManager == null || entityManager.Registry == null || aidAction == null) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive) return false;

            foreach (var targetData in entityManager.Registry.GetAll())
            {
                if (targetData == null || !targetData.IsAlive || !targetData.Handle.IsValid) continue;
                if (targetData.Handle == actor) continue;

                if (aidAction.GetAidTargetFailure(actor, targetData.Handle) == TargetingFailureReason.None)
                    return true;
            }

            return false;
        }

        public bool TryExecuteAid(EntityHandle ally)
        {
            if (turnManager == null || entityManager == null || aidAction == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Aid");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            bool prepared = aidAction.TryPrepareAid(actor, ally, turnManager.RoundNumber, turnManager.AidService);
            if (!prepared)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted(); // rollback (invalid attempt)
                return false;
            }

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(AidAction.ActionCost);
            return true;
        }

        public bool TryExecuteShove(EntityHandle target)
        {
            if (turnManager == null || entityManager == null || shoveAction == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;
            if (shoveAction.GetShoveTargetFailure(actor, target) != TargetingFailureReason.None) return false;
            int aidCircumstanceBonus = ResolveAidBonusForSkillCheck(actor, SkillType.Athletics, "Shove");

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Shove");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            var degree = shoveAction.TryShove(actor, target, UnityRng.Shared, aidCircumstanceBonus);
            if (!degree.HasValue)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted(); // rollback (invalid attempt)
                return false;
            }

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(ShoveAction.ActionCost);
            return true;
        }

        public bool TryExecuteGrapple(EntityHandle target)
        {
            if (turnManager == null || entityManager == null || grappleAction == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;
            if (grappleAction.GetGrappleTargetFailure(actor, target) != TargetingFailureReason.None) return false;
            int aidCircumstanceBonus = ResolveAidBonusForSkillCheck(actor, SkillType.Athletics, "Grapple");

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Grapple");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            var degree = grappleAction.TryGrapple(actor, target, UnityRng.Shared, aidCircumstanceBonus);
            if (!degree.HasValue)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted(); // rollback (invalid attempt)
                return false;
            }

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(GrappleAction.ActionCost);
            return true;
        }

        public RepositionTargetSelectionResult TryBeginRepositionTargetSelection(EntityHandle target)
        {
            if (turnManager == null || entityManager == null || repositionAction == null)
                return RepositionTargetSelectionResult.Rejected;
            if (hasPendingRepositionSelection)
                return RepositionTargetSelectionResult.Rejected;
            if (!CanActNow())
                return RepositionTargetSelectionResult.Rejected;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return RepositionTargetSelectionResult.Rejected;
            if (repositionAction.GetRepositionTargetFailure(actor, target) != TargetingFailureReason.None)
                return RepositionTargetSelectionResult.Rejected;

            int aidCircumstanceBonus = ResolveAidBonusForSkillCheck(actor, SkillType.Athletics, "Reposition");

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Reposition");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            var degree = repositionAction.ResolveRepositionCheck(
                actor,
                target,
                out pendingRepositionContext,
                UnityRng.Shared,
                aidCircumstanceBonus);
            if (!degree.HasValue)
            {
                ResetPendingRepositionState(rollbackActionLock: true);
                return RepositionTargetSelectionResult.Rejected;
            }

            if (degree.Value != DegreeOfSuccess.Success && degree.Value != DegreeOfSuccess.CriticalSuccess)
            {
                ResetPendingRepositionState(rollbackActionLock: false);
                turnManager.CompleteActionWithCost(RepositionAction.ActionCost);
                return RepositionTargetSelectionResult.ResolvedAndClear;
            }

            pendingRepositionDestinations.Clear();
            bool anyDestinations = repositionAction.TryGetValidRepositionDestinations(
                actor,
                target,
                pendingRepositionContext.maxMoveFeet,
                pendingRepositionDestinations);

            if (!anyDestinations || pendingRepositionDestinations.Count <= 0)
            {
                // "Up to X feet" allows 0 feet if no legal destination exists.
                ResetPendingRepositionState(rollbackActionLock: false);
                turnManager.CompleteActionWithCost(RepositionAction.ActionCost);
                return RepositionTargetSelectionResult.ResolvedAndClear;
            }

            hasPendingRepositionSelection = true;
            return RepositionTargetSelectionResult.EnterCellSelection;
        }

        public bool TryConfirmRepositionDestination(Vector3Int destinationCell)
        {
            if (!hasPendingRepositionSelection || repositionAction == null || turnManager == null)
                return false;

            bool moved = repositionAction.TryApplyRepositionMove(
                pendingRepositionContext.actor,
                pendingRepositionContext.target,
                destinationCell,
                in pendingRepositionContext);

            if (!moved)
                return false;

            ResetPendingRepositionState(rollbackActionLock: false);
            // Guard: if TurnManager lock was force-released by watchdog, don't call CompleteActionWithCost
            // (it would silently fail anyway, but this makes the intent explicit).
            if (turnManager.State == TurnState.ExecutingAction)
                turnManager.CompleteActionWithCost(RepositionAction.ActionCost);
            else
                Debug.LogWarning("[Executor] Reposition confirm: action lock already released (watchdog?). Action cost not deducted.");
            return true;
        }

        public void CancelPendingRepositionSelection()
        {
            if (!hasPendingRepositionSelection || turnManager == null)
                return;

            // Check already resolved; player cancels destination selection => no movement, action still spent.
            ResetPendingRepositionState(rollbackActionLock: false);
            if (turnManager.State == TurnState.ExecutingAction)
                turnManager.CompleteActionWithCost(RepositionAction.ActionCost);
            else
                Debug.LogWarning("[Executor] Reposition cancel: action lock already released (watchdog?). Action cost not deducted.");
        }

        public bool TryExecuteEscape(EntityHandle grappler)
        {
            if (turnManager == null || entityManager == null || escapeAction == null) return false;
            if (!CanActNow(FleeingActionAllowance.Escape)) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;
            if (escapeAction.GetEscapeTargetFailure(actor, grappler) != TargetingFailureReason.None) return false;
            SkillType escapeSkill = escapeAction.ResolveEscapeSkill(actor);
            int aidCircumstanceBonus = ResolveAidBonusForSkillCheck(actor, escapeSkill, "Escape");

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Escape");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            var degree = escapeAction.TryEscape(actor, grappler, UnityRng.Shared, aidCircumstanceBonus);
            if (!degree.HasValue)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted(); // rollback (invalid attempt)
                return false;
            }

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(EscapeAction.ActionCost);
            return true;
        }


        public bool TryExecuteRaiseShield()
        {
            if (turnManager == null || entityManager == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            bool canRaisePhysicalShield = raiseShieldAction != null && raiseShieldAction.CanRaiseShield(actor);
            if (!canRaisePhysicalShield) return false;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.RaiseShield");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            bool raised = raiseShieldAction.TryRaiseShield(actor);
            if (!raised)
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
            turnManager.CompleteActionWithCost(RaiseShieldAction.ActionCost);
            return true;
        }

        public bool TryExecuteCastShieldSpell(RaiseShieldSpellMode preferredSpellMode)
        {
            if (turnManager == null || entityManager == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            bool canCastStandardShield = standardShieldAction != null && standardShieldAction.CanCastStandardShield(actor);
            bool canCastGlassShield = glassShieldAction != null && glassShieldAction.CanCastGlassShield(actor);
            if (!canCastStandardShield && !canCastGlassShield) return false;

            bool executeStandard;
            if (canCastStandardShield && canCastGlassShield)
                executeStandard = preferredSpellMode == RaiseShieldSpellMode.Standard;
            else
                executeStandard = canCastStandardShield;

            string actionSource = executeStandard ? "Player.StandardShield" : "Player.GlassShield";

            executingActor = actor;
            turnManager.BeginActionExecution(actor, actionSource);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            if (!TryContinueAfterActionStartReactions(
                    actor,
                    actionName: executeStandard ? "Shield" : "Glass Shield",
                    actionKind: CombatActionKind.Spell,
                    traits: CombatActionTraitFlags.None,
                    actionCost: RaiseShieldAction.ActionCost))
            {
                return false;
            }

            bool casted = executeStandard
                ? standardShieldAction.TryCastStandardShield(actor)
                : glassShieldAction.TryCastGlassShield(actor);
            if (!casted)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted();
                return false;
            }

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(RaiseShieldAction.ActionCost);
            return true;
        }

        public bool TryBeginForceBarrage(int actionCount)
        {
            if (turnManager == null || entityManager == null) return false;
            if (!CanActNow()) return false;

            int clampedActionCount = Mathf.Clamp(actionCount, 1, 3);
            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;

            var actorData = entityManager.Registry != null ? entityManager.Registry.Get(actor) : null;
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsForceBarrage) return false;

            return turnManager.ActionsRemaining >= clampedActionCount;
        }

        public bool TryConfirmForceBarrage(IReadOnlyList<EntityHandle> targets, int actionCount, IRng rng = null)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null) return false;
            if (!CanActNow()) return false;

            int clampedActionCount = Mathf.Clamp(actionCount, 1, 3);
            if (targets == null || targets.Count != clampedActionCount) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsForceBarrage) return false;
            if (turnManager.ActionsRemaining < clampedActionCount) return false;

            var definition = SpellCatalog.Get(SpellId.ForceBarrage);
            for (int i = 0; i < targets.Count; i++)
            {
                if (GetSpellTargetFailure(actor, targets[i], definition) != TargetingFailureReason.None)
                    return false;
            }

            rng ??= UnityRng.Shared;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.ForceBarrage");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            if (!TryContinueAfterActionStartReactions(
                    actor,
                    definition.actionName,
                    CombatActionKind.Spell,
                    SpellCatalog.HasManipulateTrait(SpellId.ForceBarrage, clampedActionCount) ? CombatActionTraitFlags.Manipulate : CombatActionTraitFlags.None,
                    clampedActionCount))
            {
                return false;
            }

            var groupedShardRolls = new Dictionary<EntityHandle, List<int>>(targets.Count);
            for (int i = 0; i < targets.Count; i++)
            {
                if (!groupedShardRolls.TryGetValue(targets[i], out var shardRolls))
                {
                    shardRolls = new List<int>(clampedActionCount);
                    groupedShardRolls.Add(targets[i], shardRolls);
                }

                shardRolls.Add(rng.RollDie(4) + 1);
            }

            bool canResolveReactions = turnManager != null && shieldBlockAction != null && EnsureReactionPolicy();
            var outcomes = new SpellResolvedTargetOutcome[groupedShardRolls.Count];
            int outcomeIndex = 0;

            foreach (var pair in groupedShardRolls)
            {
                var targetData = entityManager.Registry.Get(pair.Key);
                if (targetData == null || !targetData.IsAlive)
                {
                    executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    executionStartTime = -1f;
#endif
                    turnManager.ActionCompleted();
                    return false;
                }

                int[] shardRolls = pair.Value.ToArray();
                int rolledDamage = 0;
                for (int i = 0; i < shardRolls.Length; i++)
                    rolledDamage += shardRolls[i];

                int hpBefore = Mathf.Max(0, targetData.CurrentHP);
                int appliedDamage = DamageApplicationService.ApplyDamage(
                    actor,
                    pair.Key,
                    rolledDamage,
                    definition.damageType,
                    definition.actionName,
                    isCritical: false,
                    entityManager,
                    eventBus,
                    initiativeOrder: canResolveReactions ? turnManager.InitiativeOrder : null,
                    getEntity: canResolveReactions ? handle => entityManager.Registry.Get(handle) : null,
                    canUseReaction: canResolveReactions ? handle => turnManager.CanUseReaction(handle) : null,
                    reactionPolicy: canResolveReactions ? reactionPolicy : null,
                    shieldBlockAction: canResolveReactions ? shieldBlockAction : null,
                    reactionBuffer: canResolveReactions ? reactionBuffer : null,
                    reactionPhase: ReactionTriggerPhase.PostHit,
                    reactionOwnerTag: "PlayerActionExecutor.ForceBarrage");

                outcomes[outcomeIndex++] = new SpellResolvedTargetOutcome(
                    pair.Key,
                    shardRolls.Length,
                    shardRolls,
                    rolledDamage,
                    attackResult: null,
                    saveResult: null,
                    appliedConditionType: null,
                    appliedConditionValue: 0,
                    appliedConditionRounds: 0,
                    resolvedDamage: rolledDamage,
                    appliedDamage: appliedDamage,
                    hpBefore: hpBefore,
                    hpAfter: Mathf.Max(0, targetData.CurrentHP),
                    targetDefeated: !targetData.IsAlive);
            }

            eventBus?.PublishSpellResolved(new SpellResolvedEvent(
                SpellId.ForceBarrage,
                actor,
                clampedActionCount,
                spellDc: 0,
                spellAttackModifier: 0,
                rolledDamage: 0,
                targetOutcomes: outcomes));

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(clampedActionCount);
            return true;
        }

        public bool TryBeginElectricArc()
        {
            if (turnManager == null || entityManager == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;

            var actorData = entityManager.Registry != null ? entityManager.Registry.Get(actor) : null;
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsElectricArc) return false;

            return turnManager.ActionsRemaining >= SpellCatalog.Get(SpellId.ElectricArc).minActionCost;
        }

        public bool TryConfirmElectricArc(IReadOnlyList<EntityHandle> targets, IRng rng = null)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null) return false;
            if (!CanActNow()) return false;
            if (targets == null || targets.Count <= 0 || targets.Count > 2) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsElectricArc) return false;

            var definition = SpellCatalog.Get(SpellId.ElectricArc);
            if (turnManager.ActionsRemaining < definition.minActionCost) return false;

            for (int i = 0; i < targets.Count; i++)
            {
                if (GetSpellTargetFailure(actor, targets[i], definition) != TargetingFailureReason.None)
                    return false;

                for (int j = i + 1; j < targets.Count; j++)
                {
                    if (targets[i] == targets[j])
                        return false;
                }
            }

            rng ??= UnityRng.Shared;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.ElectricArc");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            if (!TryContinueAfterActionStartReactions(
                    actor,
                    definition.actionName,
                    CombatActionKind.Spell,
                    SpellCatalog.HasManipulateTrait(SpellId.ElectricArc, definition.minActionCost) ? CombatActionTraitFlags.Manipulate : CombatActionTraitFlags.None,
                    definition.minActionCost))
            {
                return false;
            }

            int spellDc = SpellcastingRules.ComputeWizardSpellDc(actorData);
            int rolledDamage = rng.RollDie(4) + rng.RollDie(4);
            bool canResolveReactions = turnManager != null && shieldBlockAction != null && EnsureReactionPolicy();
            var outcomes = new SpellResolvedTargetOutcome[targets.Count];

            for (int i = 0; i < targets.Count; i++)
            {
                var targetData = entityManager.Registry.Get(targets[i]);
                if (targetData == null || !targetData.IsAlive)
                {
                    executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    executionStartTime = -1f;
#endif
                    turnManager.ActionCompleted();
                    return false;
                }

                var save = CheckResolver.RollSave(targetData, SaveType.Reflex, spellDc, rng);
                int resolvedDamage = CheckResolver.ApplyBasicSaveDamage(rolledDamage, save.degree);
                int hpBefore = Mathf.Max(0, targetData.CurrentHP);
                int appliedDamage = DamageApplicationService.ApplyDamage(
                    actor,
                    targets[i],
                    resolvedDamage,
                    definition.damageType,
                    definition.actionName,
                    isCritical: save.degree == DegreeOfSuccess.CriticalFailure,
                    entityManager,
                    eventBus,
                    initiativeOrder: canResolveReactions ? turnManager.InitiativeOrder : null,
                    getEntity: canResolveReactions ? handle => entityManager.Registry.Get(handle) : null,
                    canUseReaction: canResolveReactions ? handle => turnManager.CanUseReaction(handle) : null,
                    reactionPolicy: canResolveReactions ? reactionPolicy : null,
                    shieldBlockAction: canResolveReactions ? shieldBlockAction : null,
                    reactionBuffer: canResolveReactions ? reactionBuffer : null,
                    reactionPhase: ReactionTriggerPhase.PostHit,
                    reactionOwnerTag: "PlayerActionExecutor.ElectricArc");

                outcomes[i] = new SpellResolvedTargetOutcome(
                    targets[i],
                    shardCount: 0,
                    shardRolls: null,
                    rolledDamage: rolledDamage,
                    attackResult: null,
                    saveResult: save,
                    appliedConditionType: null,
                    appliedConditionValue: 0,
                    appliedConditionRounds: 0,
                    resolvedDamage: resolvedDamage,
                    appliedDamage: appliedDamage,
                    hpBefore: hpBefore,
                    hpAfter: Mathf.Max(0, targetData.CurrentHP),
                    targetDefeated: !targetData.IsAlive);
            }

            eventBus?.PublishSpellResolved(new SpellResolvedEvent(
                SpellId.ElectricArc,
                actor,
                definition.minActionCost,
                spellDc,
                spellAttackModifier: 0,
                rolledDamage,
                outcomes));

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(definition.minActionCost);
            return true;
        }

        public bool TryBeginSnowball()
        {
            if (turnManager == null || entityManager == null)
                return false;
            if (!CanActNow())
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = entityManager.Registry != null ? entityManager.Registry.Get(actor) : null;
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsSnowball)
                return false;

            return turnManager.ActionsRemaining >= SpellCatalog.Get(SpellId.Snowball).minActionCost;
        }

        public bool TryBeginBurningHands()
        {
            if (turnManager == null || entityManager == null)
                return false;
            if (!CanActNow())
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = entityManager.Registry != null ? entityManager.Registry.Get(actor) : null;
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsBurningHands)
                return false;

            return turnManager.ActionsRemaining >= SpellCatalog.Get(SpellId.BurningHands).minActionCost;
        }

        public bool TryBeginFear()
        {
            if (turnManager == null || entityManager == null)
                return false;
            if (!CanActNow())
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = entityManager.Registry != null ? entityManager.Registry.Get(actor) : null;
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsFear)
                return false;

            return turnManager.ActionsRemaining >= SpellCatalog.Get(SpellId.Fear).minActionCost;
        }

        public bool TryBeginHeal(int actionCount)
        {
            if (turnManager == null || entityManager == null)
                return false;
            if (!CanActNow())
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = entityManager.Registry != null ? entityManager.Registry.Get(actor) : null;
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsHeal)
                return false;

            int clampedActionCount = Mathf.Clamp(actionCount, 1, 3);
            return turnManager.ActionsRemaining >= clampedActionCount;
        }

        public bool TryBeginHarm(int actionCount)
        {
            if (turnManager == null || entityManager == null)
                return false;
            if (!CanActNow())
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = entityManager.Registry != null ? entityManager.Registry.Get(actor) : null;
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsHarm)
                return false;

            int clampedActionCount = Mathf.Clamp(actionCount, 1, 3);
            return turnManager.ActionsRemaining >= clampedActionCount;
        }

        public bool TryConfirmSnowball(EntityHandle target, IRng rng = null)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return false;
            if (!CanActNow())
                return false;
            if (!target.IsValid)
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsSnowball)
                return false;

            var definition = SpellCatalog.Get(SpellId.Snowball);
            if (turnManager.ActionsRemaining < definition.minActionCost)
                return false;
            if (GetSpellTargetFailure(actor, target, definition) != TargetingFailureReason.None)
                return false;

            rng ??= UnityRng.Shared;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Snowball");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            if (!TryContinueAfterActionStartReactions(
                    actor,
                    definition.actionName,
                    CombatActionKind.Spell,
                    SpellCatalog.HasManipulateTrait(SpellId.Snowball, definition.minActionCost) ? CombatActionTraitFlags.Manipulate : CombatActionTraitFlags.None,
                    definition.minActionCost))
            {
                return false;
            }

            var targetData = entityManager.Registry.Get(target);
            if (targetData == null || !targetData.IsAlive)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted();
                return false;
            }

            int spellAttackModifier = SpellcastingRules.ComputeWizardSpellAttackModifier(actorData);
            var attackResult = CheckResolver.RollCheck(
                spellAttackModifier,
                targetData.EffectiveAC,
                CheckSource.Custom("SPA"),
                rng);

            int rolledDamage = rng.RollDie(4) + rng.RollDie(4);
            int resolvedDamage = attackResult.degree switch
            {
                DegreeOfSuccess.CriticalSuccess => rolledDamage * 2,
                DegreeOfSuccess.Success => rolledDamage,
                _ => 0
            };

            ConditionType? appliedConditionType = null;
            int appliedConditionValue = 0;
            int appliedConditionRounds = 0;
            conditionDeltaBuffer.Clear();

            if (attackResult.degree == DegreeOfSuccess.CriticalSuccess)
            {
                appliedConditionType = ConditionType.SpeedPenalty;
                appliedConditionValue = 10;
                appliedConditionRounds = 1;
            }
            else if (attackResult.degree == DegreeOfSuccess.Success)
            {
                appliedConditionType = ConditionType.SpeedPenalty;
                appliedConditionValue = 5;
                appliedConditionRounds = 1;
            }

            if (appliedConditionType.HasValue)
            {
                conditionService.AddOrRefresh(
                    targetData,
                    appliedConditionType.Value,
                    appliedConditionValue,
                    appliedConditionRounds,
                    conditionDeltaBuffer);
            }

            bool canResolveReactions = turnManager != null && shieldBlockAction != null && EnsureReactionPolicy();
            int hpBefore = Mathf.Max(0, targetData.CurrentHP);
            int appliedDamage = 0;
            if (resolvedDamage > 0)
            {
                appliedDamage = DamageApplicationService.ApplyDamage(
                    actor,
                    target,
                    resolvedDamage,
                    definition.damageType,
                    definition.actionName,
                    isCritical: attackResult.degree == DegreeOfSuccess.CriticalSuccess,
                    entityManager,
                    eventBus,
                    initiativeOrder: canResolveReactions ? turnManager.InitiativeOrder : null,
                    getEntity: canResolveReactions ? handle => entityManager.Registry.Get(handle) : null,
                    canUseReaction: canResolveReactions ? handle => turnManager.CanUseReaction(handle) : null,
                    reactionPolicy: canResolveReactions ? reactionPolicy : null,
                    shieldBlockAction: canResolveReactions ? shieldBlockAction : null,
                    reactionBuffer: canResolveReactions ? reactionBuffer : null,
                    reactionPhase: ReactionTriggerPhase.PostHit,
                    reactionOwnerTag: "PlayerActionExecutor.Snowball");
            }

            var outcomes = new[]
            {
                new SpellResolvedTargetOutcome(
                    target,
                    shardCount: 0,
                    shardRolls: null,
                    rolledDamage: rolledDamage,
                    attackResult: attackResult,
                    saveResult: null,
                    appliedConditionType: appliedConditionType,
                    appliedConditionValue: appliedConditionValue,
                    appliedConditionRounds: appliedConditionRounds,
                    resolvedDamage: resolvedDamage,
                    appliedDamage: appliedDamage,
                    hpBefore: hpBefore,
                    hpAfter: Mathf.Max(0, targetData.CurrentHP),
                    targetDefeated: !targetData.IsAlive)
            };

            eventBus?.PublishSpellResolved(new SpellResolvedEvent(
                SpellId.Snowball,
                actor,
                definition.minActionCost,
                spellDc: 0,
                spellAttackModifier: spellAttackModifier,
                rolledDamage: rolledDamage,
                targetOutcomes: outcomes));
            PublishConditionDeltas();

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(definition.minActionCost);
            return true;
        }

        public bool TryConfirmFear(EntityHandle target, IRng rng = null)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return false;
            if (!CanActNow())
                return false;
            if (!target.IsValid)
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsFear)
                return false;

            var definition = SpellCatalog.Get(SpellId.Fear);
            if (turnManager.ActionsRemaining < definition.minActionCost)
                return false;
            if (GetSpellTargetFailure(actor, target, definition) != TargetingFailureReason.None)
                return false;

            rng ??= UnityRng.Shared;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Fear");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            if (!TryContinueAfterActionStartReactions(
                    actor,
                    definition.actionName,
                    CombatActionKind.Spell,
                    SpellCatalog.HasManipulateTrait(SpellId.Fear, definition.minActionCost) ? CombatActionTraitFlags.Manipulate : CombatActionTraitFlags.None,
                    definition.minActionCost))
            {
                return false;
            }

            var targetData = entityManager.Registry.Get(target);
            if (targetData == null || !targetData.IsAlive)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted();
                return false;
            }

            int spellDc = SpellcastingRules.ComputeWizardSpellDc(actorData);
            var save = CheckResolver.RollSave(targetData, SaveType.Will, spellDc, rng);
            int hpBefore = Mathf.Max(0, targetData.CurrentHP);

            ConditionType? appliedConditionType = null;
            bool applyFleeing = save.degree == DegreeOfSuccess.CriticalFailure;
            int appliedConditionValue = save.degree switch
            {
                DegreeOfSuccess.Success => 1,
                DegreeOfSuccess.Failure => 2,
                DegreeOfSuccess.CriticalFailure => 3,
                _ => 0
            };

            conditionDeltaBuffer.Clear();
            if (appliedConditionValue > 0)
            {
                appliedConditionType = ConditionType.Frightened;
                conditionService.AddOrRefresh(
                    targetData,
                    ConditionType.Frightened,
                    appliedConditionValue,
                    rounds: -1,
                    conditionDeltaBuffer);

                if (applyFleeing)
                {
                    conditionService.AddOrRefresh(
                        targetData,
                        ConditionType.Fleeing,
                        value: 0,
                        rounds: 1,
                        conditionDeltaBuffer);
                    targetData.SetFleeingSource(actor);
                }
            }

            var outcomes = new[]
            {
                new SpellResolvedTargetOutcome(
                    target,
                    shardCount: 0,
                    shardRolls: null,
                    rolledDamage: 0,
                    attackResult: null,
                    saveResult: save,
                    appliedConditionType: appliedConditionType,
                    appliedConditionValue: appliedConditionValue,
                    appliedConditionRounds: appliedConditionValue > 0 ? -1 : 0,
                    resolvedDamage: 0,
                    appliedDamage: 0,
                    hpBefore: hpBefore,
                    hpAfter: Mathf.Max(0, targetData.CurrentHP),
                    targetDefeated: !targetData.IsAlive)
            };

            eventBus?.PublishSpellResolved(new SpellResolvedEvent(
                SpellId.Fear,
                actor,
                definition.minActionCost,
                spellDc,
                spellAttackModifier: 0,
                rolledDamage: 0,
                targetOutcomes: outcomes));
            PublishConditionDeltas();

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(definition.minActionCost);
            return true;
        }

        public TargetingEvaluationResult PreviewHealTargetDetailed(EntityHandle target, int actionCount)
        {
            if (!target.IsValid)
                return TargetingEvaluationResult.FromFailure(TargetingFailureReason.InvalidTarget);
            if (turnManager == null || entityManager == null)
                return TargetingEvaluationResult.FromFailure(TargetingFailureReason.InvalidState);

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return TargetingEvaluationResult.FromFailure(TargetingFailureReason.InvalidState);

            var failure = GetHealTargetFailure(actor, target, Mathf.Clamp(actionCount, 1, 3));
            return failure == TargetingFailureReason.None
                ? TargetingEvaluationResult.Success()
                : TargetingEvaluationResult.FromFailure(failure);
        }

        public bool TryConfirmHeal(EntityHandle target, int actionCount, IRng rng = null)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return false;
            if (!CanActNow())
                return false;
            if (!target.IsValid)
                return false;

            int clampedActionCount = Mathf.Clamp(actionCount, 1, 2);
            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsHeal)
                return false;
            if (turnManager.ActionsRemaining < clampedActionCount)
                return false;
            if (GetHealTargetFailure(actor, target, clampedActionCount) != TargetingFailureReason.None)
                return false;

            rng ??= UnityRng.Shared;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Heal");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            if (!TryContinueAfterActionStartReactions(
                    actor,
                    "Heal",
                    CombatActionKind.Spell,
                    SpellCatalog.HasManipulateTrait(SpellId.Heal, clampedActionCount) ? CombatActionTraitFlags.Manipulate : CombatActionTraitFlags.None,
                    clampedActionCount))
            {
                return false;
            }

            var targetData = entityManager.Registry.Get(target);
            if (targetData == null || !targetData.IsAlive)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted();
                return false;
            }

            var definition = SpellCatalog.Get(SpellId.Heal);
            int spellDc = SpellcastingRules.ComputeWizardSpellDc(actorData);
            int rolledAmount = rng.RollDie(8);
            int totalHealing = rolledAmount + (clampedActionCount >= 2 ? 8 : 0);
            int hpBefore = Mathf.Max(0, targetData.CurrentHP);
            int resolvedDamage = 0;
            int appliedDamage = 0;
            int appliedHealing = 0;
            CheckResult? saveResult = null;
            bool canResolveReactions = turnManager != null && shieldBlockAction != null && EnsureReactionPolicy();

            if (targetData.VitalityAffinity == VitalityAffinity.Undead)
            {
                saveResult = CheckResolver.RollSave(targetData, SaveType.Fortitude, spellDc, rng);
                resolvedDamage = CheckResolver.ApplyBasicSaveDamage(rolledAmount, saveResult.Value.degree);
                if (resolvedDamage > 0)
                {
                    appliedDamage = DamageApplicationService.ApplyDamage(
                        actor,
                        target,
                        resolvedDamage,
                        definition.damageType,
                        definition.actionName,
                        isCritical: saveResult.Value.degree == DegreeOfSuccess.CriticalFailure,
                        entityManager,
                        eventBus,
                        initiativeOrder: canResolveReactions ? turnManager.InitiativeOrder : null,
                        getEntity: canResolveReactions ? handle => entityManager.Registry.Get(handle) : null,
                        canUseReaction: canResolveReactions ? handle => turnManager.CanUseReaction(handle) : null,
                        reactionPolicy: canResolveReactions ? reactionPolicy : null,
                        shieldBlockAction: canResolveReactions ? shieldBlockAction : null,
                        reactionBuffer: canResolveReactions ? reactionBuffer : null,
                        reactionPhase: ReactionTriggerPhase.PostHit,
                        reactionOwnerTag: "PlayerActionExecutor.Heal");
                }
            }
            else
            {
                appliedHealing = HealingApplicationService.ApplyHealing(
                    actor,
                    target,
                    totalHealing,
                    definition.actionName,
                    entityManager,
                    eventBus);
            }

            var outcomes = new[]
            {
                new SpellResolvedTargetOutcome(
                    target,
                    shardCount: 0,
                    shardRolls: null,
                    rolledDamage: targetData.VitalityAffinity == VitalityAffinity.Undead ? rolledAmount : totalHealing,
                    attackResult: null,
                    saveResult: saveResult,
                    appliedConditionType: null,
                    appliedConditionValue: 0,
                    appliedConditionRounds: 0,
                    resolvedDamage: resolvedDamage,
                    appliedDamage: appliedDamage,
                    appliedHealing: appliedHealing,
                    hpBefore: hpBefore,
                    hpAfter: Mathf.Max(0, targetData.CurrentHP),
                    targetDefeated: !targetData.IsAlive)
            };

            eventBus?.PublishSpellResolved(new SpellResolvedEvent(
                SpellId.Heal,
                actor,
                clampedActionCount,
                spellDc,
                spellAttackModifier: 0,
                rolledDamage: targetData.VitalityAffinity == VitalityAffinity.Undead ? rolledAmount : totalHealing,
                targetOutcomes: outcomes));

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(clampedActionCount);
            return true;
        }

        public bool TryConfirmHealArea(Vector3Int aimCell, IRng rng = null)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return false;
            if (!CanActNow())
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsHeal)
                return false;

            const int actionCost = 3;
            if (turnManager.ActionsRemaining < actionCost)
                return false;

            var preview = BuildHealAreaPreview(aimCell);
            if (!preview.IsValid)
                return false;

            rng ??= UnityRng.Shared;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Heal");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            if (!TryContinueAfterActionStartReactions(
                    actor,
                    "Heal",
                    CombatActionKind.Spell,
                    SpellCatalog.HasManipulateTrait(SpellId.Heal, actionCost) ? CombatActionTraitFlags.Manipulate : CombatActionTraitFlags.None,
                    actionCost))
            {
                return false;
            }

            var definition = SpellCatalog.Get(SpellId.Heal);
            int spellDc = SpellcastingRules.ComputeWizardSpellDc(actorData);
            int rolledAmount = rng.RollDie(8);
            bool canResolveReactions = turnManager != null && shieldBlockAction != null && EnsureReactionPolicy();
            var outcomes = new SpellResolvedTargetOutcome[preview.TargetCount];

            for (int i = 0; i < preview.TargetCount; i++)
            {
                var target = preview.targets[i];
                var targetData = entityManager.Registry.Get(target);
                if (targetData == null || !targetData.IsAlive)
                {
                    executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    executionStartTime = -1f;
#endif
                    turnManager.ActionCompleted();
                    return false;
                }

                int hpBefore = Mathf.Max(0, targetData.CurrentHP);
                int resolvedDamage = 0;
                int appliedDamage = 0;
                int appliedHealing = 0;
                CheckResult? saveResult = null;

                if (targetData.VitalityAffinity == VitalityAffinity.Undead)
                {
                    saveResult = CheckResolver.RollSave(targetData, SaveType.Fortitude, spellDc, rng);
                    resolvedDamage = CheckResolver.ApplyBasicSaveDamage(rolledAmount, saveResult.Value.degree);
                    if (resolvedDamage > 0)
                    {
                        appliedDamage = DamageApplicationService.ApplyDamage(
                            actor,
                            target,
                            resolvedDamage,
                            definition.damageType,
                            definition.actionName,
                            isCritical: saveResult.Value.degree == DegreeOfSuccess.CriticalFailure,
                            entityManager,
                            eventBus,
                            initiativeOrder: canResolveReactions ? turnManager.InitiativeOrder : null,
                            getEntity: canResolveReactions ? handle => entityManager.Registry.Get(handle) : null,
                            canUseReaction: canResolveReactions ? handle => turnManager.CanUseReaction(handle) : null,
                            reactionPolicy: canResolveReactions ? reactionPolicy : null,
                            shieldBlockAction: canResolveReactions ? shieldBlockAction : null,
                            reactionBuffer: canResolveReactions ? reactionBuffer : null,
                            reactionPhase: ReactionTriggerPhase.PostHit,
                            reactionOwnerTag: "PlayerActionExecutor.HealArea");
                    }
                }
                else
                {
                    appliedHealing = HealingApplicationService.ApplyHealing(
                        actor,
                        target,
                        rolledAmount,
                        definition.actionName,
                        entityManager,
                        eventBus);
                }

                outcomes[i] = new SpellResolvedTargetOutcome(
                    target,
                    shardCount: 0,
                    shardRolls: null,
                    rolledDamage: rolledAmount,
                    attackResult: null,
                    saveResult: saveResult,
                    appliedConditionType: null,
                    appliedConditionValue: 0,
                    appliedConditionRounds: 0,
                    resolvedDamage: resolvedDamage,
                    appliedDamage: appliedDamage,
                    hpBefore: hpBefore,
                    hpAfter: Mathf.Max(0, targetData.CurrentHP),
                    targetDefeated: !targetData.IsAlive,
                    appliedHealing: appliedHealing);
            }

            eventBus?.PublishSpellResolved(new SpellResolvedEvent(
                SpellId.Heal,
                actor,
                actionCost,
                spellDc,
                spellAttackModifier: 0,
                rolledDamage: rolledAmount,
                targetOutcomes: outcomes));

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(actionCost);
            return true;
        }

        public TargetingEvaluationResult PreviewHarmTargetDetailed(EntityHandle target, int actionCount)
        {
            if (!target.IsValid)
                return TargetingEvaluationResult.FromFailure(TargetingFailureReason.InvalidTarget);
            if (turnManager == null || entityManager == null)
                return TargetingEvaluationResult.FromFailure(TargetingFailureReason.InvalidState);

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return TargetingEvaluationResult.FromFailure(TargetingFailureReason.InvalidState);

            var failure = GetHarmTargetFailure(actor, target, Mathf.Clamp(actionCount, 1, 3));
            return failure == TargetingFailureReason.None
                ? TargetingEvaluationResult.Success()
                : TargetingEvaluationResult.FromFailure(failure);
        }

        public bool TryConfirmHarm(EntityHandle target, int actionCount, IRng rng = null)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return false;
            if (!CanActNow())
                return false;
            if (!target.IsValid)
                return false;

            int clampedActionCount = Mathf.Clamp(actionCount, 1, 2);
            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsHarm)
                return false;
            if (turnManager.ActionsRemaining < clampedActionCount)
                return false;
            if (GetHarmTargetFailure(actor, target, clampedActionCount) != TargetingFailureReason.None)
                return false;

            rng ??= UnityRng.Shared;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Harm");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            if (!TryContinueAfterActionStartReactions(
                    actor,
                    "Harm",
                    CombatActionKind.Spell,
                    SpellCatalog.HasManipulateTrait(SpellId.Harm, clampedActionCount) ? CombatActionTraitFlags.Manipulate : CombatActionTraitFlags.None,
                    clampedActionCount))
            {
                return false;
            }

            var targetData = entityManager.Registry.Get(target);
            if (targetData == null || !targetData.IsAlive)
            {
                executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                executionStartTime = -1f;
#endif
                turnManager.ActionCompleted();
                return false;
            }

            var definition = SpellCatalog.Get(SpellId.Harm);
            int spellDc = SpellcastingRules.ComputeWizardSpellDc(actorData);
            int rolledAmount = rng.RollDie(8);
            int totalDamage = rolledAmount + (clampedActionCount >= 2 ? 8 : 0);
            int hpBefore = Mathf.Max(0, targetData.CurrentHP);
            int resolvedDamage = 0;
            int appliedDamage = 0;
            int appliedHealing = 0;
            CheckResult? saveResult = null;
            bool canResolveReactions = turnManager != null && shieldBlockAction != null && EnsureReactionPolicy();

            if (targetData.VitalityAffinity == VitalityAffinity.Undead)
            {
                appliedHealing = HealingApplicationService.ApplyHealing(
                    actor,
                    target,
                    totalDamage,
                    definition.actionName,
                    entityManager,
                    eventBus);
            }
            else
            {
                saveResult = CheckResolver.RollSave(targetData, SaveType.Fortitude, spellDc, rng);
                resolvedDamage = CheckResolver.ApplyBasicSaveDamage(totalDamage, saveResult.Value.degree);
                if (resolvedDamage > 0)
                {
                    appliedDamage = DamageApplicationService.ApplyDamage(
                        actor,
                        target,
                        resolvedDamage,
                        definition.damageType,
                        definition.actionName,
                        isCritical: saveResult.Value.degree == DegreeOfSuccess.CriticalFailure,
                        entityManager,
                        eventBus,
                        initiativeOrder: canResolveReactions ? turnManager.InitiativeOrder : null,
                        getEntity: canResolveReactions ? handle => entityManager.Registry.Get(handle) : null,
                        canUseReaction: canResolveReactions ? handle => turnManager.CanUseReaction(handle) : null,
                        reactionPolicy: canResolveReactions ? reactionPolicy : null,
                        shieldBlockAction: canResolveReactions ? shieldBlockAction : null,
                        reactionBuffer: canResolveReactions ? reactionBuffer : null,
                        reactionPhase: ReactionTriggerPhase.PostHit,
                        reactionOwnerTag: "PlayerActionExecutor.Harm");
                }
            }

            var outcomes = new[]
            {
                new SpellResolvedTargetOutcome(
                    target,
                    shardCount: 0,
                    shardRolls: null,
                    rolledDamage: targetData.VitalityAffinity == VitalityAffinity.Undead ? totalDamage : totalDamage,
                    attackResult: null,
                    saveResult: saveResult,
                    appliedConditionType: null,
                    appliedConditionValue: 0,
                    appliedConditionRounds: 0,
                    resolvedDamage: resolvedDamage,
                    appliedDamage: appliedDamage,
                    hpBefore: hpBefore,
                    hpAfter: Mathf.Max(0, targetData.CurrentHP),
                    targetDefeated: !targetData.IsAlive,
                    appliedHealing: appliedHealing)
            };

            eventBus?.PublishSpellResolved(new SpellResolvedEvent(
                SpellId.Harm,
                actor,
                clampedActionCount,
                spellDc,
                spellAttackModifier: 0,
                rolledDamage: totalDamage,
                targetOutcomes: outcomes));

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(clampedActionCount);
            return true;
        }

        public bool TryConfirmHarmArea(Vector3Int aimCell, IRng rng = null)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return false;
            if (!CanActNow())
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsHarm)
                return false;

            const int actionCost = 3;
            if (turnManager.ActionsRemaining < actionCost)
                return false;

            var preview = BuildHarmAreaPreview(aimCell);
            if (!preview.IsValid)
                return false;

            rng ??= UnityRng.Shared;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Harm");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            if (!TryContinueAfterActionStartReactions(
                    actor,
                    "Harm",
                    CombatActionKind.Spell,
                    SpellCatalog.HasManipulateTrait(SpellId.Harm, actionCost) ? CombatActionTraitFlags.Manipulate : CombatActionTraitFlags.None,
                    actionCost))
            {
                return false;
            }

            var definition = SpellCatalog.Get(SpellId.Harm);
            int spellDc = SpellcastingRules.ComputeWizardSpellDc(actorData);
            int rolledAmount = rng.RollDie(8);
            bool canResolveReactions = turnManager != null && shieldBlockAction != null && EnsureReactionPolicy();
            var outcomes = new SpellResolvedTargetOutcome[preview.TargetCount];

            for (int i = 0; i < preview.TargetCount; i++)
            {
                var target = preview.targets[i];
                var targetData = entityManager.Registry.Get(target);
                if (targetData == null || !targetData.IsAlive)
                {
                    executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    executionStartTime = -1f;
#endif
                    turnManager.ActionCompleted();
                    return false;
                }

                int hpBefore = Mathf.Max(0, targetData.CurrentHP);
                int resolvedDamage = 0;
                int appliedDamage = 0;
                int appliedHealing = 0;
                CheckResult? saveResult = null;

                if (targetData.VitalityAffinity == VitalityAffinity.Undead)
                {
                    appliedHealing = HealingApplicationService.ApplyHealing(
                        actor,
                        target,
                        rolledAmount,
                        definition.actionName,
                        entityManager,
                        eventBus);
                }
                else
                {
                    saveResult = CheckResolver.RollSave(targetData, SaveType.Fortitude, spellDc, rng);
                    resolvedDamage = CheckResolver.ApplyBasicSaveDamage(rolledAmount, saveResult.Value.degree);
                    if (resolvedDamage > 0)
                    {
                        appliedDamage = DamageApplicationService.ApplyDamage(
                            actor,
                            target,
                            resolvedDamage,
                            definition.damageType,
                            definition.actionName,
                            isCritical: saveResult.Value.degree == DegreeOfSuccess.CriticalFailure,
                            entityManager,
                            eventBus,
                            initiativeOrder: canResolveReactions ? turnManager.InitiativeOrder : null,
                            getEntity: canResolveReactions ? handle => entityManager.Registry.Get(handle) : null,
                            canUseReaction: canResolveReactions ? handle => turnManager.CanUseReaction(handle) : null,
                            reactionPolicy: canResolveReactions ? reactionPolicy : null,
                            shieldBlockAction: canResolveReactions ? shieldBlockAction : null,
                            reactionBuffer: canResolveReactions ? reactionBuffer : null,
                            reactionPhase: ReactionTriggerPhase.PostHit,
                            reactionOwnerTag: "PlayerActionExecutor.HarmArea");
                    }
                }

                outcomes[i] = new SpellResolvedTargetOutcome(
                    target,
                    shardCount: 0,
                    shardRolls: null,
                    rolledDamage: rolledAmount,
                    attackResult: null,
                    saveResult: saveResult,
                    appliedConditionType: null,
                    appliedConditionValue: 0,
                    appliedConditionRounds: 0,
                    resolvedDamage: resolvedDamage,
                    appliedDamage: appliedDamage,
                    hpBefore: hpBefore,
                    hpAfter: Mathf.Max(0, targetData.CurrentHP),
                    targetDefeated: !targetData.IsAlive,
                    appliedHealing: appliedHealing);
            }

            eventBus?.PublishSpellResolved(new SpellResolvedEvent(
                SpellId.Harm,
                actor,
                actionCost,
                spellDc,
                spellAttackModifier: 0,
                rolledDamage: rolledAmount,
                targetOutcomes: outcomes));

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(actionCost);
            return true;
        }

        public bool TryConfirmBurningHands(Vector3Int aimCell, IRng rng = null)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return false;
            if (!CanActNow())
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsBurningHands)
                return false;

            var definition = SpellCatalog.Get(SpellId.BurningHands);
            if (turnManager.ActionsRemaining < definition.minActionCost)
                return false;

            var preview = BuildBurningHandsPreview(aimCell);
            if (!preview.IsValid)
                return false;

            rng ??= UnityRng.Shared;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.BurningHands");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            if (!TryContinueAfterActionStartReactions(
                    actor,
                    definition.actionName,
                    CombatActionKind.Spell,
                    SpellCatalog.HasManipulateTrait(SpellId.BurningHands, definition.minActionCost) ? CombatActionTraitFlags.Manipulate : CombatActionTraitFlags.None,
                    definition.minActionCost))
            {
                return false;
            }

            int spellDc = SpellcastingRules.ComputeWizardSpellDc(actorData);
            int rolledDamage = rng.RollDie(6) + rng.RollDie(6);
            bool canResolveReactions = turnManager != null && shieldBlockAction != null && EnsureReactionPolicy();
            var outcomes = new SpellResolvedTargetOutcome[preview.TargetCount];

            for (int i = 0; i < preview.TargetCount; i++)
            {
                var target = preview.targets[i];
                var targetData = entityManager.Registry.Get(target);
                if (targetData == null || !targetData.IsAlive)
                {
                    executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    executionStartTime = -1f;
#endif
                    turnManager.ActionCompleted();
                    return false;
                }

                var save = CheckResolver.RollSave(targetData, SaveType.Reflex, spellDc, rng);
                int resolvedDamage = CheckResolver.ApplyBasicSaveDamage(rolledDamage, save.degree);
                int hpBefore = Mathf.Max(0, targetData.CurrentHP);
                int appliedDamage = 0;
                if (resolvedDamage > 0)
                {
                    appliedDamage = DamageApplicationService.ApplyDamage(
                        actor,
                        target,
                        resolvedDamage,
                        definition.damageType,
                        definition.actionName,
                        isCritical: save.degree == DegreeOfSuccess.CriticalFailure,
                        entityManager,
                        eventBus,
                        initiativeOrder: canResolveReactions ? turnManager.InitiativeOrder : null,
                        getEntity: canResolveReactions ? handle => entityManager.Registry.Get(handle) : null,
                        canUseReaction: canResolveReactions ? handle => turnManager.CanUseReaction(handle) : null,
                        reactionPolicy: canResolveReactions ? reactionPolicy : null,
                        shieldBlockAction: canResolveReactions ? shieldBlockAction : null,
                        reactionBuffer: canResolveReactions ? reactionBuffer : null,
                        reactionPhase: ReactionTriggerPhase.PostHit,
                        reactionOwnerTag: "PlayerActionExecutor.BurningHands");
                }

                outcomes[i] = new SpellResolvedTargetOutcome(
                    target,
                    shardCount: 0,
                    shardRolls: null,
                    rolledDamage: rolledDamage,
                    attackResult: null,
                    saveResult: save,
                    appliedConditionType: null,
                    appliedConditionValue: 0,
                    appliedConditionRounds: 0,
                    resolvedDamage: resolvedDamage,
                    appliedDamage: appliedDamage,
                    hpBefore: hpBefore,
                    hpAfter: Mathf.Max(0, targetData.CurrentHP),
                    targetDefeated: !targetData.IsAlive);
            }

            eventBus?.PublishSpellResolved(new SpellResolvedEvent(
                SpellId.BurningHands,
                actor,
                definition.minActionCost,
                spellDc,
                spellAttackModifier: 0,
                rolledDamage,
                targetOutcomes: outcomes));

            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif
            turnManager.CompleteActionWithCost(definition.minActionCost);
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (turnManager == null) return;

            // Reposition cell selection is a legitimate interactive wait — keep the lock alive.
            if (hasPendingRepositionSelection)
            {
                turnManager.RefreshActionLockTimer();
                return;
            }

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

        private TargetingFailureReason GetSpellTargetFailure(
            EntityHandle actor,
            EntityHandle target,
            in SpellSliceDefinition definition)
        {
            if (!actor.IsValid || !target.IsValid)
                return TargetingFailureReason.InvalidTarget;
            if (entityManager == null || entityManager.Registry == null)
                return TargetingFailureReason.InvalidState;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null)
                return TargetingFailureReason.InvalidTarget;
            if (!actorData.IsAlive || !targetData.IsAlive)
                return TargetingFailureReason.NotAlive;
            if (actor == target)
                return TargetingFailureReason.SelfTarget;
            if (actorData.GridPosition.y != targetData.GridPosition.y)
                return TargetingFailureReason.WrongElevation;

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, targetData.GridPosition);
            if (distanceFeet > definition.rangeFeet)
                return TargetingFailureReason.OutOfRange;

            if (!definition.requiresLineOfSight || entityManager.GridData == null)
                return TargetingFailureReason.None;

            var line = StrikeLineResolver.ResolveSameElevation(
                entityManager.GridData,
                entityManager.Occupancy,
                actorData.GridPosition,
                targetData.GridPosition,
                actor,
                target);

            return line.hasLineOfSight
                ? TargetingFailureReason.None
                : TargetingFailureReason.NoLineOfSight;
        }

        private TargetingFailureReason GetHealTargetFailure(
            EntityHandle actor,
            EntityHandle target,
            int actionCount)
        {
            if (!actor.IsValid || !target.IsValid)
                return TargetingFailureReason.InvalidTarget;
            if (entityManager == null || entityManager.Registry == null)
                return TargetingFailureReason.InvalidState;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null)
                return TargetingFailureReason.InvalidTarget;
            if (!actorData.IsAlive || !targetData.IsAlive)
                return TargetingFailureReason.NotAlive;
            if (actorData.GridPosition.y != targetData.GridPosition.y)
                return TargetingFailureReason.WrongElevation;

            bool isUndeadTarget = targetData.VitalityAffinity == VitalityAffinity.Undead;
            bool isSelfTarget = actor == target;
            bool isFriendlyTarget = targetData.Team == actorData.Team;
            if (!isUndeadTarget && !isSelfTarget && !isFriendlyTarget)
                return TargetingFailureReason.WrongTeam;

            int allowedRangeFeet = Mathf.Clamp(actionCount, 1, 3) >= 2 ? 30 : 5;
            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, targetData.GridPosition);
            if (distanceFeet > allowedRangeFeet)
                return TargetingFailureReason.OutOfRange;

            if (isSelfTarget || entityManager.GridData == null)
                return TargetingFailureReason.None;

            var line = StrikeLineResolver.ResolveSameElevation(
                entityManager.GridData,
                entityManager.Occupancy,
                actorData.GridPosition,
                targetData.GridPosition,
                actor,
                target);

            return line.hasLineOfSight
                ? TargetingFailureReason.None
                : TargetingFailureReason.NoLineOfSight;
        }

        private TargetingFailureReason GetHarmTargetFailure(
            EntityHandle actor,
            EntityHandle target,
            int actionCount)
        {
            if (!actor.IsValid || !target.IsValid)
                return TargetingFailureReason.InvalidTarget;
            if (entityManager == null || entityManager.Registry == null)
                return TargetingFailureReason.InvalidState;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null)
                return TargetingFailureReason.InvalidTarget;
            if (!actorData.IsAlive || !targetData.IsAlive)
                return TargetingFailureReason.NotAlive;
            if (actorData.GridPosition.y != targetData.GridPosition.y)
                return TargetingFailureReason.WrongElevation;

            bool isUndeadTarget = targetData.VitalityAffinity == VitalityAffinity.Undead;
            bool isLivingEnemy = targetData.Team != actorData.Team;
            if (!isUndeadTarget && !isLivingEnemy)
                return TargetingFailureReason.WrongTeam;

            int allowedRangeFeet = Mathf.Clamp(actionCount, 1, 3) >= 2 ? 30 : 5;
            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, targetData.GridPosition);
            if (distanceFeet > allowedRangeFeet)
                return TargetingFailureReason.OutOfRange;

            if (actor == target || entityManager.GridData == null)
                return TargetingFailureReason.None;

            var line = StrikeLineResolver.ResolveSameElevation(
                entityManager.GridData,
                entityManager.Occupancy,
                actorData.GridPosition,
                targetData.GridPosition,
                actor,
                target);

            return line.hasLineOfSight
                ? TargetingFailureReason.None
                : TargetingFailureReason.NoLineOfSight;
        }

        private SpellAreaPreview BuildBurningHandsPreview(Vector3Int aimCell)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return SpellAreaPreview.Invalid(SpellId.BurningHands, aimCell, TargetingFailureReason.InvalidState);

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return SpellAreaPreview.Invalid(SpellId.BurningHands, aimCell, TargetingFailureReason.InvalidState);

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive)
                return SpellAreaPreview.Invalid(SpellId.BurningHands, aimCell, TargetingFailureReason.InvalidState);

            var definition = SpellCatalog.Get(SpellId.BurningHands);
            if (aimCell == actorData.GridPosition)
                return SpellAreaPreview.Invalid(SpellId.BurningHands, aimCell, TargetingFailureReason.InvalidTarget);
            if (aimCell.y != actorData.GridPosition.y)
                return SpellAreaPreview.Invalid(SpellId.BurningHands, aimCell, TargetingFailureReason.WrongElevation);

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, aimCell);
            if (distanceFeet > definition.rangeFeet)
                return SpellAreaPreview.Invalid(SpellId.BurningHands, aimCell, TargetingFailureReason.OutOfRange);

            spellAreaCellBuffer.Clear();
            if (!BurningHandsConeResolver.TryResolve(actorData.GridPosition, aimCell, spellAreaCellBuffer, out int directionIndex))
                return SpellAreaPreview.Invalid(SpellId.BurningHands, aimCell, TargetingFailureReason.InvalidTarget);

            var gridData = entityManager.GridData;
            int filteredCellCount = 0;
            for (int i = 0; i < spellAreaCellBuffer.Count; i++)
            {
                if (gridData == null || gridData.HasCell(spellAreaCellBuffer[i]))
                    filteredCellCount++;
            }

            var areaCells = new Vector3Int[filteredCellCount];
            int areaCellIndex = 0;
            for (int i = 0; i < spellAreaCellBuffer.Count; i++)
            {
                if (gridData == null || gridData.HasCell(spellAreaCellBuffer[i]))
                    areaCells[areaCellIndex++] = spellAreaCellBuffer[i];
            }

            spellAreaTargetBuffer.Clear();
            int allyCount = 0;
            int enemyCount = 0;
            foreach (var targetData in entityManager.Registry.GetAll())
            {
                if (targetData == null || !targetData.IsAlive || !targetData.Handle.IsValid)
                    continue;
                if (!ContainsCell(areaCells, targetData.GridPosition))
                    continue;

                spellAreaTargetBuffer.Add(targetData.Handle);
                if (targetData.Team == actorData.Team)
                    allyCount++;
                else
                    enemyCount++;
            }

            var warning = allyCount > 0
                ? TargetingWarningReason.AlliesInArea
                : TargetingWarningReason.None;

            return new SpellAreaPreview(
                SpellId.BurningHands,
                aimCell,
                TargetingFailureReason.None,
                warning,
                directionIndex,
                areaCells,
                spellAreaTargetBuffer.ToArray(),
                allyCount,
                enemyCount);
        }

        private SpellAreaPreview BuildHealAreaPreview(Vector3Int aimCell)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return SpellAreaPreview.Invalid(SpellId.Heal, aimCell, TargetingFailureReason.InvalidState);

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return SpellAreaPreview.Invalid(SpellId.Heal, aimCell, TargetingFailureReason.InvalidState);

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsHeal)
                return SpellAreaPreview.Invalid(SpellId.Heal, aimCell, TargetingFailureReason.InvalidState);

            if (turnManager.ActionsRemaining < 3)
                return SpellAreaPreview.Invalid(SpellId.Heal, aimCell, TargetingFailureReason.InvalidState);
            if (aimCell.y != actorData.GridPosition.y)
                return SpellAreaPreview.Invalid(SpellId.Heal, aimCell, TargetingFailureReason.WrongElevation);

            spellAreaCellBuffer.Clear();
            EmanationAreaResolver.Resolve(
                actorData.GridPosition,
                radiusFeet: 30,
                outCells: spellAreaCellBuffer,
                gridData: entityManager.GridData);

            if (spellAreaCellBuffer.Count <= 0)
                return SpellAreaPreview.Invalid(SpellId.Heal, aimCell, TargetingFailureReason.InvalidState);

            var areaCells = spellAreaCellBuffer.ToArray();
            spellAreaTargetBuffer.Clear();
            int allyCount = 0;
            int enemyCount = 0;
            foreach (var targetData in entityManager.Registry.GetAll())
            {
                if (targetData == null || !targetData.IsAlive || !targetData.Handle.IsValid)
                    continue;
                if (targetData.GridPosition.y != actorData.GridPosition.y)
                    continue;
                if (!ContainsCell(areaCells, targetData.GridPosition))
                    continue;

                spellAreaTargetBuffer.Add(targetData.Handle);
                if (targetData.Team == actorData.Team)
                    allyCount++;
                else
                    enemyCount++;
            }

            return new SpellAreaPreview(
                SpellId.Heal,
                aimCell,
                TargetingFailureReason.None,
                TargetingWarningReason.None,
                directionIndex: -1,
                areaCells,
                spellAreaTargetBuffer.ToArray(),
                allyCount,
                enemyCount);
        }

        private SpellAreaPreview BuildHarmAreaPreview(Vector3Int aimCell)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return SpellAreaPreview.Invalid(SpellId.Harm, aimCell, TargetingFailureReason.InvalidState);

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return SpellAreaPreview.Invalid(SpellId.Harm, aimCell, TargetingFailureReason.InvalidState);

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive || !actorData.KnowsHarm)
                return SpellAreaPreview.Invalid(SpellId.Harm, aimCell, TargetingFailureReason.InvalidState);

            if (turnManager.ActionsRemaining < 3)
                return SpellAreaPreview.Invalid(SpellId.Harm, aimCell, TargetingFailureReason.InvalidState);
            if (aimCell.y != actorData.GridPosition.y)
                return SpellAreaPreview.Invalid(SpellId.Harm, aimCell, TargetingFailureReason.WrongElevation);

            spellAreaCellBuffer.Clear();
            EmanationAreaResolver.Resolve(
                actorData.GridPosition,
                radiusFeet: 30,
                outCells: spellAreaCellBuffer,
                gridData: entityManager.GridData);

            if (spellAreaCellBuffer.Count <= 0)
                return SpellAreaPreview.Invalid(SpellId.Harm, aimCell, TargetingFailureReason.InvalidState);

            var areaCells = spellAreaCellBuffer.ToArray();
            spellAreaTargetBuffer.Clear();
            int livingAllyCount = 0;
            int affectedOtherCount = 0;
            foreach (var targetData in entityManager.Registry.GetAll())
            {
                if (targetData == null || !targetData.IsAlive || !targetData.Handle.IsValid)
                    continue;
                if (targetData.GridPosition.y != actorData.GridPosition.y)
                    continue;
                if (!ContainsCell(areaCells, targetData.GridPosition))
                    continue;

                spellAreaTargetBuffer.Add(targetData.Handle);
                if (targetData.Team == actorData.Team && targetData.VitalityAffinity != VitalityAffinity.Undead)
                    livingAllyCount++;
                else
                    affectedOtherCount++;
            }

            return new SpellAreaPreview(
                SpellId.Harm,
                aimCell,
                TargetingFailureReason.None,
                livingAllyCount > 0 ? TargetingWarningReason.AlliesInArea : TargetingWarningReason.None,
                directionIndex: -1,
                areaCells,
                spellAreaTargetBuffer.ToArray(),
                livingAllyCount,
                affectedOtherCount);
        }

        private static bool ContainsCell(Vector3Int[] areaCells, Vector3Int cell)
        {
            if (areaCells == null)
                return false;

            for (int i = 0; i < areaCells.Length; i++)
            {
                if (areaCells[i] == cell)
                    return true;
            }

            return false;
        }

        private void ResetPendingRepositionState(bool rollbackActionLock)
        {
            hasPendingRepositionSelection = false;
            pendingRepositionContext = default;
            pendingRepositionDestinations.Clear();
            executingActor = EntityHandle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = -1f;
#endif

            if (rollbackActionLock && turnManager != null)
                turnManager.ActionCompleted();
        }

        private int ResolveAidBonusForSkillCheck(EntityHandle actor, SkillType skill, string actionName)
        {
            var context = AidCheckContext.ForSkill(actor, skill, actionName);
            return ResolveAidBonusForCheck(in context);
        }

        private int ResolveAidBonusForStrike(EntityHandle actor)
        {
            var context = AidCheckContext.ForStrike(actor, "Strike");
            return ResolveAidBonusForCheck(in context);
        }

        private int ResolveAidBonusForCheck(in AidCheckContext context)
        {
            if (!context.ally.IsValid) return 0;
            if (turnManager == null || entityManager == null || entityManager.Registry == null) return 0;

            var aidService = turnManager.AidService;
            if (aidService == null) return 0;

            if (!aidService.TryConsumeAidForCheck(
                    context,
                    getEntity: handle => entityManager.Registry.Get(handle),
                    canUseReaction: handle => turnManager.CanUseReaction(handle),
                    rng: UnityRng.Shared,
                    out var outcome))
            {
                return 0;
            }

            eventBus?.PublishAidResolved(new AidResolvedEvent(
                outcome.helper,
                outcome.ally,
                outcome.checkType,
                outcome.skill,
                outcome.triggeringActionName,
                outcome.roll,
                outcome.dc,
                outcome.degree,
                outcome.appliedModifier,
                outcome.reactionConsumed));
            eventBus?.PublishAidCleared(outcome.helper, outcome.ally, AidClearReason.Consumed);

            return outcome.appliedModifier;
        }

        private void PublishConditionDeltas()
        {
            if (conditionDeltaBuffer.Count <= 0)
                return;

            if (eventBus == null)
            {
                conditionDeltaBuffer.Clear();
                return;
            }

            for (int i = 0; i < conditionDeltaBuffer.Count; i++)
            {
                var delta = conditionDeltaBuffer[i];
                eventBus.PublishConditionChanged(
                    delta.entity,
                    delta.type,
                    delta.changeType,
                    delta.oldValue,
                    delta.newValue,
                    delta.oldRemainingRounds,
                    delta.newRemainingRounds);
            }

            conditionDeltaBuffer.Clear();
        }
    }
}
