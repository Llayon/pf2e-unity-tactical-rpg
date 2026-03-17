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
    public class TurnManagerReactiveStrikeTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void EntityMoved_StartedWithinReach_ConsumesReactiveStrikeReaction()
        {
            using var ctx = new ReactiveStrikeContext();

            var fighter = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            var fighterData = ctx.Registry.Get(fighter);
            var enemyData = ctx.Registry.Get(enemy);

            fighterData.HasReactiveStrike = true;
            fighterData.ReactionAvailable = true;

            enemyData.GridPosition = new Vector3Int(1, 0, 1);
            ctx.EventBus.PublishEntityMoved(enemy, new Vector3Int(1, 0, 0), enemyData.GridPosition, forced: false);

            Assert.IsFalse(fighterData.ReactionAvailable, "Reactive Strike should consume reaction on in-reach movement.");
        }

        [Test]
        public void EntityMoved_EnteringReachFromOutside_DoesNotConsumeReaction()
        {
            using var ctx = new ReactiveStrikeContext();

            var fighter = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(2, 0, 0), strength: 10);
            var fighterData = ctx.Registry.Get(fighter);
            var enemyData = ctx.Registry.Get(enemy);

            fighterData.HasReactiveStrike = true;
            fighterData.ReactionAvailable = true;

            enemyData.GridPosition = new Vector3Int(1, 0, 0);
            ctx.EventBus.PublishEntityMoved(enemy, new Vector3Int(2, 0, 0), enemyData.GridPosition, forced: false);

            Assert.IsTrue(fighterData.ReactionAvailable, "Entering reach should not trigger Reactive Strike.");
        }

        [Test]
        public void EntityMoved_ForcedMovement_DoesNotConsumeReaction()
        {
            using var ctx = new ReactiveStrikeContext();

            var fighter = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            var fighterData = ctx.Registry.Get(fighter);
            var enemyData = ctx.Registry.Get(enemy);

            fighterData.HasReactiveStrike = true;
            fighterData.ReactionAvailable = true;

            enemyData.GridPosition = new Vector3Int(1, 0, 1);
            ctx.EventBus.PublishEntityMoved(enemy, new Vector3Int(1, 0, 0), enemyData.GridPosition, forced: true);

            Assert.IsTrue(fighterData.ReactionAvailable, "Forced movement must not trigger Reactive Strike.");
        }

        [Test]
        public void EntityMoved_StepWithinReach_DoesNotConsumeReaction()
        {
            using var ctx = new ReactiveStrikeContext();

            var fighter = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            var fighterData = ctx.Registry.Get(fighter);
            var enemyData = ctx.Registry.Get(enemy);

            fighterData.HasReactiveStrike = true;
            fighterData.ReactionAvailable = true;

            enemyData.GridPosition = new Vector3Int(1, 0, 1);
            ctx.EventBus.PublishEntityMoved(enemy, new Vector3Int(1, 0, 0), enemyData.GridPosition, MovementTriggerKind.Step);

            Assert.IsTrue(fighterData.ReactionAvailable, "Step must not trigger Reactive Strike.");
        }

        [Test]
        public void StrikePreDamage_RangedAttackWithinReach_ConsumesReaction()
        {
            using var ctx = new ReactiveStrikeContext();
            var rangedDef = CreateRangedWeaponDefinition();

            try
            {
                var fighter = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
                var ally = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(1, 0, 1), strength: 10);
                var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);

                var fighterData = ctx.Registry.Get(fighter);
                var enemyData = ctx.Registry.Get(enemy);
                fighterData.HasReactiveStrike = true;
                fighterData.ReactionAvailable = true;
                enemyData.EquippedWeapon = new WeaponInstance
                {
                    def = rangedDef,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                ctx.EventBus.PublishStrikePreDamage(
                    attacker: enemy,
                    target: ally,
                    naturalRoll: 15,
                    total: 20,
                    dc: 18,
                    degree: DegreeOfSuccess.Success,
                    damageRolled: 5,
                    damageType: DamageType.Piercing);

                Assert.IsFalse(fighterData.ReactionAvailable, "Ranged attack within reach should trigger Reactive Strike.");
            }
            finally
            {
                Object.DestroyImmediate(rangedDef);
            }
        }

        [Test]
        public void StrikePreDamage_MeleeAttack_DoesNotConsumeReaction()
        {
            using var ctx = new ReactiveStrikeContext();

            var fighter = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var ally = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(1, 0, 1), strength: 10);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);

            var fighterData = ctx.Registry.Get(fighter);
            fighterData.HasReactiveStrike = true;
            fighterData.ReactionAvailable = true;

            ctx.EventBus.PublishStrikePreDamage(
                attacker: enemy,
                target: ally,
                naturalRoll: 15,
                total: 20,
                dc: 18,
                degree: DegreeOfSuccess.Success,
                damageRolled: 5,
                damageType: DamageType.Slashing);

            Assert.IsTrue(fighterData.ReactionAvailable, "Melee attack must not trigger Reactive Strike.");
        }

        [Test]
        public void CombatActionStarted_StandWithinReach_ConsumesReaction()
        {
            using var ctx = new ReactiveStrikeContext();

            var fighter = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            var fighterData = ctx.Registry.Get(fighter);
            fighterData.HasReactiveStrike = true;
            fighterData.ReactionAvailable = true;

            ctx.EventBus.PublishCombatActionStarted(
                enemy,
                actionName: "Stand",
                actionKind: CombatActionKind.Stand,
                traits: CombatActionTraitFlags.None,
                actionCost: 1);

            Assert.IsFalse(fighterData.ReactionAvailable, "Standing in reach should trigger Reactive Strike.");
        }

        [Test]
        public void CombatActionStarted_ManipulateSpellWithinReach_ConsumesReaction()
        {
            using var ctx = new ReactiveStrikeContext();

            var fighter = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var enemy = ctx.RegisterEntity("Wizard", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            var fighterData = ctx.Registry.Get(fighter);
            fighterData.HasReactiveStrike = true;
            fighterData.ReactionAvailable = true;

            ctx.EventBus.PublishCombatActionStarted(
                enemy,
                actionName: "Fear",
                actionKind: CombatActionKind.Spell,
                traits: CombatActionTraitFlags.Manipulate,
                actionCost: 2);

            Assert.IsFalse(fighterData.ReactionAvailable, "Manipulate spell in reach should trigger Reactive Strike.");
        }

        [Test]
        public void CombatActionStarted_NonManipulateSpell_DoesNotConsumeReaction()
        {
            using var ctx = new ReactiveStrikeContext();

            var fighter = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var enemy = ctx.RegisterEntity("Cleric", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            var fighterData = ctx.Registry.Get(fighter);
            fighterData.HasReactiveStrike = true;
            fighterData.ReactionAvailable = true;

            ctx.EventBus.PublishCombatActionStarted(
                enemy,
                actionName: "Heal",
                actionKind: CombatActionKind.Spell,
                traits: CombatActionTraitFlags.None,
                actionCost: 1);

            Assert.IsTrue(fighterData.ReactionAvailable, "Non-manipulate spell must not trigger Reactive Strike.");
        }

        private static WeaponDefinition CreateRangedWeaponDefinition()
        {
            var def = ScriptableObject.CreateInstance<WeaponDefinition>();
            def.itemName = "ReactiveStrikeBow";
            def.isRanged = true;
            def.rangeIncrementFeet = 60;
            def.maxRangeIncrements = 6;
            def.reachFeet = 5;
            def.diceCount = 1;
            def.dieSides = 6;
            def.damageType = DamageType.Piercing;
            return def;
        }

        private sealed class ReactiveStrikeContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;

            public readonly CombatEventBus EventBus;
            public readonly EntityManager EntityManager;
            public readonly TurnManager TurnManager;
            public readonly StrikeAction StrikeAction;
            public readonly EntityRegistry Registry;

            public ReactiveStrikeContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("TurnManagerReactiveStrikeTests_Root");

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
                var handle = Registry.Register(new EntityData
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

                var data = Registry.Get(handle);
                data.ReactionAvailable = true;

                SetPrivateField(
                    TurnManager,
                    "initiativeOrder",
                    BuildInitiativeSnapshot());

                return handle;
            }

            private List<InitiativeEntry> BuildInitiativeSnapshot()
            {
                var list = new List<InitiativeEntry>();
                foreach (var data in Registry.GetAll())
                {
                    if (data == null)
                        continue;

                    list.Add(new InitiativeEntry
                    {
                        Handle = data.Handle,
                        Roll = new CheckRoll(20 - list.Count, data.PerceptionModifier, CheckSource.Perception()),
                        IsPlayer = data.Team == Team.Player
                    });
                }

                return list;
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
