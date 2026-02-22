namespace PF2e.Core
{
    /// <summary>
    /// Typed event for non-strike skill-based check actions (Trip, Grapple, Shove, etc.).
    /// Modifier is the effective modifier used for the roll (includes action-specific adjustments such as MAP).
    /// </summary>
    public readonly struct SkillCheckResolvedEvent
    {
        public readonly EntityHandle actor;
        public readonly EntityHandle target;
        public readonly SkillType skill;
        public readonly int naturalRoll;
        public readonly int modifier;
        public readonly int total;
        public readonly int dc;
        public readonly DegreeOfSuccess degree;
        public readonly string actionName;

        public SkillCheckResolvedEvent(
            EntityHandle actor,
            EntityHandle target,
            SkillType skill,
            int naturalRoll,
            int modifier,
            int total,
            int dc,
            DegreeOfSuccess degree,
            string actionName)
        {
            this.actor = actor;
            this.target = target;
            this.skill = skill;
            this.naturalRoll = naturalRoll;
            this.modifier = modifier;
            this.total = total;
            this.dc = dc;
            this.degree = degree;
            this.actionName = actionName;
        }
    }
}
