using PF2e.Core;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Captures a resolved Reposition check so destination selection can happen afterward without re-rolling.
    /// </summary>
    public readonly struct RepositionCheckContext
    {
        public readonly EntityHandle actor;
        public readonly EntityHandle target;
        public readonly int naturalRoll;
        public readonly int modifier;
        public readonly int total;
        public readonly int dc;
        public readonly DegreeOfSuccess degree;
        public readonly int maxMoveFeet;

        public RepositionCheckContext(
            EntityHandle actor,
            EntityHandle target,
            int naturalRoll,
            int modifier,
            int total,
            int dc,
            DegreeOfSuccess degree,
            int maxMoveFeet)
        {
            this.actor = actor;
            this.target = target;
            this.naturalRoll = naturalRoll;
            this.modifier = modifier;
            this.total = total;
            this.dc = dc;
            this.degree = degree;
            this.maxMoveFeet = maxMoveFeet;
        }
    }
}
