using NUnit.Framework;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class CombatLogRetentionPolicyTests
    {
        [Test]
        public void IsCapped_ReturnsTrue_ForPositiveLimit()
        {
            Assert.IsTrue(CombatLogRetentionPolicy.IsCapped(80));
        }

        [Test]
        public void IsCapped_ReturnsFalse_ForZeroOrNegativeLimit()
        {
            Assert.IsFalse(CombatLogRetentionPolicy.IsCapped(0));
            Assert.IsFalse(CombatLogRetentionPolicy.IsCapped(-1));
        }

        [Test]
        public void BuildNoticeText_FormatsCappedAndUnlimitedModes()
        {
            string capped = CombatLogRetentionPolicy.BuildNoticeText(80);
            string unlimited = CombatLogRetentionPolicy.BuildNoticeText(0);

            Assert.AreEqual("Showing last 80 lines", capped);
            Assert.AreEqual("Showing all lines", unlimited);
        }
    }
}
