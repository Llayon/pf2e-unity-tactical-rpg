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
    public class TargetingControllerSpellModeTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ForceBarrage_AssigningFinalShard_RequiresExplicitConfirm()
        {
            using var ctx = new SpellTargetingContext();
            var actor = ctx.RegisterEntity("Wizard", Team.Player);
            var enemyA = ctx.RegisterEntity("Goblin_A", Team.Enemy);
            var enemyB = ctx.RegisterEntity("Goblin_B", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int confirmCalls = 0;
            IReadOnlyList<EntityHandle> confirmedTargets = null;
            ctx.Controller.BeginForceBarrageTargeting(
                shardCount: 2,
                onConfirmed: targets =>
                {
                    confirmCalls++;
                    confirmedTargets = new List<EntityHandle>(targets);
                    return true;
                });

            Assert.AreEqual(TargetingResult.Success, ctx.Controller.TryConfirmEntity(enemyA));
            Assert.AreEqual(TargetingMode.ForceBarrage, ctx.Controller.ActiveMode);
            Assert.AreEqual(1, ctx.Controller.ForceBarrageAssignedShardCount);
            Assert.AreEqual(0, confirmCalls);

            Assert.AreEqual(TargetingResult.Success, ctx.Controller.TryConfirmEntity(enemyB));
            Assert.AreEqual(TargetingMode.ForceBarrage, ctx.Controller.ActiveMode);
            Assert.AreEqual(2, ctx.Controller.ForceBarrageAssignedShardCount);
            Assert.IsTrue(ctx.Controller.CanConfirmSpellTargeting);
            Assert.AreEqual(0, confirmCalls);

            Assert.IsTrue(ctx.Controller.TryConfirmSpellTargeting());
            Assert.AreEqual(1, confirmCalls);
            CollectionAssert.AreEqual(new[] { enemyA, enemyB }, confirmedTargets);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void ElectricArc_TargetSelection_RequiresExplicitConfirm()
        {
            using var ctx = new SpellTargetingContext();
            var actor = ctx.RegisterEntity("Wizard", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int confirmCalls = 0;
            ctx.Controller.BeginElectricArcTargeting(
                onConfirmed: targets =>
                {
                    confirmCalls++;
                    return true;
                });

            Assert.AreEqual(TargetingResult.Success, ctx.Controller.TryConfirmEntity(enemy));
            Assert.AreEqual(TargetingMode.ElectricArc, ctx.Controller.ActiveMode);
            Assert.AreEqual(1, ctx.Controller.ElectricArcSelectedTargetCount);
            Assert.AreEqual(0, confirmCalls);

            Assert.IsTrue(ctx.Controller.TryConfirmSpellTargeting());
            Assert.AreEqual(1, confirmCalls);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void CancelTargeting_ForceBarrage_ClearsPendingAssignmentsWithoutConfirm()
        {
            using var ctx = new SpellTargetingContext();
            var actor = ctx.RegisterEntity("Wizard", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int confirmCalls = 0;
            int cancelCalls = 0;
            ctx.Controller.BeginForceBarrageTargeting(
                shardCount: 3,
                onConfirmed: _ =>
                {
                    confirmCalls++;
                    return true;
                },
                onCancelled: () => cancelCalls++);

            Assert.AreEqual(TargetingResult.Success, ctx.Controller.TryConfirmEntity(enemy));
            Assert.AreEqual(1, ctx.Controller.ForceBarrageAssignedShardCount);

            ctx.Controller.CancelTargeting();

            Assert.AreEqual(0, confirmCalls);
            Assert.AreEqual(1, cancelCalls);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
            Assert.AreEqual(0, ctx.Controller.ForceBarrageAssignedShardCount);
        }

        [Test]
        public void ForceBarrage_UndoLastSelection_RemovesMostRecentShard()
        {
            using var ctx = new SpellTargetingContext();
            var actor = ctx.RegisterEntity("Wizard", Team.Player);
            var enemyA = ctx.RegisterEntity("Goblin_A", Team.Enemy);
            var enemyB = ctx.RegisterEntity("Goblin_B", Team.Enemy);
            ctx.SetCurrentActor(actor);

            ctx.Controller.BeginForceBarrageTargeting(
                shardCount: 3,
                onConfirmed: _ => true);

            Assert.AreEqual(TargetingResult.Success, ctx.Controller.TryConfirmEntity(enemyA));
            Assert.AreEqual(TargetingResult.Success, ctx.Controller.TryConfirmEntity(enemyB));
            Assert.AreEqual(2, ctx.Controller.ForceBarrageAssignedShardCount);

            Assert.IsTrue(ctx.Controller.TryUndoLastSpellSelection());
            Assert.AreEqual(1, ctx.Controller.ForceBarrageAssignedShardCount);
            CollectionAssert.AreEqual(new[] { enemyA }, ctx.Controller.ForceBarrageAssignedTargets);
            Assert.IsTrue(ctx.Controller.CanConfirmSpellTargeting);
        }

        [Test]
        public void ForceBarrage_CanConfirmBeforeCapacity_WhenAtLeastOneShardAssigned()
        {
            using var ctx = new SpellTargetingContext();
            var actor = ctx.RegisterEntity("Wizard", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int confirmCalls = 0;
            IReadOnlyList<EntityHandle> confirmedTargets = null;
            ctx.Controller.BeginForceBarrageTargeting(
                shardCount: 3,
                onConfirmed: targets =>
                {
                    confirmCalls++;
                    confirmedTargets = new List<EntityHandle>(targets);
                    return true;
                });

            Assert.AreEqual(TargetingResult.Success, ctx.Controller.TryConfirmEntity(enemy));
            Assert.AreEqual(1, ctx.Controller.ForceBarrageAssignedShardCount);
            Assert.IsTrue(ctx.Controller.CanConfirmSpellTargeting);

            Assert.IsTrue(ctx.Controller.TryConfirmSpellTargeting());
            Assert.AreEqual(1, confirmCalls);
            CollectionAssert.AreEqual(new[] { enemy }, confirmedTargets);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void Snowball_TargetSelection_RequiresExplicitConfirm()
        {
            using var ctx = new SpellTargetingContext();
            var actor = ctx.RegisterEntity("Wizard", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int confirmCalls = 0;
            IReadOnlyList<EntityHandle> confirmedTargets = null;
            ctx.Controller.BeginSnowballTargeting(
                onConfirmed: targets =>
                {
                    confirmCalls++;
                    confirmedTargets = new List<EntityHandle>(targets);
                    return true;
                });

            Assert.AreEqual(TargetingResult.Success, ctx.Controller.TryConfirmEntity(enemy));
            Assert.AreEqual(TargetingMode.Snowball, ctx.Controller.ActiveMode);
            Assert.AreEqual(1, ctx.Controller.SnowballSelectedTargetCount);
            Assert.AreEqual(0, confirmCalls);

            Assert.IsTrue(ctx.Controller.TryConfirmSpellTargeting());
            Assert.AreEqual(1, confirmCalls);
            CollectionAssert.AreEqual(new[] { enemy }, confirmedTargets);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void BurningHands_CellSelection_RequiresExplicitConfirm()
        {
            using var ctx = new SpellTargetingContext();
            var actor = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(0, 0, 0));
            ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(0, 0, 2));
            ctx.SetCurrentActor(actor);

            int confirmCalls = 0;
            Vector3Int confirmedCell = default;
            ctx.Controller.BeginSpellAoETargeting(
                SpellId.BurningHands,
                cell =>
                {
                    confirmCalls++;
                    confirmedCell = cell;
                    return true;
                });

            var aimCell = new Vector3Int(0, 0, 1);
            Assert.AreEqual(TargetingResult.Success, ctx.Controller.TryConfirmCell(aimCell));
            Assert.AreEqual(TargetingMode.SpellAoE, ctx.Controller.ActiveMode);
            Assert.IsTrue(ctx.Controller.HasSelectedSpellAreaCell);
            Assert.AreEqual(aimCell, ctx.Controller.SelectedSpellAreaCell.Value);
            Assert.AreEqual(0, confirmCalls);

            Assert.IsTrue(ctx.Controller.TryConfirmSpellTargeting());
            Assert.AreEqual(1, confirmCalls);
            Assert.AreEqual(aimCell, confirmedCell);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        private sealed class SpellTargetingContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public TurnManager TurnManager { get; }
            public PlayerActionExecutor ActionExecutor { get; }
            public TargetingController Controller { get; }
            public EntityRegistry Registry { get; }

            public SpellTargetingContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("TargetingControllerSpellModeTests_Root");

                var eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                var turnManagerGo = new GameObject("TurnManager");
                turnManagerGo.transform.SetParent(root.transform);
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);
                SetPrivateField(TurnManager, "initiativeOrder", new List<InitiativeEntry>());
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);

                var executorGo = new GameObject("Executor");
                executorGo.transform.SetParent(root.transform);
                ActionExecutor = executorGo.AddComponent<PlayerActionExecutor>();
                SetPrivateField(ActionExecutor, "turnManager", TurnManager);
                SetPrivateField(ActionExecutor, "entityManager", EntityManager);
                SetPrivateField(ActionExecutor, "eventBus", EventBus);

                var targetingGo = new GameObject("TargetingController");
                targetingGo.transform.SetParent(root.transform);
                Controller = targetingGo.AddComponent<TargetingController>();
                SetPrivateField(Controller, "actionExecutor", ActionExecutor);
                SetPrivateField(Controller, "entityManager", EntityManager);
                SetPrivateField(Controller, "turnManager", TurnManager);
                SetPrivateField(Controller, "eventBus", EventBus);
            }

            public EntityHandle RegisterEntity(string name, Team team, Vector3Int gridPosition = default)
            {
                return Registry.Register(new EntityData
                {
                    Name = name,
                    Team = team,
                    Level = 1,
                    MaxHP = 10,
                    CurrentHP = 10,
                    Speed = 25,
                    Size = CreatureSize.Medium,
                    GridPosition = gridPosition
                });
            }

            public void SetCurrentActor(EntityHandle actor)
            {
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
