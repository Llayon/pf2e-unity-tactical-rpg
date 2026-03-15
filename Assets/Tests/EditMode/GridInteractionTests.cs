using NUnit.Framework;
using PF2e.Grid;
using UnityEngine;

namespace PF2e.Tests
{
    [TestFixture]
    public class GridInteractionTests
    {
        private const float IdleWorldRefreshInterval = 0.1f;
        private const float MouseDeadZoneSqr = 1f;

        [Test]
        public void GT030_FirstFrameWithoutCachedState_PerformsWorldRaycast()
        {
            bool shouldPerform = GridInteraction.ShouldPerformWorldRaycast(
                false,
                Vector2.zero,
                Vector2.zero,
                false,
                false,
                false,
                0f,
                IdleWorldRefreshInterval,
                MouseDeadZoneSqr);

            Assert.IsTrue(shouldPerform);
        }

        [Test]
        public void GT031_MouseDeltaAboveDeadZone_PerformsWorldRaycast()
        {
            bool shouldPerform = GridInteraction.ShouldPerformWorldRaycast(
                true,
                new Vector2(1.1f, 0f),
                Vector2.zero,
                false,
                false,
                false,
                0.05f,
                IdleWorldRefreshInterval,
                MouseDeadZoneSqr);

            Assert.IsTrue(shouldPerform);
        }

        [Test]
        public void GT032_MouseDeltaWithinDeadZone_DoesNotPerformWorldRaycast()
        {
            bool shouldPerform = GridInteraction.ShouldPerformWorldRaycast(
                true,
                new Vector2(1f, 0f),
                Vector2.zero,
                false,
                false,
                false,
                0.05f,
                IdleWorldRefreshInterval,
                MouseDeadZoneSqr);

            Assert.IsFalse(shouldPerform);
        }

        [Test]
        public void GT033_CameraChanged_PerformsWorldRaycast()
        {
            bool shouldPerform = GridInteraction.ShouldPerformWorldRaycast(
                true,
                Vector2.zero,
                Vector2.zero,
                true,
                false,
                false,
                0.05f,
                IdleWorldRefreshInterval,
                MouseDeadZoneSqr);

            Assert.IsTrue(shouldPerform);
        }

        [Test]
        public void GT034_PointerPressedThisFrame_PerformsWorldRaycast()
        {
            bool shouldPerform = GridInteraction.ShouldPerformWorldRaycast(
                true,
                Vector2.zero,
                Vector2.zero,
                false,
                true,
                false,
                0.05f,
                IdleWorldRefreshInterval,
                MouseDeadZoneSqr);

            Assert.IsTrue(shouldPerform);
        }

        [Test]
        public void GT035_JustLeftUi_PerformsWorldRaycast()
        {
            bool shouldPerform = GridInteraction.ShouldPerformWorldRaycast(
                true,
                Vector2.zero,
                Vector2.zero,
                false,
                false,
                true,
                0.05f,
                IdleWorldRefreshInterval,
                MouseDeadZoneSqr);

            Assert.IsTrue(shouldPerform);
        }

        [Test]
        public void GT036_IdleRefreshTimeout_PerformsWorldRaycast()
        {
            bool shouldPerform = GridInteraction.ShouldPerformWorldRaycast(
                true,
                Vector2.zero,
                Vector2.zero,
                false,
                false,
                false,
                IdleWorldRefreshInterval,
                IdleWorldRefreshInterval,
                MouseDeadZoneSqr);

            Assert.IsTrue(shouldPerform);
        }

        [Test]
        public void GT037_NoStateChangeBeforeTimeout_DoesNotPerformWorldRaycast()
        {
            bool shouldPerform = GridInteraction.ShouldPerformWorldRaycast(
                true,
                Vector2.zero,
                Vector2.zero,
                false,
                false,
                false,
                0.05f,
                IdleWorldRefreshInterval,
                MouseDeadZoneSqr);

            Assert.IsFalse(shouldPerform);
        }
    }
}
