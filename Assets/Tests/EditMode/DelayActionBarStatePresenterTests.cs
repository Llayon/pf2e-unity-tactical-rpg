using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class DelayActionBarStatePresenterTests
    {
        [Test]
        public void BuildInactiveState_DisablesAllDelayControls()
        {
            var presenter = new DelayActionBarStatePresenter();

            var state = presenter.BuildInactiveState();

            Assert.IsTrue(state.blocksNormalActions);
            Assert.IsFalse(state.delayInteractable);
            Assert.IsFalse(state.returnControlsVisible);
            Assert.IsFalse(state.returnNowInteractable);
            Assert.IsFalse(state.skipInteractable);
        }

        [Test]
        public void BuildReturnWindowState_EnablesSkipAndShowsReturnControls()
        {
            var presenter = new DelayActionBarStatePresenter();

            var state = presenter.BuildReturnWindowState(canReturnNow: false);

            Assert.IsTrue(state.blocksNormalActions);
            Assert.IsFalse(state.delayInteractable);
            Assert.IsTrue(state.returnControlsVisible);
            Assert.IsFalse(state.returnNowInteractable);
            Assert.IsTrue(state.skipInteractable);
        }

        [Test]
        public void BuildPlacementSelectionState_EnablesDelayAsCancelToggle()
        {
            var presenter = new DelayActionBarStatePresenter();

            var state = presenter.BuildPlacementSelectionState();

            Assert.IsTrue(state.blocksNormalActions);
            Assert.IsTrue(state.delayInteractable);
            Assert.IsFalse(state.returnControlsVisible);
            Assert.IsFalse(state.returnNowInteractable);
            Assert.IsFalse(state.skipInteractable);
        }

        [Test]
        public void BuildNormalState_UsesCanDelayFlag()
        {
            var presenter = new DelayActionBarStatePresenter();

            var enabled = presenter.BuildNormalState(canDelayCurrentTurn: true);
            var disabled = presenter.BuildNormalState(canDelayCurrentTurn: false);

            Assert.IsFalse(enabled.blocksNormalActions);
            Assert.IsTrue(enabled.delayInteractable);
            Assert.IsFalse(enabled.returnControlsVisible);
            Assert.IsFalse(enabled.returnNowInteractable);
            Assert.IsFalse(enabled.skipInteractable);

            Assert.IsFalse(disabled.blocksNormalActions);
            Assert.IsFalse(disabled.delayInteractable);
            Assert.IsFalse(disabled.returnControlsVisible);
            Assert.IsFalse(disabled.returnNowInteractable);
            Assert.IsFalse(disabled.skipInteractable);
        }

        [Test]
        public void Apply_TogglesButtonVisibilityAndInteractable()
        {
            var presenter = new DelayActionBarStatePresenter();

            var root = new GameObject("DelayButtonsRoot");
            var delayButton = CreateButton("DelayButton", root.transform);
            var returnButton = CreateButton("ReturnNowButton", root.transform);
            var skipButton = CreateButton("SkipDelayWindowButton", root.transform);

            try
            {
                presenter.Apply(
                    presenter.BuildReturnWindowState(canReturnNow: true),
                    delayButton,
                    returnButton,
                    skipButton);

                Assert.IsFalse(delayButton.interactable);
                Assert.IsTrue(returnButton.gameObject.activeSelf);
                Assert.IsTrue(skipButton.gameObject.activeSelf);
                Assert.IsTrue(returnButton.interactable);
                Assert.IsTrue(skipButton.interactable);

                presenter.Apply(
                    presenter.BuildNormalState(canDelayCurrentTurn: true),
                    delayButton,
                    returnButton,
                    skipButton);

                Assert.IsTrue(delayButton.interactable);
                Assert.IsFalse(returnButton.gameObject.activeSelf);
                Assert.IsFalse(skipButton.gameObject.activeSelf);
                Assert.IsFalse(returnButton.interactable);
                Assert.IsFalse(skipButton.interactable);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Button CreateButton(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Button>();
        }
    }
}
