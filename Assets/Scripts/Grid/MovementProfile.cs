using System;

namespace PF2e.Grid
{
    /// <summary>
    /// All movement parameters in one struct. Used as cache key in pathfinding.
    /// All fields must participate in Equals/GetHashCode.
    /// </summary>
    public struct MovementProfile : IEquatable<MovementProfile>
    {
        public MovementType moveType;
        public int speedFeet;             // not used by pathfinding cost; used as zone budget (speedFeet * actions)
        public int creatureSizeCells;     // 1=Medium, 2=Large, 3=Huge. Not used yet; reserved for footprint checks
        public bool ignoresDifficultTerrain;

        public static MovementProfile Default => new MovementProfile
        {
            moveType = MovementType.Walk,
            speedFeet = 30,
            creatureSizeCells = 1,
            ignoresDifficultTerrain = false
        };

        public bool Equals(MovementProfile other)
        {
            return moveType == other.moveType
                && speedFeet == other.speedFeet
                && creatureSizeCells == other.creatureSizeCells
                && ignoresDifficultTerrain == other.ignoresDifficultTerrain;
        }

        public override bool Equals(object obj)
        {
            return obj is MovementProfile other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(moveType, speedFeet, creatureSizeCells, ignoresDifficultTerrain);
        }

        public static bool operator ==(MovementProfile left, MovementProfile right) => left.Equals(right);
        public static bool operator !=(MovementProfile left, MovementProfile right) => !left.Equals(right);
    }
}
