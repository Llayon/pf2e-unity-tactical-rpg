using UnityEngine;

namespace PF2e.Core
{
    public static class JumpRules
    {
        public const int LeapActionCost = 1;
        public const int LongJumpActionCost = 2;
        public const int HighJumpActionCost = 2;

        public const int HighJumpDc = 30;
        public const int MinimumRunUpFeet = 10;
        public const int VerticalLeapHeightFeet = 3;
        public const int VerticalLeapHorizontalFeet = 5;

        private const int MinimumHorizontalLeapSpeedFeet = 15;

        public static int GetLeapRangeFeet(int speedFeet)
        {
            if (speedFeet < MinimumHorizontalLeapSpeedFeet)
                return 0;

            return speedFeet >= 30 ? 15 : 10;
        }

        public static int GetLongJumpDc(int distanceFeet)
        {
            return Mathf.Max(0, distanceFeet);
        }

        public static int GetLongJumpSuccessDistanceFeet(int checkTotal, int speedFeet)
        {
            int roundedDownToFive = Mathf.FloorToInt(checkTotal / 5f) * 5;
            return Mathf.Clamp(roundedDownToFive, 0, Mathf.Max(0, speedFeet));
        }

        public static JumpType ClassifyJump(
            Vector3Int startCell,
            Vector3Int targetCell,
            int speedFeet,
            float heightStepFeet = GameConstants.CardinalCostFeet)
        {
            float verticalRiseFeet = Mathf.Max(0f, (targetCell.y - startCell.y) * heightStepFeet);
            if (verticalRiseFeet > VerticalLeapHeightFeet)
                return JumpType.HighJump;

            int horizontalDistanceFeet = GridDistancePF2e.DistanceFeetXZ(startCell, targetCell);
            int leapRangeFeet = GetLeapRangeFeet(speedFeet);
            if (horizontalDistanceFeet > leapRangeFeet)
                return JumpType.LongJump;

            return JumpType.Leap;
        }

        public static bool RequiresRunUp(JumpType jumpType)
        {
            return jumpType == JumpType.LongJump || jumpType == JumpType.HighJump;
        }

        public static int GetActionCost(JumpType jumpType)
        {
            return jumpType switch
            {
                JumpType.LongJump => LongJumpActionCost,
                JumpType.HighJump => HighJumpActionCost,
                _ => LeapActionCost
            };
        }

        public static bool IsLeapPossible(
            Vector3Int startCell,
            Vector3Int targetCell,
            int speedFeet,
            float heightStepFeet = GameConstants.CardinalCostFeet)
        {
            if (startCell == targetCell)
                return false;

            int horizontalDistanceFeet = GridDistancePF2e.DistanceFeetXZ(startCell, targetCell);
            int verticalDeltaLevels = targetCell.y - startCell.y;

            if (verticalDeltaLevels > 0)
            {
                float verticalRiseFeet = verticalDeltaLevels * heightStepFeet;
                if (verticalRiseFeet > VerticalLeapHeightFeet)
                    return false;

                return horizontalDistanceFeet <= VerticalLeapHorizontalFeet;
            }

            int leapRangeFeet = GetLeapRangeFeet(speedFeet);
            return leapRangeFeet > 0 && horizontalDistanceFeet <= leapRangeFeet;
        }
    }
}
