using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PF2e.Grid;

namespace PF2e.Tests
{
    [TestFixture]
    public class GridDataTests
    {
        private GridData grid;
        private List<NeighborInfo> neighborBuffer;

        private static GridData CreateGrid(float cellWorldSize = 1.5f, float heightStep = 1.5f, int chunkSize = 16)
        {
            return new GridData(cellWorldSize, heightStep, chunkSize);
        }

        /// <summary>
        /// Fill a rectangular region at a given elevation with walkable cells.
        /// </summary>
        private static void FillRect(GridData g, int xMin, int xMax, int zMin, int zMax, int elevation = 0,
            CellTerrain terrain = CellTerrain.Normal)
        {
            for (int x = xMin; x <= xMax; x++)
                for (int z = zMin; z <= zMax; z++)
                    g.SetCell(new Vector3Int(x, elevation, z), CellData.CreateWalkable(terrain));
        }

        [SetUp]
        public void SetUp()
        {
            grid = CreateGrid();
            neighborBuffer = new List<NeighborInfo>(16);
        }

        // =============================================
        // GT-001: Grid creation 10x10
        // =============================================
        [Test]
        public void GT001_GridCreation10x10()
        {
            FillRect(grid, 0, 9, 0, 9, elevation: 0);

            Assert.AreEqual(100, grid.CellCount);
            Assert.IsTrue(grid.HasCell(new Vector3Int(0, 0, 0)));
            Assert.IsTrue(grid.HasCell(new Vector3Int(9, 0, 9)));
            Assert.IsFalse(grid.HasCell(new Vector3Int(10, 0, 0)));
            Assert.IsFalse(grid.HasCell(new Vector3Int(0, 1, 0)));
        }

        // =============================================
        // GT-002: WorldToCell conversion
        // =============================================
        [Test]
        public void GT002_WorldToCell()
        {
            // cellWorldSize=1.5, heightStepWorld=1.5
            Assert.AreEqual(new Vector3Int(0, 0, 0), grid.WorldToCell(new Vector3(0.0f, 0.0f, 0.0f)));
            Assert.AreEqual(new Vector3Int(0, 0, 0), grid.WorldToCell(new Vector3(0.7f, 0.0f, 0.7f)));
            Assert.AreEqual(new Vector3Int(1, 0, 0), grid.WorldToCell(new Vector3(1.5f, 0.0f, 0.0f)));
            Assert.AreEqual(new Vector3Int(0, 0, 0), grid.WorldToCell(new Vector3(1.4f, 0.0f, 0.0f)));
            Assert.AreEqual(new Vector3Int(0, 1, 0), grid.WorldToCell(new Vector3(0.0f, 2.0f, 0.0f)));
            // FloorToInt((2.9 + eps) / 1.5) = FloorToInt(1.9337...) = 1
            Assert.AreEqual(new Vector3Int(0, 1, 0), grid.WorldToCell(new Vector3(0.0f, 2.9f, 0.0f)));
            Assert.AreEqual(new Vector3Int(-1, 0, -1), grid.WorldToCell(new Vector3(-0.1f, 0.0f, -0.1f)));
        }

        // =============================================
        // GT-003: CellToWorld conversion
        // =============================================
        [Test]
        public void GT003_CellToWorld()
        {
            // cellWorldSize=1.5, heightStepWorld=1.5
            AssertVector3Near(new Vector3(0.75f, 0.0f, 0.75f), grid.CellToWorld(new Vector3Int(0, 0, 0)));
            AssertVector3Near(new Vector3(2.25f, 0.0f, 0.75f), grid.CellToWorld(new Vector3Int(1, 0, 0)));
            AssertVector3Near(new Vector3(0.75f, 3.0f, 0.75f), grid.CellToWorld(new Vector3Int(0, 2, 0)));
            AssertVector3Near(new Vector3(14.25f, 0.0f, 14.25f), grid.CellToWorld(new Vector3Int(9, 0, 9)));
        }

