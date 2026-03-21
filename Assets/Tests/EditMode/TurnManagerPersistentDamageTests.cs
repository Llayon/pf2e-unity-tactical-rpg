using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace PF2e.Tests
{
    [TestFixture]
    public class TurnManagerPersistentDamageTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ApplyEndTurnEffects_PersistentFireFailedFlatCheck_DealsDamageAndKeepsCondition()
        {
            using var ctx = new PersistentDamageContext();
            var actor = ctx.RegisterActor(currentHp: 20);
            var actorData = ctx.Registry.Get(actor);
            ctx.ApplyPersistentFire(actorData, value: 3);
            ctx.SetOngoingEffectRng(new FixedRng(12));

            DamageAppliedEvent? damageEvent = null;
            CombatLogEntry? flatCheckLog = null;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            ctx.EventBus.OnLogEntry += HandleLog;

            try
            {
                ctx.ApplyEndTurnEffects(actor, actorData);

                Assert.AreEqual(17, actorData.CurrentHP);
                Assert.IsTrue(actorData.HasCondition(ConditionType.PersistentFire));
                Assert.AreEqual(3, actorData.GetConditionValue(ConditionType.PersistentFire));
                Assert.IsTrue(damageEvent.HasValue);
                Assert.AreEqual(DamageType.Fire, damageEvent.Value.damageType);
                Assert.AreEqual(PersistentDamageRules.PersistentFireActionName, damageEvent.Value.sourceActionName);
                Assert.IsTrue(flatCheckLog.HasValue);
                StringAssert.Contains("flat check d20(12) vs DC 15 - Failure", flatCheckLog.Value.Message);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
                ctx.EventBus.OnLogEntry -= HandleLog;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damageEvent = e;
            }

            void HandleLog(CombatLogEntry entry)
            {
                if (entry.Message.Contains("flat check"))
                    flatCheckLog = entry;
            }
        }

        [Test]
        public void ApplyEndTurnEffects_PersistentFireSuccessfulFlatCheck_RemovesConditionAfterDamage()
        {
            using var ctx = new PersistentDamageContext();
            var actor = ctx.RegisterActor(currentHp: 20);
            var actorData = ctx.Registry.Get(actor);
            ctx.ApplyPersistentFire(actorData, value: 2);
            ctx.SetOngoingEffectRng(new FixedRng(17));

            ConditionChangedEvent? removedDelta = null;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;

            try
            {
                ctx.ApplyEndTurnEffects(actor, actorData);

                Assert.AreEqual(18, actorData.CurrentHP);
                Assert.IsFalse(actorData.HasCondition(ConditionType.PersistentFire));
                Assert.IsTrue(removedDelta.HasValue);
                Assert.AreEqual(ConditionType.PersistentFire, removedDelta.Value.conditionType);
                Assert.AreEqual(ConditionChangeType.Removed, removedDelta.Value.changeType);
            }
            finally
            {
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.PersistentFire && e.changeType == ConditionChangeType.Removed)
                    removedDelta = e;
            }
        }

        [Test]
        public void ApplyEndTurnEffects_PersistentAcidFailedFlatCheck_DealsDamageAndKeepsCondition()
        {
            using var ctx = new PersistentDamageContext();
            var actor = ctx.RegisterActor(currentHp: 20);
            var actorData = ctx.Registry.Get(actor);
            ctx.ApplyPersistentAcid(actorData, value: 4);
            ctx.SetOngoingEffectRng(new FixedRng(11));

            DamageAppliedEvent? damageEvent = null;
            CombatLogEntry? flatCheckLog = null;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            ctx.EventBus.OnLogEntry += HandleLog;

            try
            {
                ctx.ApplyEndTurnEffects(actor, actorData);

                Assert.AreEqual(16, actorData.CurrentHP);
                Assert.IsTrue(actorData.HasCondition(ConditionType.PersistentAcid));
                Assert.AreEqual(4, actorData.GetConditionValue(ConditionType.PersistentAcid));
                Assert.IsTrue(damageEvent.HasValue);
                Assert.AreEqual(DamageType.Acid, damageEvent.Value.damageType);
                Assert.AreEqual(PersistentDamageRules.PersistentAcidActionName, damageEvent.Value.sourceActionName);
                Assert.IsTrue(flatCheckLog.HasValue);
                StringAssert.Contains("persistent acid flat check d20(11) vs DC 15 - Failure", flatCheckLog.Value.Message);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
                ctx.EventBus.OnLogEntry -= HandleLog;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damageEvent = e;
            }

            void HandleLog(CombatLogEntry entry)
            {
                if (entry.Message.Contains("persistent acid flat check"))
                    flatCheckLog = entry;
            }
        }

        [Test]
        public void ApplyEndTurnEffects_PersistentAcidSuccessfulFlatCheck_RemovesConditionAfterDamage()
        {
            using var ctx = new PersistentDamageContext();
            var actor = ctx.RegisterActor(currentHp: 20);
            var actorData = ctx.Registry.Get(actor);
            ctx.ApplyPersistentAcid(actorData, value: 2);
            ctx.SetOngoingEffectRng(new FixedRng(17));

            ConditionChangedEvent? removedDelta = null;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;

            try
            {
                ctx.ApplyEndTurnEffects(actor, actorData);

                Assert.AreEqual(18, actorData.CurrentHP);
                Assert.IsFalse(actorData.HasCondition(ConditionType.PersistentAcid));
                Assert.IsTrue(removedDelta.HasValue);
                Assert.AreEqual(ConditionType.PersistentAcid, removedDelta.Value.conditionType);
                Assert.AreEqual(ConditionChangeType.Removed, removedDelta.Value.changeType);
            }
            finally
            {
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.PersistentAcid && e.changeType == ConditionChangeType.Removed)
                    removedDelta = e;
            }
        }

        private sealed class PersistentDamageContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;
            private readonly ConditionService conditionService = new();
            private int actorCounter;

            public readonly CombatEventBus EventBus;
            public readonly EntityManager EntityManager;
            public readonly TurnManager TurnManager;
            public readonly EntityRegistry Registry;

            public PersistentDamageContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("TurnManagerPersistentDamageTests_Root");

                var eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                var turnManagerGo = new GameObject("TurnManager");
                turnManagerGo.transform.SetParent(root.transform);
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);
            }

            public EntityHandle RegisterActor(int currentHp)
            {
                return Registry.Register(new EntityData
                {
                    Name = $"PersistentActor_{++actorCounter}",
                    Team = Team.Player,
                    Level = 1,
                    Strength = 10,
                    Dexterity = 10,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    MaxHP = 20,
                    CurrentHP = currentHp,
                    ActionsRemaining = 3
                });
            }

            public void ApplyPersistentFire(EntityData actorData, int value)
            {
                var deltas = new List<ConditionDelta>(1);
                conditionService.AddOrRefresh(actorData, ConditionType.PersistentFire, value, rounds: -1, deltas);
            }

            public void ApplyPersistentAcid(EntityData actorData, int value)
            {
                var deltas = new List<ConditionDelta>(1);
                conditionService.AddOrRefresh(actorData, ConditionType.PersistentAcid, value, rounds: -1, deltas);
            }

            public void SetOngoingEffectRng(IRng rng)
            {
                InvokePrivate(TurnManager, "SetOngoingEffectRngForTesting", new object[] { rng });
            }

            public void ApplyEndTurnEffects(EntityHandle actor, EntityData actorData)
            {
                InvokePrivate(TurnManager, "ApplyEndTurnEffects", new object[] { actor, actorData });
            }

            public void Dispose()
            {
                if (root != null)
                    Object.DestroyImmediate(root);

                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private sealed class FixedRng : IRng
        {
            private readonly Queue<int> d20Rolls;

            public FixedRng(params int[] d20Rolls)
            {
                this.d20Rolls = new Queue<int>(d20Rolls ?? new[] { 1 });
            }

            public int RollD20()
            {
                return d20Rolls.Count > 0 ? d20Rolls.Dequeue() : 1;
            }

            public int RollDie(int sides)
            {
                return 1;
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

        private static void InvokePrivate(object target, string methodName, object[] args)
        {
            var method = target.GetType().GetMethod(methodName, InstanceNonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}");
            method.Invoke(target, args);
        }
    }
}
