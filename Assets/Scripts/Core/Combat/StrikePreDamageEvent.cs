namespace PF2e.Core
{
    /// <summary>
    /// Typed strike event published after hit/damage resolution and before HP application.
    /// </summary>
    public readonly struct StrikePreDamageEvent
    {
        private static readonly CheckSource AttackSource = CheckSource.Custom("ATK");
        private static readonly CheckSource DefenseSource = CheckSource.Custom("AC");

        public readonly EntityHandle attacker;
        public readonly EntityHandle target;
        public readonly CheckRoll attackRoll;
        public readonly CheckSource defenseSource;
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
            this.attackRoll = new CheckRoll(naturalRoll, total - naturalRoll, AttackSource);
            this.defenseSource = DefenseSource;
            this.naturalRoll = naturalRoll;
            this.total = total;
            this.dc = dc;
            this.degree = degree;
            this.damageRolled = damageRolled;
            this.damageType = damageType;
        }

        public StrikePreDamageEvent(
            EntityHandle attacker,
            EntityHandle target,
            in CheckRoll attackRoll,
            in CheckSource defenseSource,
            int dc,
            DegreeOfSuccess degree,
            int damageRolled,
            DamageType damageType)
        {
            this.attacker = attacker;
            this.target = target;
            this.attackRoll = attackRoll;
            this.defenseSource = defenseSource;
            this.naturalRoll = attackRoll.naturalRoll;
            this.total = attackRoll.total;
            this.dc = dc;
            this.degree = degree;
            this.damageRolled = damageRolled;
            this.damageType = damageType;
        }
    }
}
