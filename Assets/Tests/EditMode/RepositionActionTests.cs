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
    public class RepositionActionTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void CanRepositionTarget_WeaponWithRepositionTrait_IsAllowed()
        {
            using var ctx = new RepositionContext();
            var weapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Reposition);
            var actor = ctx.RegisterDefaultActor(weapon);
            var target = ctx.RegisterDefaultTarget(weapon);

            Assert.IsTrue(ctx.Action.CanRepositionTarget(actor, target));
        }

        [Test]
        public void CanRepositionTarget_GrapplingTarget_AllowsWithoutTrait()
        {
            using var ctx = new RepositionContext();
            var plainWeapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.None);
            var actor = ctx.RegisterDefaultActor(plainWeapon);
            var target = ctx.RegisterDefaultTarget(plainWeapon);
            ctx.EstablishGrapple(actor, target, GrappleHoldState.Grabbed);

            Assert.IsTrue(ctx.Action.CanRepositionTarget(actor, target));
        }

        [Test]
        public void ResolveRepositionCheck_Success_ReturnsContext_MaxMove5_AndIncrementsMap()
        {
            using var ctx = new RepositionContext();
            var weapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Reposition);
            var actor = ctx.RegisterDefaultActor(weapon);
            var target = ctx.RegisterDefaultTarget(weapon);

            var degree = ctx.Action.ResolveRepositionCheck(actor, target, out var check, new FixedRng(d20Rolls: new[] { 13 }));

            Assert.AreEqual(DegreeOfSuccess.Success, degree);
            Assert.AreEqual(DegreeOfSuccess.Success, check.degree);
            Assert.AreEqual(5, check.maxMoveFeet);
            Assert.AreEqual(1, ctx.Registry.Get(actor).MAPCount);
        }

        [Test]
        public void ResolveRepositionCheck_CritSuccess_ReturnsContext_MaxMove10()
        {
            using var ctx = new RepositionContext();
            var weapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Reposition, reachFeet: 15);
            var actor = ctx.RegisterDefaultActor(weapon);
            var target = ctx.RegisterDefaultTarget(weapon);

            var degree = ctx.Action.ResolveRepositionCheck(actor, target, out var check, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.AreEqual(DegreeOfSuccess.CriticalSuccess, degree);
            Assert.AreEqual(10, check.maxMoveFeet);
        }

        [Test]
        public void ResolveRepositionCheck_CritFailure_TargetMovesActorUpTo5Feet_DeterministicMvp()
        {
            using var ctx = new RepositionContext();
            var weapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Reposition);
            var actor = ctx.RegisterDefaultActor(weapon, gridPosition: new Vector3Int(0, 0, 0));
            var target = ctx.RegisterDefaultTarget(weapon, gridPosition: new Vector3Int(1, 0, 0));
            Vector3Int actorStart = ctx.Registry.Get(actor).GridPosition;

            var degree = ctx.Action.ResolveRepositionCheck(actor, target, out var check, new FixedRng(d20Rolls: new[] { 1 }));

            Assert.AreEqual(DegreeOfSuccess.CriticalFailure, degree);
            Assert.AreEqual(DegreeOfSuccess.CriticalFailure, check.degree);
            Assert.AreNotEqual(actorStart, ctx.Registry.Get(actor).GridPosition, "Target should move actor on crit failure (MVP deterministic choice).");
            Assert.LessOrEqual(GridDistancePF2e.DistanceFeetXZ(ctx.Registry.Get(target).GridPosition, ctx.Registry.Get(actor).GridPosition), 5);
        }

        [Test]
        public void TryGetValidRepositionDestinations_SuccessRange_FiltersOccupiedCells()
        {
            using var ctx = new RepositionContext();
            var weapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Reposition, reachFeet: 10);
            var actor = ctx.RegisterDefaultActor(weapon, gridPosition: new Vector3Int(0, 0, 0));
            var target = ctx.RegisterDefaultTarget(weapon, gridPosition: new Vector3Int(1, 0, 0));
            var blocker = ctx.RegisterBlocker(weapon, new Vector3Int(1, 0, 1));
            Assert.IsTrue(blocker.IsValid);

            var cells = new List<Vector3Int>();
            bool any = ctx.Action.TryGetValidRepositionDestinations(actor, target, maxMoveFeet: 5, cells);

            Assert.IsTrue(any);
            CollectionAssert.DoesNotContain(cells, new Vector3Int(1, 0, 1));
        }

        [Test]
        public void TryGetValidRepositionDestinations_CritRange_IncludesTwoStepCell_WhenReachAllows()
        {
            using var ctx = new RepositionContext();
            var weapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Reposition, reachFeet: 15);
            var actor = ctx.RegisterDefaultActor(weapon, gridPosition: new Vector3Int(0, 0, 0));
            var target = ctx.RegisterDefaultTarget(weapon, gridPosition: new Vector3Int(1, 0, 0));

            var cells = new List<Vector3Int>();
            bool any = ctx.Action.TryGetValidRepositionDestinations(actor, target, maxMoveFeet: 10, cells);

            Assert.IsTrue(any);
            CollectionAssert.Contains(cells, new Vector3Int(2, 0, 1), "A two-step path that remains within 15 ft reach should be valid.");
        }

        [Test]
        public void TryGetValidRepositionDestinations_PathMustRemainWithinReach_DoesNotIncludeTooFarSecondStep()
        {
            using var ctx = new RepositionContext();
            var weapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Reposition, reachFeet: 5);
            var actor = ctx.RegisterDefaultActor(weapon, gridPosition: new Vector3Int(0, 0, 0));
            var target = ctx.RegisterDefaultTarget(weapon, gridPosition: new Vector3Int(1, 0, 0));

            var cells = new List<Vector3Int>();
            bool any = ctx.Action.TryGetValidRepositionDestinations(actor, target, maxMoveFeet: 10, cells);

            Assert.IsTrue(any);
            CollectionAssert.DoesNotContain(cells, new Vector3Int(0, 0, 2));
            CollectionAssert.DoesNotContain(cells, new Vector3Int(2, 0, 0));
        }

        [Test]
        public void TryApplyRepositionMove_InvalidDestination_ReturnsFalse()
        {
            using var ctx = new RepositionContext();
            var weapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Reposition);
            var actor = ctx.RegisterDefaultActor(weapon);
            var target = ctx.RegisterDefaultTarget(weapon);

            var degree = ctx.Action.ResolveRepositionCheck(actor, target, out var check, new FixedRng(d20Rolls: new[] { 13 }));
            Assert.AreEqual(DegreeOfSuccess.Success, degree);

            bool moved = ctx.Action.TryApplyRepositionMove(actor, target, new Vector3Int(9, 0, 9), in check);

            Assert.IsFalse(moved);
        }

        [Test]
        public void TryApplyRepositionMove_ValidDestination_MovesTarget()
        {
            using var ctx = new RepositionContext();
            var weapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Reposition, reachFeet: 10);
            var actor = ctx.RegisterDefaultActor(weapon, gridPosition: new Vector3Int(0, 0, 0));
            var target = ctx.RegisterDefaultTarget(weapon, gridPosition: new Vector3Int(1, 0, 0));

            var degree = ctx.Action.ResolveRepositionCheck(actor, target, out var check, new FixedRng(d20Rolls: new[] { 13 }));
            Assert.AreEqual(DegreeOfSuccess.Success, degree);

            var cells = new List<Vector3Int>();
            Assert.IsTrue(ctx.Action.TryGetValidRepositionDestinations(actor, target, check.maxMoveFeet, cells));
            Assert.IsNotEmpty(cells);

            bool moved = ctx.Action.TryApplyRepositionMove(actor, target, cells[0], in check);

            Assert.IsTrue(moved);
            Assert.AreEqual(cells[0], ctx.Registry.Get(target).GridPosition);
        }

        [Test]
        public void ResolveRepositionCheck_PublishesSkillCheckResolvedEvent()
        {
            using var ctx = new RepositionContext();
            var weapon = ctx.CreateWeaponDef(traits: WeaponTraitFlags.Reposition);
            var actor = ctx.RegisterDefaultActor(weapon);
            var target = ctx.RegisterDefaultTarget(weapon);

            int count = 0;
            SkillCheckResolvedEvent last = default;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnSkillCheckResolved;

            try
            {
                var degree = ctx.Action.ResolveRepositionCheck(actor, target, out var _, new FixedRng(d20Rolls: new[] { 13 }));
                Assert.AreEqual(DegreeOfSuccess.Success, degree);
                Assert.AreEqual(1, count);
                Assert.AreEqual("Reposition", last.actionName);
                Assert.AreEqual(SkillType.Athletics, last.skill);
                Assert.AreEqual(actor, last.actor);
                Assert.AreEqual(target, last.target);
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

        private sealed class RepositionContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly List<WeaponDefinition> weaponDefs = new();
            private readonly GameObject eventBusGo;
            private readonly GameObject gridManagerGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject grappleLifecycleGo;
            private readonly GameObject repositionActionGo;
            private readonly GridData gridData;

            public CombatEventBus EventBus { get; }
            public GridManager GridManager { get; }
            public EntityManager EntityManager { get; }
            public GrappleLifecycleController GrappleLifecycle { get; }
            public RepositionAction Action { get; }
            public EntityRegistry Registry { get; }
            public OccupancyMap Occupancy { get; }

            public RepositionContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("Reposition_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                gridManagerGo = new GameObject("Reposition_GridManager_Test");
                GridManager = gridManagerGo.AddComponent<GridManager>();
                gridData = new GridData(cellWorldSize: 1f, heightStepWorld: 1f);
                BuildFlatWalkableGrid(gridData, minX: -2, maxX: 12, minZ: -2, maxZ: 12);
                SetAutoPropertyBackingField(GridManager, "Data", gridData);

                entityManagerGo = new GameObject("Reposition_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                Occupancy = new OccupancyMap(Registry);
                SetPrivateField(EntityManager, "gridManager", GridManager);
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);
                SetAutoPropertyBackingField(EntityManager, "Occupancy", Occupancy);

                grappleLifecycleGo = new GameObject("Reposition_GrappleLifecycle_Test");
                GrappleLifecycle = grappleLifecycleGo.AddComponent<GrappleLifecycleController>();
                SetPrivateField(GrappleLifecycle, "entityManager", EntityManager);
                SetPrivateField(GrappleLifecycle, "eventBus", EventBus);

                repositionActionGo = new GameObject("RepositionAction_Test");
                Action = repositionActionGo.AddComponent<RepositionAction>();
                SetPrivateField(Action, "entityManager", EntityManager);
                SetPrivateField(Action, "gridManager", GridManager);
                SetPrivateField(Action, "eventBus", EventBus);
                SetPrivateField(Action, "grappleLifecycle", GrappleLifecycle);
            }

            public WeaponDefinition CreateWeaponDef(WeaponTraitFlags traits, int reachFeet = 5, bool isRanged = false)
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

            public EntityHandle RegisterDefaultActor(
                WeaponDefinition weaponDef,
                Team team = Team.Player,
                Vector3Int? gridPosition = null,
                CreatureSize size = CreatureSize.Medium,
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
                    currentHp: 20,
                    athleticsProf: athleticsProf,
                    fortitudeProf: ProficiencyRank.Trained);
            }

            public EntityHandle RegisterDefaultTarget(
                WeaponDefinition weaponDef,
                Team team = Team.Enemy,
                Vector3Int? gridPosition = null,
                CreatureSize size = CreatureSize.Medium,
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
                    currentHp: 20,
                    athleticsProf: ProficiencyRank.Untrained,
                    fortitudeProf: fortitudeProf);
            }

            public EntityHandle RegisterBlocker(WeaponDefinition weaponDef, Vector3Int gridPosition)
            {
                return RegisterEntity(
                    Team.Enemy,
                    gridPosition,
                    weaponDef,
                    level: 1,
                    size: CreatureSize.Medium,
                    strength: 10,
                    dexterity: 10,
                    constitution: 10,
                    currentHp: 20,
                    athleticsProf: ProficiencyRank.Untrained,
                    fortitudeProf: ProficiencyRank.Trained);
            }

            public void EstablishGrapple(EntityHandle grappler, EntityHandle target, GrappleHoldState hold)
            {
                var deltas = new List<ConditionDelta>();
                GrappleLifecycle.Service.ApplyOrRefresh(Registry.Get(grappler), Registry.Get(target), hold, Registry, deltas);
            }

            private EntityHandle RegisterEntity(
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

                if (repositionActionGo != null) Object.DestroyImmediate(repositionActionGo);
                if (grappleLifecycleGo != null) Object.DestroyImmediate(grappleLifecycleGo);
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
                    {
                        data.SetCell(new Vector3Int(x, 0, z), CellData.CreateWalkable());
                    }
                }
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
