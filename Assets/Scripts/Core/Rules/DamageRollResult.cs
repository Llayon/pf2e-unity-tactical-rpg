namespace PF2e.Core
{
    /// <summary>
    /// Result of a damage roll.
    /// </summary>
    public readonly struct DamageRollResult
    {
        public readonly bool dealt;
        public readonly bool critical;
        public readonly int diceCount;
        public readonly int dieSides;
        public readonly int bonus;
        public readonly int fatalBonusDamage;
        public readonly int deadlyBonusDamage;
        public readonly int damage;

        public DamageRollResult(
            bool dealt,
            bool critical,
            int diceCount,
            int dieSides,
            int bonus,
            int fatalBonusDamage,
            int deadlyBonusDamage,
            int damage)
        {
            this.dealt = dealt;
            this.critical = critical;
            this.diceCount = diceCount;
            this.dieSides = dieSides;
            this.bonus = bonus;
            this.fatalBonusDamage = fatalBonusDamage;
            this.deadlyBonusDamage = deadlyBonusDamage;
            this.damage = damage;
        }

        public static DamageRollResult None() => new DamageRollResult(false, false, 0, 0, 0, 0, 0, 0);
    }
}
