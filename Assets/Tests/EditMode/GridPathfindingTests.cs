using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PF2e.Grid;

namespace PF2e.Tests
{
    [TestFixture]
    public class GridPathfindingTests
    {
        private GridData grid;
        private GridPathfinding pathfinding;
        private List<Vector3Int> pathBuffer;
        private Dictionary<Vector3Int, int> zoneBuffer;
        private MovementProfile defaultProfile;

        private static GridData CreateGrid(float cellWorldSize = 1.5f, float heightStep = 1.5f, int chunkSize = 16)
        {
            return new GridData(cellWorldSize, heightStep, chunkSize);
        }

        private static void FillRect(GridData g, int xMin, int xMax, int zMin, int zMax,
            int elevation = 0, CellTerrain terrain = CellTerrain.Normal)
        {
            for (int x = xMin; x <= xMax; x++)
                for (int z = zMin; z <= zMax; z++)
                    g.SetCell(new Vector3Int(x, elevation, z), CellData.CreateWalkable(terrain));
        }

        [SetUp]
        public void SetUp()
        {
            grid = CreateGrid();
            pathfinding = new GridPathfinding();
            pathBuffer = new List<Vector3Int>();
            zoneBuffer = new Dictionary<Vector3Int, int>();
            defaultProfile = MovementProfile.Default;
        }

        // =============================================
        // GT-021: Straight cardinal path
        // =============================================
        [Test]
        public void GT021_StraightCardinalPath()
        {
            FillRect(grid, 0, 9, 0, 9);

            bool found = pathfinding.FindPath(grid,
                new Vector3Int(0, 0, 0), new Vector3Int(5, 0, 0),
                defaultProfile, pathBuffer, out int totalCost);

            Assert.IsTrue(found);
            Assert.AreEqual(6, pathBuffer.Count, "Path includes start: 6 nodes for 5 steps");
            Assert.AreEqual(25, totalCost, "5 cardinal steps × 5ft = 25ft");
            Assert.AreEqual(new Vector3Int(0, 0, 0), pathBuffer[0]);
            Assert.AreEqual(new Vector3Int(5, 0, 0), pathBuffer[pathBuffer.Count - 1]);
        }

        // =============================================
        // GT-022: Diagonal path with 5/10 alternation
        // =============================================
        [Test]
        public void GT022_DiagonalPathAlternation()
        {
            FillRect(grid, 0, 9, 0, 9);

            bool found = pathfinding.FindPath(grid,
                new Vector3Int(0, 0, 0), new Vector3Int(4, 0, 4),
                defaultProfile, pathBuffer, out int totalCost);

            Assert.IsTrue(found);
            Assert.AreEqual(5, pathBuffer.Count, "4 diagonal steps + start = 5");
            Assert.AreEqual(30, totalCost, "4 diags: 5+10+5+10 = 30ft");

            // All steps should be diagonal
            for (int i = 1; i < pathBuffer.Count; i++)
            {
                var diff = pathBuffer[i] - pathBuffer[i - 1];
                Assert.AreEqual(1, Mathf.Abs(diff.x), $"Step {i} dx should be ±1");
                Assert.AreEqual(1, Mathf.Abs(diff.z), $"Step {i} dz should be ±1");
            }
        }

        // =============================================
        // GT-023: Path around obstacle
        // =============================================
        [Test]
        public void GT023_PathAroundObstacle()
        {
            FillRect(grid, 0, 9, 0, 9);
            grid.SetCell(new Vector3Int(3, 0, 0), CellData.CreateImpassable());

            bool found = pathfinding.FindPath(grid,
                new Vector3Int(0, 0, 0), new Vector3Int(5, 0, 0),
                defaultProfile, pathBuffer, out int totalCost);

            Assert.IsTrue(found);
            Assert.IsFalse(pathBuffer.Contains(new Vector3Int(3, 0, 0)),
                "Path must not contain impassable cell");
            Assert.Greater(totalCost, 25, "Detour must cost more than straight 25ft");
        }

