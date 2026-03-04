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
    public class ReadyStrikeRuntimeCoordinatorTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryPrepare_RequiresAliveActorAndReaction()
        {
            using var ctx = new RuntimeContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0));
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);

            actorData.ReactionAvailable = false;
            Assert.IsFalse(ctx.Coordinator.TryPrepare(actor, 1, actorData, ctx.CanUseReaction));

            actorData.ReactionAvailable = true;
            actorData.CurrentHP = 0;
            Assert.IsFalse(ctx.Coordinator.TryPrepare(actor, 1, actorData, ctx.CanUseReaction));

            actorData.CurrentHP = actorData.MaxHP;
            Assert.IsTrue(ctx.Coordinator.TryPrepare(actor, 1, actorData, ctx.CanUseReaction, ReadyTriggerMode.Attack));
            Assert.IsTrue(ctx.Coordinator.HasPrepared(actor));
            Assert.IsTrue(ctx.Coordinator.TryGetPreparedTriggerMode(actor, out var triggerMode));
            Assert.AreEqual(ReadyTriggerMode.Attack, triggerMode);
        }

        [Test]
        public void HandleEntityMoved_PlayerTurn_ConsumesPreparedAndReaction()
        {
            using var ctx = new RuntimeContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(2, 0, 0), strength: 10);
            var actorData = ctx.Registry.Get(actor);
            var enemyData = ctx.Registry.Get(enemy);
            Assert.IsNotNull(actorData);
            Assert.IsNotNull(enemyData);

            actorData.ReactionAvailable = true;
            Assert.IsTrue(ctx.Coordinator.TryPrepare(actor, 1, actorData, ctx.CanUseReaction));

            var evt = new EntityMovedEvent(enemy, from: new Vector3Int(2, 0, 0), to: new Vector3Int(1, 0, 0), forced: false);
            enemyData.GridPosition = evt.to; // Runtime contract: position is already updated at publish time.

            ctx.Coordinator.HandleEntityMoved(
                in evt,
                TurnState.PlayerTurn,
                ctx.EntityManager,
                ctx.StrikeAction,
                handle => handle == actor ? 0 : 1,
                ctx.CanUseReaction,
                ctx.EventBus);

            Assert.IsFalse(ctx.Coordinator.HasPrepared(actor));
            Assert.IsFalse(actorData.ReactionAvailable);
        }

        [Test]
        public void HandleEntityMoved_InactiveState_DoesNotConsumePrepared()
        {
            using var ctx = new RuntimeContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(2, 0, 0), strength: 10);
            var actorData = ctx.Registry.Get(actor);
            var enemyData = ctx.Registry.Get(enemy);
            Assert.IsNotNull(actorData);
            Assert.IsNotNull(enemyData);

            actorData.ReactionAvailable = true;
            Assert.IsTrue(ctx.Coordinator.TryPrepare(actor, 1, actorData, ctx.CanUseReaction));

            var evt = new EntityMovedEvent(enemy, from: new Vector3Int(2, 0, 0), to: new Vector3Int(1, 0, 0), forced: false);
            enemyData.GridPosition = evt.to;

            ctx.Coordinator.HandleEntityMoved(
                in evt,
                TurnState.Inactive,
                ctx.EntityManager,
                ctx.StrikeAction,
                handle => handle == actor ? 0 : 1,
                ctx.CanUseReaction,
                ctx.EventBus);

            Assert.IsTrue(ctx.Coordinator.HasPrepared(actor));
            Assert.IsTrue(actorData.ReactionAvailable);
        }

        private sealed class RuntimeContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public EntityRegistry Registry { get; }
            public StrikeAction StrikeAction { get; }
            public ReadyStrikeRuntimeCoordinator Coordinator { get; } = new();

            public RuntimeContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("ReadyStrikeRuntimeCoordinatorTests_Root");

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
    }
}
