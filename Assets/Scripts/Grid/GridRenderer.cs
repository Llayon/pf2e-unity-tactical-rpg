using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PF2e.Data;

namespace PF2e.Grid
{
    /// <summary>
    /// Manages chunk GameObjects for grid rendering.
    /// Listens to GridManager.OnGridChanged, rebuilds only dirty chunks.
    /// Toggle G to show/hide.
    /// </summary>
    [RequireComponent(typeof(GridManager))]
    public class GridRenderer : MonoBehaviour
    {
        private GridManager gridManager;
        private GridData gridData;
        private Material gridMaterial;

        private readonly Dictionary<Vector3Int, GridChunk> chunks = new();
        private readonly List<Vector3Int> dirtyBuffer = new(16);
        private readonly HashSet<Vector3Int> neededChunksBuffer = new();
        private readonly List<Vector3Int> toRemoveBuffer = new();

        private bool gridVisible = true;
        private int chunkSize;

        private void OnEnable()
        {
            gridManager = GetComponent<GridManager>();
            if (gridManager == null) return;

            gridManager.OnGridChanged += OnGridChanged;
        }

        private void OnDisable()
        {
            if (gridManager != null)
                gridManager.OnGridChanged -= OnGridChanged;
        }

        private void Start()
        {
            if (gridManager == null || gridManager.Data == null || gridManager.Config == null) return;

            gridData = gridManager.Data;
            chunkSize = Mathf.Max(1, gridManager.Config.chunkSize);

            // Create material: URP Unlit, Transparent, ZWrite Off, vertex color
            gridMaterial = CreateGridMaterial(gridManager.Config.gridColor);

            // Initial full build
            RebuildAllChunks();
        }

        private void Update()
        {
            // Toggle G
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.gKey.wasPressedThisFrame)
            {
                gridVisible = !gridVisible;
                SetChunksVisible(gridVisible);
            }
        }

        private void OnGridChanged()
        {
            if (gridData == null || gridMaterial == null) return;

            int count = gridData.GetDirtyChunks(dirtyBuffer);
            if (count == 0) return;

            for (int i = 0; i < dirtyBuffer.Count; i++)
            {
                RebuildChunk(dirtyBuffer[i]);
            }
        }

        private void RebuildAllChunks()
        {
            // Collect all chunks that have cells (reusable buffer)
            neededChunksBuffer.Clear();
            foreach (var kvp in gridData.Cells)
            {
                neededChunksBuffer.Add(gridData.GetChunkCoord(kvp.Key));
            }

            // Remove chunks that no longer have cells (reusable buffer)
            toRemoveBuffer.Clear();
            foreach (var kvp in chunks)
            {
                if (!neededChunksBuffer.Contains(kvp.Key))
                    toRemoveBuffer.Add(kvp.Key);
            }
            foreach (var coord in toRemoveBuffer)
            {
                DestroyChunk(coord);
            }

            // Build/rebuild needed chunks
            foreach (var coord in neededChunksBuffer)
            {
                RebuildChunk(coord);
            }

            // Drain any remaining dirty flags
            gridData.GetDirtyChunks(dirtyBuffer);
        }

        private void RebuildChunk(Vector3Int chunkCoord)
        {
            if (!chunks.TryGetValue(chunkCoord, out var chunk))
            {
                chunk = CreateChunk(chunkCoord);
                chunks[chunkCoord] = chunk;
            }

            GridMeshBuilder.BuildChunkMesh(
                gridData, chunkCoord, chunkSize,
                gridManager.Config.gridColor, chunk.Mesh);

            chunk.MeshFilter.mesh = chunk.Mesh;
            chunk.IsDirty = false;

            // Hide empty chunks
            bool hasVerts = chunk.Mesh.vertexCount > 0;
            chunk.GameObject.SetActive(hasVerts && gridVisible);
        }

        private GridChunk CreateChunk(Vector3Int chunkCoord)
        {
            var chunk = new GridChunk(chunkCoord);

            var go = new GameObject($"GridChunk_{chunkCoord.x}_{chunkCoord.y}_{chunkCoord.z}");
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = gridMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            chunk.GameObject = go;
            chunk.MeshFilter = mf;
            chunk.MeshRenderer = mr;
            chunk.Mesh = new Mesh { name = $"GridMesh_{chunkCoord}" };

            return chunk;
        }

        private void DestroyChunk(Vector3Int chunkCoord)
        {
            if (chunks.TryGetValue(chunkCoord, out var chunk))
            {
                if (chunk.Mesh != null) Destroy(chunk.Mesh);
                if (chunk.GameObject != null) Destroy(chunk.GameObject);
                chunks.Remove(chunkCoord);
            }
        }

        private void SetChunksVisible(bool visible)
        {
            foreach (var kvp in chunks)
            {
                var chunk = kvp.Value;
                bool hasVerts = chunk.Mesh != null && chunk.Mesh.vertexCount > 0;
                if (chunk.GameObject != null)
                    chunk.GameObject.SetActive(visible && hasVerts);
            }
        }

        private static Material CreateGridMaterial(Color color)
        {
            // Use URP Unlit shader with transparency
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                // Fallback
                shader = Shader.Find("Unlit/Color");
            }

            var mat = new Material(shader);

            // Set up for transparency
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);   // Alpha
            mat.SetFloat("_ZWrite", 0);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetColor("_BaseColor", color);
            mat.renderQueue = 3000; // Transparent queue
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            return mat;
        }

        private void OnDestroy()
        {
            foreach (var kvp in chunks)
            {
                if (kvp.Value.Mesh != null) Destroy(kvp.Value.Mesh);
                if (kvp.Value.GameObject != null) Destroy(kvp.Value.GameObject);
            }
            chunks.Clear();

            if (gridMaterial != null) Destroy(gridMaterial);
        }
    }
}
