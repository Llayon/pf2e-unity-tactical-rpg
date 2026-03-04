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
    public class TurnManagerReadiedStrikeTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void CycleReadyTriggerMode_CyclesAnyToMovementToAttackToAny()
        {
            using var ctx = new ReadyStrikeContext();

            Assert.AreEqual(ReadyTriggerMode.Any, ctx.TurnManager.CurrentReadyTriggerMode);
            Assert.AreEqual(ReadyTriggerMode.Movement, ctx.TurnManager.CycleReadyTriggerMode());
            Assert.AreEqual(ReadyTriggerMode.Attack, ctx.TurnManager.CycleReadyTriggerMode());
            Assert.AreEqual(ReadyTriggerMode.Any, ctx.TurnManager.CycleReadyTriggerMode());
        }

        [Test]
        public void SetReadyTriggerMode_Changed_PublishesTypedEvent()
        {
            using var ctx = new ReadyStrikeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0));
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);

            SetPrivateField(
                ctx.TurnManager,
                "initiativeOrder",
                new List<InitiativeEntry>
                {
                    new InitiativeEntry
                    {
                        Handle = actor,
                        Roll = new CheckRoll(10, actorData.PerceptionModifier, CheckSource.Perception()),
                        IsPlayer = true
                    }
                });
            SetPrivateField(ctx.TurnManager, "currentIndex", 0);

            bool raised = false;
            EntityHandle raisedActor = default;
            ReadyTriggerMode raisedMode = ReadyTriggerMode.Any;
            ctx.EventBus.OnReadyTriggerModeChangedTyped += HandleModeChanged;

            try
            {
                bool changed = ctx.TurnManager.SetReadyTriggerMode(ReadyTriggerMode.Attack);

                Assert.IsTrue(changed);
                Assert.IsTrue(raised);
                Assert.AreEqual(actor, raisedActor);
                Assert.AreEqual(ReadyTriggerMode.Attack, raisedMode);
            }
            finally
            {
                ctx.EventBus.OnReadyTriggerModeChangedTyped -= HandleModeChanged;
            }

            void HandleModeChanged(in ReadyTriggerModeChangedEvent e)
            {
                raised = true;
                raisedActor = e.actor;
                raisedMode = e.mode;
            }
        }

        [Test]
        public void SetReadyTriggerMode_SameMode_ReturnsFalseAndDoesNotPublishEvent()
        {
            using var ctx = new ReadyStrikeContext();

            bool raised = false;
            ctx.EventBus.OnReadyTriggerModeChangedTyped += HandleModeChanged;

            try
            {
                bool changed = ctx.TurnManager.SetReadyTriggerMode(ReadyTriggerMode.Any);

                Assert.IsFalse(changed);
                Assert.IsFalse(raised);
            }
            finally
            {
                ctx.EventBus.OnReadyTriggerModeChangedTyped -= HandleModeChanged;
            }

            void HandleModeChanged(in ReadyTriggerModeChangedEvent e)
            {
                _ = e;
                raised = true;
            }
        }

        [Test]
        public void EntityMoved_Forced_DoesNotTriggerReadiedStrike()
        {
            using var ctx = new ReadyStrikeContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0));
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(2, 0, 0));
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);
            actorData.ReactionAvailable = true;

            Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(actor, preparedRound: 1));
            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor));

            var targetData = ctx.Registry.Get(target);
            Assert.IsNotNull(targetData);
            targetData.GridPosition = new Vector3Int(1, 0, 0); // enters reach, but forced movement must not trigger
            ctx.EventBus.PublishEntityMoved(target, new Vector3Int(2, 0, 0), targetData.GridPosition, forced: true);

            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor), "Forced movement should not consume readied strike.");
            Assert.IsTrue(actorData.ReactionAvailable, "Forced movement should not consume reaction.");
        }

        [Test]
        public void EntityMoved_Normal_ConsumesReadiedStrikeAndReaction()
        {
            using var ctx = new ReadyStrikeContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(2, 0, 0), strength: 10);
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);
            actorData.ReactionAvailable = true;

            Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(actor, preparedRound: 1));
            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor));

            var targetData = ctx.Registry.Get(target);
            Assert.IsNotNull(targetData);
            targetData.GridPosition = new Vector3Int(1, 0, 0); // enters melee reach from outside
            ctx.EventBus.PublishEntityMoved(target, new Vector3Int(2, 0, 0), targetData.GridPosition, forced: false);

            Assert.IsFalse(ctx.TurnManager.HasReadiedStrike(actor), "Normal movement trigger should consume readied strike.");
            Assert.IsFalse(actorData.ReactionAvailable, "Triggered readied strike should consume reaction.");
        }

        [Test]
        public void EntityMoved_Normal_AttackOnlyMode_DoesNotConsumeReadiedStrike()
        {
            using var ctx = new ReadyStrikeContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(2, 0, 0), strength: 10);
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);
            actorData.ReactionAvailable = true;

            Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(actor, preparedRound: 1, triggerMode: ReadyTriggerMode.Attack));
            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor));

            var targetData = ctx.Registry.Get(target);
            Assert.IsNotNull(targetData);
            targetData.GridPosition = new Vector3Int(1, 0, 0);
            ctx.EventBus.PublishEntityMoved(target, new Vector3Int(2, 0, 0), targetData.GridPosition, forced: false);

            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor), "Attack-only ready must not trigger on movement.");
            Assert.IsTrue(actorData.ReactionAvailable, "Reaction should remain available when trigger mode filters movement.");
        }

        [Test]
        public void EntityMoved_Normal_WithinReachToWithinReach_ConsumesReadiedStrikeAndReaction()
        {
            using var ctx = new ReadyStrikeContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);
            actorData.ReactionAvailable = true;

            Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(actor, preparedRound: 1));
            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor));

            var targetData = ctx.Registry.Get(target);
            Assert.IsNotNull(targetData);
            targetData.GridPosition = new Vector3Int(1, 0, 1); // still in melee reach
            ctx.EventBus.PublishEntityMoved(target, new Vector3Int(1, 0, 0), targetData.GridPosition, forced: false);

            Assert.IsFalse(ctx.TurnManager.HasReadiedStrike(actor), "Movement started within reach should trigger melee ready.");
            Assert.IsFalse(actorData.ReactionAvailable, "Triggered melee ready should consume reaction.");
        }

        [Test]
        public void EntityMoved_TwoPreparedAllies_FirstKillsTriggerTarget_SecondKeepsReactionUntilNextEnemy()
        {
            using var ctx = new ReadyStrikeContext();
            var meleeDef = ScriptableObject.CreateInstance<WeaponDefinition>();
            meleeDef.itemName = "ReadyTestMelee";
            meleeDef.isRanged = false;
            meleeDef.reachFeet = 5;
            meleeDef.diceCount = 1;
            meleeDef.dieSides = 6;
            meleeDef.damageType = DamageType.Slashing;
            meleeDef.category = WeaponCategory.Simple;
            meleeDef.group = WeaponGroup.Club;
            meleeDef.hands = WeaponHands.One;

            try
            {
                var fighter = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 5000);
                var wizard = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 1), strength: 5000);
                var goblinA = ctx.RegisterEntity("Goblin_A", Team.Enemy, new Vector3Int(2, 0, 0), strength: 10);
                var goblinB = ctx.RegisterEntity("Goblin_B", Team.Enemy, new Vector3Int(2, 0, 1), strength: 10);

                var fighterData = ctx.Registry.Get(fighter);
                var wizardData = ctx.Registry.Get(wizard);
                var goblinAData = ctx.Registry.Get(goblinA);
                var goblinBData = ctx.Registry.Get(goblinB);
                Assert.IsNotNull(fighterData);
                Assert.IsNotNull(wizardData);
                Assert.IsNotNull(goblinAData);
                Assert.IsNotNull(goblinBData);

                fighterData.EquippedWeapon = new WeaponInstance
                {
                    def = meleeDef,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };
                wizardData.EquippedWeapon = new WeaponInstance
                {
                    def = meleeDef,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };
                fighterData.ReactionAvailable = true;
                wizardData.ReactionAvailable = true;
                goblinAData.MaxHP = 1;
                goblinAData.CurrentHP = 1; // Fighter must reliably kill first trigger target.

                SetPrivateField(
                    ctx.TurnManager,
                    "initiativeOrder",
                    new List<InitiativeEntry>
                    {
                        new InitiativeEntry
                        {
                            Handle = fighter,
                            Roll = new CheckRoll(20, fighterData.PerceptionModifier, CheckSource.Perception()),
                            IsPlayer = true
                        },
                        new InitiativeEntry
                        {
                            Handle = wizard,
                            Roll = new CheckRoll(19, wizardData.PerceptionModifier, CheckSource.Perception()),
                            IsPlayer = true
                        },
                        new InitiativeEntry
                        {
                            Handle = goblinA,
                            Roll = new CheckRoll(10, goblinAData.PerceptionModifier, CheckSource.Perception()),
                            IsPlayer = false
                        },
                        new InitiativeEntry
                        {
                            Handle = goblinB,
                            Roll = new CheckRoll(9, goblinBData.PerceptionModifier, CheckSource.Perception()),
                            IsPlayer = false
                        }
                    });

                Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(fighter, preparedRound: 1));
                Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(wizard, preparedRound: 1));
                Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(fighter));
                Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(wizard));

                // First movement trigger: first resolver kills goblinA; second resolver must keep ready+reaction.
                goblinAData.GridPosition = new Vector3Int(1, 0, 0);
                ctx.EventBus.PublishEntityMoved(goblinA, new Vector3Int(2, 0, 0), goblinAData.GridPosition, forced: false);

                Assert.LessOrEqual(goblinAData.CurrentHP, 0, "First trigger target should be dead after first readied strike resolves.");

                bool fighterPreparedAfterFirst = ctx.TurnManager.HasReadiedStrike(fighter);
                bool wizardPreparedAfterFirst = ctx.TurnManager.HasReadiedStrike(wizard);
                Assert.AreNotEqual(
                    fighterPreparedAfterFirst,
                    wizardPreparedAfterFirst,
                    "Exactly one ally must keep ready after first target dies mid-trigger chain.");

                EntityHandle remainingActor = fighterPreparedAfterFirst ? fighter : wizard;
                EntityHandle consumedActor = fighterPreparedAfterFirst ? wizard : fighter;
                var remainingData = fighterPreparedAfterFirst ? fighterData : wizardData;
                var consumedData = fighterPreparedAfterFirst ? wizardData : fighterData;

                Assert.IsTrue(remainingData.ReactionAvailable, "Remaining prepared ally must keep reaction.");
                Assert.IsFalse(consumedData.ReactionAvailable, "Consumed ally must have reaction spent.");

                // Second movement trigger: the remaining prepared ally should react to another enemy entering reach.
                goblinBData.GridPosition = new Vector3Int(1, 0, 1);
                ctx.EventBus.PublishEntityMoved(goblinB, new Vector3Int(2, 0, 1), goblinBData.GridPosition, forced: false);

                Assert.IsFalse(
                    ctx.TurnManager.HasReadiedStrike(remainingActor),
                    "Remaining ally ready should trigger on next valid enemy movement.");
                Assert.IsFalse(remainingData.ReactionAvailable, "Remaining ally reaction should be spent on second trigger.");
                Assert.IsFalse(ctx.TurnManager.HasReadiedStrike(consumedActor), "Consumed ally should not regain ready state.");
            }
            finally
            {
                Object.DestroyImmediate(meleeDef);
            }
        }

        [Test]
        public void EntityMoved_Normal_Ranged_EnteringFirstIncrement_ConsumesReadiedStrikeAndReaction()
        {
            using var ctx = new ReadyStrikeContext();
            var rangedDef = CreateRangedWeaponDefinition(60, 6);

            try
            {
                var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), strength: 10);
                var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(13, 0, 0), strength: 10); // 65 ft
                var actorData = ctx.Registry.Get(actor);
                Assert.IsNotNull(actorData);
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = rangedDef,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };
                actorData.ReactionAvailable = true;

                Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(actor, preparedRound: 1));
                Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor));

                var targetData = ctx.Registry.Get(target);
                Assert.IsNotNull(targetData);
                targetData.GridPosition = new Vector3Int(12, 0, 0); // 60 ft (first increment edge)
                ctx.EventBus.PublishEntityMoved(target, new Vector3Int(13, 0, 0), targetData.GridPosition, forced: false);

                Assert.IsFalse(ctx.TurnManager.HasReadiedStrike(actor), "Entering first increment should trigger ranged ready.");
                Assert.IsFalse(actorData.ReactionAvailable, "Triggered ranged ready should consume reaction.");
            }
            finally
            {
                Object.DestroyImmediate(rangedDef);
            }
        }

        [Test]
        public void EntityMoved_Normal_Ranged_WithinMaxRangeButOutsideFirstIncrement_DoesNotTriggerReadiedStrike()
        {
            using var ctx = new ReadyStrikeContext();
            var rangedDef = CreateRangedWeaponDefinition(60, 6);

            try
            {
                var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), strength: 10);
                var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(20, 0, 0), strength: 10); // 100 ft
                var actorData = ctx.Registry.Get(actor);
                Assert.IsNotNull(actorData);
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = rangedDef,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };
                actorData.ReactionAvailable = true;

                Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(actor, preparedRound: 1));
                Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor));

                var targetData = ctx.Registry.Get(target);
                Assert.IsNotNull(targetData);
                targetData.GridPosition = new Vector3Int(16, 0, 0); // 80 ft (still outside first increment)
                ctx.EventBus.PublishEntityMoved(target, new Vector3Int(20, 0, 0), targetData.GridPosition, forced: false);

                Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor), "Movement outside first increment should not trigger ranged ready.");
                Assert.IsTrue(actorData.ReactionAvailable, "No ranged trigger => reaction remains available.");
            }
            finally
            {
                Object.DestroyImmediate(rangedDef);
            }
        }

        [Test]
        public void EntityMoved_Normal_Ranged_WithinFirstIncrement_ConsumesReadiedStrikeAndReaction()
        {
            using var ctx = new ReadyStrikeContext();
            var rangedDef = CreateRangedWeaponDefinition(60, 6);

            try
            {
                var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), strength: 10);
                var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(12, 0, 0), strength: 10); // 60 ft
                var actorData = ctx.Registry.Get(actor);
                Assert.IsNotNull(actorData);
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = rangedDef,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };
                actorData.ReactionAvailable = true;

                Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(actor, preparedRound: 1));
                Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor));

                var targetData = ctx.Registry.Get(target);
                Assert.IsNotNull(targetData);
                targetData.GridPosition = new Vector3Int(12, 0, 1); // still 60 ft, but movement started inside first increment
                ctx.EventBus.PublishEntityMoved(target, new Vector3Int(12, 0, 0), targetData.GridPosition, forced: false);

                Assert.IsFalse(ctx.TurnManager.HasReadiedStrike(actor), "Movement started within first increment should trigger ranged ready.");
                Assert.IsFalse(actorData.ReactionAvailable, "Triggered ranged ready should consume reaction.");
            }
            finally
            {
                Object.DestroyImmediate(rangedDef);
            }
        }

        [Test]
        public void StrikePreDamage_EnemyAttackingReadiedActorWithinReach_ConsumesReadiedStrikeAndReaction()
        {
            using var ctx = new ReadyStrikeContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);
            actorData.ReactionAvailable = true;

            Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(actor, preparedRound: 1));
            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor));

            ctx.EventBus.PublishStrikePreDamage(
                attacker: enemy,
                target: actor,
                naturalRoll: 15,
                total: 20,
                dc: 18,
                degree: DegreeOfSuccess.Success,
                damageRolled: 5,
                damageType: DamageType.Slashing);

            Assert.IsFalse(ctx.TurnManager.HasReadiedStrike(actor), "Enemy attack start should trigger readied strike.");
            Assert.IsFalse(actorData.ReactionAvailable, "Triggered readied strike should consume reaction.");
        }

        [Test]
        public void StrikePreDamage_MovementOnlyMode_DoesNotConsumeReadiedStrike()
        {
            using var ctx = new ReadyStrikeContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);
            actorData.ReactionAvailable = true;

            Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(actor, preparedRound: 1, triggerMode: ReadyTriggerMode.Movement));
            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor));

            ctx.EventBus.PublishStrikePreDamage(
                attacker: enemy,
                target: actor,
                naturalRoll: 15,
                total: 20,
                dc: 18,
                degree: DegreeOfSuccess.Success,
                damageRolled: 5,
                damageType: DamageType.Slashing);

            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor), "Movement-only ready must not trigger on attack start.");
            Assert.IsTrue(actorData.ReactionAvailable, "Reaction should remain available when trigger mode filters attack.");
        }

        [Test]
        public void StrikePreDamage_EnemyAttackingOtherTargetWithinReach_ConsumesReadiedStrikeAndReaction()
        {
            using var ctx = new ReadyStrikeContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var ally = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(1, 0, 0), strength: 10);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 1), strength: 10);
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);
            actorData.ReactionAvailable = true;

            Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(actor, preparedRound: 1));
            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor));

            ctx.EventBus.PublishStrikePreDamage(
                attacker: enemy,
                target: ally,
                naturalRoll: 15,
                total: 20,
                dc: 18,
                degree: DegreeOfSuccess.Success,
                damageRolled: 5,
                damageType: DamageType.Slashing);

            Assert.IsFalse(ctx.TurnManager.HasReadiedStrike(actor), "Enemy attack start within reach should trigger readied strike even when target is ally.");
            Assert.IsFalse(actorData.ReactionAvailable, "Triggered readied strike should consume reaction.");
        }

        [Test]
        public void StrikePreDamage_EnemyAttackingOtherTargetOutOfReach_DoesNotTriggerReadiedStrike()
        {
            using var ctx = new ReadyStrikeContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var ally = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(1, 0, 0), strength: 10);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(2, 0, 0), strength: 10); // 10 ft from actor
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);
            actorData.ReactionAvailable = true;

            Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(actor, preparedRound: 1));
            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor));

            ctx.EventBus.PublishStrikePreDamage(
                attacker: enemy,
                target: ally,
                naturalRoll: 15,
                total: 20,
                dc: 18,
                degree: DegreeOfSuccess.Success,
                damageRolled: 5,
                damageType: DamageType.Slashing);

            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor), "Enemy attack start out of reach should not trigger readied strike.");
            Assert.IsTrue(actorData.ReactionAvailable, "No trigger => reaction remains available.");
        }

        [Test]
        public void StrikePreDamage_ReadyResolution_DoesNotCascadeIntoCounterReadyInSameTriggerScope()
        {
            using var ctx = new ReadyStrikeContext();

            var fighter = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var wizard = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 1), strength: 10);
            var goblin = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);

            var fighterData = ctx.Registry.Get(fighter);
            var goblinData = ctx.Registry.Get(goblin);
            Assert.IsNotNull(fighterData);
            Assert.IsNotNull(goblinData);

            fighterData.ReactionAvailable = true;
            goblinData.ReactionAvailable = true;

            Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(fighter, preparedRound: 1));
            Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(goblin, preparedRound: 1));
            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(fighter));
            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(goblin));

            // Root trigger: enemy starts attack on fighter's ally.
            ctx.EventBus.PublishStrikePreDamage(
                attacker: goblin,
                target: wizard,
                naturalRoll: 15,
                total: 20,
                dc: 18,
                degree: DegreeOfSuccess.Success,
                damageRolled: 5,
                damageType: DamageType.Slashing);

            // Fighter should react and consume own ready.
            Assert.IsFalse(ctx.TurnManager.HasReadiedStrike(fighter), "Fighter ready should be consumed by root attack-start trigger.");
            Assert.IsFalse(fighterData.ReactionAvailable, "Fighter reaction should be spent by ready strike.");

            // Goblin should NOT counter-react to fighter's ready strike pre-damage in the same root trigger scope.
            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(goblin), "Goblin ready should remain; counter-ready chain in same trigger scope must be suppressed.");
            Assert.IsTrue(goblinData.ReactionAvailable, "Goblin reaction should remain available when counter-ready is suppressed.");
        }

        [Test]
        public void ApplyStartTurnEffects_ExpiresReadiedStrikeForActor()
        {
            using var ctx = new ReadyStrikeContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0));
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0));
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);
            actorData.ReactionAvailable = true;

            Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(actor, preparedRound: 1));
            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor));

            InvokePrivate(
                ctx.TurnManager,
                "ApplyStartTurnEffects",
                new object[] { actor, actorData });

            Assert.IsFalse(ctx.TurnManager.HasReadiedStrike(actor));
        }

        private static WeaponDefinition CreateRangedWeaponDefinition(int incrementFeet, int maxIncrements)
        {
            var def = ScriptableObject.CreateInstance<WeaponDefinition>();
            def.itemName = "TestRangedWeapon";
            def.isRanged = true;
            def.rangeIncrementFeet = incrementFeet;
            def.maxRangeIncrements = maxIncrements;
            def.diceCount = 1;
            def.dieSides = 6;
            def.reachFeet = 5;
            return def;
        }

        private sealed class ReadyStrikeContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;

            public readonly CombatEventBus EventBus;
            public readonly EntityManager EntityManager;
            public readonly TurnManager TurnManager;
            public readonly StrikeAction StrikeAction;
            public readonly EntityRegistry Registry;

            public ReadyStrikeContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("TurnManagerReadiedStrikeTests_Root");

                var eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                var strikeActionGo = new GameObject("StrikeAction");
                strikeActionGo.transform.SetParent(root.transform);
                StrikeAction = strikeActionGo.AddComponent<StrikeAction>();
                SetPrivateField(StrikeAction, "entityManager", EntityManager);
                SetPrivateField(StrikeAction, "eventBus", EventBus);

                var turnManagerGo = new GameObject("TurnManager");
                turnManagerGo.transform.SetParent(root.transform);
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);
                SetPrivateField(TurnManager, "strikeAction", StrikeAction);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);

                var binder = turnManagerGo.AddComponent<ReadyStrikeEventBinder>();
                SetPrivateField(binder, "turnManager", TurnManager);
                SetPrivateField(binder, "eventBus", EventBus);

                InvokePrivate(TurnManager, "OnEnable", System.Array.Empty<object>());
            }

            public EntityHandle RegisterEntity(string name, Team team, Vector3Int position, int strength = 16)
            {
                return Registry.Register(new EntityData
                {
                    Name = name,
                    Team = team,
                    Level = 1,
                    Strength = strength,
                    Dexterity = 14,
                    Constitution = 12,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    MaxHP = 30,
                    CurrentHP = 30,
                    GridPosition = position,
                    ActionsRemaining = 3
                });
            }

            public void Dispose()
            {
                if (root != null)
                    Object.DestroyImmediate(root);
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

        private static void InvokePrivate(object target, string methodName, object[] args)
        {
            var method = target.GetType().GetMethod(methodName, InstanceNonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}");
            method.Invoke(target, args);
        }
    }
}
