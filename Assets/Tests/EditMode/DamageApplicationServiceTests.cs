using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class DamageApplicationServiceTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ApplyDamage_ClampsHp_HandlesDeath_AndPublishesEvent()
        {
            using var ctx = new DamageContext();
            var source = ctx.RegisterEntity("Source", Team.Player, hp: 10);
            var target = ctx.RegisterEntity("Target", Team.Enemy, hp: 5);

            int damageEventCount = 0;
            DamageAppliedEvent lastDamage = default;
            int defeatedCount = 0;
            EntityDefeatedEvent lastDefeated = default;

            ctx.EventBus.OnDamageAppliedTyped += OnDamageApplied;
            ctx.EventBus.OnEntityDefeated += OnDefeated;

            try
            {
                int applied = DamageApplicationService.ApplyDamage(
                    source,
                    target,
                    amount: 9,
                    damageType: DamageType.Bludgeoning,
                    sourceActionName: "Trip",
                    isCritical: true,
                    entityManager: ctx.EntityManager,
                    eventBus: ctx.EventBus);

                var targetData = ctx.Registry.Get(target);
                Assert.AreEqual(5, applied, "Applied damage should clamp to target HP.");
                Assert.AreEqual(0, targetData.CurrentHP);
                Assert.AreEqual(1, damageEventCount);
                Assert.AreEqual(1, defeatedCount);

                Assert.AreEqual(source, lastDamage.source);
                Assert.AreEqual(target, lastDamage.target);
                Assert.AreEqual(5, lastDamage.amount);
                Assert.AreEqual("Trip", lastDamage.sourceActionName);
                Assert.AreEqual(DamageType.Bludgeoning, lastDamage.damageType);
                Assert.IsTrue(lastDamage.isCritical);
                Assert.AreEqual(5, lastDamage.hpBefore);
                Assert.AreEqual(0, lastDamage.hpAfter);
                Assert.IsTrue(lastDamage.targetDefeated);

                Assert.AreEqual(target, lastDefeated.handle);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= OnDamageApplied;
                ctx.EventBus.OnEntityDefeated -= OnDefeated;
            }

            void OnDamageApplied(in DamageAppliedEvent e)
            {
                damageEventCount++;
                lastDamage = e;
            }

            void OnDefeated(in EntityDefeatedEvent e)
            {
                defeatedCount++;
                lastDefeated = e;
            }
        }

        [Test]
        public void ApplyDamage_ZeroAmount_DoesNotPublish_AndDoesNotChangeHp()
        {
            using var ctx = new DamageContext();
            var source = ctx.RegisterEntity("Source", Team.Player, hp: 10);
            var target = ctx.RegisterEntity("Target", Team.Enemy, hp: 5);

            int damageEventCount = 0;
            ctx.EventBus.OnDamageAppliedTyped += OnDamageApplied;

            try
            {
                int applied = DamageApplicationService.ApplyDamage(
                    source,
                    target,
                    amount: 0,
                    damageType: DamageType.Bludgeoning,
                    sourceActionName: "Trip",
                    isCritical: true,
                    entityManager: ctx.EntityManager,
                    eventBus: ctx.EventBus);

                Assert.AreEqual(0, applied);
                Assert.AreEqual(5, ctx.Registry.Get(target).CurrentHP);
                Assert.AreEqual(0, damageEventCount);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= OnDamageApplied;
            }

            void OnDamageApplied(in DamageAppliedEvent e)
            {
                _ = e;
                damageEventCount++;
            }
        }

        [Test]
        public void ApplyDamage_WithShieldBlockContext_AppliesReductionAndPublishesShieldBlockEvent()
        {
            using var ctx = new DamageContext();
            var source = ctx.RegisterEntity("Source", Team.Player, hp: 10);
            var target = ctx.RegisterEntity("Target", Team.Enemy, hp: 5);

            var shieldDef = ScriptableObject.CreateInstance<ShieldDefinition>();
            shieldDef.itemName = "Test Shield";
            shieldDef.acBonus = 2;
            shieldDef.hardness = 5;
            shieldDef.maxHP = 20;

            try
            {
                var targetData = ctx.Registry.Get(target);
                targetData.EquippedShield = ShieldInstance.CreateEquipped(shieldDef);
                targetData.RaiseShield();
                targetData.ReactionAvailable = true;

                int damageEventCount = 0;
                int shieldEventCount = 0;
                DamageAppliedEvent lastDamage = default;
                ShieldBlockResolvedEvent lastShield = default;

                ctx.EventBus.OnDamageAppliedTyped += OnDamageApplied;
                ctx.EventBus.OnShieldBlockResolvedTyped += OnShieldBlockResolved;

                try
                {
                    var initiative = new List<InitiativeEntry>
                    {
                        new InitiativeEntry { Handle = source, Roll = new CheckRoll(12, 3, CheckSource.Perception()), IsPlayer = true },
                        new InitiativeEntry { Handle = target, Roll = new CheckRoll(10, 2, CheckSource.Perception()), IsPlayer = false }
                    };
                    var reactionBuffer = new List<ReactionOption>(2);
                    var policy = new AutoShieldBlockPolicy();

                    int applied = DamageApplicationService.ApplyDamage(
                        source,
                        target,
                        amount: 7,
                        damageType: DamageType.Bludgeoning,
                        sourceActionName: "Trip",
                        isCritical: true,
                        entityManager: ctx.EntityManager,
                        eventBus: ctx.EventBus,
                        initiativeOrder: initiative,
                        getEntity: handle => ctx.Registry.Get(handle),
                        canUseReaction: handle =>
                        {
                            var data = ctx.Registry.Get(handle);
                            return data != null && data.IsAlive && data.ReactionAvailable;
                        },
                        reactionPolicy: policy,
                        shieldBlockAction: ctx.ShieldBlockAction,
                        reactionBuffer: reactionBuffer,
                        reactionOwnerTag: "DamageApplicationServiceTests");

                    Assert.AreEqual(2, applied, "Incoming 7 with hardness 5 should deal 2 final damage.");
                    Assert.AreEqual(3, targetData.CurrentHP);
                    Assert.AreEqual(1, damageEventCount);
                    Assert.AreEqual(1, shieldEventCount);
                    Assert.AreEqual(2, lastDamage.amount);
                    Assert.AreEqual(7, lastShield.incomingDamage);
                    Assert.AreEqual(5, lastShield.damageReduction);
                    Assert.IsFalse(targetData.ReactionAvailable, "Shield Block reaction should be consumed.");
                    Assert.AreEqual(18, targetData.EquippedShield.currentHP, "Shield should take incoming-hardness self-damage.");
                }
                finally
                {
                    ctx.EventBus.OnDamageAppliedTyped -= OnDamageApplied;
                    ctx.EventBus.OnShieldBlockResolvedTyped -= OnShieldBlockResolved;
                }

                void OnDamageApplied(in DamageAppliedEvent e)
                {
                    damageEventCount++;
                    lastDamage = e;
                }

                void OnShieldBlockResolved(in ShieldBlockResolvedEvent e)
                {
                    shieldEventCount++;
                    lastShield = e;
                }
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        private sealed class DamageContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject shieldBlockActionGo;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public ShieldBlockAction ShieldBlockAction { get; }
            public EntityRegistry Registry => EntityManager.Registry;

            public DamageContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("Damage_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("Damage_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                shieldBlockActionGo = new GameObject("Damage_ShieldBlockAction_Test");
                ShieldBlockAction = shieldBlockActionGo.AddComponent<ShieldBlockAction>();
                SetPrivateField(ShieldBlockAction, "entityManager", EntityManager);
                SetPrivateField(ShieldBlockAction, "eventBus", EventBus);
            }

            public EntityHandle RegisterEntity(string name, Team team, int hp)
            {
                var data = new EntityData
                {
                    Name = name,
                    Team = team,
                    Size = CreatureSize.Medium,
                    Level = 1,
                    MaxHP = hp,
                    CurrentHP = hp,
                    GridPosition = Vector3Int.zero,
                    Strength = 10,
                    Dexterity = 10,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                };

                return Registry.Register(data);
            }

            public void Dispose()
            {
                if (shieldBlockActionGo != null) Object.DestroyImmediate(shieldBlockActionGo);
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
    }
}
