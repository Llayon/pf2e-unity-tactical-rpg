using UnityEngine;

namespace PF2e.Grid
{
    /// <summary>
    /// Data holder for a single grid chunk.
    /// Each chunk covers a chunkSize×chunkSize area at a specific elevation.
    /// ChunkCoord = (chunkX, elevation, chunkZ).
    /// </summary>
    public class GridChunk
    {
        public Vector3Int ChunkCoord { get; }
        public GameObject GameObject { get; set; }
        public MeshFilter MeshFilter { get; set; }
        public MeshRenderer MeshRenderer { get; set; }
        public Mesh Mesh { get; set; }
        public bool IsDirty { get; set; }

        public GridChunk(Vector3Int chunkCoord)
        {
            ChunkCoord = chunkCoord;
            IsDirty = true;
        }
    }
}
