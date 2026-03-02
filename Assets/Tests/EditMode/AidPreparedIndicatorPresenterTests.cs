using NUnit.Framework;
using UnityEngine;
using PF2e.Core;
using PF2e.Presentation;
using System.Reflection;

namespace PF2e.Tests
{
    [TestFixture]
    public class AidPreparedIndicatorPresenterTests
    {
        private const BindingFlags InstancePublic = BindingFlags.Instance | BindingFlags.Public;

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

                InvokeRefreshForActor(presenter, helper, root);
                Assert.IsTrue(root.activeSelf);

                presenter.HandleAidCleared(new AidClearedEvent(helper, allyA, AidClearReason.Consumed));
                presenter.HandleAidCleared(new AidClearedEvent(helper, allyB, AidClearReason.Consumed));

                InvokeRefreshForActor(presenter, helper, root);
                Assert.IsFalse(root.activeSelf);

                InvokeRefreshForActor(presenter, enemy, root);
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
                InvokeRefreshForActor(presenter, helper, root);
                Assert.IsTrue(root.activeSelf);

                presenter.Clear();
                InvokeRefreshForActor(presenter, helper, root);
                Assert.IsFalse(root.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void InvokeRefreshForActor(AidPreparedIndicatorPresenter presenter, EntityHandle actor, GameObject indicatorRoot)
        {
            var method = typeof(AidPreparedIndicatorPresenter).GetMethod("RefreshForActor", InstancePublic);
            Assert.IsNotNull(method, "Missing RefreshForActor method.");

            method.Invoke(presenter, new object[] { actor, indicatorRoot, null, "!", "x{0}" });
        }
    }
}
