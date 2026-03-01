namespace PF2e.Core
{
    /// <summary>
    /// Typed event for non-strike skill-based check actions (Trip, Grapple, Shove, etc.).
    /// roll contains the effective modifier used for the check (including action-specific adjustments such as MAP).
    /// </summary>
    public readonly struct SkillCheckResolvedEvent
    {
        public readonly EntityHandle actor;
        public readonly EntityHandle target;
        public readonly SkillType skill;
        public readonly CheckRoll roll;
        public readonly CheckSource defenseSource;
        public readonly int dc;
        public readonly DegreeOfSuccess degree;
        public readonly string actionName;
        public readonly int aidCircumstanceBonus;
        public readonly bool hasOpposedProjection;
        public readonly OpposedCheckResult opposedProjection;

        // Backward-compatible convenience accessors.
        public int naturalRoll => roll.naturalRoll;
        public int modifier => roll.modifier;
        public int total => roll.total;

        public SkillCheckResolvedEvent(
            EntityHandle actor,
            EntityHandle target,
            SkillType skill,
            in CheckRoll roll,
            in CheckSource defenseSource,
            int dc,
            DegreeOfSuccess degree,
            string actionName,
            int aidCircumstanceBonus = 0)
        {
            this.actor = actor;
            this.target = target;
            this.skill = skill;
            this.roll = roll;
            this.defenseSource = defenseSource;
            this.dc = dc;
            this.degree = degree;
            this.actionName = actionName;
            this.aidCircumstanceBonus = aidCircumstanceBonus;
            hasOpposedProjection = false;
            opposedProjection = default;
        }

        public SkillCheckResolvedEvent(
            EntityHandle actor,
            EntityHandle target,
            SkillType skill,
            in CheckRoll roll,
            in CheckSource defenseSource,
            int dc,
            DegreeOfSuccess degree,
            string actionName,
            in OpposedCheckResult opposedProjection,
            int aidCircumstanceBonus = 0)
        {
            this.actor = actor;
            this.target = target;
            this.skill = skill;
            this.roll = roll;
            this.defenseSource = defenseSource;
            this.dc = dc;
            this.degree = degree;
            this.actionName = actionName;
            this.aidCircumstanceBonus = aidCircumstanceBonus;
            hasOpposedProjection = true;
            this.opposedProjection = opposedProjection;
        }
    }
}
