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
    public class EscapeActionTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TryEscape_Success_ReleasesGrabbedRelation()
        {
            using var ctx = new EscapeContext();
            ctx.ApplyGrapple(GrappleHoldState.Grabbed);

            var degree = ctx.Action.TryEscape(ctx.Escaper, ctx.Grappler, new FixedRng(d20Rolls: new[] { 10 }));

            Assert.AreEqual(DegreeOfSuccess.Success, degree);
            Assert.IsFalse(ctx.Lifecycle.Service.HasExactRelation(ctx.Grappler, ctx.Escaper));
            Assert.IsFalse(ctx.Registry.Get(ctx.Escaper).HasCondition(ConditionType.Grabbed));
            Assert.IsFalse(ctx.Registry.Get(ctx.Escaper).HasCondition(ConditionType.Restrained));
        }

        [Test]
        public void TryEscape_CritSuccess_ReleasesRestrainedRelation()
        {
            using var ctx = new EscapeContext();
            ctx.ApplyGrapple(GrappleHoldState.Restrained);

            var degree = ctx.Action.TryEscape(ctx.Escaper, ctx.Grappler, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.AreEqual(DegreeOfSuccess.CriticalSuccess, degree);
            Assert.IsFalse(ctx.Lifecycle.Service.HasExactRelation(ctx.Grappler, ctx.Escaper));
            Assert.IsFalse(ctx.Registry.Get(ctx.Escaper).HasCondition(ConditionType.Grabbed));
            Assert.IsFalse(ctx.Registry.Get(ctx.Escaper).HasCondition(ConditionType.Restrained));
        }

        [Test]
        public void TryEscape_Failure_KeepsRelation()
        {
            using var ctx = new EscapeContext();
            ctx.ApplyGrapple(GrappleHoldState.Grabbed);

            var degree = ctx.Action.TryEscape(ctx.Escaper, ctx.Grappler, new FixedRng(d20Rolls: new[] { 5 }));

            Assert.AreEqual(DegreeOfSuccess.Failure, degree);
            Assert.IsTrue(ctx.Lifecycle.Service.HasExactRelation(ctx.Grappler, ctx.Escaper));
            Assert.IsTrue(ctx.Registry.Get(ctx.Escaper).HasCondition(ConditionType.Grabbed));
        }

        [Test]
        public void TryEscape_NoRelation_ReturnsNull()
        {
            using var ctx = new EscapeContext();

            var degree = ctx.Action.TryEscape(ctx.Escaper, ctx.Grappler, new FixedRng(d20Rolls: new[] { 20 }));

            Assert.IsFalse(degree.HasValue);
        }

        [Test]
        public void TryEscape_UsesAthleticsDcOfGrappler()
        {
            using var ctx = new EscapeContext(grapplerStrength: 16, grapplerAthleticsProf: ProficiencyRank.Expert);
            ctx.ApplyGrapple(GrappleHoldState.Grabbed);

            int count = 0;
            SkillCheckResolvedEvent last = default;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnResolved;
            try
            {
                var degree = ctx.Action.TryEscape(ctx.Escaper, ctx.Grappler, new FixedRng(d20Rolls: new[] { 20 }));
                Assert.IsTrue(degree.HasValue);
                Assert.AreEqual(1, count);
                Assert.AreEqual(10 + ctx.Registry.Get(ctx.Grappler).GetSkillModifier(SkillType.Athletics), last.dc);
            }
            finally
            {
                ctx.EventBus.OnSkillCheckResolvedTyped -= OnResolved;
            }

            void OnResolved(in SkillCheckResolvedEvent e)
            {
                count++;
                last = e;
            }
        }

        [Test]
        public void TryEscape_ChoosesBestOfAthleticsOrAcrobatics_Mvp_AndPublishesChosenSkill()
        {
            using var ctx = new EscapeContext(
                escaperStrength: 10,
                escaperDexterity: 16,
                escaperAthleticsProf: ProficiencyRank.Untrained,
                escaperAcrobaticsProf: ProficiencyRank.Trained);
            ctx.ApplyGrapple(GrappleHoldState.Grabbed);

            int count = 0;
            SkillCheckResolvedEvent last = default;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnResolved;
            try
            {
                var degree = ctx.Action.TryEscape(ctx.Escaper, ctx.Grappler, new FixedRng(d20Rolls: new[] { 10 }));
                Assert.IsTrue(degree.HasValue);
                Assert.AreEqual(1, count);
                Assert.AreEqual(SkillType.Acrobatics, last.skill);
            }
            finally
            {
                ctx.EventBus.OnSkillCheckResolvedTyped -= OnResolved;
            }

            void OnResolved(in SkillCheckResolvedEvent e)
            {
                count++;
                last = e;
            }
        }

        [Test]
        public void TryEscape_HasAttackTrait_MAPAppliedToCheck()
        {
            using var ctx = new EscapeContext();
            ctx.ApplyGrapple(GrappleHoldState.Grabbed);
            ctx.Registry.Get(ctx.Escaper).MAPCount = 1;

            int count = 0;
            SkillCheckResolvedEvent last = default;
            ctx.EventBus.OnSkillCheckResolvedTyped += OnResolved;
            try
            {
                var degree = ctx.Action.TryEscape(ctx.Escaper, ctx.Grappler, new FixedRng(d20Rolls: new[] { 20 }));
                Assert.IsTrue(degree.HasValue);
                Assert.AreEqual(1, count);
                Assert.AreEqual(-5, last.modifier);
            }
            finally
            {
                ctx.EventBus.OnSkillCheckResolvedTyped -= OnResolved;
            }

            void OnResolved(in SkillCheckResolvedEvent e)
            {
                count++;
                last = e;
            }
        }

        [Test]
        public void TryEscape_HasAttackTrait_IncrementsMAP()
        {
            using var ctx = new EscapeContext();
            ctx.ApplyGrapple(GrappleHoldState.Grabbed);

            Assert.AreEqual(0, ctx.Registry.Get(ctx.Escaper).MAPCount);
            var degree = ctx.Action.TryEscape(ctx.Escaper, ctx.Grappler, new FixedRng(d20Rolls: new[] { 10 }));

            Assert.IsTrue(degree.HasValue);
            Assert.AreEqual(1, ctx.Registry.Get(ctx.Escaper).MAPCount);
        }

        [Test]
        public void PlayerActionExecutor_TryExecuteEscape_InvalidAttempt_RollsBackExecutingAction()
        {
            using var ctx = new ExecutorEscapeContext();

            int actionsBefore = ctx.TurnManager.ActionsRemaining;
            bool started = ctx.Executor.TryExecuteEscape(ctx.Grappler);

            Assert.IsFalse(started);
            Assert.AreEqual(TurnState.PlayerTurn, ctx.TurnManager.State);
            Assert.AreEqual(actionsBefore, ctx.TurnManager.ActionsRemaining);
            Assert.AreEqual(EntityHandle.None, ctx.TurnManager.ExecutingActor);
        }

        private sealed class EscapeContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly List<WeaponDefinition> weaponDefs = new();
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject lifecycleGo;
            private readonly GameObject actionGo;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public GrappleLifecycleController Lifecycle { get; }
            public EscapeAction Action { get; }
            public EntityRegistry Registry { get; }
            public EntityHandle Grappler { get; }
            public EntityHandle Escaper { get; }

            public EscapeContext(
                int grapplerStrength = 10,
                ProficiencyRank grapplerAthleticsProf = ProficiencyRank.Untrained,
                int escaperStrength = 10,
                int escaperDexterity = 10,
                ProficiencyRank escaperAthleticsProf = ProficiencyRank.Untrained,
                ProficiencyRank escaperAcrobaticsProf = ProficiencyRank.Untrained)
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("Escape_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("Escape_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                lifecycleGo = new GameObject("Escape_GrappleLifecycle_Test");
                Lifecycle = lifecycleGo.AddComponent<GrappleLifecycleController>();
                SetPrivateField(Lifecycle, "entityManager", EntityManager);
                SetPrivateField(Lifecycle, "eventBus", EventBus);
                Lifecycle.enabled = false;
                Lifecycle.enabled = true;

                actionGo = new GameObject("EscapeAction_Test");
                Action = actionGo.AddComponent<EscapeAction>();
                SetPrivateField(Action, "entityManager", EntityManager);
                SetPrivateField(Action, "eventBus", EventBus);
                SetPrivateField(Action, "grappleLifecycle", Lifecycle);

                var normalWeapon = CreateWeaponDef("Normal", WeaponTraitFlags.None);

                Grappler = Registry.Register(new EntityData
                {
                    Name = "Grappler",
                    Team = Team.Enemy,
                    Size = CreatureSize.Medium,
                    Level = 1,
                    MaxHP = 20,
                    CurrentHP = 20,
                    Speed = 25,
                    GridPosition = new Vector3Int(1, 0, 0),
                    Strength = grapplerStrength,
                    Dexterity = 10,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    AthleticsProf = grapplerAthleticsProf,
                    EquippedWeapon = new WeaponInstance { def = normalWeapon, strikingRank = StrikingRuneRank.None }
                });

                Escaper = Registry.Register(new EntityData
                {
                    Name = "Escaper",
                    Team = Team.Player,
                    Size = CreatureSize.Medium,
                    Level = 1,
                    MaxHP = 20,
                    CurrentHP = 20,
                    Speed = 25,
                    GridPosition = new Vector3Int(0, 0, 0),
                    Strength = escaperStrength,
                    Dexterity = escaperDexterity,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    AthleticsProf = escaperAthleticsProf,
                    AcrobaticsProf = escaperAcrobaticsProf,
                    EquippedWeapon = new WeaponInstance { def = normalWeapon, strikingRank = StrikingRuneRank.None }
                });
            }

            public void ApplyGrapple(GrappleHoldState holdState)
            {
                var deltas = new List<ConditionDelta>();
                Lifecycle.Service.ApplyOrRefresh(Registry.Get(Grappler), Registry.Get(Escaper), holdState, Registry, deltas);
            }

            private WeaponDefinition CreateWeaponDef(string name, WeaponTraitFlags traits)
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
                weaponDefs.Add(def);
                return def;
            }

            public void Dispose()
            {
                for (int i = 0; i < weaponDefs.Count; i++)
                {
                    if (weaponDefs[i] != null)
                        Object.DestroyImmediate(weaponDefs[i]);
                }

                if (actionGo != null) Object.DestroyImmediate(actionGo);
                if (lifecycleGo != null) Object.DestroyImmediate(lifecycleGo);
                if (entityManagerGo != null) Object.DestroyImmediate(entityManagerGo);
                if (eventBusGo != null) Object.DestroyImmediate(eventBusGo);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private sealed class ExecutorEscapeContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject turnManagerGo;
            private readonly GameObject lifecycleGo;
            private readonly GameObject escapeActionGo;
            private readonly GameObject executorGo;
            private readonly WeaponDefinition weaponDef;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public TurnManager TurnManager { get; }
            public GrappleLifecycleController Lifecycle { get; }
            public EscapeAction EscapeAction { get; }
            public PlayerActionExecutor Executor { get; }
            public EntityRegistry Registry => EntityManager.Registry;
            public EntityHandle Player { get; }
            public EntityHandle Grappler { get; }

            public ExecutorEscapeContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("ExecutorEscape_EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("ExecutorEscape_EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());
                SetPrivateField(EntityManager, "eventBus", EventBus);
                SetPrivateField(EntityManager, "spawnTestEntities", false);

                turnManagerGo = new GameObject("ExecutorEscape_TurnManager_Test");
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);

                lifecycleGo = new GameObject("ExecutorEscape_GrappleLifecycle_Test");
                Lifecycle = lifecycleGo.AddComponent<GrappleLifecycleController>();
                SetPrivateField(Lifecycle, "entityManager", EntityManager);
                SetPrivateField(Lifecycle, "eventBus", EventBus);
                Lifecycle.enabled = false;
                Lifecycle.enabled = true;

                escapeActionGo = new GameObject("ExecutorEscape_EscapeAction_Test");
                EscapeAction = escapeActionGo.AddComponent<EscapeAction>();
                SetPrivateField(EscapeAction, "entityManager", EntityManager);
                SetPrivateField(EscapeAction, "eventBus", EventBus);
                SetPrivateField(EscapeAction, "grappleLifecycle", Lifecycle);

                executorGo = new GameObject("ExecutorEscape_PlayerExecutor_Test");
                Executor = executorGo.AddComponent<PlayerActionExecutor>();
                SetPrivateField(Executor, "turnManager", TurnManager);
                SetPrivateField(Executor, "entityManager", EntityManager);
                SetPrivateField(Executor, "escapeAction", EscapeAction);

                weaponDef = ScriptableObject.CreateInstance<WeaponDefinition>();
                weaponDef.itemName = "Test Weapon";
                weaponDef.category = WeaponCategory.Simple;
                weaponDef.group = WeaponGroup.Club;
                weaponDef.hands = WeaponHands.One;
                weaponDef.diceCount = 1;
                weaponDef.dieSides = 6;
                weaponDef.damageType = DamageType.Bludgeoning;
                weaponDef.reachFeet = 5;
                weaponDef.isRanged = false;
                weaponDef.traits = WeaponTraitFlags.None;

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
                    EquippedWeapon = new WeaponInstance { def = weaponDef, strikingRank = StrikingRuneRank.None }
                });

                Grappler = Registry.Register(new EntityData
                {
                    Name = "Grappler",
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
                    EquippedWeapon = new WeaponInstance { def = weaponDef, strikingRank = StrikingRuneRank.None }
                });

                var playerData = Registry.Get(Player);
                playerData.ActionsRemaining = 3;
                playerData.MAPCount = 0;
                playerData.ReactionAvailable = true;

                var grapplerData = Registry.Get(Grappler);
                grapplerData.ActionsRemaining = 3;
                grapplerData.MAPCount = 0;
                grapplerData.ReactionAvailable = true;

                SetPrivateField(TurnManager, "initiativeOrder", new List<InitiativeEntry>
                {
                    new InitiativeEntry { Handle = Player, Roll = new CheckRoll(20, 5, CheckSource.Perception()), IsPlayer = true },
                    new InitiativeEntry { Handle = Grappler, Roll = new CheckRoll(1, -1, CheckSource.Perception()), IsPlayer = false }
                });
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
            }

            public void Dispose()
            {
                if (weaponDef != null) Object.DestroyImmediate(weaponDef);
                if (executorGo != null) Object.DestroyImmediate(executorGo);
                if (escapeActionGo != null) Object.DestroyImmediate(escapeActionGo);
                if (lifecycleGo != null) Object.DestroyImmediate(lifecycleGo);
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
