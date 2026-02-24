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
            int baseDieSidesForThisRoll = dieSides;
            int bonus = attacker.WeaponDamageBonus;
            int fatalBonusDamage = 0;

            if (crit && attacker.EquippedWeapon.HasFatal)
            {
                // PF2e Fatal (Phase 25.3): crit upgrades base weapon dice to the fatal die size.
                baseDieSidesForThisRoll = attacker.EquippedWeapon.FatalDieSides;
            }

            int damage = (diceCount > 0 && baseDieSidesForThisRoll > 0)
                ? DamageCalculator.RollWeaponDamage(rng, diceCount, baseDieSidesForThisRoll, bonus, crit)
                : 0;

            if (crit && attacker.EquippedWeapon.HasFatal)
            {
                int fatalDieSides = attacker.EquippedWeapon.FatalDieSides;
                if (fatalDieSides > 0)
                {
                    // PF2e Fatal timing decision (Phase 25.3): extra fatal die is added after doubling, not doubled.
                    fatalBonusDamage = rng.RollDie(fatalDieSides);
                    damage += fatalBonusDamage;
                }
            }

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

            return new DamageRollResult(
                true,
                crit,
                diceCount,
                baseDieSidesForThisRoll,
                bonus,
                fatalBonusDamage,
                deadlyBonusDamage,
                damage);
        }
    }
}