        // =============================================
        // GT-024: Difficult terrain ×2
        // =============================================
        [Test]
        public void GT024_DifficultTerrain()
        {
            FillRect(grid, 0, 9, 0, 9);
            grid.SetCell(new Vector3Int(3, 0, 0), CellData.CreateWalkable(CellTerrain.Difficult));

            bool found = pathfinding.FindPath(grid,
                new Vector3Int(2, 0, 0), new Vector3Int(4, 0, 0),
                defaultProfile, pathBuffer, out int totalCost);

            Assert.IsTrue(found);
            // Path: (2,0,0) → (3,0,0) → (4,0,0)
            Assert.AreEqual(3, pathBuffer.Count);
            // Cost entering (3,0,0): 5ft × 2 (difficult) = 10ft
            // Cost entering (4,0,0): 5ft × 1 (normal) = 5ft
            // Total: 15ft
            Assert.AreEqual(15, totalCost);
        }

        // =============================================
        // GT-025: No path exists
        // =============================================
        [Test]
        public void GT025_NoPath()
        {
            FillRect(grid, 0, 9, 0, 9);
            // Block entire row z=5
            for (int x = 0; x < 10; x++)
                grid.SetCell(new Vector3Int(x, 0, 5), CellData.CreateImpassable());

            bool found = pathfinding.FindPath(grid,
                new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 9),
                defaultProfile, pathBuffer, out int totalCost);

            Assert.IsFalse(found);
        }

        // =============================================
        // GT-026: Flood-fill 30ft
        // =============================================
        [Test]
        public void GT026_FloodFill30ft()
        {
            FillRect(grid, 0, 9, 0, 9);

            pathfinding.GetMovementZone(grid,
                new Vector3Int(5, 0, 5), defaultProfile, 30, zoneBuffer);

            // Origin cost = 0
            Assert.IsTrue(zoneBuffer.ContainsKey(new Vector3Int(5, 0, 5)));
            Assert.AreEqual(0, zoneBuffer[new Vector3Int(5, 0, 5)]);

            // (5,0,0): 5 cardinal south = 25ft
            Assert.IsTrue(zoneBuffer.ContainsKey(new Vector3Int(5, 0, 0)));
            Assert.AreEqual(25, zoneBuffer[new Vector3Int(5, 0, 0)]);

            // (0,0,5): 5 cardinal west = 25ft
            Assert.IsTrue(zoneBuffer.ContainsKey(new Vector3Int(0, 0, 5)));
            Assert.AreEqual(25, zoneBuffer[new Vector3Int(0, 0, 5)]);

            // (9,0,9): 4 diag = 5+10+5+10 = 30ft
            Assert.IsTrue(zoneBuffer.ContainsKey(new Vector3Int(9, 0, 9)));
            Assert.AreEqual(30, zoneBuffer[new Vector3Int(9, 0, 9)]);

            // (2,0,2): 3 diag = 5+10+5 = 20ft (min parity)
            Assert.IsTrue(zoneBuffer.ContainsKey(new Vector3Int(2, 0, 2)));
            Assert.AreEqual(20, zoneBuffer[new Vector3Int(2, 0, 2)]);

            // (0,0,0): 5 diag = 5+10+5+10+5 = 35ft > 30ft — NOT reachable
            Assert.IsFalse(zoneBuffer.ContainsKey(new Vector3Int(0, 0, 0)),
                "(0,0,0) costs 35ft, exceeds 30ft budget");
        }

        // =============================================
        // GT-027: Flood-fill diagonal parity
        // =============================================
        [Test]
        public void GT027_FloodFillDiagonalParity()
        {
            // 10 cells in a row + two cells to enable diagonal
            for (int x = 0; x < 10; x++)
                grid.SetCell(new Vector3Int(x, 0, 0), CellData.CreateWalkable());
            grid.SetCell(new Vector3Int(0, 0, 1), CellData.CreateWalkable()); // needed for strict diagonal
            grid.SetCell(new Vector3Int(1, 0, 1), CellData.CreateWalkable());

            pathfinding.GetMovementZone(grid,
                new Vector3Int(0, 0, 0), defaultProfile, 15, zoneBuffer);

            Assert.AreEqual(5, zoneBuffer[new Vector3Int(1, 0, 0)], "1 cardinal = 5ft");
            Assert.AreEqual(10, zoneBuffer[new Vector3Int(2, 0, 0)], "2 cardinal = 10ft");
            Assert.AreEqual(15, zoneBuffer[new Vector3Int(3, 0, 0)], "3 cardinal = 15ft");
            Assert.AreEqual(5, zoneBuffer[new Vector3Int(1, 0, 1)], "1 diagonal (first) = 5ft");

            // (4,0,0): 4 cardinal = 20ft > 15ft — NOT reachable
            Assert.IsFalse(zoneBuffer.ContainsKey(new Vector3Int(4, 0, 0)),
                "(4,0,0) costs 20ft > 15ft budget");
        }

