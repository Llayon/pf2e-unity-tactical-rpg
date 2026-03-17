using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class PlayerActionExecutorStepTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryExecuteStepToCell_ValidDestination_SpendsOneActionAndMoves()
        {
            using var ctx = new ExecutorStepContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1));
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            bool executed = ctx.Executor.TryExecuteStepToCell(new Vector3Int(2, 0, 1));

            Assert.IsTrue(executed);
            Assert.AreEqual(new Vector3Int(2, 0, 1), ctx.Registry.Get(actor).GridPosition);
            Assert.AreEqual(2, ctx.Registry.Get(actor).ActionsRemaining);
            Assert.AreEqual(TurnState.PlayerTurn, ctx.TurnManager.State);
        }

        [Test]
        public void TryExecuteStepToCell_InvalidDestination_SpendsNoActions()
        {
            using var ctx = new ExecutorStepContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1));
            ctx.GridData.SetCell(new Vector3Int(2, 0, 1), CellData.CreateWalkable(CellTerrain.Difficult));
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            bool executed = ctx.Executor.TryExecuteStepToCell(new Vector3Int(2, 0, 1));

            Assert.IsFalse(executed);
            Assert.AreEqual(new Vector3Int(1, 0, 1), ctx.Registry.Get(actor).GridPosition);
            Assert.AreEqual(3, ctx.Registry.Get(actor).ActionsRemaining);
            Assert.AreEqual(TurnState.PlayerTurn, ctx.TurnManager.State);
        }

        private sealed class ExecutorStepContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;
            private int entityCounter;

            public readonly CombatEventBus EventBus;
            public readonly GridManager GridManager;
            public readonly GridData GridData;
            public readonly EntityManager EntityManager;
            public readonly EntityRegistry Registry;
            public readonly OccupancyMap Occupancy;
            public readonly TurnManager TurnManager;
            public readonly PlayerActionExecutor Executor;
            public readonly StepAction StepAction;

            public ExecutorStepContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("PlayerActionExecutorStepTests_Root");

                var eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                var gridManagerGo = new GameObject("GridManager");
                gridManagerGo.transform.SetParent(root.transform);
                GridManager = gridManagerGo.AddComponent<GridManager>();
                GridData = new GridData(cellWorldSize: 1f, heightStepWorld: 1f);
                SeedWalkableGrid(GridData, sizeX: 4, sizeZ: 4);
                SetAutoPropertyBackingField(GridManager, "Data", GridData);

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                Occupancy = new OccupancyMap(Registry);
                SetPrivateField(EntityManager, "gridManager", GridManager);
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);
                SetAutoPropertyBackingField(EntityManager, "Occupancy", Occupancy);
                SetAutoPropertyBackingField(EntityManager, "Pathfinding", new GridPathfinding());

                var stepGo = new GameObject("StepAction");
                stepGo.transform.SetParent(root.transform);
                StepAction = stepGo.AddComponent<StepAction>();
                SetPrivateField(StepAction, "entityManager", EntityManager);
                SetPrivateField(StepAction, "eventBus", EventBus);

                var turnManagerGo = new GameObject("TurnManager");
                turnManagerGo.transform.SetParent(root.transform);
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);
                SetPrivateField(TurnManager, "initiativeOrder", new List<InitiativeEntry>());
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
                SetPrivateField(TurnManager, "roundNumber", 1);

                var executorGo = new GameObject("Executor");
                executorGo.transform.SetParent(root.transform);
                Executor = executorGo.AddComponent<PlayerActionExecutor>();
                SetPrivateField(Executor, "turnManager", TurnManager);
                SetPrivateField(Executor, "entityManager", EntityManager);
                SetPrivateField(Executor, "eventBus", EventBus);
                SetPrivateField(Executor, "stepAction", StepAction);
            }

            public EntityHandle RegisterActor(Vector3Int position)
            {
                var handle = Registry.Register(new EntityData
                {
                    Name = $"Actor_{++entityCounter}",
                    Team = Team.Player,
                    Level = 1,
                    MaxHP = 20,
                    CurrentHP = 20,
                    Speed = 25,
                    Strength = 16,
                    Dexterity = 14,
                    Constitution = 12,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    GridPosition = position,
                    ActionsRemaining = 3
                });

                Assert.IsTrue(Occupancy.Place(handle, position));
                return handle;
            }

            public void SetCurrentActor(EntityHandle actor, int actionsRemaining)
            {
                var actorData = Registry.Get(actor);
                Assert.IsNotNull(actorData);
                actorData.ActionsRemaining = actionsRemaining;

                var order = new List<InitiativeEntry>
                {
                    new InitiativeEntry
                    {
                        Handle = actor,
                        Roll = new CheckRoll(20, 0, CheckSource.Perception()),
                        IsPlayer = true
                    }
                };

                SetPrivateField(TurnManager, "initiativeOrder", order);
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
            }

            public void Dispose()
            {
                if (root != null)
                    Object.DestroyImmediate(root);

                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private static void SeedWalkableGrid(GridData gridData, int sizeX, int sizeZ)
        {
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                    gridData.SetCell(new Vector3Int(x, 0, z), CellData.CreateWalkable());
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            string fieldName = $"<{propertyName}>k__BackingField";
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing auto-property backing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
