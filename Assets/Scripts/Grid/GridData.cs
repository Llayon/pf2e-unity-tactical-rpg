using System;
using System.Collections.Generic;
using UnityEngine;
using PF2e.Data;

namespace PF2e.Grid
{
    public class GridData
    {
        // --- Private collections ---
        private readonly Dictionary<Vector3Int, CellData> cells = new();
        private readonly Dictionary<EdgeKey, EdgeData> edges = new();
        // Add-only; kept for potential serialization. Runtime queries use verticalAdj.
        private readonly List<VerticalLink> verticalLinks = new();

        // Adjacency in convenient form: (to, cost, type) — no need to compute "other end"
        private struct VerticalAdjacency
        {
            public Vector3Int to;
            public int costFeet;
            public VerticalLinkType type;
        }
        private readonly Dictionary<Vector3Int, List<VerticalAdjacency>> verticalAdj = new();

        // --- Properties ---
        public float CellWorldSize { get; }
        public float HeightStepWorld { get; }
        public int Version => version;

        private int version;
        private readonly int chunkSize;
        private readonly HashSet<Vector3Int> dirtyChunks = new();

        private const float ElevationSnapEps = 0.001f;

        // Cardinal directions (dx, dz)
        private static readonly Vector3Int[] CardinalOffsets =
        {
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 0, 1), new(0, 0, -1)
        };

        // Diagonal directions (dx, dz)
        private static readonly Vector3Int[] DiagonalOffsets =
        {
            new(1, 0, 1), new(1, 0, -1),
            new(-1, 0, 1), new(-1, 0, -1)
        };

        // For each diagonal: the two intermediate cardinals that must be passable
        // Index matches DiagonalOffsets
        private static readonly (Vector3Int, Vector3Int)[] DiagonalIntermediates =
        {
            (new(1, 0, 0), new(0, 0, 1)),   // (1,0,1)
            (new(1, 0, 0), new(0, 0, -1)),  // (1,0,-1)
            (new(-1, 0, 0), new(0, 0, 1)),  // (-1,0,1)
            (new(-1, 0, 0), new(0, 0, -1)), // (-1,0,-1)
        };

        // --- Constructor ---
        public GridData(GridConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            CellWorldSize = config.cellWorldSize;
            HeightStepWorld = config.heightStepWorldSize;
            chunkSize = Mathf.Max(1, config.chunkSize);
        }

        /// <summary>
        /// Constructor for unit tests without ScriptableObject.
        /// </summary>
        public GridData(float cellWorldSize, float heightStepWorld, int chunkSize = 16)
        {
            CellWorldSize = cellWorldSize;
            HeightStepWorld = heightStepWorld;
            this.chunkSize = Mathf.Max(1, chunkSize);
        }

        // =============================================
        // READ API
        // =============================================

        public bool HasCell(Vector3Int pos) => cells.ContainsKey(pos);

        public bool TryGetCell(Vector3Int pos, out CellData cell) => cells.TryGetValue(pos, out cell);

        public IReadOnlyDictionary<Vector3Int, CellData> Cells => cells;

        public bool TryGetEdge(EdgeKey key, out EdgeData data) => edges.TryGetValue(key, out data);

        public int CellCount => cells.Count;

        // =============================================
        // WRITE API (version++, dirty chunks)
        // =============================================

        public void SetCell(Vector3Int pos, CellData data)
        {
            cells[pos] = data;
            dirtyChunks.Add(GetChunkCoord(pos));
            version++;
        }

        public void SetEdge(EdgeKey key, EdgeData data)
        {
            edges[key] = data;
            dirtyChunks.Add(GetChunkCoord(key.CellA));
            dirtyChunks.Add(GetChunkCoord(key.CellB));
            version++;
        }

