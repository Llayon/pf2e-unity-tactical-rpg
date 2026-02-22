using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.Presentation;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class CombatRoundRegressionPlayModeTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string SampleSceneName = "SampleScene";
        private const float DefaultTimeoutSeconds = 8f;
        private const float SimulationTimeoutSeconds = 25f;
        private static float MaxExecutingActionSeconds => Application.isBatchMode ? 8f : 3f;
        private static float MaxNoProgressSeconds => Application.isBatchMode ? 14f : 6f;

        private TurnManager turnManager;
        private EntityManager entityManager;
        private CombatEventBus eventBus;
        private PlayerActionExecutor playerActionExecutor;
        private GridManager gridManager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene(SampleSceneName);
            yield return WaitUntilOrTimeout(
                () => SceneManager.GetActiveScene().name == SampleSceneName,
                DefaultTimeoutSeconds,
                "SampleScene did not load.");

            // Let scene objects finish startup.
            yield return null;
            ResolveSceneReferences();

            yield return WaitUntilOrTimeout(
                () => entityManager.Registry != null && entityManager.Registry.Count >= 2,
                DefaultTimeoutSeconds,
                "EntityManager registry was not populated.");
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_401_MultiRound_PlayerStride_EnemyStrike_ReachesRound2()
        {
            int latestRound = 0;
            bool playerStrideSeen = false;
            bool enemyStrikeSeen = false;

            void RoundHandler(in RoundStartedEvent e) => latestRound = e.round;

            void StrideHandler(in StrideCompletedEvent e)
            {
                var data = entityManager.Registry.Get(e.actor);
                if (data != null && data.Team == Team.Player)
                    playerStrideSeen = true;
            }

            void StrikeHandler(in StrikeResolvedEvent e)
            {
                var data = entityManager.Registry.Get(e.attacker);
                if (data != null && data.Team == Team.Enemy)
                    enemyStrikeSeen = true;
            }

            eventBus.OnRoundStartedTyped += RoundHandler;
            eventBus.OnStrideCompletedTyped += StrideHandler;
            eventBus.OnStrikeResolved += StrikeHandler;

            try
            {
                BoostAllCombatantsHP(200);

                var fighter = GetEntityByName("Fighter");
                var goblin1 = GetEntityByName("Goblin_1");
                MoveEntityToCell(goblin1, fighter.GridPosition + Vector3Int.right);

                turnManager.StartCombat();

                yield return SimulateCombatUntil(
                    () => latestRound >= 2 && playerStrideSeen && enemyStrikeSeen,
                    SimulationTimeoutSeconds,
                    "Did not observe expected multi-round player/enemy actions.");

                Assert.GreaterOrEqual(latestRound, 2);
                Assert.IsTrue(playerStrideSeen, "Expected at least one real player Stride action.");
                Assert.IsTrue(enemyStrikeSeen, "Expected at least one real enemy Strike action.");
            }
            finally
            {
                eventBus.OnRoundStartedTyped -= RoundHandler;
                eventBus.OnStrideCompletedTyped -= StrideHandler;
                eventBus.OnStrikeResolved -= StrikeHandler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_402_ConditionTick_Frightened_DecrementsOnEndTurn()
        {
            var fighter = GetEntityByName("Fighter");
            fighter.Wisdom = 5000; // deterministic first actor
            var conditionService = new ConditionService();
            var seedDeltas = new List<ConditionDelta>(2);
            conditionService.Apply(fighter, ConditionType.Frightened, value: 2, rounds: -1, seedDeltas);

            ConditionChangedEvent observed = default;
            bool tickSeen = false;

            void ConditionHandler(in ConditionChangedEvent e)
            {
                if (e.entity != fighter.Handle) return;
                if (e.conditionType != ConditionType.Frightened) return;
                if (e.changeType != ConditionChangeType.ValueChanged) return;

                observed = e;
                tickSeen = true;
            }

            eventBus.OnConditionChangedTyped += ConditionHandler;

            try
            {
                turnManager.StartCombat();

                yield return WaitUntilOrTimeout(
                    () => turnManager.State == TurnState.PlayerTurn && turnManager.CurrentEntity == fighter.Handle,
                    DefaultTimeoutSeconds,
                    "Fighter did not become the current actor.");

                turnManager.EndTurn();

                yield return WaitUntilOrTimeout(
                    () => tickSeen,
                    DefaultTimeoutSeconds,
                    "Did not receive frightened condition tick event on end turn.");

                Assert.AreEqual(2, observed.oldValue);
                Assert.AreEqual(1, observed.newValue);
                Assert.AreEqual(1, fighter.GetConditionValue(ConditionType.Frightened));
            }
            finally
            {
                eventBus.OnConditionChangedTyped -= ConditionHandler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P18_PM_408_ConditionTick_SlowedDurationOnly_DecrementsWithoutRemoval_AndPublishesLog()
        {
            var fighter = GetEntityByName("Fighter");
            fighter.Wisdom = 5000; // deterministic first actor
            var conditionService = new ConditionService();
            var seedDeltas = new List<ConditionDelta>(2);
            conditionService.Apply(fighter, ConditionType.Slowed, value: 1, rounds: 3, seedDeltas);

            ConditionChangedEvent observedChange = default;
            ConditionDelta observedTick = default;
            bool durationChangeSeen = false;
            bool tickSeen = false;
            bool logSeen = false;
            string logMessage = null;

            void ChangeHandler(in ConditionChangedEvent e)
            {
                if (e.entity != fighter.Handle) return;
                if (e.conditionType != ConditionType.Slowed) return;
                if (e.changeType != ConditionChangeType.DurationChanged) return;
                observedChange = e;
                durationChangeSeen = true;
            }

            void TickHandler(in ConditionsTickedEvent e)
            {
                if (e.actor != fighter.Handle) return;
                if (e.ticks == null || e.ticks.Count == 0) return;

                for (int i = 0; i < e.ticks.Count; i++)
                {
                    if (e.ticks[i].type != ConditionType.Slowed) continue;
                    observedTick = e.ticks[i];
                    tickSeen = true;
                    break;
                }
            }

            void LogHandler(CombatLogEntry entry)
            {
                if (entry.Actor != fighter.Handle) return;
                if (entry.Category != CombatLogCategory.Condition) return;
                if (string.IsNullOrEmpty(entry.Message)) return;
                if (!entry.Message.Contains("slowed duration decreases to 2")) return;
                logMessage = entry.Message;
                logSeen = true;
            }

            eventBus.OnConditionChangedTyped += ChangeHandler;
            eventBus.OnConditionsTickedTyped += TickHandler;
            eventBus.OnLogEntry += LogHandler;

            try
            {
                turnManager.StartCombat();

                yield return WaitUntilOrTimeout(
                    () => turnManager.State == TurnState.PlayerTurn && turnManager.CurrentEntity == fighter.Handle,
                    DefaultTimeoutSeconds,
                    "Fighter did not become the first actor.");

                Assert.AreEqual(2, turnManager.ActionsRemaining, "Slowed 1 should reduce first-turn actions to 2.");

                turnManager.EndTurn();

                yield return WaitUntilOrTimeout(
                    () => durationChangeSeen && tickSeen && logSeen,
                    DefaultTimeoutSeconds,
                    "Did not receive duration-changed condition signals/log for slowed.");

                Assert.AreEqual(1, observedChange.oldValue);
                Assert.AreEqual(1, observedChange.newValue);
                Assert.AreEqual(3, observedChange.oldRemainingRounds);
                Assert.AreEqual(2, observedChange.newRemainingRounds);

                Assert.AreEqual(ConditionChangeType.DurationChanged, observedTick.changeType);
                Assert.AreEqual(1, observedTick.oldValue);
                Assert.AreEqual(1, observedTick.newValue);
                Assert.AreEqual(3, observedTick.oldRemainingRounds);
                Assert.AreEqual(2, observedTick.newRemainingRounds);
                Assert.IsFalse(observedTick.removed);

                var slowed = fighter.Conditions.Find(c => c.Type == ConditionType.Slowed);
                Assert.IsNotNull(slowed, "Slowed should remain after one duration decrement.");
                Assert.AreEqual(2, slowed.RemainingRounds);
                Assert.AreEqual("slowed duration decreases to 2", logMessage);
            }
            finally
            {
                eventBus.OnConditionChangedTyped -= ChangeHandler;
                eventBus.OnConditionsTickedTyped -= TickHandler;
                eventBus.OnLogEntry -= LogHandler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P18_PM_409_ConditionTick_SlowedDurationOne_RemovesOnEndTurn_WithEventsAndLog()
        {
            var fighter = GetEntityByName("Fighter");
            fighter.Wisdom = 5000; // deterministic first actor
            var conditionService = new ConditionService();
            var seedDeltas = new List<ConditionDelta>(2);
            conditionService.Apply(fighter, ConditionType.Slowed, value: 1, rounds: 1, seedDeltas);

            ConditionChangedEvent observedChange = default;
            ConditionDelta observedTick = default;
            bool removedSeen = false;
            bool tickSeen = false;
            bool logSeen = false;
            string logMessage = null;

            void ChangeHandler(in ConditionChangedEvent e)
            {
                if (e.entity != fighter.Handle) return;
                if (e.conditionType != ConditionType.Slowed) return;
                if (e.changeType != ConditionChangeType.Removed) return;
                observedChange = e;
                removedSeen = true;
            }

            void TickHandler(in ConditionsTickedEvent e)
            {
                if (e.actor != fighter.Handle) return;
                if (e.ticks == null || e.ticks.Count == 0) return;

                for (int i = 0; i < e.ticks.Count; i++)
                {
                    if (e.ticks[i].type != ConditionType.Slowed) continue;
                    observedTick = e.ticks[i];
                    tickSeen = true;
                    break;
                }
            }

            void LogHandler(CombatLogEntry entry)
            {
                if (entry.Actor != fighter.Handle) return;
                if (entry.Category != CombatLogCategory.Condition) return;
                if (!string.Equals(entry.Message, "loses slowed", StringComparison.Ordinal)) return;
                logMessage = entry.Message;
                logSeen = true;
            }

            eventBus.OnConditionChangedTyped += ChangeHandler;
            eventBus.OnConditionsTickedTyped += TickHandler;
            eventBus.OnLogEntry += LogHandler;

            try
            {
                turnManager.StartCombat();

                yield return WaitUntilOrTimeout(
                    () => turnManager.State == TurnState.PlayerTurn && turnManager.CurrentEntity == fighter.Handle,
                    DefaultTimeoutSeconds,
                    "Fighter did not become the first actor.");

                Assert.AreEqual(2, turnManager.ActionsRemaining, "Slowed 1 should reduce first-turn actions to 2.");

                turnManager.EndTurn();

                yield return WaitUntilOrTimeout(
                    () => removedSeen && tickSeen && logSeen,
                    DefaultTimeoutSeconds,
                    "Did not receive slowed removal condition signals/log.");

                Assert.AreEqual(1, observedChange.oldValue);
                Assert.AreEqual(0, observedChange.newValue);
                Assert.AreEqual(1, observedChange.oldRemainingRounds);
                Assert.AreEqual(0, observedChange.newRemainingRounds);

                Assert.AreEqual(ConditionChangeType.Removed, observedTick.changeType);
                Assert.AreEqual(1, observedTick.oldValue);
                Assert.AreEqual(0, observedTick.newValue);
                Assert.AreEqual(1, observedTick.oldRemainingRounds);
                Assert.AreEqual(0, observedTick.newRemainingRounds);
                Assert.IsTrue(observedTick.removed);
                Assert.IsFalse(fighter.HasCondition(ConditionType.Slowed));
                Assert.AreEqual("loses slowed", logMessage);
            }
            finally
            {
                eventBus.OnConditionChangedTyped -= ChangeHandler;
                eventBus.OnConditionsTickedTyped -= TickHandler;
                eventBus.OnLogEntry -= LogHandler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P18_PM_410_StrikeAttackBonus_UsesMaxStatusPenalty_NotSum()
        {
            var fighter = GetEntityByName("Fighter");
            var goblin1 = GetEntityByName("Goblin_1");
            fighter.Wisdom = 5000; // deterministic first actor
            MoveEntityToCell(goblin1, fighter.GridPosition + Vector3Int.right);

            var conditionService = new ConditionService();
            var seedDeltas = new List<ConditionDelta>(4);
            conditionService.Apply(fighter, ConditionType.Frightened, value: 2, rounds: -1, seedDeltas);
            conditionService.Apply(fighter, ConditionType.Sickened, value: 3, rounds: -1, seedDeltas);

            StrikeResolvedEvent observed = default;
            bool strikeSeen = false;

            void StrikeHandler(in StrikeResolvedEvent e)
            {
                if (e.attacker != fighter.Handle) return;
                if (e.target != goblin1.Handle) return;
                observed = e;
                strikeSeen = true;
            }

            eventBus.OnStrikeResolved += StrikeHandler;

            try
            {
                turnManager.StartCombat();

                yield return WaitUntilOrTimeout(
                    () => turnManager.State == TurnState.PlayerTurn && turnManager.CurrentEntity == fighter.Handle,
                    DefaultTimeoutSeconds,
                    "Fighter did not become the first actor.");

                int expectedStatusPenalty = Mathf.Max(2, 3);
                int expectedAttackBonus =
                    fighter.GetProficiencyBonus(fighter.GetWeaponProfRank(fighter.EquippedWeapon.Category))
                    + fighter.GetAttackAbilityMod(fighter.EquippedWeapon)
                    + fighter.EquippedWeapon.Potency
                    - expectedStatusPenalty;

                Assert.AreEqual(expectedStatusPenalty, fighter.ConditionPenaltyToAttack, "Attack penalty should use max status value, not sum.");

                bool started = playerActionExecutor.TryExecuteStrike(goblin1.Handle);
                Assert.IsTrue(started, "Player strike did not start.");

                yield return WaitUntilOrTimeout(
                    () => strikeSeen,
                    DefaultTimeoutSeconds,
                    "Did not receive strike event for fighter.");

                Assert.AreEqual(expectedAttackBonus, observed.attackBonus, "Strike attack bonus should use max(Frightened,Sickened) status penalty.");
                Assert.AreEqual(0, observed.mapPenalty, "First strike in turn should have MAP 0.");
            }
            finally
            {
                eventBus.OnStrikeResolved -= StrikeHandler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P18_PM_411_StrikeDC_TargetPenalties_UseMaxStatusAndSingleCircumstance()
        {
            var fighter = GetEntityByName("Fighter");
            var goblin1 = GetEntityByName("Goblin_1");
            fighter.Wisdom = 5000; // deterministic first actor
            MoveEntityToCell(goblin1, fighter.GridPosition + Vector3Int.right);

            var conditionService = new ConditionService();
            var seedDeltas = new List<ConditionDelta>(6);
            conditionService.Apply(goblin1, ConditionType.OffGuard, value: 0, rounds: -1, seedDeltas);
            conditionService.Apply(goblin1, ConditionType.Prone, value: 0, rounds: -1, seedDeltas);
            conditionService.Apply(goblin1, ConditionType.Frightened, value: 2, rounds: -1, seedDeltas);
            conditionService.Apply(goblin1, ConditionType.Sickened, value: 3, rounds: -1, seedDeltas);

            StrikeResolvedEvent observed = default;
            bool strikeSeen = false;

            void StrikeHandler(in StrikeResolvedEvent e)
            {
                if (e.attacker != fighter.Handle) return;
                if (e.target != goblin1.Handle) return;
                observed = e;
                strikeSeen = true;
            }

            eventBus.OnStrikeResolved += StrikeHandler;

            try
            {
                turnManager.StartCombat();

                yield return WaitUntilOrTimeout(
                    () => turnManager.State == TurnState.PlayerTurn && turnManager.CurrentEntity == fighter.Handle,
                    DefaultTimeoutSeconds,
                    "Fighter did not become the first actor.");

                int expectedStatusPenalty = Mathf.Max(2, 3);
                int expectedCircumstancePenalty = 2; // OffGuard || Prone, no double count
                int expectedDC = goblin1.BaseAC - expectedStatusPenalty - expectedCircumstancePenalty;

                bool started = playerActionExecutor.TryExecuteStrike(goblin1.Handle);
                Assert.IsTrue(started, "Player strike did not start.");

                yield return WaitUntilOrTimeout(
                    () => strikeSeen,
                    DefaultTimeoutSeconds,
                    "Did not receive strike event against goblin.");

                Assert.AreEqual(expectedDC, observed.dc, "Target AC/DC should use max status + single circumstance penalty.");
                Assert.AreEqual(expectedDC, goblin1.EffectiveAC, "Entity EffectiveAC should match strike payload DC.");
            }
            finally
            {
                eventBus.OnStrikeResolved -= StrikeHandler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_403_NoExecutingActionDeadlock_ThroughRound3()
        {
            int latestRound = 0;
            void RoundHandler(in RoundStartedEvent e) => latestRound = e.round;
            eventBus.OnRoundStartedTyped += RoundHandler;

            try
            {
                BoostAllCombatantsHP(220);

                var fighter = GetEntityByName("Fighter");
                var goblin1 = GetEntityByName("Goblin_1");
                MoveEntityToCell(goblin1, fighter.GridPosition + Vector3Int.right);

                turnManager.StartCombat();

                yield return SimulateCombatUntil(
                    () => latestRound >= 3,
                    SimulationTimeoutSeconds,
                    "Combat did not reach round 3.");

                Assert.AreNotEqual(
                    TurnState.ExecutingAction,
                    turnManager.State,
                    "TurnManager remained in ExecutingAction after multi-turn simulation.");
            }
            finally
            {
                eventBus.OnRoundStartedTyped -= RoundHandler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P18_PM_406_EndTurnEventOrder_ConditionsThenTurnEndedThenNextTurnStarted()
        {
            var fighter = GetEntityByName("Fighter");
            fighter.Wisdom = 5000; // deterministic first actor
            var conditionService = new ConditionService();
            var seedDeltas = new List<ConditionDelta>(2);
            conditionService.Apply(fighter, ConditionType.Frightened, value: 2, rounds: -1, seedDeltas);

            var order = new List<string>(4);
            bool nextTurnStarted = false;

            void ConditionsHandler(in ConditionsTickedEvent e)
            {
                if (e.actor != fighter.Handle) return;
                order.Add("conditions");
            }

            void TurnEndedHandler(in TurnEndedEvent e)
            {
                if (e.actor != fighter.Handle) return;
                order.Add("turn_ended");
            }

            void TurnStartedHandler(in TurnStartedEvent e)
            {
                if (e.actor == fighter.Handle) return; // ignore initial fighter turn-start
                if (nextTurnStarted) return;
                nextTurnStarted = true;
                order.Add("next_turn_started");
            }

            try
            {
                turnManager.StartCombat();

                yield return WaitUntilOrTimeout(
                    () => turnManager.State == TurnState.PlayerTurn && turnManager.CurrentEntity == fighter.Handle,
                    DefaultTimeoutSeconds,
                    "Fighter did not become the first actor.");

                eventBus.OnConditionsTickedTyped += ConditionsHandler;
                eventBus.OnTurnEndedTyped += TurnEndedHandler;
                eventBus.OnTurnStartedTyped += TurnStartedHandler;

                turnManager.EndTurn();

                yield return WaitUntilOrTimeout(
                    () => nextTurnStarted,
                    DefaultTimeoutSeconds,
                    "Did not observe next turn start after fighter EndTurn.");

                Assert.AreEqual(3, order.Count, "Unexpected number of tracked lifecycle events.");
                Assert.AreEqual("conditions", order[0], "ConditionsTicked should be published before TurnEnded.");
                Assert.AreEqual("turn_ended", order[1], "TurnEnded should be published after ConditionsTicked.");
                Assert.AreEqual("next_turn_started", order[2], "Next TurnStarted should be published after TurnEnded.");
            }
            finally
            {
                eventBus.OnConditionsTickedTyped -= ConditionsHandler;
                eventBus.OnTurnEndedTyped -= TurnEndedHandler;
                eventBus.OnTurnStartedTyped -= TurnStartedHandler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P18_PM_407_InitiativePayloadIntegrity_TypedBus()
        {
            var expectedAlive = new List<EntityHandle>(8);
            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null || !data.IsAlive) continue;
                expectedAlive.Add(data.Handle);
            }

            List<InitiativeEntry> receivedOrder = null;
            bool received = false;

            void InitiativeHandler(in InitiativeRolledEvent e)
            {
                received = true;
                receivedOrder = new List<InitiativeEntry>(e.order);
            }

            eventBus.OnInitiativeRolledTyped += InitiativeHandler;

            try
            {
                turnManager.StartCombat();

                yield return WaitUntilOrTimeout(
                    () => received,
                    DefaultTimeoutSeconds,
                    "Did not receive typed InitiativeRolled event.");

                Assert.IsNotNull(receivedOrder, "Initiative payload list is null.");
                Assert.Greater(receivedOrder.Count, 0, "Initiative payload list is empty.");
                Assert.AreEqual(expectedAlive.Count, receivedOrder.Count, "Initiative payload count mismatch.");

                var seenHandles = new HashSet<int>();
                int playerCount = 0;
                int enemyCount = 0;

                for (int i = 0; i < receivedOrder.Count; i++)
                {
                    var entry = receivedOrder[i];
                    Assert.IsTrue(entry.Handle.IsValid, "Initiative payload contains invalid handle.");
                    Assert.IsTrue(seenHandles.Add(entry.Handle.Id), $"Duplicate handle in initiative payload: {entry.Handle.Id}.");
                    Assert.That(entry.Roll, Is.InRange(1, 20), "Initiative roll must be in [1, 20].");

                    var data = entityManager.Registry.Get(entry.Handle);
                    Assert.IsNotNull(data, $"Initiative entry handle {entry.Handle.Id} not found in registry.");
                    Assert.IsTrue(data.IsAlive, $"Initiative entry handle {entry.Handle.Id} is not alive.");

                    if (data.Team == Team.Player) playerCount++;
                    if (data.Team == Team.Enemy) enemyCount++;

                    bool expectedIsPlayer = data.Team == Team.Player;
                    Assert.AreEqual(expectedIsPlayer, entry.IsPlayer, "Initiative IsPlayer flag mismatch.");
                }

                Assert.GreaterOrEqual(playerCount, 1, "Initiative payload has no player entries.");
                Assert.GreaterOrEqual(enemyCount, 1, "Initiative payload has no enemy entries.");

                for (int i = 1; i < receivedOrder.Count; i++)
                {
                    Assert.GreaterOrEqual(
                        receivedOrder[i - 1].SortValue,
                        receivedOrder[i].SortValue,
                        "Initiative payload is not sorted descending by SortValue.");
                }
            }
            finally
            {
                eventBus.OnInitiativeRolledTyped -= InitiativeHandler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P18_PM_404_BlockedEnemyTurn_EndsWithoutDeadlock()
        {
            BoostAllCombatantsHP(220);

            var goblin1 = GetEntityByName("Goblin_1");
            Assert.IsNotNull(goblin1, "Goblin_1 not found for blocked-turn regression.");

            int existingCardinals = CountExistingCardinalNeighbors(goblin1.GridPosition);
            int blockedEdges = BlockCardinalEdges(goblin1.GridPosition);

            Assert.Greater(existingCardinals, 0, "Goblin_1 has no cardinal neighbors in current grid setup.");
            Assert.AreEqual(existingCardinals, blockedEdges, "Failed to block all cardinal exits around Goblin_1.");

            MoveAllPlayersAwayFrom(goblin1.GridPosition, minDistFeet: 10);

            turnManager.StartCombat();

            // Advance the battle until Goblin_1 starts an enemy turn.
            yield return WaitUntilOrTimeout(
                () =>
                {
                    if (turnManager.State == TurnState.PlayerTurn)
                        turnManager.EndTurn();

                    return turnManager.State == TurnState.EnemyTurn
                        && turnManager.CurrentEntity == goblin1.Handle;
                },
                DefaultTimeoutSeconds,
                "Goblin_1 did not receive an enemy turn.");

            float enemyTurnStartTime = Time.realtimeSinceStartup;

            // Goblin_1 is boxed in; AI must end turn promptly and never stick in ExecutingAction.
            yield return WaitUntilOrTimeout(
                () =>
                    turnManager.State != TurnState.EnemyTurn
                    || turnManager.CurrentEntity != goblin1.Handle,
                DefaultTimeoutSeconds,
                "Blocked Goblin_1 turn did not end.");

            float enemyTurnDuration = Time.realtimeSinceStartup - enemyTurnStartTime;
            Assert.Less(
                enemyTurnDuration,
                3f,
                $"Blocked enemy turn took too long ({enemyTurnDuration:0.00}s).");

            Assert.AreNotEqual(
                TurnState.ExecutingAction,
                turnManager.State,
                "TurnManager stayed in ExecutingAction after blocked enemy turn.");
        }

        [UnityTest]
        public IEnumerator GT_P18_PM_405_StickyTargetLock_DoesNotSwitchMidTurn()
        {
            BoostAllCombatantsHP(220);

            var fighter = GetEntityByName("Fighter");
            var wizard = GetEntityByName("Wizard");
            var goblin1 = GetEntityByName("Goblin_1");

            MoveEntityToCell(goblin1, fighter.GridPosition + Vector3Int.right);
            MoveEntityToCell(wizard, FindFarthestAvailableCell(goblin1.GridPosition, wizard.Handle, minDistFeet: 20));

            int goblinStrikeCount = 0;
            EntityHandle firstTarget = EntityHandle.None;
            EntityHandle secondTarget = EntityHandle.None;
            bool wizardRelocated = false;
            string relocateFailure = null;

            void StrikeHandler(in StrikeResolvedEvent e)
            {
                if (e.attacker != goblin1.Handle) return;

                goblinStrikeCount++;
                if (goblinStrikeCount == 1)
                {
                    firstTarget = e.target;

                    try
                    {
                        Vector3Int wizardNearCell = FindAvailableAdjacentCell(
                            goblin1.GridPosition,
                            wizard.Handle,
                            fighter.GridPosition);
                        MoveEntityToCell(wizard, wizardNearCell);
                        wizard.CurrentHP = 1;
                        wizardRelocated = true;
                    }
                    catch (Exception ex)
                    {
                        relocateFailure = ex.Message;
                    }
                }
                else if (goblinStrikeCount == 2)
                {
                    secondTarget = e.target;
                }
            }

            eventBus.OnStrikeResolved += StrikeHandler;

            try
            {
                turnManager.StartCombat();

                yield return WaitUntilOrTimeout(
                    () =>
                    {
                        if (turnManager.State == TurnState.PlayerTurn)
                            turnManager.EndTurn();

                        return turnManager.State == TurnState.EnemyTurn
                            && turnManager.CurrentEntity == goblin1.Handle;
                    },
                    DefaultTimeoutSeconds,
                    "Goblin_1 did not receive an enemy turn for sticky-target regression.");

                yield return WaitUntilOrTimeout(
                    () =>
                    {
                        bool goblinTurnOrActionWindowActive =
                            (turnManager.State == TurnState.EnemyTurn && turnManager.CurrentEntity == goblin1.Handle)
                            || (turnManager.State == TurnState.ExecutingAction && turnManager.ExecutingActor == goblin1.Handle);

                        return goblinStrikeCount >= 2 || !goblinTurnOrActionWindowActive;
                    },
                    SimulationTimeoutSeconds,
                    "Did not observe two Goblin_1 strikes in one turn.");

                Assert.IsNull(relocateFailure, $"Failed to reposition Wizard after first strike: {relocateFailure}");
                Assert.IsTrue(wizardRelocated, "Wizard was not repositioned after first Goblin_1 strike.");
                Assert.AreEqual(fighter.Handle, firstTarget, "Setup invalid: first Goblin_1 strike should target Fighter.");
                Assert.GreaterOrEqual(goblinStrikeCount, 2, "Goblin_1 did not perform two strikes in the same turn.");
                Assert.AreEqual(
                    firstTarget,
                    secondTarget,
                    "Sticky target lock regression: Goblin_1 switched targets mid-turn after Wizard became a better candidate.");
                Assert.AreEqual(
                    fighter.Handle,
                    secondTarget,
                    "Goblin_1 should keep attacking Fighter for the rest of the turn once target lock is set.");
            }
            finally
            {
                eventBus.OnStrikeResolved -= StrikeHandler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P19_PM_412_ShieldBlockPromptTimeout_ReleasesExecutingActionLock()
        {
            var promptController = UnityEngine.Object.FindFirstObjectByType<ReactionPromptController>();
            Assert.IsNotNull(promptController, "ReactionPromptController not found in SampleScene.");

            SetPrivateField(promptController, "timeoutSeconds", 1f);

            var fighter = GetEntityByName("Fighter");
            var wizard = GetEntityByName("Wizard");
            var goblin1 = GetEntityByName("Goblin_1");
            var goblin2 = GetEntityByName("Goblin_2");

            BoostAllCombatantsHP(200);

            fighter.Wisdom = 5000; // deterministic first actor (player)
            fighter.ShieldBlockPreference = ReactionPreference.AlwaysAsk;
            Assert.IsTrue(fighter.EquippedShield.IsEquipped, "Fighter must have an equipped shield in SampleScene for Phase 19.6.");

            wizard.Wisdom = -5000;
            goblin1.Wisdom = 4000; // next actor to trigger enemy strike quickly
            goblin2.Wisdom = -4000;
            goblin1.Strength = 5000; // make the first melee strike reliably hit and open the reaction prompt

            MoveEntityToCell(goblin1, fighter.GridPosition + Vector3Int.right);
            MoveEntityToCell(goblin2, FindFarthestAvailableCell(fighter.GridPosition, goblin2.Handle, minDistFeet: 20));
            MoveEntityToCell(wizard, FindFarthestAvailableCell(goblin1.GridPosition, wizard.Handle, minDistFeet: 20));

            turnManager.StartCombat();

            int fighterRaiseShieldCount = 0;
            float deadline = Time.realtimeSinceStartup + SimulationTimeoutSeconds;

            // Drive turns until the enemy opens a Shield Block prompt on the player.
            while (!promptController.IsPromptActive)
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail("Timeout waiting for enemy Shield Block prompt.");

                if (turnManager.State == TurnState.Inactive)
                    Assert.Fail("Combat ended before Shield Block prompt timeout scenario was reached.");

                if (turnManager.State == TurnState.PlayerTurn)
                {
                    if (turnManager.CurrentEntity == fighter.Handle && !fighter.EquippedShield.isRaised)
                    {
                        Assert.IsTrue(playerActionExecutor.TryExecuteRaiseShield(), "Fighter failed to Raise Shield in timeout setup.");
                        fighterRaiseShieldCount++;
                    }

                    turnManager.EndTurn();
                }

                yield return null;
            }

            Assert.Greater(fighterRaiseShieldCount, 0, "Setup invalid: fighter never raised shield before enemy strike.");
            Assert.AreEqual(TurnState.ExecutingAction, turnManager.State, "Enemy reaction prompt should occur during ExecutingAction.");

            // Do not answer the prompt. Wait for timeout auto-decline and ensure action lock is released.
            float timeoutReleaseDeadline = Time.realtimeSinceStartup + promptController.TimeoutSeconds + 4f;
            while (promptController.IsPromptActive || turnManager.State == TurnState.ExecutingAction)
            {
                if (Time.realtimeSinceStartup > timeoutReleaseDeadline)
                {
                    Assert.Fail(
                        "Timeout waiting for Shield Block prompt timeout path to release ExecutingAction lock. " +
                        $"state={turnManager.State}, source={turnManager.ExecutingActionSource}, actor={turnManager.ExecutingActor.Id}");
                }

                yield return null;
            }

            Assert.AreNotEqual(
                TurnState.ExecutingAction,
                turnManager.State,
                "TurnManager must not remain in ExecutingAction after reaction prompt timeout auto-decline.");
        }

        private void ResolveSceneReferences()
        {
            turnManager = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
            entityManager = UnityEngine.Object.FindFirstObjectByType<EntityManager>();
            eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            playerActionExecutor = UnityEngine.Object.FindFirstObjectByType<PlayerActionExecutor>();
            gridManager = UnityEngine.Object.FindFirstObjectByType<GridManager>();

            Assert.IsNotNull(turnManager, "TurnManager not found.");
            Assert.IsNotNull(entityManager, "EntityManager not found.");
            Assert.IsNotNull(eventBus, "CombatEventBus not found.");
            Assert.IsNotNull(playerActionExecutor, "PlayerActionExecutor not found.");
            Assert.IsNotNull(gridManager, "GridManager not found.");
        }

        private IEnumerator SimulateCombatUntil(Func<bool> completionPredicate, float timeoutSeconds, string timeoutReason)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            float executingSince = -1f;
            float lastProgressTime = Time.realtimeSinceStartup;
            EntityHandle lastPlayerActor = EntityHandle.None;
            bool playerActionIssuedThisTurn = false;
            TurnState lastObservedState = turnManager.State;
            int lastObservedRound = turnManager.RoundNumber;
            EntityHandle lastObservedEntity = turnManager.CurrentEntity;
            int lastObservedActions = turnManager.ActionsRemaining;

            while (!completionPredicate())
            {
                float now = Time.realtimeSinceStartup;
                if (now > deadline)
                    Assert.Fail($"Timeout after {timeoutSeconds:0.##}s: {timeoutReason}");

                if (turnManager.State == TurnState.Inactive)
                    Assert.Fail("Combat ended before regression goal was reached.");

                bool progressed =
                    turnManager.State != lastObservedState ||
                    turnManager.RoundNumber != lastObservedRound ||
                    turnManager.CurrentEntity != lastObservedEntity ||
                    turnManager.ActionsRemaining != lastObservedActions;

                if (progressed)
                {
                    lastProgressTime = now;
                    lastObservedState = turnManager.State;
                    lastObservedRound = turnManager.RoundNumber;
                    lastObservedEntity = turnManager.CurrentEntity;
                    lastObservedActions = turnManager.ActionsRemaining;
                }

                if (turnManager.State == TurnState.ExecutingAction)
                {
                    if (executingSince < 0f)
                        executingSince = now;

                    float executingFor = now - executingSince;
                    float sinceProgress = now - lastProgressTime;
                    if (executingFor > MaxExecutingActionSeconds && sinceProgress > MaxExecutingActionSeconds)
                    {
                        Assert.Fail(
                            "TurnManager stayed in ExecutingAction too long (possible deadlock). " +
                            $"lockFor={turnManager.ExecutingActionDurationSeconds:0.00}s, " +
                            $"lockActor={turnManager.ExecutingActor.Id}, " +
                            $"lockSource={turnManager.ExecutingActionSource}, " +
                            $"round={turnManager.RoundNumber}, current={turnManager.CurrentEntity.Id}.");
                    }

                    yield return null;
                    continue;
                }

                executingSince = -1f;
                if (now - lastProgressTime > MaxNoProgressSeconds)
                {
                    Assert.Fail(
                        $"Combat made no observable progress for {MaxNoProgressSeconds:0.##}s. " +
                        $"state={turnManager.State}, round={turnManager.RoundNumber}, current={turnManager.CurrentEntity.Id}.");
                }

                if (turnManager.State == TurnState.PlayerTurn)
                {
                    EntityHandle actor = turnManager.CurrentEntity;
                    if (actor != lastPlayerActor)
                    {
                        lastPlayerActor = actor;
                        playerActionIssuedThisTurn = false;
                    }

                    if (!playerActionIssuedThisTurn && TryExecuteAnyStride(actor))
                    {
                        playerActionIssuedThisTurn = true;
                        yield return null;
                        continue;
                    }

                    turnManager.EndTurn();
                }

                yield return null;
            }
        }

        private bool TryExecuteAnyStride(EntityHandle actor)
        {
            if (!actor.IsValid) return false;
            if (turnManager.State != TurnState.PlayerTurn) return false;

            var data = entityManager.Registry.Get(actor);
            if (data == null) return false;

            int availableActions = Mathf.Clamp(turnManager.ActionsRemaining, 0, 3);
            if (availableActions <= 0) return false;

            var profile = new MovementProfile
            {
                moveType = MovementType.Walk,
                speedFeet = data.Speed,
                creatureSizeCells = data.SizeCells,
                ignoresDifficultTerrain = false
            };

            var zone = new Dictionary<Vector3Int, int>(64);
            entityManager.Pathfinding.GetMovementZoneByActions(
                gridManager.Data,
                data.GridPosition,
                profile,
                availableActions,
                actor,
                entityManager.Occupancy,
                zone);

            Vector3Int bestCell = data.GridPosition;
            int bestCost = int.MaxValue;
            int bestDist = int.MaxValue;

            foreach (var kvp in zone)
            {
                if (kvp.Key == data.GridPosition) continue;
                int distFeet = GridDistancePF2e.DistanceFeetXZ(data.GridPosition, kvp.Key);
                if (distFeet <= 0) continue;

                int cost = kvp.Value;
                if (cost < bestCost || (cost == bestCost && distFeet < bestDist))
                {
                    bestCost = cost;
                    bestDist = distFeet;
                    bestCell = kvp.Key;
                }
            }

            if (bestCell == data.GridPosition) return false;
            return playerActionExecutor.TryExecuteStrideToCell(bestCell);
        }

        private EntityData GetEntityByName(string name)
        {
            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null) continue;
                if (string.Equals(data.Name, name, StringComparison.Ordinal))
                    return data;
            }

            Assert.Fail($"Entity '{name}' not found.");
            return null;
        }

        private void MoveEntityToCell(EntityData data, Vector3Int targetCell)
        {
            Assert.IsNotNull(data, "MoveEntityToCell received null EntityData.");

            if (data.GridPosition == targetCell)
                return;

            bool moved = entityManager.Occupancy.Move(data.Handle, targetCell, data.SizeCells);
            Assert.IsTrue(moved, $"Failed moving '{data.Name}' to {targetCell}.");

            data.GridPosition = targetCell;
            var view = entityManager.GetView(data.Handle);
            if (view != null)
                view.transform.position = entityManager.GetEntityWorldPosition(targetCell);
        }

        private Vector3Int FindAvailableAdjacentCell(Vector3Int center, EntityHandle mover, Vector3Int blockedCell)
        {
            // Prefer cardinal neighbors first to make tie-break by HP deterministic at equal melee distance.
            Vector3Int[] directions =
            {
                Vector3Int.right,
                Vector3Int.left,
                Vector3Int.forward,
                Vector3Int.back,
                new Vector3Int(1, 0, 1),
                new Vector3Int(1, 0, -1),
                new Vector3Int(-1, 0, 1),
                new Vector3Int(-1, 0, -1)
            };

            foreach (var direction in directions)
            {
                Vector3Int candidate = center + direction;
                if (candidate == blockedCell) continue;
                if (!gridManager.Data.HasCell(candidate)) continue;
                if (!entityManager.Occupancy.CanOccupy(candidate, mover)) continue;
                return candidate;
            }

            Assert.Fail($"No available adjacent cell around {center} for mover {mover.Id}.");
            return center;
        }

        private int CountExistingCardinalNeighbors(Vector3Int center)
        {
            var data = gridManager.Data;
            int count = 0;

            if (data.HasCell(center + Vector3Int.right)) count++;
            if (data.HasCell(center + Vector3Int.left)) count++;
            if (data.HasCell(center + Vector3Int.forward)) count++;
            if (data.HasCell(center + Vector3Int.back)) count++;

            return count;
        }

        private int BlockCardinalEdges(Vector3Int center)
        {
            var data = gridManager.Data;
            int blocked = 0;

            void BlockIfCellExists(Vector3Int neighbor)
            {
                if (!data.HasCell(neighbor)) return;
                data.SetEdge(new EdgeKey(center, neighbor), EdgeData.CreateWall());
                blocked++;
            }

            BlockIfCellExists(center + Vector3Int.right);
            BlockIfCellExists(center + Vector3Int.left);
            BlockIfCellExists(center + Vector3Int.forward);
            BlockIfCellExists(center + Vector3Int.back);

            return blocked;
        }

        private void MoveAllPlayersAwayFrom(Vector3Int center, int minDistFeet)
        {
            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null || !data.IsAlive) continue;
                if (data.Team != Team.Player) continue;

                int distFeet = GridDistancePF2e.DistanceFeetXZ(center, data.GridPosition);
                if (distFeet >= minDistFeet) continue;

                Vector3Int targetCell = FindFarthestAvailableCell(center, data.Handle, minDistFeet);
                MoveEntityToCell(data, targetCell);
            }
        }

        private Vector3Int FindFarthestAvailableCell(Vector3Int from, EntityHandle mover, int minDistFeet)
        {
            Vector3Int bestCell = default;
            int bestDist = int.MinValue;
            bool found = false;

            foreach (var kvp in gridManager.Data.Cells)
            {
                Vector3Int cell = kvp.Key;
                if (!kvp.Value.IsWalkable) continue;
                if (!entityManager.Occupancy.CanOccupy(cell, mover)) continue;

                int distFeet = GridDistancePF2e.DistanceFeetXZ(from, cell);
                if (distFeet < minDistFeet) continue;
                if (distFeet <= bestDist) continue;

                bestDist = distFeet;
                bestCell = cell;
                found = true;
            }

            Assert.IsTrue(found, $"Could not find a walkable free cell at distance >= {minDistFeet}.");
            return bestCell;
        }

        private void BoostAllCombatantsHP(int hpValue)
        {
            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null || !data.IsAlive) continue;
                if (data.Team != Team.Player && data.Team != Team.Enemy) continue;

                data.MaxHP = hpValue;
                data.CurrentHP = hpValue;
            }
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> predicate, float timeoutSeconds, string timeoutReason)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Timeout after {timeoutSeconds:0.##}s: {timeoutReason}");
                yield return null;
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            Assert.IsNotNull(target, "SetPrivateField target is null.");
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
