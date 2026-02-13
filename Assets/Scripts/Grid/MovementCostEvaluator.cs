using System;
using PF2e.Core;

namespace PF2e.Grid
{
    /// <summary>
    /// Single source of truth for step cost calculation.
    /// Both A* and Dijkstra call this — one place for PF2e rules.
    /// </summary>
    public static class MovementCostEvaluator
    {
        public static int GetStepCost(
            CellData targetCell, NeighborInfo neighbor,
            bool diagonalParity, MovementProfile profile)
        {
            int baseCost = neighbor.type switch
            {
                NeighborType.Cardinal => GameConstants.CardinalCostFeet,
                NeighborType.Diagonal => diagonalParity
                    ? GameConstants.DiagonalCostSecondFeet
                    : GameConstants.DiagonalCostFirstFeet,
                NeighborType.Vertical => neighbor.verticalCostFeet,
                _ => throw new ArgumentOutOfRangeException(nameof(neighbor))
            };

            // PF2e rule: terrain multiplier applies to ALL entry types including vertical.
            // Example: stairs into difficult terrain = link cost × 2. Confirmed by GT-037.
            int terrainMult = targetCell.terrain switch
            {
                CellTerrain.Difficult when !profile.ignoresDifficultTerrain
                    => GameConstants.DifficultTerrainMultiplier,
                CellTerrain.GreaterDifficult when !profile.ignoresDifficultTerrain
                    => GameConstants.GreaterDifficultTerrainMultiplier,
                _ => 1,
            };

            return baseCost * terrainMult;
        }
    }
}
