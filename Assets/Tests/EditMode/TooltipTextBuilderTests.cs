using NUnit.Framework;
using PF2e.Core;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class TooltipTextBuilderTests
    {
        [Test]
        public void StrikeResultBreakdown_BasicMelee()
        {
            string text = TooltipTextBuilder.StrikeResultBreakdown(
                naturalRoll: 12,
                attackBonus: 9,
                mapPenalty: -5,
                rangePenalty: 0,
                volleyPenalty: 0,
                aidCircumstanceBonus: 0,
                total: 16,
                degree: DegreeOfSuccess.Success,
                baseAc: 18,
                coverBonus: 0);

            StringAssert.Contains("Attack Roll vs AC 18", text);
            StringAssert.Contains("D20 Roll: 12", text);
            StringAssert.Contains("Attack Bonus: +9", text);
            StringAssert.Contains("MAP: -5", text);
            StringAssert.Contains("Total: 16", text);
            StringAssert.Contains("Degree: Success!", text);
            StringAssert.Contains("Armor Class", text);
            StringAssert.Contains("Base AC: 18", text);
            StringAssert.Contains("Total: 18", text);
        }

        [Test]
        public void StrikeResultBreakdown_WithAllModifiers()
        {
            string text = TooltipTextBuilder.StrikeResultBreakdown(
                naturalRoll: 12,
                attackBonus: 9,
                mapPenalty: -5,
                rangePenalty: -2,
                volleyPenalty: -2,
                aidCircumstanceBonus: 2,
                total: 14,
                degree: DegreeOfSuccess.Success,
                baseAc: 18,
                coverBonus: 2);

            StringAssert.Contains("MAP: -5", text);
            StringAssert.Contains("Range Penalty: -2", text);
            StringAssert.Contains("Volley Penalty: -2", text);
            StringAssert.Contains("Aid: +2", text);
            StringAssert.Contains("Cover: +2", text);
            StringAssert.Contains("Total: 20", text);
        }

        [Test]
        public void SkillCheckResultBreakdown_Basic()
        {
            var roll = new CheckRoll(14, 8, CheckSource.Skill(SkillType.Athletics));
            string text = TooltipTextBuilder.SkillCheckResultBreakdown(
                roll,
                CheckSource.Save(SaveType.Fortitude),
                dc: 17,
                degree: DegreeOfSuccess.Success,
                aidCircumstanceBonus: 2);

            StringAssert.Contains("ATHLETICS Check vs FORTITUDE DC 17", text);
            StringAssert.Contains("D20 Roll: 14", text);
            StringAssert.Contains("Modifier: +8", text);
            StringAssert.Contains("Aid: +2", text);
            StringAssert.Contains("Total: 22", text);
            StringAssert.Contains("Degree: Success!", text);
            StringAssert.Contains("FORTITUDE DC: 17", text);
        }

        [Test]
        public void StrikeDamageBreakdown_NoCritTraits()
        {
            string text = TooltipTextBuilder.StrikeDamageBreakdown(
                totalDamage: 8,
                damageType: DamageType.Slashing);

            Assert.AreEqual(
                "Damage Roll\n" +
                "Base Damage: 8\n" +
                "Total: 8 SLASHING",
                text);
        }

        [Test]
        public void StrikeDamageBreakdown_WithFatalAndDeadly()
        {
            string text = TooltipTextBuilder.StrikeDamageBreakdown(
                totalDamage: 19,
                damageType: DamageType.Piercing,
                fatalBonusDamage: 4,
                deadlyBonusDamage: 6);

            Assert.AreEqual(
                "Damage Roll\n" +
                "Base Damage: 9\n" +
                "Fatal Bonus: +4\n" +
                "Deadly Bonus: +6\n" +
                "Total: 19 PIERCING",
                text);
        }
    }
}
