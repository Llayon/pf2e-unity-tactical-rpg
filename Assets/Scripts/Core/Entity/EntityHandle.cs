using System;

namespace PF2e.Core
{
    /// <summary>
    /// Lightweight value-type identifier for entities.
    /// None = Id 0. Valid ids start from 1.
    /// </summary>
    public readonly struct EntityHandle : IEquatable<EntityHandle>
    {
        public static readonly EntityHandle None = new EntityHandle(0);

        public readonly int Id;

        public EntityHandle(int id) => Id = id;

        public bool IsValid => Id > 0;

        public bool Equals(EntityHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is EntityHandle h && Equals(h);
        public override int GetHashCode() => Id;
        public override string ToString() => $"Entity#{Id}";

        public static bool operator ==(EntityHandle a, EntityHandle b) => a.Id == b.Id;
        public static bool operator !=(EntityHandle a, EntityHandle b) => a.Id != b.Id;
    }
}
