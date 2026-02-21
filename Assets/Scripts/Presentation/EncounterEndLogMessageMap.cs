using PF2e.Core;

namespace PF2e.Presentation
{
    /// <summary>
    /// Central mapping for encounter result text used in combat-end log messages.
    /// </summary>
    public static class EncounterEndLogMessageMap
    {
        public static string For(EncounterResult result)
        {
            switch (result)
            {
                case EncounterResult.Victory:
                    return "Combat ended. Victory.";
                case EncounterResult.Defeat:
                    return "Combat ended. Defeat.";
                default:
                    return "Combat ended.";
            }
        }
    }
}
