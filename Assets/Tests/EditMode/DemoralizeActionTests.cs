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
    public class DemoralizeActionTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryDemoralize_CritSuccess_AppliesFrightened2()
        {
            using var ctx = new DemoralizeContext();
            var actor = ctx.RegisterDefaultActor();
            var target = ctx.RegisterDefaultTarget();

            var degree = ctx.Action.TryDemoralize(actor, target, new FixedRng(new[] { 20 }));

            Assert.AreEqual(DegreeOfSuccess.CriticalSuccess, degree);
            Assert.AreEqual(2, ctx.Registry.Get(target).GetConditionValue(ConditionType.Frightened));
        }

        [Test]
        public void TryDemoralize_Success_AppliesFrightened1()
        {
            using var ctx = new DemoralizeContext();
            var actor = ctx.RegisterDefaultActor();
            var target = ctx.RegisterDefaultTarget();

            var degree = ctx.Action.TryDemoralize(actor, target, new FixedRng(new[] { 13 }));

            Assert.AreEqual(DegreeOfSuccess.Success, degree);
            Assert.AreEqual(1, ctx.Registry.Get(target).GetConditionValue(ConditionType.Frightened));
        }

        [Test]
        public void TryDemoralize_Failure_NoConditionApplied()
        {
            using var ctx = new DemoralizeContext();
            var actor = ctx.RegisterDefaultActor();
            var target = ctx.RegisterDefaultTarget();

            var degree = ctx.Action.TryDemoralize(actor, target, new FixedRng(new[] { 8 }));

            Assert.AreEqual(DegreeOfSuccess.Failure, degree);
            Assert.IsFalse(ctx.Registry.Get(target).HasCondition(ConditionType.Frightened));
        }

        [Test]
        public void TryDemoralize_CritFailure_NoConditionApplied_MvpNoImmunity()
        {
            using var ctx = new DemoralizeContext();
            var actor = ctx.RegisterDefaultActor();
            var target = ctx.RegisterDefaultTarget();

            var degree = ctx.Action.TryDemoralize(actor, target, new FixedRng(new[] { 3 }));

            Assert.AreEqual(DegreeOfSuccess.CriticalFailure, degree);
            Assert.IsFalse(ctx.Registry.Get(target).HasCondition(ConditionType.Frightened));
        }

        [Test]
        public void TryDemoralize_UsesWillDC()
        {
            using var ctx = new DemoralizeContext();
            var actor = ctx.RegisterDefaultActor();
            var target = ctx.RegisterTarget(wisdom: 14, willProf: ProficiencyRank.Trained);
            int expectedDc = ctx.Registry.Get(target).GetSaveDC(SaveType.Will);

            SkillCheckResolvedEvent last = default;
            int count = 0;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnSkillCheckResolved;
            try
            {
                var degree = ctx.Action.TryDemoralize(actor, target, new FixedRng(new[] { 13 }));
                Assert.IsTrue(degree.HasValue);
                Assert.AreEqual(1, count);
                Assert.AreEqual(expectedDc, last.dc);
            }
            finally
            {
                ctx.EventBus.OnSkillCheckResolvedTyped -= OnSkillCheckResolved;
            }

            void OnSkillCheckResolved(in SkillCheckResolvedEvent e)
            {
                count++;
                last = e;
            }
        }

        [Test]
        public void TryDemoralize_UntrainedIntimidation_IsAllowed()
        {
            using var ctx = new DemoralizeContext();
            var actor = ctx.RegisterActor(charisma: 10, intimidationProf: ProficiencyRank.Untrained);
            var target = ctx.RegisterDefaultTarget();

            var degree = ctx.Action.TryDemoralize(actor, target, new FixedRng(new[] { 13 }));

            Assert.IsTrue(degree.HasValue);
        }

        [Test]
        public void TryDemoralize_DoesNotIncrementMAP()
        {
            using var ctx = new DemoralizeContext();
            var actor = ctx.RegisterDefaultActor();
            var target = ctx.RegisterDefaultTarget();
            var actorData = ctx.Registry.Get(actor);
            actorData.MAPCount = 1;

            int baseModifier = actorData.GetSkillModifier(SkillType.Intimidation);
            SkillCheckResolvedEvent last = default;
            int count = 0;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnSkillCheckResolved;
            try
            {
                var degree = ctx.Action.TryDemoralize(actor, target, new FixedRng(new[] { 13 }));
                Assert.IsTrue(degree.HasValue);
                Assert.AreEqual(1, actorData.MAPCount, "Demoralize must not increment MAPCount.");
                Assert.AreEqual(1, count);
                Assert.AreEqual(baseModifier, last.modifier, "Demoralize must not apply MAP to check modifier.");
            }
            finally
            {
                ctx.EventBus.OnSkillCheckResolvedTyped -= OnSkillCheckResolved;
            }

            void OnSkillCheckResolved(in SkillCheckResolvedEvent e)
            {
                count++;
                last = e;
            }
        }

        [Test]
        public void TryDemoralize_30ftRange_IsAllowed()
        {
            using var ctx = new DemoralizeContext();
            var actor = ctx.RegisterDefaultActor(gridPosition: new Vector3Int(0, 0, 0));
            var target = ctx.RegisterDefaultTarget(gridPosition: new Vector3Int(6, 0, 0)); // 30 ft

            bool canDemoralize = ctx.Action.CanDemoralize(actor, target);

            Assert.IsTrue(canDemoralize);
        }

        [Test]
        public void TryDemoralize_Beyond30ft_ReturnsNull()
        {
            using var ctx = new DemoralizeContext();
            var actor = ctx.RegisterDefaultActor(gridPosition: new Vector3Int(0, 0, 0));
            var target = ctx.RegisterDefaultTarget(gridPosition: new Vector3Int(7, 0, 0)); // 35 ft

            var degree = ctx.Action.TryDemoralize(actor, target, new FixedRng(new[] { 20 }));

            Assert.IsFalse(degree.HasValue);
        }

        [Test]
        public void TryDemoralize_SameTeam_ReturnsNull()
        {
            using var ctx = new DemoralizeContext();
            var actor = ctx.RegisterDefaultActor(team: Team.Player);
            var target = ctx.RegisterDefaultTarget(team: Team.Player);

            var degree = ctx.Action.TryDemoralize(actor, target, new FixedRng(new[] { 20 }));

            Assert.IsFalse(degree.HasValue);
        }

        [Test]
        public void TryDemoralize_PublishesSkillCheckResolvedEvent()
        {
            using var ctx = new DemoralizeContext();
            var actor = ctx.RegisterDefaultActor();
            var target = ctx.RegisterDefaultTarget();

            int count = 0;
            SkillCheckResolvedEvent last = default;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnSkillCheckResolved;
            try
            {
                var degree = ctx.Action.TryDemoralize(actor, target, new FixedRng(new[] { 13 }));
                Assert.IsTrue(degree.HasValue);
                Assert.AreEqual(1, count);
                Assert.AreEqual(actor, last.actor);
                Assert.AreEqual(target, last.target);
                Assert.AreEqual(SkillType.Intimidation, last.skill);
                Assert.AreEqual("Demoralize", last.actionName);
                Assert.IsTrue(last.hasOpposedProjection);
                Assert.AreEqual(last.total - last.dc, last.opposedProjection.margin);
                Assert.AreEqual(CheckSourceType.Save, last.opposedProjection.defenderRoll.source.type);
                Assert.AreEqual(SaveType.Will, last.opposedProjection.defenderRoll.source.save);
            }
            finally
            {
                ctx.EventBus.OnSkillCheckResolvedTyped -= OnSkillCheckResolved;
            }

            void OnSkillCheckResolved(in SkillCheckResolvedEvent e)
            {
                count++;
                last = e;
            }
        }

        [Test]
        public void TryDemoralize_TargetAlreadyFrightened1_CritSuccess_RefreshesTo2()
        {
            using var ctx = new DemoralizeContext();
            var actor = ctx.RegisterDefaultActor();
            var target = ctx.RegisterDefaultTarget();
            ctx.Registry.Get(target).Conditions.Add(new ActiveCondition(ConditionType.Frightened, value: 1));

            var degree = ctx.Action.TryDemoralize(actor, target, new FixedRng(new[] { 20 }));

            Assert.AreEqual(DegreeOfSuccess.CriticalSuccess, degree);
            Assert.AreEqual(2, ctx.Registry.Get(target).GetConditionValue(ConditionType.Frightened));
        }

        [Test]
        public void PlayerActionExecutor_TryExecuteDemoralize_InvalidAttempt_RollsBackExecutingAction()
        {
            using var ctx = new ExecutorDemoralizeContext();

            int actionsBefore = ctx.TurnManager.ActionsRemaining;

            bool started = ctx.Executor.TryExecuteDemoralize(ctx.EnemyOutOfRange);

            Assert.IsFalse(started);
            Assert.AreEqual(TurnState.PlayerTurn, ctx.TurnManager.State);
            Assert.AreEqual(actionsBefore, ctx.TurnManager.ActionsRemaining);
            Assert.AreEqual(EntityHandle.None, ctx.TurnManager.ExecutingActor);
        }

        private sealed class DemoralizeContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject actionGo;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public DemoralizeAction Action { get; }
            public EntityRegistry Registry => EntityManager.Registry;

            public DemoralizeContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("Demoralize_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("Demoralize_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                actionGo = new GameObject("DemoralizeAction_Test");
                Action = actionGo.AddComponent<DemoralizeAction>();
                SetPrivateField(Action, "entityManager", EntityManager);
                SetPrivateField(Action, "eventBus", EventBus);
            }

            public EntityHandle RegisterDefaultActor(Team team = Team.Player, Vector3Int? gridPosition = null)
            {
                return RegisterActor(charisma: 10, intimidationProf: ProficiencyRank.Untrained, team: team, gridPosition: gridPosition);
            }

            public EntityHandle RegisterActor(
                int charisma,
                ProficiencyRank intimidationProf,
                Team team = Team.Player,
                Vector3Int? gridPosition = null)
            {
                return Registry.Register(new EntityData
                {
                    Name = "Actor",
                    Team = team,
                    Size = CreatureSize.Medium,
                    Level = 1,
                    MaxHP = 20,
                    CurrentHP = 20,
                    Speed = 25,
                    GridPosition = gridPosition ?? new Vector3Int(0, 0, 0),
                    Strength = 10,
                    Dexterity = 10,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = charisma,
                    IntimidationProf = intimidationProf
                });
            }

            public EntityHandle RegisterDefaultTarget(Team team = Team.Enemy, Vector3Int? gridPosition = null)
            {
                return RegisterTarget(wisdom: 10, willProf: ProficiencyRank.Trained, team: team, gridPosition: gridPosition);
            }

            public EntityHandle RegisterTarget(
                int wisdom,
                ProficiencyRank willProf,
                Team team = Team.Enemy,
                Vector3Int? gridPosition = null)
            {
                return Registry.Register(new EntityData
                {
                    Name = "Target",
                    Team = team,
                    Size = CreatureSize.Medium,
                    Level = 1,
                    MaxHP = 20,
                    CurrentHP = 20,
                    Speed = 25,
                    GridPosition = gridPosition ?? new Vector3Int(1, 0, 0),
                    Strength = 10,
                    Dexterity = 10,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = wisdom,
                    Charisma = 10,
                    WillProf = willProf
                });
            }

            public void Dispose()
            {
                if (actionGo != null) Object.DestroyImmediate(actionGo);
                if (entityManagerGo != null) Object.DestroyImmediate(entityManagerGo);
                if (eventBusGo != null) Object.DestroyImmediate(eventBusGo);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private sealed class ExecutorDemoralizeContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject turnManagerGo;
            private readonly GameObject demoralizeActionGo;
            private readonly GameObject executorGo;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public TurnManager TurnManager { get; }
            public DemoralizeAction DemoralizeAction { get; }
            public PlayerActionExecutor Executor { get; }
            public EntityRegistry Registry => EntityManager.Registry;
            public EntityHandle Player { get; }
            public EntityHandle EnemyOutOfRange { get; }

            public ExecutorDemoralizeContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("ExecutorDemoralize_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("ExecutorDemoralize_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                turnManagerGo = new GameObject("ExecutorDemoralize_TurnManager_Test");
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);

                demoralizeActionGo = new GameObject("DemoralizeAction_Test");
                DemoralizeAction = demoralizeActionGo.AddComponent<DemoralizeAction>();
                SetPrivateField(DemoralizeAction, "entityManager", EntityManager);
                SetPrivateField(DemoralizeAction, "eventBus", EventBus);

                executorGo = new GameObject("PlayerExecutor_Demoralize_Test");
                Executor = executorGo.AddComponent<PlayerActionExecutor>();
                SetPrivateField(Executor, "turnManager", TurnManager);
                SetPrivateField(Executor, "entityManager", EntityManager);
                SetPrivateField(Executor, "demoralizeAction", DemoralizeAction);

                Player = Registry.Register(new EntityData
                {
                    Name = "Player",
                    Team = Team.Player,
                    Size = CreatureSize.Medium,
                    Level = 1,
                    MaxHP = 20,
                    CurrentHP = 20,
                    Speed = 25,
                    GridPosition = new Vector3Int(0, 0, 0),
                    Strength = 10,
                    Dexterity = 10,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    ActionsRemaining = 3
                });

                EnemyOutOfRange = Registry.Register(new EntityData
                {
                    Name = "Enemy",
                    Team = Team.Enemy,
                    Size = CreatureSize.Medium,
                    Level = 1,
                    MaxHP = 20,
                    CurrentHP = 20,
                    Speed = 25,
                    GridPosition = new Vector3Int(7, 0, 0), // 35ft
                    Strength = 10,
                    Dexterity = 10,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    ActionsRemaining = 3
                });

                SetPrivateField(TurnManager, "initiativeOrder", new List<InitiativeEntry>
                {
                    new InitiativeEntry { Handle = Player, Roll = new CheckRoll(20, 5, CheckSource.Perception()), IsPlayer = true },
                    new InitiativeEntry { Handle = EnemyOutOfRange, Roll = new CheckRoll(1, -1, CheckSource.Perception()), IsPlayer = false }
                });
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
            }

            public void Dispose()
            {
                if (executorGo != null) Object.DestroyImmediate(executorGo);
                if (demoralizeActionGo != null) Object.DestroyImmediate(demoralizeActionGo);
                if (turnManagerGo != null) Object.DestroyImmediate(turnManagerGo);
                if (entityManagerGo != null) Object.DestroyImmediate(entityManagerGo);
                if (eventBusGo != null) Object.DestroyImmediate(eventBusGo);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private sealed class FixedRng : IRng
        {
            private readonly Queue<int> d20;
            private readonly Queue<int> dice = new();

            public FixedRng(IEnumerable<int> d20Rolls)
            {
                d20 = d20Rolls != null ? new Queue<int>(d20Rolls) : new Queue<int>();
            }

            public int RollD20() => d20.Count > 0 ? d20.Dequeue() : 10;

            public int RollDie(int sides)
            {
                if (sides <= 0) return 0;
                int value = dice.Count > 0 ? dice.Dequeue() : 1;
                return Mathf.Clamp(value, 1, sides);
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
