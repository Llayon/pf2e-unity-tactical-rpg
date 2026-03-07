using NUnit.Framework;
using PF2e.Core;
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
        public void Strike_SuccessWithConcealmentWarning_ReturnsWarningMessage()
        {
            var msg = TargetingReasonFormatter.ForPreview(
                TargetingMode.Strike,
                TargetingEvaluationResult.SuccessWithWarning(TargetingWarningReason.ConcealmentFlatCheck),
                strikeIsRanged: true);

            Assert.AreEqual(TargetingHintTone.Warning, msg.Tone);
            Assert.AreEqual("Strike: valid target (concealed: DC 5 flat check)", msg.Text);
        }

        [Test]
        public void Strike_SuccessWithCoverWarning_ReturnsWarningMessage()
        {
            var msg = TargetingReasonFormatter.ForPreview(
                TargetingMode.Strike,
                TargetingEvaluationResult.SuccessWithWarning(TargetingWarningReason.CoverAcBonus),
                strikeIsRanged: true);

            Assert.AreEqual(TargetingHintTone.Warning, msg.Tone);
            Assert.AreEqual("Strike: valid target (cover: +2 AC)", msg.Text);
        }

        [Test]
        public void Strike_SuccessWithCoverAndConcealmentWarnings_UsesCombinedRiskMessage()
        {
            var msg = TargetingReasonFormatter.ForPreview(
                TargetingMode.Strike,
                TargetingEvaluationResult.SuccessWithWarning(
                    TargetingWarningReason.CoverAcBonus | TargetingWarningReason.ConcealmentFlatCheck),
                strikeIsRanged: true);

            Assert.AreEqual(TargetingHintTone.Warning, msg.Tone);
            Assert.AreEqual("Strike: valid target (cover: +2 AC; concealed: DC 5 flat check)", msg.Text);
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
        public void Aid_NoHover_ReturnsAllyPrompt()
        {
            var msg = TargetingReasonFormatter.ForModeNoHover(TargetingMode.Aid);

            Assert.AreEqual(TargetingHintTone.Info, msg.Tone);
            Assert.AreEqual("Aid: choose an ally in reach", msg.Text);
        }

        [Test]
        public void Aid_Success_ReturnsValidAllyMessage()
        {
            var msg = TargetingReasonFormatter.ForPreview(TargetingMode.Aid, TargetingEvaluationResult.Success());

            Assert.AreEqual(TargetingHintTone.Valid, msg.Tone);
            Assert.AreEqual("Aid: valid ally target", msg.Text);
        }

        [Test]
        public void Aid_WrongTeam_ReturnsChooseAllyMessage()
        {
            var msg = TargetingReasonFormatter.ForPreview(
                TargetingMode.Aid,
                TargetingEvaluationResult.FromFailure(TargetingFailureReason.WrongTeam));

            Assert.AreEqual(TargetingHintTone.Invalid, msg.Tone);
            Assert.AreEqual("Aid: choose an ally", msg.Text);
        }

        [Test]
        public void ReadyStrike_NoHover_ReturnsEnemyPrompt()
        {
            var msg = TargetingReasonFormatter.ForModeNoHover(TargetingMode.ReadyStrike);

            Assert.AreEqual(TargetingHintTone.Info, msg.Tone);
            Assert.AreEqual("Ready Strike: choose an enemy in reach", msg.Text);
        }

        [Test]
        public void ReadyStrike_WrongTeam_ReturnsChooseEnemyMessage()
        {
            var msg = TargetingReasonFormatter.ForPreview(
                TargetingMode.ReadyStrike,
                TargetingEvaluationResult.FromFailure(TargetingFailureReason.WrongTeam));

            Assert.AreEqual(TargetingHintTone.Invalid, msg.Tone);
            Assert.AreEqual("Ready Strike: choose an enemy", msg.Text);
        }

        [Test]
        public void Jump_NoHover_ReturnsLandingPrompt()
        {
            var msg = TargetingReasonFormatter.ForModeNoHover(TargetingMode.Jump);

            Assert.AreEqual(TargetingHintTone.Info, msg.Tone);
            Assert.AreEqual("Jump: choose a landing cell", msg.Text);
        }

        [Test]
        public void JumpPreview_LongJumpWithEnoughActions_ReturnsValidDcMessage()
        {
            var preview = JumpPreviewResult.Valid(
                jumpType: JumpType.LongJump,
                actionCost: 2,
                requiresCheck: true,
                dc: 15,
                requiresRunUp: true,
                runUpFeet: 10,
                jumpDistanceFeet: 15,
                takeoffCell: new UnityEngine.Vector3Int(1, 0, 0),
                landingCell: new UnityEngine.Vector3Int(4, 0, 0));

            var msg = TargetingReasonFormatter.ForJumpPreview(in preview, actionsRemaining: 2);
            Assert.AreEqual(TargetingHintTone.Valid, msg.Tone);
            Assert.AreEqual("Jump: Long Jump [2], Athletics vs DC 15", msg.Text);
        }

        [Test]
        public void JumpPreview_ValidButNotEnoughActions_ReturnsInvalidCostMessage()
        {
            var preview = JumpPreviewResult.Valid(
                jumpType: JumpType.HighJump,
                actionCost: 2,
                requiresCheck: true,
                dc: 30,
                requiresRunUp: true,
                runUpFeet: 10,
                jumpDistanceFeet: 5,
                takeoffCell: new UnityEngine.Vector3Int(1, 0, 0),
                landingCell: new UnityEngine.Vector3Int(1, 1, 0));

            var msg = TargetingReasonFormatter.ForJumpPreview(in preview, actionsRemaining: 1);
            Assert.AreEqual(TargetingHintTone.Invalid, msg.Tone);
            Assert.AreEqual("Jump: needs 2 action(s), only 1 left", msg.Text);
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
