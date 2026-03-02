using NUnit.Framework;
using UnityEngine;
using PF2e.Core;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class AidPreparedIndicatorPresenterTests
    {
        [Test]
        public void FormatLabelText_NoPreparedAid_ReturnsEmpty()
        {
            var text = AidPreparedIndicatorPresenter.FormatLabelText(0, "!", "x{0}");

            Assert.AreEqual(string.Empty, text);
        }

        [Test]
        public void FormatLabelText_SinglePreparedAid_UsesSingleText()
        {
            var text = AidPreparedIndicatorPresenter.FormatLabelText(1, "!", "x{0}");

            Assert.AreEqual("!", text);
        }

        [Test]
        public void FormatLabelText_MultiplePreparedAid_UsesCountFormat()
        {
            var text = AidPreparedIndicatorPresenter.FormatLabelText(3, "!", "x{0}");

            Assert.AreEqual("x3", text);
        }

        [Test]
        public void FormatLabelText_InvalidCountFormat_FallsBackToNumericCount()
        {
            var text = AidPreparedIndicatorPresenter.FormatLabelText(2, "!", "{bad");

            Assert.AreEqual("2", text);
        }

        [Test]
        public void HandleAidPreparedAndCleared_UpdatesIndicatorVisibilityForCurrentActor()
        {
            var presenter = new AidPreparedIndicatorPresenter();
            var helper = new EntityHandle(1);
            var allyA = new EntityHandle(2);
            var allyB = new EntityHandle(3);
            var enemy = new EntityHandle(4);

            var root = new GameObject("AidPreparedBadge");
            root.SetActive(false);

            try
            {
                presenter.HandleAidPrepared(new AidPreparedEvent(helper, allyA, preparedRound: 1));
                presenter.HandleAidPrepared(new AidPreparedEvent(helper, allyB, preparedRound: 1));

                presenter.RefreshForActor(helper, root, indicatorLabel: null, singleText: "!", countFormat: "x{0}");
                Assert.IsTrue(root.activeSelf);

                presenter.HandleAidCleared(new AidClearedEvent(helper, allyA, AidClearReason.Consumed));
                presenter.HandleAidCleared(new AidClearedEvent(helper, allyB, AidClearReason.Consumed));

                presenter.RefreshForActor(helper, root, indicatorLabel: null, singleText: "!", countFormat: "x{0}");
                Assert.IsFalse(root.activeSelf);

                presenter.RefreshForActor(enemy, root, indicatorLabel: null, singleText: "!", countFormat: "x{0}");
                Assert.IsFalse(root.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RebuildFromService_UsesPreparedAidSnapshot()
        {
            var presenter = new AidPreparedIndicatorPresenter();
            var aidService = new AidService();
            var helper = new EntityHandle(10);
            var allyA = new EntityHandle(11);
            var allyB = new EntityHandle(12);

            Assert.IsTrue(aidService.PrepareAid(helper, allyA, preparedRound: 2));
            Assert.IsTrue(aidService.PrepareAid(helper, allyB, preparedRound: 2));

            var root = new GameObject("AidPreparedBadge");
            root.SetActive(false);

            try
            {
                presenter.RebuildFromService(aidService);
                presenter.RefreshForActor(helper, root, indicatorLabel: null, singleText: "!", countFormat: "x{0}");
                Assert.IsTrue(root.activeSelf);

                presenter.Clear();
                presenter.RefreshForActor(helper, root, indicatorLabel: null, singleText: "!", countFormat: "x{0}");
                Assert.IsFalse(root.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
