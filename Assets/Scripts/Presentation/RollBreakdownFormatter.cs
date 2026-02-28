using PF2e.Core;

namespace PF2e.Presentation
{
    /// <summary>
    /// Shared formatter for roll/source breakdown text used by logs and UI hints.
    /// </summary>
    public static class RollBreakdownFormatter
    {
        public static string FormatRoll(in CheckRoll roll)
        {
            return $"{roll.source.ToShortLabel()} d20({roll.naturalRoll}) {FormatSigned(roll.modifier)} = {roll.total}";
        }

        public static string FormatVsDc(in CheckRoll roll, in CheckSource defenseSource, int dc)
        {
            return $"{FormatRoll(roll)} vs {defenseSource.ToShortLabel()} DC {dc}";
        }

        public static string FormatCheckVsDcLabel(in CheckSource offenseSource, in CheckSource defenseSource)
        {
            return $"{FormatUiSource(offenseSource)} vs {FormatUiSource(defenseSource)} DC";
        }

        public static string FormatSigned(int value)
        {
            return value >= 0 ? $"+{value}" : value.ToString();
        }

        private static string FormatUiSource(in CheckSource source)
        {
            return source.type switch
            {
                CheckSourceType.Perception => "Perception",
                CheckSourceType.Skill => source.skill.HasValue ? source.skill.Value.ToString() : "Skill",
                CheckSourceType.Save => source.save.HasValue ? source.save.Value.ToString() : "Save",
                _ => string.IsNullOrWhiteSpace(source.customLabel) ? "Check" : source.customLabel
            };
        }
    }
}
