using System.Collections.Generic;
using UnityEngine;

namespace PF2e.Core
{
    /// <summary>
    /// Small deterministic 15 ft cone helper for the Burning Hands slice.
    /// Uses fixed 7-cell patterns across 8 directions instead of a generic template DSL.
    /// </summary>
    public static class BurningHandsConeResolver
    {
        private static readonly Vector3Int[][] DirectionOffsets =
        {
            new[] // East
            {
                new Vector3Int(1, 0, 0),
                new Vector3Int(2, 0, -1), new Vector3Int(2, 0, 0), new Vector3Int(2, 0, 1),
                new Vector3Int(3, 0, -1), new Vector3Int(3, 0, 0), new Vector3Int(3, 0, 1)
            },
            new[] // NorthEast
            {
                new Vector3Int(1, 0, 1),
                new Vector3Int(1, 0, 2), new Vector3Int(2, 0, 1), new Vector3Int(2, 0, 2),
                new Vector3Int(2, 0, 3), new Vector3Int(3, 0, 2), new Vector3Int(3, 0, 3)
            },
            new[] // North
            {
                new Vector3Int(0, 0, 1),
                new Vector3Int(-1, 0, 2), new Vector3Int(0, 0, 2), new Vector3Int(1, 0, 2),
                new Vector3Int(-1, 0, 3), new Vector3Int(0, 0, 3), new Vector3Int(1, 0, 3)
            },
            new[] // NorthWest
            {
                new Vector3Int(-1, 0, 1),
                new Vector3Int(-2, 0, 1), new Vector3Int(-1, 0, 2), new Vector3Int(-2, 0, 2),
                new Vector3Int(-3, 0, 2), new Vector3Int(-2, 0, 3), new Vector3Int(-3, 0, 3)
            },
            new[] // West
            {
                new Vector3Int(-1, 0, 0),
                new Vector3Int(-2, 0, -1), new Vector3Int(-2, 0, 0), new Vector3Int(-2, 0, 1),
                new Vector3Int(-3, 0, -1), new Vector3Int(-3, 0, 0), new Vector3Int(-3, 0, 1)
            },
            new[] // SouthWest
            {
                new Vector3Int(-1, 0, -1),
                new Vector3Int(-1, 0, -2), new Vector3Int(-2, 0, -1), new Vector3Int(-2, 0, -2),
                new Vector3Int(-2, 0, -3), new Vector3Int(-3, 0, -2), new Vector3Int(-3, 0, -3)
            },
            new[] // South
            {
                new Vector3Int(0, 0, -1),
                new Vector3Int(-1, 0, -2), new Vector3Int(0, 0, -2), new Vector3Int(1, 0, -2),
                new Vector3Int(-1, 0, -3), new Vector3Int(0, 0, -3), new Vector3Int(1, 0, -3)
            },
            new[] // SouthEast
            {
                new Vector3Int(1, 0, -1),
                new Vector3Int(1, 0, -2), new Vector3Int(2, 0, -1), new Vector3Int(2, 0, -2),
                new Vector3Int(2, 0, -3), new Vector3Int(3, 0, -2), new Vector3Int(3, 0, -3)
            }
        };

        public static bool TryResolve(
            Vector3Int casterCell,
            Vector3Int aimCell,
            List<Vector3Int> outCells,
            out int directionIndex)
        {
            directionIndex = GetDirectionIndex(casterCell, aimCell);
            if (directionIndex < 0)
                return false;

            outCells?.Clear();
            if (outCells == null)
                return true;

            var offsets = DirectionOffsets[directionIndex];
            for (int i = 0; i < offsets.Length; i++)
                outCells.Add(casterCell + offsets[i]);

            return true;
        }

        public static int GetDirectionIndex(Vector3Int casterCell, Vector3Int aimCell)
        {
            int dx = aimCell.x - casterCell.x;
            int dz = aimCell.z - casterCell.z;
            if (dx == 0 && dz == 0)
                return -1;

            float angle = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
            if (angle < 0f)
                angle += 360f;

            return Mathf.RoundToInt(angle / 45f) % 8;
        }
    }
}
