using UnityEngine;

namespace PF2e.Core
{
    public static class DamageCalculator
    {
        public static int RollDice(int count, int sides)
        {
            int sum = 0;
            for (int i = 0; i < count; i++)
                sum += Random.Range(1, sides + 1);
            return sum;
        }

        /// <summary>
        /// Legacy overload using Unity Random (kept for compatibility).
        /// </summary>
        public static int RollWeaponDamage(int diceCount, int dieSides, int bonus, bool critical)
        {
            int dmg = RollDice(diceCount, dieSides) + bonus;
            if (dmg < 1) dmg = 1;
            return critical ? dmg * 2 : dmg;
        }

        /// <summary>
        /// Testable overload accepting IRng for deterministic testing.
        /// </summary>
        public static int RollWeaponDamage(IRng rng, int diceCount, int dieSides, int bonus, bool critical)
        {
            if (rng == null)
                rng = UnityRng.Shared;

            int sum = 0;
            for (int i = 0; i < diceCount; i++)
                sum += rng.RollDie(dieSides);

            int dmg = sum + bonus;
            if (dmg < 1) dmg = 1;
            return critical ? dmg * 2 : dmg;
        }
    }
}
