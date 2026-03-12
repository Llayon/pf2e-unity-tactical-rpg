using NUnit.Framework;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class CombatLogLinkHelperTests
    {
        [Test]
        public void Link_ProducesCorrectTmpMarkup()
        {
            string markup = CombatLogLinkHelper.Link("atk", "16");
            Assert.AreEqual("<u><color=#D4C4A8><link=\"atk\">16</link></color></u>", markup);
        }

        [Test]
        public void Link_DifferentTokens_ProduceDifferentMarkup()
        {
            string atk = CombatLogLinkHelper.Link("atk", "16");
            string ac = CombatLogLinkHelper.Link("ac", "20");
            Assert.AreNotEqual(atk, ac);
        }

        [Test]
        public void Link_ColorOverride_UsesProvidedColor()
        {
            string markup = CombatLogLinkHelper.Link("atk", "16 Success!", "#90BC79");

            Assert.AreEqual("<u><color=#90BC79><link=\"atk\">16 Success!</link></color></u>", markup);
        }

        [Test]
        public void LinkWithoutUnderline_OmitsUnderlineMarkup()
        {
            string markup = CombatLogLinkHelper.LinkWithoutUnderline("dmg", "3 Slashing", "#DEC38D");

            Assert.AreEqual("<color=#DEC38D><link=\"dmg\">3 Slashing</link></color>", markup);
        }

        [Test]
        public void Link_TwoDifferentTokens_CanCoexistInOneLineMarkup()
        {
            string line = "roll " + CombatLogLinkHelper.Link("atk", "16") + " vs AC " + CombatLogLinkHelper.Link("ac", "20");

            StringAssert.Contains("<link=\"atk\">", line);
            StringAssert.Contains("<link=\"ac\">", line);
        }
    }
}
