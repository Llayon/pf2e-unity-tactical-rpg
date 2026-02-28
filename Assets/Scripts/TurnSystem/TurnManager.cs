using System;
using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Central state machine for PF2e combat. Manages initiative order, round tracking,
    /// action economy, and turn sequencing.
    /// Attach to a scene GameObject; wire EntityManager via Inspector.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        [Header("Initiative")]
        [SerializeField] private InitiativeCheckMode initiativeCheckMode = InitiativeCheckMode.Perception;
        [SerializeField] private SkillType initiativeSkill = SkillType.Stealth;

        [Header("Debug — visible in Inspector")]
        [SerializeField] private TurnState state = TurnState.Inactive;
        [SerializeField] private int currentIndex = -1;
        [SerializeField] private int roundNumber = 0;
        [SerializeField] private bool delayTurnBeginTriggerOpen;
        [SerializeField] private bool delayPlacementSelectionOpen;
        [SerializeField] private EntityHandle delayReturnWindowAfterActor = EntityHandle.None;

        private List<InitiativeEntry> initiativeOrder = new();
        private TurnState stateBeforeExecution;
        private EntityHandle executingActor = EntityHandle.None;
        private string executingActionSource = string.Empty;
        private float executingActionStartTime = -1f;
        private readonly List<ConditionDelta> conditionDeltaBuffer = new();
        private readonly ConditionService conditionService = new();
        private readonly Dictionary<EntityHandle, DelayedTurnRecord> delayedTurns = new();
        private readonly HashSet<EntityHandle> delayReactionSuppressed = new();
        private IRng initiativeRng = UnityRng.Shared;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float ActionLockWarnSeconds = 4f;
        private const float ActionLockForceReleaseSeconds = 30f;
        private bool actionLockWarned;
