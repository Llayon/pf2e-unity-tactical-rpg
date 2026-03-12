using UnityEngine;

namespace PF2e.Presentation
{
    /// <summary>
    /// Shared combat UI palette for combat log and tooltip cards.
    /// </summary>
    public static class CombatUiPalette
    {
        public const string TooltipBackgroundHex = "#0A0C13";
        public const string TooltipTitleHex = "#D9CCB4";
        public const string TooltipBodyHex = "#C7BDAF";
        public const string TooltipSecondaryHex = "#958E83";
        public const string TooltipDividerHex = "#3D372E";
        public const string TooltipAccentHex = "#D6C4A3";
        public const string TooltipValueHex = "#E4D7BA";

        public const string SuccessHex = "#A0CB86";
        public const string FailureHex = "#CE8B80";
        public const string CritSuccessHex = "#DEC276";
        public const string CritFailureHex = "#B17068";

        public const string PlayerNameHex = "#89ACC4";
        public const string EnemyNameHex = "#BB7957";
        public const string NeutralTextHex = "#EFE5D9";
        public const string NarrativeTextHex = "#E9E0D4";
        public const string WeaponTextHex = "#E1D4BC";
        public const string SecondaryNoteHex = "#B8B0A4";
        public const string RoundHex = "#B3AB9F";
        public const string ConditionGainHex = "#C2A59C";
        public const string ConditionLoseHex = "#97B1AF";
        public const string DefeatedHex = "#B06A63";
        public const string StatusTokenHex = "#D8C8A7";
        public const string HealHex = "#7FA271";

        public const string SlashingHex = "#BC7267";
        public const string PiercingHex = "#8EA2B9";
        public const string BludgeoningHex = "#C1B9AB";
        public const string DamageAccentHex = "#DEC38D";

        public const string ActionDiamondHex = "#D4B366";

        public static readonly Color TooltipBackgroundColor = ParseColor(TooltipBackgroundHex, 0.975f);
        public static readonly Color TooltipTitleColor = ParseColor(TooltipTitleHex);
        public static readonly Color TooltipBodyColor = ParseColor(TooltipBodyHex);
        public static readonly Color TooltipDividerColor = ParseColor(TooltipDividerHex, 0.9f);

        private static Color ParseColor(string html, float alpha = 1f)
        {
            if (!ColorUtility.TryParseHtmlString(html, out var color))
            {
                color = Color.white;
            }

            color.a = alpha;
            return color;
        }
    }
}
