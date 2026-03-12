using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PF2e.Core;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class CombatLogTooltipPanelDockTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject canvasGo;
        private RectTransform rootRect;
        private RectTransform dockTargetRect;
        private RectTransform viewportRect;
        private CombatLogTooltipPanel panel;
        private RectTransform panelRect;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI bodyText;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            rootRect = canvasGo.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(1920f, 1080f);

            var dockTargetGo = new GameObject("DockTarget", typeof(RectTransform), typeof(Image));
            dockTargetGo.transform.SetParent(canvasGo.transform, false);
            dockTargetRect = dockTargetGo.GetComponent<RectTransform>();
            dockTargetRect.anchorMin = new Vector2(1f, 1f);
            dockTargetRect.anchorMax = new Vector2(1f, 1f);
            dockTargetRect.pivot = new Vector2(1f, 1f);
            dockTargetRect.anchoredPosition = new Vector2(-8f, -16f);
            dockTargetRect.sizeDelta = new Vector2(400f, 250f);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(dockTargetGo.transform, false);
            viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var panelGo = new GameObject(
                "Tooltip",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            panelGo.transform.SetParent(canvasGo.transform, false);
            panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);

            var layout = panelGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = panelGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(panelGo.transform, false);
            titleText = titleGo.AddComponent<TextMeshProUGUI>();

            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(panelGo.transform, false);
            bodyText = bodyGo.AddComponent<TextMeshProUGUI>();

            panel = panelGo.AddComponent<CombatLogTooltipPanel>();
            SetPrivateField(panel, "panelRect", panelRect);
            SetPrivateField(panel, "titleText", titleText);
            SetPrivateField(panel, "bodyText", bodyText);
            SetPrivateField(panel, "canvasGroup", panelGo.GetComponent<CanvasGroup>());
            SetPrivateField(panel, "rootCanvas", canvas);
            SetPrivateField(panel, "dockTarget", dockTargetRect);
            SetPrivateField(panel, "dockViewport", viewportRect);
            SetPrivateField(panel, "minWidth", 420f);
            SetPrivateField(panel, "maxWidth", 560f);
            SetPrivateField(panel, "compactMinWidth", 360f);
            SetPrivateField(panel, "compactMaxWidth", 500f);
            SetPrivateField(panel, "extendedMinWidth", 480f);
            SetPrivateField(panel, "extendedMaxWidth", 640f);
            SetPrivateField(panel, "edgePadding", 24f);
            SetPrivateField(panel, "dockGap", 20f);
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void Show_DockLeft_UsesRightPivot()
        {
            panel.Show("16 vs AC 18 - Success!", "Attack Roll vs AC 18\nTotal: 16", new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Canvas.ForceUpdateCanvases();

            Assert.That(panelRect.pivot.x, Is.EqualTo(1f).Within(0.001f));

            var panelCorners = new Vector3[4];
            panelRect.GetWorldCorners(panelCorners);
            var rootCorners = new Vector3[4];
            rootRect.GetWorldCorners(rootCorners);

            Assert.GreaterOrEqual(panelCorners[0].x, rootCorners[0].x + 23.5f);
        }

        [Test]
        public void Show_DockLeft_ClampsCardInsideParentBoundsForTallCard()
        {
            string tallBody =
                "Attack Roll\nD20 Roll: 12\nAttack Bonus: +9\nMAP: -5\nRange Penalty: -2\nVolley Penalty: -2\nAid: +2\nTotal: 14\nDegree: Failure\n\n" +
                "Armor Class\nBase AC: 18\nCover: +2\nTotal: 20\n\nDefense\nLine 1\nLine 2\nLine 3\nLine 4";

            panel.Show("14 vs AC 20 - Failure", tallBody, new Vector2(Screen.width * 0.5f, 0f));

            Canvas.ForceUpdateCanvases();

            var panelCorners = new Vector3[4];
            panelRect.GetWorldCorners(panelCorners);
            float panelBottom = panelCorners[0].y;
            float panelTop = panelCorners[1].y;

            var rootCorners = new Vector3[4];
            rootRect.GetWorldCorners(rootCorners);
            float rootBottom = rootCorners[0].y;
            float rootTop = rootCorners[1].y;

            Assert.GreaterOrEqual(panelBottom, rootBottom + 23.5f);
            Assert.LessOrEqual(panelTop, rootTop - 23.5f);
        }

        [Test]
        public void Show_AppliesMinAndMaxWidthConstraints()
        {
            panel.Show("Short", "Body", new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            Canvas.ForceUpdateCanvases();
            Assert.GreaterOrEqual(panelRect.rect.width, 419.5f);

            panel.Show(
                "Very Long Tooltip Title",
                "Body with long text that should drive preferred width above maximum and force clamp to max width value for readability.",
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            Canvas.ForceUpdateCanvases();
            Assert.LessOrEqual(panelRect.rect.width, 560.5f);
        }

        [Test]
        public void Show_CompactProfile_UsesCompactWidthRange()
        {
            panel.Show(
                "Damage Breakdown",
                "Damage Roll\n<mspace=0.60em>   8</mspace> Base Damage\nTotal: 8 SLASHING",
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                TooltipLayoutProfile.Compact);
            Canvas.ForceUpdateCanvases();

            Assert.GreaterOrEqual(panelRect.rect.width, 359.5f);
            Assert.LessOrEqual(panelRect.rect.width, 500.5f);
        }

        [Test]
        public void Show_ProfileAppliesTypographyAndLayoutPresets()
        {
            panel.Show(
                "Standard",
                "Result body",
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                TooltipLayoutProfile.Standard);
            Canvas.ForceUpdateCanvases();

            var layout = panelRect.GetComponent<VerticalLayoutGroup>();
            Assert.NotNull(layout);
            Assert.That(titleText.fontSize, Is.EqualTo(20f).Within(0.01f));
            Assert.That(bodyText.fontSize, Is.EqualTo(18f).Within(0.01f));
            Assert.That(bodyText.characterSpacing, Is.EqualTo(2f).Within(0.01f));
            Assert.That(titleText.characterSpacing, Is.EqualTo(2f).Within(0.01f));
            Assert.That(bodyText.lineSpacing, Is.EqualTo(5f).Within(0.01f));
            Assert.AreEqual(16, layout.padding.left);
            Assert.That(layout.spacing, Is.EqualTo(7f).Within(0.01f));

            panel.Show(
                "Compact",
                "Damage body",
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                TooltipLayoutProfile.Compact);
            Canvas.ForceUpdateCanvases();

            Assert.That(titleText.fontSize, Is.EqualTo(18f).Within(0.01f));
            Assert.That(bodyText.fontSize, Is.EqualTo(16f).Within(0.01f));
            Assert.That(bodyText.characterSpacing, Is.EqualTo(2f).Within(0.01f));
            Assert.That(titleText.characterSpacing, Is.EqualTo(2f).Within(0.01f));
            Assert.That(bodyText.lineSpacing, Is.EqualTo(4f).Within(0.01f));
            Assert.AreEqual(14, layout.padding.left);
            Assert.That(layout.spacing, Is.EqualTo(6f).Within(0.01f));
        }

        [Test]
        public void Show_WithoutDockTarget_FallsBackToLegacyCursorPositioning()
        {
            SetPrivateField(panel, "dockTarget", null);

            panel.Show("Legacy", "Body", new Vector2(100f, 100f));
            Canvas.ForceUpdateCanvases();

            Assert.That(panelRect.pivot.x, Is.EqualTo(0f).Within(0.001f));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
