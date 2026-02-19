using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class EncounterFlowControllerPlayModeTests
    {
        private const string SampleSceneName = "SampleScene";
        private const float TimeoutSeconds = 6f;

        private TurnManager turnManager;
        private CombatEventBus eventBus;
        private Button startEncounterButton;
        private Button endEncounterButton;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene(SampleSceneName);
            yield return WaitUntilOrTimeout(
                () => SceneManager.GetActiveScene().name == SampleSceneName,
                TimeoutSeconds,
                "SampleScene did not load.");

            // Let OnEnable/Start build runtime encounter-flow UI.
            yield return null;
            yield return null;

            turnManager = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
            eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();

            Assert.IsNotNull(turnManager, "TurnManager not found in SampleScene.");
            Assert.IsNotNull(eventBus, "CombatEventBus not found in SampleScene.");

            yield return WaitUntilOrTimeout(
                () => TryResolveButtons(out startEncounterButton, out endEncounterButton),
                TimeoutSeconds,
                "Encounter flow buttons were not created/found.");
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_301_StartButton_StartsCombat_AndUpdatesButtonGates()
        {
            Assert.AreEqual(TurnState.Inactive, turnManager.State);
            Assert.IsTrue(startEncounterButton.interactable, "Start Encounter button should be interactable before combat.");
            Assert.IsFalse(endEncounterButton.interactable, "End Encounter button should be disabled before combat.");

            startEncounterButton.onClick.Invoke();

            yield return WaitUntilOrTimeout(
                () => turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn,
                TimeoutSeconds,
                "Start Encounter button did not start combat.");

            Assert.IsFalse(startEncounterButton.interactable, "Start Encounter should be disabled during combat.");
            Assert.IsTrue(endEncounterButton.interactable, "End Encounter should be enabled during combat.");
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_302_EndButton_EndsCombatAsAborted_AndPublishesResult()
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
                startEncounterButton.onClick.Invoke();
                yield return WaitUntilOrTimeout(
                    () => turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn,
                    TimeoutSeconds,
                    "Start Encounter did not begin combat before End Encounter test.");

                endEncounterButton.onClick.Invoke();

                yield return WaitUntilOrTimeout(
                    () => eventReceived,
                    TimeoutSeconds,
                    "End Encounter did not publish CombatEndedEvent.");
                yield return WaitUntilOrTimeout(
                    () => turnManager.State == TurnState.Inactive,
                    TimeoutSeconds,
                    "TurnManager did not return to Inactive after End Encounter.");

                Assert.AreEqual(EncounterResult.Aborted, received);
                Assert.IsTrue(startEncounterButton.interactable, "Start Encounter should re-enable after ending combat.");
                Assert.IsFalse(endEncounterButton.interactable, "End Encounter should disable after combat ends.");
            }
            finally
            {
                eventBus.OnCombatEndedTyped -= Handler;
            }
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
