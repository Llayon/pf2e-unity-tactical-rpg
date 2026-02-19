using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PF2e.Core;
using PF2e.Presentation;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class EncounterFlowControllerPlayModeTests
    {
        private const string SampleSceneName = "SampleScene";
        private const float TimeoutSeconds = 6f;

        private TurnManager turnManager;
        private CombatEventBus eventBus;
        private EncounterFlowController encounterFlowController;
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

            // Let startup initialize UI state.
            yield return null;

            turnManager = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
            eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            encounterFlowController = UnityEngine.Object.FindFirstObjectByType<EncounterFlowController>();

            Assert.IsNotNull(turnManager, "TurnManager not found in SampleScene.");
            Assert.IsNotNull(eventBus, "CombatEventBus not found in SampleScene.");
            Assert.IsNotNull(encounterFlowController, "EncounterFlowController not found in SampleScene.");

            yield return WaitUntilOrTimeout(
                () => TryResolveButtons(out startEncounterButton, out endEncounterButton),
                TimeoutSeconds,
                "Encounter flow buttons were not created/found.");
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_300_AuthoringRefs_AreSerialized_AndAutoCreateDisabled()
        {
            var autoCreate = ReadPrivateField<bool>(encounterFlowController, "autoCreateRuntimeButtons");
            var wiredStart = ReadPrivateField<Button>(encounterFlowController, "startEncounterButton");
            var wiredEnd = ReadPrivateField<Button>(encounterFlowController, "endEncounterButton");

            Assert.IsFalse(autoCreate, "EncounterFlowController should default to authored scene wiring.");
            Assert.IsNotNull(wiredStart, "startEncounterButton should be serialized in scene authoring mode.");
            Assert.IsNotNull(wiredEnd, "endEncounterButton should be serialized in scene authoring mode.");
            Assert.AreEqual(startEncounterButton, wiredStart, "Resolved start button should match serialized reference.");
            Assert.AreEqual(endEncounterButton, wiredEnd, "Resolved end button should match serialized reference.");

            yield return null;
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

        private static T ReadPrivateField<T>(object target, string fieldName)
        {
            Assert.IsNotNull(target, $"ReadPrivateField target is null for '{fieldName}'.");
            var type = target.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on {type.Name}.");

            object value = field.GetValue(target);
            return (T)value;
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
