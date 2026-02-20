using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Visualizes movement zone for the active actor.
    /// In combat: zone driven by TurnManager (active entity + remaining actions).
    /// In exploration: zone driven by EntityManager selection (legacy behavior).
    /// Green = 1 action, Yellow = 2 actions, Red = 3 actions.
    /// Path preview on hover within zone.
    ///
    /// Must be on the same GameObject as GridManager, GridFloorController, CellHighlightPool
    /// (TacticalGrid) so GetComponent works for those dependencies.
    /// EntityManager and TurnManager must be assigned via Inspector.
    /// </summary>
    public class MovementZoneVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private CellHighlightPool highlightPool;

        [Header("Combat")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private StrideAction strideAction;
        [SerializeField] private EntityMover entityMover;

        [Header("Zone Colors")]
        [SerializeField] private Color action1Color = new Color(0f, 0.8f, 0f, 0.35f);
        [SerializeField] private Color action2Color = new Color(0.9f, 0.9f, 0f, 0.35f);
        [SerializeField] private Color action3Color = new Color(0.9f, 0.2f, 0f, 0.35f);

        [Header("Path Preview")]
        [SerializeField] private Color pathAction1Color = new Color(0f, 0.85f, 0f, 0.55f);
        [SerializeField] private Color pathAction2Color = new Color(0.95f, 0.95f, 0f, 0.55f);
        [SerializeField] private Color pathAction3Color = new Color(0.95f, 0.25f, 0f, 0.55f);

        [Header("Stride Boundary Markers")]
        [SerializeField] private float boundaryMarkerScale = 0.45f;
        [SerializeField] private float boundaryMarkerExtraYOffset = 0.03f;
        [SerializeField] private float boundaryMarkerYawDegrees = 45f;

        private readonly Dictionary<Vector3Int, int> currentZoneActions = new Dictionary<Vector3Int, int>();
        private readonly List<GameObject> zoneHighlights = new List<GameObject>();
        private readonly List<GameObject> pathHighlights = new List<GameObject>();
        private readonly List<Vector3Int> pathBuffer = new List<Vector3Int>();
        private readonly List<int> pathActionBoundaries = new List<int>(8);

        private EntityHandle showingFor;
        private int showingMaxActions = 0;
        private Vector3Int? lastHoveredZoneCell;

        // ─── Committed path playback state ───────────────────────────────────
        private bool movementPlaybackActive;
        private readonly List<Vector3Int> committedPath = new List<Vector3Int>();
        private readonly List<int> committedBoundaries = new List<int>(8);
        private readonly List<GameObject> committedPathHighlights = new List<GameObject>();
        private readonly List<GameObject> strideBoundaryMarkers = new List<GameObject>();
        private int committedNextPathIndex; // next cell index the entity will reach

        private float lastPathUpdateTime = -1f;
        private const float PathUpdateInterval = 0.05f;

        private GridFloorController floorController;
        private bool visualsEnabled = true;

        private bool IsCombatMode =>
            turnManager != null && turnManager.State != TurnState.Inactive;

        private void Start()
        {
            // gridManager, highlightPool must be on same GO (TacticalGrid)
            if (gridManager == null) gridManager = GetComponent<GridManager>();
            if (highlightPool == null) highlightPool = GetComponent<CellHighlightPool>();
            floorController = GetComponent<GridFloorController>();

            if (entityManager == null)
            {
                Debug.LogError("[MovementZoneVisualizer] EntityManager not assigned! Assign in Inspector. Disabling.", this);
                enabled = false;
                return;
            }

            if (gridManager == null || highlightPool == null || floorController == null)
            {
                Debug.LogError("[MovementZoneVisualizer] Missing GridManager/CellHighlightPool/GridFloorController! Disabling.", this);
                enabled = false;
                return;
            }

            if (turnManager != null && eventBus == null)
            {
                Debug.LogError("[MovementZoneVisualizer] Missing CombatEventBus for typed combat events. Disabling.", this);
                enabled = false;
                return;
            }

            // Selection-driven zone (exploration mode; guarded in handlers for combat)
            entityManager.OnEntitySelected += OnEntitySelected;
            entityManager.OnEntityDeselected += OnEntityDeselected;

            // TurnManager-driven zone (combat mode)
            if (turnManager != null && eventBus != null)
            {
                eventBus.OnTurnStartedTyped += HandleTurnStarted;
                eventBus.OnTurnEndedTyped += HandleTurnEnded;
                eventBus.OnActionsChangedTyped += HandleActionsChanged;
                eventBus.OnCombatEndedTyped += HandleCombatEnded;
            }

            if (strideAction != null)
                strideAction.OnStrideStarted += HandleStrideStarted;
            if (entityMover != null)
            {
                entityMover.OnCellReached += HandleCellReached;
                entityMover.OnMovementComplete += HandleMovementComplete;
            }

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
            if (turnManager != null && eventBus != null)
            {
                eventBus.OnTurnStartedTyped -= HandleTurnStarted;
                eventBus.OnTurnEndedTyped -= HandleTurnEnded;
                eventBus.OnActionsChangedTyped -= HandleActionsChanged;
                eventBus.OnCombatEndedTyped -= HandleCombatEnded;
            }
            if (strideAction != null)
                strideAction.OnStrideStarted -= HandleStrideStarted;
            if (entityMover != null)
            {
                entityMover.OnCellReached -= HandleCellReached;
                entityMover.OnMovementComplete -= HandleMovementComplete;
            }
            if (floorController != null)
                floorController.OnGridVisualsToggled -= SetVisualsEnabled;
        }

        // ─── TurnManager event handlers ─────────────────────────────────────

        private void HandleTurnStarted(in TurnStartedEvent e)
        {
            RefreshCombatZone();
        }

        private void HandleTurnEnded(in TurnEndedEvent e)
        {
            if (IsCombatMode)
                ClearZone();
        }

        private void HandleActionsChanged(in ActionsChangedEvent e)
        {
            if (IsCombatMode)
                RefreshCombatZone();
        }

        private void HandleCombatEnded(in CombatEndedEvent e)
        {
            ClearZone();
        }

        // ─── Combat zone refresh ────────────────────────────────────────────

        private void RefreshCombatZone()
        {
            if (!visualsEnabled) { ClearZone(); return; }

            if (turnManager == null || turnManager.State != TurnState.PlayerTurn)
            { ClearZone(); return; }

            var handle = turnManager.CurrentEntity;
            if (!handle.IsValid) { ClearZone(); return; }

            var data = entityManager.Registry.Get(handle);
            if (data == null || data.Team != Team.Player)
            { ClearZone(); return; }

            int remainingActions = Mathf.Clamp(turnManager.ActionsRemaining, 0, 3);
            if (remainingActions <= 0) { ClearZone(); return; }

            ShowZoneFor(handle, remainingActions);
        }

        // ─── Selection-driven handlers (exploration mode) ───────────────────

        private void OnEntitySelected(EntityHandle handle)
        {
            if (IsCombatMode) return;
            if (!visualsEnabled) return;

            var data = entityManager.Registry.Get(handle);
            if (data == null || data.Team != Team.Player)
            {
                ClearZone();
                return;
            }
            ShowZoneFor(handle, 3);
        }

        private void OnEntityDeselected()
        {
            if (IsCombatMode) return;
            ClearZone();
        }

        // ─── Visuals toggle ─────────────────────────────────────────────────

        public void SetVisualsEnabled(bool enabled)
        {
            visualsEnabled = enabled;

            if (!visualsEnabled)
            {
                ClearZone();
                return;
            }

            if (IsCombatMode)
            {
                RefreshCombatZone();
            }
            else
            {
                if (entityManager != null && entityManager.SelectedEntity.IsValid)
                    OnEntitySelected(entityManager.SelectedEntity);
            }
        }

        // ─── Update / Path Preview ──────────────────────────────────────────

        private void Update()
        {
            if (!visualsEnabled || !showingFor.IsValid) return;
            if (movementPlaybackActive) return;
            UpdatePathPreview();
        }

        private void ShowZoneFor(EntityHandle handle, int maxActions)
        {
            ClearZone();

            var data = entityManager.Registry.Get(handle);
            if (data == null || gridManager.Data == null) return;

            showingFor = handle;
            showingMaxActions = Mathf.Clamp(maxActions, 0, 3);

            var profile = new MovementProfile
            {
                moveType = MovementType.Walk,
                speedFeet = data.Speed,
                creatureSizeCells = data.SizeCells,
                ignoresDifficultTerrain = false
            };

            entityManager.Pathfinding.GetMovementZoneByActions(
                gridManager.Data,
                data.GridPosition,
                profile,
                showingMaxActions,
                handle,
                entityManager.Occupancy,
                currentZoneActions);

            float cellSize = gridManager.Config.cellWorldSize;

            foreach (var kvp in currentZoneActions)
            {
                var pos = kvp.Key;
                int actions = kvp.Value;
                if (pos == data.GridPosition) continue;
                if (actions <= 0) continue;

                Color color = actions == 1 ? action1Color :
                              actions == 2 ? action2Color : action3Color;

                var worldPos = gridManager.Data.CellToWorld(pos);
                zoneHighlights.Add(highlightPool.ShowHighlight(worldPos, cellSize, color));
            }
        }

        private void ClearZone()
        {
            foreach (var go in zoneHighlights)
                if (go != null && highlightPool != null) highlightPool.Return(go);
            zoneHighlights.Clear();

            currentZoneActions.Clear();
            showingFor = EntityHandle.None;
            showingMaxActions = 0;

            ClearPathPreview();
            ClearCommittedPath();
        }

        private void UpdatePathPreview()
        {
            if (IsCombatMode && turnManager.State != TurnState.PlayerTurn)
            {
                ClearPathPreview();
                return;
            }

            var hoveredCell = gridManager.HoveredCell;

            if (!hoveredCell.HasValue)
            {
                if (lastHoveredZoneCell.HasValue)
                {
                    HidePathPreviewImmediate();
                    lastHoveredZoneCell = null;
                }
                return;
            }

            // If hovered cell is not in STOP zone, hide and exit immediately (no throttle)
            if (!currentZoneActions.ContainsKey(hoveredCell.Value))
            {
                if (lastHoveredZoneCell != hoveredCell.Value)
                    lastHoveredZoneCell = hoveredCell.Value;

                HidePathPreviewImmediate();
                return;
            }

            // If same cell, do nothing
            if (hoveredCell == lastHoveredZoneCell)
                return;

            // Hover changed -> hide old preview immediately (objects stay reserved)
            lastHoveredZoneCell = hoveredCell.Value;
            HidePathPreviewImmediate();

            // Throttle reconstruction updates
            if (Time.time - lastPathUpdateTime < PathUpdateInterval)
                return;

            // O(path_length) reconstruction from cached zone graph
            pathActionBoundaries.Clear();

            if (!entityManager.Pathfinding.ReconstructPathFromZone(
                hoveredCell.Value, pathBuffer, out int _actionsCost, pathActionBoundaries))
                return;

            float cellSize = gridManager.Config.cellWorldSize;
            int needed = Mathf.Max(0, pathBuffer.Count - 1);

            // boundaries: [0, start2, start3]
            int boundaryPtr = 0;
            int nextBoundary = (pathActionBoundaries.Count > 1) ? pathActionBoundaries[1] : int.MaxValue;

            // 1) Ensure list has enough objects (create only when needed grows)
            for (int i = pathHighlights.Count; i < needed; i++)
            {
                var dummyWorld = gridManager.Data.CellToWorld(pathBuffer[Mathf.Min(1, pathBuffer.Count - 1)]);
                var go = highlightPool.ShowHighlight(dummyWorld, cellSize * 0.6f, pathAction1Color);
                pathHighlights.Add(go);
            }

            // 2) Configure used highlights (reuse existing objects)
            for (int pathIndex = 1; pathIndex < pathBuffer.Count; pathIndex++)
            {
                while (pathIndex >= nextBoundary && boundaryPtr + 1 < pathActionBoundaries.Count)
                {
                    boundaryPtr++;
                    nextBoundary = (boundaryPtr + 1 < pathActionBoundaries.Count)
                        ? pathActionBoundaries[boundaryPtr + 1]
                        : int.MaxValue;
                }

                int actionIndex = boundaryPtr + 1; // 1..3
                if (actionIndex < 1) actionIndex = 1;
                if (actionIndex > 3) actionIndex = 3;

                Color c = actionIndex == 1 ? pathAction1Color :
                          actionIndex == 2 ? pathAction2Color : pathAction3Color;

                int highlightIndex = pathIndex - 1;
                var worldPos = gridManager.Data.CellToWorld(pathBuffer[pathIndex]);

                var go = pathHighlights[highlightIndex];
                if (go == null)
                {
                    go = highlightPool.ShowHighlight(worldPos, cellSize * 0.6f, c);
                    pathHighlights[highlightIndex] = go;
                }
                else
                {
                    highlightPool.ConfigureExistingHighlight(go, worldPos, cellSize * 0.6f, c, active: true);
                }
            }

            // 3) Hide unused highlights (no Return — reserved for reuse)
            for (int i = needed; i < pathHighlights.Count; i++)
            {
                var go = pathHighlights[i];
                if (go != null)
                    highlightPool.HideWithoutReturn(go);
            }

            lastPathUpdateTime = Time.time;
        }

        /// <summary>
        /// Hide path preview objects immediately without returning to pool (reserved for reuse).
        /// </summary>
        private void HidePathPreviewImmediate()
        {
            if (highlightPool == null) return;

            for (int i = 0; i < pathHighlights.Count; i++)
            {
                var go = pathHighlights[i];
                if (go != null)
                    highlightPool.HideWithoutReturn(go);
            }
        }

        /// <summary>
        /// Hard clear: return all path highlights to pool and clear list.
        /// Used when zone is cleared, visuals toggled off, or combat mode changes.
        /// </summary>
        private void ClearPathPreview()
        {
            foreach (var go in pathHighlights)
                if (go != null && highlightPool != null) highlightPool.Return(go);
            pathHighlights.Clear();
        }

        // ─── Committed path (Stride playback) ───────────────────────────────

        private void HandleStrideStarted(
            EntityHandle handle, IReadOnlyList<Vector3Int> path,
            IReadOnlyList<int> boundaries, int actionsCost)
        {
            // Hide hover preview and zone
            HidePathPreviewImmediate();

            // Snapshot
            committedPath.Clear();
            for (int i = 0; i < path.Count; i++) committedPath.Add(path[i]);
            committedBoundaries.Clear();
            for (int i = 0; i < boundaries.Count; i++) committedBoundaries.Add(boundaries[i]);

            committedNextPathIndex = 1; // entity starts at path[0], first arrival is path[1]
            movementPlaybackActive = true;

            RenderCommittedPath();
        }

        private void HandleCellReached(EntityHandle handle, Vector3Int cell)
        {
            if (!movementPlaybackActive) return;

            // Hide the highlight for the cell we just arrived at
            int highlightIdx = committedNextPathIndex - 1; // path index 1 → highlight 0
            if (highlightIdx >= 0 && highlightIdx < committedPathHighlights.Count)
            {
                var go = committedPathHighlights[highlightIdx];
                if (go != null) highlightPool.HideWithoutReturn(go);
            }

            // Hide boundary marker if one existed at this path index
            HideBoundaryMarkerAtIndex(committedNextPathIndex);

            committedNextPathIndex++;
        }

        private void HandleMovementComplete(EntityHandle handle, Vector3Int finalCell)
        {
            if (!movementPlaybackActive) return;
            ClearCommittedPath();
        }

        private void ClearCommittedPath()
        {
            movementPlaybackActive = false;

            foreach (var go in committedPathHighlights)
                if (go != null && highlightPool != null) highlightPool.Return(go);
            committedPathHighlights.Clear();

            foreach (var go in strideBoundaryMarkers)
                if (go != null && highlightPool != null) highlightPool.Return(go);
            strideBoundaryMarkers.Clear();

            committedPath.Clear();
            committedBoundaries.Clear();
        }

        /// <summary>
        /// Render the full committed path with per-action colors and boundary markers.
        /// </summary>
        private void RenderCommittedPath()
        {
            float cellSize = gridManager.Config.cellWorldSize;
            int boundaryPtr = 0;
            int nextBoundary = (committedBoundaries.Count > 1) ? committedBoundaries[1] : int.MaxValue;

            // Path highlights (skip path[0] — entity's start cell)
            for (int i = 1; i < committedPath.Count; i++)
            {
                while (i >= nextBoundary && boundaryPtr + 1 < committedBoundaries.Count)
                {
                    boundaryPtr++;
                    nextBoundary = (boundaryPtr + 1 < committedBoundaries.Count)
                        ? committedBoundaries[boundaryPtr + 1]
                        : int.MaxValue;
                }

                int actionIndex = Mathf.Clamp(boundaryPtr + 1, 1, 3);
                Color c = actionIndex == 1 ? pathAction1Color :
                          actionIndex == 2 ? pathAction2Color : pathAction3Color;

                var worldPos = gridManager.Data.CellToWorld(committedPath[i]);
                committedPathHighlights.Add(highlightPool.ShowHighlight(worldPos, cellSize * 0.6f, c));
            }

            // Boundary markers at Stride 2/3 start cells (raised above path to avoid z-fighting)
            for (int b = 1; b < committedBoundaries.Count; b++)
            {
                int pathIdx = committedBoundaries[b];
                if (pathIdx < 1 || pathIdx >= committedPath.Count) continue;

                int actionIndex = Mathf.Clamp(b + 1, 2, 3);
                Color markerColor = actionIndex == 2 ? pathAction2Color : pathAction3Color;
                markerColor.a = 0.95f;

                var worldPos = gridManager.Data.CellToWorld(committedPath[pathIdx]);
                worldPos.y += boundaryMarkerExtraYOffset;
                float markerSize = cellSize * boundaryMarkerScale;
                var marker = highlightPool.ShowHighlight(worldPos, markerSize, markerColor);
                marker.transform.rotation = Quaternion.Euler(90f, boundaryMarkerYawDegrees, 0f);
                strideBoundaryMarkers.Add(marker);
            }
        }

        /// <summary>
        /// Hide the boundary marker whose path index matches, if any.
        /// </summary>
        private void HideBoundaryMarkerAtIndex(int pathIndex)
        {
            for (int b = 1; b < committedBoundaries.Count; b++)
            {
                if (committedBoundaries[b] == pathIndex)
                {
                    int markerIdx = b - 1; // boundary 1 → marker 0, boundary 2 → marker 1
                    if (markerIdx >= 0 && markerIdx < strideBoundaryMarkers.Count)
                    {
                        var go = strideBoundaryMarkers[markerIdx];
                        if (go != null) highlightPool.HideWithoutReturn(go);
                    }
                    break;
                }
            }
        }
    }
}
