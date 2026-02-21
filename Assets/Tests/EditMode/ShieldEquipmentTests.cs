using NUnit.Framework;
using UnityEngine;
using PF2e.Core;

namespace PF2e.Tests
{
    [TestFixture]
    public class ShieldEquipmentTests
    {
        private static ShieldDefinition CreateShieldDef(int acBonus = 2, int hardness = 5, int maxHP = 20)
        {
            var def = ScriptableObject.CreateInstance<ShieldDefinition>();
            def.itemName = "Test Shield";
            def.acBonus = acBonus;
            def.hardness = hardness;
            def.maxHP = maxHP;
            return def;
        }

        [Test]
        public void CreateEquipped_InitializesCurrentHpFromDefinition()
        {
            var def = CreateShieldDef(maxHP: 18);
            try
            {
                var shield = ShieldInstance.CreateEquipped(def);
                Assert.AreSame(def, shield.def);
                Assert.AreEqual(18, shield.currentHP);
                Assert.IsFalse(shield.isRaised);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void ACBonus_DependsOnRaisedState()
        {
            var def = CreateShieldDef(acBonus: 2);
            try
            {
                var shield = ShieldInstance.CreateEquipped(def);
                Assert.AreEqual(0, shield.ACBonus);

                shield.isRaised = true;
                Assert.AreEqual(2, shield.ACBonus);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void ACBonus_WithoutDefinition_ReturnsZero()
        {
            var shield = default(ShieldInstance);
            shield.isRaised = true;
            Assert.AreEqual(0, shield.ACBonus);
        }

        [Test]
        public void IsBroken_UsesCurrentHpWhenShieldIsEquipped()
        {
            var def = CreateShieldDef(maxHP: 12);
            try
            {
                var shield = ShieldInstance.CreateEquipped(def);
                Assert.IsFalse(shield.IsBroken);

                shield.currentHP = 0;
                Assert.IsTrue(shield.IsBroken);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void EntityData_SetShieldRaised_MutatesEquippedShield()
        {
            var def = CreateShieldDef();
            try
            {
                var data = new EntityData
                {
                    EquippedShield = ShieldInstance.CreateEquipped(def)
                };

                data.SetShieldRaised(true);
                Assert.IsTrue(data.EquippedShield.isRaised);

                data.SetShieldRaised(false);
                Assert.IsFalse(data.EquippedShield.isRaised);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void EntityData_ApplyShieldDamage_MutatesEquippedShieldAndClampsToZero()
        {
            var def = CreateShieldDef(maxHP: 10);
            try
            {
                var data = new EntityData
                {
                    EquippedShield = ShieldInstance.CreateEquipped(def)
                };

                data.SetShieldRaised(true);
                data.ApplyShieldDamage(4);
                Assert.AreEqual(6, data.EquippedShield.currentHP);
                Assert.IsTrue(data.EquippedShield.isRaised);

                data.ApplyShieldDamage(99);
                Assert.AreEqual(0, data.EquippedShield.currentHP);
                Assert.IsFalse(data.EquippedShield.isRaised);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }
    }
}
