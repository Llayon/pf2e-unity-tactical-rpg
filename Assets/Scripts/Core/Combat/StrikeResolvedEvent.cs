namespace PF2e.Core
{
    /// <summary>
    /// Typed event for strike resolution.
    /// Published by StrikeAction; consumed by StrikeLogForwarder.
    /// </summary>
    public readonly struct StrikeResolvedEvent
    {
        public readonly EntityHandle attacker;
        public readonly EntityHandle target;
        public readonly string weaponName;
        public readonly int naturalRoll;
        public readonly int attackBonus;
        public readonly int mapPenalty;
        public readonly int total;
        public readonly int dc;
        public readonly DegreeOfSuccess degree;
        public readonly int damage;
        public readonly string damageType;
        public readonly int hpBefore;
        public readonly int hpAfter;
        public readonly bool targetDefeated;

        public StrikeResolvedEvent(
            EntityHandle attacker,
            EntityHandle target,
            string weaponName,
            int naturalRoll,
            int attackBonus,
            int mapPenalty,
            int total,
            int dc,
            DegreeOfSuccess degree,
            int damage,
            string damageType,
            int hpBefore,
            int hpAfter,
            bool targetDefeated)
        {
            this.attacker = attacker;
            this.target = target;
            this.weaponName = weaponName;
            this.naturalRoll = naturalRoll;
            this.attackBonus = attackBonus;
            this.mapPenalty = mapPenalty;
            this.total = total;
            this.dc = dc;
            this.degree = degree;
            this.damage = damage;
            this.damageType = damageType;
            this.hpBefore = hpBefore;
            this.hpAfter = hpAfter;
            this.targetDefeated = targetDefeated;
        }
    }
}
