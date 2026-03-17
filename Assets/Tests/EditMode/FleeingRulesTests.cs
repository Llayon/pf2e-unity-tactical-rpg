using System.Collections.Generic;
using NUnit.Framework;
using PF2e.Core;
using PF2e.Grid;
using UnityEngine;

namespace PF2e.Tests
{
    [TestFixture]
    public class FleeingRulesTests
    {
        [Test]
        public void TryBuildFleeZone_FiltersToMaximumDistanceCellsOnly()
        {
            var grid = new GridData(1f, 1f, 16);
            FillLine(grid, 0, 6);

            var registry = new EntityRegistry();
            var actor = registry.Register(new EntityData
            {
                Name = "Actor",
                Team = Team.Enemy,
                CurrentHP = 10,
                MaxHP = 10,
                Speed = 10,
                GridPosition = new Vector3Int(2, 0, 0)
            });
            var source = registry.Register(new EntityData
            {
                Name = "Wizard",
                Team = Team.Player,
                CurrentHP = 10,
                MaxHP = 10,
                Speed = 25,
                GridPosition = new Vector3Int(1, 0, 0)
            });

            var actorData = registry.Get(actor);
            actorData.Conditions.Add(new ActiveCondition(ConditionType.Fleeing, value: 0, remainingRounds: 1));
            actorData.SetFleeingSource(source);

            var occupancy = new OccupancyMap(registry);
            Assert.IsTrue(occupancy.Place(actor, actorData.GridPosition));
            Assert.IsTrue(occupancy.Place(source, registry.Get(source).GridPosition));

            var zone = new Dictionary<Vector3Int, int>();
            bool built = FleeingRules.TryBuildFleeZone(
                grid,
                new GridPathfinding(),
                occupancy,
                registry,
                actorData,
                availableActions: 1,
                zone,
                out var sourcePosition);

            Assert.IsTrue(built);
            Assert.AreEqual(new Vector3Int(1, 0, 0), sourcePosition);
            CollectionAssert.AreEquivalent(new[] { new Vector3Int(4, 0, 0) }, zone.Keys);
            Assert.AreEqual(1, zone[new Vector3Int(4, 0, 0)]);
        }

        [Test]
        public void TryBuildFleeZone_NoFurtherReachableCell_ReturnsFalse()
        {
            var grid = new GridData(1f, 1f, 16);
            FillLine(grid, 1, 2);

            var registry = new EntityRegistry();
            var actor = registry.Register(new EntityData
            {
                Name = "Actor",
                Team = Team.Enemy,
                CurrentHP = 10,
                MaxHP = 10,
                Speed = 10,
                GridPosition = new Vector3Int(2, 0, 0)
            });
            var source = registry.Register(new EntityData
            {
                Name = "Wizard",
                Team = Team.Player,
                CurrentHP = 10,
                MaxHP = 10,
                Speed = 25,
                GridPosition = new Vector3Int(1, 0, 0)
            });

            var actorData = registry.Get(actor);
            actorData.Conditions.Add(new ActiveCondition(ConditionType.Fleeing, value: 0, remainingRounds: 1));
            actorData.SetFleeingSource(source);

            var occupancy = new OccupancyMap(registry);
            Assert.IsTrue(occupancy.Place(actor, actorData.GridPosition));
            Assert.IsTrue(occupancy.Place(source, registry.Get(source).GridPosition));

            var zone = new Dictionary<Vector3Int, int>();
            bool built = FleeingRules.TryBuildFleeZone(
                grid,
                new GridPathfinding(),
                occupancy,
                registry,
                actorData,
                availableActions: 1,
                zone,
                out _);

            Assert.IsFalse(built);
            Assert.AreEqual(0, zone.Count);
        }

        private static void FillLine(GridData grid, int xMin, int xMax)
        {
            for (int x = xMin; x <= xMax; x++)
                grid.SetCell(new Vector3Int(x, 0, 0), CellData.CreateWalkable());
        }
    }
}
