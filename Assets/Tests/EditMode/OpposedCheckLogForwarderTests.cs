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
    public class OpposedCheckLogForwarderTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void OpposedCheckResolved_PublishesCombatLogLine()
        {
            using var ctx = new OpposedCheckLogContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var target = ctx.RegisterEntity("Goblin_1", Team.Enemy);

            var attackerRoll = new CheckRoll(14, 7, CheckSource.Skill(SkillType.Athletics));
            var defenderRoll = new CheckRoll(9, 6, CheckSource.Save(SaveType.Fortitude));

            var ev = new OpposedCheckResolvedEvent(
                actor,
                target,
                "Contest",
                in attackerRoll,
                in defenderRoll,
                margin: attackerRoll.total - defenderRoll.total,
                winner: OpposedCheckWinner.Attacker);

            int count = 0;
            CombatLogEntry last = default;
            ctx.EventBus.OnLogEntry += HandleLog;

            try
            {
                ctx.EventBus.PublishOpposedCheckResolved(in ev);

                Assert.AreEqual(1, count);
                Assert.AreEqual(actor, last.Actor);
                Assert.AreEqual(CombatLogCategory.Attack, last.Category);
                StringAssert.Contains("uses Contest on Goblin_1", last.Message);
                StringAssert.Contains("ATHLETICS d20(14) +7 = 21", last.Message);
                StringAssert.Contains("FORTITUDE d20(9) +6 = 15", last.Message);
                StringAssert.Contains("Attacker (+6)", last.Message);
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

        private sealed class OpposedCheckLogContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject forwarderGo;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public OpposedCheckLogForwarder Forwarder { get; }
            public EntityRegistry Registry => EntityManager.Registry;

            public OpposedCheckLogContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("OpposedCheckLog_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("OpposedCheckLog_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                forwarderGo = new GameObject("OpposedCheckLogForwarder_Test");
                forwarderGo.SetActive(false);
                Forwarder = forwarderGo.AddComponent<OpposedCheckLogForwarder>();
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
