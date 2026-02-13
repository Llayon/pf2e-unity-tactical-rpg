using UnityEngine;
using UnityEngine.InputSystem;
using PF2e.Data;

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
        }

        private void Update()
        {
            if (gridData == null || mainCamera == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            // Raycast from mouse
            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0));

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, gridLayerMask))
            {
                var cell = ResolveCell(hit);

                if (gridData.HasCell(cell))
                {
                    // Hover
                    gridManager.SetHoveredCell(cell);
                    UpdateHoverHighlight(cell);

                    // Click
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
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
    }
}
