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
    public class AidResolvedLogForwarderTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void AidResolved_PublishesCombatLogLine_WithPositiveModifier()
        {
            using var ctx = new AidResolvedLogContext();

            var helper = ctx.RegisterEntity("Helper", Team.Player);
            var ally = ctx.RegisterEntity("Fighter", Team.Player);
            var roll = new CheckRoll(14, 7, CheckSource.Skill(SkillType.Athletics));
            var ev = new AidResolvedEvent(
                helper,
                ally,
                AidCheckType.Skill,
                SkillType.Athletics,
                "Trip",
                in roll,
                dc: 15,
                degree: DegreeOfSuccess.Success,
                modifierApplied: 2,
                reactionConsumed: true);

            int count = 0;
            CombatLogEntry last = default;
            ctx.EventBus.OnLogEntry += HandleLog;

            try
            {
                ctx.EventBus.PublishAidResolved(in ev);

                Assert.AreEqual(1, count);
                Assert.AreEqual(helper, last.Actor);
                Assert.AreEqual(CombatLogCategory.Attack, last.Category);
                StringAssert.Contains("aids Fighter for Trip", last.Message);
                StringAssert.Contains("ATHLETICS d20(14) +7 = 21", last.Message);
                StringAssert.Contains("vs DC 15", last.Message);
                StringAssert.Contains("Success", last.Message);
                StringAssert.Contains("(+2 circumstance)", last.Message);
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
        public void AidResolved_ZeroModifier_UsesNoModifierText()
        {
            using var ctx = new AidResolvedLogContext();

            var helper = ctx.RegisterEntity("Helper", Team.Player);
            var ally = ctx.RegisterEntity("Fighter", Team.Player);
            var roll = new CheckRoll(3, 5, CheckSource.Custom("AID-ATK"));
            var ev = new AidResolvedEvent(
                helper,
                ally,
                AidCheckType.Strike,
                skill: null,
                triggeringActionName: "Strike",
                in roll,
                dc: 15,
                degree: DegreeOfSuccess.Failure,
                modifierApplied: 0,
                reactionConsumed: true);

            CombatLogEntry last = default;
            int count = 0;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                ctx.EventBus.PublishAidResolved(in ev);

                Assert.AreEqual(1, count);
                StringAssert.Contains("no modifier", last.Message);
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
        public void AidResolved_NegativeModifier_UsesPenaltyText()
        {
            using var ctx = new AidResolvedLogContext();

            var helper = ctx.RegisterEntity("Helper", Team.Player);
            var ally = ctx.RegisterEntity("Fighter", Team.Player);
            var roll = new CheckRoll(1, 4, CheckSource.Skill(SkillType.Athletics));
            var ev = new AidResolvedEvent(
                helper,
                ally,
                AidCheckType.Skill,
                SkillType.Athletics,
                "Reposition",
                in roll,
                dc: 15,
                degree: DegreeOfSuccess.CriticalFailure,
                modifierApplied: -1,
                reactionConsumed: true);

            CombatLogEntry last = default;
            int count = 0;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                ctx.EventBus.PublishAidResolved(in ev);

                Assert.AreEqual(1, count);
                StringAssert.Contains("CriticalFailure", last.Message);
                StringAssert.Contains("(-1 circumstance penalty)", last.Message);
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
        public void AidResolved_AllyMissing_UsesUnknownFallback()
        {
            using var ctx = new AidResolvedLogContext();

            var helper = ctx.RegisterEntity("Helper", Team.Player);
            var missingAlly = new EntityHandle(9999);
            var roll = new CheckRoll(13, 6, CheckSource.Skill(SkillType.Athletics));
            var ev = new AidResolvedEvent(
                helper,
                missingAlly,
                AidCheckType.Skill,
                SkillType.Athletics,
                "Trip",
                in roll,
                dc: 15,
                degree: DegreeOfSuccess.Success,
                modifierApplied: 1,
                reactionConsumed: true);

            CombatLogEntry last = default;
            int count = 0;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                ctx.EventBus.PublishAidResolved(in ev);

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

        private sealed class AidResolvedLogContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject forwarderGo;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public AidResolvedLogForwarder Forwarder { get; }
            public EntityRegistry Registry => EntityManager.Registry;

            public AidResolvedLogContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("AidResolvedLog_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("AidResolvedLog_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                forwarderGo = new GameObject("AidResolvedLogForwarder_Test");
                forwarderGo.SetActive(false);
                Forwarder = forwarderGo.AddComponent<AidResolvedLogForwarder>();
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
