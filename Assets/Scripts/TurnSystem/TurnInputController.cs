using UnityEngine;
using UnityEngine.InputSystem;
using PF2e.Grid;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Thin wiring between input and combat actions.
    /// - Space → EndTurn (PlayerTurn only, not while Stride in progress)
    /// - Left click → StrideAction.TryStride(hoveredCell) (if CanStride)
    ///
    /// Pure polling in Update — no event subscriptions, no OnDestroy needed.
    ///
    /// Note: relies on GridInteraction.Update() having set GridManager.HoveredCell
    /// before this runs. If hover lags by one frame on fast clicks, set Script
    /// Execution Order so GridInteraction runs before TurnInputController.
    /// </summary>
    public class TurnInputController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private StrideAction strideAction;
        [SerializeField] private GridManager gridManager;

        private void Update()
        {
            if (turnManager == null || strideAction == null || gridManager == null) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // Space → EndTurn (only during PlayerTurn and not while a Stride is animating)
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                if (turnManager.IsPlayerTurn && !strideAction.StrideInProgress)
                    turnManager.EndTurn();
            }

            // Left click → Stride to hovered cell (only if CanStride passes all checks)
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (strideAction.CanStride())
                {
                    var hoveredCell = gridManager.HoveredCell;
                    if (hoveredCell.HasValue)
                    {
                        // If TryStride returns false (out of range, blocked, etc.) — silently ignore.
                        // GridInteraction handles entity selection on the same click independently.
                        strideAction.TryStride(hoveredCell.Value);
                    }
                }
            }
        }
    }
}
