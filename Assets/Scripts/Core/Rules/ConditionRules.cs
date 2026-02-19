namespace PF2e.Core
{
    public static class ConditionRules
    {
        public static bool IsValued(ConditionType type) => type switch
        {
            ConditionType.Frightened or ConditionType.Sickened or
            ConditionType.Stunned or ConditionType.Slowed or
            ConditionType.Wounded or ConditionType.Dying or
            ConditionType.Doomed => true,
            _ => false
        };

        // MVP: Sickened auto-ticks (RAW requires Fort save — future).
        public static bool AutoDecrementsAtEndOfTurn(ConditionType type) => type switch
        {
            ConditionType.Frightened => true,
            ConditionType.Sickened => true,
            _ => false
        };

        public static string DisplayName(ConditionType type) => type switch
        {
            ConditionType.OffGuard => "off-guard",
            _ => type.ToString().ToLowerInvariant()
        };
    }
}
