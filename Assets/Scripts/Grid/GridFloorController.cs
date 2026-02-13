using UnityEngine;
using UnityEngine.InputSystem;

namespace PF2e.Grid
{
    /// <summary>
    /// Input controller for floor selection (PageUp/PageDown) and grid visibility toggle (G).
    /// Drives GridRenderer (visual) and GridFloorColliders (physics) via their public APIs.
    /// </summary>
    [RequireComponent(typeof(GridManager))]
    [RequireComponent(typeof(GridRenderer))]
    public class GridFloorController : MonoBehaviour
    {
        private GridManager gridManager;
        private GridRenderer gridRenderer;
        private GridFloorColliders floorColliders;

        private int currentFloor;
        private int maxFloor;
        private bool gridVisible = true;
        private bool initialized;

        private void Start()
        {
            gridManager = GetComponent<GridManager>();
            gridRenderer = GetComponent<GridRenderer>();
            floorColliders = GetComponent<GridFloorColliders>();

            if (gridManager != null)
                gridManager.OnGridChanged += OnGridChanged;

            RecalculateAndApply(forceToTopFloor: true);
        }

        private void OnDestroy()
        {
            if (gridManager != null)
                gridManager.OnGridChanged -= OnGridChanged;
        }

        private void OnGridChanged()
        {
            RecalculateAndApply(forceToTopFloor: !initialized);
        }

        private void RecalculateAndApply(bool forceToTopFloor)
        {
            UpdateMaxFloor();

            if (forceToTopFloor)
            {
                currentFloor = maxFloor;
                initialized = true;
            }
            else
            {
                currentFloor = Mathf.Clamp(currentFloor, 0, maxFloor);
            }

            ApplyState();
        }

        private void UpdateMaxFloor()
        {
            maxFloor = 0;
            if (gridManager == null || gridManager.Data == null) return;

            foreach (var kvp in gridManager.Data.Cells)
                if (kvp.Key.y > maxFloor) maxFloor = kvp.Key.y;
        }

        private void ApplyState()
        {
            if (gridRenderer != null)
                gridRenderer.SetFloorState(currentFloor, gridVisible);

            if (floorColliders != null)
                floorColliders.SetCurrentFloor(currentFloor);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.gKey.wasPressedThisFrame)
            {
                gridVisible = !gridVisible;
                if (gridRenderer != null)
                    gridRenderer.SetFloorState(currentFloor, gridVisible);
            }

            if (kb.pageUpKey.wasPressedThisFrame && currentFloor < maxFloor)
            {
                currentFloor++;
                ApplyState();
            }

            if (kb.pageDownKey.wasPressedThisFrame && currentFloor > 0)
            {
                currentFloor--;
                ApplyState();
            }
        }
    }
}
