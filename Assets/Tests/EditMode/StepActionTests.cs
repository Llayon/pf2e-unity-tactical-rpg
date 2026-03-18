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
    public class StepActionTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryPreviewStep_AdjacentCardinalCell_IsValid()
        {
            using var ctx = new StepActionContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1));

            bool previewed = ctx.Action.TryPreviewStep(actor, new Vector3Int(2, 0, 1), out var preview);

            Assert.IsTrue(previewed);
            Assert.IsTrue(preview.isValid);
            Assert.AreEqual(StepFailureReason.None, preview.failureReason);
            Assert.AreEqual(GameConstants.CardinalCostFeet, preview.stepCostFeet);
        }

        [Test]
        public void TryPreviewStep_DifficultTerrainDestination_IsInvalid()
        {
            using var ctx = new StepActionContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1));
            ctx.GridData.SetCell(new Vector3Int(2, 0, 1), CellData.CreateWalkable(CellTerrain.Difficult));

            bool previewed = ctx.Action.TryPreviewStep(actor, new Vector3Int(2, 0, 1), out var preview);

            Assert.IsFalse(previewed);
            Assert.IsFalse(preview.isValid);
            Assert.AreEqual(StepFailureReason.DifficultTerrain, preview.failureReason);
        }

        [Test]
        public void TryPreviewStep_TwoCellsAway_IsInvalid()
        {
            using var ctx = new StepActionContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1));

            bool previewed = ctx.Action.TryPreviewStep(actor, new Vector3Int(3, 0, 1), out var preview);

            Assert.IsFalse(previewed);
            Assert.IsFalse(preview.isValid);
            Assert.AreEqual(StepFailureReason.NotAdjacent, preview.failureReason);
        }

        [Test]
        public void TryPreviewStep_OccupiedDestination_IsInvalid()
        {
            using var ctx = new StepActionContext();
            _ = ctx.RegisterActor(new Vector3Int(1, 0, 1));
            _ = ctx.RegisterActor(new Vector3Int(2, 0, 1), team: Team.Enemy);

            bool previewed = ctx.Action.TryPreviewStep(ctx.PrimaryActor, new Vector3Int(2, 0, 1), out var preview);

            Assert.IsFalse(previewed);
            Assert.IsFalse(preview.isValid);
            Assert.AreEqual(StepFailureReason.Occupied, preview.failureReason);
        }

        [Test]
        public void TryPreviewStep_SpeedZero_IsInvalid()
        {
            using var ctx = new StepActionContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), speed: 0);

            bool previewed = ctx.Action.TryPreviewStep(actor, new Vector3Int(2, 0, 1), out var preview);

            Assert.IsFalse(previewed);
            Assert.IsFalse(preview.isValid);
            Assert.AreEqual(StepFailureReason.SpeedZero, preview.failureReason);
        }

        [Test]
        public void TryExecuteStep_ValidStep_UpdatesGridPositionAndPublishesStepMovement()
        {
            using var ctx = new StepActionContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1));

            EntityMovedEvent? moved = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            try
            {
                bool executed = ctx.Action.TryExecuteStep(actor, new Vector3Int(2, 0, 1));

                Assert.IsTrue(executed);
                Assert.AreEqual(new Vector3Int(2, 0, 1), ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Step, moved.Value.movementTriggerKind);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }
        }

        [Test]
        public void TryExecuteStep_HazardousDestination_AppliesEntryDamage()
        {
            using var ctx = new StepActionContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1));
            ctx.GridData.SetCell(new Vector3Int(2, 0, 1), CellData.CreateWalkable(CellTerrain.Hazardous));

            DamageAppliedEvent? damage = null;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            try
            {
                bool executed = ctx.Action.TryExecuteStep(actor, new Vector3Int(2, 0, 1));

                Assert.IsTrue(executed);
                Assert.AreEqual(18, ctx.Registry.Get(actor).CurrentHP);
                Assert.IsTrue(damage.HasValue);
                Assert.AreEqual(actor, damage.Value.target);
                Assert.AreEqual(HazardousTerrainRules.HazardousEntryDamage, damage.Value.amount);
                Assert.AreEqual(HazardousTerrainRules.HazardousTerrainActionName, damage.Value.sourceActionName);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damage = e;
            }
        }

        [Test]
        public void TryMoveEntityImmediate_HazardousDestination_AppliesEntryDamage()
        {
            using var ctx = new StepActionContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1));
            ctx.GridData.SetCell(new Vector3Int(2, 0, 1), CellData.CreateWalkable(CellTerrain.Hazardous));

            DamageAppliedEvent? damage = null;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            try
            {
                bool moved = ctx.EntityManager.TryMoveEntityImmediate(actor, new Vector3Int(2, 0, 1));

                Assert.IsTrue(moved);
                Assert.AreEqual(18, ctx.Registry.Get(actor).CurrentHP);
                Assert.IsTrue(damage.HasValue);
                Assert.AreEqual(actor, damage.Value.target);
                Assert.AreEqual(HazardousTerrainRules.HazardousEntryDamage, damage.Value.amount);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damage = e;
            }
        }

        private sealed class StepActionContext : System.IDisposable
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
            public readonly StepAction Action;

            public EntityHandle PrimaryActor { get; private set; } = EntityHandle.None;

            public StepActionContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("StepActionTests_Root");

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

                var actionGo = new GameObject("StepAction");
                actionGo.transform.SetParent(root.transform);
                Action = actionGo.AddComponent<StepAction>();
                SetPrivateField(Action, "entityManager", EntityManager);
                SetPrivateField(Action, "eventBus", EventBus);
            }

            public EntityHandle RegisterActor(Vector3Int position, Team team = Team.Player, int speed = 25)
            {
                var handle = Registry.Register(new EntityData
                {
                    Name = $"Actor_{++entityCounter}",
                    Team = team,
                    Level = 1,
                    MaxHP = 20,
                    CurrentHP = 20,
                    Speed = speed,
                    Strength = 16,
                    Dexterity = 14,
                    Constitution = 12,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    GridPosition = position
                });

                Assert.IsTrue(Occupancy.Place(handle, position));
                if (!PrimaryActor.IsValid)
                    PrimaryActor = handle;
                return handle;
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
