using UnityEngine;

namespace PF2e.Grid
{
    public enum VerticalLinkType : byte
    {
        Stairs,
        Ladder,
        Ramp,
        Jumpable
    }

    public struct VerticalLink
    {
        public Vector3Int lower;
        public Vector3Int upper;
        public VerticalLinkType type;
        public int movementCostFeet; // 5 for stairs, 10 for ladder

        public static VerticalLink CreateStairs(Vector3Int lower, Vector3Int upper, int costFeet = 5)
        {
            return new VerticalLink
            {
                lower = lower,
                upper = upper,
                type = VerticalLinkType.Stairs,
                movementCostFeet = costFeet
            };
        }

        public static VerticalLink CreateLadder(Vector3Int lower, Vector3Int upper, int costFeet = 10)
        {
            return new VerticalLink
            {
                lower = lower,
                upper = upper,
                type = VerticalLinkType.Ladder,
                movementCostFeet = costFeet
            };
        }
    }
}
