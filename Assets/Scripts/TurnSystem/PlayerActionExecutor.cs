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
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private StrideAction strideAction;
        [SerializeField] private StrikeAction strikeAction;
        [SerializeField] private StandAction standAction;
                [SerializeField] private TripAction tripAction;
        [SerializeField] private ShoveAction shoveAction;
        [SerializeField] private GrappleAction grappleAction;
        [SerializeField] private EscapeAction escapeAction;
        [SerializeField] private DemoralizeAction demoralizeAction;
        [SerializeField] private RaiseShieldAction raiseShieldAction;
        [SerializeField] private ShieldBlockAction shieldBlockAction;
        [SerializeField] private ReactionPromptController reactionPromptController;

        private EntityHandle executingActor = EntityHandle.None;
        private readonly List<ReactionOption> reactionBuffer = new(2);
        private IReactionDecisionPolicy reactionPolicy;

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
            if (tripAction == null) Debug.LogWarning("[Executor] Missing TripAction", this);
            if (shoveAction == null) Debug.LogWarning("[Executor] Missing ShoveAction", this);
            if (grappleAction == null) Debug.LogWarning("[Executor] Missing GrappleAction", this);
            if (escapeAction == null) Debug.LogWarning("[Executor] Missing EscapeAction", this);
            if (demoralizeAction == null) Debug.LogWarning("[Executor] Missing DemoralizeAction", this);
            if (raiseShieldAction == null) Debug.LogWarning("[Executor] Missing RaiseShieldAction", this);
            if (shieldBlockAction == null) Debug.LogWarning("[Executor] Missing ShieldBlockAction", this);
            if (reactionPromptController == null) Debug.LogWarning("[Executor] Missing ReactionPromptController", this);
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
            if (!EnsureReactionPolicy()) return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;

            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Strike");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            var phase = strikeAction.ResolveAttackRoll(actor, target, UnityRng.Shared);
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

        private int ResolvePostHitReactionReduction(StrikePhaseResult resolved)
        {
            if (!resolved.damageDealt || resolved.damageRolled <= 0)
                return 0;
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return 0;

            reactionBuffer.Clear();
            ReactionService.CollectEligibleReactions(
                ReactionTriggerPhase.PostHit,
                resolved.attacker,
                resolved.target,
                turnManager.InitiativeOrder,
                handle => entityManager.Registry.Get(handle),
                reactionBuffer);

            if (reactionBuffer.Count <= 0)
                return 0;

            // MVP invariant: at most one option (self-only shield block).
            var option = reactionBuffer[0];
            var reactorData = entityManager.Registry.Get(option.entity);
            if (reactorData == null || !reactorData.IsAlive)
                return 0;

            bool? decided = null;
            reactionPolicy.DecideReaction(
                option,
                reactorData,
                resolved.damageRolled,
                result => decided = result);

            if (!decided.HasValue)
            {
                Debug.LogWarning("[Reaction] DecideReaction did not invoke callback synchronously. Treating as decline.");
                return 0;
            }

            if (!decided.Value)
                return 0;

            var blockResult = ShieldBlockRules.Calculate(reactorData.EquippedShield, resolved.damageRolled);
            if (shieldBlockAction == null)
            {
                Debug.LogWarning("[Executor] ShieldBlockAction is missing. Skipping Shield Block reaction.", this);
                return 0;
            }

            return shieldBlockAction.Execute(option.entity, resolved.damageRolled, in blockResult)
                ? blockResult.targetDamageReduction
                : 0;
        }

        private bool EnsureReactionPolicy()
        {
            if (reactionPolicy != null) return true;
            reactionPolicy = new ModalReactionPolicy(reactionPromptController);
            return true;
        }

        public bool TryExecuteStand()
        {
            if (turnManager == null || standAction == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            if (!standAction.CanStand(actor)) return false;
            if (!standAction.TryStand(actor)) return false;

            turnManager.SpendActions(StandAction.ActionCost);
            return true;
        }

        public bool TryExecuteTrip(EntityHandle target)
        {
            if (turnManager == null || entityManager == null || tripAction == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Trip");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            var degree = tripAction.TryTrip(actor, target, UnityRng.Shared);
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
            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Demoralize");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            var degree = demoralizeAction.TryDemoralize(actor, target, UnityRng.Shared);
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

        public bool TryExecuteShove(EntityHandle target)
        {
            if (turnManager == null || entityManager == null || shoveAction == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Shove");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            var degree = shoveAction.TryShove(actor, target, UnityRng.Shared);
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
            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Grapple");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            var degree = grappleAction.TryGrapple(actor, target, UnityRng.Shared);
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

        public bool TryExecuteEscape(EntityHandle grappler)
        {
            if (turnManager == null || entityManager == null || escapeAction == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            executingActor = actor;
            turnManager.BeginActionExecution(actor, "Player.Escape");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            executionStartTime = Time.time;
#endif

            var degree = escapeAction.TryEscape(actor, grappler, UnityRng.Shared);
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
            if (turnManager == null || entityManager == null || raiseShieldAction == null) return false;
            if (!CanActNow()) return false;

            var actor = turnManager.CurrentEntity;
            if (!raiseShieldAction.CanRaiseShield(actor)) return false;

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
