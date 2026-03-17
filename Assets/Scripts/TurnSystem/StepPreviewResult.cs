using UnityEngine;

namespace PF2e.TurnSystem
{
    public readonly struct StepPreviewResult
    {
        public readonly bool isValid;
        public readonly StepFailureReason failureReason;
        public readonly Vector3Int targetCell;
        public readonly int actionCost;
        public readonly int stepCostFeet;

        private StepPreviewResult(
            bool isValid,
            StepFailureReason failureReason,
            Vector3Int targetCell,
            int actionCost,
            int stepCostFeet)
        {
            this.isValid = isValid;
            this.failureReason = failureReason;
            this.targetCell = targetCell;
            this.actionCost = actionCost;
            this.stepCostFeet = stepCostFeet;
        }

        public static StepPreviewResult Valid(Vector3Int targetCell, int stepCostFeet)
        {
            return new StepPreviewResult(
                isValid: true,
                failureReason: StepFailureReason.None,
                targetCell: targetCell,
                actionCost: StepAction.ActionCost,
                stepCostFeet: stepCostFeet);
        }

        public static StepPreviewResult Invalid(StepFailureReason failureReason, Vector3Int targetCell)
        {
            return new StepPreviewResult(
                isValid: false,
                failureReason: failureReason,
                targetCell: targetCell,
                actionCost: StepAction.ActionCost,
                stepCostFeet: 0);
        }
    }
}
