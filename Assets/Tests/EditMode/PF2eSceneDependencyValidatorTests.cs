using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PF2e.Core;
using PF2e.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
