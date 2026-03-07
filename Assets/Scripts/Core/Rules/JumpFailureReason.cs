namespace PF2e.Core
{
    public enum JumpFailureReason : byte
    {
        None = 0,
        InvalidLanding,
        Unreachable,
        MissingRunUp,
        InvalidState
    }
}
