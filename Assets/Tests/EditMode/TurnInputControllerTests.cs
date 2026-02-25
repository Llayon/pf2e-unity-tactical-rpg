using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class TurnInputControllerTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void HandleCellClicked_RepositionSelectCellDuringExecutingAction_ForwardsClick()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            var root = new GameObject("TurnInputControllerTests_Root");
            try
            {
                LogAssert.ignoreFailingMessages = true;

                var turnManager = root.AddComponent<TurnManager>();
                var executor = root.AddComponent<PlayerActionExecutor>();
                var targeting = root.AddComponent<TargetingController>();
                var input = root.AddComponent<TurnInputController>();

                SetPrivateField(turnManager, "state", TurnState.ExecutingAction);
                SetPrivateField(executor, "turnManager", turnManager);

                SetAutoPropertyBackingField(targeting, "ActiveMode", TargetingMode.Reposition);
                SetPrivateRepositionPhase(targeting, "SelectCell");

                int calls = 0;
                Vector3Int clicked = default;
                SetPrivateField(targeting, "_onRepositionCellConfirmed", new Func<Vector3Int, bool>(cell =>
                {
                    calls++;
                    clicked = cell;
                    return true;
                }));

                SetPrivateField(input, "turnManager", turnManager);
                SetPrivateField(input, "actionExecutor", executor);
                SetPrivateField(input, "targetingController", targeting);

                InvokePrivate(input, "HandleCellClicked", new object[] { new Vector3Int(2, 0, 1) });

                Assert.AreEqual(1, calls, "Reposition destination click should pass through while TurnManager is ExecutingAction.");
                Assert.AreEqual(new Vector3Int(2, 0, 1), clicked);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void InvokePrivate(object target, string methodName, object[] args)
        {
            var method = target.GetType().GetMethod(methodName, InstanceNonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}");
            method.Invoke(target, args);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            string fieldName = $"<{propertyName}>k__BackingField";
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing auto-property backing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static void SetPrivateRepositionPhase(TargetingController targeting, string enumName)
        {
            var field = typeof(TargetingController).GetField("_repositionPhase", InstanceNonPublic);
            Assert.IsNotNull(field, "Missing _repositionPhase field on TargetingController");

            object enumValue = Enum.Parse(field.FieldType, enumName);
            field.SetValue(targeting, enumValue);
        }
    }
}
