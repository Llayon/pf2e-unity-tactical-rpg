using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class TurnManagerActionLockTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TurnManager_BeginActionExecution_TracksActorAndSource_ActionCompleted_ClearsTracking()
        {
            var context = CreateCombatContext("TM_LockTrack_Clear");
            try
            {
                context.turnManager.BeginActionExecution(context.player.Handle, "Test.Lock");

                Assert.AreEqual(TurnState.ExecutingAction, context.turnManager.State);
                Assert.AreEqual(context.player.Handle, context.turnManager.ExecutingActor);
                Assert.AreEqual("Test.Lock", context.turnManager.ExecutingActionSource);
                Assert.GreaterOrEqual(context.turnManager.ExecutingActionDurationSeconds, 0f);

                context.turnManager.ActionCompleted();

                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State);
                Assert.AreEqual(EntityHandle.None, context.turnManager.ExecutingActor);
                Assert.IsTrue(string.IsNullOrEmpty(context.turnManager.ExecutingActionSource));
                Assert.AreEqual(0f, context.turnManager.ExecutingActionDurationSeconds);
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_CompleteActionWithCost_SpendsOnExecutingActor_NotCurrentEntity()
        {
            var context = CreateCombatContext("TM_LockSpend_ExecutingActor");
            try
            {
                context.enemy.ActionsRemaining = 3;

                context.turnManager.BeginActionExecution(context.player.Handle, "Test.SpendActor");

                int enemyIndex = FindInitiativeIndex(context.turnManager, context.enemy.Handle);
                Assert.GreaterOrEqual(enemyIndex, 0, "Enemy must exist in initiative order for test.");
                SetPrivateField(context.turnManager, "currentIndex", enemyIndex);

                context.turnManager.CompleteActionWithCost(1);

                Assert.AreEqual(2, context.player.ActionsRemaining, "Cost should be paid by executing actor.");
                Assert.AreEqual(3, context.enemy.ActionsRemaining, "Non-executing actor should remain unchanged.");
                Assert.AreEqual(EntityHandle.None, context.turnManager.ExecutingActor);
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_CompleteActionWithCost_ClearsTrackingAndRestoresTurn()
        {
            var context = CreateCombatContext("TM_LockComplete_Clear");
            try
            {
                context.turnManager.BeginActionExecution(context.player.Handle, "Test.Complete");
                context.turnManager.CompleteActionWithCost(1);

                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State);
                Assert.AreEqual(2, context.player.ActionsRemaining);
                Assert.AreEqual(EntityHandle.None, context.turnManager.ExecutingActor);
                Assert.IsTrue(string.IsNullOrEmpty(context.turnManager.ExecutingActionSource));
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_EndCombat_WhileExecutingAction_ClearsTracking()
        {
            var context = CreateCombatContext("TM_LockEndCombat_Clear");
            try
            {
                context.turnManager.BeginActionExecution(context.player.Handle, "Test.EndCombat");
                context.turnManager.EndCombat();

                Assert.AreEqual(TurnState.Inactive, context.turnManager.State);
                Assert.AreEqual(EntityHandle.None, context.turnManager.ExecutingActor);
                Assert.IsTrue(string.IsNullOrEmpty(context.turnManager.ExecutingActionSource));
                Assert.AreEqual(0f, context.turnManager.ExecutingActionDurationSeconds);
            }
            finally
            {
                DestroyContext(context);
            }
        }

        private static TestCombatContext CreateCombatContext(string namePrefix)
        {
            var turnManagerGo = new GameObject($"{namePrefix}_TurnManager");
            var entityManagerGo = new GameObject($"{namePrefix}_EntityManager");

            var turnManager = turnManagerGo.AddComponent<TurnManager>();
            LogAssert.Expect(LogType.Error, "[EntityManager] Missing reference: GridManager. Assign it in Inspector.");
            var entityManager = entityManagerGo.AddComponent<EntityManager>();
            var registry = new EntityRegistry();

            SetPrivateField(turnManager, "entityManager", entityManager);
            SetAutoPropertyBackingField(entityManager, "Registry", registry);

            var player = CreateEntity("Player", Team.Player, 5000);
            var enemy = CreateEntity("Enemy", Team.Enemy, 10);
            registry.Register(player);
            registry.Register(enemy);

            turnManager.StartCombat();
            Assert.AreEqual(TurnState.PlayerTurn, turnManager.State, "Player should act first in test setup.");
            Assert.AreEqual(player.Handle, turnManager.CurrentEntity, "Current actor must be player in test setup.");

            return new TestCombatContext
            {
                turnManagerGo = turnManagerGo,
                entityManagerGo = entityManagerGo,
                turnManager = turnManager,
                player = player,
                enemy = enemy
            };
        }

        private static void DestroyContext(TestCombatContext context)
        {
            if (context.turnManagerGo != null)
                Object.DestroyImmediate(context.turnManagerGo);
            if (context.entityManagerGo != null)
                Object.DestroyImmediate(context.entityManagerGo);
        }

        private static EntityData CreateEntity(string name, Team team, int wisdom)
        {
            return new EntityData
            {
                Name = name,
                Team = team,
                MaxHP = 30,
                CurrentHP = 30,
                Speed = 25,
                Strength = 10,
                Dexterity = 10,
                Constitution = 10,
                Intelligence = 10,
                Wisdom = wisdom,
                Charisma = 10
            };
        }

        private static int FindInitiativeIndex(TurnManager turnManager, EntityHandle handle)
        {
            var order = turnManager.InitiativeOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i].Handle == handle)
                    return i;
            }

            return -1;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            var fieldName = $"<{propertyName}>k__BackingField";
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing backing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private sealed class TestCombatContext
        {
            public GameObject turnManagerGo;
            public GameObject entityManagerGo;
            public TurnManager turnManager;
            public EntityData player;
            public EntityData enemy;
        }
    }
}
