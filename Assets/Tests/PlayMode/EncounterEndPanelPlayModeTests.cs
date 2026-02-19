using System;
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PF2e.Core;
using PF2e.Presentation;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class EncounterEndPanelPlayModeTests
    {
        private const string SampleSceneName = "SampleScene";
        private const float DefaultTimeoutSeconds = 5f;

        private TurnManager turnManager;
        private CombatEventBus eventBus;
        private EncounterEndPanelController panelController;
        private CanvasGroup panelCanvasGroup;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI subtitleText;
        private Button restartButton;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene(SampleSceneName);
            yield return WaitUntilOrTimeout(
                () => SceneManager.GetActiveScene().name == SampleSceneName,
                DefaultTimeoutSeconds,
                "SampleScene did not load in setup.");

            // Let scene objects run Awake/OnEnable.
            yield return null;
            ResolveSceneReferences();
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_001_Victory_ShowsPanel()
        {
            EncounterResult received = EncounterResult.Unknown;
            bool eventReceived = false;

            void Handler(in CombatEndedEvent e)
            {
                received = e.result;
                eventReceived = true;
            }

            eventBus.OnCombatEndedTyped += Handler;
            try
            {
                turnManager.EndCombat(EncounterResult.Victory);

                yield return WaitUntilOrTimeout(
                    () => eventReceived,
                    DefaultTimeoutSeconds,
                    "Did not receive CombatEndedEvent for Victory.");
                yield return WaitUntilOrTimeout(
                    () => IsPanelVisible(panelCanvasGroup),
                    DefaultTimeoutSeconds,
                    "Encounter end panel did not become visible for Victory.");

                Assert.AreEqual(EncounterResult.Victory, received);
                Assert.AreEqual("Victory", titleText.text);
                Assert.AreEqual("All enemies defeated.", subtitleText.text);
            }
            finally
            {
                eventBus.OnCombatEndedTyped -= Handler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_002_Defeat_ShowsPanel()
        {
            EncounterResult received = EncounterResult.Unknown;
            bool eventReceived = false;

            void Handler(in CombatEndedEvent e)
            {
                received = e.result;
                eventReceived = true;
            }

            eventBus.OnCombatEndedTyped += Handler;
            try
            {
                turnManager.EndCombat(EncounterResult.Defeat);

                yield return WaitUntilOrTimeout(
                    () => eventReceived,
                    DefaultTimeoutSeconds,
                    "Did not receive CombatEndedEvent for Defeat.");
                yield return WaitUntilOrTimeout(
                    () => IsPanelVisible(panelCanvasGroup),
                    DefaultTimeoutSeconds,
                    "Encounter end panel did not become visible for Defeat.");

                Assert.AreEqual(EncounterResult.Defeat, received);
                Assert.AreEqual("Defeat", titleText.text);
                Assert.AreEqual("All players defeated.", subtitleText.text);
            }
            finally
            {
                eventBus.OnCombatEndedTyped -= Handler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_003_Aborted_ShowsGenericPanel()
        {
            EncounterResult received = EncounterResult.Unknown;
            bool eventReceived = false;

            void Handler(in CombatEndedEvent e)
            {
                received = e.result;
                eventReceived = true;
            }

            eventBus.OnCombatEndedTyped += Handler;
            try
            {
                turnManager.EndCombat();

                yield return WaitUntilOrTimeout(
                    () => eventReceived,
                    DefaultTimeoutSeconds,
                    "Did not receive CombatEndedEvent for Aborted.");
                yield return WaitUntilOrTimeout(
                    () => IsPanelVisible(panelCanvasGroup),
                    DefaultTimeoutSeconds,
                    "Encounter end panel did not become visible for Aborted.");

                Assert.AreEqual(EncounterResult.Aborted, received);
                Assert.AreEqual("Encounter Ended", titleText.text);
                Assert.AreEqual("Combat was ended manually.", subtitleText.text);
            }
            finally
            {
                eventBus.OnCombatEndedTyped -= Handler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_004_Restart_ReloadsScene()
        {
            int previousTurnManagerId = turnManager.GetInstanceID();

            turnManager.EndCombat(EncounterResult.Victory);
            yield return WaitUntilOrTimeout(
                () => IsPanelVisible(panelCanvasGroup),
                DefaultTimeoutSeconds,
                "Encounter end panel did not become visible before restart.");

            restartButton.onClick.Invoke();

            yield return WaitUntilOrTimeout(
                () =>
                {
                    if (SceneManager.GetActiveScene().name != SampleSceneName)
                        return false;

                    var candidate = UnityEngine.Object.FindObjectOfType<TurnManager>();
                    return candidate != null && candidate.GetInstanceID() != previousTurnManagerId;
                },
                DefaultTimeoutSeconds,
                "Restart did not reload scene with a new TurnManager instance.");

            ResolveSceneReferences();
            Assert.IsFalse(IsPanelVisible(panelCanvasGroup),
                "Encounter end panel should be hidden after scene reload.");
        }

        private void ResolveSceneReferences()
        {
            turnManager = UnityEngine.Object.FindObjectOfType<TurnManager>();
            eventBus = UnityEngine.Object.FindObjectOfType<CombatEventBus>();
            panelController = UnityEngine.Object.FindObjectOfType<EncounterEndPanelController>();

            Assert.IsNotNull(turnManager, "TurnManager not found in SampleScene.");
            Assert.IsNotNull(eventBus, "CombatEventBus not found in SampleScene.");
            Assert.IsNotNull(panelController, "EncounterEndPanelController not found in SampleScene.");

            var panelGo = GameObject.Find("Canvas/EncounterEndPanel");
            Assert.IsNotNull(panelGo, "Canvas/EncounterEndPanel not found.");
            panelCanvasGroup = panelGo.GetComponent<CanvasGroup>();
            Assert.IsNotNull(panelCanvasGroup, "Canvas/EncounterEndPanel missing CanvasGroup.");

            titleText = GameObject.Find("Canvas/EncounterEndPanel/TitleText")?.GetComponent<TextMeshProUGUI>();
            subtitleText = GameObject.Find("Canvas/EncounterEndPanel/SubtitleText")?.GetComponent<TextMeshProUGUI>();
            restartButton = GameObject.Find("Canvas/EncounterEndPanel/RestartButton")?.GetComponent<Button>();

            Assert.IsNotNull(titleText, "TitleText not found or missing TextMeshProUGUI.");
            Assert.IsNotNull(subtitleText, "SubtitleText not found or missing TextMeshProUGUI.");
            Assert.IsNotNull(restartButton, "RestartButton not found or missing Button.");
        }

        private static bool IsPanelVisible(CanvasGroup canvasGroup)
        {
            return canvasGroup != null
                && canvasGroup.alpha > 0.99f
                && canvasGroup.interactable
                && canvasGroup.blocksRaycasts;
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> predicate, float timeoutSeconds, string timeoutReason)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Timeout after {timeoutSeconds:0.##}s: {timeoutReason}");
                yield return null;
            }
        }
    }
}
