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
            for (int x = 0; x < testGridSizeX; x++)
            {
                for (int z = 0; z < testGridSizeZ; z++)
                {
                    Data.SetCell(new Vector3Int(x, 0, z), CellData.CreateWalkable());
                }
            }

            Debug.Log($"[GridManager] Test grid {testGridSizeX}x{testGridSizeZ} created. Cells: {Data.CellCount}");
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
