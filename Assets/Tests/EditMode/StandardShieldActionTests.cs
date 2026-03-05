using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class StandardShieldActionTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryCastStandardShield_CastsAndPublishesShieldRaised()
        {
            using var ctx = new StandardShieldContext();
            var actor = ctx.RegisterActor(knowsCantrip: true, cooldownRounds: 0);

            ShieldRaisedEvent lastEvent = default;
            int eventCount = 0;
            ctx.EventBus.OnShieldRaisedTyped += OnShieldRaised;
            try
            {
                bool cast = ctx.Action.TryCastStandardShield(actor);
                Assert.IsTrue(cast);

                var data = ctx.Registry.Get(actor);
                Assert.IsNotNull(data);
                Assert.IsTrue(data.StandardShieldRaised);
                Assert.AreEqual(StandardShieldAction.BaseAcBonus, data.StandardShieldAcBonus);
                Assert.AreEqual(StandardShieldAction.BaseHardness, data.StandardShieldHardness);

                Assert.AreEqual(1, eventCount);
                Assert.AreEqual(actor, lastEvent.actor);
                Assert.AreEqual(StandardShieldAction.BaseAcBonus, lastEvent.acBonus);
                Assert.AreEqual(StandardShieldAction.BaseMaxHP, lastEvent.shieldHP);
            }
            finally
            {
                ctx.EventBus.OnShieldRaisedTyped -= OnShieldRaised;
            }

            void OnShieldRaised(in ShieldRaisedEvent e)
            {
                eventCount++;
                lastEvent = e;
            }
        }

        [Test]
        public void TryCastStandardShield_LevelFive_UsesHeightenedHardness()
        {
            using var ctx = new StandardShieldContext();
            var actor = ctx.RegisterActor(knowsCantrip: true, cooldownRounds: 0, level: 5);

            bool cast = ctx.Action.TryCastStandardShield(actor);
            Assert.IsTrue(cast);

            var data = ctx.Registry.Get(actor);
            Assert.IsNotNull(data);
            Assert.AreEqual(10, data.StandardShieldHardness, "Rank 3 cantrip should grant Hardness 10.");
        }

        [Test]
        public void TryCastStandardShield_FailsWithoutCantrip()
        {
            using var ctx = new StandardShieldContext();
            var actor = ctx.RegisterActor(knowsCantrip: false, cooldownRounds: 0);

            bool cast = ctx.Action.TryCastStandardShield(actor);
            Assert.IsFalse(cast);
        }

        [Test]
        public void TryCastStandardShield_FailsWhileCooldownActive()
        {
            using var ctx = new StandardShieldContext();
            var actor = ctx.RegisterActor(knowsCantrip: true, cooldownRounds: 3);

            bool cast = ctx.Action.TryCastStandardShield(actor);
            Assert.IsFalse(cast);
        }

        [Test]
        public void TickStartTurn_ExpiresStandardShield_AndTicksCooldown()
        {
            var data = new EntityData
            {
                MaxHP = 10,
                CurrentHP = 10,
                KnowsStandardShieldCantrip = true,
                StandardShieldCooldownRoundsRemaining = 0
            };
            Assert.IsTrue(data.ActivateStandardShield(1, 5, 1));
            data.StartStandardShieldCooldown(2);

            var deltas = new System.Collections.Generic.List<ConditionDelta>();
            var service = new ConditionService();
            service.TickStartTurn(data, deltas);

            Assert.IsFalse(data.StandardShieldRaised);
            Assert.AreEqual(1, data.StandardShieldCooldownRoundsRemaining);
        }

        [Test]
        public void CanCastShieldCantrips_AreMutuallyExclusive()
        {
            var withGlassRaised = new EntityData
            {
                MaxHP = 10,
                CurrentHP = 10,
                KnowsGlassShieldCantrip = true,
                KnowsStandardShieldCantrip = true
            };
            Assert.IsTrue(withGlassRaised.ActivateGlassShield(1, 2, 1));
            Assert.IsFalse(withGlassRaised.CanCastStandardShield, "Standard Shield must be unavailable while Glass Shield is raised.");

            var withStandardRaised = new EntityData
            {
                MaxHP = 10,
                CurrentHP = 10,
                KnowsGlassShieldCantrip = true,
                KnowsStandardShieldCantrip = true
            };
            Assert.IsTrue(withStandardRaised.ActivateStandardShield(1, 5, 1));
            Assert.IsFalse(withStandardRaised.CanCastGlassShield, "Glass Shield must be unavailable while Standard Shield is raised.");
        }

        private sealed class StandardShieldContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityGo;
            private readonly GameObject actionGo;

            public readonly CombatEventBus EventBus;
            public readonly EntityManager EntityManager;
            public readonly EntityRegistry Registry;
            public readonly StandardShieldAction Action;

            public StandardShieldContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("EventBus_StandardShieldTest");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityGo = new GameObject("EntityManager_StandardShieldTest");
                EntityManager = entityGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                actionGo = new GameObject("StandardShieldAction_Test");
                Action = actionGo.AddComponent<StandardShieldAction>();
                Action.InjectDependencies(EntityManager, EventBus);
            }

            public EntityHandle RegisterActor(bool knowsCantrip, int cooldownRounds, int level = 1)
            {
                var data = new EntityData
                {
                    Name = "Wizard",
                    Team = Team.Player,
                    Size = CreatureSize.Medium,
                    Level = level,
                    MaxHP = 10,
                    CurrentHP = 10,
                    KnowsStandardShieldCantrip = knowsCantrip,
                    StandardShieldCooldownRoundsRemaining = cooldownRounds
                };

                return Registry.Register(data);
            }

            public void Dispose()
            {
                if (actionGo != null) Object.DestroyImmediate(actionGo);
                if (entityGo != null) Object.DestroyImmediate(entityGo);
                if (eventBusGo != null) Object.DestroyImmediate(eventBusGo);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            string fieldName = $"<{propertyName}>k__BackingField";
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing auto-property backing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
