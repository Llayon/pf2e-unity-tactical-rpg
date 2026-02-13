using System;
using UnityEngine;

namespace PF2e.Grid
{
    public enum EdgeType : byte
    {
        None,
        Wall,
        Door,
        Window,
        ArrowSlit
    }

    public struct EdgeData
    {
        public EdgeType type;
        public bool blocksMovement;
        public bool blocksLineOfSight;

        public static EdgeData CreateWall()
        {
            return new EdgeData
            {
                type = EdgeType.Wall,
                blocksMovement = true,
                blocksLineOfSight = true
            };
        }

        public static EdgeData CreateDoor(bool open = false)
        {
            return new EdgeData
            {
                type = EdgeType.Door,
                blocksMovement = !open,
                blocksLineOfSight = !open
            };
        }
    }

    /// <summary>
    /// Normalized edge key between two adjacent cells.
    /// Constructor ensures CellA &lt; CellB lexicographically, so
    /// EdgeKey(a,b) == EdgeKey(b,a) always.
    /// </summary>
    public readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        public readonly Vector3Int CellA;
        public readonly Vector3Int CellB;

        public EdgeKey(Vector3Int a, Vector3Int b)
        {
            int cmp = a.x != b.x ? a.x.CompareTo(b.x)
                    : a.y != b.y ? a.y.CompareTo(b.y)
                    : a.z.CompareTo(b.z);

            if (cmp <= 0)
            {
                CellA = a;
                CellB = b;
            }
            else
            {
                CellA = b;
                CellB = a;
            }
        }

        public bool Equals(EdgeKey other)
        {
            return CellA == other.CellA && CellB == other.CellB;
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CellA, CellB);
        }

        public static bool operator ==(EdgeKey left, EdgeKey right) => left.Equals(right);
        public static bool operator !=(EdgeKey left, EdgeKey right) => !left.Equals(right);

        public override string ToString() => $"Edge({CellA} <-> {CellB})";
    }
}
