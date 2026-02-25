using PF2e.Core;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Immutable payload carried through strike phases.
    /// </summary>
    public readonly struct StrikePhaseResult
    {
        public readonly EntityHandle attacker;
        public readonly EntityHandle target;
        public readonly string weaponName;
        public readonly int naturalRoll;
        public readonly int attackBonus;
        public readonly int mapPenalty;
        public readonly int rangePenalty;
        public readonly int volleyPenalty;
        public readonly int coverAcBonus;
        public readonly int total;
        public readonly int dc;
        public readonly DegreeOfSuccess acDegree;
        public readonly DegreeOfSuccess degree;
        public readonly bool concealmentCheckRequired;
        public readonly int concealmentFlatCheckRoll;
        public readonly bool concealmentFlatCheckPassed;
        public readonly int damageRolled;
        public readonly int fatalBonusDamage;
        public readonly int deadlyBonusDamage;
        public readonly DamageType damageType;
        public readonly bool damageDealt;

        private StrikePhaseResult(
            EntityHandle attacker,
            EntityHandle target,
            string weaponName,
            int naturalRoll,
            int attackBonus,
            int mapPenalty,
            int rangePenalty,
            int volleyPenalty,
            int coverAcBonus,
            int total,
            int dc,
            DegreeOfSuccess acDegree,
            DegreeOfSuccess degree,
            bool concealmentCheckRequired,
            int concealmentFlatCheckRoll,
            bool concealmentFlatCheckPassed,
            int damageRolled,
            int fatalBonusDamage,
            int deadlyBonusDamage,
            DamageType damageType,
            bool damageDealt)
        {
            this.attacker = attacker;
            this.target = target;
            this.weaponName = weaponName;
            this.naturalRoll = naturalRoll;
            this.attackBonus = attackBonus;
            this.mapPenalty = mapPenalty;
            this.rangePenalty = rangePenalty;
            this.volleyPenalty = volleyPenalty;
            this.coverAcBonus = coverAcBonus;
            this.total = total;
            this.dc = dc;
            this.acDegree = acDegree;
            this.degree = degree;
            this.concealmentCheckRequired = concealmentCheckRequired;
            this.concealmentFlatCheckRoll = concealmentFlatCheckRoll;
            this.concealmentFlatCheckPassed = concealmentFlatCheckPassed;
            this.damageRolled = damageRolled;
            this.fatalBonusDamage = fatalBonusDamage;
            this.deadlyBonusDamage = deadlyBonusDamage;
            this.damageType = damageType;
            this.damageDealt = damageDealt;
        }

        public static StrikePhaseResult FromAttackRoll(
            EntityHandle attacker,
            EntityHandle target,
            string weaponName,
            int naturalRoll,
            int attackBonus,
            int mapPenalty,
            int total,
            int rangePenalty = 0,
            int volleyPenalty = 0)
        {
            return new StrikePhaseResult(
                attacker,
                target,
                weaponName,
                naturalRoll,
                attackBonus,
                mapPenalty,
                rangePenalty,
                volleyPenalty,
                coverAcBonus: 0,
                total,
                dc: 0,
                acDegree: DegreeOfSuccess.CriticalFailure,
                degree: DegreeOfSuccess.CriticalFailure,
                concealmentCheckRequired: false,
                concealmentFlatCheckRoll: 0,
                concealmentFlatCheckPassed: false,
                damageRolled: 0,
                fatalBonusDamage: 0,
                deadlyBonusDamage: 0,
                damageType: DamageType.Bludgeoning,
                damageDealt: false);
        }

        public StrikePhaseResult WithHitAndDamage(
            int dc,
            DegreeOfSuccess degree,
            int damageRolled,
            DamageType damageType,
            bool damageDealt,
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

            return new StrikePhaseResult(
                attacker,
                target,
                weaponName,
                naturalRoll,
                attackBonus,
                mapPenalty,
                rangePenalty,
                volleyPenalty,
                coverAcBonus,
                total,
                dc,
                resolvedAcDegree,
                degree,
                concealmentCheckRequired,
                concealmentFlatCheckRoll,
                concealmentFlatCheckPassed,
                damageRolled,
                fatalBonusDamage,
                deadlyBonusDamage,
                damageType,
                damageDealt);
        }
    }
}
