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
        public readonly int total;
        public readonly int dc;
        public readonly DegreeOfSuccess degree;
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
            int total,
            int dc,
            DegreeOfSuccess degree,
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
            this.total = total;
            this.dc = dc;
            this.degree = degree;
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
            int rangePenalty = 0)
        {
            return new StrikePhaseResult(
                attacker,
                target,
                weaponName,
                naturalRoll,
                attackBonus,
                mapPenalty,
                rangePenalty,
                total,
                dc: 0,
                degree: DegreeOfSuccess.CriticalFailure,
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
            int fatalBonusDamage = 0,
            int deadlyBonusDamage = 0)
        {
            return new StrikePhaseResult(
                attacker,
                target,
                weaponName,
                naturalRoll,
                attackBonus,
                mapPenalty,
                rangePenalty,
                total,
                dc,
                degree,
                damageRolled,
                fatalBonusDamage,
                deadlyBonusDamage,
                damageType,
                damageDealt);
        }
    }
}
