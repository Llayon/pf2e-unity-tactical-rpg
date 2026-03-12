using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class ConditionLogForwarderTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ConditionAdded_UsesStatusAppliedWording()
        {
            using var ctx = new ConditionLogContext();

            CombatLogEntry last = default;
            int count = 0;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                ctx.EventBus.PublishConditionChanged(
                    ctx.Entity,
                    ConditionType.Prone,
                    ConditionChangeType.Added,
                    oldValue: 0,
                    newValue: 1);

                Assert.AreEqual(1, count);
                Assert.AreEqual(ctx.Entity, last.Actor);
                StringAssert.Contains("is now", last.Message);
                StringAssert.Contains("prone", last.Message);
            }
            finally
            {
                ctx.EventBus.OnLogEntry -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry)
            {
                count++;
                last = entry;
            }
        }

        [Test]
        public void ConditionRemoved_UsesStatusRemovedWording()
        {
            using var ctx = new ConditionLogContext();

            CombatLogEntry last = default;
            int count = 0;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                ctx.EventBus.PublishConditionChanged(
                    ctx.Entity,
                    ConditionType.Prone,
                    ConditionChangeType.Removed,
                    oldValue: 1,
                    newValue: 0);

                Assert.AreEqual(1, count);
                StringAssert.Contains("is no longer", last.Message);
                StringAssert.Contains("prone", last.Message);
            }
            finally
            {
                ctx.EventBus.OnLogEntry -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry)
            {
                count++;
                last = entry;
            }
        }

        [Test]
        public void ConditionValueChanged_UsesUpdatedStatusValue()
        {
            using var ctx = new ConditionLogContext();

            CombatLogEntry last = default;
            int count = 0;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                ctx.EventBus.PublishConditionChanged(
                    ctx.Entity,
                    ConditionType.Frightened,
                    ConditionChangeType.ValueChanged,
                    oldValue: 2,
                    newValue: 1);

                Assert.AreEqual(1, count);
                StringAssert.Contains("is now", last.Message);
                StringAssert.Contains("frightened 1", last.Message);
                StringAssert.DoesNotContain("decreases to", last.Message);
            }
            finally
            {
                ctx.EventBus.OnLogEntry -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry)
            {
                count++;
                last = entry;
            }
        }

        private sealed class ConditionLogContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject forwarderGo;

            public CombatEventBus EventBus { get; }
            public ConditionLogForwarder Forwarder { get; }
            public EntityHandle Entity { get; } = new EntityHandle(42);

            public ConditionLogContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("ConditionLog_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                forwarderGo = new GameObject("ConditionLogForwarder_Test");
                forwarderGo.SetActive(false);
                Forwarder = forwarderGo.AddComponent<ConditionLogForwarder>();
                SetPrivateField(Forwarder, "eventBus", EventBus);
                InvokePrivateMethod(Forwarder, "OnEnable");
            }

            public void Dispose()
            {
                if (Forwarder != null)
                {
                    InvokePrivateMethod(Forwarder, "OnDisable");
                }

                if (forwarderGo != null)
                {
                    Object.DestroyImmediate(forwarderGo);
                }

                if (eventBusGo != null)
                {
                    Object.DestroyImmediate(eventBusGo);
                }

                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, InstanceNonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}");
            method.Invoke(target, null);
        }
    }
}
