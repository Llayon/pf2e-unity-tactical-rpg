using UnityEngine;

namespace PF2e.Core
{
    public readonly struct JumpPreviewResult
    {
        public readonly bool isValid;
        public readonly JumpType jumpType;
        public readonly int actionCost;
        public readonly bool requiresCheck;
        public readonly SkillType checkSkill;
        public readonly int dc;
        public readonly bool requiresRunUp;
        public readonly int runUpFeet;
        public readonly int jumpDistanceFeet;
        public readonly Vector3Int takeoffCell;
        public readonly Vector3Int landingCell;
        public readonly JumpFailureReason failureReason;

        private JumpPreviewResult(
            bool isValid,
            JumpType jumpType,
            int actionCost,
            bool requiresCheck,
            SkillType checkSkill,
            int dc,
            bool requiresRunUp,
            int runUpFeet,
            int jumpDistanceFeet,
            Vector3Int takeoffCell,
            Vector3Int landingCell,
            JumpFailureReason failureReason)
        {
            this.isValid = isValid;
            this.jumpType = jumpType;
            this.actionCost = actionCost;
            this.requiresCheck = requiresCheck;
            this.checkSkill = checkSkill;
            this.dc = dc;
            this.requiresRunUp = requiresRunUp;
            this.runUpFeet = runUpFeet;
            this.jumpDistanceFeet = jumpDistanceFeet;
            this.takeoffCell = takeoffCell;
            this.landingCell = landingCell;
            this.failureReason = failureReason;
        }

        public static JumpPreviewResult Valid(
            JumpType jumpType,
            int actionCost,
            bool requiresCheck,
            int dc,
            bool requiresRunUp,
            int runUpFeet,
            int jumpDistanceFeet,
            Vector3Int takeoffCell,
            Vector3Int landingCell)
        {
            return new JumpPreviewResult(
                isValid: true,
                jumpType: jumpType,
                actionCost: actionCost,
                requiresCheck: requiresCheck,
                checkSkill: SkillType.Athletics,
                dc: dc,
                requiresRunUp: requiresRunUp,
                runUpFeet: runUpFeet,
                jumpDistanceFeet: jumpDistanceFeet,
                takeoffCell: takeoffCell,
                landingCell: landingCell,
                failureReason: JumpFailureReason.None);
        }

        public static JumpPreviewResult Invalid(JumpFailureReason reason, Vector3Int landingCell)
        {
            return new JumpPreviewResult(
                isValid: false,
                jumpType: JumpType.Leap,
                actionCost: 0,
                requiresCheck: false,
                checkSkill: SkillType.Athletics,
                dc: 0,
                requiresRunUp: false,
                runUpFeet: 0,
                jumpDistanceFeet: 0,
                takeoffCell: landingCell,
                landingCell: landingCell,
                failureReason: reason);
        }
    }
}
