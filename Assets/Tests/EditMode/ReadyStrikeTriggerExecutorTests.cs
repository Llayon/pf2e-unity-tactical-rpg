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
    public class ReadyStrikeTriggerExecutorTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void Resolve_ValidPreparedActor_ConsumesReactionAndPrepared()
        {
            using var ctx = new ExecutorContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);
            actorData.ReactionAvailable = true;
            Assert.IsTrue(ctx.ReadyService.TryPrepare(actor, preparedRound: 1));

            ExecuteInTriggerWindow(ctx, actor, target);

            Assert.IsFalse(ctx.ReadyService.HasPrepared(actor), "Prepared ready should be removed after successful trigger.");
            Assert.IsFalse(actorData.ReactionAvailable, "Ready trigger should consume actor reaction.");
            Assert.IsFalse(ctx.Executor.IsResolving, "Executor resolving flag should be restored after execution.");
        }

        [Test]
        public void Resolve_ReactionUnavailable_KeepsPrepared()
        {
            using var ctx = new ExecutorContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);
            actorData.ReactionAvailable = false;
            Assert.IsTrue(ctx.ReadyService.TryPrepare(actor, preparedRound: 1));

            ExecuteInTriggerWindow(ctx, actor, target);

            Assert.IsTrue(ctx.ReadyService.HasPrepared(actor), "Prepared ready should remain when reaction is unavailable.");
            Assert.IsFalse(actorData.ReactionAvailable, "Reaction availability should stay unchanged.");
        }

        [Test]
        public void Resolve_DeadActor_RemovesPreparedWithoutConsumingReaction()
        {
            using var ctx = new ExecutorContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);
            actorData.ReactionAvailable = true;
            actorData.CurrentHP = 0;
            Assert.IsTrue(ctx.ReadyService.TryPrepare(actor, preparedRound: 1));

            ExecuteInTriggerWindow(ctx, actor, target);

            Assert.IsFalse(ctx.ReadyService.HasPrepared(actor), "Prepared ready should be cleaned up for dead actor.");
            Assert.IsTrue(actorData.ReactionAvailable, "Dead-actor cleanup should not flip reaction availability.");
        }

        [Test]
        public void Resolve_TargetDeadAfterTrigger_KeepsPreparedAndReaction()
        {
            using var ctx = new ExecutorContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            var actorData = ctx.Registry.Get(actor);
            var targetData = ctx.Registry.Get(target);
            Assert.IsNotNull(actorData);
            Assert.IsNotNull(targetData);
            actorData.ReactionAvailable = true;
            targetData.CurrentHP = 0; // Simulates first ready striker killing the trigger target.
            Assert.IsTrue(ctx.ReadyService.TryPrepare(actor, preparedRound: 1));

            ExecuteInTriggerWindow(ctx, actor, target);

            Assert.IsTrue(ctx.ReadyService.HasPrepared(actor), "Prepared ready should remain when trigger target is no longer valid.");
            Assert.IsTrue(actorData.ReactionAvailable, "Reaction should not be consumed when readied strike does not execute.");
        }

        private sealed class ExecutorContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public EntityRegistry Registry { get; }
            public StrikeAction StrikeAction { get; }
            public ReadyStrikeService ReadyService { get; } = new();
            public ReadyStrikeTriggerExecutor Executor { get; } = new();

            public ExecutorContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("ReadyStrikeTriggerExecutorTests_Root");

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
            }

            public EntityHandle RegisterEntity(string name, Team team, Vector3Int position, int strength)
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

            public bool CanUseReaction(EntityHandle actor)
            {
                if (!actor.IsValid)
                    return false;

                var data = Registry.Get(actor);
                return data != null && data.IsAlive && data.ReactionAvailable;
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

        private static void ExecuteInTriggerWindow(ExecutorContext ctx, EntityHandle actor, EntityHandle target)
        {
            var windowToken = ctx.ReadyService.OpenTriggerWindow(TriggerWindowType.MovementEnter);
            try
            {
                ctx.Executor.Resolve(
                    actor,
                    target,
                    "movement",
                    ctx.ReadyService,
                    ctx.EntityManager,
                    ctx.StrikeAction,
                    ctx.EventBus,
                    ctx.CanUseReaction,
                    windowToken);
            }
            finally
            {
                ctx.ReadyService.CloseTriggerWindow(windowToken);
            }
        }
    }
}
