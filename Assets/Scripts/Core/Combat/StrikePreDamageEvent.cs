namespace PF2e.Core
{
    /// <summary>
    /// Typed strike event published after hit/damage resolution and before HP application.
    /// </summary>
    public readonly struct StrikePreDamageEvent
    {
        public readonly EntityHandle attacker;
        public readonly EntityHandle target;
        public readonly int naturalRoll;
        public readonly int total;
        public readonly int dc;
        public readonly DegreeOfSuccess degree;
        public readonly int damageRolled;
        public readonly DamageType damageType;

        public StrikePreDamageEvent(
            EntityHandle attacker,
            EntityHandle target,
            int naturalRoll,
            int total,
            int dc,
            DegreeOfSuccess degree,
            int damageRolled,
            DamageType damageType)
        {
            this.attacker = attacker;
            this.target = target;
            this.naturalRoll = naturalRoll;
            this.total = total;
            this.dc = dc;
            this.degree = degree;
            this.damageRolled = damageRolled;
            this.damageType = damageType;
        }
    }
}
