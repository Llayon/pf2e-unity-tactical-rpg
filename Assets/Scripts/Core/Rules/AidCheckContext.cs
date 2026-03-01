namespace PF2e.Core
{
    public readonly struct AidCheckContext
    {
        public readonly EntityHandle ally;
        public readonly AidCheckType checkType;
        public readonly SkillType? skill;
        public readonly string triggeringActionName;

        private AidCheckContext(EntityHandle ally, AidCheckType checkType, SkillType? skill, string triggeringActionName)
        {
            this.ally = ally;
            this.checkType = checkType;
            this.skill = skill;
            this.triggeringActionName = triggeringActionName;
        }

        public static AidCheckContext ForSkill(EntityHandle ally, SkillType skill, string triggeringActionName)
        {
            return new AidCheckContext(ally, AidCheckType.Skill, skill, triggeringActionName);
        }

        public static AidCheckContext ForStrike(EntityHandle ally, string triggeringActionName)
        {
            return new AidCheckContext(ally, AidCheckType.Strike, null, triggeringActionName);
        }
    }
}
