using UnityEngine;
using UnityEngine.InputSystem;
using PF2e.Data;
using PF2e.Managers;
using PF2e.Presentation.Entity;

namespace PF2e.Grid
{
    /// <summary>
    /// Handles mouse interaction with the grid.
    /// Physics.Raycast → FloorLevel → snap to cell → hover/click events.
    /// </summary>
    [RequireComponent(typeof(GridManager))]
    public class GridInteraction : MonoBehaviour
    {
        [SerializeField] private LayerMask gridLayerMask = ~0;
        [SerializeField] private float maxRayDistance = 100f;

        private GridManager gridManager;
        private GridData gridData;
        private CellHighlightPool highlightPool;

        private GameObject hoverHighlight;
        private GameObject selectionHighlight;

        private Vector3Int? lastHoverCell;
        private Vector3Int? lastSelectedCell;

        private UnityEngine.Camera mainCamera;
        private EntityManager entityManager;

        private void OnEnable()
        {
            gridManager = GetComponent<GridManager>();
            highlightPool = GetComponent<CellHighlightPool>();
            if (gridManager == null) return;
        }

        private void Start()
        {
            if (gridManager == null || gridManager.Data == null) return;
            gridData = gridManager.Data;
            mainCamera = UnityEngine.Camera.main;
            entityManager = FindObjectOfType<EntityManager>();
        }

        private void Update()
        {
            if (gridData == null || mainCamera == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0));

            var hits = Physics.RaycastAll(ray, maxRayDistance, gridLayerMask);
            if (hits.Length == 0)
            {
                ClearHover();
                return;
            }

            // RaycastAll does NOT guarantee order by distance
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit? floorHit = null;
            EntityView entityView = null;

            for (int i = 0; i < hits.Length; i++)
            {
                if (!floorHit.HasValue)
                {
                    var floor = hits[i].collider.GetComponent<FloorLevel>();
                    if (floor != null)
                        floorHit = hits[i];
                }

                if (entityView == null)
                {
                    // GetComponentInParent for future-proofing (child colliders)
                    var ev = hits[i].collider.GetComponentInParent<EntityView>();
                    if (ev != null)
                        entityView = ev;
                }

                if (floorHit.HasValue && entityView != null)
                    break;
            }

            if (!floorHit.HasValue)
            {
                ClearHover();
                return;
            }

            var cell = ResolveCell(floorHit.Value);

            if (gridData.HasCell(cell))
            {
                gridManager.SetHoveredCell(cell);
                UpdateHoverHighlight(cell);

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (entityManager != null)
                    {
                        if (entityView != null)
                            entityManager.SelectEntity(entityView.Handle);
                        else
                            entityManager.DeselectEntity();
                    }

                    gridManager.SetClickedCell(cell);
                    gridManager.SetSelectedCell(cell);
                    UpdateSelectionHighlight(cell);
                }
            }
            else
            {
                ClearHover();
            }
        }

        /// <summary>
        /// Resolve cell from raycast hit. Uses FloorLevel for elevation, hit.point for X/Z.
        /// </summary>
        private Vector3Int ResolveCell(RaycastHit hit)
        {
            var floor = hit.collider.GetComponent<FloorLevel>();
            int elevation = floor != null ? floor.elevation : 0;

            int x = Mathf.FloorToInt(hit.point.x / gridData.CellWorldSize);
            int z = Mathf.FloorToInt(hit.point.z / gridData.CellWorldSize);

            return new Vector3Int(x, elevation, z);
        }

        private void UpdateHoverHighlight(Vector3Int cell)
        {
            if (highlightPool == null) return;
            if (lastHoverCell.HasValue && lastHoverCell.Value == cell) return;

            if (hoverHighlight != null)
                highlightPool.Return(hoverHighlight);

            var worldPos = gridData.CellToWorld(cell);
            var config = gridManager.Config;
            hoverHighlight = highlightPool.ShowHighlight(worldPos, config.cellWorldSize, config.hoverColor);
            lastHoverCell = cell;
        }

        private void UpdateSelectionHighlight(Vector3Int cell)
        {
            if (highlightPool == null) return;
            if (lastSelectedCell.HasValue && lastSelectedCell.Value == cell) return;

            if (selectionHighlight != null)
                highlightPool.Return(selectionHighlight);

            var worldPos = gridData.CellToWorld(cell);
            var config = gridManager.Config;
            selectionHighlight = highlightPool.ShowHighlight(worldPos, config.cellWorldSize, config.selectedColor);
            lastSelectedCell = cell;
        }

        private void ClearHover()
        {
            gridManager.SetHoveredCell(null);
            lastHoverCell = null;
            if (highlightPool != null && hoverHighlight != null)
            {
                highlightPool.Return(hoverHighlight);
                hoverHighlight = null;
            }
        }

        /// <summary>
        /// Clears all visual highlights (hover + selection).
        /// Called when grid visibility is toggled off to avoid confusion.
        /// </summary>
        public void ClearHighlights()
        {
            ClearHover();
            lastSelectedCell = null;

            if (highlightPool != null)
            {
                if (selectionHighlight != null)
                {
                    highlightPool.Return(selectionHighlight);
                    selectionHighlight = null;
                }

                highlightPool.ClearAll();
            }
        }
    }
}
