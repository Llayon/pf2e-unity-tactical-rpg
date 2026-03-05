namespace PF2e.TurnSystem
{
    /// <summary>
    /// Preferred cantrip variant for Raise Shield when both spell shields are available.
    /// Physical shield raise remains the highest priority when equipped and not raised.
    /// </summary>
    public enum RaiseShieldSpellMode
    {
        Standard = 0,
        Glass = 1
    }

    public static class RaiseShieldSpellModeExtensions
    {
        public static string ToShortToken(this RaiseShieldSpellMode mode)
        {
            return mode switch
            {
                RaiseShieldSpellMode.Glass => "GLS",
                _ => "STD"
            };
        }
    }
}
