using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;

namespace PF2e.Presentation
{
    /// <summary>
    /// Visualizes movement zone for selected player entity.
    /// Green = 1 action, Yellow = 2 actions, Red = 3 actions.
    /// Path preview on hover within zone.
    ///
    /// NOTE: Update order between GridInteraction and this component
    /// is not guaranteed. Path preview may lag by 1 frame. Acceptable.
    ///
    /// TODO: After G key (grid hide), ClearAll may deactivate zone highlights.
    /// Zone won't auto-restore. Re-clicking entity will re-show zone.
    /// </summary>
    public class MovementZoneVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private CellHighlightPool highlightPool;

        [Header("Zone Colors")]
        [SerializeField] private Color action1Color = new Color(0f, 0.8f, 0f, 0.35f);
        [SerializeField] private Color action2Color = new Color(0.9f, 0.9f, 0f, 0.35f);
        [SerializeField] private Color action3Color = new Color(0.9f, 0.2f, 0f, 0.35f);

        [Header("Path Preview")]
        [SerializeField] private Color pathColor = new Color(0f, 0.5f, 1f, 0.5f);

        private readonly Dictionary<Vector3Int, int> currentZone = new Dictionary<Vector3Int, int>();
        private readonly List<GameObject> zoneHighlights = new List<GameObject>();
        private readonly List<GameObject> pathHighlights = new List<GameObject>();
        private readonly List<Vector3Int> pathBuffer = new List<Vector3Int>();

        private EntityHandle showingFor;
        private Vector3Int? lastHoveredZoneCell;

        private void Start()
        {
            if (entityManager == null)
                entityManager = FindObjectOfType<EntityManager>();
            if (gridManager == null)
                gridManager = FindObjectOfType<GridManager>();
            if (highlightPool == null)
                highlightPool = FindObjectOfType<CellHighlightPool>();

            if (entityManager == null || gridManager == null || highlightPool == null)
            {
                Debug.LogError("[MovementZoneVisualizer] Missing references! Disabling.");
                enabled = false;
                return;
            }

            entityManager.OnEntitySelected += OnEntitySelected;
            entityManager.OnEntityDeselected += OnEntityDeselected;
        }

        private void OnDestroy()
        {
            if (entityManager != null)
            {
                entityManager.OnEntitySelected -= OnEntitySelected;
                entityManager.OnEntityDeselected -= OnEntityDeselected;
            }
        }

        private void Update()
        {
            if (!showingFor.IsValid) return;
            UpdatePathPreview();
        }

        private void OnEntitySelected(EntityHandle handle)
        {
            var data = entityManager.Registry.Get(handle);
            if (data == null || data.Team != Team.Player)
            {
                ClearZone();
                return;
            }
            ShowZoneFor(handle);
        }

        private void OnEntityDeselected()
        {
            ClearZone();
        }

        private void ShowZoneFor(EntityHandle handle)
        {
            ClearZone();

            var data = entityManager.Registry.Get(handle);
            if (data == null || gridManager.Data == null) return;

            showingFor = handle;

            int budgetFeet = data.Speed * 3;

            var profile = new MovementProfile
            {
                moveType = MovementType.Walk,
                speedFeet = data.Speed,
                creatureSizeCells = data.SizeCells,
                ignoresDifficultTerrain = false
            };

            entityManager.Pathfinding.GetMovementZone(
                gridManager.Data,
                data.GridPosition,
                profile,
                budgetFeet,
                handle,
                entityManager.Occupancy,
                currentZone);

            float cellSize = gridManager.Config.cellWorldSize;
            int speed1 = data.Speed;
            int speed2 = data.Speed * 2;

            foreach (var kvp in currentZone)
            {
                Vector3Int pos = kvp.Key;
                int cost = kvp.Value;

                if (pos == data.GridPosition) continue;

                Color color;
                if (cost <= speed1) color = action1Color;
                else if (cost <= speed2) color = action2Color;
                else color = action3Color;

                var worldPos = gridManager.Data.CellToWorld(pos);
                var highlight = highlightPool.ShowHighlight(worldPos, cellSize, color);
                zoneHighlights.Add(highlight);
            }
        }

        private void ClearZone()
        {
            foreach (var go in zoneHighlights)
            {
                if (go != null && highlightPool != null)
                    highlightPool.Return(go);
            }
            zoneHighlights.Clear();
            currentZone.Clear();
            showingFor = EntityHandle.None;

            ClearPathPreview();
        }

        private void UpdatePathPreview()
        {
            var hoveredCell = gridManager.HoveredCell;

            if (!hoveredCell.HasValue)
            {
                if (lastHoveredZoneCell.HasValue)
                {
                    ClearPathPreview();
                    lastHoveredZoneCell = null;
                }
                return;
            }

            if (hoveredCell == lastHoveredZoneCell)
                return;

            lastHoveredZoneCell = hoveredCell.Value;

            if (!currentZone.ContainsKey(hoveredCell.Value))
            {
                ClearPathPreview();
                return;
            }

            ClearPathPreview();

            var data = entityManager.Registry.Get(showingFor);
            if (data == null) return;

            var profile = new MovementProfile
            {
                moveType = MovementType.Walk,
                speedFeet = data.Speed,
                creatureSizeCells = data.SizeCells,
                ignoresDifficultTerrain = false
            };

            bool found = entityManager.Pathfinding.FindPath(
                gridManager.Data,
                data.GridPosition,
                hoveredCell.Value,
                profile,
                showingFor,
                entityManager.Occupancy,
                pathBuffer,
                out int totalCost);

            if (!found) return;

            float cellSize = gridManager.Config.cellWorldSize;
            for (int i = 1; i < pathBuffer.Count; i++)
            {
                var worldPos = gridManager.Data.CellToWorld(pathBuffer[i]);
                var highlight = highlightPool.ShowHighlight(worldPos, cellSize * 0.6f, pathColor);
                pathHighlights.Add(highlight);
            }
        }

        private void ClearPathPreview()
        {
            foreach (var go in pathHighlights)
            {
                if (go != null && highlightPool != null)
                    highlightPool.Return(go);
            }
            pathHighlights.Clear();
        }
    }
}
