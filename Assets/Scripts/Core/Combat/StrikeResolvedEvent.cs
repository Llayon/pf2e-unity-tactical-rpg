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
        public readonly int rangePenalty;
        public readonly int total;
        public readonly int dc;
        public readonly DegreeOfSuccess degree;
        public readonly int damage;
        public readonly int deadlyBonusDamage;
        public readonly DamageType damageType;
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
            DamageType damageType,
            int hpBefore,
            int hpAfter,
            bool targetDefeated,
            int rangePenalty = 0,
            int deadlyBonusDamage = 0)
        {
            this.attacker = attacker;
            this.target = target;
            this.weaponName = weaponName;
            this.naturalRoll = naturalRoll;
            this.attackBonus = attackBonus;
            this.mapPenalty = mapPenalty;
            this.rangePenalty = rangePenalty;
            this.total = total;
            this.dc = dc;
            this.degree = degree;
            this.damage = damage;
            this.deadlyBonusDamage = deadlyBonusDamage;
            this.damageType = damageType;
            this.hpBefore = hpBefore;
            this.hpAfter = hpAfter;
            this.targetDefeated = targetDefeated;
        }

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
            DamageType damageType,
            int hpBefore,
            int hpAfter,
            bool targetDefeated)
        {
            this = new StrikeResolvedEvent(
                attacker,
                target,
                weaponName,
                naturalRoll,
                attackBonus,
                mapPenalty,
                total,
                dc,
                degree,
                damage,
                damageType,
                hpBefore,
                hpAfter,
                targetDefeated,
                rangePenalty: 0,
                deadlyBonusDamage: 0);
        }
    }
}
