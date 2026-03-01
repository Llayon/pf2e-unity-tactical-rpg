namespace PF2e.Core
{
    public readonly struct AidResolvedEvent
    {
        public readonly EntityHandle helper;
        public readonly EntityHandle ally;
        public readonly AidCheckType checkType;
        public readonly SkillType? skill;
        public readonly string triggeringActionName;
        public readonly CheckRoll roll;
        public readonly int dc;
        public readonly DegreeOfSuccess degree;
        public readonly int modifierApplied;
        public readonly bool reactionConsumed;

        public AidResolvedEvent(
            EntityHandle helper,
            EntityHandle ally,
            AidCheckType checkType,
            SkillType? skill,
            string triggeringActionName,
            in CheckRoll roll,
            int dc,
            DegreeOfSuccess degree,
            int modifierApplied,
            bool reactionConsumed)
        {
            this.helper = helper;
            this.ally = ally;
            this.checkType = checkType;
            this.skill = skill;
            this.triggeringActionName = triggeringActionName;
            this.roll = roll;
            this.dc = dc;
            this.degree = degree;
            this.modifierApplied = modifierApplied;
            this.reactionConsumed = reactionConsumed;
        }
    }
}
