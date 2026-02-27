using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
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

        private static void InvokePrivateValidatorMethod(string methodName)
        {
            var validatorType = FindTypeByName("PF2eSceneDependencyValidator");
            Assert.IsNotNull(validatorType, "PF2eSceneDependencyValidator type not found.");

            var method = validatorType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, $"{methodName} not found on PF2eSceneDependencyValidator.");

            method.Invoke(null, null);
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
