using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PF2e.Core;
using PF2e.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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

            var eventBus = Object.FindFirstObjectByType<CombatEventBus>();
            var actionBar = Object.FindFirstObjectByType<ActionBarController>();
            var initiativeBar = Object.FindFirstObjectByType<InitiativeBarController>();
            Assert.IsNotNull(eventBus, "SampleScene must contain CombatEventBus.");
            Assert.IsNotNull(actionBar, "SampleScene must contain ActionBarController.");
            Assert.IsNotNull(initiativeBar, "SampleScene must contain InitiativeBarController.");

            var existing = Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                Object.DestroyImmediate(existing[i]);
            }

            Assert.AreEqual(
                0,
                Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None).Length,
                "Test precondition: DelayUiOrchestrator must be missing before autofix.");

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var after = Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
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

            var existing = Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                Object.DestroyImmediate(existing[i]);
            }

            var singletonGo = new GameObject("DelayUiOrchestrator_IdempotencyTest");
            singletonGo.AddComponent<DelayUiOrchestrator>();
            Assert.AreEqual(
                1,
                Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None).Length,
                "Test precondition: exactly one DelayUiOrchestrator must exist before autofix.");

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var after = Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            Assert.AreEqual(1, after.Length, "AutoFix must keep DelayUiOrchestrator singleton idempotent.");
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
    }
}
