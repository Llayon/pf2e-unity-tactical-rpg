using UnityEngine;
using PF2e.TurnSystem;

namespace PF2e.Core
{
    public readonly struct SpellAreaPreview
    {
        public readonly SpellId spellId;
        public readonly Vector3Int aimCell;
        public readonly TargetingFailureReason failureReason;
        public readonly TargetingWarningReason warningReason;
        public readonly int directionIndex;
        public readonly Vector3Int[] areaCells;
        public readonly EntityHandle[] targets;
        public readonly int allyCount;
        public readonly int enemyCount;

        public bool IsValid => failureReason == TargetingFailureReason.None;
        public bool HasWarning => warningReason != TargetingWarningReason.None;
        public int TargetCount => targets != null ? targets.Length : 0;

        public SpellAreaPreview(
            SpellId spellId,
            Vector3Int aimCell,
            TargetingFailureReason failureReason,
            TargetingWarningReason warningReason,
            int directionIndex,
            Vector3Int[] areaCells,
            EntityHandle[] targets,
            int allyCount,
            int enemyCount)
        {
            this.spellId = spellId;
            this.aimCell = aimCell;
            this.failureReason = failureReason;
            this.warningReason = warningReason;
            this.directionIndex = directionIndex;
            this.areaCells = areaCells;
            this.targets = targets;
            this.allyCount = allyCount;
            this.enemyCount = enemyCount;
        }

        public static SpellAreaPreview Invalid(
            SpellId spellId,
            Vector3Int aimCell,
            TargetingFailureReason failureReason)
        {
            return new SpellAreaPreview(
                spellId,
                aimCell,
                failureReason,
                TargetingWarningReason.None,
                directionIndex: -1,
                areaCells: System.Array.Empty<Vector3Int>(),
                targets: System.Array.Empty<EntityHandle>(),
                allyCount: 0,
                enemyCount: 0);
        }
    }
}
