namespace PF2e.Core
{
    /// <summary>
    /// Pathfinder 2e rules constants. These are game rules and do not change.
    /// Visual/layout constants live in GridConfig (ScriptableObject).
    /// </summary>
    public static class GameConstants
    {
        public const int CardinalCostFeet = 5;
        public const int DiagonalCostFirstFeet = 5;
        public const int DiagonalCostSecondFeet = 10;
        public const int DifficultTerrainMultiplier = 2;
        public const int GreaterDifficultTerrainMultiplier = 3;
    }
}
