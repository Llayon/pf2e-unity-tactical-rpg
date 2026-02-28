using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PF2e.Core;
using PF2e.Data;
using PF2e.Managers;
using PF2e.Presentation;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class EncounterFlowControllerPlayModeTests
    {
        private const string SampleSceneName = "SampleScene";
        private const float TimeoutSeconds = 6f;

        private TurnManager turnManager;
        private EntityManager entityManager;
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
            entityManager = UnityEngine.Object.FindFirstObjectByType<EntityManager>();
            eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            encounterFlowController = UnityEngine.Object.FindFirstObjectByType<EncounterFlowController>();

            Assert.IsNotNull(turnManager, "TurnManager not found in SampleScene.");
            Assert.IsNotNull(entityManager, "EntityManager not found in SampleScene.");
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
            var initiativeMode = ReadPrivateField<InitiativeCheckMode>(encounterFlowController, "initiativeCheckMode");

            Assert.IsFalse(autoCreate, "EncounterFlowController should default to authored scene wiring.");
            Assert.IsNotNull(wiredStart, "startEncounterButton should be serialized in scene authoring mode.");
            Assert.IsNotNull(wiredEnd, "endEncounterButton should be serialized in scene authoring mode.");
            Assert.AreEqual(startEncounterButton, wiredStart, "Resolved start button should match serialized reference.");
            Assert.AreEqual(endEncounterButton, wiredEnd, "Resolved end button should match serialized reference.");
            Assert.AreEqual(InitiativeCheckMode.Perception, initiativeMode, "Encounter default initiative mode should be Perception.");

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

        [UnityTest]
        public IEnumerator GT_P17_PM_303_AutoCreate_UsesPrefabTemplate_WhenAuthoringPanelMissing()
        {
            var canvas = ReadPrivateField<Canvas>(encounterFlowController, "rootCanvas");
            Assert.IsNotNull(canvas, "EncounterFlowController rootCanvas must be assigned.");

            var authoredPanel = canvas.transform.Find("EncounterFlowPanel") as RectTransform;
            Assert.IsNotNull(authoredPanel, "Authored EncounterFlowPanel should exist in SampleScene.");

            var templateRoot = new GameObject("EncounterFlowTemplateRoot");
            var templatePanel = UnityEngine.Object.Instantiate(authoredPanel, templateRoot.transform, false);
            templatePanel.name = "EncounterFlowPanel";

            try
            {
                encounterFlowController.enabled = false;
                yield return null;

                SetPrivateField(encounterFlowController, "startEncounterButton", null);
                SetPrivateField(encounterFlowController, "endEncounterButton", null);
                SetPrivateField(encounterFlowController, "encounterFlowPanelPrefab", templatePanel);
                SetPrivateField(encounterFlowController, "autoCreateRuntimeButtons", true);

                UnityEngine.Object.Destroy(authoredPanel.gameObject);
                yield return null;

                Assert.IsNull(canvas.transform.Find("EncounterFlowPanel"), "Authored EncounterFlowPanel should be removed before auto-create fallback.");

                encounterFlowController.enabled = true;
                yield return WaitUntilOrTimeout(
                    () => TryResolveButtons(out startEncounterButton, out endEncounterButton),
                    TimeoutSeconds,
                    "Auto-create fallback did not recreate encounter flow buttons.");

                var runtimePanel = canvas.transform.Find("EncounterFlowPanel") as RectTransform;
                Assert.IsNotNull(runtimePanel, "Runtime EncounterFlowPanel should exist after prefab fallback.");
                Assert.AreEqual(canvas.transform, runtimePanel.parent, "Runtime encounter flow panel should be parented under Canvas.");
                Assert.AreNotEqual(canvas.transform, templatePanel.parent, "Template panel should stay detached from Canvas.");

                startEncounterButton.onClick.Invoke();
                yield return WaitUntilOrTimeout(
                    () => turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn,
                    TimeoutSeconds,
                    "Start button from prefab fallback panel did not start combat.");

                endEncounterButton.onClick.Invoke();
                yield return WaitUntilOrTimeout(
                    () => turnManager.State == TurnState.Inactive,
                    TimeoutSeconds,
                    "End button from prefab fallback panel did not end combat.");
            }
            finally
            {
                if (templateRoot != null)
                    UnityEngine.Object.Destroy(templateRoot);
            }
        }

        [UnityTest]
        public IEnumerator GT_P31_PM_420_StartButton_UsesConfiguredSkillInitiativeMode()
        {
            SetPrivateField(encounterFlowController, "initiativeCheckMode", InitiativeCheckMode.Skill);
            SetPrivateField(encounterFlowController, "initiativeSkill", SkillType.Stealth);

            Assert.AreEqual(TurnState.Inactive, turnManager.State);
            startEncounterButton.onClick.Invoke();

            yield return WaitUntilOrTimeout(
                () => turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn,
                TimeoutSeconds,
                "Start Encounter button did not start combat in skill-initiative test.");

            var order = turnManager.InitiativeOrder;
            Assert.Greater(order.Count, 0);
            for (int i = 0; i < order.Count; i++)
            {
                Assert.AreEqual(CheckSourceType.Skill, order[i].Roll.source.type);
                Assert.AreEqual(SkillType.Stealth, order[i].Roll.source.skill);
            }

            endEncounterButton.onClick.Invoke();
            yield return WaitUntilOrTimeout(
                () => turnManager.State == TurnState.Inactive,
                TimeoutSeconds,
                "End Encounter did not return turn manager to Inactive in skill-initiative test.");
        }

        [UnityTest]
        public IEnumerator GT_P31_PM_421_StartButton_AppliesPerActorInitiativeOverride()
        {
            var overrides = new System.Collections.Generic.List<InitiativeActorOverride>
            {
                new InitiativeActorOverride
                {
                    actorId = "wizard",
                    useSkillOverride = true,
                    skill = SkillType.Stealth
                }
            };

            SetPrivateField(encounterFlowController, "initiativeCheckMode", InitiativeCheckMode.Perception);
            SetPrivateField(encounterFlowController, "actorInitiativeOverrides", overrides);

            Assert.AreEqual(TurnState.Inactive, turnManager.State);
            startEncounterButton.onClick.Invoke();

            yield return WaitUntilOrTimeout(
                () => turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn,
                TimeoutSeconds,
                "Start Encounter did not start combat for per-actor initiative override test.");

            bool wizardFound = false;
            foreach (var entry in turnManager.InitiativeOrder)
            {
                var data = entityManager.Registry.Get(entry.Handle);
                Assert.IsNotNull(data);

                if (data.Name == "Wizard")
                {
                    wizardFound = true;
                    Assert.AreEqual(CheckSourceType.Skill, entry.Roll.source.type);
                    Assert.AreEqual(SkillType.Stealth, entry.Roll.source.skill);
                }
                else
                {
                    Assert.AreEqual(CheckSourceType.Perception, entry.Roll.source.type);
                }
            }

            Assert.IsTrue(wizardFound, "Wizard entry not found in initiative order.");

            endEncounterButton.onClick.Invoke();
            yield return WaitUntilOrTimeout(
                () => turnManager.State == TurnState.Inactive,
                TimeoutSeconds,
                "End Encounter did not return to Inactive in per-actor initiative override test.");
        }

        [UnityTest]
        public IEnumerator GT_P31_PM_422_StartButton_UnknownOverrideActor_LogsWarning_AndStartsCombat()
        {
            var overrides = new System.Collections.Generic.List<InitiativeActorOverride>
            {
                new InitiativeActorOverride
                {
                    actorId = "unknown_sneaker",
                    useSkillOverride = true,
                    skill = SkillType.Stealth
                }
            };

            SetPrivateField(encounterFlowController, "initiativeCheckMode", InitiativeCheckMode.Perception);
            SetPrivateField(encounterFlowController, "actorInitiativeOverrides", overrides);
            LogAssert.Expect(LogType.Warning, "[EncounterFlow] Initiative override actorId not found: 'unknown_sneaker'.");

            startEncounterButton.onClick.Invoke();

            yield return WaitUntilOrTimeout(
                () => turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn,
                TimeoutSeconds,
                "Start Encounter did not start combat with unknown actor override.");

            endEncounterButton.onClick.Invoke();
            yield return WaitUntilOrTimeout(
                () => turnManager.State == TurnState.Inactive,
                TimeoutSeconds,
                "End Encounter did not return to Inactive with unknown actor override.");
        }

        [UnityTest]
        public IEnumerator GT_P31_PM_423_StartButton_EmptyActorIdOverride_WarnsAndIsIgnored()
        {
            var overrides = new System.Collections.Generic.List<InitiativeActorOverride>
            {
                new InitiativeActorOverride
                {
                    actorId = " ",
                    useSkillOverride = true,
                    skill = SkillType.Stealth
                }
            };

            SetPrivateField(encounterFlowController, "initiativeCheckMode", InitiativeCheckMode.Perception);
            SetPrivateField(encounterFlowController, "actorInitiativeOverrides", overrides);
            LogAssert.Expect(LogType.Warning, "[EncounterFlow] Initiative override entry #0 has empty actorId and was ignored.");

            startEncounterButton.onClick.Invoke();

            yield return WaitUntilOrTimeout(
                () => turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn,
                TimeoutSeconds,
                "Start Encounter did not start combat for empty actorId override test.");

            foreach (var entry in turnManager.InitiativeOrder)
            {
                var data = entityManager.Registry.Get(entry.Handle);
                Assert.IsNotNull(data);
                Assert.AreEqual(CheckSourceType.Perception, entry.Roll.source.type);
            }

            endEncounterButton.onClick.Invoke();
            yield return WaitUntilOrTimeout(
                () => turnManager.State == TurnState.Inactive,
                TimeoutSeconds,
                "End Encounter did not return to Inactive for empty actorId override test.");
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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            Assert.IsNotNull(target, $"SetPrivateField target is null for '{fieldName}'.");
            var type = target.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on {type.Name}.");
            field.SetValue(target, value);
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
