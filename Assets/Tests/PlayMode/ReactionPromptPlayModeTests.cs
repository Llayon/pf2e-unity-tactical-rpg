using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using PF2e.Core;
using PF2e.Presentation;

namespace PF2e.Tests
{
    public class ReactionPromptPlayModeTests
    {
        private GameObject canvasGo;
        private GameObject controllerGo;
        private GameObject panelGo;
        private ReactionPromptController controller;
        private Button yesButton;
        private Button noButton;
        private TMP_Text titleText;
        private TMP_Text bodyText;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Canvas
            canvasGo = new GameObject("TestCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // Controller (parent)
            controllerGo = new GameObject("ReactionPromptController");
            controllerGo.transform.SetParent(canvasGo.transform);

            // Panel (child — SetActive toggles this, not the controller)
            panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(controllerGo.transform);

            // Title
            var titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(panelGo.transform);
            titleText = titleGo.AddComponent<TextMeshProUGUI>();

            // Body
            var bodyGo = new GameObject("BodyText");
            bodyGo.transform.SetParent(panelGo.transform);
            bodyText = bodyGo.AddComponent<TextMeshProUGUI>();

            // Yes button
            var yesBtnGo = new GameObject("YesButton");
            yesBtnGo.transform.SetParent(panelGo.transform);
            yesBtnGo.AddComponent<RectTransform>();
            yesButton = yesBtnGo.AddComponent<Button>();

            // No button
            var noBtnGo = new GameObject("NoButton");
            noBtnGo.transform.SetParent(panelGo.transform);
            noBtnGo.AddComponent<RectTransform>();
            noButton = noBtnGo.AddComponent<Button>();

            // Wire controller via reflection (inspector-only wiring pattern)
            controller = controllerGo.AddComponent<ReactionPromptController>();
            SetPrivateField(controller, "rootPanel", panelGo);
            SetPrivateField(controller, "titleText", titleText);
            SetPrivateField(controller, "bodyText", bodyText);
            SetPrivateField(controller, "yesButton", yesButton);
            SetPrivateField(controller, "noButton", noButton);
            SetPrivateField(controller, "timeoutSeconds", 2f); // short timeout for tests

            // Let Awake/OnEnable fire.
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (canvasGo != null) Object.Destroy(canvasGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Prompt_YesButton_InvokesCallbackTrue_AndClosesPanel()
        {
            bool? result = null;
            controller.RequestShieldBlockPrompt(
                new EntityHandle(1), incomingDamage: 10, shieldHp: 15, shieldMaxHp: 20,
                decided => result = decided);

            Assert.IsTrue(controller.IsPromptActive);
            Assert.IsTrue(panelGo.activeSelf, "Panel should be visible.");

            yesButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(result.HasValue);
            Assert.IsTrue(result.Value);
            Assert.IsFalse(controller.IsPromptActive);
            Assert.IsFalse(panelGo.activeSelf, "Panel should be hidden after decision.");
        }

        [UnityTest]
        public IEnumerator Prompt_NoButton_InvokesCallbackFalse_AndClosesPanel()
        {
            bool? result = null;
            controller.RequestShieldBlockPrompt(
                new EntityHandle(1), incomingDamage: 10, shieldHp: 15, shieldMaxHp: 20,
                decided => result = decided);

            Assert.IsTrue(controller.IsPromptActive);

            noButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(result.HasValue);
            Assert.IsFalse(result.Value);
            Assert.IsFalse(controller.IsPromptActive);
            Assert.IsFalse(panelGo.activeSelf);
        }

        [UnityTest]
        public IEnumerator Prompt_Timeout_AutoDeclinesFalse()
        {
            bool? result = null;
            controller.RequestShieldBlockPrompt(
                new EntityHandle(1), incomingDamage: 10, shieldHp: 15, shieldMaxHp: 20,
                decided => result = decided);

            Assert.IsTrue(controller.IsPromptActive);

            LogAssert.Expect(LogType.Warning, "[ReactionPrompt] Timeout reached. Auto-declining Shield Block.");

            // Wait for timeout (set to 2s in SetUp) + margin.
            float deadline = Time.time + controller.TimeoutSeconds + 1f;
            while (!result.HasValue && Time.time < deadline)
                yield return null;

            Assert.IsTrue(result.HasValue, "Callback should have been invoked by timeout.");
            Assert.IsFalse(result.Value, "Timeout should decline.");
            Assert.IsFalse(controller.IsPromptActive);
            Assert.IsFalse(panelGo.activeSelf);
        }

        [UnityTest]
        public IEnumerator ForceCloseAsDecline_InvokesCallbackFalse()
        {
            bool? result = null;
            controller.RequestShieldBlockPrompt(
                new EntityHandle(1), incomingDamage: 10, shieldHp: 15, shieldMaxHp: 20,
                decided => result = decided);

            Assert.IsTrue(controller.IsPromptActive);

            controller.ForceCloseAsDecline();
            yield return null;

            Assert.IsTrue(result.HasValue);
            Assert.IsFalse(result.Value);
            Assert.IsFalse(controller.IsPromptActive);
        }

        [UnityTest]
        public IEnumerator ForceCloseAsDecline_WhenNoPrompt_DoesNothing()
        {
            Assert.IsFalse(controller.IsPromptActive);
            controller.ForceCloseAsDecline();
            yield return null;

            // No error, no callback, no state change.
            Assert.IsFalse(controller.IsPromptActive);
        }

        [UnityTest]
        public IEnumerator DoubleRequest_ForcesCloseFirstAsDecline_ThenOpensNew()
        {
            bool? firstResult = null;
            bool? secondResult = null;

            controller.RequestShieldBlockPrompt(
                new EntityHandle(1), incomingDamage: 5, shieldHp: 10, shieldMaxHp: 20,
                decided => firstResult = decided);

            LogAssert.Expect(LogType.Warning, "[ReactionPrompt] Previous prompt still active. Force-closing as decline.");

            controller.RequestShieldBlockPrompt(
                new EntityHandle(2), incomingDamage: 12, shieldHp: 8, shieldMaxHp: 20,
                decided => secondResult = decided);

            yield return null;

            // First callback should have been force-declined.
            Assert.IsTrue(firstResult.HasValue);
            Assert.IsFalse(firstResult.Value);

            // Second prompt is now active.
            Assert.IsTrue(controller.IsPromptActive);
            Assert.IsFalse(secondResult.HasValue);

            // Resolve second prompt.
            yesButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(secondResult.HasValue);
            Assert.IsTrue(secondResult.Value);
        }

        [UnityTest]
        public IEnumerator OnDisable_WithActivePrompt_ForcesDecline()
        {
            bool? result = null;
            controller.RequestShieldBlockPrompt(
                new EntityHandle(1), incomingDamage: 10, shieldHp: 15, shieldMaxHp: 20,
                decided => result = decided);

            Assert.IsTrue(controller.IsPromptActive);

            // Disable the controller (simulates scene teardown, component disable, etc.)
            controller.enabled = false;
            yield return null;

            Assert.IsTrue(result.HasValue, "OnDisable should force-close active prompt.");
            Assert.IsFalse(result.Value);

            // Re-enable for teardown
            controller.enabled = true;
        }

        [UnityTest]
        public IEnumerator BodyText_ShowsCorrectDamageAndShieldInfo()
        {
            controller.RequestShieldBlockPrompt(
                new EntityHandle(1), incomingDamage: 14, shieldHp: 18, shieldMaxHp: 20,
                _ => { });

            yield return null;

            Assert.AreEqual("Shield Block?", titleText.text);
            Assert.IsTrue(bodyText.text.Contains("14"), "Body should contain incoming damage.");
            Assert.IsTrue(bodyText.text.Contains("18"), "Body should contain current shield HP.");
            Assert.IsTrue(bodyText.text.Contains("20"), "Body should contain max shield HP.");

            controller.ForceCloseAsDecline();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
