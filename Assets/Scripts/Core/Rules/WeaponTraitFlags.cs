using System;

namespace PF2e.Core
{
    [Flags]
    public enum WeaponTraitFlags : ushort
    {
        None    = 0,
        Agile   = 1 << 0, // MAP -4/-8
        Finesse = 1 << 1, // can use Dex for attack (MVP: choose best)
        Reach   = 1 << 2,        Trip    = 1 << 3,
        Shove   = 1 << 4,
        Grapple = 1 << 5,

    }
}
