using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PF2e.Core;

namespace PF2e.Tests
{
    [TestFixture]
    public class CombatEventBusTooltipPublishTests
    {
        [Test]
        public void Publish_ActorWithPayload_FiresBothEvents_InOrder()
        {
            var go = new GameObject("CombatEventBusTooltipPublishTests_Bus");
            var bus = go.AddComponent<CombatEventBus>();

            var order = new List<string>(2);
            CombatLogEntry withEntry = default;
            CombatLogEntry plainEntry = default;
            CombatLogTooltipPayload? withPayload = null;

            bus.OnLogEntryWithTooltip += HandleWithTooltip;
            bus.OnLogEntry += HandlePlain;
            try
            {
                var payload = new CombatLogTooltipPayload(new[]
                {
                    new TooltipEntry("atk", "Attack", "d20(12) + ATK(+9) + MAP(-5) = 16")
                });

                bus.Publish(new EntityHandle(7), "swings", CombatLogCategory.Attack, payload);

                CollectionAssert.AreEqual(new[] { "with", "plain" }, order);
                Assert.AreEqual(new EntityHandle(7), withEntry.Actor);
                Assert.AreEqual(new EntityHandle(7), plainEntry.Actor);
                Assert.IsTrue(withPayload.HasValue);
                Assert.AreEqual("atk", withPayload.Value.entries[0].token);
            }
            finally
            {
                bus.OnLogEntryWithTooltip -= HandleWithTooltip;
                bus.OnLogEntry -= HandlePlain;
                Object.DestroyImmediate(go);
            }

            void HandleWithTooltip(CombatLogEntry entry, CombatLogTooltipPayload? payload)
            {
                order.Add("with");
                withEntry = entry;
                withPayload = payload;
            }

            void HandlePlain(CombatLogEntry entry)
            {
                order.Add("plain");
                plainEntry = entry;
            }
        }

        [Test]
        public void Publish_LegacyActorPath_FiresBothEvents_WithNullPayload()
        {
            var go = new GameObject("CombatEventBusTooltipPublishTests_Bus");
            var bus = go.AddComponent<CombatEventBus>();

            int withCount = 0;
            int plainCount = 0;
            CombatLogTooltipPayload? observedPayload = default;

            bus.OnLogEntryWithTooltip += HandleWithTooltip;
            bus.OnLogEntry += HandlePlain;
            try
            {
                bus.Publish(new EntityHandle(3), "legacy", CombatLogCategory.Debug);

                Assert.AreEqual(1, withCount);
                Assert.AreEqual(1, plainCount);
                Assert.IsFalse(observedPayload.HasValue);
            }
            finally
            {
                bus.OnLogEntryWithTooltip -= HandleWithTooltip;
                bus.OnLogEntry -= HandlePlain;
                Object.DestroyImmediate(go);
            }

            void HandleWithTooltip(CombatLogEntry entry, CombatLogTooltipPayload? payload)
            {
                withCount++;
                observedPayload = payload;
            }

            void HandlePlain(CombatLogEntry entry)
            {
                plainCount++;
            }
        }

        [Test]
        public void Publish_LegacyCombatLogEntryPath_FiresBothEvents_WithNullPayload()
        {
            var go = new GameObject("CombatEventBusTooltipPublishTests_Bus");
            var bus = go.AddComponent<CombatEventBus>();

            int withCount = 0;
            int plainCount = 0;
            CombatLogTooltipPayload? observedPayload = default;

            bus.OnLogEntryWithTooltip += HandleWithTooltip;
            bus.OnLogEntry += HandlePlain;
            try
            {
                bus.Publish(new CombatLogEntry(new EntityHandle(2), "entry path", CombatLogCategory.Turn));

                Assert.AreEqual(1, withCount);
                Assert.AreEqual(1, plainCount);
                Assert.IsFalse(observedPayload.HasValue);
            }
            finally
            {
                bus.OnLogEntryWithTooltip -= HandleWithTooltip;
                bus.OnLogEntry -= HandlePlain;
                Object.DestroyImmediate(go);
            }

            void HandleWithTooltip(CombatLogEntry entry, CombatLogTooltipPayload? payload)
            {
                withCount++;
                observedPayload = payload;
            }

            void HandlePlain(CombatLogEntry entry)
            {
                plainCount++;
            }
        }

        [Test]
        public void PublishSystem_LegacyPaths_FireBothEvents_WithNullPayload()
        {
            var go = new GameObject("CombatEventBusTooltipPublishTests_Bus");
            var bus = go.AddComponent<CombatEventBus>();

            int withCount = 0;
            int plainCount = 0;
            CombatLogTooltipPayload? observedPayload = default;
            CombatLogEntry observedEntry = default;

            bus.OnLogEntryWithTooltip += HandleWithTooltip;
            bus.OnLogEntry += HandlePlain;
            try
            {
                bus.PublishSystem("system one");
                bus.PublishSystem("system two", CombatLogCategory.CombatStart);

                Assert.AreEqual(2, withCount);
                Assert.AreEqual(2, plainCount);
                Assert.IsFalse(observedPayload.HasValue);
                Assert.AreEqual(EntityHandle.None, observedEntry.Actor);
                Assert.AreEqual(CombatLogCategory.CombatStart, observedEntry.Category);
            }
            finally
            {
                bus.OnLogEntryWithTooltip -= HandleWithTooltip;
                bus.OnLogEntry -= HandlePlain;
                Object.DestroyImmediate(go);
            }

            void HandleWithTooltip(CombatLogEntry entry, CombatLogTooltipPayload? payload)
            {
                withCount++;
                observedPayload = payload;
                observedEntry = entry;
            }

            void HandlePlain(CombatLogEntry entry)
            {
                plainCount++;
            }
        }
    }
}
