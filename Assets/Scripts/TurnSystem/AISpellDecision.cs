using PF2e.Core;
using UnityEngine;

namespace PF2e.TurnSystem
{
    public readonly struct AISpellDecision
    {
        public readonly SpellId spellId;
        public readonly int actionCount;
        public readonly EntityHandle primaryTarget;
        public readonly EntityHandle secondaryTarget;
        public readonly Vector3Int aimCell;
        public readonly bool hasAimCell;

        public AISpellDecision(
            SpellId spellId,
            int actionCount,
            EntityHandle primaryTarget,
            EntityHandle secondaryTarget = default,
            Vector3Int aimCell = default,
            bool hasAimCell = false)
        {
            this.spellId = spellId;
            this.actionCount = actionCount;
            this.primaryTarget = primaryTarget;
            this.secondaryTarget = secondaryTarget;
            this.aimCell = aimCell;
            this.hasAimCell = hasAimCell;
        }

        public bool HasSecondaryTarget => secondaryTarget.IsValid;
        public bool HasAimCell => hasAimCell;

        public static AISpellDecision SingleTarget(SpellId spellId, int actionCount, EntityHandle target)
        {
            return new AISpellDecision(spellId, actionCount, target);
        }

        public static AISpellDecision ChainTwo(SpellId spellId, int actionCount, EntityHandle primaryTarget, EntityHandle secondaryTarget)
        {
            return new AISpellDecision(spellId, actionCount, primaryTarget, secondaryTarget);
        }

        public static AISpellDecision MultiShard(SpellId spellId, int actionCount, EntityHandle target)
        {
            return new AISpellDecision(spellId, actionCount, target);
        }

        public static AISpellDecision AreaAimCell(SpellId spellId, int actionCount, EntityHandle primaryTarget, Vector3Int aimCell)
        {
            return new AISpellDecision(
                spellId,
                actionCount,
                primaryTarget,
                aimCell: aimCell,
                hasAimCell: true);
        }
    }
}
