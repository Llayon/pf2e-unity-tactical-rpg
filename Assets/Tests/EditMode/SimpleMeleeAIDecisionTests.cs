using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class SimpleMeleeAIDecisionTests
    {
        private static EntityHandle RegisterEntity(
            EntityRegistry registry,
            OccupancyMap occupancy,
            Team team,
            Vector3Int pos,
            bool alive = true,
            int speedFeet = 25)
        {
            var data = new EntityData
            {
                Name = $"{team}_{pos}",
                Team = team,
                Size = CreatureSize.Medium,
                MaxHP = 20,
                CurrentHP = alive ? 20 : 0,
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

        [Test]
        public void FindBestTarget_PicksNearestAlivePlayerOnSameElevation()
        {
            var registry = new EntityRegistry();
            var occupancy = new OccupancyMap(registry);

            var actor = RegisterEntity(registry, occupancy, Team.Enemy, new Vector3Int(0, 0, 0), alive: true);
            RegisterEntity(registry, occupancy, Team.Player, new Vector3Int(5, 0, 0), alive: true);
            var nearPlayer = RegisterEntity(registry, occupancy, Team.Player, new Vector3Int(2, 0, 0), alive: true);
            RegisterEntity(registry, occupancy, Team.Player, new Vector3Int(1, 1, 0), alive: true); // different elevation
            RegisterEntity(registry, occupancy, Team.Player, new Vector3Int(1, 0, 1), alive: false); // dead

            var actorData = registry.Get(actor);
            var target = SimpleMeleeAIDecision.FindBestTarget(actorData, registry.GetAll());

            Assert.AreEqual(nearPlayer, target);
        }

        [Test]
        public void FindBestTarget_ReturnsNoneWhenNoValidPlayers()
        {
            var registry = new EntityRegistry();
            var occupancy = new OccupancyMap(registry);

            var actor = RegisterEntity(registry, occupancy, Team.Enemy, new Vector3Int(0, 0, 0), alive: true);
            RegisterEntity(registry, occupancy, Team.Enemy, new Vector3Int(1, 0, 0), alive: true);
            RegisterEntity(registry, occupancy, Team.Player, new Vector3Int(3, 0, 0), alive: false);

            var actorData = registry.Get(actor);
            var target = SimpleMeleeAIDecision.FindBestTarget(actorData, registry.GetAll());

            Assert.AreEqual(EntityHandle.None, target);
        }

        [Test]
        public void IsInMeleeRange_RespectsReachAndElevation()
        {
            var attacker = new EntityData
            {
                Team = Team.Enemy,
                CurrentHP = 10,
                MaxHP = 10,
                GridPosition = new Vector3Int(0, 0, 0),
                EquippedWeapon = new WeaponInstance
                {
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                }
            };

            var targetInRange = new EntityData
            {
                Team = Team.Player,
                CurrentHP = 10,
                MaxHP = 10,
                GridPosition = new Vector3Int(1, 0, 0)
            };

            var targetOutOfRange = new EntityData
            {
                Team = Team.Player,
                CurrentHP = 10,
                MaxHP = 10,
                GridPosition = new Vector3Int(2, 0, 0)
            };

            var targetDifferentElevation = new EntityData
            {
                Team = Team.Player,
                CurrentHP = 10,
                MaxHP = 10,
                GridPosition = new Vector3Int(1, 1, 0)
            };

            Assert.IsTrue(SimpleMeleeAIDecision.IsInMeleeRange(attacker, targetInRange));
            Assert.IsFalse(SimpleMeleeAIDecision.IsInMeleeRange(attacker, targetOutOfRange));
            Assert.IsFalse(SimpleMeleeAIDecision.IsInMeleeRange(attacker, targetDifferentElevation));
        }

        [Test]
        public void FindBestMoveCell_PrefersReachableAdjacentCell()
        {
            var registry = new EntityRegistry();
            var occupancy = new OccupancyMap(registry);
            var pathfinding = new GridPathfinding();
            var grid = CreateLineGrid(6); // x:0..5, z fixed 0
            var pathBuffer = new List<Vector3Int>(32);
            var zoneBuffer = new Dictionary<Vector3Int, int>();

            var actor = RegisterEntity(registry, occupancy, Team.Enemy, new Vector3Int(0, 0, 0), alive: true, speedFeet: 30);
            var target = RegisterEntity(registry, occupancy, Team.Player, new Vector3Int(4, 0, 0), alive: true, speedFeet: 25);

            var actorData = registry.Get(actor);
            var targetData = registry.Get(target);

            var cell = SimpleMeleeAIDecision.FindBestMoveCell(
                grid,
                pathfinding,
                occupancy,
                actorData,
                targetData,
                availableActions: 3,
                pathBuffer,
                zoneBuffer);

            Assert.AreEqual(new Vector3Int(3, 0, 0), cell);
        }

        [Test]
        public void FindBestMoveCell_FallbacksToClosestReachableWhenAdjacentUnreachable()
        {
            var registry = new EntityRegistry();
            var occupancy = new OccupancyMap(registry);
            var pathfinding = new GridPathfinding();
            var grid = CreateLineGrid(7); // x:0..6
            var pathBuffer = new List<Vector3Int>(32);
            var zoneBuffer = new Dictionary<Vector3Int, int>();

            var actor = RegisterEntity(registry, occupancy, Team.Enemy, new Vector3Int(0, 0, 0), alive: true, speedFeet: 5);
            var target = RegisterEntity(registry, occupancy, Team.Player, new Vector3Int(6, 0, 0), alive: true, speedFeet: 25);

            var actorData = registry.Get(actor);
            var targetData = registry.Get(target);

            var cell = SimpleMeleeAIDecision.FindBestMoveCell(
                grid,
                pathfinding,
                occupancy,
                actorData,
                targetData,
                availableActions: 1,
                pathBuffer,
                zoneBuffer);

            Assert.AreEqual(new Vector3Int(1, 0, 0), cell);
        }

        [Test]
        public void FindBestMoveCell_ReturnsNullWhenNoActions()
        {
            var registry = new EntityRegistry();
            var occupancy = new OccupancyMap(registry);
            var pathfinding = new GridPathfinding();
            var grid = CreateLineGrid(5);
            var pathBuffer = new List<Vector3Int>(32);
            var zoneBuffer = new Dictionary<Vector3Int, int>();

            var actor = RegisterEntity(registry, occupancy, Team.Enemy, new Vector3Int(0, 0, 0), alive: true, speedFeet: 25);
            var target = RegisterEntity(registry, occupancy, Team.Player, new Vector3Int(4, 0, 0), alive: true, speedFeet: 25);

            var actorData = registry.Get(actor);
            var targetData = registry.Get(target);

            var cell = SimpleMeleeAIDecision.FindBestMoveCell(
                grid,
                pathfinding,
                occupancy,
                actorData,
                targetData,
                availableActions: 0,
                pathBuffer,
                zoneBuffer);

            Assert.IsNull(cell);
        }
    }
}
