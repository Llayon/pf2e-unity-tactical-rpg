using NUnit.Framework;
using UnityEngine;
using PF2e.Core;

namespace PF2e.Tests
{
    [TestFixture]
    public class ShieldBlockRulesTests
    {
        [Test]
        public void Calculate_DamageGreaterThanHardness_SplitsReductionAndShieldDamage()
        {
            var def = CreateShieldDef(hardness: 5, maxHP: 20);
            try
            {
                var shield = ShieldInstance.CreateEquipped(def);
                var result = ShieldBlockRules.Calculate(shield, incomingDamage: 10);

                Assert.AreEqual(5, result.targetDamageReduction);
                Assert.AreEqual(5, result.shieldSelfDamage);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void Calculate_DamageLowerThanHardness_ReducesAllDamage()
        {
            var def = CreateShieldDef(hardness: 5, maxHP: 20);
            try
            {
                var shield = ShieldInstance.CreateEquipped(def);
                var result = ShieldBlockRules.Calculate(shield, incomingDamage: 3);

                Assert.AreEqual(3, result.targetDamageReduction);
                Assert.AreEqual(0, result.shieldSelfDamage);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void Calculate_ZeroDamage_ReturnsZeroes()
        {
            var def = CreateShieldDef(hardness: 5, maxHP: 20);
            try
            {
                var shield = ShieldInstance.CreateEquipped(def);
                var result = ShieldBlockRules.Calculate(shield, incomingDamage: 0);

                Assert.AreEqual(0, result.targetDamageReduction);
                Assert.AreEqual(0, result.shieldSelfDamage);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void Calculate_BrokenShield_ReturnsZeroes()
        {
            var def = CreateShieldDef(hardness: 5, maxHP: 20);
            try
            {
                var shield = ShieldInstance.CreateEquipped(def);
                shield.currentHP = 0;

                var result = ShieldBlockRules.Calculate(shield, incomingDamage: 10);
                Assert.AreEqual(0, result.targetDamageReduction);
                Assert.AreEqual(0, result.shieldSelfDamage);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void Calculate_UnequippedShield_ReturnsZeroes()
        {
            var result = ShieldBlockRules.Calculate(default, incomingDamage: 10);
            Assert.AreEqual(0, result.targetDamageReduction);
            Assert.AreEqual(0, result.shieldSelfDamage);
        }

        private static ShieldDefinition CreateShieldDef(int hardness, int maxHP)
        {
            var def = ScriptableObject.CreateInstance<ShieldDefinition>();
            def.itemName = "TestShield";
            def.hardness = hardness;
            def.maxHP = maxHP;
            def.acBonus = 2;
            return def;
        }
    }
}
