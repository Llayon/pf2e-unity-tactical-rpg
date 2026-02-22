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
    public class TripActionTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryTrip_CritSuccess_TargetProne_AndDeals1d6Bludgeoning()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip);

            var actor = ctx.RegisterEntity(
                team: Team.Player,
                gridPosition: new Vector3Int(0, 0, 0),
                weaponDef: tripWeapon,
                level: 1,
                size: CreatureSize.Medium,
                strength: 10,
                dexterity: 10,
                currentHp: 20,
                athleticsProf: ProficiencyRank.Untrained);

            var target = ctx.RegisterEntity(
                team: Team.Enemy,
                gridPosition: new Vector3Int(1, 0, 0),
                weaponDef: tripWeapon,
                level: 1,
                size: CreatureSize.Medium,
                strength: 10,
                dexterity: 10,
                currentHp: 20,
                athleticsProf: ProficiencyRank.Untrained);

            int strikeResolvedCount = 0;
            ctx.EventBus.OnStrikeResolved += OnStrikeResolved;

            try
            {
                var degree = ctx.Action.TryTrip(actor, target, new FixedRng(d20Rolls: new[] { 20 }, dieRolls: new[] { 4 }));

                Assert.AreEqual(DegreeOfSuccess.CriticalSuccess, degree);

                var targetData = ctx.Registry.Get(target);
                Assert.IsTrue(targetData.HasCondition(ConditionType.Prone));
                Assert.AreEqual(16, targetData.CurrentHP, "Trip crit should apply flat 1d6 damage.");
                Assert.AreEqual(0, strikeResolvedCount, "Trip crit damage must not publish StrikeResolvedEvent.");
            }
            finally
            {
                ctx.EventBus.OnStrikeResolved -= OnStrikeResolved;
            }

            void OnStrikeResolved(in StrikeResolvedEvent e)
            {
                _ = e;
                strikeResolvedCount++;
            }
        }

        [Test]
        public void TryTrip_Success_TargetFallsProne()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip);
            var actor = ctx.RegisterDefaultTripActor(tripWeapon);
            var target = ctx.RegisterDefaultTripTarget(tripWeapon);

            var degree = ctx.Action.TryTrip(actor, target, new FixedRng(d20Rolls: new[] { 13 }));

            Assert.AreEqual(DegreeOfSuccess.Success, degree);
            Assert.IsTrue(ctx.Registry.Get(target).HasCondition(ConditionType.Prone));
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
        }

        [Test]
        public void TryTrip_Failure_NothingHappens()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip);
            var actor = ctx.RegisterDefaultTripActor(tripWeapon);
            var target = ctx.RegisterDefaultTripTarget(tripWeapon);
            int hpBefore = ctx.Registry.Get(target).CurrentHP;

            var degree = ctx.Action.TryTrip(actor, target, new FixedRng(d20Rolls: new[] { 8 }));

            Assert.AreEqual(DegreeOfSuccess.Failure, degree);
            Assert.IsFalse(ctx.Registry.Get(target).HasCondition(ConditionType.Prone));
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
            Assert.AreEqual(hpBefore, ctx.Registry.Get(target).CurrentHP);
        }

        [Test]
        public void TryTrip_CritFailure_ActorFallsProne()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip);
            var actor = ctx.RegisterDefaultTripActor(tripWeapon);
            var target = ctx.RegisterDefaultTripTarget(tripWeapon);

            var degree = ctx.Action.TryTrip(actor, target, new FixedRng(d20Rolls: new[] { 3 }));

            Assert.AreEqual(DegreeOfSuccess.CriticalFailure, degree);
            Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
            Assert.IsFalse(ctx.Registry.Get(target).HasCondition(ConditionType.Prone));
        }

        [Test]
        public void TryTrip_IncrementsMAP()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip);
            var actor = ctx.RegisterDefaultTripActor(tripWeapon);
            var target = ctx.RegisterDefaultTripTarget(tripWeapon);

            Assert.AreEqual(0, ctx.Registry.Get(actor).MAPCount);

            var degree = ctx.Action.TryTrip(actor, target, new FixedRng(d20Rolls: new[] { 13 }));

            Assert.IsTrue(degree.HasValue);
            Assert.AreEqual(1, ctx.Registry.Get(actor).MAPCount);
        }

        [Test]
        public void TryTrip_MAPAppliedToCheck()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip);
            var actor = ctx.RegisterDefaultTripActor(tripWeapon);
            var target = ctx.RegisterDefaultTripTarget(tripWeapon);
            var actorData = ctx.Registry.Get(actor);
            actorData.MAPCount = 1;
            Assert.AreEqual(1, actorData.MAPCount);

            int eventCount = 0;
            SkillCheckResolvedEvent lastEvent = default;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnSkillCheckResolved;

            try
            {
                var degree = ctx.Action.TryTrip(actor, target, new FixedRng(d20Rolls: new[] { 20 }, dieRolls: new[] { 1 }));
                Assert.IsTrue(degree.HasValue);

                Assert.AreEqual(1, eventCount);
                Assert.AreEqual(-5, lastEvent.modifier, "Event modifier should include MAP (-5 on second attack for non-agile weapon).");
            }
            finally
            {
                ctx.EventBus.OnSkillCheckResolvedTyped -= OnSkillCheckResolved;
            }

            void OnSkillCheckResolved(in SkillCheckResolvedEvent e)
            {
                eventCount++;
                lastEvent = e;
            }
        }

        [Test]
        public void TryTrip_UntrainedAthletics_IsAllowed()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip);
            var actor = ctx.RegisterDefaultTripActor(tripWeapon, athleticsProf: ProficiencyRank.Untrained);
            var target = ctx.RegisterDefaultTripTarget(tripWeapon);

            var degree = ctx.Action.TryTrip(actor, target, new FixedRng(d20Rolls: new[] { 13 }));

            Assert.IsTrue(degree.HasValue);
        }

        [Test]
        public void TryTrip_OutOfRange_ReturnsNull()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip, reachFeet: 5);
            var actor = ctx.RegisterDefaultTripActor(tripWeapon, gridPosition: new Vector3Int(0, 0, 0));
            var target = ctx.RegisterDefaultTripTarget(tripWeapon, gridPosition: new Vector3Int(2, 0, 0));

            var degree = ctx.Action.TryTrip(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.IsFalse(degree.HasValue);
        }

        [Test]
        public void TryTrip_SameTeam_ReturnsNull()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip);
            var actor = ctx.RegisterDefaultTripActor(tripWeapon, team: Team.Player);
            var target = ctx.RegisterDefaultTripTarget(tripWeapon, team: Team.Player);

            var degree = ctx.Action.TryTrip(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.IsFalse(degree.HasValue);
        }

        [Test]
        public void TryTrip_TargetMoreThanOneSizeLarger_ReturnsNull()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip);
            var actor = ctx.RegisterDefaultTripActor(tripWeapon, size: CreatureSize.Medium);
            var target = ctx.RegisterDefaultTripTarget(tripWeapon, size: CreatureSize.Huge);

            var degree = ctx.Action.TryTrip(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.IsFalse(degree.HasValue);
        }

        [Test]
        public void TryTrip_TargetOneSizeLarger_IsAllowed()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip);
            var actor = ctx.RegisterDefaultTripActor(tripWeapon, size: CreatureSize.Medium);
            var target = ctx.RegisterDefaultTripTarget(tripWeapon, size: CreatureSize.Large);

            bool canTrip = ctx.Action.CanTrip(actor, target);

            Assert.IsTrue(canTrip);
        }

        [Test]
        public void TryTrip_WeaponWithTripTrait_IsAllowed()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip);
            var actor = ctx.RegisterDefaultTripActor(tripWeapon);
            var target = ctx.RegisterDefaultTripTarget(tripWeapon);

            bool canTrip = ctx.Action.CanTrip(actor, target);

            Assert.IsTrue(canTrip);
        }

        [Test]
        public void TryTrip_WeaponWithoutTripTrait_ReturnsNull()
        {
            using var ctx = new TripContext();
            var normalWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.None);
            var actor = ctx.RegisterDefaultTripActor(normalWeapon);
            var target = ctx.RegisterDefaultTripTarget(normalWeapon);

            var degree = ctx.Action.TryTrip(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.IsFalse(degree.HasValue);
        }

        [Test]
        public void TryTrip_PublishesSkillCheckResolvedEvent()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip);
            var actor = ctx.RegisterDefaultTripActor(tripWeapon);
            var target = ctx.RegisterDefaultTripTarget(tripWeapon);

            int count = 0;
            SkillCheckResolvedEvent last = default;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnSkillCheckResolved;

            try
            {
                var degree = ctx.Action.TryTrip(actor, target, new FixedRng(d20Rolls: new[] { 13 }));
                Assert.IsTrue(degree.HasValue);
                Assert.AreEqual(1, count);
                Assert.AreEqual(actor, last.actor);
                Assert.AreEqual(target, last.target);
                Assert.AreEqual(SkillType.Athletics, last.skill);
                Assert.AreEqual("Trip", last.actionName);
                Assert.AreEqual(DegreeOfSuccess.Success, last.degree);
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
        public void TryTrip_CritSuccess_TargetKilled_HandlesDeathWithoutException()
        {
            using var ctx = new TripContext();
            var tripWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Trip);
            var actor = ctx.RegisterDefaultTripActor(tripWeapon);
            var target = ctx.RegisterDefaultTripTarget(tripWeapon, currentHp: 1);

            int defeatedCount = 0;
            EntityDefeatedEvent defeatedEvent = default;
            ctx.EventBus.OnEntityDefeated += OnDefeated;

            try
            {
                var degree = ctx.Action.TryTrip(actor, target, new FixedRng(d20Rolls: new[] { 20 }, dieRolls: new[] { 6 }));

                Assert.AreEqual(DegreeOfSuccess.CriticalSuccess, degree);
                Assert.AreEqual(0, ctx.Registry.Get(target).CurrentHP);
                Assert.AreEqual(1, defeatedCount);
                Assert.AreEqual(target, defeatedEvent.handle);
            }
            finally
            {
                ctx.EventBus.OnEntityDefeated -= OnDefeated;
            }

            void OnDefeated(in EntityDefeatedEvent e)
            {
                defeatedCount++;
                defeatedEvent = e;
            }
        }

        [Test]
        public void PlayerActionExecutor_TryExecuteTrip_InvalidAttempt_RollsBackExecutingAction()
        {
            using var ctx = new ExecutorTripContext();

            int actionsBefore = ctx.TurnManager.ActionsRemaining;

            bool started = ctx.Executor.TryExecuteTrip(ctx.Enemy);

            Assert.IsFalse(started);
            Assert.AreEqual(TurnState.PlayerTurn, ctx.TurnManager.State);
            Assert.AreEqual(actionsBefore, ctx.TurnManager.ActionsRemaining);
            Assert.AreEqual(EntityHandle.None, ctx.TurnManager.ExecutingActor);
        }

        private sealed class TripContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly List<WeaponDefinition> weaponDefs = new();
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject tripActionGo;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public TripAction Action { get; }
            public EntityRegistry Registry => EntityManager.Registry;

            public TripContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("Trip_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("Trip_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                tripActionGo = new GameObject("TripAction_Test");
                Action = tripActionGo.AddComponent<TripAction>();
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

            public EntityHandle RegisterDefaultTripActor(
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
                    currentHp: currentHp,
                    athleticsProf: athleticsProf);
            }

            public EntityHandle RegisterDefaultTripTarget(
                WeaponDefinition weaponDef,
                Team team = Team.Enemy,
                Vector3Int? gridPosition = null,
                CreatureSize size = CreatureSize.Medium,
                int currentHp = 20)
            {
                return RegisterEntity(
                    team,
                    gridPosition ?? new Vector3Int(1, 0, 0),
                    weaponDef,
                    level: 1,
                    size: size,
                    strength: 10,
                    dexterity: 10,
                    currentHp: currentHp,
                    athleticsProf: ProficiencyRank.Untrained);
            }

            public EntityHandle RegisterEntity(
                Team team,
                Vector3Int gridPosition,
                WeaponDefinition weaponDef,
                int level,
                CreatureSize size,
                int strength,
                int dexterity,
                int currentHp,
                ProficiencyRank athleticsProf)
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
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    AthleticsProf = athleticsProf,
                    ReflexProf = ProficiencyRank.Trained,
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

                if (tripActionGo != null) Object.DestroyImmediate(tripActionGo);
                if (entityManagerGo != null) Object.DestroyImmediate(entityManagerGo);
                if (eventBusGo != null) Object.DestroyImmediate(eventBusGo);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private sealed class ExecutorTripContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject turnManagerGo;
            private readonly GameObject tripActionGo;
            private readonly GameObject executorGo;
            private readonly WeaponDefinition tripWeaponDef;
            private readonly WeaponDefinition nonTripWeaponDef;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public TurnManager TurnManager { get; }
            public TripAction TripAction { get; }
            public PlayerActionExecutor Executor { get; }
            public EntityRegistry Registry => EntityManager.Registry;
            public EntityHandle Player { get; }
            public EntityHandle Enemy { get; }

            public ExecutorTripContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("ExecutorTrip_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("ExecutorTrip_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                turnManagerGo = new GameObject("ExecutorTrip_TurnManager_Test");
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);

                tripActionGo = new GameObject("ExecutorTrip_TripAction_Test");
                TripAction = tripActionGo.AddComponent<TripAction>();
                SetPrivateField(TripAction, "entityManager", EntityManager);
                SetPrivateField(TripAction, "eventBus", EventBus);

                executorGo = new GameObject("ExecutorTrip_PlayerExecutor_Test");
                Executor = executorGo.AddComponent<PlayerActionExecutor>();
                SetPrivateField(Executor, "turnManager", TurnManager);
                SetPrivateField(Executor, "entityManager", EntityManager);
                SetPrivateField(Executor, "tripAction", TripAction);

                tripWeaponDef = CreateWeaponDef("Trip Weapon", WeaponTraitFlags.Trip);
                nonTripWeaponDef = CreateWeaponDef("Normal Weapon", WeaponTraitFlags.None);

                // Player gets no-Trip weapon so TryTrip fails after BeginActionExecution (valid target, invalid trip eligibility).
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
                    Wisdom = 100, // guarantee first initiative
                    Charisma = 10,
                    EquippedWeapon = new WeaponInstance { def = nonTripWeaponDef, strikingRank = StrikingRuneRank.None },
                    ActionsRemaining = 0 // overwritten by TurnManager.StartCombat/TickStartTurn
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
                    Wisdom = 1,
                    Charisma = 10,
                    EquippedWeapon = new WeaponInstance { def = tripWeaponDef, strikingRank = StrikingRuneRank.None }
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
                    new InitiativeEntry { Handle = Player, Roll = 20, Modifier = 45, IsPlayer = true },
                    new InitiativeEntry { Handle = Enemy, Roll = 1, Modifier = -5, IsPlayer = false }
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
                if (tripWeaponDef != null) Object.DestroyImmediate(tripWeaponDef);
                if (nonTripWeaponDef != null) Object.DestroyImmediate(nonTripWeaponDef);
                if (executorGo != null) Object.DestroyImmediate(executorGo);
                if (tripActionGo != null) Object.DestroyImmediate(tripActionGo);
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
