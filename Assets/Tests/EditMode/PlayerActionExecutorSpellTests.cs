using System.Collections.Generic;
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
    public class PlayerActionExecutorSpellTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryBeginForceBarrage_DoesNotSpendActions()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0));
            ctx.Registry.Get(actor).KnowsForceBarrage = true;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            bool began = ctx.Executor.TryBeginForceBarrage(2);

            Assert.IsTrue(began);
            Assert.AreEqual(3, ctx.Registry.Get(actor).ActionsRemaining);
        }

        [Test]
        public void TryConfirmForceBarrage_SameTarget_GroupsIntoSingleDamagePacket()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), intelligence: 18);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), hp: 20);
            ctx.Registry.Get(actor).KnowsForceBarrage = true;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            int damageEventCount = 0;
            DamageAppliedEvent lastDamage = default;
            SpellResolvedEvent lastSpell = default;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmForceBarrage(
                    new[] { target, target, target },
                    actionCount: 3,
                    rng: new FixedRng(dieRolls: new[] { 1, 2, 3 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(1, damageEventCount);
                Assert.AreEqual(9, lastDamage.amount);
                Assert.AreEqual(DamageType.Force, lastDamage.damageType);
                Assert.AreEqual(SpellCatalog.Get(SpellId.ForceBarrage).actionName, lastDamage.sourceActionName);
                Assert.AreEqual(0, ctx.Registry.Get(actor).ActionsRemaining);

                Assert.AreEqual(SpellId.ForceBarrage, lastSpell.spellId);
                Assert.AreEqual(1, lastSpell.targetOutcomes.Length);
                Assert.AreEqual(3, lastSpell.targetOutcomes[0].shardCount);
                Assert.AreEqual(9, lastSpell.targetOutcomes[0].rolledDamage);
                Assert.AreEqual(9, lastSpell.targetOutcomes[0].appliedDamage);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damageEventCount++;
                lastDamage = e;
            }

            void HandleSpell(in SpellResolvedEvent e)
            {
                lastSpell = e;
            }
        }

        [Test]
        public void TryConfirmForceBarrage_SplitTargets_PublishesSeparateDamagePackets()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0));
            var targetA = ctx.RegisterEntity("Goblin_A", Team.Enemy, new Vector3Int(1, 0, 0), hp: 10);
            var targetB = ctx.RegisterEntity("Goblin_B", Team.Enemy, new Vector3Int(2, 0, 0), hp: 10);
            ctx.Registry.Get(actor).KnowsForceBarrage = true;
            ctx.SetCurrentActor(actor, actionsRemaining: 2);

            var damageEvents = new List<DamageAppliedEvent>();
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            try
            {
                bool executed = ctx.Executor.TryConfirmForceBarrage(
                    new[] { targetA, targetB },
                    actionCount: 2,
                    rng: new FixedRng(dieRolls: new[] { 1, 4 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(2, damageEvents.Count);
                Assert.AreEqual(2, damageEvents[0].amount);
                Assert.AreEqual(targetA, damageEvents[0].target);
                Assert.AreEqual(5, damageEvents[1].amount);
                Assert.AreEqual(targetB, damageEvents[1].target);
                Assert.AreEqual(0, ctx.Registry.Get(actor).ActionsRemaining);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damageEvents.Add(e);
            }
        }

        [Test]
        public void TryConfirmForceBarrage_OutOfRange_ReturnsFalse_AndSpendsNoActions()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0));
            var farTarget = ctx.RegisterEntity("Distant Goblin", Team.Enemy, new Vector3Int(25, 0, 0));
            ctx.Registry.Get(actor).KnowsForceBarrage = true;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            bool executed = ctx.Executor.TryConfirmForceBarrage(
                new[] { farTarget },
                actionCount: 1,
                rng: new FixedRng(dieRolls: new[] { 4 }));

            Assert.IsFalse(executed);
            Assert.AreEqual(3, ctx.Registry.Get(actor).ActionsRemaining);
            Assert.AreEqual(10, ctx.Registry.Get(farTarget).CurrentHP);
        }

        [Test]
        public void TryConfirmElectricArc_TwoTargets_UsesBasicSavesAndSpendsTwoActions()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), intelligence: 18);
            var targetA = ctx.RegisterEntity("Goblin_A", Team.Enemy, new Vector3Int(1, 0, 0), dexterity: 10, hp: 12);
            var targetB = ctx.RegisterEntity("Goblin_B", Team.Enemy, new Vector3Int(2, 0, 0), dexterity: 18, hp: 12);
            ctx.Registry.Get(actor).KnowsElectricArc = true;
            ctx.Registry.Get(targetB).ReflexProf = ProficiencyRank.Expert;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            var damageEvents = new List<DamageAppliedEvent>();
            SpellResolvedEvent lastSpell = default;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmElectricArc(
                    new[] { targetA, targetB },
                    rng: new FixedRng(d20Rolls: new[] { 5, 10 }, dieRolls: new[] { 3, 1 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(2, damageEvents.Count);
                Assert.AreEqual(4, damageEvents[0].amount);
                Assert.AreEqual(targetA, damageEvents[0].target);
                Assert.AreEqual(2, damageEvents[1].amount);
                Assert.AreEqual(targetB, damageEvents[1].target);
                Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);

                Assert.AreEqual(SpellId.ElectricArc, lastSpell.spellId);
                Assert.AreEqual(17, lastSpell.spellDc);
                Assert.AreEqual(4, lastSpell.rolledDamage);
                Assert.AreEqual(DegreeOfSuccess.Failure, lastSpell.targetOutcomes[0].saveResult.Value.degree);
                Assert.AreEqual(DegreeOfSuccess.Success, lastSpell.targetOutcomes[1].saveResult.Value.degree);
                Assert.AreEqual(4, lastSpell.targetOutcomes[0].appliedDamage);
                Assert.AreEqual(2, lastSpell.targetOutcomes[1].appliedDamage);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damageEvents.Add(e);
            }

            void HandleSpell(in SpellResolvedEvent e)
            {
                lastSpell = e;
            }
        }

        [Test]
        public void TryConfirmSnowball_Success_AppliesColdDamageSpeedPenaltyAndSpendsTwoActions()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), intelligence: 18);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), hp: 12);
            ctx.Registry.Get(actor).KnowsSnowball = true;
            ctx.Registry.Get(target).Speed = 25;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            DamageAppliedEvent lastDamage = default;
            ConditionChangedEvent lastCondition = default;
            SpellResolvedEvent lastSpell = default;
            int damageCount = 0;
            int conditionCount = 0;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmSnowball(
                    target,
                    rng: new FixedRng(d20Rolls: new[] { 6 }, dieRolls: new[] { 2, 3 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(1, damageCount);
                Assert.AreEqual(5, lastDamage.amount);
                Assert.AreEqual(DamageType.Cold, lastDamage.damageType);
                Assert.AreEqual(1, conditionCount);
                Assert.AreEqual(ConditionType.SpeedPenalty, lastCondition.conditionType);
                Assert.AreEqual(5, lastCondition.newValue);
                Assert.AreEqual(1, lastCondition.newRemainingRounds);
                Assert.AreEqual(20, ctx.Registry.Get(target).EffectiveSpeed);
                Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);

                Assert.AreEqual(SpellId.Snowball, lastSpell.spellId);
                Assert.AreEqual(7, lastSpell.spellAttackModifier);
                Assert.AreEqual(5, lastSpell.rolledDamage);
                Assert.AreEqual(DegreeOfSuccess.Success, lastSpell.targetOutcomes[0].attackResult.Value.degree);
                Assert.AreEqual(5, lastSpell.targetOutcomes[0].appliedDamage);
                Assert.AreEqual(5, lastSpell.targetOutcomes[0].appliedConditionValue);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damageCount++;
                lastDamage = e;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                conditionCount++;
                lastCondition = e;
            }

            void HandleSpell(in SpellResolvedEvent e)
            {
                lastSpell = e;
            }
        }

        [Test]
        public void TryConfirmSnowball_CriticalSuccess_DoublesDamageAndAppliesTenFootPenalty()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), intelligence: 18);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), hp: 12);
            ctx.Registry.Get(actor).KnowsSnowball = true;
            ctx.Registry.Get(target).Speed = 25;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            SpellResolvedEvent lastSpell = default;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmSnowball(
                    target,
                    rng: new FixedRng(d20Rolls: new[] { 20 }, dieRolls: new[] { 1, 2 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(6, ctx.Registry.Get(target).CurrentHP);
                Assert.AreEqual(15, ctx.Registry.Get(target).EffectiveSpeed);
                Assert.AreEqual(ConditionType.SpeedPenalty, ctx.Registry.Get(target).Conditions[0].Type);
                Assert.AreEqual(10, ctx.Registry.Get(target).Conditions[0].Value);
                Assert.AreEqual(DegreeOfSuccess.CriticalSuccess, lastSpell.targetOutcomes[0].attackResult.Value.degree);
                Assert.AreEqual(6, lastSpell.targetOutcomes[0].appliedDamage);
                Assert.AreEqual(10, lastSpell.targetOutcomes[0].appliedConditionValue);
            }
            finally
            {
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleSpell(in SpellResolvedEvent e)
            {
                lastSpell = e;
            }
        }

        [Test]
        public void TryConfirmSnowball_Failure_HasNoEffectAndStillSpendsTwoActions()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), intelligence: 18);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), hp: 12);
            ctx.Registry.Get(actor).KnowsSnowball = true;
            ctx.Registry.Get(target).Speed = 25;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            int damageCount = 0;
            int conditionCount = 0;
            SpellResolvedEvent lastSpell = default;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmSnowball(
                    target,
                    rng: new FixedRng(d20Rolls: new[] { 1 }, dieRolls: new[] { 4, 4 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(0, damageCount);
                Assert.AreEqual(0, conditionCount);
                Assert.AreEqual(12, ctx.Registry.Get(target).CurrentHP);
                Assert.AreEqual(25, ctx.Registry.Get(target).EffectiveSpeed);
                Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);
                Assert.AreEqual(DegreeOfSuccess.CriticalFailure, lastSpell.targetOutcomes[0].attackResult.Value.degree);
                Assert.AreEqual(0, lastSpell.targetOutcomes[0].appliedDamage);
                Assert.AreEqual(0, lastSpell.targetOutcomes[0].appliedConditionValue);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damageCount++;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                conditionCount++;
            }

            void HandleSpell(in SpellResolvedEvent e)
            {
                lastSpell = e;
            }
        }

        [Test]
        public void TryConfirmFear_Success_AppliesFrightenedOneAndSpendsTwoActions()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), intelligence: 18);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), hp: 12);
            ctx.Registry.Get(actor).KnowsFear = true;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            int damageCount = 0;
            int conditionCount = 0;
            ConditionChangedEvent lastCondition = default;
            SpellResolvedEvent lastSpell = default;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmFear(
                    target,
                    rng: new FixedRng(d20Rolls: new[] { 14 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(0, damageCount);
                Assert.AreEqual(1, conditionCount);
                Assert.AreEqual(ConditionType.Frightened, lastCondition.conditionType);
                Assert.AreEqual(1, lastCondition.newValue);
                Assert.AreEqual(1, ctx.Registry.Get(target).GetConditionValue(ConditionType.Frightened));
                Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);
                Assert.AreEqual(DegreeOfSuccess.Success, lastSpell.targetOutcomes[0].saveResult.Value.degree);
                Assert.AreEqual(ConditionType.Frightened, lastSpell.targetOutcomes[0].appliedConditionType);
                Assert.AreEqual(1, lastSpell.targetOutcomes[0].appliedConditionValue);
                Assert.AreEqual(0, lastSpell.targetOutcomes[0].appliedDamage);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damageCount++;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                conditionCount++;
                lastCondition = e;
            }

            void HandleSpell(in SpellResolvedEvent e)
            {
                lastSpell = e;
            }
        }

        [Test]
        public void TryConfirmFear_CriticalFailure_AppliesFrightenedThreeAndFleeing()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), intelligence: 18);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), hp: 12);
            ctx.Registry.Get(actor).KnowsFear = true;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            int damageCount = 0;
            var changedConditions = new List<ConditionType>();
            SpellResolvedEvent lastSpell = default;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            ctx.EventBus.OnConditionChangedTyped += HandleCondition;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmFear(
                    target,
                    rng: new FixedRng(d20Rolls: new[] { 1 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(0, damageCount);
                CollectionAssert.AreEquivalent(
                    new[] { ConditionType.Frightened, ConditionType.Fleeing },
                    changedConditions);
                Assert.AreEqual(3, ctx.Registry.Get(target).GetConditionValue(ConditionType.Frightened));
                Assert.IsTrue(ctx.Registry.Get(target).HasCondition(ConditionType.Fleeing));
                Assert.AreEqual(1, ctx.Registry.Get(target).Conditions.Find(c => c.Type == ConditionType.Fleeing).RemainingRounds);
                Assert.AreEqual(actor, ctx.Registry.Get(target).FleeingSourceHandle);
                Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);
                Assert.AreEqual(DegreeOfSuccess.CriticalFailure, lastSpell.targetOutcomes[0].saveResult.Value.degree);
                Assert.AreEqual(ConditionType.Frightened, lastSpell.targetOutcomes[0].appliedConditionType);
                Assert.AreEqual(3, lastSpell.targetOutcomes[0].appliedConditionValue);
                Assert.AreEqual(0, lastSpell.targetOutcomes[0].appliedDamage);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
                ctx.EventBus.OnConditionChangedTyped -= HandleCondition;
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damageCount++;
            }

            void HandleCondition(in ConditionChangedEvent e)
            {
                changedConditions.Add(e.conditionType);
            }

            void HandleSpell(in SpellResolvedEvent e)
            {
                lastSpell = e;
            }
        }

        [Test]
        public void TryConfirmBurningHands_MultipleTargets_UsesBasicSavesAndSpendsTwoActions()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), intelligence: 18);
            var targetA = ctx.RegisterEntity("Goblin_A", Team.Enemy, new Vector3Int(0, 0, 2), dexterity: 10, hp: 12);
            var targetB = ctx.RegisterEntity("Goblin_B", Team.Enemy, new Vector3Int(1, 0, 2), dexterity: 18, hp: 12);
            ctx.Registry.Get(actor).KnowsBurningHands = true;
            ctx.Registry.Get(targetB).ReflexProf = ProficiencyRank.Expert;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            var damageEvents = new List<DamageAppliedEvent>();
            SpellResolvedEvent lastSpell = default;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmBurningHands(
                    new Vector3Int(0, 0, 1),
                    rng: new FixedRng(d20Rolls: new[] { 5, 10 }, dieRolls: new[] { 3, 2 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(2, damageEvents.Count);
                Assert.AreEqual(5, damageEvents[0].amount);
                Assert.AreEqual(targetA, damageEvents[0].target);
                Assert.AreEqual(2, damageEvents[1].amount);
                Assert.AreEqual(targetB, damageEvents[1].target);
                Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);

                Assert.AreEqual(SpellId.BurningHands, lastSpell.spellId);
                Assert.AreEqual(17, lastSpell.spellDc);
                Assert.AreEqual(5, lastSpell.rolledDamage);
                Assert.AreEqual(2, lastSpell.targetOutcomes.Length);
                Assert.AreEqual(DegreeOfSuccess.Failure, lastSpell.targetOutcomes[0].saveResult.Value.degree);
                Assert.AreEqual(DegreeOfSuccess.Success, lastSpell.targetOutcomes[1].saveResult.Value.degree);
                Assert.AreEqual(5, lastSpell.targetOutcomes[0].appliedDamage);
                Assert.AreEqual(2, lastSpell.targetOutcomes[1].appliedDamage);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damageEvents.Add(e);
            }

            void HandleSpell(in SpellResolvedEvent e)
            {
                lastSpell = e;
            }
        }

        [Test]
        public void TryConfirmBurningHands_AlliesInCone_AppliesFriendlyFire()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), intelligence: 18);
            var ally = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(-1, 0, 2), hp: 20);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(0, 0, 2), hp: 12);
            ctx.Registry.Get(actor).KnowsBurningHands = true;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            var damageTargets = new List<EntityHandle>();
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            try
            {
                bool executed = ctx.Executor.TryConfirmBurningHands(
                    new Vector3Int(0, 0, 1),
                    rng: new FixedRng(d20Rolls: new[] { 5, 5 }, dieRolls: new[] { 2, 2 }));

                Assert.IsTrue(executed);
                CollectionAssert.AreEquivalent(new[] { ally, enemy }, damageTargets);
                Assert.AreEqual(16, ctx.Registry.Get(ally).CurrentHP);
                Assert.AreEqual(8, ctx.Registry.Get(enemy).CurrentHP);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damageTargets.Add(e.target);
            }
        }

        private sealed class SpellExecutorContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;
            private readonly List<EntityHandle> registeredHandles = new();

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public TurnManager TurnManager { get; }
            public PlayerActionExecutor Executor { get; }
            public EntityRegistry Registry { get; }

            public SpellExecutorContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("PlayerActionExecutorSpellTests_Root");

                var eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                var turnManagerGo = new GameObject("TurnManager");
                turnManagerGo.transform.SetParent(root.transform);
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);
                SetPrivateField(TurnManager, "initiativeOrder", new List<InitiativeEntry>());
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
                SetPrivateField(TurnManager, "roundNumber", 1);

                var executorGo = new GameObject("Executor");
                executorGo.transform.SetParent(root.transform);
                Executor = executorGo.AddComponent<PlayerActionExecutor>();
                SetPrivateField(Executor, "turnManager", TurnManager);
                SetPrivateField(Executor, "entityManager", EntityManager);
                SetPrivateField(Executor, "eventBus", EventBus);
            }

            public EntityHandle RegisterEntity(
                string name,
                Team team,
                Vector3Int gridPosition,
                int hp = 10,
                int intelligence = 10,
                int dexterity = 10)
            {
                var handle = Registry.Register(new EntityData
                {
                    Name = name,
                    Team = team,
                    Level = 1,
                    Strength = 10,
                    Dexterity = dexterity,
                    Constitution = 10,
                    Intelligence = intelligence,
                    Wisdom = 10,
                    Charisma = 10,
                    MaxHP = hp,
                    CurrentHP = hp,
                    GridPosition = gridPosition,
                    ActionsRemaining = 3,
                    ReactionAvailable = true
                });

                registeredHandles.Add(handle);
                return handle;
            }

            public void SetCurrentActor(EntityHandle actor, int actionsRemaining)
            {
                var actorData = Registry.Get(actor);
                Assert.IsNotNull(actorData);
                actorData.ActionsRemaining = actionsRemaining;

                var order = new List<InitiativeEntry>
                {
                    new InitiativeEntry
                    {
                        Handle = actor,
                        Roll = new CheckRoll(10, 0, CheckSource.Perception()),
                        IsPlayer = true
                    }
                };

                for (int i = 0; i < registeredHandles.Count; i++)
                {
                    var handle = registeredHandles[i];
                    if (handle == actor)
                        continue;

                    var data = Registry.Get(handle);

                    order.Add(new InitiativeEntry
                    {
                        Handle = handle,
                        Roll = new CheckRoll(5, 0, CheckSource.Perception()),
                        IsPlayer = data != null && data.Team == Team.Player
                    });
                }

                SetPrivateField(TurnManager, "initiativeOrder", order);
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
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
            private readonly Queue<int> d20;
            private readonly Queue<int> dice;

            public FixedRng(IEnumerable<int> d20Rolls = null, IEnumerable<int> dieRolls = null)
            {
                d20 = d20Rolls != null ? new Queue<int>(d20Rolls) : new Queue<int>();
                dice = dieRolls != null ? new Queue<int>(dieRolls) : new Queue<int>();
            }

            public int RollD20()
            {
                return d20.Count > 0 ? d20.Dequeue() : 10;
            }

            public int RollDie(int sides)
            {
                if (sides <= 0)
                    return 0;

                int value = dice.Count > 0 ? dice.Dequeue() : 1;
                if (value < 1) value = 1;
                if (value > sides) value = sides;
                return value;
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
