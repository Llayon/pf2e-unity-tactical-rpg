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
    public class StrikeLogForwarderTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void StrikeLog_RangedWithRangePenalty_IncludesRngPenaltyToken()
        {
            using var ctx = new StrikeLogContext();
            var attacker = ctx.RegisterEntity("Archer", Team.Player);
            var target = ctx.RegisterEntity("Goblin_1", Team.Enemy);

            var ev = CreateStrikeEvent(attacker, target, rangePenalty: -2);

            CombatLogEntry first = default;
            int count = 0;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                ctx.EventBus.PublishStrikeResolved(in ev);

                Assert.GreaterOrEqual(count, 1);
                StringAssert.Contains("RNG(-2)", first.Message);
            }
            finally
            {
                ctx.EventBus.OnLogEntry -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry)
            {
                count++;
                if (count == 1) first = entry;
            }
        }

        [Test]
        public void StrikeLog_RangedFirstIncrement_DoesNotShowRngZero()
        {
            using var ctx = new StrikeLogContext();
            var attacker = ctx.RegisterEntity("Archer", Team.Player);
            var target = ctx.RegisterEntity("Goblin_1", Team.Enemy);

            var ev = CreateStrikeEvent(attacker, target, rangePenalty: 0);

            CombatLogEntry first = default;
            int count = 0;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                ctx.EventBus.PublishStrikeResolved(in ev);

                Assert.GreaterOrEqual(count, 1);
                StringAssert.DoesNotContain("RNG(", first.Message);
            }
            finally
            {
                ctx.EventBus.OnLogEntry -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry)
            {
                count++;
                if (count == 1) first = entry;
            }
        }

        [Test]
        public void StrikeLog_Melee_DoesNotShowRngToken()
        {
            using var ctx = new StrikeLogContext();
            var attacker = ctx.RegisterEntity("Fighter", Team.Player);
            var target = ctx.RegisterEntity("Goblin_1", Team.Enemy);

            var ev = CreateStrikeEvent(attacker, target);

            CombatLogEntry first = default;
            int count = 0;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                ctx.EventBus.PublishStrikeResolved(in ev);

                Assert.GreaterOrEqual(count, 1);
                StringAssert.DoesNotContain("RNG(", first.Message);
            }
            finally
            {
                ctx.EventBus.OnLogEntry -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry)
            {
                count++;
                if (count == 1) first = entry;
            }
        }

        private static StrikeResolvedEvent CreateStrikeEvent(EntityHandle attacker, EntityHandle target, int rangePenalty = 0)
        {
            return new StrikeResolvedEvent(
                attacker,
                target,
                weaponName: "Shortbow",
                naturalRoll: 12,
                attackBonus: 9,
                mapPenalty: -5,
                total: 14,
                dc: 18,
                degree: DegreeOfSuccess.Failure,
                damage: 0,
                damageType: DamageType.Piercing,
                hpBefore: 20,
                hpAfter: 20,
                targetDefeated: false,
                rangePenalty: rangePenalty);
        }

        private sealed class StrikeLogContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject forwarderGo;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public StrikeLogForwarder Forwarder { get; }
            public EntityRegistry Registry => EntityManager.Registry;

            public StrikeLogContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("StrikeLog_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("StrikeLog_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                forwarderGo = new GameObject("StrikeLogForwarder_Test");
                forwarderGo.SetActive(false);
                Forwarder = forwarderGo.AddComponent<StrikeLogForwarder>();
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
                    MaxHP = 20,
                    CurrentHP = 20,
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
