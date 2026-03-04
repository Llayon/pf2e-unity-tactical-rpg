using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PF2e.Core;
using PF2e.Presentation;
using PF2e.TurnSystem;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PF2e.Tests
{
    [TestFixture]
    public class PF2eSceneDependencyValidatorTests
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string EncounterFlowScenePath = "Assets/Scenes/EncounterFlowPrefabScene.unity";

        [Test]
        public void AutoFixWorkflowGuard_WhenActiveSceneIsCleanNonSample_ReturnsToSampleScene()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");
            Assert.IsTrue(System.IO.File.Exists(EncounterFlowScenePath), $"Missing scene: {EncounterFlowScenePath}");

            EditorSceneManager.OpenScene(EncounterFlowScenePath, OpenSceneMode.Single);
            var activeBefore = SceneManager.GetActiveScene();
            Assert.AreEqual(EncounterFlowScenePath, activeBefore.path);
            Assert.IsFalse(activeBefore.isDirty, "Test precondition: active non-sample scene must be clean.");

            InvokePrivateValidatorMethod("TryReturnToSampleSceneIfSafe");

            var activeAfter = SceneManager.GetActiveScene();
            Assert.AreEqual(SampleScenePath, activeAfter.path);
        }

        [Test]
        public void SampleScene_Smoke_WiresCriticalCombatUiControllers()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<ActionBarController>(),
                "SampleScene must contain ActionBarController.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<InitiativeBarController>(),
                "SampleScene must contain InitiativeBarController.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<TurnUIController>(),
                "SampleScene must contain TurnUIController.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<CombatLogController>(),
                "SampleScene must contain CombatLogController.");
        }

        [Test]
        public void SampleScene_AidUiAndExecutor_Wired()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            var actionBar = UnityEngine.Object.FindFirstObjectByType<ActionBarController>();
            var executor = UnityEngine.Object.FindFirstObjectByType<PlayerActionExecutor>();
            var aidAction = UnityEngine.Object.FindFirstObjectByType<AidAction>();

            Assert.IsNotNull(eventBus, "SampleScene must contain CombatEventBus.");
            Assert.IsNotNull(actionBar, "SampleScene must contain ActionBarController.");
            Assert.IsNotNull(executor, "SampleScene must contain PlayerActionExecutor.");

            SetPrivateField(actionBar, "aidButton", null);
            SetPrivateField(actionBar, "aidHighlight", null);
            SetPrivateField(actionBar, "aidPreparedIndicatorRoot", null);
            SetPrivateField(actionBar, "aidPreparedIndicatorLabel", null);

            var staleAidButton = actionBar.transform.Find("AidButton");
            if (staleAidButton != null)
            {
                UnityEngine.Object.DestroyImmediate(staleAidButton.gameObject);
            }

            // Scene/tooling policy: validator autofix is responsible for Aid UI creation/wiring in legacy scenes.
            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);
            InvokePrivateInstanceMethod(executor, "ResolveOptionalReferences");

            var aidButton = GetPrivateField<Button>(actionBar, "aidButton");
            var aidHighlight = GetPrivateField<Image>(actionBar, "aidHighlight");
            var aidPreparedBadgeRoot = GetPrivateField<GameObject>(actionBar, "aidPreparedIndicatorRoot");
            Assert.IsNotNull(aidButton, "ActionBarController must have Aid button wired by scene/autofix.");
            Assert.IsNotNull(aidHighlight, "ActionBarController must have Aid highlight wired by scene/autofix.");
            Assert.IsNotNull(aidPreparedBadgeRoot, "ActionBarController must have Aid prepared badge wired by scene/autofix.");
            Assert.AreEqual("AidButton", aidButton.gameObject.name, "Resolved Aid button should use canonical name AidButton.");
            Assert.AreEqual("AidPreparedBadge", aidPreparedBadgeRoot.name, "Aid prepared badge should use canonical name AidPreparedBadge.");
            Assert.AreSame(aidButton.transform, aidPreparedBadgeRoot.transform.parent, "Aid prepared badge must be attached to Aid button.");
            Assert.IsNotNull(aidButton.transform.Find("AidPreparedBadge"), "Aid button hierarchy must contain AidPreparedBadge child.");

            var resolvedAidAction = GetPrivateField<AidAction>(executor, "aidAction");
            var wiredEventBus = GetPrivateField<CombatEventBus>(executor, "eventBus");
            Assert.IsNotNull(resolvedAidAction, "PlayerActionExecutor must resolve AidAction (wired or runtime fallback).");
            Assert.AreSame(eventBus, wiredEventBus, "PlayerActionExecutor.eventBus must be wired/resolved.");

            if (aidAction != null)
                Assert.AreSame(aidAction, resolvedAidAction, "When AidAction exists in scene, executor should reuse it.");
        }

        [Test]
        public void SampleScene_ReadyModeUi_Wired()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var actionBar = UnityEngine.Object.FindFirstObjectByType<ActionBarController>();
            Assert.IsNotNull(actionBar, "SampleScene must contain ActionBarController.");

            SetPrivateField(actionBar, "readyModeSelectorRoot", null);
            SetPrivateField(actionBar, "readyModeMoveButton", null);
            SetPrivateField(actionBar, "readyModeAttackButton", null);
            SetPrivateField(actionBar, "readyModeAnyButton", null);

            var readyButton = GetPrivateField<Button>(actionBar, "readyButton");
            if (readyButton != null)
            {
                var staleSelector = readyButton.transform.Find("ReadyModeSelector");
                if (staleSelector != null)
                    UnityEngine.Object.DestroyImmediate(staleSelector.gameObject);
            }

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var selectorRoot = GetPrivateField<RectTransform>(actionBar, "readyModeSelectorRoot");
            var moveButton = GetPrivateField<Button>(actionBar, "readyModeMoveButton");
            var attackButton = GetPrivateField<Button>(actionBar, "readyModeAttackButton");
            var anyButton = GetPrivateField<Button>(actionBar, "readyModeAnyButton");

            Assert.IsNotNull(selectorRoot, "ActionBarController must have Ready mode selector root wired by scene/autofix.");
            Assert.IsNotNull(moveButton, "ActionBarController must have Ready mode Move button wired by scene/autofix.");
            Assert.IsNotNull(attackButton, "ActionBarController must have Ready mode Attack button wired by scene/autofix.");
            Assert.IsNotNull(anyButton, "ActionBarController must have Ready mode Any button wired by scene/autofix.");
            Assert.AreEqual("ReadyModeSelector", selectorRoot.gameObject.name);
            Assert.AreEqual("ReadyModeMoveButton", moveButton.gameObject.name);
            Assert.AreEqual("ReadyModeAttackButton", attackButton.gameObject.name);
            Assert.AreEqual("ReadyModeAnyButton", anyButton.gameObject.name);
            Assert.AreSame(selectorRoot, moveButton.transform.parent);
            Assert.AreSame(selectorRoot, attackButton.transform.parent);
            Assert.AreSame(selectorRoot, anyButton.transform.parent);
        }

        [Test]
        public void AutoFix_WhenDelayUiOrchestratorMissing_CreatesAndWiresIt()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            var actionBar = UnityEngine.Object.FindFirstObjectByType<ActionBarController>();
            var initiativeBar = UnityEngine.Object.FindFirstObjectByType<InitiativeBarController>();
            Assert.IsNotNull(eventBus, "SampleScene must contain CombatEventBus.");
            Assert.IsNotNull(actionBar, "SampleScene must contain ActionBarController.");
            Assert.IsNotNull(initiativeBar, "SampleScene must contain InitiativeBarController.");

            var existing = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(existing[i]);
            }

            Assert.AreEqual(
                0,
                UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None).Length,
                "Test precondition: DelayUiOrchestrator must be missing before autofix.");

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var after = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            Assert.AreEqual(1, after.Length, "AutoFix must create exactly one DelayUiOrchestrator.");

            var orchestrator = after[0];
            Assert.AreSame(eventBus, GetPrivateField<CombatEventBus>(orchestrator, "eventBus"));
            Assert.AreSame(actionBar, GetPrivateField<ActionBarController>(orchestrator, "actionBarController"));
            Assert.AreSame(initiativeBar, GetPrivateField<InitiativeBarController>(orchestrator, "initiativeBarController"));
        }

        [Test]
        public void AutoFix_WhenDelayUiOrchestratorAlreadyExists_DoesNotCreateDuplicate()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var existing = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(existing[i]);
            }

            var singletonGo = new GameObject("DelayUiOrchestrator_IdempotencyTest");
            singletonGo.AddComponent<DelayUiOrchestrator>();
            Assert.AreEqual(
                1,
                UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None).Length,
                "Test precondition: exactly one DelayUiOrchestrator must exist before autofix.");

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var after = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            Assert.AreEqual(1, after.Length, "AutoFix must keep DelayUiOrchestrator singleton idempotent.");
        }

        [Test]
        public void AutoFix_WhenDelayUiOrchestratorExistsWithNullRefs_RewiresWithoutDuplicate()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            var actionBar = UnityEngine.Object.FindFirstObjectByType<ActionBarController>();
            var initiativeBar = UnityEngine.Object.FindFirstObjectByType<InitiativeBarController>();
            Assert.IsNotNull(eventBus, "SampleScene must contain CombatEventBus.");
            Assert.IsNotNull(actionBar, "SampleScene must contain ActionBarController.");
            Assert.IsNotNull(initiativeBar, "SampleScene must contain InitiativeBarController.");

            var all = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            if (all.Length == 0)
            {
                var go = new GameObject("DelayUiOrchestrator_RewireTest");
                go.AddComponent<DelayUiOrchestrator>();
                all = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            }

            for (int i = 1; i < all.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(all[i]);
            }

            var orchestrator = UnityEngine.Object.FindFirstObjectByType<DelayUiOrchestrator>();
            Assert.IsNotNull(orchestrator, "Test precondition: one DelayUiOrchestrator must exist.");

            SetPrivateField(orchestrator, "eventBus", null);
            SetPrivateField(orchestrator, "actionBarController", null);
            SetPrivateField(orchestrator, "initiativeBarController", null);

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var after = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            Assert.AreEqual(1, after.Length, "AutoFix must not create duplicate DelayUiOrchestrator while rewiring.");

            var rewired = after[0];
            Assert.AreSame(eventBus, GetPrivateField<CombatEventBus>(rewired, "eventBus"));
            Assert.AreSame(actionBar, GetPrivateField<ActionBarController>(rewired, "actionBarController"));
            Assert.AreSame(initiativeBar, GetPrivateField<InitiativeBarController>(rewired, "initiativeBarController"));
        }

        [Test]
        public void AutoFix_WhenReadyStrikeEventBinderMissing_CreatesAndWiresOnTurnManager()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var turnManager = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
            var eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            Assert.IsNotNull(turnManager, "SampleScene must contain TurnManager.");
            Assert.IsNotNull(eventBus, "SampleScene must contain CombatEventBus.");

            var existing = UnityEngine.Object.FindObjectsByType<ReadyStrikeEventBinder>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(existing[i]);
            }

            Assert.AreEqual(
                0,
                UnityEngine.Object.FindObjectsByType<ReadyStrikeEventBinder>(FindObjectsSortMode.None).Length,
                "Test precondition: ReadyStrikeEventBinder must be missing before autofix.");

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var after = UnityEngine.Object.FindObjectsByType<ReadyStrikeEventBinder>(FindObjectsSortMode.None);
            Assert.AreEqual(1, after.Length, "AutoFix must create exactly one ReadyStrikeEventBinder.");

            var binder = after[0];
            Assert.AreSame(turnManager.gameObject, binder.gameObject, "ReadyStrikeEventBinder must be created on TurnManager GameObject.");
            Assert.AreSame(turnManager, GetPrivateField<TurnManager>(binder, "turnManager"));
            Assert.AreSame(eventBus, GetPrivateField<CombatEventBus>(binder, "eventBus"));
        }

        [Test]
        public void Validator_MissingEncounterActorId_OnAliveCombatants_EmitsWarnings()
        {
            var entities = new List<EntityData>
            {
                new EntityData
                {
                    Name = "Fighter",
                    Team = Team.Player,
                    CurrentHP = 20,
                    EncounterActorId = ""
                },
                new EntityData
                {
                    Name = "Goblin_1",
                    Team = Team.Enemy,
                    CurrentHP = 6,
                    EncounterActorId = "   "
                },
                new EntityData
                {
                    Name = "DeadGoblin",
                    Team = Team.Enemy,
                    CurrentHP = 0,
                    EncounterActorId = ""
                },
                new EntityData
                {
                    Name = "NeutralNpc",
                    Team = Team.Neutral,
                    CurrentHP = 10,
                    EncounterActorId = ""
                }
            };

            LogAssert.Expect(LogType.Warning, new Regex(@"\[PF2eValidator\] Entity 'Fighter' \(Player\) has empty EncounterActorId\."));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[PF2eValidator\] Entity 'Goblin_1' \(Enemy\) has empty EncounterActorId\."));

            int warnings = InvokePrivateValidatorMethodForEncounterActorIds(entities);
            Assert.AreEqual(2, warnings, "Only alive Player/Enemy entities with empty EncounterActorId should be warned.");
        }

        private static void InvokePrivateValidatorMethod(string methodName)
        {
            var validatorType = FindTypeByName("PF2eSceneDependencyValidator");
            Assert.IsNotNull(validatorType, "PF2eSceneDependencyValidator type not found.");

            var method = validatorType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, $"{methodName} not found on PF2eSceneDependencyValidator.");

            method.Invoke(null, null);
        }

        private static void InvokePrivateValidatorMethodWithBoolArg(string methodName, bool arg)
        {
            var validatorType = FindTypeByName("PF2eSceneDependencyValidator");
            Assert.IsNotNull(validatorType, "PF2eSceneDependencyValidator type not found.");

            var method = validatorType.GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(bool) },
                null);
            Assert.IsNotNull(method, $"{methodName}(bool) not found on PF2eSceneDependencyValidator.");

            method.Invoke(null, new object[] { arg });
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");

            var value = field.GetValue(target) as T;
            Assert.IsNotNull(value, $"Field '{fieldName}' is null on {target.GetType().Name}.");
            return value;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateInstanceMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"{methodName} not found on {target.GetType().Name}.");
            method.Invoke(target, null);
        }

        private static Type FindTypeByName(string typeName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException e)
                    {
                        return e.Types.Where(t => t != null);
                    }
                })
                .FirstOrDefault(t => t != null && t.Name == typeName);
        }

        private static int InvokePrivateValidatorMethodForEncounterActorIds(IEnumerable<EntityData> entities)
        {
            var validatorType = FindTypeByName("PF2eSceneDependencyValidator");
            Assert.IsNotNull(validatorType, "PF2eSceneDependencyValidator type not found.");

            var method = validatorType.GetMethod(
                "WarnMissingEncounterActorIds",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(IEnumerable<EntityData>), typeof(UnityEngine.Object) },
                null);
            Assert.IsNotNull(method, "WarnMissingEncounterActorIds(IEnumerable<EntityData>, Object) not found.");

            object result = method.Invoke(null, new object[] { entities, null });
            Assert.IsNotNull(result, "WarnMissingEncounterActorIds returned null.");
            return (int)result;
        }
    }
}