        // =============================================
        // GT-028: A* through vertical link
        // =============================================
        [Test]
        public void GT028_PathThroughVerticalLink()
        {
            // Elevation 0: cells (0-4,0,0)
            for (int x = 0; x <= 4; x++)
                grid.SetCell(new Vector3Int(x, 0, 0), CellData.CreateWalkable());
            // Elevation 1: cells (0-4,1,0)
            for (int x = 0; x <= 4; x++)
                grid.SetCell(new Vector3Int(x, 1, 0), CellData.CreateWalkable());

            // Stairs at (2,0,0) → (2,1,0), cost=5ft
            grid.AddVerticalLink(VerticalLink.CreateStairs(
                new Vector3Int(2, 0, 0), new Vector3Int(2, 1, 0), costFeet: 5));

            bool found = pathfinding.FindPath(grid,
                new Vector3Int(0, 0, 0), new Vector3Int(4, 1, 0),
                defaultProfile, pathBuffer, out int totalCost);

            Assert.IsTrue(found);
            Assert.IsTrue(pathBuffer.Contains(new Vector3Int(2, 0, 0)), "Path goes through stairs lower");
            Assert.IsTrue(pathBuffer.Contains(new Vector3Int(2, 1, 0)), "Path goes through stairs upper");
            // 2 cardinal to stairs + 5ft stairs + 2 cardinal from stairs = 10+5+10 = 25ft
            Assert.AreEqual(25, totalCost);
        }

        // =============================================
        // GT-034: Diagonal parity in A*
        // =============================================
        [Test]
        public void GT034_DiagonalParityInAStar()
        {
            FillRect(grid, 0, 9, 0, 9);

            // 2 diagonal steps: costs 5+10 = 15ft
            bool found1 = pathfinding.FindPath(grid,
                new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 2),
                defaultProfile, pathBuffer, out int cost1);
            Assert.IsTrue(found1);
            Assert.AreEqual(15, cost1, "2 diags: 5+10 = 15ft");

            // 3 diagonal steps: costs 5+10+5 = 20ft
            bool found2 = pathfinding.FindPath(grid,
                new Vector3Int(0, 0, 0), new Vector3Int(3, 0, 3),
                defaultProfile, pathBuffer, out int cost2);
            Assert.IsTrue(found2);
            Assert.AreEqual(20, cost2, "3 diags: 5+10+5 = 20ft");
        }

        // =============================================
        // GT-037: Vertical link + difficult terrain
        // =============================================
        [Test]
        public void GT037_VerticalLinkDifficultTerrain()
        {
            grid.SetCell(new Vector3Int(2, 0, 0), CellData.CreateWalkable(CellTerrain.Normal));
            grid.SetCell(new Vector3Int(2, 1, 0), CellData.CreateWalkable(CellTerrain.Difficult));

            grid.AddVerticalLink(VerticalLink.CreateStairs(
                new Vector3Int(2, 0, 0), new Vector3Int(2, 1, 0), costFeet: 5));

            bool found = pathfinding.FindPath(grid,
                new Vector3Int(2, 0, 0), new Vector3Int(2, 1, 0),
                defaultProfile, pathBuffer, out int totalCost);

            Assert.IsTrue(found);
            // Stairs cost 5ft × difficult ×2 = 10ft
            Assert.AreEqual(10, totalCost, "Vertical 5ft × difficult ×2 = 10ft");
        }
    }
}
