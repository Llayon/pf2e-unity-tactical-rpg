using PF2e.Core;
using UnityEngine;

namespace PF2e.Grid
{
    public readonly struct GridHazardInfo
    {
        public readonly string displayName;
        public readonly Vector3Int cell;
        public readonly int entryDamage;
        public readonly DamageType damageType;
        public readonly int aiPressure;
        public readonly Color telegraphColor;

        public GridHazardInfo(
            string displayName,
            Vector3Int cell,
            int entryDamage,
            DamageType damageType,
            int aiPressure,
            Color telegraphColor)
        {
            this.displayName = displayName;
            this.cell = cell;
            this.entryDamage = entryDamage;
            this.damageType = damageType;
            this.aiPressure = aiPressure;
            this.telegraphColor = telegraphColor;
        }

        public bool IsValid => entryDamage > 0 || aiPressure > 0;
    }
}
