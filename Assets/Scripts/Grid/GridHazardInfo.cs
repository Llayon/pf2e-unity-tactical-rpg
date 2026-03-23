using PF2e.Core;
using UnityEngine;

namespace PF2e.Grid
{
    public readonly struct GridHazardInfo
    {
        public readonly string displayName;
        public readonly Vector3Int cell;
        public readonly HazardEffectKind effectKind;
        public readonly int entryDamage;
        public readonly int persistentDamage;
        public readonly int forcedMoveCells;
        public readonly int forcedMoveElevationPerCell;
        public readonly DamageType damageType;
        public readonly SaveType saveType;
        public readonly int saveDc;
        public readonly int aiPressure;
        public readonly Color telegraphColor;

        public GridHazardInfo(
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

        public bool IsValid => HasEffect || aiPressure > 0;

        public bool HasEffect => effectKind switch
        {
            HazardEffectKind.FlatDamage => entryDamage > 0,
            HazardEffectKind.BasicSaveDamage => entryDamage > 0 && saveDc > 0,
            HazardEffectKind.ProneOnEntry => true,
            HazardEffectKind.DamageAndProneOnFailure => entryDamage > 0 && saveDc > 0,
            HazardEffectKind.PersistentFireOnEntry => entryDamage > 0,
            HazardEffectKind.PersistentFireOnFailedSave => entryDamage > 0 && saveDc > 0,
            HazardEffectKind.BasicSaveDamageAndPersistentFireOnFailure => entryDamage > 0 && saveDc > 0,
            HazardEffectKind.ProneAndPersistentFireOnFailedSave => persistentDamage > 0 && saveDc > 0,
            HazardEffectKind.PushOnFailedSave => forcedMoveCells > 0 && saveDc > 0,
            HazardEffectKind.BasicSaveDamageAndPushOnFailedSave => entryDamage > 0 && forcedMoveCells > 0 && saveDc > 0,
            HazardEffectKind.ProneAndPushAndPersistentFireOnFailedSave => persistentDamage > 0 && forcedMoveCells > 0 && saveDc > 0,
            HazardEffectKind.PullOnFailedSave => forcedMoveCells > 0 && saveDc > 0,
            HazardEffectKind.BasicSaveDamageAndPullOnFailedSave => entryDamage > 0 && forcedMoveCells > 0 && saveDc > 0,
            HazardEffectKind.ProneAndPullOnFailedSave => forcedMoveCells > 0 && saveDc > 0,
            HazardEffectKind.PullAndPersistentFireOnFailedSave => persistentDamage > 0 && forcedMoveCells > 0 && saveDc > 0,
            HazardEffectKind.ProneAndPullAndPersistentFireOnFailedSave => persistentDamage > 0 && forcedMoveCells > 0 && saveDc > 0,
            HazardEffectKind.PersistentAcidOnFailedSave => persistentDamage > 0 && saveDc > 0,
            HazardEffectKind.BasicSaveDamageAndPersistentAcidOnFailure => entryDamage > 0 && persistentDamage > 0 && saveDc > 0,
            HazardEffectKind.ProneAndPersistentAcidOnFailedSave => persistentDamage > 0 && saveDc > 0,
            HazardEffectKind.BasicSaveDamageAndProneAndPushOnFailedSave => entryDamage > 0 && forcedMoveCells > 0 && saveDc > 0,
            HazardEffectKind.BasicSaveDamageAndProneAndPullOnFailedSave => entryDamage > 0 && forcedMoveCells > 0 && saveDc > 0,
            HazardEffectKind.PullAndPersistentAcidOnFailedSave => persistentDamage > 0 && forcedMoveCells > 0 && saveDc > 0,
            HazardEffectKind.PushAndPersistentAcidOnFailedSave => persistentDamage > 0 && forcedMoveCells > 0 && saveDc > 0,
            _ => false
        };
    }
}
