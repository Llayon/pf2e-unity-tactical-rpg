using NUnit.Framework;
using UnityEngine;
using PF2e.Core;

namespace PF2e.Tests
{
    [TestFixture]
    public class DamageResolverTests
    {
        [Test]
        public void RollStrikeDamage_CritWithDeadly_AddsOneExtraDieAfterDoubling()
        {
            var attacker = CreateAttacker(
                diceCount: 1,
                dieSides: 6,
                hasDeadly: true,
                deadlyDieSides: 10,
                dexterity: 18);

            var rng = new FixedRng(dieRolls: new[] { 3, 7 });
            var result = DamageResolver.RollStrikeDamage(attacker, DegreeOfSuccess.CriticalSuccess, rng);

            Assert.IsTrue(result.dealt);
            Assert.IsTrue(result.critical);
            Assert.AreEqual(7, result.deadlyBonusDamage);
            Assert.AreEqual(13, result.damage, "Expected (1d6=3)*2 + deadly d10=7 = 13 for ranged weapon.");
        }

        [Test]
        public void RollStrikeDamage_SuccessWithDeadly_DoesNotAddDeadly()
        {
            var attacker = CreateAttacker(
                diceCount: 1,
                dieSides: 6,
                hasDeadly: true,
                deadlyDieSides: 10,
                dexterity: 18);

            var rng = new FixedRng(dieRolls: new[] { 4, 9 });
            var result = DamageResolver.RollStrikeDamage(attacker, DegreeOfSuccess.Success, rng);

            Assert.AreEqual(0, result.deadlyBonusDamage);
            Assert.AreEqual(4, result.damage);
        }

        [Test]
        public void RollStrikeDamage_CritWithoutDeadly_NoDeadlyBonus()
        {
            var attacker = CreateAttacker(diceCount: 1, dieSides: 6, hasDeadly: false, deadlyDieSides: 0, dexterity: 18);
            var rng = new FixedRng(dieRolls: new[] { 5 });

            var result = DamageResolver.RollStrikeDamage(attacker, DegreeOfSuccess.CriticalSuccess, rng);

            Assert.AreEqual(0, result.deadlyBonusDamage);
            Assert.AreEqual(10, result.damage);
        }

        [Test]
        public void RollStrikeDamage_DeadlyUsesTraitDie_NotBaseWeaponDie()
        {
            var attacker = CreateAttacker(
                diceCount: 1,
                dieSides: 6,
                hasDeadly: true,
                deadlyDieSides: 10,
                dexterity: 18);

            // Deadly roll requests d10; FixedRng clamps to provided value against requested sides.
            var rng = new FixedRng(dieRolls: new[] { 1, 10 });
            var result = DamageResolver.RollStrikeDamage(attacker, DegreeOfSuccess.CriticalSuccess, rng);

            Assert.AreEqual(10, result.deadlyBonusDamage);
            Assert.AreEqual(12, result.damage); // (1*2) + 10
        }

        private static EntityData CreateAttacker(int diceCount, int dieSides, bool hasDeadly, int deadlyDieSides, int dexterity)
        {
            var weaponDef = ScriptableObject.CreateInstance<WeaponDefinition>();
            weaponDef.itemName = "Test Bow";
            weaponDef.isRanged = true;
            weaponDef.rangeIncrementFeet = 60;
            weaponDef.maxRangeIncrements = 6;
            weaponDef.diceCount = diceCount;
            weaponDef.dieSides = dieSides;
            weaponDef.damageType = DamageType.Piercing;
            weaponDef.hasDeadly = hasDeadly;
            weaponDef.deadlyDieSides = deadlyDieSides;

            return new EntityData
            {
                Name = "Attacker",
                Team = Team.Player,
                Level = 1,
                Dexterity = dexterity,
                Strength = 10,
                EquippedWeapon = new WeaponInstance
                {
                    def = weaponDef,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                }
            };
        }

        private sealed class FixedRng : IRng
        {
            private readonly System.Collections.Generic.Queue<int> d20;
            private readonly System.Collections.Generic.Queue<int> dice;

            public FixedRng(System.Collections.Generic.IEnumerable<int> d20Rolls = null, System.Collections.Generic.IEnumerable<int> dieRolls = null)
            {
                d20 = d20Rolls != null ? new System.Collections.Generic.Queue<int>(d20Rolls) : new System.Collections.Generic.Queue<int>();
                dice = dieRolls != null ? new System.Collections.Generic.Queue<int>(dieRolls) : new System.Collections.Generic.Queue<int>();
            }

            public int RollD20()
            {
                return d20.Count > 0 ? d20.Dequeue() : 10;
            }

            public int RollDie(int sides)
            {
                if (sides <= 0) return 0;
                int value = dice.Count > 0 ? dice.Dequeue() : 1;
                return Mathf.Clamp(value, 1, sides);
            }
        }
    }
}
