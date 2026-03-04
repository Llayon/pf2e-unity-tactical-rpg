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
    public class GlassShieldActionTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryCastGlassShield_CastsAndPublishesShieldRaised()
        {
            using var ctx = new GlassShieldContext();
            var actor = ctx.RegisterActor(knowsCantrip: true, cooldownRounds: 0);

            ShieldRaisedEvent lastEvent = default;
            int eventCount = 0;
            ctx.EventBus.OnShieldRaisedTyped += OnShieldRaised;
            try
            {
                bool cast = ctx.Action.TryCastGlassShield(actor);
                Assert.IsTrue(cast);

                var data = ctx.Registry.Get(actor);
                Assert.IsNotNull(data);
                Assert.IsTrue(data.GlassShieldRaised);
                Assert.AreEqual(GlassShieldAction.BaseAcBonus, data.GlassShieldAcBonus);
                Assert.AreEqual(GlassShieldAction.BaseHardness, data.GlassShieldHardness);

                Assert.AreEqual(1, eventCount);
                Assert.AreEqual(actor, lastEvent.actor);
                Assert.AreEqual(GlassShieldAction.BaseAcBonus, lastEvent.acBonus);
                Assert.AreEqual(GlassShieldAction.BaseMaxHP, lastEvent.shieldHP);
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
        public void TryCastGlassShield_FailsWithoutCantrip()
        {
            using var ctx = new GlassShieldContext();
            var actor = ctx.RegisterActor(knowsCantrip: false, cooldownRounds: 0);

            bool cast = ctx.Action.TryCastGlassShield(actor);
            Assert.IsFalse(cast);
        }

        [Test]
        public void TryCastGlassShield_FailsWhileCooldownActive()
        {
            using var ctx = new GlassShieldContext();
            var actor = ctx.RegisterActor(knowsCantrip: true, cooldownRounds: 3);

            bool cast = ctx.Action.TryCastGlassShield(actor);
            Assert.IsFalse(cast);
        }

        [Test]
        public void TickStartTurn_ExpiresGlassShield_AndTicksCooldown()
        {
            var data = new EntityData
            {
                MaxHP = 10,
                CurrentHP = 10,
                KnowsGlassShieldCantrip = true,
                GlassShieldCooldownRoundsRemaining = 0
            };
            Assert.IsTrue(data.ActivateGlassShield(1, 2, 1));
            data.StartGlassShieldCooldown(2);

            var deltas = new System.Collections.Generic.List<ConditionDelta>();
            var service = new ConditionService();
            service.TickStartTurn(data, deltas);

            Assert.IsFalse(data.GlassShieldRaised);
            Assert.AreEqual(1, data.GlassShieldCooldownRoundsRemaining);
        }

        private sealed class GlassShieldContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityGo;
            private readonly GameObject actionGo;

            public readonly CombatEventBus EventBus;
            public readonly EntityManager EntityManager;
            public readonly EntityRegistry Registry;
            public readonly GlassShieldAction Action;

            public GlassShieldContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("EventBus_GlassShieldTest");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityGo = new GameObject("EntityManager_GlassShieldTest");
                EntityManager = entityGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                actionGo = new GameObject("GlassShieldAction_Test");
                Action = actionGo.AddComponent<GlassShieldAction>();
                Action.InjectDependencies(EntityManager, EventBus);
            }

            public EntityHandle RegisterActor(bool knowsCantrip, int cooldownRounds)
            {
                var data = new EntityData
                {
                    Name = "Wizard",
                    Team = Team.Player,
                    Size = CreatureSize.Medium,
                    MaxHP = 10,
                    CurrentHP = 10,
                    KnowsGlassShieldCantrip = knowsCantrip,
                    GlassShieldCooldownRoundsRemaining = cooldownRounds
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
