using PF2e.Core;
using UnityEngine;

namespace PF2e.Grid
{
    [System.Serializable]
    public struct GridHazardDefinition
    {
        public string displayName;
        public Vector3Int cell;
        public int entryDamage;
        public DamageType damageType;
        public int aiPressure;
        public Color telegraphColor;

        public GridHazardDefinition(
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
    }
}
