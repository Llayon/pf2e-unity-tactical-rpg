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
    public class PlayerActionExecutorReactiveStrikeTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryConfirmFear_ReactiveStrikeKillsCaster_ActionFailsAndSpendsNoActions()
        {
            using var ctx = new ExecutorContext();
            var meleeDef = CreateMeleeWeaponDefinition();

            try
            {
                var wizard = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), hp: 1, strength: 8, intelligence: 18);
                var fighter = ctx.RegisterEntity("Fighter", Team.Enemy, new Vector3Int(1, 0, 0), hp: 30, strength: 5000);

                var wizardData = ctx.Registry.Get(wizard);
                var fighterData = ctx.Registry.Get(fighter);
                wizardData.KnowsFear = true;
                fighterData.HasReactiveStrike = true;
                fighterData.ReactionAvailable = true;
                fighterData.EquippedWeapon = new WeaponInstance
                {
                    def = meleeDef,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                ctx.SetCurrentActor(wizard, 3);

                int spellResolvedCount = 0;
                ctx.EventBus.OnSpellResolvedTyped += HandleSpellResolved;
                try
                {
                    bool executed = ctx.Executor.TryConfirmFear(fighter);

                    Assert.IsFalse(executed);
                    Assert.AreEqual(0, spellResolvedCount, "Interrupted spell must not publish SpellResolved.");
                    Assert.AreEqual(3, wizardData.ActionsRemaining, "Interrupted spell must not spend actions.");
                    Assert.LessOrEqual(wizardData.CurrentHP, 0, "Reactive Strike should kill the caster before spell resolution.");
                    Assert.IsFalse(fighterData.ReactionAvailable, "Reactive Strike should consume the fighter reaction.");
                }
                finally
                {
                    ctx.EventBus.OnSpellResolvedTyped -= HandleSpellResolved;
                }

                void HandleSpellResolved(in SpellResolvedEvent e)
                {
                    _ = e;
                    spellResolvedCount++;
                }
            }
            finally
            {
                Object.DestroyImmediate(meleeDef);
            }
        }

        [Test]
        public void TryConfirmFear_ReactiveStrikeCriticalHitDisruptsManipulateWithoutSpendingActions()
        {
            using var ctx = new ExecutorContext();
            var meleeDef = CreateMeleeWeaponDefinition();

            try
            {
                var wizard = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), hp: 20000, strength: 8, intelligence: 18);
                var fighter = ctx.RegisterEntity("Fighter", Team.Enemy, new Vector3Int(1, 0, 0), hp: 30, strength: 5000);

                var wizardData = ctx.Registry.Get(wizard);
                var fighterData = ctx.Registry.Get(fighter);
                wizardData.KnowsFear = true;
                fighterData.HasReactiveStrike = true;
                fighterData.ReactionAvailable = true;
                fighterData.EquippedWeapon = new WeaponInstance
                {
                    def = meleeDef,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                ctx.SetCurrentActor(wizard, 3);
                ctx.SetReactiveStrikeRng(new FixedRng(d20Rolls: new[] { 10 }, dieRolls: new[] { 4 }));

                int spellResolvedCount = 0;
                ctx.EventBus.OnSpellResolvedTyped += HandleSpellResolved;
                try
                {
                    bool executed = ctx.Executor.TryConfirmFear(fighter);

                    Assert.IsFalse(executed);
                    Assert.AreEqual(0, spellResolvedCount, "Disrupted spell must not publish SpellResolved.");
                    Assert.AreEqual(3, wizardData.ActionsRemaining, "Disrupted spell must not spend actions.");
                    Assert.Greater(wizardData.CurrentHP, 0, "Critical Reactive Strike should disrupt without killing the caster in this scenario.");
                    Assert.IsFalse(fighterData.ReactionAvailable, "Reactive Strike should consume the fighter reaction.");
                    Assert.AreEqual(TurnState.PlayerTurn, ctx.TurnManager.State, "Disrupted action should return control to the current turn.");
                }
                finally
                {
                    ctx.EventBus.OnSpellResolvedTyped -= HandleSpellResolved;
                }

                void HandleSpellResolved(in SpellResolvedEvent e)
                {
                    _ = e;
                    spellResolvedCount++;
                }
            }
            finally
            {
                Object.DestroyImmediate(meleeDef);
            }
        }

        [Test]
        public void TryConfirmFear_NonCriticalReactiveStrikeHit_DoesNotDisruptManipulate()
        {
            using var ctx = new ExecutorContext();
            var meleeDef = CreateMeleeWeaponDefinition();

            try
            {
                var wizard = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), hp: 30, strength: 8, intelligence: 18);
                var fighter = ctx.RegisterEntity("Fighter", Team.Enemy, new Vector3Int(1, 0, 0), hp: 30, strength: 12);

                var wizardData = ctx.Registry.Get(wizard);
                var fighterData = ctx.Registry.Get(fighter);
                wizardData.KnowsFear = true;
                fighterData.HasReactiveStrike = true;
                fighterData.ReactionAvailable = true;
                fighterData.EquippedWeapon = new WeaponInstance
                {
                    def = meleeDef,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                ctx.SetCurrentActor(wizard, 3);
                ctx.SetReactiveStrikeRng(new FixedRng(d20Rolls: new[] { 11 }, dieRolls: new[] { 4 }));

                int spellResolvedCount = 0;
                ctx.EventBus.OnSpellResolvedTyped += HandleSpellResolved;
                try
                {
                    bool executed = ctx.Executor.TryConfirmFear(fighter);

                    Assert.IsTrue(executed);
                    Assert.AreEqual(1, spellResolvedCount, "Non-critical Reactive Strike should not stop spell resolution.");
                    Assert.AreEqual(1, wizardData.ActionsRemaining, "Resolved Fear should spend 2 actions.");
                    Assert.Greater(wizardData.CurrentHP, 0, "Caster should survive the non-critical Reactive Strike.");
                    Assert.IsFalse(fighterData.ReactionAvailable, "Reactive Strike should still consume the fighter reaction.");
                }
                finally
                {
                    ctx.EventBus.OnSpellResolvedTyped -= HandleSpellResolved;
                }

                void HandleSpellResolved(in SpellResolvedEvent e)
                {
                    _ = e;
                    spellResolvedCount++;
                }
            }
            finally
            {
                Object.DestroyImmediate(meleeDef);
            }
        }

        [Test]
        public void TryExecuteStand_ReactiveStrikeKillsActor_ActionFailsAndSpendsNoActions()
        {
            using var ctx = new ExecutorContext();
            var meleeDef = CreateMeleeWeaponDefinition();

            try
            {
                var wizard = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0), hp: 1, strength: 8);
                var fighter = ctx.RegisterEntity("Fighter", Team.Enemy, new Vector3Int(1, 0, 0), hp: 30, strength: 5000);

                var wizardData = ctx.Registry.Get(wizard);
                var fighterData = ctx.Registry.Get(fighter);
                wizardData.Conditions.Add(new ActiveCondition(ConditionType.Prone));
                fighterData.HasReactiveStrike = true;
                fighterData.ReactionAvailable = true;
                fighterData.EquippedWeapon = new WeaponInstance
                {
                    def = meleeDef,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                ctx.SetCurrentActor(wizard, 3);

                bool executed = ctx.Executor.TryExecuteStand();

                Assert.IsFalse(executed);
                Assert.AreEqual(3, wizardData.ActionsRemaining, "Interrupted stand must not spend actions.");
                Assert.IsTrue(wizardData.HasCondition(ConditionType.Prone), "Interrupted stand must not remove prone.");
                Assert.LessOrEqual(wizardData.CurrentHP, 0, "Reactive Strike should kill the actor before stand resolves.");
                Assert.IsFalse(fighterData.ReactionAvailable, "Reactive Strike should consume the fighter reaction.");
            }
            finally
            {
                Object.DestroyImmediate(meleeDef);
            }
        }

        private static WeaponDefinition CreateMeleeWeaponDefinition()
        {
            var def = ScriptableObject.CreateInstance<WeaponDefinition>();
            def.itemName = "ReactiveStrikeSword";
            def.isRanged = false;
            def.reachFeet = 5;
            def.diceCount = 1;
            def.dieSides = 6;
            def.damageType = DamageType.Slashing;
            def.category = WeaponCategory.Martial;
            def.group = WeaponGroup.Sword;
            def.hands = WeaponHands.One;
            return def;
        }

        private sealed class ExecutorContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;
            private readonly List<EntityHandle> registeredHandles = new();

            public readonly CombatEventBus EventBus;
            public readonly EntityManager EntityManager;
            public readonly TurnManager TurnManager;
            public readonly PlayerActionExecutor Executor;
            public readonly StrikeAction StrikeAction;
            public readonly StandAction StandAction;
            public readonly EntityRegistry Registry;

            public ExecutorContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("PlayerActionExecutorReactiveStrikeTests_Root");

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

                var standActionGo = new GameObject("StandAction");
                standActionGo.transform.SetParent(root.transform);
                StandAction = standActionGo.AddComponent<StandAction>();
                SetPrivateField(StandAction, "entityManager", EntityManager);
                SetPrivateField(StandAction, "eventBus", EventBus);

                var turnManagerGo = new GameObject("TurnManager");
                turnManagerGo.transform.SetParent(root.transform);
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);
                SetPrivateField(TurnManager, "strikeAction", StrikeAction);
                SetPrivateField(TurnManager, "initiativeOrder", new List<InitiativeEntry>());
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
                SetPrivateField(TurnManager, "roundNumber", 1);

                var binder = turnManagerGo.AddComponent<ReadyStrikeEventBinder>();
                SetPrivateField(binder, "turnManager", TurnManager);
                SetPrivateField(binder, "eventBus", EventBus);
                InvokePrivate(TurnManager, "OnEnable", System.Array.Empty<object>());

                var executorGo = new GameObject("Executor");
                executorGo.transform.SetParent(root.transform);
                Executor = executorGo.AddComponent<PlayerActionExecutor>();
                SetPrivateField(Executor, "turnManager", TurnManager);
                SetPrivateField(Executor, "entityManager", EntityManager);
                SetPrivateField(Executor, "eventBus", EventBus);
                SetPrivateField(Executor, "standAction", StandAction);
            }

            public EntityHandle RegisterEntity(
                string name,
                Team team,
                Vector3Int gridPosition,
                int hp = 10,
                int strength = 10,
                int intelligence = 10)
            {
                var handle = Registry.Register(new EntityData
                {
                    Name = name,
                    Team = team,
                    Level = 1,
                    Strength = strength,
                    Dexterity = 10,
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
                        Roll = new CheckRoll(20, 0, CheckSource.Perception()),
                        IsPlayer = actorData.Team == Team.Player
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
                        Roll = new CheckRoll(10 - i, 0, CheckSource.Perception()),
                        IsPlayer = data != null && data.Team == Team.Player
                    });
                }

                SetPrivateField(TurnManager, "initiativeOrder", order);
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
            }

            public void SetReactiveStrikeRng(IRng rng)
            {
                InvokePrivate(TurnManager, "SetReactiveStrikeRngForTesting", new object[] { rng });
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

        private sealed class FixedRng : IRng
        {
            private readonly Queue<int> d20Rolls;
            private readonly Queue<int> dieRolls;

            public FixedRng(IEnumerable<int> d20Rolls = null, IEnumerable<int> dieRolls = null)
            {
                this.d20Rolls = new Queue<int>(d20Rolls ?? new[] { 10 });
                this.dieRolls = new Queue<int>(dieRolls ?? new[] { 1 });
            }

            public int RollD20()
            {
                return d20Rolls.Count > 0 ? d20Rolls.Dequeue() : 10;
            }

            public int RollDie(int sides)
            {
                _ = sides;
                return dieRolls.Count > 0 ? dieRolls.Dequeue() : 1;
            }
        }

        private static void InvokePrivate(object target, string methodName, object[] args)
        {
            var method = target.GetType().GetMethod(methodName, InstanceNonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}");
            method.Invoke(target, args);
        }
    }
}
