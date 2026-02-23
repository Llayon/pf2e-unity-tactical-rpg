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

        public bool IsSuccess => result == TargetingResult.Success;

        public TargetingEvaluationResult(TargetingResult result, TargetingFailureReason failureReason)
        {
            this.result = result;
            this.failureReason = failureReason;
        }

        public static TargetingEvaluationResult Success()
        {
            return new TargetingEvaluationResult(TargetingResult.Success, TargetingFailureReason.None);
        }

        public static TargetingEvaluationResult FromFailure(TargetingFailureReason reason)
        {
            return new TargetingEvaluationResult(MapResult(reason), reason);
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
