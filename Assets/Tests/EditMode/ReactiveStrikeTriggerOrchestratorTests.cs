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
    public class ReactiveStrikeTriggerOrchestratorTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void HandleEntityMoved_StartedWithinReach_DispatchesInInitiativeOrder()
        {
            using var ctx = new OrchestratorContext();

            var actorA = ctx.RegisterEntity("Fighter_A", Team.Player, new Vector3Int(0, 0, 0));
            var actorB = ctx.RegisterEntity("Fighter_B", Team.Player, new Vector3Int(0, 0, 1));
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0));

            ctx.Registry.Get(actorA).HasReactiveStrike = true;
            ctx.Registry.Get(actorB).HasReactiveStrike = true;

            var dispatchOrder = new List<EntityHandle>(4);
            var evt = new EntityMovedEvent(
                enemy,
                from: new Vector3Int(1, 0, 0),
                to: new Vector3Int(1, 0, 1),
                forced: false);

            var movedData = ctx.Registry.Get(enemy);
            movedData.GridPosition = evt.to;

            var token = ctx.Ledger.OpenWindow(TriggerWindowType.MovementEnter);
            try
            {
                ctx.Orchestrator.HandleEntityMoved(
                    in evt,
                    ctx.InitiativeOrder(actorB, actorA, enemy),
                    ctx.EntityManager,
                    ctx.StrikeAction,
                    handle => true,
                    (actor, target, reason, windowToken) =>
                    {
                        _ = windowToken;
                        dispatchOrder.Add(actor);
                        Assert.AreEqual(enemy, target);
                        Assert.AreEqual("movement", reason);
                    },
                    token);
            }
            finally
            {
                ctx.Ledger.CloseWindow(token);
            }

            Assert.AreEqual(2, dispatchOrder.Count);
            Assert.AreEqual(actorB, dispatchOrder[0]);
            Assert.AreEqual(actorA, dispatchOrder[1]);
        }

        [Test]
        public void HandleEntityMoved_EnteringReachFromOutside_DoesNotDispatch()
        {
            using var ctx = new OrchestratorContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0));
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(2, 0, 0));
            ctx.Registry.Get(actor).HasReactiveStrike = true;

            int dispatchCount = 0;
            var evt = new EntityMovedEvent(
                enemy,
                from: new Vector3Int(2, 0, 0),
                to: new Vector3Int(1, 0, 0),
                forced: false);

            var movedData = ctx.Registry.Get(enemy);
            movedData.GridPosition = evt.to;

            var token = ctx.Ledger.OpenWindow(TriggerWindowType.MovementEnter);
            try
            {
                ctx.Orchestrator.HandleEntityMoved(
                    in evt,
                    ctx.InitiativeOrder(actor, enemy),
                    ctx.EntityManager,
                    ctx.StrikeAction,
                    handle => true,
                    (triggerActor, triggerTarget, triggerReason, windowToken) =>
                    {
                        _ = triggerActor;
                        _ = triggerTarget;
                        _ = triggerReason;
                        _ = windowToken;
                        dispatchCount++;
                    },
                    token);
            }
            finally
            {
                ctx.Ledger.CloseWindow(token);
            }

            Assert.AreEqual(0, dispatchCount);
        }

        [Test]
        public void HandleEntityMoved_StepWithinReach_DoesNotDispatch()
        {
            using var ctx = new OrchestratorContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0));
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0));
            ctx.Registry.Get(actor).HasReactiveStrike = true;

            int dispatchCount = 0;
            var evt = new EntityMovedEvent(
                enemy,
                from: new Vector3Int(1, 0, 0),
                to: new Vector3Int(1, 0, 1),
                movementTriggerKind: MovementTriggerKind.Step);

            var movedData = ctx.Registry.Get(enemy);
            movedData.GridPosition = evt.to;

            var token = ctx.Ledger.OpenWindow(TriggerWindowType.MovementEnter);
            try
            {
                ctx.Orchestrator.HandleEntityMoved(
                    in evt,
                    ctx.InitiativeOrder(actor, enemy),
                    ctx.EntityManager,
                    ctx.StrikeAction,
                    handle => true,
                    (triggerActor, triggerTarget, triggerReason, windowToken) =>
                    {
                        _ = triggerActor;
                        _ = triggerTarget;
                        _ = triggerReason;
                        _ = windowToken;
                        dispatchCount++;
                    },
                    token);
            }
            finally
            {
                ctx.Ledger.CloseWindow(token);
            }

            Assert.AreEqual(0, dispatchCount);
        }

        [Test]
        public void HandleStrikePreDamage_RangedAttackWithinReach_Dispatches()
        {
            using var ctx = new OrchestratorContext();
            var rangedDef = CreateRangedWeaponDefinition();

            try
            {
                var fighter = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0));
                var ally = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(1, 0, 1));
                var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0));

                ctx.Registry.Get(fighter).HasReactiveStrike = true;
                ctx.Registry.Get(enemy).EquippedWeapon = new WeaponInstance
                {
                    def = rangedDef,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                int dispatchCount = 0;
                var evt = new StrikePreDamageEvent(
                    attacker: enemy,
                    target: ally,
                    naturalRoll: 15,
                    total: 20,
                    dc: 18,
                    degree: DegreeOfSuccess.Success,
                    damageRolled: 5,
                    damageType: DamageType.Piercing);

                var token = ctx.Ledger.OpenWindow(TriggerWindowType.AttackStart);
                try
                {
                    ctx.Orchestrator.HandleStrikePreDamage(
                        in evt,
                        ctx.InitiativeOrder(fighter, ally, enemy),
                        ctx.EntityManager,
                        ctx.StrikeAction,
                        handle => true,
                        (triggerActor, triggerTarget, triggerReason, windowToken) =>
                        {
                            _ = triggerReason;
                            _ = windowToken;
                            dispatchCount++;
                            Assert.AreEqual(fighter, triggerActor);
                            Assert.AreEqual(enemy, triggerTarget);
                        },
                        token);
                }
                finally
                {
                    ctx.Ledger.CloseWindow(token);
                }

                Assert.AreEqual(1, dispatchCount);
            }
            finally
            {
                Object.DestroyImmediate(rangedDef);
            }
        }

        [Test]
        public void HandleStrikePreDamage_MeleeAttack_DoesNotDispatch()
        {
            using var ctx = new OrchestratorContext();

            var fighter = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0));
            var ally = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(1, 0, 1));
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0));
            ctx.Registry.Get(fighter).HasReactiveStrike = true;

            int dispatchCount = 0;
            var evt = new StrikePreDamageEvent(
                attacker: enemy,
                target: ally,
                naturalRoll: 15,
                total: 20,
                dc: 18,
                degree: DegreeOfSuccess.Success,
                damageRolled: 5,
                damageType: DamageType.Slashing);

            var token = ctx.Ledger.OpenWindow(TriggerWindowType.AttackStart);
            try
            {
                ctx.Orchestrator.HandleStrikePreDamage(
                    in evt,
                    ctx.InitiativeOrder(fighter, ally, enemy),
                    ctx.EntityManager,
                    ctx.StrikeAction,
                    handle => true,
                    (triggerActor, triggerTarget, triggerReason, windowToken) =>
                    {
                        _ = triggerActor;
                        _ = triggerTarget;
                        _ = triggerReason;
                        _ = windowToken;
                        dispatchCount++;
                    },
                    token);
            }
            finally
            {
                ctx.Ledger.CloseWindow(token);
            }

            Assert.AreEqual(0, dispatchCount);
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

        private sealed class OrchestratorContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;

            public EntityManager EntityManager { get; }
            public EntityRegistry Registry { get; }
            public StrikeAction StrikeAction { get; }
            public ReactiveStrikeTriggerOrchestrator Orchestrator { get; } = new();
            public TriggerWindowLedger Ledger { get; } = new();

            public OrchestratorContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("ReactiveStrikeTriggerOrchestratorTests_Root");

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                var strikeActionGo = new GameObject("StrikeAction");
                strikeActionGo.transform.SetParent(root.transform);
                StrikeAction = strikeActionGo.AddComponent<StrikeAction>();
                SetPrivateField(StrikeAction, "entityManager", EntityManager);
            }

            public EntityHandle RegisterEntity(string name, Team team, Vector3Int position)
            {
                return Registry.Register(new EntityData
                {
                    Name = name,
                    Team = team,
                    Level = 1,
                    Strength = 18,
                    Dexterity = 14,
                    Constitution = 12,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    MaxHP = 30,
                    CurrentHP = 30,
                    GridPosition = position,
                    ActionsRemaining = 3,
                    ReactionAvailable = true
                });
            }

            public IReadOnlyList<InitiativeEntry> InitiativeOrder(params EntityHandle[] handles)
            {
                var list = new List<InitiativeEntry>(handles.Length);
                for (int i = 0; i < handles.Length; i++)
                {
                    list.Add(new InitiativeEntry
                    {
                        Handle = handles[i],
                        Roll = new CheckRoll(20 - i, 0, CheckSource.Perception()),
                        IsPlayer = true
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
    }
}
