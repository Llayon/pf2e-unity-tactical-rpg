using UnityEngine;
using PF2e.Core;

namespace PF2e.TurnSystem
{
    public enum AISkillActionKind
    {
        None = 0,
        Demoralize = 1,
        Trip = 2,
        Grapple = 3,
        Shove = 4,
        Reposition = 5
    }

    public readonly struct AISkillDecision
    {
        public readonly AISkillActionKind actionKind;
        public readonly EntityHandle primaryTarget;
        public readonly bool hasDestinationCell;
        public readonly Vector3Int destinationCell;

        public AISkillDecision(AISkillActionKind actionKind, EntityHandle primaryTarget)
        {
            this.actionKind = actionKind;
            this.primaryTarget = primaryTarget;
            hasDestinationCell = false;
            destinationCell = default;
        }

        public AISkillDecision(AISkillActionKind actionKind, EntityHandle primaryTarget, Vector3Int destinationCell)
        {
            this.actionKind = actionKind;
            this.primaryTarget = primaryTarget;
            hasDestinationCell = true;
            this.destinationCell = destinationCell;
        }

        public bool IsValid => actionKind != AISkillActionKind.None && primaryTarget.IsValid;
        public bool HasDestinationCell => hasDestinationCell;

        public static AISkillDecision Demoralize(EntityHandle target)
        {
            return new AISkillDecision(AISkillActionKind.Demoralize, target);
        }

        public static AISkillDecision Trip(EntityHandle target)
        {
            return new AISkillDecision(AISkillActionKind.Trip, target);
        }

        public static AISkillDecision Grapple(EntityHandle target)
        {
            return new AISkillDecision(AISkillActionKind.Grapple, target);
        }

        public static AISkillDecision Shove(EntityHandle target)
        {
            return new AISkillDecision(AISkillActionKind.Shove, target);
        }

        public static AISkillDecision Reposition(EntityHandle target, Vector3Int destinationCell)
        {
            return new AISkillDecision(AISkillActionKind.Reposition, target, destinationCell);
        }
    }
}