        /// <summary>
        /// Links are add-only for now. If RemoveVerticalLink is needed later,
        /// must update both verticalLinks and verticalAdj.
        /// </summary>
        public void AddVerticalLink(VerticalLink link)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (link.movementCostFeet <= 0)
                Debug.LogWarning($"[GridData] VerticalLink cost <= 0: {link.movementCostFeet} ({link.lower} -> {link.upper})");
            if (link.lower.y >= link.upper.y)
                Debug.LogWarning($"[GridData] VerticalLink lower.y >= upper.y: lower={link.lower}, upper={link.upper} (semantics check)");
#endif
            verticalLinks.Add(link);
            AddAdj(link.lower, new VerticalAdjacency
            {
                to = link.upper,
                costFeet = link.movementCostFeet,
                type = link.type
            });
            AddAdj(link.upper, new VerticalAdjacency
            {
                to = link.lower,
                costFeet = link.movementCostFeet,
                type = link.type
            });
            dirtyChunks.Add(GetChunkCoord(link.lower));
            dirtyChunks.Add(GetChunkCoord(link.upper));
            version++;
        }

        private void AddAdj(Vector3Int cell, VerticalAdjacency adj)
        {
            if (!verticalAdj.TryGetValue(cell, out var list))
            {
                list = new List<VerticalAdjacency>(2);
                verticalAdj[cell] = list;
            }
            list.Add(adj);
        }

        // =============================================
        // CHUNK TRACKING
        // =============================================

        /// <summary>
        /// Uses Mathf.FloorToInt, NOT integer division.
        /// Integer division truncates toward zero and breaks negative coordinates.
        /// </summary>
        public Vector3Int GetChunkCoord(Vector3Int cellPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt((float)cellPos.x / chunkSize),
                cellPos.y, // elevation = chunk layer, not divided
                Mathf.FloorToInt((float)cellPos.z / chunkSize));
        }

        /// <summary>
        /// Zero-alloc: caller provides reusable buffer.
        /// Uses foreach+Add (not AddRange) for guaranteed zero GC.
        /// </summary>
        public int GetDirtyChunks(List<Vector3Int> outList)
        {
            outList.Clear();
            foreach (var c in dirtyChunks) outList.Add(c);
            dirtyChunks.Clear();
            return outList.Count;
        }

        // =============================================
        // COORDINATE CONVERSION
        // =============================================

        public Vector3Int WorldToCell(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / CellWorldSize),
                Mathf.FloorToInt((worldPos.y + ElevationSnapEps) / HeightStepWorld),
                Mathf.FloorToInt(worldPos.z / CellWorldSize));
        }

        public Vector3 CellToWorld(Vector3Int cell)
        {
            return new Vector3(
                (cell.x + 0.5f) * CellWorldSize,
                cell.y * HeightStepWorld,
                (cell.z + 0.5f) * CellWorldSize);
        }

        // =============================================
        // NEIGHBORS
        // =============================================

        /// <summary>
        /// Gets passable neighbors for a cell.
        /// Strict diagonal rule: both intermediate cardinals must be passable
        /// (cell exists + walkable + edge not blocking).
        /// outList.Clear() is called inside.
        /// </summary>
        public void GetNeighbors(Vector3Int from, MovementType moveType, List<NeighborInfo> outList)
        {
            outList.Clear();

            if (!IsCellPassable(from, moveType)) return;

            // Cardinal neighbors
            for (int i = 0; i < CardinalOffsets.Length; i++)
            {
                var to = from + CardinalOffsets[i];
                // Keep same elevation
                // to.y is already == from.y because CardinalOffsets have y=0

                if (IsStepPassable(from, to, moveType))
                {
                    outList.Add(new NeighborInfo(to, NeighborType.Cardinal));
                }
            }

            // Diagonal neighbors (strict rule: both intermediates must be passable steps)
            for (int i = 0; i < DiagonalOffsets.Length; i++)
            {
                var to = from + DiagonalOffsets[i];
                var (interA, interB) = DiagonalIntermediates[i];

                var intermediateA = from + interA;
                var intermediateB = from + interB;

                // Both intermediate cardinal steps must be legal,
                // and edges intermediate→target must not block (wall corner check)
                if (IsStepPassable(from, intermediateA, moveType) &&
                    IsStepPassable(from, intermediateB, moveType) &&
                    IsCellPassable(to, moveType) &&
                    !IsEdgeBlocking(from, to) &&
                    !IsEdgeBlocking(intermediateA, to) &&
                    !IsEdgeBlocking(intermediateB, to))
                {
                    outList.Add(new NeighborInfo(to, NeighborType.Diagonal));
                }
            }

            // Vertical neighbors (from adjacency map)
            if (verticalAdj.TryGetValue(from, out var adjList))
            {
                foreach (var adj in adjList)
                {
                    if (IsCellPassable(adj.to, moveType))
                    {
                        outList.Add(new NeighborInfo(adj.to, NeighborType.Vertical, adj.costFeet));
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the edge between two cells blocks movement.
        /// </summary>
        private bool IsEdgeBlocking(Vector3Int a, Vector3Int b)
        {
            var key = new EdgeKey(a, b);
            return edges.TryGetValue(key, out var data) && data.blocksMovement;
        }

        /// <summary>
        /// Checks if a step from → to is passable:
        /// 1. Target cell exists and is walkable/flyable
        /// 2. Edge between from and to does not block movement
        /// </summary>
        private bool IsStepPassable(Vector3Int from, Vector3Int to, MovementType moveType)
        {
            if (!IsCellPassable(to, moveType)) return false;
            if (IsEdgeBlocking(from, to)) return false;
            return true;
        }

        /// <summary>
        /// Checks if a cell is passable for the given movement type.
        /// Non-existent cells are treated as impassable.
        /// </summary>
        public bool IsCellPassable(Vector3Int pos, MovementType moveType)
        {
            if (!cells.TryGetValue(pos, out var cell)) return false;

            return moveType switch
            {
                MovementType.Walk => cell.IsWalkable,
                MovementType.Fly => cell.IsFlyable,
                MovementType.Swim => (cell.flags & CellFlags.Swimmable) != 0,
                MovementType.Climb => cell.IsWalkable, // Climb uses walkable cells + vertical links
                _ => false
            };
        }
    }
}