        // =============================================
        // GT-004: WorldToCell → CellToWorld roundtrip
        // =============================================
        [Test]
        public void GT004_RoundTrip()
        {
            for (int x = 0; x < 10; x++)
            {
                for (int z = 0; z < 10; z++)
                {
                    var cell = new Vector3Int(x, 0, z);
                    var world = grid.CellToWorld(cell);
                    var back = grid.WorldToCell(world);
                    Assert.AreEqual(cell, back, $"Roundtrip failed for cell {cell}, world={world}");
                }
            }
        }

        // =============================================
        // GT-005: GetNeighbors cardinal + diagonal (center)
        // =============================================
        [Test]
        public void GT005_GetNeighborsCenter()
        {
            FillRect(grid, 0, 9, 0, 9);

            grid.GetNeighbors(new Vector3Int(5, 0, 5), MovementType.Walk, neighborBuffer);

            Assert.AreEqual(8, neighborBuffer.Count);

            // Cardinal
            AssertContainsNeighbor(new Vector3Int(4, 0, 5), NeighborType.Cardinal);
            AssertContainsNeighbor(new Vector3Int(6, 0, 5), NeighborType.Cardinal);
            AssertContainsNeighbor(new Vector3Int(5, 0, 4), NeighborType.Cardinal);
            AssertContainsNeighbor(new Vector3Int(5, 0, 6), NeighborType.Cardinal);

            // Diagonal
            AssertContainsNeighbor(new Vector3Int(4, 0, 4), NeighborType.Diagonal);
            AssertContainsNeighbor(new Vector3Int(6, 0, 4), NeighborType.Diagonal);
            AssertContainsNeighbor(new Vector3Int(4, 0, 6), NeighborType.Diagonal);
            AssertContainsNeighbor(new Vector3Int(6, 0, 6), NeighborType.Diagonal);
        }

        // =============================================
        // GT-006: GetNeighbors on edge (corner 0,0,0)
        // =============================================
        [Test]
        public void GT006_GetNeighborsCorner()
        {
            FillRect(grid, 0, 9, 0, 9);

            grid.GetNeighbors(new Vector3Int(0, 0, 0), MovementType.Walk, neighborBuffer);

            Assert.AreEqual(3, neighborBuffer.Count);
            AssertContainsNeighbor(new Vector3Int(1, 0, 0), NeighborType.Cardinal);
            AssertContainsNeighbor(new Vector3Int(0, 0, 1), NeighborType.Cardinal);
            AssertContainsNeighbor(new Vector3Int(1, 0, 1), NeighborType.Diagonal);
        }

        // =============================================
        // GT-007: Diagonal blocked — one intermediate impassable
        // =============================================
        [Test]
        public void GT007_DiagonalBlockedOneImpassable()
        {
            FillRect(grid, 0, 9, 0, 9);
            // Make (6,0,5) impassable
            grid.SetCell(new Vector3Int(6, 0, 5), CellData.CreateImpassable());

            grid.GetNeighbors(new Vector3Int(5, 0, 5), MovementType.Walk, neighborBuffer);

            // (6,0,5) is impassable — not a neighbor
            AssertNotContainsNeighbor(new Vector3Int(6, 0, 5));
            // (6,0,4) blocked — intermediate (6,0,5) impassable
            AssertNotContainsNeighbor(new Vector3Int(6, 0, 4));
            // (6,0,6) blocked — intermediate (6,0,5) impassable
            AssertNotContainsNeighbor(new Vector3Int(6, 0, 6));

            // Present: (4,0,5), (5,0,4), (5,0,6), (4,0,4), (4,0,6)
            Assert.AreEqual(5, neighborBuffer.Count);
            AssertContainsNeighbor(new Vector3Int(4, 0, 5), NeighborType.Cardinal);
            AssertContainsNeighbor(new Vector3Int(5, 0, 4), NeighborType.Cardinal);
            AssertContainsNeighbor(new Vector3Int(5, 0, 6), NeighborType.Cardinal);
            AssertContainsNeighbor(new Vector3Int(4, 0, 4), NeighborType.Diagonal);
            AssertContainsNeighbor(new Vector3Int(4, 0, 6), NeighborType.Diagonal);
        }

