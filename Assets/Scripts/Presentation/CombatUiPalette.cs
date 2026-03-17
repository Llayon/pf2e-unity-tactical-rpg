using UnityEngine;

namespace PF2e.Presentation
{
    /// <summary>
    /// Shared combat UI palette for combat log and tooltip cards.
    /// </summary>
    public static class CombatUiPalette
    {
        public const string TooltipBackgroundHex = "#0B1118";
        public const string TooltipTitleHex = "#E5DBCA";
        public const string TooltipBodyHex = "#D7DEE6";
        public const string TooltipSecondaryHex = "#95A2B1";
        public const string TooltipDividerHex = "#253243";
        public const string TooltipAccentHex = "#CDBA94";
        public const string TooltipValueHex = "#F0E2BF";

        public const string HudPanelBackgroundHex = "#0D121A";
        public const string HudPanelSurfaceHex = "#162231";
        public const string HudPanelAccentHex = "#25384E";
        public const string HudButtonBackgroundHex = "#1A2A3A";
        public const string HudButtonSelectedHex = "#DDBD71";
        public const string HudTextPrimaryHex = "#E2E8EF";
        public const string HudTextSecondaryHex = "#B2BCC9";
        public const string HudTextMutedHex = "#8F99A6";
        public const string CombatLogBodyHex = "#F0E9C9";
        public const string HudButtonTextHex = "#F1F4F8";
        public const string HudButtonSelectedTextHex = "#141A22";
        public const string HudHealthGoodHex = "#54C977";
        public const string HudHealthLowHex = "#E2665E";
        public const string HudProgressBackgroundHex = "#0A0F15";

        public const string SuccessHex = "#A0CB86";
        public const string FailureHex = "#CE8B80";
        public const string CritSuccessHex = "#DEC276";
        public const string CritFailureHex = "#B17068";

        public const string PlayerNameHex = "#8FAFD2";
        public const string EnemyNameHex = "#C28767";
        public const string NeutralTextHex = "#E7DED0";
        public const string NarrativeTextHex = "#DED4C6";
        public const string WeaponTextHex = "#D8C7A8";
        public const string SecondaryNoteHex = "#B8AB9A";
        public const string RoundHex = "#B7C1CC";
        public const string ConditionGainHex = "#C2A59C";
        public const string ConditionLoseHex = "#97B1AF";
        public const string DefeatedHex = "#B06A63";
        public const string StatusTokenHex = "#D8C8A7";
        public const string HealHex = "#7FA271";

        public const string SlashingHex = "#BC7267";
        public const string PiercingHex = "#8EA2B9";
        public const string BludgeoningHex = "#C1B9AB";
        public const string ForceHex = "#D9D1A8";
        public const string ElectricityHex = "#7CBED9";
        public const string ColdHex = "#8FC8EA";
        public const string FireHex = "#E39A62";
        public const string VoidHex = "#A189B8";
        public const string DamageAccentHex = "#DEC38D";

        public const string ActionDiamondHex = "#D4B366";

        public static readonly Color TooltipBackgroundColor = ParseColor(TooltipBackgroundHex, 0.84f);
        public static readonly Color TooltipTitleColor = ParseColor(TooltipTitleHex);
        public static readonly Color TooltipBodyColor = ParseColor(TooltipBodyHex);
        public static readonly Color TooltipSecondaryColor = ParseColor(TooltipSecondaryHex);
        public static readonly Color TooltipDividerColor = ParseColor(TooltipDividerHex, 0.72f);

        public static readonly Color HudPanelBackgroundColor = ParseColor(HudPanelBackgroundHex, 0.84f);
        public static readonly Color HudPanelSurfaceColor = ParseColor(HudPanelSurfaceHex, 0.92f);
        public static readonly Color HudPanelAccentColor = ParseColor(HudPanelAccentHex, 0.96f);
        public static readonly Color HudButtonBackgroundColor = ParseColor(HudButtonBackgroundHex, 0.94f);
        public static readonly Color HudButtonSelectedColor = ParseColor(HudButtonSelectedHex, 0.95f);
        public static readonly Color HudTextPrimaryColor = ParseColor(HudTextPrimaryHex);
        public static readonly Color HudTextSecondaryColor = ParseColor(HudTextSecondaryHex);
        public static readonly Color HudTextMutedColor = ParseColor(HudTextMutedHex);
        public static readonly Color CombatLogBodyColor = ParseColor(CombatLogBodyHex);
        public static readonly Color HudButtonTextColor = ParseColor(HudButtonTextHex);
        public static readonly Color HudButtonSelectedTextColor = ParseColor(HudButtonSelectedTextHex);
        public static readonly Color HudHealthGoodColor = ParseColor(HudHealthGoodHex);
        public static readonly Color HudHealthLowColor = ParseColor(HudHealthLowHex);
        public static readonly Color HudProgressBackgroundColor = ParseColor(HudProgressBackgroundHex, 0.58f);

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
