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
    /// Must be on the same GameObject as GridManager, GridFloorController, CellHighlightPool
    /// (TacticalGrid) so GetComponent works for all dependencies.
    ///
    /// NOTE: Update order vs GridInteraction not guaranteed; path preview may lag 1 frame.
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

        private GridFloorController floorController;
        private bool visualsEnabled = true;

        private void Start()
        {
            // gridManager, highlightPool must be on same GO (TacticalGrid)
            if (gridManager == null) gridManager = GetComponent<GridManager>();
            if (highlightPool == null) highlightPool = GetComponent<CellHighlightPool>();
            floorController = GetComponent<GridFloorController>();

            // EntityManager is on a separate GO — FindObjectOfType acceptable for prototype
            if (entityManager == null) entityManager = FindObjectOfType<EntityManager>();

            if (entityManager == null || gridManager == null || highlightPool == null || floorController == null)
            {
                Debug.LogError("[MovementZoneVisualizer] Missing references/components! Disabling.");
                enabled = false;
                return;
            }

            entityManager.OnEntitySelected += OnEntitySelected;
            entityManager.OnEntityDeselected += OnEntityDeselected;

            floorController.OnGridVisualsToggled += SetVisualsEnabled;
            SetVisualsEnabled(floorController.GridVisualsEnabled);
        }

        private void OnDestroy()
        {
            if (entityManager != null)
            {
                entityManager.OnEntitySelected -= OnEntitySelected;
                entityManager.OnEntityDeselected -= OnEntityDeselected;
            }
            if (floorController != null)
                floorController.OnGridVisualsToggled -= SetVisualsEnabled;
        }

        /// <summary>
        /// Called by GridFloorController.OnGridVisualsToggled.
        /// Clears zone when hidden; restores zone when re-shown if a player is still selected.
        /// </summary>
        public void SetVisualsEnabled(bool enabled)
        {
            visualsEnabled = enabled;

            if (!visualsEnabled)
            {
                ClearZone();
                return;
            }

            // Restore zone if a player entity is still selected
            if (entityManager != null && entityManager.SelectedEntity.IsValid)
                OnEntitySelected(entityManager.SelectedEntity);
        }

        private void Update()
        {
            if (!visualsEnabled || !showingFor.IsValid) return;
            UpdatePathPreview();
        }

        private void OnEntitySelected(EntityHandle handle)
        {
            if (!visualsEnabled) return;

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
                var pos = kvp.Key;
                int cost = kvp.Value;
                if (pos == data.GridPosition) continue;

                Color color = cost <= speed1 ? action1Color :
                              cost <= speed2 ? action2Color : action3Color;

                var worldPos = gridManager.Data.CellToWorld(pos);
                zoneHighlights.Add(highlightPool.ShowHighlight(worldPos, cellSize, color));
            }
        }

        private void ClearZone()
        {
            foreach (var go in zoneHighlights)
                if (go != null && highlightPool != null) highlightPool.Return(go);
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

            if (!entityManager.Pathfinding.FindPath(
                gridManager.Data,
                data.GridPosition,
                hoveredCell.Value,
                profile,
                showingFor,
                entityManager.Occupancy,
                pathBuffer,
                out int _))
                return;

            float cellSize = gridManager.Config.cellWorldSize;
            for (int i = 1; i < pathBuffer.Count; i++)
            {
                var worldPos = gridManager.Data.CellToWorld(pathBuffer[i]);
                pathHighlights.Add(highlightPool.ShowHighlight(worldPos, cellSize * 0.6f, pathColor));
            }
        }

        private void ClearPathPreview()
        {
            foreach (var go in pathHighlights)
                if (go != null && highlightPool != null) highlightPool.Return(go);
            pathHighlights.Clear();
        }
    }
}
