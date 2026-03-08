namespace PF2e.Presentation
{
    public static class CombatLogLinkHelper
    {
        public const string InteractiveColor = "#D4C4A8";

        public static string Link(string token, string visibleText)
        {
            string safeToken = token ?? string.Empty;
            string safeText = visibleText ?? string.Empty;
            return $"<u><color={InteractiveColor}><link=\"{safeToken}\">{safeText}</link></color></u>";
        }
    }

    public static class CombatLogLinkTokens
    {
        public const string Result = "result";
        public const string DamageTotal = "dmg";
    }
}
