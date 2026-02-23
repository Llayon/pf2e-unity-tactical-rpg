using System.Collections.Generic;
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
    public class SkillCheckLogForwarderTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void SkillCheckResolved_PublishesCombatLogLine()
        {
            using var ctx = new SkillCheckLogContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var target = ctx.RegisterEntity("Goblin_1", Team.Enemy);

            var ev = new SkillCheckResolvedEvent(
                actor,
                target,
                SkillType.Athletics,
                naturalRoll: 13,
                modifier: 7,
                total: 20,
                dc: 17,
                degree: DegreeOfSuccess.Success,
                actionName: "Trip");

            int count = 0;
            CombatLogEntry last = default;
            ctx.EventBus.OnLogEntry += HandleLog;

            try
            {
                ctx.EventBus.PublishSkillCheckResolved(in ev);

                Assert.AreEqual(1, count);
                Assert.AreEqual(actor, last.Actor);
                Assert.AreEqual(CombatLogCategory.Attack, last.Category);
                StringAssert.Contains("uses Trip on Goblin_1", last.Message);
                StringAssert.Contains("d20(13)", last.Message);
                StringAssert.Contains("mod(7)", last.Message);
                StringAssert.Contains("= 20", last.Message);
                StringAssert.Contains("vs DC 17", last.Message);
                StringAssert.Contains("Success", last.Message);
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
        public void SkillCheckResolved_TargetMissing_UsesUnknownFallback()
        {
            using var ctx = new SkillCheckLogContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var missingTarget = new EntityHandle(9999);

            var ev = new SkillCheckResolvedEvent(
                actor,
                missingTarget,
                SkillType.Athletics,
                naturalRoll: 10,
                modifier: 4,
                total: 14,
                dc: 18,
                degree: DegreeOfSuccess.Failure,
                actionName: "Trip");

            CombatLogEntry last = default;
            int count = 0;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                ctx.EventBus.PublishSkillCheckResolved(in ev);

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

        private sealed class SkillCheckLogContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject forwarderGo;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public SkillCheckLogForwarder Forwarder { get; }
            public EntityRegistry Registry => EntityManager.Registry;

            public SkillCheckLogContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("SkillCheckLog_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("SkillCheckLog_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                forwarderGo = new GameObject("SkillCheckLogForwarder_Test");
                forwarderGo.SetActive(false);
                Forwarder = forwarderGo.AddComponent<SkillCheckLogForwarder>();
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
