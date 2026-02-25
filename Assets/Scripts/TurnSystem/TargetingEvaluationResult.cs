namespace PF2e.TurnSystem
{
    /// <summary>
    /// Detailed targeting evaluation result for preview/UI.
    /// Preserves existing TargetingResult compatibility while exposing a richer failure reason.
    /// </summary>
    public readonly struct TargetingEvaluationResult
    {
        public readonly TargetingResult result;
        public readonly TargetingFailureReason failureReason;
        public readonly TargetingWarningReason warningReason;

        public bool IsSuccess => result == TargetingResult.Success;
        public bool HasWarning => warningReason != TargetingWarningReason.None;

        public TargetingEvaluationResult(
            TargetingResult result,
            TargetingFailureReason failureReason,
            TargetingWarningReason warningReason = TargetingWarningReason.None)
        {
            this.result = result;
            this.failureReason = failureReason;
            this.warningReason = warningReason;
        }

        public static TargetingEvaluationResult Success()
        {
            return new TargetingEvaluationResult(
                TargetingResult.Success,
                TargetingFailureReason.None,
                TargetingWarningReason.None);
        }

        public static TargetingEvaluationResult SuccessWithWarning(TargetingWarningReason warningReason)
        {
            return new TargetingEvaluationResult(
                TargetingResult.Success,
                TargetingFailureReason.None,
                warningReason);
        }

        public static TargetingEvaluationResult FromFailure(TargetingFailureReason reason)
        {
            return new TargetingEvaluationResult(
                MapResult(reason),
                reason,
                TargetingWarningReason.None);
        }

        public TargetingEvaluationResult WithWarning(TargetingWarningReason warningReason)
        {
            if (warningReason == TargetingWarningReason.None)
                return this;

            var combinedWarnings = this.warningReason | warningReason;
            if (combinedWarnings == this.warningReason)
                return this;

            return new TargetingEvaluationResult(result, failureReason, combinedWarnings);
        }

        private static TargetingResult MapResult(TargetingFailureReason reason)
        {
            return reason switch
            {
                TargetingFailureReason.None => TargetingResult.Success,
                TargetingFailureReason.InvalidTarget => TargetingResult.InvalidTarget,
                TargetingFailureReason.NotAlive => TargetingResult.NotAlive,
                TargetingFailureReason.SelfTarget => TargetingResult.SelfTarget,
                TargetingFailureReason.WrongTeam => TargetingResult.WrongTeam,
                TargetingFailureReason.OutOfRange => TargetingResult.OutOfRange,
                TargetingFailureReason.ModeNotSupported => TargetingResult.ModeNotSupported,
                _ => TargetingResult.InvalidTarget,
            };
        }
    }
}
