using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using PF2e.Grid;

namespace PF2e.Tests
{
    [TestFixture]
    public class GridMeshBuilderTests
    {
        private GridData grid;
        private Mesh mesh;

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
            mesh = new Mesh();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(mesh);
        }

        // =============================================
        // GT-014: Mesh vertex count
        // =============================================
        [Test]
        public void GT014_MeshVertexCount()
        {
            FillRect(grid, 0, 9, 0, 9);

            GridMeshBuilder.BuildFullMesh(grid, Color.white, mesh);

            Assert.AreEqual(400, mesh.vertexCount, "100 cells × 4 verts = 400");
            Assert.AreEqual(600, mesh.triangles.Length, "100 cells × 6 indices = 600");
            Assert.AreEqual(IndexFormat.UInt32, mesh.indexFormat);
        }

        // =============================================
        // GT-015: Quad position for cell (0,0,0)
        // =============================================
        [Test]
        public void GT015_QuadPositionForCell000()
        {
            // Single cell at (0,0,0)
            grid.SetCell(new Vector3Int(0, 0, 0), CellData.CreateWalkable());

            GridMeshBuilder.BuildFullMesh(grid, Color.white, mesh);

            Assert.AreEqual(4, mesh.vertexCount);
            var verts = mesh.vertices;

            // With CellInset=0.03, cellSize=1.5:
            // x0 = 0 + 0.03 = 0.03, x1 = 1.5 - 0.03 = 1.47
            // z0 = 0 + 0.03 = 0.03, z1 = 1.5 - 0.03 = 1.47
            // Y = 0 * 1.5 + 0.01 = 0.01
            float inset = 0.03f;
            float y = 0.01f;
            float cellSize = 1.5f;

            AssertVertex(verts, new Vector3(inset, y, inset));
            AssertVertex(verts, new Vector3(cellSize - inset, y, inset));
            AssertVertex(verts, new Vector3(cellSize - inset, y, cellSize - inset));
            AssertVertex(verts, new Vector3(inset, y, cellSize - inset));
        }

        // =============================================
        // GT-016: Impassable cells not rendered
        // =============================================
        [Test]
        public void GT016_ImpassableNotRendered()
        {
            FillRect(grid, 0, 9, 0, 9);
            grid.SetCell(new Vector3Int(3, 0, 3), CellData.CreateImpassable());

            GridMeshBuilder.BuildFullMesh(grid, Color.white, mesh);

            Assert.AreEqual(396, mesh.vertexCount, "99 walkable cells × 4 verts = 396");
        }

        // =============================================
        // GT-029: Mesh on two levels
        // =============================================
        [Test]
        public void GT029_MeshTwoLevels()
        {
            // 5×5 at elevation 0
            FillRect(grid, 0, 4, 0, 4, elevation: 0);
            // 3×3 at elevation 1
            FillRect(grid, 0, 2, 0, 2, elevation: 1);

            GridMeshBuilder.BuildFullMesh(grid, Color.white, mesh);

            int expectedCells = 25 + 9;
            Assert.AreEqual(expectedCells * 4, mesh.vertexCount, $"{expectedCells} cells × 4 = {expectedCells * 4}");

            // Verify elevation 1 vertices have Y = heightStep + YOffset = 1.5 + 0.01 = 1.51
            var verts = mesh.vertices;
            float heightStep = 1.5f;
            float yOffset = 0.01f;
            float elevY = heightStep + yOffset;

            bool foundElevated = false;
            foreach (var v in verts)
            {
                if (Mathf.Abs(v.y - elevY) < 0.001f)
                {
                    foundElevated = true;
                    break;
                }
            }
            Assert.IsTrue(foundElevated, $"Should have vertices at Y ≈ {elevY} for elevation 1");
        }

        // =============================================
        // GT-036: Chunk dirty rebuild
        // =============================================
        [Test]
        public void GT036_ChunkDirtyRebuild()
        {
            FillRect(grid, 0, 9, 0, 9);

            // Build initial chunk mesh
            var chunkCoord = grid.GetChunkCoord(new Vector3Int(5, 0, 5));
            var chunkMesh = new Mesh();
            GridMeshBuilder.BuildChunkMesh(grid, chunkCoord, 16, Color.white, chunkMesh);
            int initialVertCount = chunkMesh.vertexCount;
            Assert.AreEqual(400, initialVertCount, "10×10 in one chunk = 400 verts");

            // Drain dirty flags from initial setup
            var dirtyBuffer = new System.Collections.Generic.List<Vector3Int>();
            grid.GetDirtyChunks(dirtyBuffer);

            // Make (5,0,5) impassable
            grid.SetCell(new Vector3Int(5, 0, 5), CellData.CreateImpassable());

            // Check only the correct chunk is marked dirty
            int dirtyCount = grid.GetDirtyChunks(dirtyBuffer);
            Assert.AreEqual(1, dirtyCount, "Only one chunk should be dirty");
            Assert.AreEqual(chunkCoord, dirtyBuffer[0], "The dirty chunk should contain (5,0,5)");

            // Rebuild
            GridMeshBuilder.BuildChunkMesh(grid, chunkCoord, 16, Color.white, chunkMesh);
            Assert.AreEqual(initialVertCount - 4, chunkMesh.vertexCount,
                "After making one cell impassable, mesh should have 4 fewer vertices");

            Object.DestroyImmediate(chunkMesh);
        }

        // =============================================
        // Helpers
        // =============================================

        private static void AssertVertex(Vector3[] verts, Vector3 expected, float tolerance = 0.001f)
        {
            bool found = false;
            foreach (var v in verts)
            {
                if (Mathf.Abs(v.x - expected.x) < tolerance &&
                    Mathf.Abs(v.y - expected.y) < tolerance &&
                    Mathf.Abs(v.z - expected.z) < tolerance)
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, $"Expected vertex {expected} not found in mesh vertices");
        }
    }
}
