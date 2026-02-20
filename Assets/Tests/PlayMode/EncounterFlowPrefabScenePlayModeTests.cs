using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PF2e.Presentation;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class EncounterFlowPrefabScenePlayModeTests
    {
        private const string SceneName = "EncounterFlowPrefabScene";
        private const float TimeoutSeconds = 6f;

        private TurnManager turnManager;
        private EncounterFlowController encounterFlowController;
        private Button startEncounterButton;
        private Button endEncounterButton;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene(SceneName);
            yield return WaitUntilOrTimeout(
                () => SceneManager.GetActiveScene().name == SceneName,
                TimeoutSeconds,
                "EncounterFlowPrefabScene did not load.");

            yield return null;

            turnManager = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
            encounterFlowController = UnityEngine.Object.FindFirstObjectByType<EncounterFlowController>();

            Assert.IsNotNull(turnManager, "TurnManager not found in EncounterFlowPrefabScene.");
            Assert.IsNotNull(encounterFlowController, "EncounterFlowController not found in EncounterFlowPrefabScene.");

            yield return WaitUntilOrTimeout(
                () => TryResolveButtons(out startEncounterButton, out endEncounterButton),
                TimeoutSeconds,
                "Encounter flow prefab fallback buttons were not created/found.");
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_310_PrefabScene_AutoCreatesPanel_AndButtonsControlEncounter()
        {
            var templatePanel = GameObject.Find("Canvas/EncounterFlowPanelTemplate");
            var runtimePanel = GameObject.Find("Canvas/EncounterFlowPanel");

            Assert.IsNotNull(templatePanel, "Prefab-wired scene should keep the authored template panel.");
            Assert.IsNotNull(runtimePanel, "Prefab-wired scene should instantiate runtime EncounterFlowPanel.");
            Assert.AreNotEqual(templatePanel, runtimePanel, "Template and runtime panel must be different objects.");

            Assert.AreEqual(TurnState.Inactive, turnManager.State);
            Assert.IsTrue(startEncounterButton.interactable, "Start Encounter should be enabled before combat.");
            Assert.IsFalse(endEncounterButton.interactable, "End Encounter should be disabled before combat.");

            startEncounterButton.onClick.Invoke();
            yield return WaitUntilOrTimeout(
                () => turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn,
                TimeoutSeconds,
                "Start button in prefab scene did not start combat.");

            Assert.IsFalse(startEncounterButton.interactable, "Start Encounter should be disabled during combat.");
            Assert.IsTrue(endEncounterButton.interactable, "End Encounter should be enabled during combat.");

            endEncounterButton.onClick.Invoke();
            yield return WaitUntilOrTimeout(
                () => turnManager.State == TurnState.Inactive,
                TimeoutSeconds,
                "End button in prefab scene did not end combat.");

            Assert.IsTrue(startEncounterButton.interactable, "Start Encounter should re-enable after combat end.");
            Assert.IsFalse(endEncounterButton.interactable, "End Encounter should disable after combat end.");
        }

        private static bool TryResolveButtons(out Button startButton, out Button endButton)
        {
            startButton = GameObject.Find("Canvas/EncounterFlowPanel/StartEncounterButton")?.GetComponent<Button>();
            endButton = GameObject.Find("Canvas/EncounterFlowPanel/EndEncounterButton")?.GetComponent<Button>();
            return startButton != null && endButton != null;
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
