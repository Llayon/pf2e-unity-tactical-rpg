using NUnit.Framework;
using PF2e.Core;

namespace PF2e.Tests
{
    [TestFixture]
    public class SkillRulesTests
    {
        [Test]
        public void GetKeyAbilityMod_Athletics_ReturnsStrMod()
        {
            var data = CreateEntity(strength: 16, dexterity: 8, charisma: 10);

            int result = SkillRules.GetKeyAbilityMod(data, SkillType.Athletics);

            Assert.AreEqual(3, result);
        }

        [Test]
        public void GetKeyAbilityMod_Intimidation_ReturnsChaMod()
        {
            var data = CreateEntity(strength: 10, dexterity: 10, charisma: 18);

            int result = SkillRules.GetKeyAbilityMod(data, SkillType.Intimidation);

            Assert.AreEqual(4, result);
        }

        [Test]
        public void GetSaveAbilityMod_Fortitude_ReturnsConMod()
        {
            var data = CreateEntity(constitution: 14, dexterity: 8, wisdom: 8);

            int result = SkillRules.GetSaveAbilityMod(data, SaveType.Fortitude);

            Assert.AreEqual(2, result);
        }

        [Test]
        public void GetSaveAbilityMod_Reflex_ReturnsDexMod()
        {
            var data = CreateEntity(constitution: 8, dexterity: 16, wisdom: 8);

            int result = SkillRules.GetSaveAbilityMod(data, SaveType.Reflex);

            Assert.AreEqual(3, result);
        }

        private static EntityData CreateEntity(
            int strength = 10,
            int dexterity = 10,
            int constitution = 10,
            int wisdom = 10,
            int charisma = 10)
        {
            return new EntityData
            {
                Strength = strength,
                Dexterity = dexterity,
                Constitution = constitution,
                Intelligence = 10,
                Wisdom = wisdom,
                Charisma = charisma
            };
        }
    }
}
