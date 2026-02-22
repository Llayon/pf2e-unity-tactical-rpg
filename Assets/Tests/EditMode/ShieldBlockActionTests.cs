using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class ShieldBlockActionTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void Execute_ValidActor_AppliesShieldDamage_SpendsReaction_AndPublishesEvent()
        {
            using var ctx = new ShieldBlockActionContext();

            var shieldDef = ctx.CreateShieldDef(hardness: 5, maxHp: 20);
            var actor = ctx.RegisterEntity("Defender", Team.Player, shieldDef, reactionAvailable: true);

            ShieldBlockResolvedEvent lastEvent = default;
            int eventCount = 0;
            ctx.EventBus.OnShieldBlockResolvedTyped += OnResolved;
            try
            {
                bool ok = ctx.Action.Execute(actor, incomingDamage: 10, result: new ShieldBlockResult(5, 5));
                Assert.IsTrue(ok);

                var data = ctx.Registry.Get(actor);
                Assert.IsNotNull(data);
                Assert.IsFalse(data.ReactionAvailable);
                Assert.AreEqual(15, data.EquippedShield.currentHP);

                Assert.AreEqual(1, eventCount);
                Assert.AreEqual(actor, lastEvent.reactor);
                Assert.AreEqual(10, lastEvent.incomingDamage);
                Assert.AreEqual(5, lastEvent.damageReduction);
                Assert.AreEqual(5, lastEvent.shieldSelfDamage);
                Assert.AreEqual(20, lastEvent.shieldHpBefore);
                Assert.AreEqual(15, lastEvent.shieldHpAfter);
            }
            finally
            {
                ctx.EventBus.OnShieldBlockResolvedTyped -= OnResolved;
            }

            void OnResolved(in ShieldBlockResolvedEvent e)
            {
                eventCount++;
                lastEvent = e;
            }
        }

        [Test]
        public void Execute_InvalidActor_ReturnsFalse()
        {
            using var ctx = new ShieldBlockActionContext();
            bool ok = ctx.Action.Execute(EntityHandle.None, incomingDamage: 10, result: new ShieldBlockResult(5, 5));
            Assert.IsFalse(ok);
        }

        private sealed class ShieldBlockActionContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject actionGo;
            private readonly System.Collections.Generic.List<ShieldDefinition> shieldDefs = new();

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public EntityRegistry Registry { get; }
            public ShieldBlockAction Action { get; }

            public ShieldBlockActionContext()
            {
                oldIgnoreLogs = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("EventBus_ShieldBlockActionTest");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("EntityManager_ShieldBlockActionTest");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                actionGo = new GameObject("ShieldBlockAction_Test");
                Action = actionGo.AddComponent<ShieldBlockAction>();
                SetPrivateField(Action, "entityManager", EntityManager);
                SetPrivateField(Action, "eventBus", EventBus);
            }

            public ShieldDefinition CreateShieldDef(int hardness, int maxHp)
            {
                var def = ScriptableObject.CreateInstance<ShieldDefinition>();
                def.itemName = "Test Shield";
                def.acBonus = 2;
                def.hardness = hardness;
                def.maxHP = maxHp;
                shieldDefs.Add(def);
                return def;
            }

            public EntityHandle RegisterEntity(string name, Team team, ShieldDefinition shieldDef, bool reactionAvailable)
            {
                var data = new EntityData
                {
                    Name = name,
                    Team = team,
                    Size = CreatureSize.Medium,
                    MaxHP = 20,
                    CurrentHP = 20,
                    Speed = 25,
                    Strength = 10,
                    Dexterity = 10,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    ReactionAvailable = reactionAvailable,
                    EquippedShield = ShieldInstance.CreateEquipped(shieldDef)
                };

                return Registry.Register(data);
            }

            public void Dispose()
            {
                for (int i = 0; i < shieldDefs.Count; i++)
                {
                    if (shieldDefs[i] != null)
                        Object.DestroyImmediate(shieldDefs[i]);
                }

                if (actionGo != null) Object.DestroyImmediate(actionGo);
                if (entityManagerGo != null) Object.DestroyImmediate(entityManagerGo);
                if (eventBusGo != null) Object.DestroyImmediate(eventBusGo);
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = oldIgnoreLogs;
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
