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
