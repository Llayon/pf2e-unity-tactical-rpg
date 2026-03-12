namespace PF2e.Presentation
{
    public static class CombatLogLinkHelper
    {
        public const string InteractiveColor = "#D4C4A8";

        public static string Link(string token, string visibleText)
        {
            return Link(token, visibleText, InteractiveColor);
        }

        public static string Link(string token, string visibleText, string colorHex)
        {
            string safeToken = token ?? string.Empty;
            string safeText = visibleText ?? string.Empty;
            string safeColor = string.IsNullOrWhiteSpace(colorHex) ? InteractiveColor : colorHex;
            return $"<u><color={safeColor}><link=\"{safeToken}\">{safeText}</link></color></u>";
        }

        public static string LinkWithoutUnderline(string token, string visibleText)
        {
            return LinkWithoutUnderline(token, visibleText, InteractiveColor);
        }

        public static string LinkWithoutUnderline(string token, string visibleText, string colorHex)
        {
            string safeToken = token ?? string.Empty;
            string safeText = visibleText ?? string.Empty;
            string safeColor = string.IsNullOrWhiteSpace(colorHex) ? InteractiveColor : colorHex;
            return $"<color={safeColor}><link=\"{safeToken}\">{safeText}</link></color>";
        }
    }

    public static class CombatLogLinkTokens
    {
        public const string Result = "result";
        public const string DamageTotal = "dmg";
    }
}
