using NUnit.Framework;
using PF2e.Core;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class RollBreakdownFormatterTests
    {
        [Test]
        public void FormatRoll_UsesShortSourceAndSignedModifier()
        {
            var roll = new CheckRoll(14, 7, CheckSource.Perception());

            string text = RollBreakdownFormatter.FormatRoll(roll);

            Assert.AreEqual("PRC d20(14) +7 = 21", text);
        }

        [Test]
        public void FormatVsDc_UsesDefenseSourceAndDc()
        {
            var roll = new CheckRoll(12, 5, CheckSource.Skill(SkillType.Athletics));

            string text = RollBreakdownFormatter.FormatVsDc(roll, CheckSource.Save(SaveType.Fortitude), 18);

            Assert.AreEqual("ATHLETICS d20(12) +5 = 17 vs FORTITUDE DC 18", text);
        }

        [Test]
        public void FormatCheckVsDcLabel_UsesUiSourceNames()
        {
            string text = RollBreakdownFormatter.FormatCheckVsDcLabel(
                CheckSource.Skill(SkillType.Intimidation),
                CheckSource.Save(SaveType.Will));

            Assert.AreEqual("Intimidation vs Will DC", text);
        }
    }
}
