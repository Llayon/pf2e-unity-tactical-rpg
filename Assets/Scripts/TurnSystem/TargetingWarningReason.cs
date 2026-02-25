namespace PF2e.TurnSystem
{
    /// <summary>
    /// Non-blocking preview warning reason for UI hinting.
    /// Warnings never invalidate the target; they only communicate extra risk/cost.
    /// </summary>
    public enum TargetingWarningReason : byte
    {
        None = 0,
        ConcealmentFlatCheck
    }
}
