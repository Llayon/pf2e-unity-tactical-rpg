using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Grid;
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
        public void TryConfirmFear_CurrentEnemyTurnActor_Succeeds()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("EnemyWizard", Team.Enemy, new Vector3Int(0, 0, 0), intelligence: 18);
            var target = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(1, 0, 0), hp: 20);
            ctx.Registry.Get(actor).KnowsFear = true;
            ctx.SetCurrentActor(actor, actionsRemaining: 3, turnState: TurnState.EnemyTurn);

            bool executed = ctx.Executor.TryConfirmFear(target, rng: new FixedRng(d20Rolls: new[] { 2 }));

            Assert.IsTrue(executed);
            Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);
            Assert.IsTrue(ctx.Registry.Get(target).HasCondition(ConditionType.Frightened));
        }

        [Test]
        public void TryConfirmBurningHands_CurrentEnemyTurnActor_Succeeds()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("EnemyCaster", Team.Enemy, new Vector3Int(0, 0, 0), intelligence: 18);
            var targetA = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 2), hp: 16);
            var targetB = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(1, 0, 2), hp: 14);
            ctx.Registry.Get(actor).KnowsBurningHands = true;
            ctx.SetCurrentActor(actor, actionsRemaining: 3, turnState: TurnState.EnemyTurn);

            var damageTargets = new List<EntityHandle>();
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            try
            {
                bool executed = ctx.Executor.TryConfirmBurningHands(
                    new Vector3Int(0, 0, 1),
                    rng: new FixedRng(d20Rolls: new[] { 5, 5 }, dieRolls: new[] { 3, 2 }));

                Assert.IsTrue(executed);
                CollectionAssert.AreEquivalent(new[] { targetA, targetB }, damageTargets);
                Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);
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

        [Test]
        public void TryConfirmHeal_CurrentEnemyTurnActor_Succeeds()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("EnemyCleric", Team.Enemy, new Vector3Int(0, 0, 0), hp: 14, intelligence: 18);
            var ally = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(3, 0, 0), hp: 6);
            ctx.Registry.Get(actor).KnowsHeal = true;
            ctx.Registry.Get(ally).CurrentHP = 2;
            ctx.SetCurrentActor(actor, actionsRemaining: 3, turnState: TurnState.EnemyTurn);

            bool executed = ctx.Executor.TryConfirmHeal(
                ally,
                actionCount: 2,
                rng: new FixedRng(dieRolls: new[] { 4 }));

            Assert.IsTrue(executed);
            Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);
            Assert.AreEqual(6, ctx.Registry.Get(ally).CurrentHP);
        }

        [Test]
        public void TryConfirmHarm_CurrentEnemyTurnActor_Succeeds()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("EnemyNecromancer", Team.Enemy, new Vector3Int(0, 0, 0), hp: 14, intelligence: 18);
            var undead = ctx.RegisterEntity("Skeleton", Team.Enemy, new Vector3Int(3, 0, 0), hp: 12);
            ctx.Registry.Get(actor).KnowsHarm = true;
            ctx.Registry.Get(undead).VitalityAffinity = VitalityAffinity.Undead;
            ctx.Registry.Get(undead).CurrentHP = 2;
            ctx.SetCurrentActor(actor, actionsRemaining: 3, turnState: TurnState.EnemyTurn);

            bool executed = ctx.Executor.TryConfirmHarm(
                undead,
                actionCount: 2,
                rng: new FixedRng(dieRolls: new[] { 4 }));

            Assert.IsTrue(executed);
            Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);
            Assert.AreEqual(12, ctx.Registry.Get(undead).CurrentHP);
        }

        [Test]
        public void TryExecuteRaiseShield_CurrentEnemyTurnActor_Succeeds()
        {
            using var ctx = new SpellExecutorContext();
            var shieldDef = ScriptableObject.CreateInstance<ShieldDefinition>();

            try
            {
                shieldDef.acBonus = 2;
                shieldDef.hardness = 3;
                shieldDef.maxHP = 12;

                var actor = ctx.RegisterEntity("EnemyGuard", Team.Enemy, new Vector3Int(0, 0, 0), hp: 16);
                ctx.Registry.Get(actor).EquippedShield = ShieldInstance.CreateEquipped(shieldDef);
                ctx.SetCurrentActor(actor, actionsRemaining: 2, turnState: TurnState.EnemyTurn);

                bool executed = ctx.Executor.TryExecuteRaiseShield();

                Assert.IsTrue(executed);
                Assert.IsTrue(ctx.Registry.Get(actor).HasRaisedPhysicalShield);
                Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        [Test]
        public void TryExecuteCastShieldSpell_CurrentEnemyTurnActor_Succeeds()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("EnemyMage", Team.Enemy, new Vector3Int(0, 0, 0), hp: 12, intelligence: 18);
            ctx.Registry.Get(actor).KnowsStandardShieldCantrip = true;
            ctx.SetCurrentActor(actor, actionsRemaining: 2, turnState: TurnState.EnemyTurn);

            bool executed = ctx.Executor.TryExecuteCastShieldSpell(RaiseShieldSpellMode.Standard);

            Assert.IsTrue(executed);
            Assert.IsTrue(ctx.Registry.Get(actor).StandardShieldRaised);
            Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);
        }

        [Test]
        public void TryExecuteDemoralize_CurrentEnemyTurnActor_Succeeds()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity(
                "EnemySkirmisher",
                Team.Enemy,
                new Vector3Int(0, 0, 0),
                hp: 12,
                charisma: 12,
                intimidationProf: ProficiencyRank.Trained);
            var target = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(2, 0, 0), hp: 20);
            var actorData = ctx.Registry.Get(actor);
            actorData.Level = 20;
            actorData.Charisma = 24;
            actorData.IntimidationProf = ProficiencyRank.Legendary;
            var targetData = ctx.Registry.Get(target);
            targetData.Level = 1;
            targetData.Wisdom = 8;
            targetData.WillProf = ProficiencyRank.Untrained;
            ctx.SetCurrentActor(actor, actionsRemaining: 3, turnState: TurnState.EnemyTurn);

            bool executed = ctx.Executor.TryExecuteDemoralize(target);

            Assert.IsTrue(executed);
            Assert.AreEqual(2, ctx.Registry.Get(actor).ActionsRemaining);
            Assert.IsTrue(ctx.Registry.Get(target).HasCondition(ConditionType.Frightened));
        }

        [Test]
        public void TryExecuteTrip_CurrentEnemyTurnActor_SpendsActionAndIncrementsMap()
        {
            using var ctx = new SpellExecutorContext();
            var tripWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();

            try
            {
                tripWeapon.category = WeaponCategory.Martial;
                tripWeapon.reachFeet = 5;
                tripWeapon.isRanged = false;
                tripWeapon.traits = WeaponTraitFlags.Trip;

                var actor = ctx.RegisterEntity("EnemyBruiser", Team.Enemy, new Vector3Int(0, 0, 0), hp: 14);
                var target = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(1, 0, 0), hp: 20);
                var actorData = ctx.Registry.Get(actor);
                actorData.Strength = 16;
                actorData.AthleticsProf = ProficiencyRank.Trained;
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = tripWeapon,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };
                ctx.SetCurrentActor(actor, actionsRemaining: 3, turnState: TurnState.EnemyTurn);

                bool executed = ctx.Executor.TryExecuteTrip(target);

                Assert.IsTrue(executed);
                Assert.AreEqual(2, ctx.Registry.Get(actor).ActionsRemaining);
                Assert.AreEqual(1, ctx.Registry.Get(actor).MAPCount);
            }
            finally
            {
                Object.DestroyImmediate(tripWeapon);
            }
        }

        [Test]
        public void TryExecuteGrapple_CurrentEnemyTurnActor_SpendsActionAndIncrementsMap()
        {
            using var ctx = new SpellExecutorContext();
            var grappleWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();

            try
            {
                grappleWeapon.category = WeaponCategory.Martial;
                grappleWeapon.reachFeet = 5;
                grappleWeapon.isRanged = false;
                grappleWeapon.traits = WeaponTraitFlags.Grapple;

                var actor = ctx.RegisterEntity("EnemyGrabber", Team.Enemy, new Vector3Int(0, 0, 0), hp: 14);
                var target = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(1, 0, 0), hp: 20);
                var actorData = ctx.Registry.Get(actor);
                actorData.Strength = 16;
                actorData.AthleticsProf = ProficiencyRank.Trained;
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = grappleWeapon,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };
                ctx.SetCurrentActor(actor, actionsRemaining: 3, turnState: TurnState.EnemyTurn);

                bool executed = ctx.Executor.TryExecuteGrapple(target);

                Assert.IsTrue(executed);
                Assert.AreEqual(2, ctx.Registry.Get(actor).ActionsRemaining);
                Assert.AreEqual(1, ctx.Registry.Get(actor).MAPCount);
            }
            finally
            {
                Object.DestroyImmediate(grappleWeapon);
            }
        }

        [Test]
        public void TryExecuteShove_CurrentEnemyTurnActor_PushesTargetAndSpendsAction()
        {
            using var ctx = new SpellExecutorContext();
            var shoveWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();

            try
            {
                shoveWeapon.category = WeaponCategory.Martial;
                shoveWeapon.reachFeet = 10;
                shoveWeapon.isRanged = false;
                shoveWeapon.traits = WeaponTraitFlags.Shove;

                var actor = ctx.RegisterEntity("EnemyPikeman", Team.Enemy, new Vector3Int(0, 0, 0), hp: 14);
                var target = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(1, 0, 0), hp: 20);
                var actorData = ctx.Registry.Get(actor);
                actorData.Level = 20;
                actorData.Strength = 24;
                actorData.AthleticsProf = ProficiencyRank.Legendary;
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = shoveWeapon,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };
                var targetData = ctx.Registry.Get(target);
                targetData.Level = 1;
                targetData.Constitution = 8;
                targetData.FortitudeProf = ProficiencyRank.Untrained;
                ctx.SetCurrentActor(actor, actionsRemaining: 3, turnState: TurnState.EnemyTurn);

                bool executed = ctx.Executor.TryExecuteShove(target);

                Assert.IsTrue(executed);
                Assert.AreEqual(2, ctx.Registry.Get(actor).ActionsRemaining);
                Assert.AreEqual(1, ctx.Registry.Get(actor).MAPCount);
                Assert.Greater(
                    GridDistancePF2e.DistanceFeetXZ(ctx.Registry.Get(actor).GridPosition, ctx.Registry.Get(target).GridPosition),
                    5,
                    "Shove should move the target away from the actor.");
            }
            finally
            {
                Object.DestroyImmediate(shoveWeapon);
            }
        }

        [Test]
        public void TryExecuteReposition_CurrentEnemyTurnActor_MovesTargetAndSpendsAction()
        {
            using var ctx = new SpellExecutorContext();
            var repositionWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();

            try
            {
                repositionWeapon.category = WeaponCategory.Martial;
                repositionWeapon.reachFeet = 10;
                repositionWeapon.isRanged = false;
                repositionWeapon.traits = WeaponTraitFlags.Reposition;

                var actor = ctx.RegisterEntity("EnemyController", Team.Enemy, new Vector3Int(0, 0, 0), hp: 14);
                var target = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(1, 0, 0), hp: 20);
                var actorData = ctx.Registry.Get(actor);
                actorData.Level = 20;
                actorData.Strength = 24;
                actorData.AthleticsProf = ProficiencyRank.Legendary;
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = repositionWeapon,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };
                var targetData = ctx.Registry.Get(target);
                targetData.Level = 1;
                targetData.Constitution = 8;
                targetData.FortitudeProf = ProficiencyRank.Untrained;
                ctx.SetCurrentActor(actor, actionsRemaining: 3, turnState: TurnState.EnemyTurn);

                RepositionTargetSelectionResult started = ctx.Executor.TryBeginRepositionTargetSelection(target);
                var destinations = new List<Vector3Int>();

                Assert.AreEqual(RepositionTargetSelectionResult.EnterCellSelection, started);
                Assert.IsTrue(ctx.Executor.TryGetPendingRepositionDestinations(destinations));
                Assert.IsNotEmpty(destinations);

                bool executed = ctx.Executor.TryConfirmRepositionDestination(destinations[0]);

                Assert.IsTrue(executed);
                Assert.AreEqual(2, ctx.Registry.Get(actor).ActionsRemaining);
                Assert.AreEqual(1, ctx.Registry.Get(actor).MAPCount);
                Assert.AreEqual(destinations[0], ctx.Registry.Get(target).GridPosition);
            }
            finally
            {
                Object.DestroyImmediate(repositionWeapon);
            }
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
        public void TryConfirmHeal_LivingTarget_RestoresHitPointsAndSpendsSelectedActions()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), hp: 14, intelligence: 18);
            var ally = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(3, 0, 0), hp: 20);
            ctx.Registry.Get(actor).KnowsHeal = true;
            ctx.Registry.Get(ally).CurrentHP = 6;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            HealingAppliedEvent lastHealing = default;
            SpellResolvedEvent lastSpell = default;
            int healingCount = 0;
            ctx.EventBus.OnHealingAppliedTyped += HandleHealing;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmHeal(
                    ally,
                    actionCount: 2,
                    rng: new FixedRng(dieRolls: new[] { 4 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(1, healingCount);
                Assert.AreEqual(12, lastHealing.amount);
                Assert.AreEqual(18, ctx.Registry.Get(ally).CurrentHP);
                Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);
                Assert.AreEqual(SpellId.Heal, lastSpell.spellId);
                Assert.AreEqual(2, lastSpell.actionCost);
                Assert.AreEqual(12, lastSpell.rolledDamage);
                Assert.AreEqual(12, lastSpell.targetOutcomes[0].appliedHealing);
                Assert.AreEqual(0, lastSpell.targetOutcomes[0].appliedDamage);
            }
            finally
            {
                ctx.EventBus.OnHealingAppliedTyped -= HandleHealing;
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleHealing(in HealingAppliedEvent e)
            {
                healingCount++;
                lastHealing = e;
            }

            void HandleSpell(in SpellResolvedEvent e)
            {
                lastSpell = e;
            }
        }

        [Test]
        public void TryConfirmHeal_UndeadTarget_DealsVitalityDamageWithBasicFortitude()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), hp: 14, intelligence: 18);
            var undead = ctx.RegisterEntity("Skeleton", Team.Enemy, new Vector3Int(2, 0, 0), hp: 12);
            ctx.Registry.Get(actor).KnowsHeal = true;
            ctx.Registry.Get(undead).VitalityAffinity = VitalityAffinity.Undead;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            DamageAppliedEvent lastDamage = default;
            SpellResolvedEvent lastSpell = default;
            int damageCount = 0;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmHeal(
                    undead,
                    actionCount: 2,
                    rng: new FixedRng(d20Rolls: new[] { 5 }, dieRolls: new[] { 6 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(1, damageCount);
                Assert.AreEqual(6, lastDamage.amount);
                Assert.AreEqual(DamageType.Vitality, lastDamage.damageType);
                Assert.AreEqual(6, ctx.Registry.Get(undead).CurrentHP);
                Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);
                Assert.AreEqual(SpellId.Heal, lastSpell.spellId);
                Assert.AreEqual(DegreeOfSuccess.Failure, lastSpell.targetOutcomes[0].saveResult.Value.degree);
                Assert.AreEqual(6, lastSpell.targetOutcomes[0].appliedDamage);
                Assert.AreEqual(0, lastSpell.targetOutcomes[0].appliedHealing);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damageCount++;
                lastDamage = e;
            }

            void HandleSpell(in SpellResolvedEvent e)
            {
                lastSpell = e;
            }
        }

        [Test]
        public void TryConfirmHealArea_MixedTargets_HealsLivingDamagesUndeadAndSpendsThreeActions()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), hp: 14, intelligence: 18);
            var ally = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(3, 0, 0), hp: 20);
            var undead = ctx.RegisterEntity("Skeleton", Team.Enemy, new Vector3Int(4, 0, 0), hp: 12);
            ctx.Registry.Get(actor).KnowsHeal = true;
            ctx.Registry.Get(actor).CurrentHP = 8;
            ctx.Registry.Get(ally).CurrentHP = 7;
            ctx.Registry.Get(undead).VitalityAffinity = VitalityAffinity.Undead;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            var healingEvents = new List<HealingAppliedEvent>();
            var damageEvents = new List<DamageAppliedEvent>();
            SpellResolvedEvent lastSpell = default;
            ctx.EventBus.OnHealingAppliedTyped += HandleHealing;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmHealArea(
                    ctx.Registry.Get(actor).GridPosition,
                    rng: new FixedRng(d20Rolls: new[] { 5 }, dieRolls: new[] { 5 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(2, healingEvents.Count);
                Assert.AreEqual(13, ctx.Registry.Get(actor).CurrentHP);
                Assert.AreEqual(12, ctx.Registry.Get(ally).CurrentHP);
                Assert.AreEqual(7, ctx.Registry.Get(undead).CurrentHP);
                Assert.AreEqual(0, ctx.Registry.Get(actor).ActionsRemaining);

                Assert.AreEqual(1, damageEvents.Count);
                Assert.AreEqual(DamageType.Vitality, damageEvents[0].damageType);
                Assert.AreEqual(5, damageEvents[0].amount);

                Assert.AreEqual(SpellId.Heal, lastSpell.spellId);
                Assert.AreEqual(3, lastSpell.actionCost);
                Assert.AreEqual(5, lastSpell.rolledDamage);
                Assert.AreEqual(3, lastSpell.targetOutcomes.Length);
                Assert.AreEqual(2, System.Array.FindAll(lastSpell.targetOutcomes, outcome => outcome.appliedHealing > 0).Length);
                Assert.AreEqual(1, System.Array.FindAll(lastSpell.targetOutcomes, outcome => outcome.appliedDamage > 0).Length);
            }
            finally
            {
                ctx.EventBus.OnHealingAppliedTyped -= HandleHealing;
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleHealing(in HealingAppliedEvent e)
            {
                healingEvents.Add(e);
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
        public void TryConfirmHarm_LivingTarget_DealsVoidDamageWithBasicFortitude()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), intelligence: 18);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(6, 0, 0), hp: 18);
            ctx.Registry.Get(actor).KnowsHarm = true;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            DamageAppliedEvent lastDamage = default;
            SpellResolvedEvent lastSpell = default;
            int damageCount = 0;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmHarm(
                    target,
                    actionCount: 2,
                    rng: new FixedRng(d20Rolls: new[] { 5 }, dieRolls: new[] { 4 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(1, damageCount);
                Assert.AreEqual(DamageType.Void, lastDamage.damageType);
                Assert.AreEqual(12, lastDamage.amount);
                Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);

                Assert.AreEqual(SpellId.Harm, lastSpell.spellId);
                Assert.AreEqual(DegreeOfSuccess.Failure, lastSpell.targetOutcomes[0].saveResult.Value.degree);
                Assert.AreEqual(12, lastSpell.targetOutcomes[0].appliedDamage);
                Assert.AreEqual(0, lastSpell.targetOutcomes[0].appliedHealing);
            }
            finally
            {
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleDamage(in DamageAppliedEvent e)
            {
                damageCount++;
                lastDamage = e;
            }

            void HandleSpell(in SpellResolvedEvent e)
            {
                lastSpell = e;
            }
        }

        [Test]
        public void TryConfirmHarm_UndeadTarget_RestoresHitPointsAndSpendsSelectedActions()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), intelligence: 18);
            var undead = ctx.RegisterEntity("Skeleton", Team.Enemy, new Vector3Int(1, 0, 0), hp: 20);
            ctx.Registry.Get(actor).KnowsHarm = true;
            ctx.Registry.Get(undead).VitalityAffinity = VitalityAffinity.Undead;
            ctx.Registry.Get(undead).CurrentHP = 3;
            ctx.SetCurrentActor(actor, actionsRemaining: 2);

            var healingEvents = new List<HealingAppliedEvent>();
            SpellResolvedEvent lastSpell = default;
            ctx.EventBus.OnHealingAppliedTyped += HandleHealing;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmHarm(
                    undead,
                    actionCount: 1,
                    rng: new FixedRng(dieRolls: new[] { 6 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(1, healingEvents.Count);
                Assert.AreEqual(9, ctx.Registry.Get(undead).CurrentHP);
                Assert.AreEqual(1, ctx.Registry.Get(actor).ActionsRemaining);
                Assert.AreEqual(SpellId.Harm, lastSpell.spellId);
                Assert.AreEqual(6, lastSpell.targetOutcomes[0].appliedHealing);
                Assert.AreEqual(0, lastSpell.targetOutcomes[0].appliedDamage);
            }
            finally
            {
                ctx.EventBus.OnHealingAppliedTyped -= HandleHealing;
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleHealing(in HealingAppliedEvent e)
            {
                healingEvents.Add(e);
            }

            void HandleSpell(in SpellResolvedEvent e)
            {
                lastSpell = e;
            }
        }

        [Test]
        public void TryConfirmHarmArea_MixedTargets_DamagesLivingHealsUndeadAndSpendsThreeActions()
        {
            using var ctx = new SpellExecutorContext();

            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), hp: 14, intelligence: 18);
            var ally = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(3, 0, 0), hp: 20);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(4, 0, 0), hp: 14);
            var undead = ctx.RegisterEntity("Skeleton", Team.Enemy, new Vector3Int(5, 0, 0), hp: 12);
            ctx.Registry.Get(actor).KnowsHarm = true;
            ctx.Registry.Get(undead).VitalityAffinity = VitalityAffinity.Undead;
            ctx.Registry.Get(undead).CurrentHP = 4;
            ctx.SetCurrentActor(actor, actionsRemaining: 3);

            var healingEvents = new List<HealingAppliedEvent>();
            var damageEvents = new List<DamageAppliedEvent>();
            SpellResolvedEvent lastSpell = default;
            ctx.EventBus.OnHealingAppliedTyped += HandleHealing;
            ctx.EventBus.OnDamageAppliedTyped += HandleDamage;
            ctx.EventBus.OnSpellResolvedTyped += HandleSpell;
            try
            {
                bool executed = ctx.Executor.TryConfirmHarmArea(
                    ctx.Registry.Get(actor).GridPosition,
                    rng: new FixedRng(d20Rolls: new[] { 5, 10, 5 }, dieRolls: new[] { 5 }));

                Assert.IsTrue(executed);
                Assert.AreEqual(1, healingEvents.Count);
                Assert.AreEqual(3, damageEvents.Count);
                Assert.AreEqual(9, ctx.Registry.Get(undead).CurrentHP);
                Assert.AreEqual(15, ctx.Registry.Get(ally).CurrentHP);
                Assert.AreEqual(9, ctx.Registry.Get(enemy).CurrentHP);
                Assert.AreEqual(0, ctx.Registry.Get(actor).ActionsRemaining);

                Assert.AreEqual(SpellId.Harm, lastSpell.spellId);
                Assert.AreEqual(3, lastSpell.actionCost);
                Assert.AreEqual(5, lastSpell.rolledDamage);
                Assert.AreEqual(4, lastSpell.targetOutcomes.Length);
                Assert.AreEqual(1, System.Array.FindAll(lastSpell.targetOutcomes, outcome => outcome.appliedHealing > 0).Length);
                Assert.AreEqual(3, System.Array.FindAll(lastSpell.targetOutcomes, outcome => outcome.appliedDamage > 0).Length);
            }
            finally
            {
                ctx.EventBus.OnHealingAppliedTyped -= HandleHealing;
                ctx.EventBus.OnDamageAppliedTyped -= HandleDamage;
                ctx.EventBus.OnSpellResolvedTyped -= HandleSpell;
            }

            void HandleHealing(in HealingAppliedEvent e)
            {
                healingEvents.Add(e);
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
            public GridManager GridManager { get; }

            public SpellExecutorContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("PlayerActionExecutorSpellTests_Root");

                var eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                var gridManagerGo = new GameObject("GridManager");
                gridManagerGo.transform.SetParent(root.transform);
                GridManager = gridManagerGo.AddComponent<GridManager>();
                SetAutoPropertyBackingField(GridManager, "Data", CreateOpenGrid());

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                var occupancy = new OccupancyMap(Registry);
                var pathfinding = new GridPathfinding();
                SetPrivateField(EntityManager, "gridManager", GridManager);
                SetAutoPropertyBackingField(EntityManager, "Occupancy", occupancy);
                SetAutoPropertyBackingField(EntityManager, "Pathfinding", pathfinding);
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

                var raiseShieldAction = executorGo.AddComponent<RaiseShieldAction>();
                SetPrivateField(raiseShieldAction, "entityManager", EntityManager);
                SetPrivateField(raiseShieldAction, "eventBus", EventBus);

                var standardShieldAction = executorGo.AddComponent<StandardShieldAction>();
                SetPrivateField(standardShieldAction, "entityManager", EntityManager);
                SetPrivateField(standardShieldAction, "eventBus", EventBus);

                var glassShieldAction = executorGo.AddComponent<GlassShieldAction>();
                SetPrivateField(glassShieldAction, "entityManager", EntityManager);
                SetPrivateField(glassShieldAction, "eventBus", EventBus);

                var demoralizeAction = executorGo.AddComponent<DemoralizeAction>();
                SetPrivateField(demoralizeAction, "entityManager", EntityManager);
                SetPrivateField(demoralizeAction, "eventBus", EventBus);

                var tripAction = executorGo.AddComponent<TripAction>();
                SetPrivateField(tripAction, "entityManager", EntityManager);
                SetPrivateField(tripAction, "eventBus", EventBus);
                SetPrivateField(tripAction, "turnManager", TurnManager);

                var grappleLifecycle = executorGo.AddComponent<GrappleLifecycleController>();
                SetPrivateField(grappleLifecycle, "entityManager", EntityManager);
                SetPrivateField(grappleLifecycle, "eventBus", EventBus);

                var grappleAction = executorGo.AddComponent<GrappleAction>();
                SetPrivateField(grappleAction, "entityManager", EntityManager);
                SetPrivateField(grappleAction, "eventBus", EventBus);
                SetPrivateField(grappleAction, "grappleLifecycle", grappleLifecycle);

                var shoveAction = executorGo.AddComponent<ShoveAction>();
                SetPrivateField(shoveAction, "entityManager", EntityManager);
                SetPrivateField(shoveAction, "eventBus", EventBus);

                var repositionAction = executorGo.AddComponent<RepositionAction>();
                SetPrivateField(repositionAction, "entityManager", EntityManager);
                SetPrivateField(repositionAction, "gridManager", GridManager);
                SetPrivateField(repositionAction, "eventBus", EventBus);
                SetPrivateField(repositionAction, "grappleLifecycle", grappleLifecycle);

                SetPrivateField(Executor, "turnManager", TurnManager);
                SetPrivateField(Executor, "entityManager", EntityManager);
                SetPrivateField(Executor, "eventBus", EventBus);
                SetPrivateField(Executor, "raiseShieldAction", raiseShieldAction);
                SetPrivateField(Executor, "standardShieldAction", standardShieldAction);
                SetPrivateField(Executor, "glassShieldAction", glassShieldAction);
                SetPrivateField(Executor, "demoralizeAction", demoralizeAction);
                SetPrivateField(Executor, "tripAction", tripAction);
                SetPrivateField(Executor, "grappleAction", grappleAction);
                SetPrivateField(Executor, "shoveAction", shoveAction);
                SetPrivateField(Executor, "repositionAction", repositionAction);
            }

            public EntityHandle RegisterEntity(
                string name,
                Team team,
                Vector3Int gridPosition,
                int hp = 10,
                int intelligence = 10,
                int dexterity = 10,
                int charisma = 10,
                ProficiencyRank intimidationProf = ProficiencyRank.Untrained)
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
                    Charisma = charisma,
                    IntimidationProf = intimidationProf,
                    MaxHP = hp,
                    CurrentHP = hp,
                    GridPosition = gridPosition,
                    ActionsRemaining = 3,
                    ReactionAvailable = true
                });

                registeredHandles.Add(handle);
                Assert.IsTrue(EntityManager.Occupancy.Place(handle, gridPosition, 1), $"Failed to place {name} at {gridPosition}");
                return handle;
            }

            public void SetCurrentActor(EntityHandle actor, int actionsRemaining, TurnState turnState = TurnState.PlayerTurn)
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
                SetPrivateField(TurnManager, "state", turnState);
            }

            public void Dispose()
            {
                if (root != null)
                    Object.DestroyImmediate(root);

                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private static GridData CreateOpenGrid()
        {
            var grid = new GridData(1f, 1f, 128);
            for (int x = -16; x <= 64; x++)
            {
                for (int z = -16; z <= 64; z++)
                    grid.SetCell(new Vector3Int(x, 0, z), CellData.CreateWalkable());
            }

            return grid;
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
