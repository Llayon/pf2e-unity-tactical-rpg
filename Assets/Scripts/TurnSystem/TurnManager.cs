using System;
using System.Collections.Generic;
using System.Linq;
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

        [Header("Debug — visible in Inspector")]
        [SerializeField] private TurnState state = TurnState.Inactive;
        [SerializeField] private int currentIndex = -1;
        [SerializeField] private int roundNumber = 0;

        private List<InitiativeEntry> initiativeOrder = new();
        private TurnState stateBeforeExecution;

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

        // ─── Events ───────────────────────────────────────────────────────────

        public event Action                                   OnCombatStarted;
        public event Action                                   OnCombatEnded;
        public event Action<int>                              OnRoundStarted;         // roundNumber
        public event Action<EntityHandle>                     OnTurnStarted;          // entityHandle
        public event Action<EntityHandle>                     OnTurnEnded;            // entityHandle
        public event Action<int>                              OnActionsChanged;       // actionsRemaining
        public event Action<IReadOnlyList<InitiativeEntry>>   OnInitiativeRolled;

        // ─── Public Methods ───────────────────────────────────────────────────

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

            state = TurnState.RollingInitiative;
            RollInitiative();
            OnCombatStarted?.Invoke();
            OnInitiativeRolled?.Invoke(initiativeOrder);
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

            var endingEntity = CurrentEntity;
            var data = entityManager.Registry.Get(endingEntity);
            if (data != null)
                data.EndTurn();

            OnTurnEnded?.Invoke(endingEntity);
            currentIndex++;

            if (currentIndex >= initiativeOrder.Count)
                StartNextRound();
            else
                StartCurrentTurn();
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
            OnActionsChanged?.Invoke(data.ActionsRemaining);

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

        /// <summary>
        /// Transition to ExecutingAction to block input while an animation plays.
        /// </summary>
        public void BeginActionExecution()
        {
            stateBeforeExecution = state;
            state = TurnState.ExecutingAction;
        }

        /// <summary>
        /// Called when the executing action finishes. Restores previous turn state.
        /// If no actions remain, ends the turn automatically.
        /// </summary>
        public void ActionCompleted()
        {
            if (state != TurnState.ExecutingAction) return;

            state = stateBeforeExecution;

            if (ActionsRemaining <= 0)
                EndTurn();
            else
                OnActionsChanged?.Invoke(ActionsRemaining);
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
            if (entityManager != null && entityManager.Registry != null)
                data = entityManager.Registry.Get(CurrentEntity);

            if (data != null && actionCost > 0)
                data.SpendActions(actionCost);

            // 2) Restore state from ExecutingAction
            state = stateBeforeExecution;

            // 3) Notify action count change
            int remaining = (data != null) ? data.ActionsRemaining : 0;
            OnActionsChanged?.Invoke(remaining);

            // 4) Auto-EndTurn if drained
            if (remaining <= 0)
                EndTurn();
        }


        /// <summary>
        /// Terminate the combat encounter and reset all state.
        /// </summary>
        public void EndCombat()
        {
            state = TurnState.CombatOver;

            if (entityManager != null)
                entityManager.DeselectEntity();

            OnCombatEnded?.Invoke();

            // Full reset
            state = TurnState.Inactive;
            currentIndex = -1;
            roundNumber = 0;
            initiativeOrder.Clear();
        }

        // ─── Private Methods ──────────────────────────────────────────────────

        private void RollInitiative()
        {
            initiativeOrder.Clear();

            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null || !data.IsAlive) continue;

                                int roll       = UnityEngine.Random.Range(1, 21); // d20: 1-20 inclusive (max is exclusive)
                int modifier   = data.WisMod;
                bool isPlayer  = data.Team == Team.Player;

                initiativeOrder.Add(new InitiativeEntry
                {
                    Handle   = data.Handle,
                    Roll     = roll,
                    Modifier = modifier,
                    IsPlayer = isPlayer,
                });
            }

            initiativeOrder.Sort((a, b) => b.SortValue.CompareTo(a.SortValue));

            var sb = new System.Text.StringBuilder("[TurnManager] Initiative order:\n");
            for (int i = 0; i < initiativeOrder.Count; i++)
            {
                var e    = initiativeOrder[i];
                var d    = entityManager.Registry.Get(e.Handle);
                string n = d?.Name ?? e.Handle.Id.ToString();
                sb.AppendLine($"  {i + 1}. {n} — Roll: {e.Roll}, Mod: {e.Modifier}, SortValue: {e.SortValue}");
            }
            Debug.Log(sb.ToString());
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

            if (initiativeOrder.Count == 0)
            {
                EndCombat();
                return;
            }

            OnRoundStarted?.Invoke(roundNumber);
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

            data.StartTurn();
            state = data.Team == Team.Player ? TurnState.PlayerTurn : TurnState.EnemyTurn;

            if (entityManager != null)
                entityManager.SelectEntity(entry.Handle);

            OnTurnStarted?.Invoke(entry.Handle);
            OnActionsChanged?.Invoke(data.ActionsRemaining);

            Debug.Log($"[TurnManager] Round {roundNumber} — {data.Name} ({data.Team}) starts turn. Actions: {data.ActionsRemaining}");

            // Auto-skip enemy turns (no AI yet)
            if (state == TurnState.EnemyTurn)
            {
                Debug.Log($"[TurnManager] Auto-skipping enemy turn: {data.Name}");
                EndTurn();
            }
        }
    }
}
