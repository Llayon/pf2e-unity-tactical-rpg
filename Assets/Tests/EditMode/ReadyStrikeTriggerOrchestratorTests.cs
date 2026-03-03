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
    public class ReadyStrikeTriggerOrchestratorTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void HandleEntityMoved_SortsCandidatesByInitiative_AndRemovesStalePrepared()
        {
            using var ctx = new OrchestratorContext();

            var actorA = ctx.RegisterEntity("Fighter_A", Team.Player, new Vector3Int(0, 0, 0));
            var actorB = ctx.RegisterEntity("Fighter_B", Team.Player, new Vector3Int(0, 0, 1));
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(2, 0, 0));
            var staleActor = new EntityHandle(999);

            Assert.IsTrue(ctx.ReadyService.TryPrepare(actorA, preparedRound: 1));
            Assert.IsTrue(ctx.ReadyService.TryPrepare(actorB, preparedRound: 1));
            Assert.IsTrue(ctx.ReadyService.TryPrepare(staleActor, preparedRound: 1));
            Assert.IsTrue(ctx.ReadyService.HasPrepared(staleActor));

            var dispatchOrder = new List<EntityHandle>(4);
            int FindIndex(EntityHandle handle) => handle == actorB ? 1 : (handle == actorA ? 2 : 99);

            var evt = new EntityMovedEvent(
                enemy,
                from: new Vector3Int(2, 0, 0),
                to: new Vector3Int(1, 0, 0),
                forced: false);

            ctx.Orchestrator.HandleEntityMoved(
                in evt,
                ctx.ReadyService,
                ctx.EntityManager,
                ctx.StrikeAction,
                FindIndex,
                (actor, target, reason) =>
                {
                    _ = target;
                    _ = reason;
                    dispatchOrder.Add(actor);
                });

            Assert.IsFalse(ctx.ReadyService.HasPrepared(staleActor), "Stale prepared actor should be removed.");
            Assert.AreEqual(2, dispatchOrder.Count, "Expected two ready candidates to be dispatched.");
            Assert.AreEqual(actorB, dispatchOrder[0], "Lower initiative index should dispatch first.");
            Assert.AreEqual(actorA, dispatchOrder[1], "Higher initiative index should dispatch after lower index.");
        }

        [Test]
        public void HandleStrikePreDamage_SortsCandidatesByInitiative_AndDispatchesAgainstAttacker()
        {
            using var ctx = new OrchestratorContext();

            var actorA = ctx.RegisterEntity("Fighter_A", Team.Player, new Vector3Int(0, 0, 0));
            var actorB = ctx.RegisterEntity("Fighter_B", Team.Player, new Vector3Int(0, 0, 1));
            var ally = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(1, 0, 0));
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 1));

            Assert.IsTrue(ctx.ReadyService.TryPrepare(actorA, preparedRound: 1));
            Assert.IsTrue(ctx.ReadyService.TryPrepare(actorB, preparedRound: 1));

            var dispatchOrder = new List<EntityHandle>(4);
            var targets = new List<EntityHandle>(4);
            int FindIndex(EntityHandle handle) => handle == actorA ? 1 : (handle == actorB ? 2 : 99);

            var evt = new StrikePreDamageEvent(
                attacker: enemy,
                target: ally,
                naturalRoll: 15,
                total: 20,
                dc: 18,
                degree: DegreeOfSuccess.Success,
                damageRolled: 5,
                damageType: DamageType.Slashing);

            ctx.Orchestrator.HandleStrikePreDamage(
                in evt,
                ctx.ReadyService,
                ctx.EntityManager,
                ctx.StrikeAction,
                FindIndex,
                (actor, target, reason) =>
                {
                    _ = reason;
                    dispatchOrder.Add(actor);
                    targets.Add(target);
                });

            Assert.AreEqual(2, dispatchOrder.Count, "Expected two ready candidates to dispatch on attack trigger.");
            Assert.AreEqual(actorA, dispatchOrder[0]);
            Assert.AreEqual(actorB, dispatchOrder[1]);
            Assert.AreEqual(enemy, targets[0], "Ready trigger target should be the attack source.");
            Assert.AreEqual(enemy, targets[1], "Ready trigger target should be the attack source.");
        }

        private sealed class OrchestratorContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;

            public EntityManager EntityManager { get; }
            public EntityRegistry Registry { get; }
            public StrikeAction StrikeAction { get; }
            public ReadyStrikeService ReadyService { get; } = new();
            public ReadyStrikeTriggerOrchestrator Orchestrator { get; } = new();

            public OrchestratorContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("ReadyStrikeTriggerOrchestratorTests_Root");

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
