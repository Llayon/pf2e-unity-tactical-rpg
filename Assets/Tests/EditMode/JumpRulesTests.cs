using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PF2e.Core;

namespace PF2e.Tests
{
    [TestFixture]
    public class JumpRulesTests
    {
        [Test]
        public void GetLeapRangeFeet_UsesPf2eThresholds()
        {
            Assert.AreEqual(0, JumpRules.GetLeapRangeFeet(10));
            Assert.AreEqual(10, JumpRules.GetLeapRangeFeet(25));
            Assert.AreEqual(15, JumpRules.GetLeapRangeFeet(30));
        }

        [Test]
        public void GetLongJumpDc_IsDistanceInFeet_NotFixed()
        {
            Assert.AreEqual(15, JumpRules.GetLongJumpDc(15));
            Assert.AreEqual(20, JumpRules.GetLongJumpDc(20));
            Assert.AreEqual(35, JumpRules.GetLongJumpDc(35));
        }

        [Test]
        public void ClassifyJump_UpwardToNextLevel_IsHighJump()
        {
            var start = new Vector3Int(0, 0, 0);
            var target = new Vector3Int(1, 1, 0);

            var result = JumpRules.ClassifyJump(start, target, speedFeet: 25);

            Assert.AreEqual(JumpType.HighJump, result);
        }

        [Test]
        public void ClassifyJump_SameElevationBeyondLeapRange_IsLongJump()
        {
            var start = new Vector3Int(0, 0, 0);
            var target = new Vector3Int(3, 0, 0); // 15 ft

            var result = JumpRules.ClassifyJump(start, target, speedFeet: 25);

            Assert.AreEqual(JumpType.LongJump, result);
        }

        [Test]
        public void IsLeapPossible_UpOneLevelFiveFeetRise_ReturnsFalse()
        {
            var start = new Vector3Int(0, 0, 0);
            var target = new Vector3Int(1, 1, 0); // +5 ft vertical rise

            bool possible = JumpRules.IsLeapPossible(start, target, speedFeet: 25);

            Assert.IsFalse(possible);
        }

        [Test]
        public void GetLongJumpSuccessDistanceFeet_RoundsDownAndCapsBySpeed()
        {
            int rounded = JumpRules.GetLongJumpSuccessDistanceFeet(checkTotal: 19, speedFeet: 30);
            int capped = JumpRules.GetLongJumpSuccessDistanceFeet(checkTotal: 42, speedFeet: 25);

            Assert.AreEqual(15, rounded);
            Assert.AreEqual(25, capped);
        }

        [Test]
        public void ResolvePreview_DirectLeap_ReturnsOneActionLeap()
        {
            var actor = new Vector3Int(0, 0, 0);
            var landing = new Vector3Int(2, 0, 0); // 10 ft

            var preview = JumpReachabilityResolver.ResolvePreview(
                actor,
                speedFeet: 25,
                landing,
                strideReachabilityFeet: null,
                isLandingCellValid: _ => true);

            Assert.IsTrue(preview.isValid);
            Assert.AreEqual(JumpType.Leap, preview.jumpType);
            Assert.AreEqual(1, preview.actionCost);
            Assert.IsFalse(preview.requiresCheck);
            Assert.AreEqual(actor, preview.takeoffCell);
        }

        [Test]
        public void ResolvePreview_LongJump_UsesBestRunupCandidateAndDynamicDc()
        {
            var actor = new Vector3Int(0, 0, 0);
            var landing = new Vector3Int(6, 0, 0); // 30 ft

            var reachability = new Dictionary<Vector3Int, int>
            {
                [new Vector3Int(2, 0, 0)] = 10, // jump 20 -> DC 20
                [new Vector3Int(3, 0, 0)] = 15, // jump 15 -> DC 15 (better)
                [new Vector3Int(4, 0, 0)] = 20, // jump 10 -> would be Leap, ignored
            };

            var preview = JumpReachabilityResolver.ResolvePreview(
                actor,
                speedFeet: 25,
                landing,
                reachability,
                isLandingCellValid: _ => true);

            Assert.IsTrue(preview.isValid);
            Assert.AreEqual(JumpType.LongJump, preview.jumpType);
            Assert.AreEqual(2, preview.actionCost);
            Assert.IsTrue(preview.requiresCheck);
            Assert.AreEqual(15, preview.dc);
            Assert.AreEqual(new Vector3Int(3, 0, 0), preview.takeoffCell);
            Assert.AreEqual(15, preview.jumpDistanceFeet);
        }

        [Test]
        public void ResolvePreview_HighJump_UsesFixedDc30()
        {
            var actor = new Vector3Int(0, 0, 0);
            var landing = new Vector3Int(2, 1, 0);

            var reachability = new Dictionary<Vector3Int, int>
            {
                [new Vector3Int(2, 0, 0)] = 10
            };

            var preview = JumpReachabilityResolver.ResolvePreview(
                actor,
                speedFeet: 25,
                landing,
                reachability,
                isLandingCellValid: _ => true);

            Assert.IsTrue(preview.isValid);
            Assert.AreEqual(JumpType.HighJump, preview.jumpType);
            Assert.IsTrue(preview.requiresCheck);
            Assert.AreEqual(JumpRules.HighJumpDc, preview.dc);
            Assert.AreEqual(2, preview.actionCost);
        }

        [Test]
        public void ResolvePreview_NoCandidateWithRequiredRunup_ReturnsMissingRunUp()
        {
            var actor = new Vector3Int(0, 0, 0);
            var landing = new Vector3Int(4, 0, 0); // 20 ft

            var reachability = new Dictionary<Vector3Int, int>
            {
                [new Vector3Int(1, 0, 0)] = 5,
                [new Vector3Int(0, 0, 0)] = 0
            };

            var preview = JumpReachabilityResolver.ResolvePreview(
                actor,
                speedFeet: 25,
                landing,
                reachability,
                isLandingCellValid: _ => true);

            Assert.IsFalse(preview.isValid);
            Assert.AreEqual(JumpFailureReason.MissingRunUp, preview.failureReason);
        }

        [Test]
        public void ResolvePreview_InvalidLanding_ReturnsInvalidLandingReason()
        {
            var actor = new Vector3Int(0, 0, 0);
            var landing = new Vector3Int(2, 0, 0);

            var preview = JumpReachabilityResolver.ResolvePreview(
                actor,
                speedFeet: 25,
                landing,
                strideReachabilityFeet: null,
                isLandingCellValid: _ => false);

            Assert.IsFalse(preview.isValid);
            Assert.AreEqual(JumpFailureReason.InvalidLanding, preview.failureReason);
        }
    }
}
