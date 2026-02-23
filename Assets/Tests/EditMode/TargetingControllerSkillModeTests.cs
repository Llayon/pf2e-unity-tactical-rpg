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
    public class TargetingControllerSkillModeTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TripMode_EnemyClick_InvokesCallback_AndClearsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int calls = 0;
            EntityHandle confirmed = EntityHandle.None;
            ctx.Controller.BeginTargeting(TargetingMode.Trip, h => { calls++; confirmed = h; });

            var result = ctx.Controller.TryConfirmEntity(enemy);

            Assert.AreEqual(TargetingResult.Success, result);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(enemy, confirmed);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void DemoralizeMode_EnemyClick_InvokesCallback_AndClearsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int calls = 0;
            ctx.Controller.BeginTargeting(TargetingMode.Demoralize, _ => calls++);

            var result = ctx.Controller.TryConfirmEntity(enemy);

            Assert.AreEqual(TargetingResult.Success, result);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void ShoveMode_EnemyClick_InvokesCallback_AndClearsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int calls = 0;
            ctx.Controller.BeginTargeting(TargetingMode.Shove, _ => calls++);

            var result = ctx.Controller.TryConfirmEntity(enemy);

            Assert.AreEqual(TargetingResult.Success, result);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

[Test]
        public void GrappleMode_EnemyClick_InvokesCallback_AndClearsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int calls = 0;
            ctx.Controller.BeginTargeting(TargetingMode.Grapple, _ => calls++);

            var result = ctx.Controller.TryConfirmEntity(enemy);

            Assert.AreEqual(TargetingResult.Success, result);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void EscapeMode_EnemyClick_InvokesCallback_AndClearsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int calls = 0;
            ctx.Controller.BeginTargeting(TargetingMode.Escape, _ => calls++);

            var result = ctx.Controller.TryConfirmEntity(enemy);

            Assert.AreEqual(TargetingResult.Success, result);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }


        [Test]
        public void TripMode_AllyClick_ReturnsWrongTeam_DoesNotInvokeCallback_AndKeepsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var ally = ctx.RegisterEntity("Wizard", Team.Player);
            ctx.SetCurrentActor(actor);

            int calls = 0;
            ctx.Controller.BeginTargeting(TargetingMode.Trip, _ => calls++);

            var result = ctx.Controller.TryConfirmEntity(ally);

            Assert.AreEqual(TargetingResult.WrongTeam, result);
            Assert.AreEqual(0, calls);
            Assert.AreEqual(TargetingMode.Trip, ctx.Controller.ActiveMode);
        }

        [Test]
        public void CancelTargeting_ClearsTripShoveOrDemoralizeMode()
        {
            using var ctx = new TargetingSkillModeContext();

            ctx.Controller.BeginTargeting(TargetingMode.Trip);
            ctx.Controller.CancelTargeting();
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);

                        ctx.Controller.BeginTargeting(TargetingMode.Shove);
            ctx.Controller.CancelTargeting();
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);

            ctx.Controller.BeginTargeting(TargetingMode.Grapple);
            ctx.Controller.CancelTargeting();
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);

            ctx.Controller.BeginTargeting(TargetingMode.Escape);
            ctx.Controller.CancelTargeting();
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);

            ctx.Controller.BeginTargeting(TargetingMode.Demoralize);
            ctx.Controller.CancelTargeting();
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        private sealed class TargetingSkillModeContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject turnManagerGo;
            private readonly GameObject targetingGo;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public TurnManager TurnManager { get; }
            public TargetingController Controller { get; }
            public EntityRegistry Registry { get; }

            public TargetingSkillModeContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("TargetingSkillModeTests_Root");

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

                targetingGo = new GameObject("TargetingController");
                targetingGo.transform.SetParent(root.transform);
                targetingGo.SetActive(false);
                Controller = targetingGo.AddComponent<TargetingController>();
                SetPrivateField(Controller, "entityManager", EntityManager);
                SetPrivateField(Controller, "turnManager", TurnManager);
                SetPrivateField(Controller, "eventBus", EventBus);
            }

            public EntityHandle RegisterEntity(string name, Team team)
            {
                return Registry.Register(new EntityData
                {
                    Name = name,
                    Team = team,
                    Level = 1,
                    MaxHP = 10,
                    CurrentHP = 10,
                    Speed = 25,
                    Size = CreatureSize.Medium
                });
            }

            public void SetCurrentActor(EntityHandle actor)
            {
                var order = new List<InitiativeEntry>
                {
                    new InitiativeEntry
                    {
                        Handle = actor,
                        Roll = 10,
                        Modifier = 0,
                        IsPlayer = true
                    }
                };

                SetPrivateField(TurnManager, "initiativeOrder", order);
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
            }

            public void Dispose()
            {
                if (root != null) Object.DestroyImmediate(root);
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
