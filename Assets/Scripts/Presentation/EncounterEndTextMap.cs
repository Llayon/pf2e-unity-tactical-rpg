using PF2e.Core;

namespace PF2e.Presentation
{
    /// <summary>
    /// Central mapping for encounter result text displayed in end-of-encounter UI.
    /// </summary>
    public static class EncounterEndTextMap
    {
        public static EncounterEndText For(EncounterResult result)
        {
            switch (result)
            {
                case EncounterResult.Victory:
                    return new EncounterEndText("Victory", "All enemies defeated.");
                case EncounterResult.Defeat:
                    return new EncounterEndText("Defeat", "All players defeated.");
                default:
                    return new EncounterEndText("Encounter Ended", "Combat was ended manually.");
            }
        }
    }

    public readonly struct EncounterEndText
    {
        public readonly string Title;
        public readonly string Subtitle;

        public EncounterEndText(string title, string subtitle)
        {
            Title = title;
            Subtitle = subtitle;
        }
    }
}
