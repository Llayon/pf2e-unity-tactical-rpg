namespace PF2e.Core
{
    /// <summary>
    /// Typed event for strike resolution.
    /// Published by StrikeAction; consumed by StrikeLogForwarder.
    /// </summary>
    public readonly struct StrikeResolvedEvent
    {
        private static readonly CheckSource AttackSource = CheckSource.Custom("ATK");
        private static readonly CheckSource DefenseSource = CheckSource.Custom("AC");
        private static readonly CheckSource ConcealmentSource = CheckSource.Custom("CONCEAL");

        public readonly EntityHandle attacker;
        public readonly EntityHandle target;
        public readonly string weaponName;
        public readonly CheckRoll attackRoll;
        public readonly CheckSource defenseSource;
        public readonly CheckRoll concealmentRoll;
        public readonly int naturalRoll;
        public readonly int attackBonus;
        public readonly int mapPenalty;
        public readonly int rangePenalty;
        public readonly int volleyPenalty;
        public readonly int aidCircumstanceBonus;
        public readonly int coverAcBonus;
        public readonly int total;
        public readonly int dc;
        public readonly DegreeOfSuccess acDegree;
        public readonly DegreeOfSuccess degree;
        public readonly bool concealmentCheckRequired;
        public readonly int concealmentFlatCheckRoll;
        public readonly bool concealmentFlatCheckPassed;
        public readonly int damage;
        public readonly int fatalBonusDamage;
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
            int volleyPenalty = 0,
            int aidCircumstanceBonus = 0,
            int coverAcBonus = 0,
            int fatalBonusDamage = 0,
            int deadlyBonusDamage = 0,
            DegreeOfSuccess acDegree = DegreeOfSuccess.CriticalFailure,
            bool concealmentCheckRequired = false,
            int concealmentFlatCheckRoll = 0,
            bool concealmentFlatCheckPassed = false)
        {
            DegreeOfSuccess resolvedAcDegree = acDegree == DegreeOfSuccess.CriticalFailure
                ? degree
                : acDegree;

            this.attacker = attacker;
            this.target = target;
            this.weaponName = weaponName;
            int attackModifier = attackBonus + mapPenalty + rangePenalty + volleyPenalty + aidCircumstanceBonus;
            this.attackRoll = new CheckRoll(naturalRoll, attackModifier, AttackSource);
            this.defenseSource = DefenseSource;
            this.concealmentRoll = concealmentCheckRequired
                ? new CheckRoll(concealmentFlatCheckRoll, 0, ConcealmentSource)
                : default;
            this.naturalRoll = naturalRoll;
            this.attackBonus = attackBonus;
            this.mapPenalty = mapPenalty;
            this.rangePenalty = rangePenalty;
            this.volleyPenalty = volleyPenalty;
            this.aidCircumstanceBonus = aidCircumstanceBonus;
            this.coverAcBonus = coverAcBonus;
            this.total = total;
            this.dc = dc;
            this.acDegree = resolvedAcDegree;
            this.degree = degree;
            this.concealmentCheckRequired = concealmentCheckRequired;
            this.concealmentFlatCheckRoll = concealmentFlatCheckRoll;
            this.concealmentFlatCheckPassed = concealmentFlatCheckPassed;
            this.damage = damage;
            this.fatalBonusDamage = fatalBonusDamage;
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
                volleyPenalty: 0,
                aidCircumstanceBonus: 0,
                coverAcBonus: 0,
                fatalBonusDamage: 0,
                deadlyBonusDamage: 0,
                acDegree: degree,
                concealmentCheckRequired: false,
                concealmentFlatCheckRoll: 0,
                concealmentFlatCheckPassed: false);
        }
    }
}
