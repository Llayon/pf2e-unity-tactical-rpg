using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.Presentation;
using PF2e.TurnSystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace PF2e.Tests
{
    [TestFixture]
    public class HazardousTerrainRulesTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamage_CriticalSuccess_NoDamageAndPublishesSaveLog()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 14, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Flame Jet",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.BasicSaveDamage,
                    entryDamage: 6,
                    persistentDamage: 0,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 180,
                    telegraphColor: new Color(1f, 0.45f, 0.1f, 0.4f)));

            CombatLogEntry? logEntry = null;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    new Vector3Int(2, 0, 1),
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(20));

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(20, ctx.Registry.Get(actor).CurrentHP);
                Assert.IsTrue(logEntry.HasValue);
                StringAssert.Contains("Flame Jet", logEntry.Value.Message);
                StringAssert.Contains("Critical!", logEntry.Value.Message);
            }
            finally
            {
                ctx.EventBus.OnLogEntry -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry)
            {
                logEntry = entry;
            }
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndPullAndPersistentAcidOnFailure_PublishesTypedHazardEvent()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var pulledCell = new Vector3Int(1, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Hook",
                    hazardCell,
                    HazardEffectKind.BasicSaveDamageAndPullAndPersistentAcidOnFailedSave,
                    entryDamage: 6,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 260,
                    telegraphColor: new Color(0.7f, 0.95f, 0.35f, 0.45f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            HazardTriggeredEvent? triggered = null;
            ctx.EventBus.OnHazardTriggeredTyped += HandleTriggered;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(6, appliedDamage);
                Assert.IsTrue(triggered.HasValue);
                Assert.AreEqual("Acid Hook", triggered.Value.hazardName);
                Assert.AreEqual(HazardEffectKind.BasicSaveDamageAndPullAndPersistentAcidOnFailedSave, triggered.Value.effectKind);
                Assert.IsTrue(triggered.Value.saveResult.HasValue);
                Assert.AreEqual(DegreeOfSuccess.Failure, triggered.Value.saveResult.Value.degree);
                Assert.AreEqual(6, triggered.Value.appliedDamage);
                Assert.AreEqual(1, triggered.Value.movedCells);
                Assert.IsTrue(triggered.Value.pulledTowardOrigin);
                Assert.AreEqual(hazardCell, triggered.Value.positionBefore);
                Assert.AreEqual(pulledCell, triggered.Value.positionAfter);
                Assert.AreEqual(ConditionType.PersistentAcid, triggered.Value.primaryConditionType);
                Assert.AreEqual(2, triggered.Value.primaryConditionValue);
            }
            finally
            {
                ctx.EventBus.OnHazardTriggeredTyped -= HandleTriggered;
            }

            void HandleTriggered(in HazardTriggeredEvent e)
            {
                triggered = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamage_Failure_AppliesFullDamage()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Ice Burst",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.BasicSaveDamage,
                    entryDamage: 5,
                    persistentDamage: 0,
                    damageType: DamageType.Cold,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 170,
                    telegraphColor: new Color(0.5f, 0.8f, 1f, 0.4f)));

            DamageAppliedEvent? damage = null;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    new Vector3Int(2, 0, 1),
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7));

                Assert.AreEqual(5, appliedDamage);
                Assert.AreEqual(15, ctx.Registry.Get(actor).CurrentHP);
                Assert.IsTrue(damage.HasValue);
                Assert.AreEqual(DamageType.Cold, damage.Value.damageType);
                Assert.AreEqual("Ice Burst", damage.Value.sourceActionName);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damage = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_DamageAndProneOnFailure_AppliesDamageAndProne()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Greased Spikes",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.DamageAndProneOnFailure,
                    entryDamage: 4,
                    persistentDamage: 0,
                    damageType: DamageType.Piercing,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 220,
                    telegraphColor: new Color(1f, 0.65f, 0.15f, 0.4f)));

            ConditionChangedEvent? proneDelta = null;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    new Vector3Int(2, 0, 1),
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(8));

                Assert.AreEqual(4, appliedDamage);
                Assert.AreEqual(16, ctx.Registry.Get(actor).CurrentHP);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
                Assert.IsTrue(proneDelta.HasValue);
                Assert.AreEqual(ConditionType.Prone, proneDelta.Value.conditionType);
                Assert.AreEqual(ConditionChangeType.Added, proneDelta.Value.changeType);
            }
            finally
            {
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.Prone)
                    proneDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_ProneOnEntry_AppliesProneWithoutDamage()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1));
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Slick Oil",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.ProneOnEntry,
                    entryDamage: 0,
                    persistentDamage: 0,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 0,
                    aiPressure: 140,
                    telegraphColor: new Color(0.85f, 0.85f, 0.2f, 0.4f)));

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                new Vector3Int(2, 0, 1),
                ctx.EntityManager,
                ctx.EventBus);

            Assert.AreEqual(0, appliedDamage);
            Assert.AreEqual(20, ctx.Registry.Get(actor).CurrentHP);
            Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
        }

        [Test]
        public void TryApplyEntryEffect_PersistentFireOnEntry_AppliesPersistentFireWithoutDirectDamage()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1));
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Burning Pitch",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.PersistentFireOnEntry,
                    entryDamage: 3,
                    persistentDamage: 0,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 0,
                    aiPressure: 190,
                    telegraphColor: new Color(1f, 0.38f, 0.1f, 0.45f)));

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                new Vector3Int(2, 0, 1),
                ctx.EntityManager,
                ctx.EventBus);

            Assert.AreEqual(0, appliedDamage);
            Assert.AreEqual(20, ctx.Registry.Get(actor).CurrentHP);
            Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentFire));
            Assert.AreEqual(3, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentFire));
        }

        [Test]
        public void TryApplyEntryEffect_PersistentFireOnFailedSave_CriticalSuccess_DoesNotApplyPersistentFire()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 14, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Fire Vent",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.PersistentFireOnFailedSave,
                    entryDamage: 2,
                    persistentDamage: 0,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 210,
                    telegraphColor: new Color(1f, 0.34f, 0.1f, 0.45f)));

            CombatLogEntry? logEntry = null;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    new Vector3Int(2, 0, 1),
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(20));

                Assert.AreEqual(0, appliedDamage);
                Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentFire));
                Assert.IsTrue(logEntry.HasValue);
                StringAssert.Contains("Critical!", logEntry.Value.Message);
            }
            finally
            {
                ctx.EventBus.OnLogEntry -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry)
            {
                logEntry = entry;
            }
        }

        [Test]
        public void TryApplyEntryEffect_PersistentFireOnFailedSave_Failure_AppliesPersistentFire()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Fire Vent",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.PersistentFireOnFailedSave,
                    entryDamage: 2,
                    persistentDamage: 0,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 210,
                    telegraphColor: new Color(1f, 0.34f, 0.1f, 0.45f)));

            ConditionChangedEvent? persistentFireDelta = null;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    new Vector3Int(2, 0, 1),
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7));

                Assert.AreEqual(0, appliedDamage);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentFire));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentFire));
                Assert.IsTrue(persistentFireDelta.HasValue);
                Assert.AreEqual(ConditionType.PersistentFire, persistentFireDelta.Value.conditionType);
                Assert.AreEqual(ConditionChangeType.Added, persistentFireDelta.Value.changeType);
            }
            finally
            {
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.PersistentFire)
                    persistentFireDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_PersistentAcidOnFailedSave_Success_DoesNotApplyPersistentAcid()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 14, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Vent",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.PersistentAcidOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 205,
                    telegraphColor: new Color(0.72f, 0.95f, 0.35f, 0.45f)));

            CombatLogEntry? logEntry = null;
            ctx.EventBus.OnLogEntry += HandleLog;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    new Vector3Int(2, 0, 1),
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(20));

                Assert.AreEqual(0, appliedDamage);
                Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
                Assert.IsTrue(logEntry.HasValue);
                StringAssert.Contains("Critical!", logEntry.Value.Message);
            }
            finally
            {
                ctx.EventBus.OnLogEntry -= HandleLog;
            }

            void HandleLog(CombatLogEntry entry)
            {
                logEntry = entry;
            }
        }

        [Test]
        public void TryApplyEntryEffect_PersistentAcidOnFailedSave_Failure_AppliesPersistentAcid()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Vent",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.PersistentAcidOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 205,
                    telegraphColor: new Color(0.72f, 0.95f, 0.35f, 0.45f)));

            ConditionChangedEvent? persistentAcidDelta = null;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    new Vector3Int(2, 0, 1),
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7));

                Assert.AreEqual(0, appliedDamage);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentAcid));
                Assert.IsTrue(persistentAcidDelta.HasValue);
                Assert.AreEqual(ConditionType.PersistentAcid, persistentAcidDelta.Value.conditionType);
                Assert.AreEqual(ConditionChangeType.Added, persistentAcidDelta.Value.changeType);
            }
            finally
            {
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.PersistentAcid)
                    persistentAcidDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndPersistentFireOnFailure_Success_AppliesHalfDamageWithoutPersistentFire()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Explosive Resin",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.BasicSaveDamageAndPersistentFireOnFailure,
                    entryDamage: 6,
                    persistentDamage: 2,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 240,
                    telegraphColor: new Color(1f, 0.32f, 0.08f, 0.5f)));

            DamageAppliedEvent? damage = null;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    new Vector3Int(2, 0, 1),
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(12));

                Assert.AreEqual(3, appliedDamage);
                Assert.AreEqual(17, ctx.Registry.Get(actor).CurrentHP);
                Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentFire));
                Assert.IsTrue(damage.HasValue);
                Assert.AreEqual(3, damage.Value.amount);
                Assert.AreEqual(DamageType.Fire, damage.Value.damageType);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damage = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndPersistentFireOnFailure_Failure_AppliesDamageAndPersistentFire()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Explosive Resin",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.BasicSaveDamageAndPersistentFireOnFailure,
                    entryDamage: 6,
                    persistentDamage: 2,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 240,
                    telegraphColor: new Color(1f, 0.32f, 0.08f, 0.5f)));

            ConditionChangedEvent? persistentFireDelta = null;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    new Vector3Int(2, 0, 1),
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7));

                Assert.AreEqual(6, appliedDamage);
                Assert.AreEqual(14, ctx.Registry.Get(actor).CurrentHP);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentFire));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentFire));
                Assert.IsTrue(persistentFireDelta.HasValue);
                Assert.AreEqual(ConditionType.PersistentFire, persistentFireDelta.Value.conditionType);
            }
            finally
            {
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.PersistentFire)
                    persistentFireDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndPersistentAcidOnFailure_Success_AppliesHalfDamageWithoutPersistentAcid()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Burst Vent",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.BasicSaveDamageAndPersistentAcidOnFailure,
                    entryDamage: 6,
                    persistentDamage: 2,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 240,
                    telegraphColor: new Color(0.68f, 0.94f, 0.38f, 0.45f)));

            DamageAppliedEvent? damage = null;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    new Vector3Int(2, 0, 1),
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(12));

                Assert.AreEqual(3, appliedDamage);
                Assert.AreEqual(17, ctx.Registry.Get(actor).CurrentHP);
                Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
                Assert.IsTrue(damage.HasValue);
                Assert.AreEqual(3, damage.Value.amount);
                Assert.AreEqual(DamageType.Acid, damage.Value.damageType);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damage = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndPersistentAcidOnFailure_Failure_AppliesDamageAndPersistentAcid()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Burst Vent",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.BasicSaveDamageAndPersistentAcidOnFailure,
                    entryDamage: 6,
                    persistentDamage: 2,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 240,
                    telegraphColor: new Color(0.68f, 0.94f, 0.38f, 0.45f)));

            ConditionChangedEvent? persistentAcidDelta = null;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    new Vector3Int(2, 0, 1),
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7));

                Assert.AreEqual(6, appliedDamage);
                Assert.AreEqual(14, ctx.Registry.Get(actor).CurrentHP);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentAcid));
                Assert.IsTrue(persistentAcidDelta.HasValue);
                Assert.AreEqual(ConditionType.PersistentAcid, persistentAcidDelta.Value.conditionType);
            }
            finally
            {
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.PersistentAcid)
                    persistentAcidDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPersistentAcidOnFailedSave_Success_AppliesNoConditions()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Slick",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.ProneAndPersistentAcidOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 220,
                    telegraphColor: new Color(0.72f, 0.95f, 0.35f, 0.45f)));

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                new Vector3Int(2, 0, 1),
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12));

            Assert.AreEqual(0, appliedDamage);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPersistentAcidOnFailedSave_Failure_AppliesBothConditions()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Slick",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.ProneAndPersistentAcidOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 220,
                    telegraphColor: new Color(0.72f, 0.95f, 0.35f, 0.45f)));

            ConditionChangedEvent? proneDelta = null;
            ConditionChangedEvent? persistentAcidDelta = null;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    new Vector3Int(2, 0, 1),
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7));

                Assert.AreEqual(0, appliedDamage);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentAcid));
                Assert.IsTrue(proneDelta.HasValue);
                Assert.IsTrue(persistentAcidDelta.HasValue);
            }
            finally
            {
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.Prone)
                    proneDelta = e;
                else if (e.conditionType == ConditionType.PersistentAcid)
                    persistentAcidDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPersistentFireOnFailedSave_Success_AppliesNoConditions()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Scalding Slick",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.ProneAndPersistentFireOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 220,
                    telegraphColor: new Color(1f, 0.45f, 0.12f, 0.45f)));

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                new Vector3Int(2, 0, 1),
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12));

            Assert.AreEqual(0, appliedDamage);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentFire));
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPersistentFireOnFailedSave_Failure_AppliesBothConditions()
        {
            using var ctx = new HazardRulesContext();
            var actor = ctx.RegisterActor(new Vector3Int(1, 0, 1), dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Scalding Slick",
                    new Vector3Int(2, 0, 1),
                    HazardEffectKind.ProneAndPersistentFireOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 220,
                    telegraphColor: new Color(1f, 0.45f, 0.12f, 0.45f)));

            ConditionChangedEvent? proneDelta = null;
            ConditionChangedEvent? persistentFireDelta = null;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    new Vector3Int(2, 0, 1),
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7));

                Assert.AreEqual(0, appliedDamage);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentFire));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentFire));
                Assert.IsTrue(proneDelta.HasValue);
                Assert.IsTrue(persistentFireDelta.HasValue);
            }
            finally
            {
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.Prone)
                    proneDelta = e;
                else if (e.conditionType == ConditionType.PersistentFire)
                    persistentFireDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_PushOnFailedSave_Success_DoesNotMoveActor()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Gust Plate",
                    hazardCell,
                    HazardEffectKind.PushOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 0,
                    forcedMoveCells: 1,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 180,
                    telegraphColor: new Color(0.65f, 0.85f, 1f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(0, appliedDamage);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
        }

        [Test]
        public void TryApplyEntryEffect_PushOnFailedSave_Failure_PushesActorForwardAndPublishesForcedMove()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var pushedCell = new Vector3Int(3, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Gust Plate",
                    hazardCell,
                    HazardEffectKind.PushOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 0,
                    forcedMoveCells: 1,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 180,
                    telegraphColor: new Color(0.65f, 0.85f, 1f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(pushedCell, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(hazardCell, moved.Value.from);
                Assert.AreEqual(pushedCell, moved.Value.to);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndPushOnFailedSave_Success_AppliesHalfDamageWithoutPush()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Blast Plate",
                    hazardCell,
                    HazardEffectKind.BasicSaveDamageAndPushOnFailedSave,
                    entryDamage: 6,
                    persistentDamage: 0,
                    forcedMoveCells: 1,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 230,
                    telegraphColor: new Color(0.9f, 0.85f, 0.35f, 0.45f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(3, appliedDamage);
            Assert.AreEqual(17, ctx.Registry.Get(actor).CurrentHP);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndPushOnFailedSave_Failure_AppliesDamageAndPushes()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var pushedCell = new Vector3Int(3, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Blast Plate",
                    hazardCell,
                    HazardEffectKind.BasicSaveDamageAndPushOnFailedSave,
                    entryDamage: 6,
                    persistentDamage: 0,
                    forcedMoveCells: 1,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 230,
                    telegraphColor: new Color(0.9f, 0.85f, 0.35f, 0.45f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(6, appliedDamage);
                Assert.AreEqual(14, ctx.Registry.Get(actor).CurrentHP);
                Assert.AreEqual(pushedCell, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndProneAndPushOnFailedSave_Success_AppliesHalfDamageWithoutPushOrProne()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Launch Plate",
                    hazardCell,
                    HazardEffectKind.BasicSaveDamageAndProneAndPushOnFailedSave,
                    entryDamage: 6,
                    persistentDamage: 0,
                    forcedMoveCells: 1,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 235,
                    telegraphColor: new Color(0.92f, 0.82f, 0.3f, 0.45f),
                    forcedMoveElevationPerCell: 1));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(3, appliedDamage);
            Assert.AreEqual(17, ctx.Registry.Get(actor).CurrentHP);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndProneAndPushOnFailedSave_Failure_AppliesDamageVerticalPushAndProne()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var pushedCell = new Vector3Int(3, 1, 1);
            ctx.GridData.SetCell(pushedCell, CellData.CreateWalkable());
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Launch Plate",
                    hazardCell,
                    HazardEffectKind.BasicSaveDamageAndProneAndPushOnFailedSave,
                    entryDamage: 6,
                    persistentDamage: 0,
                    forcedMoveCells: 1,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 235,
                    telegraphColor: new Color(0.92f, 0.82f, 0.3f, 0.45f),
                    forcedMoveElevationPerCell: 1));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ConditionChangedEvent? proneDelta = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(6, appliedDamage);
                Assert.AreEqual(14, ctx.Registry.Get(actor).CurrentHP);
                Assert.AreEqual(pushedCell, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
                Assert.IsTrue(proneDelta.HasValue);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.Prone)
                    proneDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndProneAndPullOnFailedSave_Success_AppliesHalfDamageWithoutPullOrProne()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Snare Plate",
                    hazardCell,
                    HazardEffectKind.BasicSaveDamageAndProneAndPullOnFailedSave,
                    entryDamage: 6,
                    persistentDamage: 0,
                    forcedMoveCells: 1,
                    damageType: DamageType.Slashing,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 235,
                    telegraphColor: new Color(0.72f, 0.84f, 0.95f, 0.45f),
                    forcedMoveElevationPerCell: 1));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(3, appliedDamage);
            Assert.AreEqual(17, ctx.Registry.Get(actor).CurrentHP);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndProneAndPullOnFailedSave_Failure_AppliesDamageVerticalPullAndProne()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var intermediatePulledCell = new Vector3Int(1, 1, 1);
            var pulledCell = new Vector3Int(0, 2, 1);
            ctx.GridData.SetCell(intermediatePulledCell, CellData.CreateWalkable());
            ctx.GridData.SetCell(pulledCell, CellData.CreateWalkable());
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Snare Plate",
                    hazardCell,
                    HazardEffectKind.BasicSaveDamageAndProneAndPullOnFailedSave,
                    entryDamage: 6,
                    persistentDamage: 0,
                    forcedMoveCells: 2,
                    damageType: DamageType.Slashing,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 235,
                    telegraphColor: new Color(0.72f, 0.84f, 0.95f, 0.45f),
                    forcedMoveElevationPerCell: 1));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ConditionChangedEvent? proneDelta = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(6, appliedDamage);
                Assert.AreEqual(14, ctx.Registry.Get(actor).CurrentHP);
                Assert.AreEqual(pulledCell, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
                Assert.IsTrue(proneDelta.HasValue);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.Prone)
                    proneDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_PullOnFailedSave_Success_DoesNotMoveActor()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Hook Chain",
                    hazardCell,
                    HazardEffectKind.PullOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 0,
                    forcedMoveCells: 1,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 175,
                    telegraphColor: new Color(0.55f, 0.75f, 0.95f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(0, appliedDamage);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
        }

        [Test]
        public void TryApplyEntryEffect_PullOnFailedSave_Failure_PullsActorBackTowardOriginAndPublishesForcedMove()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Hook Chain",
                    hazardCell,
                    HazardEffectKind.PullOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 0,
                    forcedMoveCells: 1,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 175,
                    telegraphColor: new Color(0.55f, 0.75f, 0.95f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(origin, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(hazardCell, moved.Value.from);
                Assert.AreEqual(origin, moved.Value.to);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndPullOnFailedSave_Success_AppliesHalfDamageWithoutPull()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Chain Snare",
                    hazardCell,
                    HazardEffectKind.BasicSaveDamageAndPullOnFailedSave,
                    entryDamage: 6,
                    persistentDamage: 0,
                    forcedMoveCells: 1,
                    damageType: DamageType.Slashing,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 235,
                    telegraphColor: new Color(0.7f, 0.82f, 0.95f, 0.45f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(3, appliedDamage);
            Assert.AreEqual(17, ctx.Registry.Get(actor).CurrentHP);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndPullOnFailedSave_Failure_AppliesDamageAndPulls()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Chain Snare",
                    hazardCell,
                    HazardEffectKind.BasicSaveDamageAndPullOnFailedSave,
                    entryDamage: 6,
                    persistentDamage: 0,
                    forcedMoveCells: 1,
                    damageType: DamageType.Slashing,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 235,
                    telegraphColor: new Color(0.7f, 0.82f, 0.95f, 0.45f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(6, appliedDamage);
                Assert.AreEqual(14, ctx.Registry.Get(actor).CurrentHP);
                Assert.AreEqual(origin, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPullOnFailedSave_Success_DoesNotApplyEffects()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Snag Net",
                    hazardCell,
                    HazardEffectKind.ProneAndPullOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 0,
                    forcedMoveCells: 1,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 185,
                    telegraphColor: new Color(0.65f, 0.8f, 0.9f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(0, appliedDamage);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPullOnFailedSave_Failure_PullsAndAppliesProne()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Snag Net",
                    hazardCell,
                    HazardEffectKind.ProneAndPullOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 0,
                    forcedMoveCells: 1,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 185,
                    telegraphColor: new Color(0.65f, 0.8f, 0.9f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ConditionChangedEvent? proneDelta = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(origin, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
                Assert.IsTrue(proneDelta.HasValue);
                Assert.AreEqual(ConditionType.Prone, proneDelta.Value.conditionType);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.Prone)
                    proneDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_PullAndPersistentFireOnFailedSave_Success_DoesNotApplyEffects()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Hook Ember",
                    hazardCell,
                    HazardEffectKind.PullAndPersistentFireOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 205,
                    telegraphColor: new Color(0.95f, 0.5f, 0.2f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(0, appliedDamage);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentFire));
        }

        [Test]
        public void TryApplyEntryEffect_PullAndPersistentFireOnFailedSave_Failure_PullsAndAppliesPersistentFire()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Hook Ember",
                    hazardCell,
                    HazardEffectKind.PullAndPersistentFireOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 205,
                    telegraphColor: new Color(0.95f, 0.5f, 0.2f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ConditionChangedEvent? persistentFireDelta = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(origin, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentFire));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentFire));
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
                Assert.IsTrue(persistentFireDelta.HasValue);
                Assert.AreEqual(ConditionType.PersistentFire, persistentFireDelta.Value.conditionType);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.PersistentFire)
                    persistentFireDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_PullAndPersistentAcidOnFailedSave_Success_DoesNotApplyEffects()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Hook Acid",
                    hazardCell,
                    HazardEffectKind.PullAndPersistentAcidOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 210,
                    telegraphColor: new Color(0.7f, 0.95f, 0.35f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(0, appliedDamage);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
        }

        [Test]
        public void TryApplyEntryEffect_PullAndPersistentAcidOnFailedSave_Failure_PullsAndAppliesPersistentAcid()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Hook Acid",
                    hazardCell,
                    HazardEffectKind.PullAndPersistentAcidOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 210,
                    telegraphColor: new Color(0.7f, 0.95f, 0.35f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ConditionChangedEvent? persistentAcidDelta = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(origin, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentAcid));
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
                Assert.IsTrue(persistentAcidDelta.HasValue);
                Assert.AreEqual(ConditionType.PersistentAcid, persistentAcidDelta.Value.conditionType);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.PersistentAcid)
                    persistentAcidDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPullAndPersistentFireOnFailedSave_Success_DoesNotApplyEffects()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Hook Inferno",
                    hazardCell,
                    HazardEffectKind.ProneAndPullAndPersistentFireOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 225,
                    telegraphColor: new Color(1f, 0.48f, 0.18f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(0, appliedDamage);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentFire));
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPullAndPersistentFireOnFailedSave_Failure_PullsAndAppliesBothConditions()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Hook Inferno",
                    hazardCell,
                    HazardEffectKind.ProneAndPullAndPersistentFireOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 225,
                    telegraphColor: new Color(1f, 0.48f, 0.18f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ConditionChangedEvent? proneDelta = null;
            ConditionChangedEvent? persistentFireDelta = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(origin, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentFire));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentFire));
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
                Assert.IsTrue(proneDelta.HasValue);
                Assert.IsTrue(persistentFireDelta.HasValue);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.Prone)
                    proneDelta = e;
                else if (e.conditionType == ConditionType.PersistentFire)
                    persistentFireDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPushAndPersistentFireOnFailedSave_Success_DoesNotApplyEffects()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Blast Inferno",
                    hazardCell,
                    HazardEffectKind.ProneAndPushAndPersistentFireOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 225,
                    telegraphColor: new Color(1f, 0.56f, 0.18f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(0, appliedDamage);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentFire));
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPushAndPersistentFireOnFailedSave_Failure_PushesAndAppliesBothConditions()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var pushedCell = new Vector3Int(3, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Blast Inferno",
                    hazardCell,
                    HazardEffectKind.ProneAndPushAndPersistentFireOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 225,
                    telegraphColor: new Color(1f, 0.56f, 0.18f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ConditionChangedEvent? proneDelta = null;
            ConditionChangedEvent? persistentFireDelta = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(pushedCell, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentFire));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentFire));
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
                Assert.IsTrue(proneDelta.HasValue);
                Assert.IsTrue(persistentFireDelta.HasValue);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.Prone)
                    proneDelta = e;
                else if (e.conditionType == ConditionType.PersistentFire)
                    persistentFireDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_PushAndPersistentAcidOnFailedSave_Success_DoesNotApplyEffects()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Blast Acid",
                    hazardCell,
                    HazardEffectKind.PushAndPersistentAcidOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 220,
                    telegraphColor: new Color(0.68f, 0.95f, 0.38f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(0, appliedDamage);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
        }

        [Test]
        public void TryApplyEntryEffect_PushAndPersistentAcidOnFailedSave_Failure_PushesAndAppliesPersistentAcid()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var pushedCell = new Vector3Int(3, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Blast Acid",
                    hazardCell,
                    HazardEffectKind.PushAndPersistentAcidOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 220,
                    telegraphColor: new Color(0.68f, 0.95f, 0.38f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ConditionChangedEvent? persistentAcidDelta = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(pushedCell, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentAcid));
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
                Assert.IsTrue(persistentAcidDelta.HasValue);
                Assert.AreEqual(ConditionType.PersistentAcid, persistentAcidDelta.Value.conditionType);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.PersistentAcid)
                    persistentAcidDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndPushAndPersistentAcidOnFailedSave_Success_AppliesHalfDamageWithoutPushOrPersistentAcid()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Launch Plate",
                    hazardCell,
                    HazardEffectKind.BasicSaveDamageAndPushAndPersistentAcidOnFailedSave,
                    entryDamage: 6,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 235,
                    telegraphColor: new Color(0.68f, 0.95f, 0.38f, 0.45f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(3, appliedDamage);
            Assert.AreEqual(17, ctx.Registry.Get(actor).CurrentHP);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndPushAndPersistentAcidOnFailedSave_Failure_AppliesDamagePushAndPersistentAcid()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var pushedCell = new Vector3Int(3, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Launch Plate",
                    hazardCell,
                    HazardEffectKind.BasicSaveDamageAndPushAndPersistentAcidOnFailedSave,
                    entryDamage: 6,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 235,
                    telegraphColor: new Color(0.68f, 0.95f, 0.38f, 0.45f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ConditionChangedEvent? persistentAcidDelta = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(6, appliedDamage);
                Assert.AreEqual(14, ctx.Registry.Get(actor).CurrentHP);
                Assert.AreEqual(pushedCell, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentAcid));
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
                Assert.IsTrue(persistentAcidDelta.HasValue);
                Assert.AreEqual(ConditionType.PersistentAcid, persistentAcidDelta.Value.conditionType);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.PersistentAcid)
                    persistentAcidDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndPullAndPersistentAcidOnFailedSave_Success_AppliesHalfDamageWithoutPullOrPersistentAcid()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Chain Snare",
                    hazardCell,
                    HazardEffectKind.BasicSaveDamageAndPullAndPersistentAcidOnFailedSave,
                    entryDamage: 6,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 235,
                    telegraphColor: new Color(0.7f, 0.92f, 0.45f, 0.45f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(3, appliedDamage);
            Assert.AreEqual(17, ctx.Registry.Get(actor).CurrentHP);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
        }

        [Test]
        public void TryApplyEntryEffect_BasicSaveDamageAndPullAndPersistentAcidOnFailedSave_Failure_AppliesDamagePullAndPersistentAcid()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Chain Snare",
                    hazardCell,
                    HazardEffectKind.BasicSaveDamageAndPullAndPersistentAcidOnFailedSave,
                    entryDamage: 6,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 235,
                    telegraphColor: new Color(0.7f, 0.92f, 0.45f, 0.45f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ConditionChangedEvent? persistentAcidDelta = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(6, appliedDamage);
                Assert.AreEqual(14, ctx.Registry.Get(actor).CurrentHP);
                Assert.AreEqual(origin, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentAcid));
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
                Assert.IsTrue(persistentAcidDelta.HasValue);
                Assert.AreEqual(ConditionType.PersistentAcid, persistentAcidDelta.Value.conditionType);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.PersistentAcid)
                    persistentAcidDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPushAndPersistentAcidOnFailedSave_Success_DoesNotApplyEffects()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Blast Inferno",
                    hazardCell,
                    HazardEffectKind.ProneAndPushAndPersistentAcidOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 225,
                    telegraphColor: new Color(0.68f, 0.95f, 0.38f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(0, appliedDamage);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPushAndPersistentAcidOnFailedSave_Failure_PushesAndAppliesBothConditions()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var pushedCell = new Vector3Int(3, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Blast Inferno",
                    hazardCell,
                    HazardEffectKind.ProneAndPushAndPersistentAcidOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 225,
                    telegraphColor: new Color(0.68f, 0.95f, 0.38f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ConditionChangedEvent? proneDelta = null;
            ConditionChangedEvent? persistentAcidDelta = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(pushedCell, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentAcid));
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
                Assert.IsTrue(proneDelta.HasValue);
                Assert.IsTrue(persistentAcidDelta.HasValue);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.Prone)
                    proneDelta = e;
                else if (e.conditionType == ConditionType.PersistentAcid)
                    persistentAcidDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPullAndPersistentAcidOnFailedSave_Success_DoesNotApplyEffects()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 12, reflexProf: ProficiencyRank.Trained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Hook Inferno",
                    hazardCell,
                    HazardEffectKind.ProneAndPullAndPersistentAcidOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 15,
                    aiPressure: 225,
                    telegraphColor: new Color(0.7f, 0.92f, 0.45f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                actor,
                hazardCell,
                ctx.EntityManager,
                ctx.EventBus,
                rng: new FixedRng(12),
                originCell: origin);

            Assert.AreEqual(0, appliedDamage);
            Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
        }

        [Test]
        public void TryApplyEntryEffect_ProneAndPullAndPersistentAcidOnFailedSave_Failure_PullsAndAppliesBothConditions()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(1, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Acid Hook Inferno",
                    hazardCell,
                    HazardEffectKind.ProneAndPullAndPersistentAcidOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 2,
                    forcedMoveCells: 1,
                    damageType: DamageType.Acid,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 225,
                    telegraphColor: new Color(0.7f, 0.92f, 0.45f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            EntityMovedEvent? moved = null;
            ConditionChangedEvent? proneDelta = null;
            ConditionChangedEvent? persistentAcidDelta = null;
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(origin, ctx.Registry.Get(actor).GridPosition);
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
                Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.PersistentAcid));
                Assert.AreEqual(2, ctx.Registry.Get(actor).GetConditionValue(ConditionType.PersistentAcid));
                Assert.IsTrue(moved.HasValue);
                Assert.AreEqual(MovementTriggerKind.Forced, moved.Value.movementTriggerKind);
                Assert.IsTrue(proneDelta.HasValue);
                Assert.IsTrue(persistentAcidDelta.HasValue);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                moved = e;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                if (e.conditionType == ConditionType.Prone)
                    proneDelta = e;
                else if (e.conditionType == ConditionType.PersistentAcid)
                    persistentAcidDelta = e;
            }
        }

        [Test]
        public void TryApplyEntryEffect_PushOnFailedSave_WithForcedMoveDepthTwo_MovesTwoCellsOnOpenLane()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(0, 0, 1);
            var hazardCell = new Vector3Int(1, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Depth Pusher",
                    hazardCell,
                    HazardEffectKind.PushOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 0,
                    forcedMoveCells: 2,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 190,
                    telegraphColor: new Color(0.82f, 0.7f, 0.2f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            var movedEvents = new List<EntityMovedEvent>();
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(new Vector3Int(3, 0, 1), ctx.Registry.Get(actor).GridPosition);
                Assert.AreEqual(2, movedEvents.Count);
                Assert.AreEqual(new Vector3Int(1, 0, 1), movedEvents[0].from);
                Assert.AreEqual(new Vector3Int(2, 0, 1), movedEvents[0].to);
                Assert.AreEqual(new Vector3Int(2, 0, 1), movedEvents[1].from);
                Assert.AreEqual(new Vector3Int(3, 0, 1), movedEvents[1].to);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                movedEvents.Add(e);
            }
        }

        [Test]
        public void TryApplyEntryEffect_PushOnFailedSave_WithForcedMoveDepthTwo_StopsAtOccupiedCell()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(0, 0, 1);
            var hazardCell = new Vector3Int(1, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.RegisterActor(new Vector3Int(3, 0, 1));
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Depth Pusher",
                    hazardCell,
                    HazardEffectKind.PushOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 0,
                    forcedMoveCells: 2,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 190,
                    telegraphColor: new Color(0.82f, 0.7f, 0.2f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            var movedEvents = new List<EntityMovedEvent>();
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(new Vector3Int(2, 0, 1), ctx.Registry.Get(actor).GridPosition);
                Assert.AreEqual(1, movedEvents.Count);
                Assert.AreEqual(new Vector3Int(1, 0, 1), movedEvents[0].from);
                Assert.AreEqual(new Vector3Int(2, 0, 1), movedEvents[0].to);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                movedEvents.Add(e);
            }
        }

        [Test]
        public void TryApplyEntryEffect_PullOnFailedSave_WithForcedMoveDepthTwo_MovesTwoCellsOnOpenLane()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(2, 0, 1);
            var hazardCell = new Vector3Int(1, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Depth Puller",
                    hazardCell,
                    HazardEffectKind.PullOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 0,
                    forcedMoveCells: 2,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 190,
                    telegraphColor: new Color(0.62f, 0.78f, 0.9f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            var movedEvents = new List<EntityMovedEvent>();
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(new Vector3Int(3, 0, 1), ctx.Registry.Get(actor).GridPosition);
                Assert.AreEqual(2, movedEvents.Count);
                Assert.AreEqual(new Vector3Int(1, 0, 1), movedEvents[0].from);
                Assert.AreEqual(new Vector3Int(2, 0, 1), movedEvents[0].to);
                Assert.AreEqual(new Vector3Int(2, 0, 1), movedEvents[1].from);
                Assert.AreEqual(new Vector3Int(3, 0, 1), movedEvents[1].to);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                movedEvents.Add(e);
            }
        }

        [Test]
        public void TryApplyEntryEffect_PullOnFailedSave_WithForcedMoveDepthTwo_StopsAtGridEdge()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(3, 0, 1);
            var hazardCell = new Vector3Int(2, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Depth Puller",
                    hazardCell,
                    HazardEffectKind.PullOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 0,
                    forcedMoveCells: 2,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 190,
                    telegraphColor: new Color(0.62f, 0.78f, 0.9f, 0.4f)));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            var movedEvents = new List<EntityMovedEvent>();
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(new Vector3Int(3, 0, 1), ctx.Registry.Get(actor).GridPosition);
                Assert.AreEqual(1, movedEvents.Count);
                Assert.AreEqual(new Vector3Int(2, 0, 1), movedEvents[0].from);
                Assert.AreEqual(new Vector3Int(3, 0, 1), movedEvents[0].to);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                movedEvents.Add(e);
            }
        }

        [Test]
        public void TryApplyEntryEffect_PushOnFailedSave_WithElevationPerStep_MovesUphillAcrossTwoLevels()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(0, 0, 1);
            var hazardCell = new Vector3Int(1, 0, 1);
            ctx.GridData.SetCell(new Vector3Int(2, 1, 1), CellData.CreateWalkable());
            ctx.GridData.SetCell(new Vector3Int(3, 2, 1), CellData.CreateWalkable());
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Lift Pusher",
                    hazardCell,
                    HazardEffectKind.PushOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 0,
                    forcedMoveCells: 2,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 190,
                    telegraphColor: new Color(0.82f, 0.7f, 0.2f, 0.4f),
                    forcedMoveElevationPerCell: 1));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            var movedEvents = new List<EntityMovedEvent>();
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(new Vector3Int(3, 2, 1), ctx.Registry.Get(actor).GridPosition);
                Assert.AreEqual(2, movedEvents.Count);
                Assert.AreEqual(new Vector3Int(1, 0, 1), movedEvents[0].from);
                Assert.AreEqual(new Vector3Int(2, 1, 1), movedEvents[0].to);
                Assert.AreEqual(new Vector3Int(2, 1, 1), movedEvents[1].from);
                Assert.AreEqual(new Vector3Int(3, 2, 1), movedEvents[1].to);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                movedEvents.Add(e);
            }
        }

        [Test]
        public void TryApplyEntryEffect_PullOnFailedSave_WithElevationPerStep_MovesAcrossElevatedSteps()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(2, 0, 1);
            var hazardCell = new Vector3Int(1, 0, 1);
            ctx.GridData.SetCell(new Vector3Int(2, 1, 1), CellData.CreateWalkable());
            ctx.GridData.SetCell(new Vector3Int(3, 2, 1), CellData.CreateWalkable());
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Drop Puller",
                    hazardCell,
                    HazardEffectKind.PullOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 0,
                    forcedMoveCells: 2,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 190,
                    telegraphColor: new Color(0.62f, 0.78f, 0.9f, 0.4f),
                    forcedMoveElevationPerCell: 1));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            var movedEvents = new List<EntityMovedEvent>();
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(new Vector3Int(3, 2, 1), ctx.Registry.Get(actor).GridPosition);
                Assert.AreEqual(2, movedEvents.Count);
                Assert.AreEqual(new Vector3Int(1, 0, 1), movedEvents[0].from);
                Assert.AreEqual(new Vector3Int(2, 1, 1), movedEvents[0].to);
                Assert.AreEqual(new Vector3Int(2, 1, 1), movedEvents[1].from);
                Assert.AreEqual(new Vector3Int(3, 2, 1), movedEvents[1].to);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                movedEvents.Add(e);
            }
        }

        [Test]
        public void TryApplyEntryEffect_PushOnFailedSave_WithElevationPerStep_StopsWhenElevatedCellMissing()
        {
            using var ctx = new HazardRulesContext();
            var origin = new Vector3Int(0, 0, 1);
            var hazardCell = new Vector3Int(1, 0, 1);
            var actor = ctx.RegisterActor(origin, dexterity: 10, reflexProf: ProficiencyRank.Untrained);
            ctx.SetHazards(
                new GridHazardDefinition(
                    "Lift Pusher",
                    hazardCell,
                    HazardEffectKind.PushOnFailedSave,
                    entryDamage: 0,
                    persistentDamage: 0,
                    forcedMoveCells: 2,
                    damageType: DamageType.Bludgeoning,
                    saveType: SaveType.Reflex,
                    saveDc: 16,
                    aiPressure: 190,
                    telegraphColor: new Color(0.82f, 0.7f, 0.2f, 0.4f),
                    forcedMoveElevationPerCell: 1));

            ctx.MoveActorWithoutHazard(actor, hazardCell);

            var movedEvents = new List<EntityMovedEvent>();
            ctx.EventBus.OnEntityMovedTyped += HandleMoved;
            try
            {
                int appliedDamage = HazardousTerrainRules.TryApplyEntryEffect(
                    actor,
                    hazardCell,
                    ctx.EntityManager,
                    ctx.EventBus,
                    rng: new FixedRng(7),
                    originCell: origin);

                Assert.AreEqual(0, appliedDamage);
                Assert.AreEqual(hazardCell, ctx.Registry.Get(actor).GridPosition);
                Assert.AreEqual(0, movedEvents.Count);
            }
            finally
            {
                ctx.EventBus.OnEntityMovedTyped -= HandleMoved;
            }

            void HandleMoved(in EntityMovedEvent e)
            {
                movedEvents.Add(e);
            }
        }

        private sealed class HazardRulesContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;
            private int entityCounter;

            public readonly CombatEventBus EventBus;
            public readonly GridManager GridManager;
            public readonly GridData GridData;
            public readonly EntityManager EntityManager;
            public readonly EntityRegistry Registry;
            public readonly OccupancyMap Occupancy;
            public readonly HazardLogForwarder HazardLogForwarder;

            public HazardRulesContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("HazardRulesTests_Root");

                var eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                var gridManagerGo = new GameObject("GridManager");
                gridManagerGo.transform.SetParent(root.transform);
                GridManager = gridManagerGo.AddComponent<GridManager>();
                GridData = new GridData(cellWorldSize: 1f, heightStepWorld: 1f);
                SeedWalkableGrid(GridData, sizeX: 4, sizeZ: 4);
                SetAutoPropertyBackingField(GridManager, "Data", GridData);

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                Occupancy = new OccupancyMap(Registry);
                SetPrivateField(EntityManager, "gridManager", GridManager);
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);
                SetAutoPropertyBackingField(EntityManager, "Occupancy", Occupancy);
                SetAutoPropertyBackingField(EntityManager, "Pathfinding", new GridPathfinding());

                var hazardLogForwarderGo = new GameObject("HazardLogForwarder");
                hazardLogForwarderGo.transform.SetParent(root.transform);
                HazardLogForwarder = hazardLogForwarderGo.AddComponent<HazardLogForwarder>();
                SetPrivateField(HazardLogForwarder, "eventBus", EventBus);
                SetPrivateField(HazardLogForwarder, "entityManager", EntityManager);
                InvokePrivate(HazardLogForwarder, "OnEnable");
            }

            public EntityHandle RegisterActor(
                Vector3Int position,
                int maxHp = 20,
                int dexterity = 10,
                ProficiencyRank reflexProf = ProficiencyRank.Untrained)
            {
                var handle = Registry.Register(new EntityData
                {
                    Name = $"Actor_{++entityCounter}",
                    Team = Team.Player,
                    Level = 0,
                    MaxHP = maxHp,
                    CurrentHP = maxHp,
                    Speed = 25,
                    Strength = 10,
                    Dexterity = dexterity,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    ReflexProf = reflexProf,
                    GridPosition = position
                });

                Assert.IsTrue(Occupancy.Place(handle, position));
                return handle;
            }

            public void SetHazards(params GridHazardDefinition[] hazards)
            {
                var hazardController = GridManager.gameObject.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", GridManager);
                SetPrivateField(hazardController, "hazards", new List<GridHazardDefinition>(hazards));
                hazardController.ApplyHazardsNow();
            }

            public void MoveActorWithoutHazard(EntityHandle actor, Vector3Int destination)
            {
                var data = Registry.Get(actor);
                Assert.IsNotNull(data);
                Assert.IsTrue(Occupancy.Move(actor, destination, data.SizeCells));
                data.GridPosition = destination;
            }

            public void Dispose()
            {
                if (HazardLogForwarder != null)
                    InvokePrivate(HazardLogForwarder, "OnDisable");

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

        private static void SeedWalkableGrid(GridData gridData, int sizeX, int sizeZ)
        {
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                    gridData.SetCell(new Vector3Int(x, 0, z), CellData.CreateWalkable());
            }
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
