using System.Collections.Generic;
using UnityEngine;

namespace PF2e.Grid
{
    /// <summary>
    /// Creates physics colliders for floors above 0 (GroundPlane handles floor 0).
    /// Colliders are per-chunk with tight bounds for accurate raycasting.
    /// </summary>
    [RequireComponent(typeof(GridManager))]
    public class GridFloorColliders : MonoBehaviour
    {
        private GridManager gridManager;
        private readonly List<GameObject> createdColliders = new List<GameObject>();

        private int currentFloor = int.MaxValue;

        private void Start()
        {
            gridManager = GetComponent<GridManager>();
            if (gridManager == null) return;

            gridManager.OnGridChanged += OnGridChanged;

            RebuildColliders();
        }

        private void OnDestroy()
        {
            if (gridManager != null)
                gridManager.OnGridChanged -= OnGridChanged;

            DestroyAllColliders();
        }

        private void OnGridChanged()
        {
            RebuildColliders();
        }

        public void SetCurrentFloor(int floor)
        {
            currentFloor = floor;
            ApplyActiveState();
        }

        private struct BoundsXZ
        {
            public int minX, maxX, minZ, maxZ;
            public bool initialized;

            public void Encapsulate(int x, int z)
            {
                if (!initialized)
                {
                    initialized = true;
                    minX = maxX = x;
                    minZ = maxZ = z;
                    return;
                }
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }
        }

        private void RebuildColliders()
        {
            DestroyAllColliders();

            if (gridManager == null || gridManager.Data == null || gridManager.Config == null) return;

            float cellSize = gridManager.Config.cellWorldSize;
            float heightStep = gridManager.Config.heightStepWorldSize;

            var boundsPerChunk = new Dictionary<Vector3Int, BoundsXZ>();

            foreach (var kvp in gridManager.Data.Cells)
            {
                Vector3Int pos = kvp.Key;
                if (pos.y <= 0) continue;

                Vector3Int chunkCoord = gridManager.Data.GetChunkCoord(pos);

                boundsPerChunk.TryGetValue(chunkCoord, out var b);
                b.Encapsulate(pos.x, pos.z);
                boundsPerChunk[chunkCoord] = b;
            }

            foreach (var kvp in boundsPerChunk)
            {
                Vector3Int chunkCoord = kvp.Key;
                BoundsXZ b = kvp.Value;

                int y = chunkCoord.y;

                float centerX = ((b.minX + b.maxX + 1) * 0.5f) * cellSize;
                float centerZ = ((b.minZ + b.maxZ + 1) * 0.5f) * cellSize;
                float centerY = y * heightStep;

                float sizeX = (b.maxX - b.minX + 1) * cellSize;
                float sizeZ = (b.maxZ - b.minZ + 1) * cellSize;

                var go = new GameObject($"Floor{y}_Chunk_{chunkCoord.x}_{chunkCoord.z}_Collider");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(centerX, centerY, centerZ);
                go.layer = gameObject.layer;

                var box = go.AddComponent<BoxCollider>();
                box.center = Vector3.zero;
                box.size = new Vector3(sizeX, 0.1f, sizeZ);

                var fl = go.AddComponent<FloorLevel>();
                fl.elevation = y;

                createdColliders.Add(go);
            }

            ApplyActiveState();
        }

        private void ApplyActiveState()
        {
            foreach (var go in createdColliders)
            {
                if (go == null) continue;
                var fl = go.GetComponent<FloorLevel>();
                if (fl == null) continue;

                go.SetActive(fl.elevation <= currentFloor);
            }
        }

        private void DestroyAllColliders()
        {
            for (int i = 0; i < createdColliders.Count; i++)
            {
                var go = createdColliders[i];
                if (go == null) continue;

                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            createdColliders.Clear();
        }
    }
}
