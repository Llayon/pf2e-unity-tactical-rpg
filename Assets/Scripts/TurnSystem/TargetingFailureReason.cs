namespace PF2e.TurnSystem
{
    /// <summary>
    /// Detailed non-mutating targeting failure reason for UI preview/hints.
    /// This is more granular than TargetingResult and is intended for presentation feedback.
    /// </summary>
    public enum TargetingFailureReason : byte
    {
        None = 0,
        InvalidTarget,
        NotAlive,
        SelfTarget,
        WrongTeam,
        OutOfRange,
        NoLineOfSight,
        WrongElevation,
        TargetTooLarge,
        RequiresMeleeWeapon,
        MissingRequiredWeaponTrait,
        NoGrappleRelation,
        InvalidState,
        ModeNotSupported
    }
}
