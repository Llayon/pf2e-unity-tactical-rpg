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
    public class PlayerActionExecutorAidTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryExecuteStrike_PreparedAidConsumed_AppliesAidBonusInStrikePayload()
        {
            using var ctx = new ExecutorAidContext();

            var helper = ctx.RegisterEntity("Helper", Team.Player, new Vector3Int(0, 0, 1), strength: 5000);
            var striker = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 16);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);
            ctx.SetCurrentActor(striker, actionsRemaining: 3);

            var helperData = ctx.Registry.Get(helper);
            Assert.IsNotNull(helperData);
            helperData.ReactionAvailable = true;
            helperData.SimpleWeaponProf = ProficiencyRank.Legendary;

            ctx.TurnManager.AidService.NotifyTurnStarted(helper);
            Assert.IsTrue(ctx.TurnManager.AidService.PrepareAid(helper, striker, preparedRound: 1));
            Assert.IsTrue(ctx.TurnManager.AidService.HasPreparedAidForAlly(striker));

            int strikeEvents = 0;
            StrikeResolvedEvent lastStrike = default;
            ctx.EventBus.OnStrikeResolved += HandleStrikeResolved;
            try
            {
                bool executed = ctx.Executor.TryExecuteStrike(target);

                Assert.IsTrue(executed);
                Assert.AreEqual(1, strikeEvents);
                Assert.Greater(lastStrike.aidCircumstanceBonus, 0, "Prepared Aid should contribute positive strike modifier in this setup.");
                Assert.IsFalse(helperData.ReactionAvailable, "Aid reaction should be consumed.");
                Assert.IsFalse(ctx.TurnManager.AidService.HasPreparedAidForAlly(striker), "Prepared Aid should be consumed on strike check.");
            }
            finally
            {
                ctx.EventBus.OnStrikeResolved -= HandleStrikeResolved;
            }

            void HandleStrikeResolved(in StrikeResolvedEvent e)
            {
                strikeEvents++;
                lastStrike = e;
            }
        }

        private sealed class ExecutorAidContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject turnManagerGo;
            private readonly GameObject strikeActionGo;
            private readonly GameObject executorGo;

            public readonly CombatEventBus EventBus;
            public readonly EntityManager EntityManager;
            public readonly TurnManager TurnManager;
            public readonly StrikeAction StrikeAction;
            public readonly PlayerActionExecutor Executor;
            public readonly EntityRegistry Registry;

            public ExecutorAidContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("PlayerActionExecutorAidTests_Root");

                eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                turnManagerGo = new GameObject("TurnManager");
                turnManagerGo.transform.SetParent(root.transform);
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
                SetPrivateField(TurnManager, "initiativeOrder", new List<InitiativeEntry>());
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "roundNumber", 1);

                strikeActionGo = new GameObject("StrikeAction");
                strikeActionGo.transform.SetParent(root.transform);
                StrikeAction = strikeActionGo.AddComponent<StrikeAction>();
                SetPrivateField(StrikeAction, "entityManager", EntityManager);
                SetPrivateField(StrikeAction, "eventBus", EventBus);

                executorGo = new GameObject("Executor");
                executorGo.transform.SetParent(root.transform);
                Executor = executorGo.AddComponent<PlayerActionExecutor>();
                SetPrivateField(Executor, "turnManager", TurnManager);
                SetPrivateField(Executor, "entityManager", EntityManager);
                SetPrivateField(Executor, "eventBus", EventBus);
                SetPrivateField(Executor, "strikeAction", StrikeAction);
            }

            public EntityHandle RegisterEntity(string name, Team team, Vector3Int gridPosition, int strength)
            {
                var data = new EntityData
                {
                    Name = name,
                    Team = team,
                    Level = 1,
                    Strength = strength,
                    Dexterity = 10,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    MaxHP = 30,
                    CurrentHP = 30,
                    GridPosition = gridPosition,
                    ActionsRemaining = 3
                };

                return Registry.Register(data);
            }

            public void SetCurrentActor(EntityHandle actor, int actionsRemaining)
            {
                var actorData = Registry.Get(actor);
                Assert.IsNotNull(actorData);
                actorData.ActionsRemaining = actionsRemaining;

                SetPrivateField(TurnManager, "initiativeOrder", new List<InitiativeEntry>
                {
                    new InitiativeEntry
                    {
                        Handle = actor,
                        Roll = new CheckRoll(10, 0, CheckSource.Perception()),
                        IsPlayer = true
                    }
                });
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
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
