using NUnit.Framework;
using PF2e.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PF2e.Tests
{
    public sealed class ActionBarLauncherPresenterTests
    {
        private readonly Vector3[] cornersBuffer = new Vector3[4];
        private GameObject root;
        private RectTransform canvasRect;
        private RectTransform launcherRect;
        private RectTransform popupRect;
        private ActionBarLauncherPresenter presenter;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ActionBarLauncherPresenterTestsRoot");

            var canvasGo = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(root.transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = new Vector2(800f, 600f);

            var launcherGo = new GameObject("StrikeLauncher", typeof(RectTransform));
            launcherGo.transform.SetParent(canvasRect, false);
            launcherRect = launcherGo.GetComponent<RectTransform>();
            launcherRect.anchorMin = new Vector2(0.5f, 0.5f);
            launcherRect.anchorMax = new Vector2(0.5f, 0.5f);
            launcherRect.pivot = new Vector2(0.5f, 0.5f);
            launcherRect.sizeDelta = new Vector2(120f, 36f);
            launcherRect.anchoredPosition = Vector2.zero;

            var popupGo = new GameObject(
                "StrikePopupRoot",
                typeof(RectTransform),
                typeof(Image),
                typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter));
            popupGo.transform.SetParent(launcherRect, false);
            popupRect = popupGo.GetComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(0.5f, 1f);
            popupRect.anchorMax = new Vector2(0.5f, 1f);
            popupRect.pivot = new Vector2(0.5f, 0f);
            popupRect.anchoredPosition = new Vector2(0f, 10f);
            popupRect.sizeDelta = new Vector2(220f, 140f);

            var popupImage = popupGo.GetComponent<Image>();
            popupImage.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);

            presenter = new ActionBarLauncherPresenter();
            presenter.Configure(launcherRect, null, null, popupRect, null, null);
            presenter.SetStrikePopupVisible(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void ClampOpenPopupsToCanvas_WhenEnoughTopSpace_KeepsPopupAboveLauncher()
        {
            launcherRect.anchoredPosition = new Vector2(0f, -180f);

            Canvas.ForceUpdateCanvases();
            presenter.ClampOpenPopupsToCanvas(canvasRect, 10f);
            Canvas.ForceUpdateCanvases();

            Assert.That(popupRect.anchorMin.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(popupRect.anchorMax.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(popupRect.pivot.y, Is.EqualTo(0f).Within(0.001f));
            Assert.Greater(popupRect.anchoredPosition.y, 0f);
        }

        [Test]
        public void ClampOpenPopupsToCanvas_WhenTopOverflow_FlipsPopupBelowLauncher()
        {
            launcherRect.anchoredPosition = new Vector2(0f, 260f);

            Canvas.ForceUpdateCanvases();
            presenter.ClampOpenPopupsToCanvas(canvasRect, 10f);
            Canvas.ForceUpdateCanvases();

            Assert.That(popupRect.anchorMin.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(popupRect.anchorMax.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(popupRect.pivot.y, Is.EqualTo(1f).Within(0.001f));
            Assert.Less(popupRect.anchoredPosition.y, 0f);
        }

        [Test]
        public void ClampOpenPopupsToCanvas_WhenNearRightEdge_ClampsPopupInsideCanvasBounds()
        {
            launcherRect.anchoredPosition = new Vector2(360f, -120f);
            popupRect.sizeDelta = new Vector2(320f, 120f);

            Canvas.ForceUpdateCanvases();
            presenter.ClampOpenPopupsToCanvas(canvasRect, 10f);
            Canvas.ForceUpdateCanvases();

            canvasRect.GetWorldCorners(cornersBuffer);
            float minX = cornersBuffer[0].x + 10f;
            float maxX = cornersBuffer[2].x - 10f;

            popupRect.GetWorldCorners(cornersBuffer);
            float left = cornersBuffer[0].x;
            float right = cornersBuffer[3].x;

            Assert.GreaterOrEqual(left, minX - 0.5f);
            Assert.LessOrEqual(right, maxX + 0.5f);
        }
    }
}
