namespace PF2e.Core
{
    /// <summary>
    /// Pure damage resolution for strikes.
    /// </summary>
    public static class DamageResolver
    {
        public static DamageRollResult RollStrikeDamage(EntityData attacker, DegreeOfSuccess degree, IRng rng)
        {
            if (attacker == null)
                return DamageRollResult.None();

            if (degree != DegreeOfSuccess.Success && degree != DegreeOfSuccess.CriticalSuccess)
                return DamageRollResult.None();

            bool crit = degree == DegreeOfSuccess.CriticalSuccess;

            int diceCount = attacker.EquippedWeapon.EffectiveDiceCount;
            int dieSides = attacker.EquippedWeapon.DieSides;
            int bonus = attacker.WeaponDamageBonus;

            int damage = (diceCount > 0 && dieSides > 0)
                ? DamageCalculator.RollWeaponDamage(rng, diceCount, dieSides, bonus, crit)
                : 0;

            int deadlyBonusDamage = 0;
            if (crit && attacker.EquippedWeapon.HasDeadly)
            {
                int deadlyDieSides = attacker.EquippedWeapon.DeadlyDieSides;
                if (deadlyDieSides > 0)
                {
                    deadlyBonusDamage = rng.RollDie(deadlyDieSides);
                    damage += deadlyBonusDamage;
                }
            }

            return new DamageRollResult(true, crit, diceCount, dieSides, bonus, deadlyBonusDamage, damage);
        }
    }
}
