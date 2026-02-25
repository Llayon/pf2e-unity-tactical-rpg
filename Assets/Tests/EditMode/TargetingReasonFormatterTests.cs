using NUnit.Framework;
using PF2e.Presentation;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class TargetingReasonFormatterTests
    {
        [Test]
        public void Trip_NoHover_ReturnsInfoPrompt()
        {
            var msg = TargetingReasonFormatter.ForModeNoHover(TargetingMode.Trip);

            Assert.AreEqual(TargetingHintTone.Info, msg.Tone);
            Assert.AreEqual("Trip: choose an enemy in reach", msg.Text);
        }

        [Test]
        public void Strike_NoHover_MeleeWeapon_UsesReachPrompt()
        {
            var msg = TargetingReasonFormatter.ForModeNoHover(TargetingMode.Strike, strikeIsRanged: false);

            Assert.AreEqual(TargetingHintTone.Info, msg.Tone);
            Assert.AreEqual("Strike: choose an enemy in reach", msg.Text);
        }

        [Test]
        public void Strike_NoHover_RangedWeapon_UsesRangePrompt()
        {
            var msg = TargetingReasonFormatter.ForModeNoHover(TargetingMode.Strike, strikeIsRanged: true);

            Assert.AreEqual(TargetingHintTone.Info, msg.Tone);
            Assert.AreEqual("Strike: choose an enemy in range", msg.Text);
        }

        [Test]
        public void Trip_Success_ReturnsValidMessage()
        {
            var msg = TargetingReasonFormatter.ForPreview(TargetingMode.Trip, TargetingEvaluationResult.Success());

            Assert.AreEqual(TargetingHintTone.Valid, msg.Tone);
            Assert.AreEqual("Trip: valid target (Athletics vs Reflex DC)", msg.Text);
        }

        [Test]
        public void Trip_WrongTeam_ReturnsChooseEnemy()
        {
            var msg = TargetingReasonFormatter.ForPreview(
                TargetingMode.Trip,
                TargetingEvaluationResult.FromFailure(TargetingFailureReason.WrongTeam));

            Assert.AreEqual(TargetingHintTone.Invalid, msg.Tone);
            Assert.AreEqual("Trip: choose an enemy", msg.Text);
        }

        [Test]
        public void Trip_MissingTrait_ReturnsTraitMessage()
        {
            var msg = TargetingReasonFormatter.ForPreview(
                TargetingMode.Trip,
                TargetingEvaluationResult.FromFailure(TargetingFailureReason.MissingRequiredWeaponTrait));

            Assert.AreEqual(TargetingHintTone.Invalid, msg.Tone);
            Assert.AreEqual("Trip: weapon lacks Trip trait", msg.Text);
        }

        [Test]
        public void Strike_OutOfRange_MeleeWeapon_UsesReachWording()
        {
            var msg = TargetingReasonFormatter.ForPreview(
                TargetingMode.Strike,
                TargetingEvaluationResult.FromFailure(TargetingFailureReason.OutOfRange),
                strikeIsRanged: false);

            Assert.AreEqual(TargetingHintTone.Invalid, msg.Tone);
            Assert.AreEqual("Strike: target is out of reach", msg.Text);
        }

        [Test]
        public void Strike_OutOfRange_RangedWeapon_UsesRangeWording()
        {
            var msg = TargetingReasonFormatter.ForPreview(
                TargetingMode.Strike,
                TargetingEvaluationResult.FromFailure(TargetingFailureReason.OutOfRange),
                strikeIsRanged: true);

            Assert.AreEqual(TargetingHintTone.Invalid, msg.Tone);
            Assert.AreEqual("Strike: target is out of range", msg.Text);
        }

        [Test]
        public void Strike_NoLineOfSight_ReturnsNoLineOfSightMessage()
        {
            var msg = TargetingReasonFormatter.ForPreview(
                TargetingMode.Strike,
                TargetingEvaluationResult.FromFailure(TargetingFailureReason.NoLineOfSight),
                strikeIsRanged: true);

            Assert.AreEqual(TargetingHintTone.Invalid, msg.Tone);
            Assert.AreEqual("Strike: no line of sight", msg.Text);
        }

        [Test]
        public void Escape_NoHover_ReturnsGrapplerPrompt()
        {
            var msg = TargetingReasonFormatter.ForModeNoHover(TargetingMode.Escape);

            Assert.AreEqual(TargetingHintTone.Info, msg.Tone);
            Assert.AreEqual("Escape: choose the creature grappling you", msg.Text);
        }

        [Test]
        public void Escape_NoGrappleRelation_ReturnsGrapplerPrompt()
        {
            var msg = TargetingReasonFormatter.ForPreview(
                TargetingMode.Escape,
                TargetingEvaluationResult.FromFailure(TargetingFailureReason.NoGrappleRelation));

            Assert.AreEqual(TargetingHintTone.Invalid, msg.Tone);
            Assert.AreEqual("Escape: choose the creature grappling you", msg.Text);
        }

        [Test]
        public void ModeNone_NoHover_ReturnsHidden()
        {
            var msg = TargetingReasonFormatter.ForModeNoHover(TargetingMode.None);

            Assert.AreEqual(TargetingHintTone.Hidden, msg.Tone);
            Assert.AreEqual(string.Empty, msg.Text);
        }
    }
}
