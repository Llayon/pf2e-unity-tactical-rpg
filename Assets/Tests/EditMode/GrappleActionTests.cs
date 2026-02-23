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
    public class GrappleActionTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryGrapple_CritSuccess_AppliesRestrained()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon);

            var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.AreEqual(DegreeOfSuccess.CriticalSuccess, degree);
            Assert.IsTrue(ctx.Registry.Get(target).HasCondition(ConditionType.Restrained));
            Assert.IsFalse(ctx.Registry.Get(target).HasCondition(ConditionType.Grabbed));
        }

        [Test]
        public void TryGrapple_Success_AppliesGrabbed()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon);

            var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 13 }));

            Assert.AreEqual(DegreeOfSuccess.Success, degree);
            Assert.IsTrue(ctx.Registry.Get(target).HasCondition(ConditionType.Grabbed));
            Assert.IsFalse(ctx.Registry.Get(target).HasCondition(ConditionType.Restrained));
        }

        [Test]
        public void TryGrapple_Failure_NoConditionApplied_MvpNoReleaseLogic()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon);

            var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 8 }));

            Assert.AreEqual(DegreeOfSuccess.Failure, degree);
            Assert.IsFalse(ctx.Registry.Get(target).HasCondition(ConditionType.Grabbed));
            Assert.IsFalse(ctx.Registry.Get(target).HasCondition(ConditionType.Restrained));
        }

        [Test]
        public void TryGrapple_CritFailure_ActorFallsProne_MvpSimplification()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon);

            var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 3 }));

            Assert.AreEqual(DegreeOfSuccess.CriticalFailure, degree);
            Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
        }

        [Test]
        public void TryGrapple_CritSuccess_RemovesGrabbed_WhenApplyingRestrained()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon);
            ctx.Registry.Get(target).Conditions.Add(new ActiveCondition(ConditionType.Grabbed));

            var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.AreEqual(DegreeOfSuccess.CriticalSuccess, degree);
            Assert.IsFalse(ctx.Registry.Get(target).HasCondition(ConditionType.Grabbed));
            Assert.IsTrue(ctx.Registry.Get(target).HasCondition(ConditionType.Restrained));
        }

        [Test]
        public void TryGrapple_IncrementsMAP()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon);

            Assert.AreEqual(0, ctx.Registry.Get(actor).MAPCount);
            var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 13 }));

            Assert.IsTrue(degree.HasValue);
            Assert.AreEqual(1, ctx.Registry.Get(actor).MAPCount);
        }

        [Test]
        public void TryGrapple_MAPAppliedToCheck()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon);
            ctx.Registry.Get(actor).MAPCount = 1;

            int count = 0;
            SkillCheckResolvedEvent last = default;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnSkillCheckResolved;
            try
            {
                var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 20 }));
                Assert.IsTrue(degree.HasValue);
                Assert.AreEqual(1, count);
                Assert.AreEqual(-5, last.modifier);
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
        public void TryGrapple_UntrainedAthletics_IsAllowed()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon, athleticsProf: ProficiencyRank.Untrained);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon);

            var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 13 }));

            Assert.IsTrue(degree.HasValue);
        }

        [Test]
        public void TryGrapple_UsesFortitudeDC()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon, fortitudeProf: ProficiencyRank.Expert, constitution: 16);

            SkillCheckResolvedEvent last = default;
            int count = 0;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnSkillCheckResolved;
            try
            {
                var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 13 }));
                Assert.IsTrue(degree.HasValue);
                Assert.AreEqual(1, count);
                Assert.AreEqual(ctx.Registry.Get(target).GetSaveDC(SaveType.Fortitude), last.dc);
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
        public void TryGrapple_OutOfRange_ReturnsNull()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple, reachFeet: 5);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon, gridPosition: new Vector3Int(0, 0, 0));
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon, gridPosition: new Vector3Int(2, 0, 0));

            var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.IsFalse(degree.HasValue);
        }

        [Test]
        public void TryGrapple_SameTeam_ReturnsNull()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon, team: Team.Player);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon, team: Team.Player);

            var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.IsFalse(degree.HasValue);
        }

        [Test]
        public void TryGrapple_TargetMoreThanOneSizeLarger_ReturnsNull()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon, size: CreatureSize.Medium);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon, size: CreatureSize.Huge);

            var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.IsFalse(degree.HasValue);
        }

        [Test]
        public void TryGrapple_TargetOneSizeLarger_IsAllowed()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon, size: CreatureSize.Medium);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon, size: CreatureSize.Large);

            Assert.IsTrue(ctx.Action.CanGrapple(actor, target));
        }

        [Test]
        public void TryGrapple_WeaponWithGrappleTrait_IsAllowed()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon);

            Assert.IsTrue(ctx.Action.CanGrapple(actor, target));
        }

        [Test]
        public void TryGrapple_WeaponWithoutGrappleTrait_ReturnsNull()
        {
            using var ctx = new GrappleContext();
            var normalWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.None);
            var actor = ctx.RegisterDefaultGrappleActor(normalWeapon);
            var target = ctx.RegisterDefaultGrappleTarget(normalWeapon);

            var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.IsFalse(degree.HasValue);
        }

        [Test]
        public void TryGrapple_PublishesSkillCheckResolvedEvent()
        {
            using var ctx = new GrappleContext();
            var grappleWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Grapple);
            var actor = ctx.RegisterDefaultGrappleActor(grappleWeapon);
            var target = ctx.RegisterDefaultGrappleTarget(grappleWeapon);

            int count = 0;
            SkillCheckResolvedEvent last = default;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnSkillCheckResolved;
            try
            {
                var degree = ctx.Action.TryGrapple(actor, target, new FixedRng(d20Rolls: new[] { 13 }));
                Assert.IsTrue(degree.HasValue);
                Assert.AreEqual(1, count);
                Assert.AreEqual(actor, last.actor);
                Assert.AreEqual(target, last.target);
                Assert.AreEqual(SkillType.Athletics, last.skill);
                Assert.AreEqual("Grapple", last.actionName);
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
        public void PlayerActionExecutor_TryExecuteGrapple_InvalidAttempt_RollsBackExecutingAction()
        {
            using var ctx = new ExecutorGrappleContext();

            int actionsBefore = ctx.TurnManager.ActionsRemaining;
            bool started = ctx.Executor.TryExecuteGrapple(ctx.Enemy);

            Assert.IsFalse(started);
            Assert.AreEqual(TurnState.PlayerTurn, ctx.TurnManager.State);
            Assert.AreEqual(actionsBefore, ctx.TurnManager.ActionsRemaining);
            Assert.AreEqual(EntityHandle.None, ctx.TurnManager.ExecutingActor);
        }

        private sealed class GrappleContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly List<WeaponDefinition> weaponDefs = new();
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject actionGo;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public GrappleAction Action { get; }
            public EntityRegistry Registry { get; }

            public GrappleContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("Grapple_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("Grapple_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                actionGo = new GameObject("GrappleAction_Test");
                Action = actionGo.AddComponent<GrappleAction>();
                SetPrivateField(Action, "entityManager", EntityManager);
                SetPrivateField(Action, "eventBus", EventBus);
            }

            public WeaponDefinition CreateWeaponDef(
                WeaponTraitFlags traits,
                int reachFeet = 5,
                bool isRanged = false)
            {
                var def = ScriptableObject.CreateInstance<WeaponDefinition>();
                def.itemName = "Test Weapon";
                def.category = WeaponCategory.Simple;
                def.group = WeaponGroup.Club;
                def.hands = WeaponHands.One;
                def.diceCount = 1;
                def.dieSides = 6;
                def.damageType = DamageType.Bludgeoning;
                def.reachFeet = reachFeet;
                def.isRanged = isRanged;
                def.traits = traits;
                weaponDefs.Add(def);
                return def;
            }

            public EntityHandle RegisterDefaultGrappleActor(
                WeaponDefinition weaponDef,
                Team team = Team.Player,
                Vector3Int? gridPosition = null,
                CreatureSize size = CreatureSize.Medium,
                int currentHp = 20,
                ProficiencyRank athleticsProf = ProficiencyRank.Untrained)
            {
                return RegisterEntity(
                    team,
                    gridPosition ?? new Vector3Int(0, 0, 0),
                    weaponDef,
                    level: 1,
                    size: size,
                    strength: 10,
                    dexterity: 10,
                    constitution: 10,
                    currentHp: currentHp,
                    athleticsProf: athleticsProf,
                    fortitudeProf: ProficiencyRank.Trained);
            }

            public EntityHandle RegisterDefaultGrappleTarget(
                WeaponDefinition weaponDef,
                Team team = Team.Enemy,
                Vector3Int? gridPosition = null,
                CreatureSize size = CreatureSize.Medium,
                int currentHp = 20,
                int constitution = 10,
                ProficiencyRank fortitudeProf = ProficiencyRank.Trained)
            {
                return RegisterEntity(
                    team,
                    gridPosition ?? new Vector3Int(1, 0, 0),
                    weaponDef,
                    level: 1,
                    size: size,
                    strength: 10,
                    dexterity: 10,
                    constitution: constitution,
                    currentHp: currentHp,
                    athleticsProf: ProficiencyRank.Untrained,
                    fortitudeProf: fortitudeProf);
            }

            public EntityHandle RegisterEntity(
                Team team,
                Vector3Int gridPosition,
                WeaponDefinition weaponDef,
                int level,
                CreatureSize size,
                int strength,
                int dexterity,
                int constitution,
                int currentHp,
                ProficiencyRank athleticsProf,
                ProficiencyRank fortitudeProf)
            {
                var data = new EntityData
                {
                    Name = $"{team}_{Registry.Count + 1}",
                    Team = team,
                    Size = size,
                    Level = level,
                    MaxHP = currentHp,
                    CurrentHP = currentHp,
                    Speed = 25,
                    GridPosition = gridPosition,
                    Strength = strength,
                    Dexterity = dexterity,
                    Constitution = constitution,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    AthleticsProf = athleticsProf,
                    FortitudeProf = fortitudeProf,
                    EquippedWeapon = new WeaponInstance
                    {
                        def = weaponDef,
                        potencyBonus = 0,
                        strikingRank = StrikingRuneRank.None
                    }
                };

                return Registry.Register(data);
            }

            public void Dispose()
            {
                for (int i = 0; i < weaponDefs.Count; i++)
                {
                    if (weaponDefs[i] != null)
                        Object.DestroyImmediate(weaponDefs[i]);
                }

                if (actionGo != null) Object.DestroyImmediate(actionGo);
                if (entityManagerGo != null) Object.DestroyImmediate(entityManagerGo);
                if (eventBusGo != null) Object.DestroyImmediate(eventBusGo);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private sealed class ExecutorGrappleContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject turnManagerGo;
            private readonly GameObject grappleActionGo;
            private readonly GameObject executorGo;
            private readonly WeaponDefinition grappleWeaponDef;
            private readonly WeaponDefinition nonGrappleWeaponDef;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public TurnManager TurnManager { get; }
            public GrappleAction GrappleAction { get; }
            public PlayerActionExecutor Executor { get; }
            public EntityRegistry Registry => EntityManager.Registry;
            public EntityHandle Player { get; }
            public EntityHandle Enemy { get; }

            public ExecutorGrappleContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("ExecutorGrapple_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("ExecutorGrapple_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                turnManagerGo = new GameObject("ExecutorGrapple_TurnManager_Test");
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);

                grappleActionGo = new GameObject("ExecutorGrapple_GrappleAction_Test");
                GrappleAction = grappleActionGo.AddComponent<GrappleAction>();
                SetPrivateField(GrappleAction, "entityManager", EntityManager);
                SetPrivateField(GrappleAction, "eventBus", EventBus);

                executorGo = new GameObject("ExecutorGrapple_PlayerExecutor_Test");
                Executor = executorGo.AddComponent<PlayerActionExecutor>();
                SetPrivateField(Executor, "turnManager", TurnManager);
                SetPrivateField(Executor, "entityManager", EntityManager);
                SetPrivateField(Executor, "grappleAction", GrappleAction);

                grappleWeaponDef = CreateWeaponDef("Grapple Weapon", WeaponTraitFlags.Grapple);
                nonGrappleWeaponDef = CreateWeaponDef("Normal Weapon", WeaponTraitFlags.None);

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
                    ActionsRemaining = 3,
                    EquippedWeapon = new WeaponInstance { def = nonGrappleWeaponDef, strikingRank = StrikingRuneRank.None }
                });

                Enemy = Registry.Register(new EntityData
                {
                    Name = "Enemy",
                    Team = Team.Enemy,
                    Size = CreatureSize.Medium,
                    Level = 1,
                    MaxHP = 20,
                    CurrentHP = 20,
                    Speed = 25,
                    GridPosition = new Vector3Int(1, 0, 0),
                    Strength = 10,
                    Dexterity = 10,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    ActionsRemaining = 3,
                    EquippedWeapon = new WeaponInstance { def = grappleWeaponDef, strikingRank = StrikingRuneRank.None }
                });

                var playerData = Registry.Get(Player);
                playerData.ActionsRemaining = 3;
                playerData.MAPCount = 0;
                playerData.ReactionAvailable = true;

                var enemyData = Registry.Get(Enemy);
                enemyData.ActionsRemaining = 3;
                enemyData.MAPCount = 0;
                enemyData.ReactionAvailable = true;

                SetPrivateField(TurnManager, "initiativeOrder", new List<InitiativeEntry>
                {
                    new InitiativeEntry { Handle = Player, Roll = 20, Modifier = 5, IsPlayer = true },
                    new InitiativeEntry { Handle = Enemy, Roll = 1, Modifier = -1, IsPlayer = false }
                });
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
            }

            private static WeaponDefinition CreateWeaponDef(string name, WeaponTraitFlags traits)
            {
                var def = ScriptableObject.CreateInstance<WeaponDefinition>();
                def.itemName = name;
                def.category = WeaponCategory.Simple;
                def.group = WeaponGroup.Club;
                def.hands = WeaponHands.One;
                def.diceCount = 1;
                def.dieSides = 6;
                def.damageType = DamageType.Bludgeoning;
                def.reachFeet = 5;
                def.isRanged = false;
                def.traits = traits;
                return def;
            }

            public void Dispose()
            {
                if (grappleWeaponDef != null) Object.DestroyImmediate(grappleWeaponDef);
                if (nonGrappleWeaponDef != null) Object.DestroyImmediate(nonGrappleWeaponDef);
                if (executorGo != null) Object.DestroyImmediate(executorGo);
                if (grappleActionGo != null) Object.DestroyImmediate(grappleActionGo);
                if (turnManagerGo != null) Object.DestroyImmediate(turnManagerGo);
                if (entityManagerGo != null) Object.DestroyImmediate(entityManagerGo);
                if (eventBusGo != null) Object.DestroyImmediate(eventBusGo);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private sealed class FixedRng : IRng
        {
            private readonly Queue<int> d20;
            private readonly Queue<int> dice;

            public FixedRng(IEnumerable<int> d20Rolls = null, IEnumerable<int> dieRolls = null)
            {
                d20 = d20Rolls != null ? new Queue<int>(d20Rolls) : new Queue<int>();
                dice = dieRolls != null ? new Queue<int>(dieRolls) : new Queue<int>();
            }

            public int RollD20()
            {
                return d20.Count > 0 ? d20.Dequeue() : 10;
            }

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
