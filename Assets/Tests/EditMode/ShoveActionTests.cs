using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class ShoveActionTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryShove_CritSuccess_PushesTargetTwoCellsAway_MvpGridApprox()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon, gridPosition: new Vector3Int(0, 0, 0));
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon, gridPosition: new Vector3Int(1, 0, 0));

            var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.AreEqual(DegreeOfSuccess.CriticalSuccess, degree);
            Assert.AreEqual(new Vector3Int(3, 0, 0), ctx.Registry.Get(target).GridPosition);
            Assert.AreEqual(target, ctx.Occupancy.GetOccupant(new Vector3Int(3, 0, 0)));
        }

        [Test]
        public void TryShove_Success_PushesTargetOneCellAway()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon);
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon);

            var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 13 }));

            Assert.AreEqual(DegreeOfSuccess.Success, degree);
            Assert.AreEqual(new Vector3Int(2, 0, 0), ctx.Registry.Get(target).GridPosition);
        }

        [Test]
        public void TryShove_Failure_NoMovement()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon);
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon);
            var targetStart = ctx.Registry.Get(target).GridPosition;

            var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 8 }));

            Assert.AreEqual(DegreeOfSuccess.Failure, degree);
            Assert.AreEqual(targetStart, ctx.Registry.Get(target).GridPosition);
            Assert.IsFalse(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
        }

        [Test]
        public void TryShove_CritFailure_ActorFallsProne()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon);
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon);

            var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 3 }));

            Assert.AreEqual(DegreeOfSuccess.CriticalFailure, degree);
            Assert.IsTrue(ctx.Registry.Get(actor).HasCondition(ConditionType.Prone));
        }

        [Test]
        public void TryShove_IncrementsMAP()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon);
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon);

            Assert.AreEqual(0, ctx.Registry.Get(actor).MAPCount);
            var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 13 }));

            Assert.IsTrue(degree.HasValue);
            Assert.AreEqual(1, ctx.Registry.Get(actor).MAPCount);
        }

        [Test]
        public void TryShove_MAPAppliedToCheck()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon);
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon);
            ctx.Registry.Get(actor).MAPCount = 1;

            int count = 0;
            SkillCheckResolvedEvent last = default;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnSkillCheckResolved;

            try
            {
                var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 20 }));
                Assert.IsTrue(degree.HasValue);
                Assert.AreEqual(1, count);
                Assert.AreEqual(-5, last.modifier, "Event modifier should include MAP (-5 on second attack for non-agile weapon).");
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
        public void TryShove_UntrainedAthletics_IsAllowed()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon, athleticsProf: ProficiencyRank.Untrained);
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon);

            var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 13 }));

            Assert.IsTrue(degree.HasValue);
        }

        [Test]
        public void TryShove_UsesFortitudeDC()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon);
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon, fortitudeProf: ProficiencyRank.Expert, constitution: 16);

            SkillCheckResolvedEvent last = default;
            int count = 0;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnSkillCheckResolved;

            try
            {
                var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 13 }));
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
        public void TryShove_TargetMoreThanOneSizeLarger_ReturnsNull()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon, size: CreatureSize.Medium);
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon, size: CreatureSize.Huge);

            var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.IsFalse(degree.HasValue);
        }

        [Test]
        public void TryShove_TargetOneSizeLarger_IsAllowed()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon, size: CreatureSize.Medium);
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon, size: CreatureSize.Large);

            bool canShove = ctx.Action.CanShove(actor, target);

            Assert.IsTrue(canShove);
        }

        [Test]
        public void TryShove_WeaponWithShoveTrait_IsAllowed()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon);
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon);

            Assert.IsTrue(ctx.Action.CanShove(actor, target));
        }

        [Test]
        public void TryShove_WeaponWithoutShoveTrait_ReturnsNull()
        {
            using var ctx = new ShoveContext();
            var normalWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.None);
            var actor = ctx.RegisterDefaultShoveActor(normalWeapon);
            var target = ctx.RegisterDefaultShoveTarget(normalWeapon);

            var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.IsFalse(degree.HasValue);
        }

        [Test]
        public void TryShove_DiagonalAdjacent_Success_PushesOneDiagonalCell()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove, reachFeet: 10);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon, gridPosition: new Vector3Int(0, 0, 0));
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon, gridPosition: new Vector3Int(1, 0, 1));

            var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 13 }));

            Assert.AreEqual(DegreeOfSuccess.Success, degree);
            Assert.AreEqual(new Vector3Int(2, 0, 2), ctx.Registry.Get(target).GridPosition);
        }

        [Test]
        public void TryShove_ReachOffset_NormalizesDirectionUsingSignDelta()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove, reachFeet: 20);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon, gridPosition: new Vector3Int(0, 0, 0));
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon, gridPosition: new Vector3Int(2, 0, 1));

            var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 13 }));

            Assert.AreEqual(DegreeOfSuccess.Success, degree);
            Assert.AreEqual(new Vector3Int(3, 0, 2), ctx.Registry.Get(target).GridPosition);
        }

        [Test]
        public void TryShove_CritSuccess_SecondStepBlocked_StopsAfterFirst()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon, gridPosition: new Vector3Int(0, 0, 0));
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon, gridPosition: new Vector3Int(1, 0, 0));
            ctx.SetImpassableCell(new Vector3Int(3, 0, 0));

            var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.AreEqual(DegreeOfSuccess.CriticalSuccess, degree);
            Assert.AreEqual(new Vector3Int(2, 0, 0), ctx.Registry.Get(target).GridPosition);
        }

        [Test]
        public void TryShove_PublishesSkillCheckResolvedEvent()
        {
            using var ctx = new ShoveContext();
            var shoveWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Shove);
            var actor = ctx.RegisterDefaultShoveActor(shoveWeapon);
            var target = ctx.RegisterDefaultShoveTarget(shoveWeapon);

            int count = 0;
            SkillCheckResolvedEvent last = default;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnSkillCheckResolved;

            try
            {
                var degree = ctx.Action.TryShove(actor, target, new FixedRng(d20Rolls: new[] { 13 }));
                Assert.IsTrue(degree.HasValue);
                Assert.AreEqual(1, count);
                Assert.AreEqual(actor, last.actor);
                Assert.AreEqual(target, last.target);
                Assert.AreEqual(SkillType.Athletics, last.skill);
                Assert.AreEqual("Shove", last.actionName);
                Assert.IsTrue(last.hasOpposedProjection);
                Assert.AreEqual(last.total - last.dc, last.opposedProjection.margin);
                Assert.AreEqual(CheckSourceType.Save, last.opposedProjection.defenderRoll.source.type);
                Assert.AreEqual(SaveType.Fortitude, last.opposedProjection.defenderRoll.source.save);
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
        public void PlayerActionExecutor_TryExecuteShove_InvalidAttempt_RollsBackExecutingAction()
        {
            using var ctx = new ExecutorShoveContext();

            int actionsBefore = ctx.TurnManager.ActionsRemaining;
            bool started = ctx.Executor.TryExecuteShove(ctx.Enemy);

            Assert.IsFalse(started);
            Assert.AreEqual(TurnState.PlayerTurn, ctx.TurnManager.State);
            Assert.AreEqual(actionsBefore, ctx.TurnManager.ActionsRemaining);
            Assert.AreEqual(EntityHandle.None, ctx.TurnManager.ExecutingActor);
        }

        private sealed class ShoveContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly List<WeaponDefinition> weaponDefs = new();
            private readonly GameObject eventBusGo;
            private readonly GameObject gridManagerGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject shoveActionGo;
            private readonly GridData gridData;

            public CombatEventBus EventBus { get; }
            public GridManager GridManager { get; }
            public EntityManager EntityManager { get; }
            public ShoveAction Action { get; }
            public EntityRegistry Registry { get; }
            public OccupancyMap Occupancy { get; }

            public ShoveContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("Shove_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                gridManagerGo = new GameObject("Shove_GridManager_Test");
                GridManager = gridManagerGo.AddComponent<GridManager>();
                gridData = new GridData(cellWorldSize: 1f, heightStepWorld: 1f);
                BuildFlatWalkableGrid(gridData, minX: 0, maxX: 12, minZ: 0, maxZ: 12);
                SetAutoPropertyBackingField(GridManager, "Data", gridData);

                entityManagerGo = new GameObject("Shove_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                Occupancy = new OccupancyMap(Registry);
                SetPrivateField(EntityManager, "gridManager", GridManager);
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);
                SetAutoPropertyBackingField(EntityManager, "Occupancy", Occupancy);

                shoveActionGo = new GameObject("ShoveAction_Test");
                Action = shoveActionGo.AddComponent<ShoveAction>();
                SetPrivateField(Action, "entityManager", EntityManager);
                SetPrivateField(Action, "eventBus", EventBus);
            }

            public void SetImpassableCell(Vector3Int cell)
            {
                gridData.SetCell(cell, CellData.CreateBlocked());
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

            public EntityHandle RegisterDefaultShoveActor(
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

            public EntityHandle RegisterDefaultShoveTarget(
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

                var handle = Registry.Register(data);
                bool placed = Occupancy.Place(handle, gridPosition, data.SizeCells);
                Assert.IsTrue(placed, $"Failed to place test entity at {gridPosition}");
                return handle;
            }

            public void Dispose()
            {
                for (int i = 0; i < weaponDefs.Count; i++)
                {
                    if (weaponDefs[i] != null)
                        Object.DestroyImmediate(weaponDefs[i]);
                }

                if (shoveActionGo != null) Object.DestroyImmediate(shoveActionGo);
                if (entityManagerGo != null) Object.DestroyImmediate(entityManagerGo);
                if (gridManagerGo != null) Object.DestroyImmediate(gridManagerGo);
                if (eventBusGo != null) Object.DestroyImmediate(eventBusGo);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }

            private static void BuildFlatWalkableGrid(GridData data, int minX, int maxX, int minZ, int maxZ)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                        data.SetCell(new Vector3Int(x, 0, z), CellData.CreateWalkable());
                }
            }
        }

        private sealed class ExecutorShoveContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject turnManagerGo;
            private readonly GameObject shoveActionGo;
            private readonly GameObject executorGo;
            private readonly WeaponDefinition shoveWeaponDef;
            private readonly WeaponDefinition nonShoveWeaponDef;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public TurnManager TurnManager { get; }
            public ShoveAction ShoveAction { get; }
            public PlayerActionExecutor Executor { get; }
            public EntityRegistry Registry => EntityManager.Registry;
            public EntityHandle Player { get; }
            public EntityHandle Enemy { get; }

            public ExecutorShoveContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("ExecutorShove_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("ExecutorShove_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                turnManagerGo = new GameObject("ExecutorShove_TurnManager_Test");
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);

                shoveActionGo = new GameObject("ExecutorShove_ShoveAction_Test");
                ShoveAction = shoveActionGo.AddComponent<ShoveAction>();
                SetPrivateField(ShoveAction, "entityManager", EntityManager);
                SetPrivateField(ShoveAction, "eventBus", EventBus);

                executorGo = new GameObject("ExecutorShove_PlayerExecutor_Test");
                Executor = executorGo.AddComponent<PlayerActionExecutor>();
                SetPrivateField(Executor, "turnManager", TurnManager);
                SetPrivateField(Executor, "entityManager", EntityManager);
                SetPrivateField(Executor, "shoveAction", ShoveAction);

                shoveWeaponDef = CreateWeaponDef("Shove Weapon", WeaponTraitFlags.Shove);
                nonShoveWeaponDef = CreateWeaponDef("Normal Weapon", WeaponTraitFlags.None);

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
                    Wisdom = 100,
                    Charisma = 10,
                    EquippedWeapon = new WeaponInstance { def = nonShoveWeaponDef, strikingRank = StrikingRuneRank.None }
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
                    EquippedWeapon = new WeaponInstance { def = shoveWeaponDef, strikingRank = StrikingRuneRank.None }
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
                    new InitiativeEntry { Handle = Player, Roll = new CheckRoll(20, 45, CheckSource.Perception()), IsPlayer = true },
                    new InitiativeEntry { Handle = Enemy, Roll = new CheckRoll(1, -5, CheckSource.Perception()), IsPlayer = false }
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
                if (shoveWeaponDef != null) Object.DestroyImmediate(shoveWeaponDef);
                if (nonShoveWeaponDef != null) Object.DestroyImmediate(nonShoveWeaponDef);
                if (executorGo != null) Object.DestroyImmediate(executorGo);
                if (shoveActionGo != null) Object.DestroyImmediate(shoveActionGo);
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
