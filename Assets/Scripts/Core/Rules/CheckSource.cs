namespace PF2e.Core
{
    public enum CheckSourceType : byte
    {
        Custom = 0,
        Perception = 1,
        Skill = 2,
        Save = 3,
    }

    /// <summary>
    /// Identifies which check domain produced a roll.
    /// </summary>
    public readonly struct CheckSource
    {
        public readonly CheckSourceType type;
        public readonly SkillType? skill;
        public readonly SaveType? save;
        public readonly string customLabel;

        private CheckSource(CheckSourceType type, SkillType? skill, SaveType? save, string customLabel)
        {
            this.type = type;
            this.skill = skill;
            this.save = save;
            this.customLabel = customLabel;
        }

        public static CheckSource Perception() => new(CheckSourceType.Perception, null, null, null);
        public static CheckSource Skill(SkillType skill) => new(CheckSourceType.Skill, skill, null, null);
        public static CheckSource Save(SaveType save) => new(CheckSourceType.Save, null, save, null);
        public static CheckSource Custom(string label) => new(CheckSourceType.Custom, null, null, label ?? "Check");

        public string ToShortLabel()
        {
            return type switch
            {
                CheckSourceType.Perception => "PRC",
                CheckSourceType.Skill => skill.HasValue ? skill.Value.ToString().ToUpperInvariant() : "SKL",
                CheckSourceType.Save => save.HasValue ? save.Value.ToString().ToUpperInvariant() : "SAV",
                _ => string.IsNullOrWhiteSpace(customLabel) ? "CHK" : customLabel.ToUpperInvariant()
            };
        }
    }
}
