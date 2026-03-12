using NUnit.Framework;
using PF2e.Core;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class CombatLogRichTextTests
    {
        [Test]
        public void DamageAmountAndType_AccentsOnlyAmountAndType()
        {
            string rich = CombatLogRichText.DamageAmountAndType(3, DamageType.Piercing);

            StringAssert.Contains("3", rich);
            StringAssert.Contains("Piercing", rich);
            StringAssert.DoesNotContain("damage", rich);
            StringAssert.DoesNotContain("<b>", rich);
            StringAssert.Contains(CombatUiPalette.DamageAccentHex, rich);
            StringAssert.Contains("<size=108%>", rich);
        }

        [Test]
        public void MinorNote_RendersReducedSecondarySpan()
        {
            string rich = CombatLogRichText.MinorNote(" (Sneak Attack)");

            StringAssert.Contains("<size=92%>", rich);
            StringAssert.Contains(CombatUiPalette.SecondaryNoteHex, rich);
            StringAssert.Contains("(Sneak Attack)", rich);
        }

        [Test]
        public void OutcomeTotal_UsesSizeBump()
        {
            string rich = CombatLogRichText.OutcomeTotal(24);

            StringAssert.Contains("<size=110%>", rich);
            StringAssert.Contains("24", rich);
        }

        [Test]
        public void OutcomeSummary_UsesSingleTokenWithoutDash()
        {
            string rich = CombatLogRichText.OutcomeSummary(17, DegreeOfSuccess.Success);

            StringAssert.Contains("17 Success!", rich);
            StringAssert.DoesNotContain(" - ", rich);
            StringAssert.Contains("Lora SDF", rich);
        }

        [Test]
        public void StatusAppliedSuffix_UsesNarrativeThenStatusToken()
        {
            string rich = CombatLogRichText.StatusAppliedSuffix("Dead");

            StringAssert.Contains("<cspace=0.12>", rich);
            StringAssert.Contains("is now", rich);
            StringAssert.Contains("Dead", rich);
            StringAssert.DoesNotContain("Lora SDF", rich);
            StringAssert.DoesNotContain("<b>", rich);
            StringAssert.Contains(CombatUiPalette.StatusTokenHex, rich);
        }

        [Test]
        public void Defeated_UsesStatusAppliedSuffix()
        {
            string rich = CombatLogRichText.Defeated();

            StringAssert.Contains("is now", rich);
            StringAssert.Contains("Dead", rich);
            StringAssert.DoesNotContain(CombatUiPalette.DefeatedHex, rich);
            StringAssert.DoesNotContain("Lora SDF", rich);
        }
    }
}
