using UnityEngine;

namespace PF2e.Core
{
    public static class GridDistancePF2e
    {
        /// <summary>
        /// PF2e diagonal distance on a square grid: diagonals cost 5/10/5/10...
        /// This matches the pathfinding heuristic with parity=false (first diagonal = 5).
        /// Only XZ considered; Y ignored here (reach checks usually require same elevation).
        /// </summary>
        public static int DistanceFeetXZ(Vector3Int a, Vector3Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dz = Mathf.Abs(a.z - b.z);
            int diags = Mathf.Min(dx, dz);
            int straight = Mathf.Abs(dx - dz);

            // parity=false: 5,10,5,10...
            int diagCost = ((diags + 1) / 2) * 5 + (diags / 2) * 10;

            return diagCost + straight * 5;
        }
    }
}
