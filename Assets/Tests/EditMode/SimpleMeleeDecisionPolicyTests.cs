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
    public class SimpleMeleeDecisionPolicyTests
    {
        [Test]
        public void SelectTarget_TieOnDistance_PicksLowerHpThenLowerHandle()
        {
            using var ctx = new PolicyContext(CreateLineGrid(6));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(0, 0, 0), alive: true, currentHp: 20);
            RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 0), alive: true, currentHp: 12);
            var expected = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(0, 0, 2), alive: true, currentHp: 5);

            var actorData = ctx.Registry.Get(actor);
            var selected = ctx.Policy.SelectTarget(actorData);

            Assert.AreEqual(expected, selected);
        }

        [Test]
        public void SelectTarget_ReturnsNone_WhenNoValidPlayerTarget()
        {
            using var ctx = new PolicyContext(CreateLineGrid(4));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(0, 0, 0), alive: true, currentHp: 20);
            RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 0), alive: true, currentHp: 20);
            RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 0), alive: false, currentHp: 0);

            var actorData = ctx.Registry.Get(actor);
            var selected = ctx.Policy.SelectTarget(actorData);

            Assert.AreEqual(EntityHandle.None, selected);
        }

        [Test]
        public void SelectStrideCell_ReturnsNull_WhenNoActions()
        {
            using var ctx = new PolicyContext(CreateLineGrid(5));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(0, 0, 0), alive: true, speedFeet: 25);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(4, 0, 0), alive: true, speedFeet: 25);

            var cell = ctx.Policy.SelectStrideCell(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 0);

            Assert.IsNull(cell);
        }

        [Test]
        public void SelectStrideCell_PrefersReachableAdjacentCell()
        {
            using var ctx = new PolicyContext(CreateLineGrid(6));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(0, 0, 0), alive: true, speedFeet: 30);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(4, 0, 0), alive: true, speedFeet: 25);

            var cell = ctx.Policy.SelectStrideCell(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 3);

            Assert.AreEqual(new Vector3Int(3, 0, 0), cell);
        }

        private static EntityHandle RegisterEntity(
            EntityRegistry registry,
            OccupancyMap occupancy,
            Team team,
            Vector3Int pos,
            bool alive = true,
            int speedFeet = 25,
            int currentHp = 20)
        {
            var data = new EntityData
            {
                Name = $"{team}_{pos}",
                Team = team,
                Size = CreatureSize.Medium,
                MaxHP = 20,
                CurrentHP = alive ? Mathf.Max(1, currentHp) : 0,
                Speed = speedFeet,
                GridPosition = pos,
                EquippedWeapon = new WeaponInstance
                {
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                }
            };

            var handle = registry.Register(data);
            occupancy.Place(handle, pos, data.SizeCells);
            return handle;
        }

        private static GridData CreateLineGrid(int length)
        {
            var grid = new GridData(1f, 1f, 16);
            for (int x = 0; x < length; x++)
                grid.SetCell(new Vector3Int(x, 0, 0), CellData.CreateWalkable());
            return grid;
        }

        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            string fieldName = $"<{propertyName}>k__BackingField";
            var field = target.GetType().GetField(fieldName, flags);
            Assert.IsNotNull(field, $"Missing backing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private sealed class PolicyContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            public readonly GameObject GridGo;
            public readonly GameObject EntityGo;
            public readonly EntityRegistry Registry;
            public readonly OccupancyMap Occupancy;
            public readonly SimpleMeleeDecisionPolicy Policy;

            public PolicyContext(GridData gridData)
            {
                // Harness objects intentionally skip inspector wiring.
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                GridGo = new GameObject("GridManager_Test");
                var gridManager = GridGo.AddComponent<GridManager>();
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                EntityGo = new GameObject("EntityManager_Test");
                var entityManager = EntityGo.AddComponent<EntityManager>();

                Registry = new EntityRegistry();
                Occupancy = new OccupancyMap(Registry);
                var pathfinding = new GridPathfinding();

                SetAutoPropertyBackingField(entityManager, "Registry", Registry);
                SetAutoPropertyBackingField(entityManager, "Occupancy", Occupancy);
                SetAutoPropertyBackingField(entityManager, "Pathfinding", pathfinding);

                Policy = new SimpleMeleeDecisionPolicy(entityManager, gridManager);
            }

            public void Dispose()
            {
                if (EntityGo != null) Object.DestroyImmediate(EntityGo);
                if (GridGo != null) Object.DestroyImmediate(GridGo);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }
    }
}
