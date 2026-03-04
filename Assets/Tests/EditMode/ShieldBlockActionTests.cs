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

        [Test]
        public void Execute_GlassShieldSource_ConsumesSpellAndStartsCooldown()
        {
            using var ctx = new ShieldBlockActionContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, shieldDef: null, reactionAvailable: true);
            var data = ctx.Registry.Get(actor);
            Assert.IsNotNull(data);
            data.KnowsGlassShieldCantrip = true;
            Assert.IsTrue(data.ActivateGlassShield(acBonus: 1, hardness: 2, maxHP: 1));

            ShieldBlockResolvedEvent lastEvent = default;
            int eventCount = 0;
            ctx.EventBus.OnShieldBlockResolvedTyped += OnResolved;
            try
            {
                bool ok = ctx.Action.Execute(
                    actor,
                    incomingDamage: 10,
                    result: new ShieldBlockResult(2, 8),
                    source: ShieldBlockSource.GlassShield);
                Assert.IsTrue(ok);

                Assert.IsFalse(data.ReactionAvailable);
                Assert.IsFalse(data.GlassShieldRaised);
                Assert.AreEqual(0, data.GlassShieldCurrentHP);
                Assert.AreEqual(GlassShieldAction.BlockCooldownRounds, data.GlassShieldCooldownRoundsRemaining);

                Assert.AreEqual(1, eventCount);
                Assert.AreEqual(actor, lastEvent.reactor);
                Assert.AreEqual(10, lastEvent.incomingDamage);
                Assert.AreEqual(2, lastEvent.damageReduction);
                Assert.AreEqual(8, lastEvent.shieldSelfDamage);
                Assert.AreEqual(1, lastEvent.shieldHpBefore);
                Assert.AreEqual(0, lastEvent.shieldHpAfter);
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
        public void Execute_GlassShieldSource_BreakerInRange_DealsShardDamage()
        {
            using var ctx = new ShieldBlockActionContext();

            var reactor = ctx.RegisterEntity("Wizard", Team.Player, shieldDef: null, reactionAvailable: true);
            var breaker = ctx.RegisterEntity("Goblin", Team.Enemy, shieldDef: null, reactionAvailable: true);

            var reactorData = ctx.Registry.Get(reactor);
            var breakerData = ctx.Registry.Get(breaker);
            Assert.IsNotNull(reactorData);
            Assert.IsNotNull(breakerData);

            reactorData.KnowsGlassShieldCantrip = true;
            reactorData.Level = 20;
            reactorData.Intelligence = 30;
            reactorData.GridPosition = new Vector3Int(0, 0, 0);
            Assert.IsTrue(reactorData.ActivateGlassShield(acBonus: 1, hardness: 12, maxHP: 1));

            breakerData.MaxHP = 100;
            breakerData.CurrentHP = 100;
            breakerData.Dexterity = 1;
            breakerData.ReflexProf = ProficiencyRank.Untrained;
            breakerData.GridPosition = new Vector3Int(1, 0, 0); // 5 ft

            int hpBefore = breakerData.CurrentHP;

            bool ok = ctx.Action.Execute(
                reactor,
                incomingDamage: 10,
                result: new ShieldBlockResult(2, 8),
                source: ShieldBlockSource.GlassShield,
                triggerSource: breaker);

            Assert.IsTrue(ok);
            Assert.Less(breakerData.CurrentHP, hpBefore, "In-range breaker should take shard damage.");
        }

        [Test]
        public void Execute_GlassShieldSource_BreakerOutOfRange_NoShardDamage()
        {
            using var ctx = new ShieldBlockActionContext();

            var reactor = ctx.RegisterEntity("Wizard", Team.Player, shieldDef: null, reactionAvailable: true);
            var breaker = ctx.RegisterEntity("Goblin", Team.Enemy, shieldDef: null, reactionAvailable: true);

            var reactorData = ctx.Registry.Get(reactor);
            var breakerData = ctx.Registry.Get(breaker);
            Assert.IsNotNull(reactorData);
            Assert.IsNotNull(breakerData);

            reactorData.KnowsGlassShieldCantrip = true;
            reactorData.Level = 20;
            reactorData.Intelligence = 30;
            reactorData.GridPosition = new Vector3Int(0, 0, 0);
            Assert.IsTrue(reactorData.ActivateGlassShield(acBonus: 1, hardness: 12, maxHP: 1));

            breakerData.MaxHP = 100;
            breakerData.CurrentHP = 100;
            breakerData.GridPosition = new Vector3Int(3, 0, 0); // 15 ft

            int hpBefore = breakerData.CurrentHP;

            bool ok = ctx.Action.Execute(
                reactor,
                incomingDamage: 10,
                result: new ShieldBlockResult(2, 8),
                source: ShieldBlockSource.GlassShield,
                triggerSource: breaker);

            Assert.IsTrue(ok);
            Assert.AreEqual(hpBefore, breakerData.CurrentHP, "Out-of-range breaker must not take shard damage.");
        }

        [Test]
        public void Execute_GlassShieldSource_BreakerWithRaisedShield_CanReduceShardDamage()
        {
            using var ctx = new ShieldBlockActionContext();

            var breakerShieldDef = ctx.CreateShieldDef(hardness: 20, maxHp: 30);
            var reactor = ctx.RegisterEntity("Wizard", Team.Player, shieldDef: null, reactionAvailable: true);
            var breaker = ctx.RegisterEntity("Goblin", Team.Enemy, shieldDef: breakerShieldDef, reactionAvailable: true);

            var reactorData = ctx.Registry.Get(reactor);
            var breakerData = ctx.Registry.Get(breaker);
            Assert.IsNotNull(reactorData);
            Assert.IsNotNull(breakerData);

            reactorData.KnowsGlassShieldCantrip = true;
            reactorData.Level = 5; // 4d4 shard baseline so post-save damage stays > 0.
            reactorData.Intelligence = 30;
            reactorData.GridPosition = new Vector3Int(0, 0, 0);
            Assert.IsTrue(reactorData.ActivateGlassShield(acBonus: 1, hardness: 7, maxHP: 1));

            breakerData.MaxHP = 100;
            breakerData.CurrentHP = 100;
            breakerData.Dexterity = 1;
            breakerData.ReflexProf = ProficiencyRank.Untrained;
            breakerData.GridPosition = new Vector3Int(1, 0, 0);
            breakerData.SetShieldRaised(true);
            breakerData.ReactionAvailable = true;

            int hpBefore = breakerData.CurrentHP;
            int shieldHpBefore = breakerData.EquippedShield.currentHP;
            int shieldEvents = 0;
            int shardDamageEvents = 0;
            ctx.EventBus.OnShieldBlockResolvedTyped += OnShieldBlockResolved;
            ctx.EventBus.OnDamageAppliedTyped += OnDamageApplied;

            try
            {
                bool ok = ctx.Action.Execute(
                    reactor,
                    incomingDamage: 10,
                    result: new ShieldBlockResult(2, 8),
                    source: ShieldBlockSource.GlassShield,
                    triggerSource: breaker);

                Assert.IsTrue(ok);
                Assert.AreEqual(2, shieldEvents, "Expected initial Glass Shield block and breaker shield block on shards.");
                Assert.AreEqual(0, shardDamageEvents, "Breaker shield should fully absorb shard damage with high hardness.");
                Assert.AreEqual(hpBefore, breakerData.CurrentHP, "Shard damage should be fully reduced.");
                Assert.Less(breakerData.EquippedShield.currentHP, shieldHpBefore, "Breaker shield must take self-damage from Shield Block.");
                Assert.IsFalse(breakerData.ReactionAvailable, "Breaker reaction should be consumed by shard Shield Block.");
            }
            finally
            {
                ctx.EventBus.OnShieldBlockResolvedTyped -= OnShieldBlockResolved;
                ctx.EventBus.OnDamageAppliedTyped -= OnDamageApplied;
            }

            void OnShieldBlockResolved(in ShieldBlockResolvedEvent e)
            {
                _ = e;
                shieldEvents++;
            }

            void OnDamageApplied(in DamageAppliedEvent e)
            {
                if (e.sourceActionName == "Glass Shield (Shards)")
                    shardDamageEvents++;
            }
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
