namespace PF2e.Core
{
    /// <summary>
    /// Reason why a strike attempt cannot be performed.
    /// </summary>
    public enum StrikeFailureReason
    {
        None = 0,
        AttackerDead,
        TargetDead,
        SameTeam,
        SelfTarget,
        ElevationMismatch,
        RangedNotSupported,
        OutOfRange
    }
}
