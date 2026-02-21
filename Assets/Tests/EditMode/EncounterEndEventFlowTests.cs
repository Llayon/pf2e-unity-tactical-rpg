using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class EncounterEndEventFlowTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void CombatEndedEvent_StoresResult()
        {
            var e = new CombatEndedEvent(EncounterResult.Victory);
            Assert.AreEqual(EncounterResult.Victory, e.result);
        }

        [Test]
        public void CombatEventBus_PublishCombatEnded_ForwardsResult()
        {
            var go = new GameObject("CombatEventBus_Test");
            try
            {
                var bus = go.AddComponent<CombatEventBus>();
                EncounterResult received = EncounterResult.Unknown;
                bool called = false;

                bus.OnCombatEndedTyped += (in CombatEndedEvent e) =>
                {
                    called = true;
                    received = e.result;
                };

                bus.PublishCombatEnded(EncounterResult.Defeat);

                Assert.IsTrue(called);
                Assert.AreEqual(EncounterResult.Defeat, received);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TurnManager_EndCombat_Default_IsAborted()
        {
            var go = new GameObject("TurnManager_Test");
            try
            {
                var turnManager = go.AddComponent<TurnManager>();
                EncounterResult received = EncounterResult.Unknown;
                bool called = false;

                turnManager.OnCombatEndedWithResult += e =>
                {
                    called = true;
                    received = e.result;
                };

                turnManager.EndCombat();

                Assert.IsTrue(called);
                Assert.AreEqual(EncounterResult.Aborted, received);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TurnManager_StartCombat_PublishesTypedBusLifecycleEvents_Directly()
        {
            var eventBusGo = new GameObject("CombatEventBus_Test");
            var turnManagerGo = new GameObject("TurnManager_TypedBus_Test");
            var entityManagerGo = new GameObject("EntityManager_TypedBus_Test");
            bool oldIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                var eventBus = eventBusGo.AddComponent<CombatEventBus>();
                var turnManager = turnManagerGo.AddComponent<TurnManager>();
                var entityManager = entityManagerGo.AddComponent<EntityManager>();

                SetPrivateField(turnManager, "entityManager", entityManager);
                SetPrivateField(turnManager, "eventBus", eventBus);

                var registry = entityManager.Registry ?? new EntityRegistry();
                if (entityManager.Registry == null)
                    SetAutoPropertyBackingField(entityManager, "Registry", registry);

                registry.Register(CreateEntity(Team.Player, alive: true));
                registry.Register(CreateEntity(Team.Enemy, alive: true));

                int combatStartedCount = 0;
                int initiativeRolledCount = 0;
                int roundStartedCount = 0;
                int turnStartedCount = 0;
                int actionsChangedCount = 0;

                eventBus.OnCombatStartedTyped += (in CombatStartedEvent _) => combatStartedCount++;
                eventBus.OnInitiativeRolledTyped += (in InitiativeRolledEvent e) =>
                {
                    initiativeRolledCount++;
                    Assert.GreaterOrEqual(e.order.Count, 2, "Initiative order should include both teams.");
                };
                eventBus.OnRoundStartedTyped += (in RoundStartedEvent e) =>
                {
                    roundStartedCount++;
                    Assert.AreEqual(1, e.round);
                };
                eventBus.OnTurnStartedTyped += (in TurnStartedEvent e) =>
                {
                    turnStartedCount++;
                    Assert.IsTrue(e.actor.IsValid, "TurnStarted actor should be valid.");
                };
                eventBus.OnActionsChangedTyped += (in ActionsChangedEvent e) =>
                {
                    actionsChangedCount++;
                    Assert.IsTrue(e.actor.IsValid, "ActionsChanged actor should be valid.");
                };

                turnManager.StartCombat();

                Assert.AreEqual(1, combatStartedCount, "CombatStarted typed event should publish once.");
                Assert.AreEqual(1, initiativeRolledCount, "InitiativeRolled typed event should publish once.");
                Assert.AreEqual(1, roundStartedCount, "RoundStarted typed event should publish once.");
                Assert.AreEqual(1, turnStartedCount, "TurnStarted typed event should publish once at combat start.");
                Assert.AreEqual(1, actionsChangedCount, "ActionsChanged typed event should publish once at turn start.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = oldIgnore;
                Object.DestroyImmediate(turnManagerGo);
                Object.DestroyImmediate(entityManagerGo);
                Object.DestroyImmediate(eventBusGo);
            }
        }

        [Test]
        public void TurnManager_EndTurn_PublishesTurnEnded_AndConditionsTicked_ToTypedBus()
        {
            var eventBusGo = new GameObject("CombatEventBus_EndTurn_Test");
            var turnManagerGo = new GameObject("TurnManager_EndTurn_Test");
            var entityManagerGo = new GameObject("EntityManager_EndTurn_Test");
            bool oldIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                var eventBus = eventBusGo.AddComponent<CombatEventBus>();
                var turnManager = turnManagerGo.AddComponent<TurnManager>();
                var entityManager = entityManagerGo.AddComponent<EntityManager>();

                SetPrivateField(turnManager, "entityManager", entityManager);
                SetPrivateField(turnManager, "eventBus", eventBus);

                var registry = entityManager.Registry ?? new EntityRegistry();
                if (entityManager.Registry == null)
                    SetAutoPropertyBackingField(entityManager, "Registry", registry);

                registry.Register(CreateEntity(Team.Player, alive: true));
                registry.Register(CreateEntity(Team.Enemy, alive: true));

                turnManager.StartCombat();
                var endingActor = turnManager.CurrentEntity;
                Assert.IsTrue(endingActor.IsValid, "Current actor should be valid after StartCombat.");

                var endingData = registry.Get(endingActor);
                Assert.IsNotNull(endingData, "Current actor data was not found in registry.");
                endingData.AddCondition(ConditionType.Frightened, value: 2);

                int turnEndedCount = 0;
                EntityHandle turnEndedActor = EntityHandle.None;
                int conditionsTickedCount = 0;
                EntityHandle conditionsActor = EntityHandle.None;
                ConditionTick firstTick = default;
                bool firstTickCaptured = false;

                eventBus.OnTurnEndedTyped += (in TurnEndedEvent e) =>
                {
                    turnEndedCount++;
                    turnEndedActor = e.actor;
                };

                eventBus.OnConditionsTickedTyped += (in ConditionsTickedEvent e) =>
                {
                    conditionsTickedCount++;
                    conditionsActor = e.actor;
                    if (e.ticks != null && e.ticks.Count > 0)
                    {
                        firstTick = e.ticks[0];
                        firstTickCaptured = true;
                    }
                };

                turnManager.EndTurn();

                Assert.AreEqual(1, turnEndedCount, "TurnEnded typed event should publish exactly once.");
                Assert.AreEqual(endingActor, turnEndedActor, "TurnEnded actor should match ending actor.");
                Assert.AreEqual(1, conditionsTickedCount, "ConditionsTicked typed event should publish exactly once.");
                Assert.AreEqual(endingActor, conditionsActor, "ConditionsTicked actor should match ending actor.");
                Assert.IsTrue(firstTickCaptured, "Expected at least one condition tick entry.");
                Assert.AreEqual(ConditionType.Frightened, firstTick.type);
                Assert.AreEqual(2, firstTick.oldValue);
                Assert.AreEqual(1, firstTick.newValue);
                Assert.IsFalse(firstTick.removed);
                Assert.AreEqual(1, endingData.GetConditionValue(ConditionType.Frightened));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = oldIgnore;
                Object.DestroyImmediate(turnManagerGo);
                Object.DestroyImmediate(entityManagerGo);
                Object.DestroyImmediate(eventBusGo);
            }
        }

        [Test]
        public void TurnManager_CheckVictory_EnemyWiped_IsVictory()
        {
            var turnManagerGo = new GameObject("TurnManager_Victory_Test");
            var entityManagerGo = new GameObject("EntityManager_Victory_Test");
            bool oldIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                var turnManager = turnManagerGo.AddComponent<TurnManager>();
                var entityManager = entityManagerGo.AddComponent<EntityManager>();
                var registry = new EntityRegistry();

                SetPrivateField(turnManager, "entityManager", entityManager);
                SetAutoPropertyBackingField(entityManager, "Registry", registry);

                registry.Register(CreateEntity(Team.Player, alive: true));
                registry.Register(CreateEntity(Team.Enemy, alive: false));

                EncounterResult received = EncounterResult.Unknown;
                turnManager.OnCombatEndedWithResult += e => received = e.result;

                bool ended = InvokeCheckVictory(turnManager);

                Assert.IsTrue(ended);
                Assert.AreEqual(EncounterResult.Victory, received);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = oldIgnore;
                Object.DestroyImmediate(turnManagerGo);
                Object.DestroyImmediate(entityManagerGo);
            }
        }

        [Test]
        public void TurnManager_CheckVictory_PlayerWiped_IsDefeat()
        {
            var turnManagerGo = new GameObject("TurnManager_Defeat_Test");
            var entityManagerGo = new GameObject("EntityManager_Defeat_Test");
            bool oldIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                var turnManager = turnManagerGo.AddComponent<TurnManager>();
                var entityManager = entityManagerGo.AddComponent<EntityManager>();
                var registry = new EntityRegistry();

                SetPrivateField(turnManager, "entityManager", entityManager);
                SetAutoPropertyBackingField(entityManager, "Registry", registry);

                registry.Register(CreateEntity(Team.Player, alive: false));
                registry.Register(CreateEntity(Team.Enemy, alive: true));

                EncounterResult received = EncounterResult.Unknown;
                turnManager.OnCombatEndedWithResult += e => received = e.result;

                bool ended = InvokeCheckVictory(turnManager);

                Assert.IsTrue(ended);
                Assert.AreEqual(EncounterResult.Defeat, received);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = oldIgnore;
                Object.DestroyImmediate(turnManagerGo);
                Object.DestroyImmediate(entityManagerGo);
            }
        }

        private static EntityData CreateEntity(Team team, bool alive)
        {
            return new EntityData
            {
                Name = team.ToString(),
                Team = team,
                MaxHP = 10,
                CurrentHP = alive ? 10 : 0,
                Speed = 25,
                Strength = 10,
                Dexterity = 10,
                Constitution = 10,
                Intelligence = 10,
                Wisdom = 10,
                Charisma = 10
            };
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

        private static bool InvokeCheckVictory(TurnManager turnManager)
        {
            var method = typeof(TurnManager).GetMethod("CheckVictory", InstanceNonPublic);
            Assert.IsNotNull(method, "TurnManager.CheckVictory() was not found.");
            return (bool)method.Invoke(turnManager, null);
        }
    }
}
