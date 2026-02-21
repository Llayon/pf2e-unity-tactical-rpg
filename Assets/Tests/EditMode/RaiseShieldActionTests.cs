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
    public class RaiseShieldActionTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryRaiseShield_RaisesShieldAndPublishesTypedEvent()
        {
            using var ctx = new RaiseShieldContext();

            var def = ctx.CreateShieldDef(acBonus: 2, hardness: 4, maxHP: 18);
            var actor = ctx.RegisterActor(ShieldInstance.CreateEquipped(def));

            int eventCount = 0;
            ShieldRaisedEvent lastEvent = default;
            ctx.EventBus.OnShieldRaisedTyped += HandleShieldRaised;

            try
            {
                bool raised = ctx.Action.TryRaiseShield(actor);
                Assert.IsTrue(raised);

                var data = ctx.EntityManager.Registry.Get(actor);
                Assert.IsNotNull(data);
                Assert.IsTrue(data.EquippedShield.isRaised);
                Assert.AreEqual(1, eventCount);
                Assert.AreEqual(actor, lastEvent.actor);
                Assert.AreEqual(2, lastEvent.acBonus);
            }
            finally
            {
                ctx.EventBus.OnShieldRaisedTyped -= HandleShieldRaised;
            }

            void HandleShieldRaised(in ShieldRaisedEvent e)
            {
                eventCount++;
                lastEvent = e;
            }
        }

        [Test]
        public void TryRaiseShield_FailsWithoutEquippedShield()
        {
            using var ctx = new RaiseShieldContext();
            var actor = ctx.RegisterActor(default);

            bool raised = ctx.Action.TryRaiseShield(actor);
            Assert.IsFalse(raised);
        }

        [Test]
        public void TryRaiseShield_FailsWhenShieldBroken()
        {
            using var ctx = new RaiseShieldContext();

            var def = ctx.CreateShieldDef(maxHP: 12);
            var shield = ShieldInstance.CreateEquipped(def);
            shield.currentHP = 0;
            var actor = ctx.RegisterActor(shield);

            bool raised = ctx.Action.TryRaiseShield(actor);
            Assert.IsFalse(raised);
        }

        [Test]
        public void TryRaiseShield_FailsWhenAlreadyRaised()
        {
            using var ctx = new RaiseShieldContext();

            var def = ctx.CreateShieldDef(maxHP: 12);
            var shield = ShieldInstance.CreateEquipped(def);
            shield.isRaised = true;
            var actor = ctx.RegisterActor(shield);

            bool raised = ctx.Action.TryRaiseShield(actor);
            Assert.IsFalse(raised);
        }

        private sealed class RaiseShieldContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly System.Collections.Generic.List<ShieldDefinition> defs = new();
            private readonly GameObject eventBusGo;
            private readonly GameObject entityGo;
            private readonly GameObject actionGo;

            public readonly CombatEventBus EventBus;
            public readonly EntityManager EntityManager;
            public readonly RaiseShieldAction Action;

            public RaiseShieldContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityGo = new GameObject("EntityManager_Test");
                EntityManager = entityGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());

                actionGo = new GameObject("RaiseShieldAction_Test");
                Action = actionGo.AddComponent<RaiseShieldAction>();

                SetPrivateField(Action, "entityManager", EntityManager);
                SetPrivateField(Action, "eventBus", EventBus);
            }

            public ShieldDefinition CreateShieldDef(int acBonus = 2, int hardness = 5, int maxHP = 20)
            {
                var def = ScriptableObject.CreateInstance<ShieldDefinition>();
                def.itemName = "Test Shield";
                def.acBonus = acBonus;
                def.hardness = hardness;
                def.maxHP = maxHP;
                defs.Add(def);
                return def;
            }

            public EntityHandle RegisterActor(ShieldInstance shield)
            {
                var data = new EntityData
                {
                    Name = "Actor",
                    Team = Team.Player,
                    Size = CreatureSize.Medium,
                    MaxHP = 20,
                    CurrentHP = 20,
                    EquippedShield = shield
                };

                return EntityManager.Registry.Register(data);
            }

            public void Dispose()
            {
                for (int i = 0; i < defs.Count; i++)
                {
                    if (defs[i] != null)
                        Object.DestroyImmediate(defs[i]);
                }

                if (actionGo != null) Object.DestroyImmediate(actionGo);
                if (entityGo != null) Object.DestroyImmediate(entityGo);
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
    }
}
