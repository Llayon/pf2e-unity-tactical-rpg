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
        public void EntityMoved_Forced_DoesNotTriggerReadiedStrike()
        {
            using var ctx = new ReadyStrikeContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0));
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0));
            var actorData = ctx.Registry.Get(actor);
            Assert.IsNotNull(actorData);
            actorData.ReactionAvailable = true;

            Assert.IsTrue(ctx.TurnManager.TryPrepareReadiedStrike(actor, preparedRound: 1));
            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor));

            var targetData = ctx.Registry.Get(target);
            Assert.IsNotNull(targetData);
            targetData.GridPosition = new Vector3Int(2, 0, 0);
            ctx.EventBus.PublishEntityMoved(target, new Vector3Int(1, 0, 0), targetData.GridPosition, forced: true);

            Assert.IsTrue(ctx.TurnManager.HasReadiedStrike(actor), "Forced movement should not consume readied strike.");
            Assert.IsTrue(actorData.ReactionAvailable, "Forced movement should not consume reaction.");
        }

        [Test]
        public void EntityMoved_Normal_ConsumesReadiedStrikeAndReaction()
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

            Assert.IsFalse(ctx.TurnManager.HasReadiedStrike(actor), "Normal movement trigger should consume readied strike.");
            Assert.IsFalse(actorData.ReactionAvailable, "Triggered readied strike should consume reaction.");
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
