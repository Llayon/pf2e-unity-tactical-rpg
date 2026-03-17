using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Managers;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class SpellLogForwarderTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ForceBarrage_PublishesSpellLogLine_WithTooltipPayload()
        {
            using var ctx = new SpellLogContext();

            var caster = ctx.RegisterEntity("Wizard", Team.Player);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy);

            var ev = new SpellResolvedEvent(
                SpellId.ForceBarrage,
                caster,
                actionCost: 2,
                spellDc: 0,
                spellAttackModifier: 0,
                rolledDamage: 0,
                targetOutcomes: new[]
                {
                    new SpellResolvedTargetOutcome(
                        target,
                        shardCount: 2,
                        shardRolls: new[] { 2, 4 },
                        rolledDamage: 6,
                        attackResult: null,
                        saveResult: null,
                        appliedConditionType: null,
                        appliedConditionValue: 0,
                        appliedConditionRounds: 0,
                        resolvedDamage: 6,
                        appliedDamage: 6,
                        hpBefore: 10,
                        hpAfter: 4,
                        targetDefeated: false)
                });

            CombatLogEntry lastEntry = default;
            CombatLogTooltipPayload? lastTooltip = null;
            int count = 0;
            ctx.EventBus.OnLogEntryWithTooltip += HandleLog;
            try
            {
                ctx.EventBus.PublishSpellResolved(in ev);

                Assert.AreEqual(1, count);
                Assert.AreEqual(CombatLogCategory.Spell, lastEntry.Category);
                StringAssert.Contains("Force Barrage", Strip(lastEntry.Message));
                StringAssert.Contains("Goblin", Strip(lastEntry.Message));
                Assert.IsTrue(lastTooltip.HasValue);
                Assert.IsTrue(lastTooltip.Value.HasEntries);
                Assert.AreEqual("Force Barrage", lastTooltip.Value.entries[0].title);
                StringAssert.Contains("2 shard(s)", lastTooltip.Value.entries[0].body);
                StringAssert.Contains("Goblin", lastTooltip.Value.entries[0].body);
            }
            finally
            {
                ctx.EventBus.OnLogEntryWithTooltip -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry, CombatLogTooltipPayload? tooltipPayload)
            {
                count++;
                lastEntry = entry;
                lastTooltip = tooltipPayload;
            }
        }

        [Test]
        public void ElectricArc_PublishesSaveBreakdownTooltip()
        {
            using var ctx = new SpellLogContext();

            var caster = ctx.RegisterEntity("Wizard", Team.Player);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy);
            var save = new CheckResult(
                new CheckRoll(5, 3, CheckSource.Save(SaveType.Reflex)),
                dc: 17,
                degree: DegreeOfSuccess.Failure);

            var ev = new SpellResolvedEvent(
                SpellId.ElectricArc,
                caster,
                actionCost: 2,
                spellDc: 17,
                spellAttackModifier: 0,
                rolledDamage: 5,
                targetOutcomes: new[]
                {
                    new SpellResolvedTargetOutcome(
                        target,
                        shardCount: 0,
                        shardRolls: null,
                        rolledDamage: 5,
                        attackResult: null,
                        saveResult: save,
                        appliedConditionType: null,
                        appliedConditionValue: 0,
                        appliedConditionRounds: 0,
                        resolvedDamage: 5,
                        appliedDamage: 5,
                        hpBefore: 10,
                        hpAfter: 5,
                        targetDefeated: false)
                });

            CombatLogTooltipPayload? lastTooltip = null;
            ctx.EventBus.OnLogEntryWithTooltip += HandleLog;
            try
            {
                ctx.EventBus.PublishSpellResolved(in ev);

                Assert.IsTrue(lastTooltip.HasValue);
                Assert.IsTrue(lastTooltip.Value.HasEntries);
                StringAssert.Contains("Reflex DC 17", lastTooltip.Value.entries[0].body);
                StringAssert.Contains("rolled 5 electricity", lastTooltip.Value.entries[0].body);
                StringAssert.Contains("Failure", lastTooltip.Value.entries[0].body);
            }
            finally
            {
                ctx.EventBus.OnLogEntryWithTooltip -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry, CombatLogTooltipPayload? tooltipPayload)
            {
                lastTooltip = tooltipPayload;
            }
        }

        [Test]
        public void Snowball_PublishesAttackBreakdownTooltip()
        {
            using var ctx = new SpellLogContext();

            var caster = ctx.RegisterEntity("Wizard", Team.Player);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy);
            var attack = new CheckResult(
                new CheckRoll(14, 7, CheckSource.Custom("SPA")),
                dc: 13,
                degree: DegreeOfSuccess.Success);

            var ev = new SpellResolvedEvent(
                SpellId.Snowball,
                caster,
                actionCost: 2,
                spellDc: 0,
                spellAttackModifier: 7,
                rolledDamage: 5,
                targetOutcomes: new[]
                {
                    new SpellResolvedTargetOutcome(
                        target,
                        shardCount: 0,
                        shardRolls: null,
                        rolledDamage: 5,
                        attackResult: attack,
                        saveResult: null,
                        appliedConditionType: ConditionType.SpeedPenalty,
                        appliedConditionValue: 5,
                        appliedConditionRounds: 1,
                        resolvedDamage: 5,
                        appliedDamage: 5,
                        hpBefore: 10,
                        hpAfter: 5,
                        targetDefeated: false)
                });

            CombatLogEntry lastEntry = default;
            CombatLogTooltipPayload? lastTooltip = null;
            ctx.EventBus.OnLogEntryWithTooltip += HandleLog;
            try
            {
                ctx.EventBus.PublishSpellResolved(in ev);

                StringAssert.Contains("Snowball", Strip(lastEntry.Message));
                StringAssert.Contains("speed", Strip(lastEntry.Message));
                Assert.IsTrue(lastTooltip.HasValue);
                StringAssert.Contains("spell attack +7", lastTooltip.Value.entries[0].body);
                StringAssert.Contains("Success", lastTooltip.Value.entries[0].body);
                StringAssert.Contains("speed -5 ft", lastTooltip.Value.entries[0].body);
            }
            finally
            {
                ctx.EventBus.OnLogEntryWithTooltip -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry, CombatLogTooltipPayload? tooltipPayload)
            {
                lastEntry = entry;
                lastTooltip = tooltipPayload;
            }
        }

        [Test]
        public void BurningHands_PublishesAreaSaveBreakdownTooltip()
        {
            using var ctx = new SpellLogContext();

            var caster = ctx.RegisterEntity("Wizard", Team.Player);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy);
            var save = new CheckResult(
                new CheckRoll(4, 3, CheckSource.Save(SaveType.Reflex)),
                dc: 17,
                degree: DegreeOfSuccess.Failure);

            var ev = new SpellResolvedEvent(
                SpellId.BurningHands,
                caster,
                actionCost: 2,
                spellDc: 17,
                spellAttackModifier: 0,
                rolledDamage: 6,
                targetOutcomes: new[]
                {
                    new SpellResolvedTargetOutcome(
                        target,
                        shardCount: 0,
                        shardRolls: null,
                        rolledDamage: 6,
                        attackResult: null,
                        saveResult: save,
                        appliedConditionType: null,
                        appliedConditionValue: 0,
                        appliedConditionRounds: 0,
                        resolvedDamage: 6,
                        appliedDamage: 6,
                        hpBefore: 10,
                        hpAfter: 4,
                        targetDefeated: false)
                });

            CombatLogEntry lastEntry = default;
            CombatLogTooltipPayload? lastTooltip = null;
            ctx.EventBus.OnLogEntryWithTooltip += HandleLog;
            try
            {
                ctx.EventBus.PublishSpellResolved(in ev);

                StringAssert.Contains("Burning Hands", Strip(lastEntry.Message));
                StringAssert.Contains("Goblin", Strip(lastEntry.Message));
                Assert.IsTrue(lastTooltip.HasValue);
                StringAssert.Contains("15 ft cone", lastTooltip.Value.entries[0].body);
                StringAssert.Contains("Reflex DC 17", lastTooltip.Value.entries[0].body);
                StringAssert.Contains("6 fire", lastTooltip.Value.entries[0].body);
            }
            finally
            {
                ctx.EventBus.OnLogEntryWithTooltip -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry, CombatLogTooltipPayload? tooltipPayload)
            {
                lastEntry = entry;
                lastTooltip = tooltipPayload;
            }
        }

        private sealed class SpellLogContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public SpellLogForwarder Forwarder { get; }
            public EntityRegistry Registry => EntityManager.Registry;

            public SpellLogContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("SpellLogForwarderTests_Root");

                var eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());

                var forwarderGo = new GameObject("SpellLogForwarder");
                forwarderGo.transform.SetParent(root.transform);
                Forwarder = forwarderGo.AddComponent<SpellLogForwarder>();
                SetPrivateField(Forwarder, "eventBus", EventBus);
                SetPrivateField(Forwarder, "entityManager", EntityManager);
                InvokePrivate(Forwarder, "OnEnable");
            }

            public EntityHandle RegisterEntity(string name, Team team)
            {
                return Registry.Register(new EntityData
                {
                    Name = name,
                    Team = team,
                    Level = 1,
                    MaxHP = 10,
                    CurrentHP = 10,
                    Speed = 25,
                    Size = CreatureSize.Medium
                });
            }

            public void Dispose()
            {
                if (Forwarder != null)
                    InvokePrivate(Forwarder, "OnDisable");

                if (root != null)
                    Object.DestroyImmediate(root);

                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private static string Strip(string text)
        {
            return Regex.Replace(text ?? string.Empty, "<[^>]+>", string.Empty);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, InstanceNonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}");
            method.Invoke(target, null);
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
