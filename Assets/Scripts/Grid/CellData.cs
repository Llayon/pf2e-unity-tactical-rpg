using System;

namespace PF2e.Grid
{
    public enum CellTerrain : byte
    {
        Normal,             // ×1 cost
        Difficult,          // ×2 cost
        GreaterDifficult,   // ×3 cost
        Hazardous,          // damage on entry
        Impassable          // cannot enter
    }

    [Flags]
    public enum CellFlags : ushort
    {
        None         = 0,
        Walkable     = 1 << 0,
        Flyable      = 1 << 1,
        Swimmable    = 1 << 2,
        HasLadderUp  = 1 << 3,
        HasLadderDown = 1 << 4,
        HasStairsUp  = 1 << 5,
        HasStairsDown = 1 << 6,
    }

    public struct CellData
    {
        public CellTerrain terrain;
        public CellFlags flags;
        public byte coverValue; // 0 = none, 1 = lesser, 2 = standard, 3 = greater

        public bool IsWalkable => (flags & CellFlags.Walkable) != 0 && terrain != CellTerrain.Impassable;

        /// <summary>
        /// True if a flying creature can enter this cell.
        /// Note: Impassable+Flyable means ground-blocked but flyable (pit, lava).
        /// Use CreateBlocked() for cells that block ALL movement (walls, pillars).
        /// </summary>
        public bool IsFlyable => (flags & CellFlags.Flyable) != 0;

        public static CellData CreateWalkable(CellTerrain terrain = CellTerrain.Normal)
        {
            return new CellData
            {
                terrain = terrain,
                flags = CellFlags.Walkable | CellFlags.Flyable,
                coverValue = 0
            };
        }

        /// <summary>
        /// Ground-impassable cell that flying creatures CAN enter (pit, lava, chasm).
        /// For fully blocked cells (wall, pillar), use CreateBlocked().
        /// </summary>
        public static CellData CreateImpassable()
        {
            return new CellData
            {
                terrain = CellTerrain.Impassable,
                flags = CellFlags.Flyable,
                coverValue = 0
            };
        }

        /// <summary>
        /// Fully blocked cell (wall, pillar). Cannot walk or fly through.
        /// </summary>
        public static CellData CreateBlocked()
        {
            return new CellData
            {
                terrain = CellTerrain.Impassable,
                flags = CellFlags.None,
                coverValue = 0
            };
        }
    }
}
