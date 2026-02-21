using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class CombatRoundRegressionPlayModeTests
    {
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
            fighter.AddCondition(ConditionType.Frightened, value: 2);

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
    }
}
