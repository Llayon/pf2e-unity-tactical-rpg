using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Managers;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class DamageLogForwarderTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void DamageApplied_PublishesCombatLogLine()
        {
            using var ctx = new DamageLogContext();

            var source = ctx.RegisterEntity("Fighter", Team.Player);
            var target = ctx.RegisterEntity("Goblin_1", Team.Enemy);

            var ev = new DamageAppliedEvent(
                source,
                target,
                amount: 4,
                damageType: DamageType.Bludgeoning,
                sourceActionName: "Trip",
                isCritical: true,
                hpBefore: 20,
                hpAfter: 16,
                targetDefeated: false);

            int count = 0;
            CombatLogEntry last = default;
            ctx.EventBus.OnLogEntry += HandleLog;

            try
            {
                ctx.EventBus.PublishDamageApplied(in ev);

                Assert.AreEqual(1, count);
                Assert.AreEqual(source, last.Actor);
                Assert.AreEqual(CombatLogCategory.Attack, last.Category);
                StringAssert.Contains("Trip deals 4", last.Message);
                StringAssert.Contains("Bludgeoning", last.Message);
                StringAssert.Contains("Goblin_1", last.Message);
                StringAssert.Contains("HP 20→16", last.Message);
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
        public void DamageApplied_TargetDefeated_PublishesDamageAndDefeatLines()
        {
            using var ctx = new DamageLogContext();

            var source = ctx.RegisterEntity("Fighter", Team.Player);
            var target = ctx.RegisterEntity("Goblin_1", Team.Enemy);

            var ev = new DamageAppliedEvent(
                source,
                target,
                amount: 6,
                damageType: DamageType.Bludgeoning,
                sourceActionName: "Trip",
                isCritical: true,
                hpBefore: 6,
                hpAfter: 0,
                targetDefeated: true);

            int count = 0;
            CombatLogEntry second = default;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                ctx.EventBus.PublishDamageApplied(in ev);

                Assert.AreEqual(2, count);
                Assert.AreEqual(EntityHandle.None, second.Actor);
                StringAssert.Contains("is defeated", second.Message);
            }
            finally
            {
                ctx.EventBus.OnLogEntry -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry)
            {
                count++;
                if (count == 2) second = entry;
            }
        }

        [Test]
        public void DamageApplied_TargetMissing_UsesUnknownFallback()
        {
            using var ctx = new DamageLogContext();

            var source = ctx.RegisterEntity("Fighter", Team.Player);
            var missingTarget = new EntityHandle(9999);
            var ev = new DamageAppliedEvent(
                source,
                missingTarget,
                amount: 4,
                damageType: DamageType.Bludgeoning,
                sourceActionName: "Trip",
                isCritical: false,
                hpBefore: 10,
                hpAfter: 6,
                targetDefeated: false);

            CombatLogEntry last = default;
            int count = 0;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                ctx.EventBus.PublishDamageApplied(in ev);
                Assert.AreEqual(1, count);
                StringAssert.Contains("Unknown", last.Message);
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

        private sealed class DamageLogContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject forwarderGo;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public DamageLogForwarder Forwarder { get; }
            public EntityRegistry Registry => EntityManager.Registry;

            public DamageLogContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("DamageLog_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("DamageLog_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                forwarderGo = new GameObject("DamageLogForwarder_Test");
                forwarderGo.SetActive(false);
                Forwarder = forwarderGo.AddComponent<DamageLogForwarder>();
                SetPrivateField(Forwarder, "eventBus", EventBus);
                SetPrivateField(Forwarder, "entityManager", EntityManager);
                InvokePrivateMethod(Forwarder, "OnEnable");
            }

            public EntityHandle RegisterEntity(string name, Team team)
            {
                return Registry.Register(new EntityData
                {
                    Name = name,
                    Team = team,
                    Size = CreatureSize.Medium,
                    Level = 1,
                    MaxHP = 10,
                    CurrentHP = 10,
                    Speed = 25
                });
            }

            public void Dispose()
            {
                if (Forwarder != null)
                    InvokePrivateMethod(Forwarder, "OnDisable");
                if (forwarderGo != null) Object.DestroyImmediate(forwarderGo);
                if (entityManagerGo != null) Object.DestroyImmediate(entityManagerGo);
                if (eventBusGo != null) Object.DestroyImmediate(eventBusGo);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
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

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, InstanceNonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}");
            method.Invoke(target, null);
        }
    }
}
