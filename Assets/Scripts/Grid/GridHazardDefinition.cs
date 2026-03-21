using PF2e.Core;
using UnityEngine;

namespace PF2e.Grid
{
    [System.Serializable]
    public struct GridHazardDefinition
    {
        public string displayName;
        public Vector3Int cell;
        public HazardEffectKind effectKind;
        public int entryDamage;
        public int persistentDamage;
        public int forcedMoveCells;
        public int forcedMoveElevationPerCell;
        public DamageType damageType;
        public SaveType saveType;
        public int saveDc;
        public int aiPressure;
        public Color telegraphColor;

        public GridHazardDefinition(
            string displayName,
            Vector3Int cell,
            HazardEffectKind effectKind,
            int entryDamage,
            int persistentDamage,
            int forcedMoveCells,
            DamageType damageType,
            SaveType saveType,
            int saveDc,
            int aiPressure,
            Color telegraphColor,
            int forcedMoveElevationPerCell = 0)
        {
            this.displayName = displayName;
            this.cell = cell;
            this.effectKind = effectKind;
            this.entryDamage = entryDamage;
            this.persistentDamage = persistentDamage;
            this.forcedMoveCells = forcedMoveCells;
            this.forcedMoveElevationPerCell = forcedMoveElevationPerCell;
            this.damageType = damageType;
            this.saveType = saveType;
            this.saveDc = saveDc;
            this.aiPressure = aiPressure;
            this.telegraphColor = telegraphColor;
        }

        public GridHazardDefinition(
            string displayName,
            Vector3Int cell,
            HazardEffectKind effectKind,
            int entryDamage,
            DamageType damageType,
            SaveType saveType,
            int saveDc,
            int aiPressure,
            Color telegraphColor)
            : this(
                displayName,
                cell,
                effectKind,
                entryDamage,
                0,
                0,
                damageType,
                saveType,
                saveDc,
                aiPressure,
                telegraphColor)
        {
        }

        public GridHazardDefinition(
            string displayName,
            Vector3Int cell,
            HazardEffectKind effectKind,
            int entryDamage,
            int persistentDamage,
            DamageType damageType,
            SaveType saveType,
            int saveDc,
            int aiPressure,
            Color telegraphColor)
            : this(
                displayName,
                cell,
                effectKind,
                entryDamage,
                persistentDamage,
                0,
                damageType,
                saveType,
                saveDc,
                aiPressure,
                telegraphColor)
        {
        }

        public GridHazardDefinition(
            string displayName,
            Vector3Int cell,
            int entryDamage,
            DamageType damageType,
            int aiPressure,
            Color telegraphColor)
            : this(
                displayName,
                cell,
                HazardEffectKind.FlatDamage,
                entryDamage,
                0,
                0,
                damageType,
                SaveType.Reflex,
                15,
                aiPressure,
                telegraphColor)
        {
        }
    }
}
