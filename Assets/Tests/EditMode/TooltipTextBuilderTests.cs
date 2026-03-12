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

            StringAssert.Contains("Attack Roll", text);
            StringAssert.Contains("against AC 18", text);
            StringAssert.Contains("D20 Roll", text);
            StringAssert.Contains("Attack Bonus", text);
            StringAssert.Contains("MAP", text);
            StringAssert.Contains("Result: 16", text);
            StringAssert.Contains("Success!", text);
            StringAssert.Contains("Armor Class (AC)", text);
            StringAssert.Contains("Base AC", text);
            StringAssert.Contains("Total: 18", text);
            StringAssert.Contains("-------------------------", text);
            StringAssert.Contains("<mspace=", text);
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

            StringAssert.Contains("MAP", text);
            StringAssert.Contains("Range Penalty", text);
            StringAssert.Contains("Volley Penalty", text);
            StringAssert.Contains("Aid", text);
            StringAssert.Contains("Cover", text);
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

            StringAssert.Contains("ATHLETICS Check", text);
            StringAssert.Contains("against FORTITUDE DC 17", text);
            StringAssert.Contains("D20 Roll", text);
            StringAssert.Contains("Modifier", text);
            StringAssert.Contains("Aid", text);
            StringAssert.Contains("Result: 22", text);
            StringAssert.Contains("Success!", text);
            StringAssert.Contains("Difficulty Class (FORTITUDE)", text);
            StringAssert.Contains("Total: 17", text);
        }

        [Test]
        public void StrikeDamageBreakdown_NoCritTraits()
        {
            string text = TooltipTextBuilder.StrikeDamageBreakdown(
                totalDamage: 8,
                damageType: DamageType.Slashing);

            StringAssert.Contains("Damage Roll", text);
            StringAssert.Contains("Base Damage", text);
            StringAssert.Contains("Total: 8 SLASHING", text);
            StringAssert.Contains("Damage Type", text);
            StringAssert.Contains("Swords, axes", text);
            StringAssert.Contains("<mspace=", text);
        }

        [Test]
        public void StrikeDamageBreakdown_WithFatalAndDeadly()
        {
            string text = TooltipTextBuilder.StrikeDamageBreakdown(
                totalDamage: 19,
                damageType: DamageType.Piercing,
                fatalBonusDamage: 4,
                deadlyBonusDamage: 6);

            StringAssert.Contains("Damage Roll", text);
            StringAssert.Contains("Base Damage", text);
            StringAssert.Contains("Fatal Bonus", text);
            StringAssert.Contains("Deadly Bonus", text);
            StringAssert.Contains("Total: 19 PIERCING", text);
            StringAssert.Contains("Damage Type", text);
            StringAssert.Contains("Puncturing and impaling attacks", text);
        }

        [Test]
        public void BuildResultExtendedBody_AppendsRuleBlock()
        {
            string standardBody = "Attack Roll\nResult: 16 Success!";
            string text = TooltipTextBuilder.BuildResultExtendedBody(
                standardBody,
                "Constitution",
                "Ability Score",
                "Constitution measures health and stamina.");

            StringAssert.Contains("Attack Roll", text);
            StringAssert.Contains("Constitution", text);
            StringAssert.Contains("ABILITY SCORE", text);
            StringAssert.Contains("Constitution measures health and stamina.", text);
        }
    }
}
