using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace PF2e.Tests
{
    [TestFixture]
    public class HazardLogForwarderTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void HazardTriggered_PublishesReadableSummary_WithTooltipPayload()
        {
            using var ctx = new HazardLogContext();
            var target = ctx.RegisterEntity("Goblin", Team.Enemy);

            var ev = new HazardTriggeredEvent(
                target,
                "Acid Hook",
                new Vector3Int(2, 0, 1),
                HazardEffectKind.BasicSaveDamageAndPullAndPersistentAcidOnFailedSave,
                DamageType.Acid,
                rolledDamage: 6,
                appliedDamage: 6,
                saveType: SaveType.Reflex,
                saveResult: new CheckResult(
                    new CheckRoll(5, 3, CheckSource.Save(SaveType.Reflex)),
                    dc: 16,
                    degree: DegreeOfSuccess.Failure),
                primaryConditionType: ConditionType.PersistentAcid,
                primaryConditionValue: 2,
                secondaryConditionType: null,
                secondaryConditionValue: 0,
                positionBefore: new Vector3Int(2, 0, 1),
                positionAfter: new Vector3Int(1, 0, 1),
                movedCells: 1,
                pulledTowardOrigin: true,
                hpBefore: 20,
                hpAfter: 14,
                targetDefeated: false);

            CombatLogEntry lastEntry = default;
            CombatLogTooltipPayload? lastTooltip = null;
            ctx.EventBus.OnLogEntryWithTooltip += HandleLog;
            try
            {
                ctx.EventBus.PublishHazardTriggered(in ev);

                StringAssert.Contains("Acid Hook", Strip(lastEntry.Message));
                StringAssert.Contains("Failure", Strip(lastEntry.Message));
                StringAssert.Contains("6 Acid", Strip(lastEntry.Message));
                StringAssert.Contains("pulled 1 cell", Strip(lastEntry.Message));
                StringAssert.Contains("persistent acid 2", Strip(lastEntry.Message));
                Assert.IsTrue(lastTooltip.HasValue);
                Assert.IsTrue(lastTooltip.Value.HasEntries);
                Assert.AreEqual("Acid Hook", lastTooltip.Value.entries[0].title);
                StringAssert.Contains("cell (2, 0, 1)", lastTooltip.Value.entries[0].body);
                StringAssert.Contains("Reflex DC 16", lastTooltip.Value.entries[0].body);
                StringAssert.Contains("Damage: 6 acid (20->14 HP)", lastTooltip.Value.entries[0].body);
                StringAssert.Contains("Forced movement: pulled 1 cell to (1, 0, 1)", lastTooltip.Value.entries[0].body);
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
        public void HazardTriggered_CriticalSuccessNoEffect_StillPublishesTooltip()
        {
            using var ctx = new HazardLogContext();
            var target = ctx.RegisterEntity("Fighter", Team.Player);

            var ev = new HazardTriggeredEvent(
                target,
                "Hook Snare",
                new Vector3Int(4, 0, 2),
                HazardEffectKind.ProneAndPullOnFailedSave,
                DamageType.Bludgeoning,
                rolledDamage: 0,
                appliedDamage: 0,
                saveType: SaveType.Reflex,
                saveResult: new CheckResult(
                    new CheckRoll(20, 8, CheckSource.Save(SaveType.Reflex)),
                    dc: 16,
                    degree: DegreeOfSuccess.CriticalSuccess),
                primaryConditionType: null,
                primaryConditionValue: 0,
                secondaryConditionType: null,
                secondaryConditionValue: 0,
                positionBefore: new Vector3Int(4, 0, 2),
                positionAfter: new Vector3Int(4, 0, 2),
                movedCells: 0,
                pulledTowardOrigin: true,
                hpBefore: 20,
                hpAfter: 20,
                targetDefeated: false);

            CombatLogEntry lastEntry = default;
            CombatLogTooltipPayload? lastTooltip = null;
            ctx.EventBus.OnLogEntryWithTooltip += HandleLog;
            try
            {
                ctx.EventBus.PublishHazardTriggered(in ev);

                StringAssert.Contains("Hook Snare", Strip(lastEntry.Message));
                StringAssert.Contains("Critical!", Strip(lastEntry.Message));
                StringAssert.Contains("no effect", Strip(lastEntry.Message));
                Assert.IsTrue(lastTooltip.HasValue);
                StringAssert.Contains("Reflex: Critical Success!", lastTooltip.Value.entries[0].body);
                StringAssert.Contains("HP: 20->20", lastTooltip.Value.entries[0].body);
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

        private sealed class HazardLogContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public HazardLogForwarder Forwarder { get; }
            public EntityRegistry Registry => EntityManager.Registry;

            public HazardLogContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("HazardLogForwarderTests_Root");

                var eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());

                var forwarderGo = new GameObject("HazardLogForwarder");
                forwarderGo.transform.SetParent(root.transform);
                Forwarder = forwarderGo.AddComponent<HazardLogForwarder>();
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
                    MaxHP = 20,
                    CurrentHP = 20,
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
