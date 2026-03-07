using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PF2e.Core;
using PF2e.Presentation;

namespace PF2e.Tests
{
    public class CombatLogHoverControllerPlayModeTests
    {
        private GameObject canvasGo;
        private ScrollRect scrollRect;
        private RectTransform viewportRect;
        private CombatLogController logController;
        private CombatLogHoverController hoverController;
        private CombatLogTooltipPanel tooltipPanel;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI bodyText;
        private TextMeshProUGUI lineText;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scrollGo = new GameObject("CombatLogScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(canvasGo.transform, false);
            var scrollRectTransform = scrollGo.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;
            scrollRect = scrollGo.GetComponent<ScrollRect>();

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            var lineGo = new GameObject("CombatLogLine", typeof(RectTransform));
            lineGo.transform.SetParent(contentGo.transform, false);
            lineText = lineGo.AddComponent<TextMeshProUGUI>();
            lineText.enableWordWrapping = false;
            lineText.alignment = TextAlignmentOptions.Center;
            lineText.fontSize = 36f;
            lineText.raycastTarget = false;
            lineText.text = "Strike total " + CombatLogLinkHelper.Link(CombatLogLinkTokens.AttackTotal, "16");
            var lineRect = lineText.rectTransform;
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.sizeDelta = new Vector2(560f, 70f);
            lineRect.anchoredPosition = Vector2.zero;

            var logGo = new GameObject("CombatLogController");
            logGo.transform.SetParent(canvasGo.transform, false);
            logGo.SetActive(false);
            LogAssert.Expect(LogType.Error, "[CombatLog] Missing reference: EntityManager");
            LogAssert.Expect(LogType.Error, "[CombatLog] Missing reference: CombatEventBus");
            logController = logGo.AddComponent<CombatLogController>();
            logController.enabled = false;
            logGo.SetActive(true);

            var tooltipGo = new GameObject("CombatLogTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            tooltipGo.transform.SetParent(canvasGo.transform, false);
            tooltipGo.SetActive(false);
            var tooltipRect = tooltipGo.GetComponent<RectTransform>();
            tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
            tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
            tooltipRect.pivot = new Vector2(0f, 0f);
            tooltipRect.sizeDelta = new Vector2(320f, 140f);

            var titleGo = new GameObject("TooltipTitle", typeof(RectTransform));
            titleGo.transform.SetParent(tooltipGo.transform, false);
            titleText = titleGo.AddComponent<TextMeshProUGUI>();

            var bodyGo = new GameObject("TooltipBody", typeof(RectTransform));
            bodyGo.transform.SetParent(tooltipGo.transform, false);
            bodyText = bodyGo.AddComponent<TextMeshProUGUI>();

            tooltipPanel = tooltipGo.AddComponent<CombatLogTooltipPanel>();
            SetPrivateField(tooltipPanel, "panelRect", tooltipRect);
            SetPrivateField(tooltipPanel, "titleText", titleText);
            SetPrivateField(tooltipPanel, "bodyText", bodyText);
            SetPrivateField(tooltipPanel, "canvasGroup", tooltipGo.GetComponent<CanvasGroup>());
            SetPrivateField(tooltipPanel, "rootCanvas", canvas);
            tooltipGo.SetActive(true);

            var hoverGo = new GameObject("CombatLogHoverController");
            hoverGo.transform.SetParent(canvasGo.transform, false);
            hoverGo.SetActive(false);
            hoverController = hoverGo.AddComponent<CombatLogHoverController>();
            SetPrivateField(hoverController, "logController", logController);
            SetPrivateField(hoverController, "tooltipPanel", tooltipPanel);
            SetPrivateField(hoverController, "viewportRect", viewportRect);
            SetPrivateField(hoverController, "scrollRect", scrollRect);
            hoverGo.SetActive(true);

            var activeLines = GetPrivateField<Queue<TextMeshProUGUI>>(logController, "activeLines");
            activeLines.Clear();
            activeLines.Enqueue(lineText);

            var lineTooltips = GetPrivateField<Dictionary<TextMeshProUGUI, TooltipEntry[]>>(logController, "lineTooltips");
            lineTooltips.Clear();
            lineTooltips[lineText] = new[]
            {
                new TooltipEntry(CombatLogLinkTokens.AttackTotal, "Attack Roll Breakdown", "d20(12) + ATK(+9) + MAP(-5) = 16")
            };

            yield return null;
            Canvas.ForceUpdateCanvases();
            lineText.ForceMeshUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (canvasGo != null)
            {
                Object.Destroy(canvasGo);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ScrollChange_HidesTooltip()
        {
            tooltipPanel.Show("Attack Roll Breakdown", "Body", new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            Assert.IsTrue(tooltipPanel.IsVisible);

            scrollRect.onValueChanged.Invoke(new Vector2(0f, 0.75f));
            yield return null;

            Assert.IsFalse(tooltipPanel.IsVisible, "Tooltip must hide when log scroll position changes.");
        }

        [UnityTest]
        public IEnumerator Show_WhenTooltipObjectInactive_ActivatesAndBecomesVisible()
        {
            tooltipPanel.Hide();
            Assert.IsFalse(tooltipPanel.gameObject.activeSelf, "Tooltip object should be inactive after Hide.");

            tooltipPanel.Show("Attack Roll Breakdown", "Body", new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;

            Assert.IsTrue(tooltipPanel.gameObject.activeSelf, "Show must reactivate tooltip object.");
            Assert.IsTrue(tooltipPanel.IsVisible, "Show must mark tooltip as visible.");
        }

        [UnityTest]
        public IEnumerator PointerOutsideViewport_HidesTooltip()
        {
            tooltipPanel.Show("Attack Roll Breakdown", "Body", new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            Assert.IsTrue(tooltipPanel.IsVisible);

            InvokePrivateMethod(hoverController, "ProcessPointer", new Vector2(-10f, -10f));
            yield return null;

            Assert.IsFalse(tooltipPanel.IsVisible, "Tooltip must hide when pointer leaves viewport bounds.");
        }

        [UnityTest]
        public IEnumerator PointerOnLink_HoverScanFindsLinkedLineAndToken()
        {
            InvokePrivateMethod(hoverController, "ClearHover");
            Assert.IsFalse(tooltipPanel.IsVisible);

            Vector2 linkScreenPoint = GetFirstLinkScreenPoint(lineText);
            Assert.IsTrue(lineText.gameObject.activeInHierarchy, "Line GameObject must be active.");
            Assert.IsTrue(lineText.isActiveAndEnabled, "Line TMP component must be active.");
            Assert.IsTrue(
                RectTransformUtility.RectangleContainsScreenPoint(viewportRect, linkScreenPoint, null),
                "Expected link point to be inside viewport bounds.");
            Assert.IsTrue(
                (bool)InvokePrivateMethodWithResult(hoverController, "IsLineVisibleInViewport", lineText),
                "Expected line to be considered visible in viewport.");
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(lineText, linkScreenPoint, null);
            Assert.GreaterOrEqual(linkIndex, 0, "Expected helper point to intersect TMP link.");
            Assert.AreEqual(CombatLogLinkTokens.AttackTotal, lineText.textInfo.linkInfo[linkIndex].GetLinkID());
            Assert.IsTrue(
                logController.TryGetTooltip(lineText, CombatLogLinkTokens.AttackTotal, out _, out _),
                "Expected tooltip mapping for attack token.");
            Assert.AreEqual(1, logController.GetActiveLines().Count, "Expected one active combat log line in test setup.");

            var tryFindMethod = FindMethod(hoverController.GetType(), "TryFindHoveredLink");
            Assert.IsNotNull(tryFindMethod, "Missing method 'TryFindHoveredLink' on CombatLogHoverController");
            object[] findArgs = { (Vector3)linkScreenPoint, null, -1 };
            bool foundByController = (bool)tryFindMethod.Invoke(hoverController, findArgs);
            Assert.IsTrue(foundByController, "Expected controller hover scan to find the link.");
            Assert.AreSame(lineText, findArgs[1]);
            Assert.AreEqual(linkIndex, (int)findArgs[2]);
            string foundToken = ((TextMeshProUGUI)findArgs[1]).textInfo.linkInfo[(int)findArgs[2]].GetLinkID();
            Assert.IsTrue(
                logController.TryGetTooltip((TextMeshProUGUI)findArgs[1], foundToken, out _, out _),
                "Expected controller-found token to resolve tooltip mapping.");
            yield break;
        }

        private static Vector2 GetFirstLinkScreenPoint(TextMeshProUGUI text)
        {
            text.ForceMeshUpdate();
            Assert.Greater(text.textInfo.linkCount, 0, "Expected at least one TMP link.");

            var linkInfo = text.textInfo.linkInfo[0];
            int firstCharacterIndex = linkInfo.linkTextfirstCharacterIndex;
            Assert.GreaterOrEqual(firstCharacterIndex, 0, "Link must have at least one character.");

            var character = text.textInfo.characterInfo[firstCharacterIndex];
            Vector3 localCenter = (character.bottomLeft + character.topRight) * 0.5f;
            Vector3 worldCenter = text.rectTransform.TransformPoint(localCenter);
            return RectTransformUtility.WorldToScreenPoint(null, worldCenter);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = FindField(target.GetType(), fieldName);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}");
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = FindField(target.GetType(), fieldName);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName, params object[] args)
        {
            var method = FindMethod(target.GetType(), methodName);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}");
            method.Invoke(target, args);
        }

        private static object InvokePrivateMethodWithResult(object target, string methodName, params object[] args)
        {
            var method = FindMethod(target.GetType(), methodName);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}");
            return method.Invoke(target, args);
        }

        private static FieldInfo FindField(System.Type type, string fieldName)
        {
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static MethodInfo FindMethod(System.Type type, string methodName)
        {
            while (type != null)
            {
                var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (method != null)
                {
                    return method;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
