using NUnit.Framework;
using PF2e.Core;
using PF2e.Presentation;

namespace PF2e.Tests
{
    public class EncounterEndTextMapTests
    {
        [TestCase(EncounterResult.Victory, "Victory", "All enemies defeated.")]
        [TestCase(EncounterResult.Defeat, "Defeat", "All players defeated.")]
        [TestCase(EncounterResult.Aborted, "Encounter Ended", "Combat was ended manually.")]
        [TestCase(EncounterResult.Unknown, "Encounter Ended", "Combat was ended manually.")]
        public void For_ReturnsExpectedText(EncounterResult result, string expectedTitle, string expectedSubtitle)
        {
            EncounterEndText text = EncounterEndTextMap.For(result);

            Assert.AreEqual(expectedTitle, text.Title);
            Assert.AreEqual(expectedSubtitle, text.Subtitle);
        }
    }
}
