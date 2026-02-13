using System;
using System.Collections.Generic;
using UnityEngine;
using PF2e.Data;

namespace PF2e.Grid
{
    /// <summary>
    /// MonoBehaviour facade for the grid system.
    /// Owns GridData, raises events, provides public API.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GridConfig gridConfig;

        [Header("Debug")]
        [SerializeField] private bool createTestGrid = true;
        [SerializeField] private int testGridSizeX = 10;
        [SerializeField] private int testGridSizeZ = 10;

        // --- Public API ---
        public GridData Data { get; private set; }
        public GridConfig Config => gridConfig;

        // --- Events ---
        public event Action<Vector3Int> OnCellHovered;
        public event Action OnCellUnhovered;
        public event Action<Vector3Int> OnCellClicked;
        public event Action<Vector3Int> OnCellSelected;
        public event Action OnGridChanged;

        // --- Internal state ---
        private Vector3Int? hoveredCell;
        private Vector3Int? selectedCell;

        public Vector3Int? HoveredCell => hoveredCell;
        public Vector3Int? SelectedCell => selectedCell;

        private void Awake()
        {
            if (gridConfig == null)
            {
                Debug.LogError("[GridManager] GridConfig is not assigned!", this);
                return;
            }

            Data = new GridData(gridConfig);

            if (createTestGrid)
            {
                CreateTestGrid();
            }
        }

        private void CreateTestGrid()
        {
            // Floor 0: 8×8 grid
            for (int x = 0; x < 8; x++)
            {
                for (int z = 0; z < 8; z++)
                {
                    Data.SetCell(new Vector3Int(x, 0, z), CellData.CreateWalkable());
                }
            }

            // Hole at (3,0,3) = Impassable
            Data.SetCell(new Vector3Int(3, 0, 3), CellData.CreateImpassable());

            // Floor 1: 4×4 platform (x=2..5, z=2..5)
            for (int x = 2; x <= 5; x++)
            {
                for (int z = 2; z <= 5; z++)
                {
                    Data.SetCell(new Vector3Int(x, 1, z), CellData.CreateWalkable());
                }
            }

            // Stairs connecting floor 0 to floor 1
            Data.AddVerticalLink(VerticalLink.CreateStairs(
                new Vector3Int(4, 0, 4), new Vector3Int(4, 1, 4)));

            Debug.Log($"[GridManager] Multi-height test grid created. Cells: {Data.CellCount}");
            RaiseGridChanged();
        }

        // --- Public methods for external systems ---

        public void SetHoveredCell(Vector3Int? cell)
        {
            if (hoveredCell == cell) return;
            hoveredCell = cell;
            if (cell.HasValue)
                OnCellHovered?.Invoke(cell.Value);
            else
                OnCellUnhovered?.Invoke();
        }

        public void SetClickedCell(Vector3Int cell)
        {
            OnCellClicked?.Invoke(cell);
        }

        public void SetSelectedCell(Vector3Int? cell)
        {
            if (selectedCell == cell) return;
            selectedCell = cell;
            if (cell.HasValue)
                OnCellSelected?.Invoke(cell.Value);
        }

        /// <summary>
        /// Call after modifying GridData externally.
        /// Notifies renderer and other listeners.
        /// </summary>
        public void RaiseGridChanged()
        {
            OnGridChanged?.Invoke();
        }
    }
}