        // =============================================
        // GT-007b: Diagonal — both intermediates checked (strict rule)
        // =============================================
        [Test]
        public void GT007b_DiagonalBothIntermediatesChecked()
        {
            FillRect(grid, 0, 9, 0, 9);
            // (6,0,5) = Impassable, (5,0,6) = Walkable (already from FillRect)
            grid.SetCell(new Vector3Int(6, 0, 5), CellData.CreateImpassable());

            grid.GetNeighbors(new Vector3Int(5, 0, 5), MovementType.Walk, neighborBuffer);

            // (6,0,6) must be absent: strict rule — intermediate (6,0,5) is impassable,
            // even though the other intermediate (5,0,6) is passable
            AssertNotContainsNeighbor(new Vector3Int(6, 0, 6));
        }

        // =============================================
        // GT-008: EdgeKey normalization
        // =============================================
        [Test]
        public void GT008_EdgeKeyNormalization()
        {
            var a = new Vector3Int(0, 0, 0);
            var b = new Vector3Int(1, 0, 0);

            var key1 = new EdgeKey(a, b);
            var key2 = new EdgeKey(b, a);

            Assert.AreEqual(new Vector3Int(0, 0, 0), key1.CellA);
            Assert.AreEqual(new Vector3Int(1, 0, 0), key1.CellB);
            Assert.AreEqual(new Vector3Int(0, 0, 0), key2.CellA);
            Assert.AreEqual(new Vector3Int(1, 0, 0), key2.CellB);

            Assert.IsTrue(key1.Equals(key2));
            Assert.AreEqual(key1.GetHashCode(), key2.GetHashCode());
        }

        // =============================================
        // GT-009: Edge blocks movement + diagonals
        // =============================================
        [Test]
        public void GT009_EdgeBlocksMovementAndDiagonals()
        {
            FillRect(grid, 0, 9, 0, 9);

            // Wall edge between (5,0,5) and (6,0,5)
            grid.SetEdge(
                new EdgeKey(new Vector3Int(5, 0, 5), new Vector3Int(6, 0, 5)),
                EdgeData.CreateWall());

            grid.GetNeighbors(new Vector3Int(5, 0, 5), MovementType.Walk, neighborBuffer);

            // (6,0,5) absent — edge blocks movement
            AssertNotContainsNeighbor(new Vector3Int(6, 0, 5));
            // (6,0,4) absent — diagonal: step (5,0,5)→(6,0,5) blocked by edge
            AssertNotContainsNeighbor(new Vector3Int(6, 0, 4));
            // (6,0,6) absent — diagonal: step (5,0,5)→(6,0,5) blocked by edge
            AssertNotContainsNeighbor(new Vector3Int(6, 0, 6));

            // Present: (4,0,5), (5,0,4), (5,0,6), (4,0,4), (4,0,6)
            Assert.AreEqual(5, neighborBuffer.Count);
            AssertContainsNeighbor(new Vector3Int(4, 0, 5), NeighborType.Cardinal);
            AssertContainsNeighbor(new Vector3Int(5, 0, 4), NeighborType.Cardinal);
            AssertContainsNeighbor(new Vector3Int(5, 0, 6), NeighborType.Cardinal);
            AssertContainsNeighbor(new Vector3Int(4, 0, 4), NeighborType.Diagonal);
            AssertContainsNeighbor(new Vector3Int(4, 0, 6), NeighborType.Diagonal);
        }

