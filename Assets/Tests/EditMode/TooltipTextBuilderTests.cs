using NUnit.Framework;
using PF2e.Core;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class TooltipTextBuilderTests
    {
        [Test]
        public void StrikeAttackBreakdown_BasicMelee()
        {
            string text = TooltipTextBuilder.StrikeAttackBreakdown(
                naturalRoll: 12,
                attackBonus: 9,
                mapPenalty: -5,
                rangePenalty: 0,
                volleyPenalty: 0,
                aidCircumstanceBonus: 0,
                total: 16);

            Assert.AreEqual("d20(12) + ATK(+9) + MAP(-5) = 16", text);
        }

        [Test]
        public void StrikeAttackBreakdown_WithAllModifiers()
        {
            string text = TooltipTextBuilder.StrikeAttackBreakdown(
                naturalRoll: 12,
                attackBonus: 9,
                mapPenalty: -5,
                rangePenalty: -2,
                volleyPenalty: -2,
                aidCircumstanceBonus: 2,
                total: 14);

            StringAssert.Contains("RNG(-2)", text);
            StringAssert.Contains("VOLLEY(-2)", text);
            StringAssert.Contains("AID(+2)", text);
        }

        [Test]
        public void StrikeDefenseBreakdown_WithCover()
        {
            string text = TooltipTextBuilder.StrikeDefenseBreakdown(baseAc: 18, coverBonus: 2);
            Assert.AreEqual("AC 18 + COVER(+2) = 20", text);
        }

        [Test]
        public void StrikeDefenseBreakdown_NoCover()
        {
            string text = TooltipTextBuilder.StrikeDefenseBreakdown(baseAc: 18, coverBonus: 0);
            Assert.AreEqual("AC 18 = 18", text);
        }

        [Test]
        public void SkillCheckBreakdown_WithAid()
        {
            var roll = new CheckRoll(14, 8, CheckSource.Skill(SkillType.Athletics));
            string text = TooltipTextBuilder.SkillCheckBreakdown(roll, aidCircumstanceBonus: 2);
            Assert.AreEqual("ATHLETICS d20(14) +8 + AID(+2) = 22", text);
        }
    }
}
