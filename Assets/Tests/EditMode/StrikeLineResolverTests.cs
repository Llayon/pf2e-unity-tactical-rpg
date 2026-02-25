using NUnit.Framework;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;

namespace PF2e.Tests
{
    [TestFixture]
    public class StrikeLineResolverTests
    {
        [Test]
        public void StrikeLineResolver_NoObstacles_HasLoS_NoCover()
        {
            using var ctx = new LineContext();
            ctx.FillWalkableRect(0, 3, 0, 0);

            var result = StrikeLineResolver.ResolveSameElevation(
                ctx.Grid,
                ctx.Occupancy,
                new Vector3Int(0, 0, 0),
                new Vector3Int(3, 0, 0),
                new EntityHandle(1),
                new EntityHandle(2));

            Assert.IsTrue(result.hasLineOfSight);
            Assert.AreEqual(0, result.coverAcBonus);
        }

        [Test]
        public void StrikeLineResolver_AdjacentTarget_AlwaysHasLoS()
        {
            using var ctx = new LineContext();
            ctx.FillWalkableRect(0, 1, 0, 0);

            var result = StrikeLineResolver.ResolveSameElevation(
                ctx.Grid,
                ctx.Occupancy,
                new Vector3Int(0, 0, 0),
                new Vector3Int(1, 0, 0),
                new EntityHandle(1),
                new EntityHandle(2));

            Assert.IsTrue(result.hasLineOfSight);
            Assert.AreEqual(0, result.coverAcBonus);
        }

        [Test]
        public void StrikeLineResolver_WallInLine_BlocksLoS_NoCover()
        {
            using var ctx = new LineContext();
            ctx.FillWalkableRect(0, 3, 0, 0);
            ctx.SetBlocked(1, 0);

            var result = StrikeLineResolver.ResolveSameElevation(
                ctx.Grid,
                ctx.Occupancy,
                new Vector3Int(0, 0, 0),
                new Vector3Int(3, 0, 0),
                new EntityHandle(1),
                new EntityHandle(2));

            Assert.IsFalse(result.hasLineOfSight);
            Assert.AreEqual(0, result.coverAcBonus);
        }

        [Test]
        public void StrikeLineResolver_EntityInLine_GivesCover_NotBlocksLoS()
        {
            using var ctx = new LineContext();
            ctx.FillWalkableRect(0, 3, 0, 0);

            var attacker = new EntityHandle(1);
            var target = new EntityHandle(2);
            var blocker = new EntityHandle(3);
            Assert.IsTrue(ctx.Occupancy.Place(blocker, new Vector3Int(1, 0, 0)));

            var result = StrikeLineResolver.ResolveSameElevation(
                ctx.Grid,
                ctx.Occupancy,
                new Vector3Int(0, 0, 0),
                new Vector3Int(3, 0, 0),
                attacker,
                target);

            Assert.IsTrue(result.hasLineOfSight);
            Assert.AreEqual(2, result.coverAcBonus);
        }

        [Test]
        public void StrikeLineResolver_DiagonalSupercover_PermissiveCorner_AllowsWhenOneSideClear()
        {
            using var ctx = new LineContext();
            ctx.FillWalkableRect(0, 2, 0, 2);
            ctx.SetBlocked(1, 0);
            // (0,1) remains clear; permissive corner rule should allow LoS.

            var result = StrikeLineResolver.ResolveSameElevation(
                ctx.Grid,
                ctx.Occupancy,
                new Vector3Int(0, 0, 0),
                new Vector3Int(2, 0, 2),
                new EntityHandle(1),
                new EntityHandle(2));

            Assert.IsTrue(result.hasLineOfSight);
        }

        [Test]
        public void StrikeLineResolver_DiagonalSupercover_PermissiveCorner_BlocksWhenBothSidesBlocked()
        {
            using var ctx = new LineContext();
            ctx.FillWalkableRect(0, 2, 0, 2);
            ctx.SetBlocked(1, 0);
            ctx.SetBlocked(0, 1);

            var result = StrikeLineResolver.ResolveSameElevation(
                ctx.Grid,
                ctx.Occupancy,
                new Vector3Int(0, 0, 0),
                new Vector3Int(2, 0, 2),
                new EntityHandle(1),
                new EntityHandle(2));

            Assert.IsFalse(result.hasLineOfSight);
        }

        [Test]
        public void StrikeLineResolver_DiagonalCornerEntity_GivesCover_NotBlocksLoS()
        {
            using var ctx = new LineContext();
            ctx.FillWalkableRect(0, 2, 0, 2);

            var attacker = new EntityHandle(1);
            var target = new EntityHandle(2);
            var coverEntity = new EntityHandle(3);
            Assert.IsTrue(ctx.Occupancy.Place(coverEntity, new Vector3Int(1, 0, 0)));

            var result = StrikeLineResolver.ResolveSameElevation(
                ctx.Grid,
                ctx.Occupancy,
                new Vector3Int(0, 0, 0),
                new Vector3Int(2, 0, 2),
                attacker,
                target);

            Assert.IsTrue(result.hasLineOfSight);
            Assert.AreEqual(2, result.coverAcBonus);
        }

        private sealed class LineContext : System.IDisposable
        {
            public GridData Grid { get; }
            public OccupancyMap Occupancy { get; }
            private readonly EntityRegistry registry;

            public LineContext()
            {
                Grid = new GridData(cellWorldSize: 5f, heightStepWorld: 1f);
                registry = new EntityRegistry();
                Occupancy = new OccupancyMap(registry);
            }

            public void FillWalkableRect(int minX, int maxX, int minZ, int maxZ, int y = 0)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        Grid.SetCell(new Vector3Int(x, y, z), CellData.CreateWalkable());
                    }
                }
            }

            public void SetBlocked(int x, int z, int y = 0)
            {
                Grid.SetCell(new Vector3Int(x, y, z), CellData.CreateBlocked());
            }

            public void Dispose()
            {
            }
        }
    }
}