        // =============================================
        // GT-033: GetNeighbors with VerticalLink
        // =============================================
        [Test]
        public void GT033_GetNeighborsWithVerticalLink()
        {
            // (5,0,5) walkable, (5,1,5) walkable, stairs between them
            FillRect(grid, 4, 6, 4, 6, elevation: 0);
            grid.SetCell(new Vector3Int(5, 1, 5), CellData.CreateWalkable());

            grid.AddVerticalLink(VerticalLink.CreateStairs(
                new Vector3Int(5, 0, 5),
                new Vector3Int(5, 1, 5),
                costFeet: 5));

            grid.GetNeighbors(new Vector3Int(5, 0, 5), MovementType.Walk, neighborBuffer);

            // Should contain (5,1,5) as vertical + standard horizontal neighbors
            var verticalNeighbor = neighborBuffer.FirstOrDefault(n =>
                n.pos == new Vector3Int(5, 1, 5) && n.type == NeighborType.Vertical);
            Assert.AreEqual(new Vector3Int(5, 1, 5), verticalNeighbor.pos,
                "Vertical neighbor (5,1,5) should be present");
            Assert.AreEqual(NeighborType.Vertical, verticalNeighbor.type);
            Assert.AreEqual(5, verticalNeighbor.verticalCostFeet);

            // Also has standard horizontal neighbors
            int horizontalCount = neighborBuffer.Count(n => n.type != NeighborType.Vertical);
            Assert.Greater(horizontalCount, 0, "Should have horizontal neighbors too");
        }

        // =============================================
        // GT-035: Impassable vs Non-existent
        // =============================================
        [Test]
        public void GT035_ImpassableVsNonExistent()
        {
            FillRect(grid, 0, 9, 0, 9);
            grid.SetCell(new Vector3Int(5, 0, 5), CellData.CreateImpassable());

            // Existing cell
            Assert.IsTrue(grid.HasCell(new Vector3Int(5, 0, 5)));
            Assert.IsTrue(grid.TryGetCell(new Vector3Int(5, 0, 5), out var cell));
            Assert.AreEqual(CellTerrain.Impassable, cell.terrain);

            // Non-existent cell
            Assert.IsFalse(grid.HasCell(new Vector3Int(15, 0, 15)));

            // GetNeighbors should not return non-existent cells
            grid.GetNeighbors(new Vector3Int(9, 0, 9), MovementType.Walk, neighborBuffer);
            foreach (var n in neighborBuffer)
            {
                Assert.IsTrue(grid.HasCell(n.pos),
                    $"Neighbor {n.pos} should exist in grid");
            }

            // GetNeighbors should not return impassable cells
            grid.GetNeighbors(new Vector3Int(4, 0, 5), MovementType.Walk, neighborBuffer);
            foreach (var n in neighborBuffer)
            {
                Assert.IsTrue(grid.TryGetCell(n.pos, out var nCell));
                Assert.AreNotEqual(CellTerrain.Impassable, nCell.terrain,
                    $"Neighbor {n.pos} should not be impassable");
            }

            // No exceptions in either case — the fact we got here means no exceptions
            Assert.Pass();
        }

        // =============================================
        // Helpers
        // =============================================

        private void AssertContainsNeighbor(Vector3Int pos, NeighborType type)
        {
            Assert.IsTrue(
                neighborBuffer.Any(n => n.pos == pos && n.type == type),
                $"Expected neighbor ({pos}, {type}) not found. Actual: [{string.Join(", ", neighborBuffer)}]");
        }

        private void AssertNotContainsNeighbor(Vector3Int pos)
        {
            Assert.IsFalse(
                neighborBuffer.Any(n => n.pos == pos),
                $"Neighbor {pos} should not be present. Actual: [{string.Join(", ", neighborBuffer)}]");
        }

        private static void AssertVector3Near(Vector3 expected, Vector3 actual, float tolerance = 0.001f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, $"X mismatch: expected {expected}, actual {actual}");
            Assert.AreEqual(expected.y, actual.y, tolerance, $"Y mismatch: expected {expected}, actual {actual}");
            Assert.AreEqual(expected.z, actual.z, tolerance, $"Z mismatch: expected {expected}, actual {actual}");
        }
    }
}
