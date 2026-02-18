using UnityEngine;

namespace PF2e.Core
{
    /// <summary>
    /// Unity's Random-based RNG implementation.
    /// </summary>
    public sealed class UnityRng : IRng
    {
        public static readonly UnityRng Shared = new UnityRng();

        private UnityRng() { }

        public int RollD20() => Random.Range(1, 21);
        public int RollDie(int sides) => Random.Range(1, sides + 1);
    }
}
