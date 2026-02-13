using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PF2e.Grid
{
    /// <summary>
    /// Generates mesh for a grid chunk. One quad per walkable cell.
    /// Vertices are positioned in world space. Y offset = 0.01 above elevation.
    /// Uses IndexFormat.UInt32 to support large chunks.
    /// </summary>
    public static class GridMeshBuilder
    {
        private const float YOffset = 0.01f;
        private const float CellInset = 0.03f; // Gap between cells for visible grid lines

        // Reusable buffers to avoid per-call allocations.
        // WARNING: Static buffers — NOT thread-safe. Call only from main thread.
        private static readonly List<Vector3> s_Vertices = new(1024);
        private static readonly List<int> s_Triangles = new(1536);
        private static readonly List<Color> s_Colors = new(1024);

        /// <summary>
        /// Build mesh for all walkable cells that belong to the given chunk.
        /// </summary>
        public static void BuildChunkMesh(
            GridData grid, Vector3Int chunkCoord, int chunkSize,
            Color gridColor, Mesh mesh)
        {
            s_Vertices.Clear();
            s_Triangles.Clear();
            s_Colors.Clear();

            float cellSize = grid.CellWorldSize;
            float heightStep = grid.HeightStepWorld;
            int elevation = chunkCoord.y;

            int startX = chunkCoord.x * chunkSize;
            int startZ = chunkCoord.z * chunkSize;

            for (int lx = 0; lx < chunkSize; lx++)
            {
                for (int lz = 0; lz < chunkSize; lz++)
                {
                    int cx = startX + lx;
                    int cz = startZ + lz;
                    var cellPos = new Vector3Int(cx, elevation, cz);

                    if (!grid.TryGetCell(cellPos, out var cell)) continue;
                    if (cell.terrain == CellTerrain.Impassable) continue;
                    if (!cell.IsWalkable) continue;

                    float worldX = cx * cellSize;
                    float worldZ = cz * cellSize;
                    float worldY = elevation * heightStep + YOffset;

                    AddCellQuad(worldX, worldY, worldZ, cellSize, gridColor);
                }
            }

            FlushToMesh(mesh);
        }

        /// <summary>
        /// Build mesh for ALL walkable cells in the grid (non-chunked, for unit tests).
        /// </summary>
        public static void BuildFullMesh(GridData grid, Color gridColor, Mesh mesh)
        {
            s_Vertices.Clear();
            s_Triangles.Clear();
            s_Colors.Clear();

            float cellSize = grid.CellWorldSize;
            float heightStep = grid.HeightStepWorld;

            foreach (var kvp in grid.Cells)
            {
                var cellPos = kvp.Key;
                var cell = kvp.Value;

                if (cell.terrain == CellTerrain.Impassable) continue;
                if (!cell.IsWalkable) continue;

                float worldX = cellPos.x * cellSize;
                float worldZ = cellPos.z * cellSize;
                float worldY = cellPos.y * heightStep + YOffset;

                AddCellQuad(worldX, worldY, worldZ, cellSize, gridColor);
            }

            FlushToMesh(mesh);
        }

        private static void AddCellQuad(float worldX, float worldY, float worldZ,
            float cellSize, Color color)
        {
            int vi = s_Vertices.Count;

            float x0 = worldX + CellInset;
            float x1 = worldX + cellSize - CellInset;
            float z0 = worldZ + CellInset;
            float z1 = worldZ + cellSize - CellInset;

            s_Vertices.Add(new Vector3(x0, worldY, z0));
            s_Vertices.Add(new Vector3(x1, worldY, z0));
            s_Vertices.Add(new Vector3(x1, worldY, z1));
            s_Vertices.Add(new Vector3(x0, worldY, z1));

            s_Colors.Add(color);
            s_Colors.Add(color);
            s_Colors.Add(color);
            s_Colors.Add(color);

            s_Triangles.Add(vi);
            s_Triangles.Add(vi + 3);
            s_Triangles.Add(vi + 1);
            s_Triangles.Add(vi + 1);
            s_Triangles.Add(vi + 3);
            s_Triangles.Add(vi + 2);
        }

        private static void FlushToMesh(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;

            if (s_Vertices.Count > 0)
            {
                mesh.SetVertices(s_Vertices);
                mesh.SetTriangles(s_Triangles, 0);
                mesh.SetColors(s_Colors);
                mesh.RecalculateBounds();
            }
        }
    }
}
