using System.Collections.Generic;
using UnityEngine;

namespace PF2e.Grid
{
    /// <summary>
    /// Object pool for cell highlight quads (hover, selection, path, zone).
    /// Creates quads on demand, reuses deactivated ones.
    /// </summary>
    public class CellHighlightPool : MonoBehaviour
    {
        [SerializeField] private Material highlightMaterial;

        private readonly Stack<GameObject> pool = new();
        private readonly HashSet<GameObject> activeHighlights = new();

        private static MaterialPropertyBlock s_PropBlock;

        private const float YOffset = 0.02f; // Above grid mesh (0.01)

        public Material HighlightMaterial
        {
            get => highlightMaterial;
            set => highlightMaterial = value;
        }

        /// <summary>
        /// Show a highlight at the given cell position with specified color.
        /// </summary>
        public GameObject ShowHighlight(Vector3 worldCenter, float cellSize, Color color)
        {
            var go = GetOrCreate();
            go.SetActive(true);

            // Position at cell center, slightly above grid
            go.transform.position = new Vector3(worldCenter.x, worldCenter.y + YOffset, worldCenter.z);
            go.transform.localScale = new Vector3(cellSize * 0.95f, 1f, cellSize * 0.95f);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                s_PropBlock ??= new MaterialPropertyBlock();
                s_PropBlock.SetColor("_BaseColor", color);
                mr.SetPropertyBlock(s_PropBlock);
            }

            activeHighlights.Add(go);
            return go;
        }

        /// <summary>
        /// Return all active highlights to the pool.
        /// </summary>
        public void ClearAll()
        {
            foreach (var go in activeHighlights)
            {
                if (go != null)
                {
                    go.SetActive(false);
                    pool.Push(go);
                }
            }
            activeHighlights.Clear();
        }

        /// <summary>
        /// Return a specific highlight to the pool.
        /// Idempotent: safe to call even if already returned via ClearAll().
        /// </summary>
        public void Return(GameObject go)
        {
            if (go == null) return;

            // If it wasn't in activeHighlights, it was already returned/cleared.
            // Don't push to pool again (would cause duplicates).
            if (!activeHighlights.Remove(go))
            {
                go.SetActive(false);
                return;
            }

            go.SetActive(false);
            pool.Push(go);
        }

        private GameObject GetOrCreate()
        {
            if (pool.Count > 0)
                return pool.Pop();

            // Create a quad
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "CellHighlight";
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Flat on XZ plane

            // Remove collider
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Set shared material (no clone; color via MaterialPropertyBlock)
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && highlightMaterial != null)
            {
                mr.sharedMaterial = highlightMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            go.SetActive(false);
            return go;
        }

        private void OnDestroy()
        {
            foreach (var go in activeHighlights)
                if (go != null) Destroy(go);
            foreach (var go in pool)
                if (go != null) Destroy(go);
        }
    }
}