#endif

        // ─── Public Properties ────────────────────────────────────────────────

        public TurnState State        => state;
        public int        RoundNumber => roundNumber;
        public int        CurrentIndex => currentIndex;

        public IReadOnlyList<InitiativeEntry> InitiativeOrder => initiativeOrder;

        public EntityHandle CurrentEntity
        {
            get
            {
                if (currentIndex >= 0 && currentIndex < initiativeOrder.Count)
                    return initiativeOrder[currentIndex].Handle;
                return EntityHandle.None;
            }
        }

        public bool IsPlayerTurn => state == TurnState.PlayerTurn;

        public int ActionsRemaining
        {
            get
            {
                if (entityManager == null || entityManager.Registry == null) return 0;
                var data = entityManager.Registry.Get(CurrentEntity);
                return data != null ? data.ActionsRemaining : 0;
            }
        }

        public EntityHandle ExecutingActor => executingActor;

        public string ExecutingActionSource => executingActionSource;

        public float ExecutingActionDurationSeconds
        {
            get
            {
                if (state != TurnState.ExecutingAction || executingActionStartTime < 0f)
                    return 0f;

                return Time.unscaledTime - executingActionStartTime;
            }
        }

        public bool IsDelayTurnBeginTriggerOpen => delayTurnBeginTriggerOpen;
        public bool IsDelayPlacementSelectionOpen => delayPlacementSelectionOpen;

        public int DelayedActorCount => delayedTurns.Count;
        public InitiativeCheckMode InitiativeMode => initiativeCheckMode;
        public SkillType InitiativeSkill => initiativeSkill;

        public bool IsDelayReturnWindowOpen => state == TurnState.DelayReturnWindow;

        public EntityHandle DelayReturnWindowAfterActor => delayReturnWindowAfterActor;

        public bool IsReactionSuppressedByDelay(EntityHandle actor) =>
            actor.IsValid && delayReactionSuppressed.Contains(actor);

        public bool CanUseReaction(EntityHandle actor)
        {
            if (!actor.IsValid)
                return false;

            if (delayReactionSuppressed.Contains(actor))
                return false;

            if (entityManager == null || entityManager.Registry == null)
                return false;

            var data = entityManager.Registry.Get(actor);
            return data != null && data.IsAlive && data.ReactionAvailable;
        }

        internal void SetInitiativeRngForTesting(IRng rng)
        {
            initiativeRng = rng ?? UnityRng.Shared;
        }

        public bool ConfigureInitiativeChecks(InitiativeCheckMode mode, SkillType skill = SkillType.Stealth)
        {
            if (state != TurnState.Inactive)
            {
                Debug.LogWarning("[TurnManager] ConfigureInitiativeChecks ignored because combat is active.");
                return false;
            }

            initiativeCheckMode = mode;
            initiativeSkill = skill;
            return true;
        }

        internal void SetInitiativeCheckModeForTesting(InitiativeCheckMode mode, SkillType skill)
        {
            ConfigureInitiativeChecks(mode, skill);
        }

        // ─── Events ───────────────────────────────────────────────────────────

        public event Action<CombatStartedEvent>               OnCombatStarted;
        public event Action<CombatEndedEvent>                 OnCombatEndedWithResult;
        public event Action<RoundStartedEvent>                OnRoundStarted;
        public event Action<TurnStartedEvent>                 OnTurnStarted;
        public event Action<TurnEndedEvent>                   OnTurnEnded;
        public event Action<ActionsChangedEvent>              OnActionsChanged;
        public event Action<ConditionsTickedEvent>            OnConditionsTicked;
        public event Action<InitiativeRolledEvent>            OnInitiativeRolled;

        private readonly struct DelayedTurnRecord
        {
            public readonly EntityHandle actor;
            public readonly int delayedRoundNumber;
            public readonly bool delayImmediateEffectsApplied;
            public readonly bool isPlannedAutoResume;
            public readonly EntityHandle originalAnchorActor;
            public readonly EntityHandle plannedReturnAfterActor;
            public readonly InitiativeEntry initiativeEntry;

            public DelayedTurnRecord(
                EntityHandle actor,
                int delayedRoundNumber,
                bool delayImmediateEffectsApplied,
                bool isPlannedAutoResume,
                EntityHandle originalAnchorActor,
                EntityHandle plannedReturnAfterActor,
                InitiativeEntry initiativeEntry)
            {
                this.actor = actor;
                this.delayedRoundNumber = delayedRoundNumber;
                this.delayImmediateEffectsApplied = delayImmediateEffectsApplied;
                this.isPlannedAutoResume = isPlannedAutoResume;
                this.originalAnchorActor = originalAnchorActor;
                this.plannedReturnAfterActor = plannedReturnAfterActor;
                this.initiativeEntry = initiativeEntry;
            }
        }

        // ─── Public Methods ───────────────────────────────────────────────────

        private void Awake()
        {
            ResolveEventBusIfMissing();
        }

        /// <summary>
        /// Kicks off a new combat encounter: roll initiative, fire events, begin round 1.
        /// </summary>
        public void StartCombat()
        {
            if (state != TurnState.Inactive)
            {
                Debug.LogWarning("[TurnManager] StartCombat called but state is not Inactive.");
                return;
            }
            if (entityManager == null)
            {
                Debug.LogError("TurnManager: EntityManager not assigned!");
                return;
            }
            if (entityManager.Registry == null)
            {
                Debug.LogError("TurnManager: EntityRegistry not ready!");
                return;
            }

            ResetActionExecutionTracking();
            ResetDelayState();
            ResolveEventBusIfMissing();

            state = TurnState.RollingInitiative;
            RollInitiative();
            PublishCombatStarted();
            PublishInitiativeRolled(new InitiativeRolledEvent(initiativeOrder));
            roundNumber = 0;
            StartNextRound();
        }

        /// <summary>
        /// End the current entity's turn and advance to the next.
        /// </summary>
        public void EndTurn()
        {
            if (state != TurnState.PlayerTurn && state != TurnState.EnemyTurn)
            {
                Debug.LogWarning("[TurnManager] EndTurn called but not in PlayerTurn or EnemyTurn state.");
                return;
            }
            SetDelayTurnBeginTriggerOpen(false);
            SetDelayPlacementSelectionOpen(false);

            var endingEntity = CurrentEntity;
            var data = entityManager.Registry.Get(endingEntity);
            ApplyEndTurnEffects(endingEntity, data);

            PublishTurnEnded(new TurnEndedEvent(endingEntity));

            // End encounter immediately when one side is wiped.
            if (CheckVictory()) return;
            if (TryOpenDelayReturnWindow(endingEntity)) return;
            AdvanceInitiativeAfterTurnEnd();
        }

        /// <summary>
        /// Spend action points. Automatically ends turn if actions hit 0.
        /// Returns false if the cost cannot be paid.
        /// </summary>
        public bool SpendActions(int cost)
        {
            if (cost <= 0) return false;

            var data = entityManager.Registry.Get(CurrentEntity);
            if (data == null || data.ActionsRemaining < cost) return false;

            data.SpendActions(cost);
            PublishActionsChanged(new ActionsChangedEvent(CurrentEntity, data.ActionsRemaining));

            if (data.ActionsRemaining <= 0)
                EndTurn();

            return true;
        }

        /// <summary>
        /// Returns true if the given entity is the current actor and has actions left.
        /// </summary>
        public bool CanAct(EntityHandle handle)
        {
            return CurrentEntity == handle
                && ActionsRemaining > 0
                && (state == TurnState.PlayerTurn || state == TurnState.EnemyTurn);
        }

        public bool IsDelayed(EntityHandle actor)
        {
            return actor.IsValid && delayedTurns.ContainsKey(actor);
        }

        public bool TryGetFirstDelayedPlayerActor(out EntityHandle actor)
        {
            actor = EntityHandle.None;

            if (entityManager == null || entityManager.Registry == null)
                return false;

            foreach (var kvp in delayedTurns)
            {
                var handle = kvp.Key;
                var record = kvp.Value;
                var data = entityManager.Registry.Get(handle);
                if (data == null || !data.IsAlive) continue;
                if (data.Team != Team.Player) continue;
                if (record.isPlannedAutoResume) continue;

                actor = handle;
                return true;
            }

            return false;
        }

        public bool TryGetDelayedPlannedAnchor(EntityHandle actor, out EntityHandle anchorActor)
        {
            anchorActor = EntityHandle.None;
            if (!actor.IsValid) return false;
            if (!delayedTurns.TryGetValue(actor, out var record)) return false;
            if (!record.plannedReturnAfterActor.IsValid) return false;

            anchorActor = record.plannedReturnAfterActor;
            return true;
        }

        public bool TryBeginDelayPlacementSelection()
        {
            if (!CanDelayCurrentTurn())
                return false;

            SetDelayPlacementSelectionOpen(true);
            return true;
        }

        public void CancelDelayPlacementSelection()
        {
            SetDelayPlacementSelectionOpen(false);
        }

        public bool IsValidDelayAnchorForCurrentTurn(EntityHandle anchorActor)
        {
            if (!delayPlacementSelectionOpen) return false;
            if (!CanDelayCurrentTurn()) return false;
            if (!anchorActor.IsValid) return false;

            var currentActor = CurrentEntity;
            if (!currentActor.IsValid || currentActor == anchorActor)
                return false;

            int anchorIndex = FindInitiativeIndex(anchorActor);
            if (anchorIndex < 0)
                return false;

            if (anchorIndex <= currentIndex)
                return false; // Owlcat-style: choose a later slot in the current round only.

            if (entityManager == null || entityManager.Registry == null)
                return false;

            var anchorData = entityManager.Registry.Get(anchorActor);
            return anchorData != null && anchorData.IsAlive;
        }

        public bool TryDelayCurrentTurnAfterActor(EntityHandle anchorActor)
        {
            if (!IsValidDelayAnchorForCurrentTurn(anchorActor))
                return false;

            return TryDelayCurrentTurnInternal(anchorActor);
        }

        public bool CanDelayCurrentTurn()
        {
            if (state != TurnState.PlayerTurn) return false;
            if (!delayTurnBeginTriggerOpen) return false;
            if (state == TurnState.ExecutingAction) return false;

            var actor = CurrentEntity;
            if (!actor.IsValid) return false;
            if (delayedTurns.ContainsKey(actor)) return false;
            if (entityManager == null || entityManager.Registry == null) return false;

            var data = entityManager.Registry.Get(actor);
            if (data == null || !data.IsAlive) return false;

            // Delay requires at least one other active initiative entry to yield to.
            if (initiativeOrder.Count <= 1) return false;

            return true;
        }

        public bool TryDelayCurrentTurn()
        {
            return TryDelayCurrentTurnInternal(EntityHandle.None);
        }

        public bool TryReturnDelayedActor(EntityHandle actor)
        {
            if (state != TurnState.DelayReturnWindow)
                return false;
            if (!delayReturnWindowAfterActor.IsValid)
                return false;

            return TryResumeDelayedActorInternal(actor, delayReturnWindowAfterActor);
        }

        private bool TryResumeDelayedActorInternal(EntityHandle actor, EntityHandle afterActor)
        {
            if (!actor.IsValid)
                return false;
            if (!delayedTurns.TryGetValue(actor, out var record))
                return false;
            if (entityManager == null || entityManager.Registry == null)
                return false;

            var data = entityManager.Registry.Get(actor);
            if (data == null || !data.IsAlive)
                return false;

            int insertIndex = GetInsertionIndexAfterAnchor(afterActor);
            initiativeOrder.Insert(insertIndex, record.initiativeEntry);

            delayedTurns.Remove(actor);
            delayReactionSuppressed.Remove(actor);
            ClearDelayReturnWindowIfOpen();
            SetDelayPlacementSelectionOpen(false, actor);

            currentIndex = insertIndex;
            OpenTurnForActor(actor, data, openDelayTriggerWindow: false); // resumed delayed turn
            PublishDelayedTurnResumed(actor, afterActor, record.isPlannedAutoResume);
            return true;
        }

        public void SkipDelayReturnWindow()
        {
            if (state != TurnState.DelayReturnWindow)
            {
                Debug.LogWarning("[TurnManager] SkipDelayReturnWindow called but delay return window is not open.");
                return;
            }

            ClearDelayReturnWindowIfOpen();
            AdvanceInitiativeAfterTurnEnd();
        }

        /// <summary>
        /// Transition to ExecutingAction to block input while an animation plays.
        /// </summary>
        public void BeginActionExecution()
        {
            BeginActionExecution(CurrentEntity, null);
        }

        /// <summary>
        /// Transition to ExecutingAction and track actor/source for diagnostics.
        /// </summary>
        public void BeginActionExecution(EntityHandle actor, string source = null)
        {
            if (state == TurnState.ExecutingAction)
            {
                Debug.LogWarning(
                    $"[TurnManager] BeginActionExecution called while already executing. " +
                    $"Current lock actor={executingActor.Id}, source={executingActionSource}.");
                return;
            }

            SetDelayTurnBeginTriggerOpen(false, actor);
            SetDelayPlacementSelectionOpen(false, actor);
            stateBeforeExecution = state;
            state = TurnState.ExecutingAction;
            executingActor = actor.IsValid ? actor : CurrentEntity;
            executingActionSource = string.IsNullOrEmpty(source) ? "unspecified" : source;
            executingActionStartTime = Time.unscaledTime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            actionLockWarned = false;
#endif
        }

        /// <summary>
        /// Resets the action lock timer, preventing watchdog warnings/force-release.
        /// Call periodically during interactive waits (e.g. Reposition cell selection)
        /// where the lock is legitimately held for player input.
        /// </summary>
        public void RefreshActionLockTimer()
        {
            if (state != TurnState.ExecutingAction) return;
            executingActionStartTime = Time.unscaledTime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            actionLockWarned = false;
#endif
        }

        /// <summary>
        /// Called when the executing action finishes. Restores previous turn state.
        /// If no actions remain, ends the turn automatically.
        /// </summary>
        public void ActionCompleted()
        {
            if (state != TurnState.ExecutingAction) return;

            state = stateBeforeExecution;
            ResetActionExecutionTracking();

            if (ActionsRemaining <= 0)
                EndTurn();
            else
                PublishActionsChanged(new ActionsChangedEvent(CurrentEntity, ActionsRemaining));
        }

        /// <summary>
        /// Atomic: spend actions + restore state from ExecutingAction + auto-EndTurn if drained.
        /// Solves the race where SpendActions -> EndTurn is ignored while state == ExecutingAction.
        /// Zone refresh is handled by MovementZoneVisualizer listening to OnActionsChanged.
        /// </summary>
        public void CompleteActionWithCost(int actionCost)
        {
            if (state != TurnState.ExecutingAction) return;

            if (actionCost <= 0) actionCost = 0;

            // 1) Spend actions directly via EntityData
            EntityData data = null;
            var actionActor = executingActor.IsValid ? executingActor : CurrentEntity;
            if (entityManager != null && entityManager.Registry != null)
                data = entityManager.Registry.Get(actionActor);

            if (data != null && actionCost > 0)
                data.SpendActions(actionCost);
            else if (data == null && actionCost > 0)
                Debug.LogWarning($"[TurnManager] CompleteActionWithCost({actionCost}) could not resolve executing actor data.");

            // 2) Restore state from ExecutingAction
            state = stateBeforeExecution;
            ResetActionExecutionTracking();

            // 3) Notify action count change
            int remaining = (data != null) ? data.ActionsRemaining : ActionsRemaining;
            PublishActionsChanged(new ActionsChangedEvent(actionActor, remaining));

            // 4) Auto-EndTurn if drained
            if (remaining <= 0 && CurrentEntity == actionActor)
                EndTurn();
        }


        /// <summary>
        /// Terminate the combat encounter and reset all state.
        /// </summary>
        public void EndCombat(EncounterResult result = EncounterResult.Aborted)
        {
            ResetActionExecutionTracking();
            ResetDelayState();
            state = TurnState.CombatOver;

            if (entityManager != null)
                entityManager.DeselectEntity();

            PublishCombatEnded(new CombatEndedEvent(result));

            // Full reset
            state = TurnState.Inactive;
            currentIndex = -1;
            roundNumber = 0;
            initiativeOrder.Clear();
        }

        // ─── Private Methods ──────────────────────────────────────────────────

        private EntityHandle ResolveDelayEventActor(EntityHandle actorHint = default)
        {
            if (actorHint.IsValid)
                return actorHint;

            if (CurrentEntity.IsValid)
                return CurrentEntity;

            if (executingActor.IsValid)
                return executingActor;

            return EntityHandle.None;
        }

        private void SetDelayTurnBeginTriggerOpen(bool isOpen, EntityHandle actorHint = default)
        {
            if (delayTurnBeginTriggerOpen == isOpen)
                return;

            delayTurnBeginTriggerOpen = isOpen;
            PublishDelayTurnBeginTriggerChanged(ResolveDelayEventActor(actorHint), isOpen);
        }

        private void SetDelayPlacementSelectionOpen(bool isOpen, EntityHandle actorHint = default)
        {
            if (delayPlacementSelectionOpen == isOpen)
                return;

            delayPlacementSelectionOpen = isOpen;
            PublishDelayPlacementSelectionChanged(ResolveDelayEventActor(actorHint), isOpen);
        }

        private void OpenDelayReturnWindow(EntityHandle afterActor)
        {
            delayReturnWindowAfterActor = afterActor;
            state = TurnState.DelayReturnWindow;
            PublishDelayReturnWindowOpened(afterActor);
            Debug.Log($"[TurnManager] Delay return window opened after actor {afterActor.Id}.");
        }

        private void ClearDelayReturnWindowIfOpen()
        {
            if (!delayReturnWindowAfterActor.IsValid)
                return;

            var closedAfterActor = delayReturnWindowAfterActor;
            delayReturnWindowAfterActor = EntityHandle.None;
            PublishDelayReturnWindowClosed(closedAfterActor);
        }

        private void RollInitiative()
        {
            initiativeOrder.Clear();

            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null || !data.IsAlive) continue;

                var roll = RollInitiativeForActor(data);
                bool isPlayer = data.Team == Team.Player;

                initiativeOrder.Add(new InitiativeEntry
                {
                    Handle = data.Handle,
                    Roll = roll,
                    IsPlayer = isPlayer,
                });
            }

            initiativeOrder.Sort(CompareInitiativeEntries);

            var sb = new System.Text.StringBuilder("[TurnManager] Initiative order:\n");
            for (int i = 0; i < initiativeOrder.Count; i++)
            {
                var e    = initiativeOrder[i];
                var d    = entityManager.Registry.Get(e.Handle);
                string n = d?.Name ?? e.Handle.Id.ToString();
                sb.AppendLine(
                    $"  {i + 1}. {n} — d20: {e.Roll.naturalRoll}, Mod: {e.Roll.modifier}, Total: {e.Total}, Source: {e.Roll.source.ToShortLabel()}");
            }
            Debug.Log(sb.ToString());
        }

        private CheckRoll RollInitiativeForActor(EntityData actor)
        {
            if (actor != null && actor.UseInitiativeSkillOverride)
                return CheckResolver.RollSkill(actor, actor.InitiativeSkillOverride, initiativeRng);

            return initiativeCheckMode switch
            {
                InitiativeCheckMode.Skill => CheckResolver.RollSkill(actor, initiativeSkill, initiativeRng),
                _ => CheckResolver.RollPerception(actor, initiativeRng),
            };
        }

        private static int CompareInitiativeEntries(InitiativeEntry left, InitiativeEntry right)
        {
            return CheckComparer.CompareInitiative(left, right, InitiativeTieBreak);
        }

        private static int InitiativeTieBreak(in InitiativeEntry left, in InitiativeEntry right)
        {
            if (left.IsPlayer == right.IsPlayer)
                return 0;

            // PF2e RAW: on equal initiative result between PC and adversary, adversary acts first.
            return left.IsPlayer ? 1 : -1;
        }

        private void StartNextRound()
        {
            roundNumber++;
            currentIndex = 0;

            // Remove dead/unregistered entries; one Registry.Get per entry
            initiativeOrder.RemoveAll(e =>
            {
                var data = entityManager.Registry.Get(e.Handle);
                return data == null || !data.IsAlive;
            });

            // Safety net: if an edge case bypassed EndTurn check.
            if (CheckVictory()) return;

            if (initiativeOrder.Count == 0)
            {
                EndCombat();
                return;
            }

            PublishRoundStarted(new RoundStartedEvent(roundNumber));
            StartCurrentTurn();
        }

        private void StartCurrentTurn()
        {
            // Declare before the loop so they are accessible after it
            InitiativeEntry entry = default;
            EntityData data = null;

            while (currentIndex < initiativeOrder.Count)
            {
                entry = initiativeOrder[currentIndex];
                data  = entityManager.Registry.Get(entry.Handle);
                if (data != null && data.IsAlive)
                    break;
                currentIndex++;
            }

            if (currentIndex >= initiativeOrder.Count)
            {
                StartNextRound();
                return;
            }

            ApplyStartTurnEffects(entry.Handle, data);
            OpenTurnForActor(entry.Handle, data, openDelayTriggerWindow: true);
        }

        /// <summary>
        /// Returns true if combat ended due to team wipe.
        /// Counts only Team.Player and Team.Enemy; Team.Neutral does not affect victory.
        /// </summary>
        private bool CheckVictory()
        {
            if (entityManager == null || entityManager.Registry == null)
                return false;

            bool anyPlayer = false;
            bool anyEnemy = false;

            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null || !data.IsAlive) continue;

                if (data.Team == Team.Player) anyPlayer = true;
                else if (data.Team == Team.Enemy) anyEnemy = true;
            }

            if (anyPlayer && anyEnemy) return false;

            if (!anyPlayer)
                Debug.Log("[TurnManager] All players defeated. Combat over.");
            else
                Debug.Log("[TurnManager] All enemies defeated. Victory!");

            EndCombat(!anyPlayer ? EncounterResult.Defeat : EncounterResult.Victory);
            return true;
        }

        private void ApplyStartTurnEffects(EntityHandle actor, EntityData data)
        {
            if (!actor.IsValid || data == null)
                return;

            conditionDeltaBuffer.Clear();
            conditionService.TickStartTurn(data, conditionDeltaBuffer);

            for (int i = 0; i < conditionDeltaBuffer.Count; i++)
                PublishConditionChanged(conditionDeltaBuffer[i]);
        }

        private void ApplyEndTurnEffects(EntityHandle actor, EntityData data)
        {
            if (!actor.IsValid || data == null)
                return;

            conditionDeltaBuffer.Clear();
            conditionService.TickEndTurn(data, conditionDeltaBuffer);

            for (int i = 0; i < conditionDeltaBuffer.Count; i++)
                PublishConditionChanged(conditionDeltaBuffer[i]);

            if (conditionDeltaBuffer.Count > 0)
                PublishConditionsTicked(new ConditionsTickedEvent(actor, conditionDeltaBuffer));
        }

        private void ApplyDelayImmediateEffects(EntityHandle actor, EntityData data)
        {
            if (!actor.IsValid || data == null)
                return;

            // 29c.1 MVP scope: reuse current modeled end-of-turn condition ticks (e.g. Frightened/Sickened decay).
            // Full RAW Delay timing coverage (persistent damage, beneficial expirations during the turn, etc.) will be
            // expanded in later Delay phases without routing through regular EndTurn().
            ApplyEndTurnEffects(actor, data);
        }

        private bool TryDelayCurrentTurnInternal(EntityHandle plannedReturnAfterActor)
        {
            if (!CanDelayCurrentTurn())
                return false;

            var actor = CurrentEntity;
            var data = entityManager.Registry.Get(actor);
            if (data == null)
                return false;

            SetDelayTurnBeginTriggerOpen(false, actor);
            SetDelayPlacementSelectionOpen(false, actor);

            ApplyDelayImmediateEffects(actor, data);

            if (currentIndex < 0 || currentIndex >= initiativeOrder.Count || initiativeOrder[currentIndex].Handle != actor)
            {
                Debug.LogError("[TurnManager] TryDelayCurrentTurn lost current initiative position. Delay aborted.");
                return false;
            }

            var currentEntry = initiativeOrder[currentIndex];
            var delayedRecord = new DelayedTurnRecord(
                actor,
                roundNumber,
                delayImmediateEffectsApplied: true,
                isPlannedAutoResume: plannedReturnAfterActor.IsValid,
                originalAnchorActor: GetPreviousInitiativeActorForCurrentIndex(),
                plannedReturnAfterActor: plannedReturnAfterActor,
                initiativeEntry: currentEntry);

            delayedTurns[actor] = delayedRecord;
            delayReactionSuppressed.Add(actor);
            PublishDelayedTurnEntered(actor, plannedReturnAfterActor);

            initiativeOrder.RemoveAt(currentIndex);

            if (initiativeOrder.Count == 0)
            {
                Debug.LogWarning("[TurnManager] Delay removed the only active initiative entry unexpectedly. Restoring actor to initiative.");
                initiativeOrder.Insert(0, currentEntry);
                delayedTurns.Remove(actor);
                delayReactionSuppressed.Remove(actor);
                return false;
            }

            if (CheckVictory()) return true;

            if (currentIndex >= initiativeOrder.Count)
                StartNextRound();
            else
                StartCurrentTurn();

            return true;
        }

        private bool TryOpenDelayReturnWindow(EntityHandle afterActor)
        {
            ExpireDelayedTurnsIfDueAfter(afterActor);

            if (delayedTurns.Count <= 0)
                return false;

            bool hasPlannedPlayerDelayed = HasEligiblePlayerControlledPlannedDelayedActor();

            if (TryAutoResumePlannedDelayedActor(afterActor))
                return true;

            if (hasPlannedPlayerDelayed && !HasEligiblePlayerControlledManualDelayedActor())
            {
                // A planned delay exists but its anchor was not this actor. Keep advancing without fallback window.
                // This prevents Return/Skip UI from appearing for Owlcat-style preselected delays.
                return false;
            }

            if (!HasEligiblePlayerControlledManualDelayedActor())
                return false; // 29c.2 auto-skip when no eligible player delayed actors.

            OpenDelayReturnWindow(afterActor);
            return true;
        }

        private void ExpireDelayedTurnsIfDueAfter(EntityHandle afterActor)
        {
            if (!afterActor.IsValid || delayedTurns.Count <= 0)
                return;

            if (roundNumber <= 0)
                return;

            List<DelayedTurnRecord> expired = null;

            foreach (var kvp in delayedTurns)
            {
                var record = kvp.Value;
                if (record.delayedRoundNumber >= roundNumber)
                    continue;
                if (record.originalAnchorActor != afterActor)
                    continue;

                expired ??= new List<DelayedTurnRecord>();
                expired.Add(record);
            }

            if (expired == null || expired.Count == 0)
                return;

            for (int i = 0; i < expired.Count; i++)
            {
                var record = expired[i];
                if (!delayedTurns.Remove(record.actor))
                    continue;

                delayReactionSuppressed.Remove(record.actor);

                int insertIndex = GetInsertionIndexAfterAnchor(afterActor);
                initiativeOrder.Insert(insertIndex, record.initiativeEntry);
                PublishDelayedTurnExpired(record.actor, afterActor);
            }
        }

        private bool TryAutoResumePlannedDelayedActor(EntityHandle afterActor)
        {
            if (!afterActor.IsValid || delayedTurns.Count <= 0)
                return false;
            if (entityManager == null || entityManager.Registry == null)
                return false;

            List<EntityHandle> matchingPlannedPlayers = null;

            foreach (var kvp in delayedTurns)
            {
                var record = kvp.Value;
                if (!record.isPlannedAutoResume)
                    continue;
                if (record.plannedReturnAfterActor != afterActor)
                    continue;

                var data = entityManager.Registry.Get(record.actor);
                if (data == null || !data.IsAlive)
                    continue;
                if (data.Team != Team.Player)
                    continue; // MVP scope

                matchingPlannedPlayers ??= new List<EntityHandle>(4);
                matchingPlannedPlayers.Add(record.actor);
            }

            if (matchingPlannedPlayers == null || matchingPlannedPlayers.Count == 0)
                return false;

            matchingPlannedPlayers.Sort(static (a, b) => a.Id.CompareTo(b.Id));

            // If multiple delayed actors selected the same planned anchor, auto-chain them so they resume
            // sequentially after one another without opening the manual Return/Skip window.
            for (int i = 1; i < matchingPlannedPlayers.Count; i++)
            {
                var actor = matchingPlannedPlayers[i];
                var nextAnchor = matchingPlannedPlayers[i - 1];
                if (!delayedTurns.TryGetValue(actor, out var record))
                    continue;

                delayedTurns[actor] = new DelayedTurnRecord(
                    actor: record.actor,
                    delayedRoundNumber: record.delayedRoundNumber,
                    delayImmediateEffectsApplied: record.delayImmediateEffectsApplied,
                    isPlannedAutoResume: true,
                    originalAnchorActor: record.originalAnchorActor,
                    plannedReturnAfterActor: nextAnchor,
                    initiativeEntry: record.initiativeEntry);
            }

            var chosen = matchingPlannedPlayers[0];
            if (!chosen.IsValid)
                return false;

            return TryResumeDelayedActorInternal(chosen, afterActor);
        }

        private int GetInsertionIndexAfterAnchor(EntityHandle anchorActor)
        {
            if (initiativeOrder == null || initiativeOrder.Count == 0)
                return 0;

            int anchorIndex = FindInitiativeIndex(anchorActor);
            if (anchorIndex >= 0)
                return anchorIndex + 1;

            if (currentIndex >= 0 && currentIndex < initiativeOrder.Count)
                return currentIndex + 1;

            return initiativeOrder.Count;
        }

        private int FindInitiativeIndex(EntityHandle actor)
        {
            if (!actor.IsValid)
                return -1;

            for (int i = 0; i < initiativeOrder.Count; i++)
            {
                if (initiativeOrder[i].Handle == actor)
                    return i;
            }

            return -1;
        }

        private bool HasEligiblePlayerControlledManualDelayedActor()
        {
            if (entityManager == null || entityManager.Registry == null)
                return false;

            foreach (var kvp in delayedTurns)
            {
                var actor = kvp.Key;
                var record = kvp.Value;
                var data = entityManager.Registry.Get(actor);
                if (data == null || !data.IsAlive) continue;
                if (data.Team != Team.Player) continue; // 29c.2 MVP scope
                if (record.isPlannedAutoResume) continue; // planned delays should auto-resume without manual window
                return true;
            }

            return false;
        }

        private bool HasEligiblePlayerControlledPlannedDelayedActor()
        {
            if (entityManager == null || entityManager.Registry == null)
                return false;

            foreach (var kvp in delayedTurns)
            {
                var actor = kvp.Key;
                var record = kvp.Value;
                var data = entityManager.Registry.Get(actor);
                if (data == null || !data.IsAlive) continue;
                if (data.Team != Team.Player) continue;
                if (!record.isPlannedAutoResume) continue;
                return true;
            }

            return false;
        }

        private void OpenTurnForActor(EntityHandle actor, EntityData data, bool openDelayTriggerWindow)
        {
            if (!actor.IsValid || data == null)
                return;

            state = data.Team == Team.Player ? TurnState.PlayerTurn : TurnState.EnemyTurn;
            SetDelayTurnBeginTriggerOpen(openDelayTriggerWindow, actor);
            SetDelayPlacementSelectionOpen(false, actor);

            if (entityManager != null)
                entityManager.SelectEntity(actor);

            PublishTurnStarted(new TurnStartedEvent(actor, data.ActionsRemaining));
            PublishActionsChanged(new ActionsChangedEvent(actor, data.ActionsRemaining));

            Debug.Log($"[TurnManager] Round {roundNumber} — {data.Name} ({data.Team}) starts turn. Actions: {data.ActionsRemaining}");
        }

        private void AdvanceInitiativeAfterTurnEnd()
        {
            currentIndex++;

            if (currentIndex >= initiativeOrder.Count)
                StartNextRound();
            else
                StartCurrentTurn();
        }

        private EntityHandle GetPreviousInitiativeActorForCurrentIndex()
        {
            if (currentIndex < 0 || currentIndex >= initiativeOrder.Count || initiativeOrder.Count <= 1)
                return EntityHandle.None;

            int prevIndex = currentIndex - 1;
            if (prevIndex < 0)
                prevIndex = initiativeOrder.Count - 1;

            return initiativeOrder[prevIndex].Handle;
        }

        private void ResetDelayState()
        {
            SetDelayTurnBeginTriggerOpen(false);
            SetDelayPlacementSelectionOpen(false);
            ClearDelayReturnWindowIfOpen();
            delayedTurns.Clear();
            delayReactionSuppressed.Clear();
        }

        private void ResetActionExecutionTracking()
        {
            executingActor = EntityHandle.None;
            executingActionSource = string.Empty;
            executingActionStartTime = -1f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            actionLockWarned = false;
#endif
        }

        private void ResolveEventBusIfMissing()
        {
            if (eventBus != null) return;

            eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            if (eventBus == null)
                Debug.LogWarning("[TurnManager] CombatEventBus not found. Typed bus publish is disabled.", this);
        }

        private void PublishCombatStarted()
        {
            OnCombatStarted?.Invoke(default);
            eventBus?.PublishCombatStarted();
        }

        private void PublishCombatEnded(CombatEndedEvent e)
        {
            OnCombatEndedWithResult?.Invoke(e);
            eventBus?.PublishCombatEnded(e.result);
        }

        private void PublishRoundStarted(RoundStartedEvent e)
        {
            OnRoundStarted?.Invoke(e);
            eventBus?.PublishRoundStarted(e.round);
        }

        private void PublishTurnStarted(TurnStartedEvent e)
        {
            OnTurnStarted?.Invoke(e);
            eventBus?.PublishTurnStarted(e.actor, e.actionsAtStart);
        }

        private void PublishTurnEnded(TurnEndedEvent e)
        {
            OnTurnEnded?.Invoke(e);
            eventBus?.PublishTurnEnded(e.actor);
        }

        private void PublishActionsChanged(ActionsChangedEvent e)
        {
            OnActionsChanged?.Invoke(e);
            if (e.actor.IsValid)
                eventBus?.PublishActionsChanged(e.actor, e.remaining);
        }

        private void PublishConditionsTicked(ConditionsTickedEvent e)
        {
            OnConditionsTicked?.Invoke(e);
            eventBus?.PublishConditionsTicked(e.actor, e.ticks);
        }

        private void PublishConditionChanged(ConditionDelta delta)
        {
            eventBus?.PublishConditionChanged(
                delta.entity,
                delta.type,
                delta.changeType,
                delta.oldValue,
                delta.newValue,
                delta.oldRemainingRounds,
                delta.newRemainingRounds);
        }

        private void PublishInitiativeRolled(InitiativeRolledEvent e)
        {
            OnInitiativeRolled?.Invoke(e);
            eventBus?.PublishInitiativeRolled(e.order);
        }

        private void PublishDelayTurnBeginTriggerChanged(EntityHandle actor, bool isOpen)
        {
            eventBus?.PublishDelayTurnBeginTriggerChanged(actor, isOpen);
        }

        private void PublishDelayPlacementSelectionChanged(EntityHandle actor, bool isOpen)
        {
            eventBus?.PublishDelayPlacementSelectionChanged(actor, isOpen);
        }

        private void PublishDelayReturnWindowOpened(EntityHandle afterActor)
        {
            eventBus?.PublishDelayReturnWindowOpened(afterActor);
        }

        private void PublishDelayReturnWindowClosed(EntityHandle afterActor)
        {
            eventBus?.PublishDelayReturnWindowClosed(afterActor);
        }

        private void PublishDelayedTurnEntered(EntityHandle actor, EntityHandle plannedReturnAfterActor)
        {
            eventBus?.PublishDelayedTurnEntered(actor, plannedReturnAfterActor);
        }

        private void PublishDelayedTurnResumed(EntityHandle actor, EntityHandle afterActor, bool wasPlanned)
        {
            eventBus?.PublishDelayedTurnResumed(actor, afterActor, wasPlanned);
        }

        private void PublishDelayedTurnExpired(EntityHandle actor, EntityHandle afterActor)
        {
            eventBus?.PublishDelayedTurnExpired(actor, afterActor);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (state != TurnState.ExecutingAction || executingActionStartTime < 0f)
                return;

            float elapsed = Time.unscaledTime - executingActionStartTime;
            if (!actionLockWarned && elapsed >= ActionLockWarnSeconds)
            {
                actionLockWarned = true;
                Debug.LogWarning(
                    $"[TurnManager] Action lock running for {elapsed:0.00}s. " +
                    $"Actor={executingActor.Id}, Source={executingActionSource}, StateBefore={stateBeforeExecution}.");
            }

            if (elapsed >= ActionLockForceReleaseSeconds)
            {
                Debug.LogError(
                    $"[TurnManager] Action lock exceeded {ActionLockForceReleaseSeconds:0}s. " +
                    $"Force-releasing lock. Actor={executingActor.Id}, Source={executingActionSource}.");
                ActionCompleted();
            }
        }
#endif
    }
}
