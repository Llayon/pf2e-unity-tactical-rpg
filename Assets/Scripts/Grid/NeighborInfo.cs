using UnityEngine;

namespace PF2e.Grid
{
    public enum NeighborType : byte
    {
        Cardinal,
        Diagonal,
        Vertical
    }

    public enum MovementType : byte
    {
        Walk,
        Fly,
        Swim,
        Climb
    }

    public readonly struct NeighborInfo
    {
        public readonly Vector3Int pos;
        public readonly NeighborType type;
        public readonly int verticalCostFeet; // 0 for Cardinal/Diagonal, link cost for Vertical

        public NeighborInfo(Vector3Int pos, NeighborType type, int verticalCost = 0)
        {
            this.pos = pos;
            this.type = type;
            this.verticalCostFeet = verticalCost;
        }

        public override string ToString() => $"({pos}, {type}, cost={verticalCostFeet})";
    }
}
