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
    public class SimpleMeleeDecisionPolicyTests
    {
        [Test]
        public void SelectTarget_TieOnDistance_PicksLowerHpThenLowerHandle()
        {
            using var ctx = new PolicyContext(CreateLineGrid(6));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(0, 0, 0), alive: true, currentHp: 20);
            RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 0), alive: true, currentHp: 12);
            var expected = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(0, 0, 2), alive: true, currentHp: 5);

            var actorData = ctx.Registry.Get(actor);
            var selected = ctx.Policy.SelectTarget(actorData);

            Assert.AreEqual(expected, selected);
        }

        [Test]
        public void SelectTarget_ReturnsNone_WhenNoValidPlayerTarget()
        {
            using var ctx = new PolicyContext(CreateLineGrid(4));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(0, 0, 0), alive: true, currentHp: 20);
            RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 0), alive: true, currentHp: 20);
            RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 0), alive: false, currentHp: 0);

            var actorData = ctx.Registry.Get(actor);
            var selected = ctx.Policy.SelectTarget(actorData);

            Assert.AreEqual(EntityHandle.None, selected);
        }

        [Test]
        public void SelectStrideCell_ReturnsNull_WhenNoActions()
        {
            using var ctx = new PolicyContext(CreateLineGrid(5));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(0, 0, 0), alive: true, speedFeet: 25);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(4, 0, 0), alive: true, speedFeet: 25);

            var cell = ctx.Policy.SelectStrideCell(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 0);

            Assert.IsNull(cell);
        }

        [Test]
        public void SelectStrideCell_PrefersReachableAdjacentCell()
        {
            using var ctx = new PolicyContext(CreateLineGrid(6));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(0, 0, 0), alive: true, speedFeet: 30);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(4, 0, 0), alive: true, speedFeet: 25);

            var cell = ctx.Policy.SelectStrideCell(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 3);

            Assert.AreEqual(new Vector3Int(3, 0, 0), cell);
        }

        [Test]
        public void SelectStepCell_WhenNotThreatened_ReturnsNull()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(5, 5));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(3, 0, 1), alive: true, speedFeet: 25);

            var cell = ctx.Policy.SelectStepCell(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 1);

            Assert.IsNull(cell);
        }

        [Test]
        public void SelectStepCell_WhenThreatened_PrefersSaferCellThatClosesDistance()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(5, 5));

            var actorPos = new Vector3Int(1, 0, 1);
            var threatPos = new Vector3Int(0, 0, 1);
            var targetPos = new Vector3Int(3, 0, 1);
            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, actorPos, alive: true, speedFeet: 25);
            RegisterEntity(
                ctx.Registry,
                ctx.Occupancy,
                Team.Player,
                threatPos,
                alive: true,
                speedFeet: 25,
                currentHp: 20,
                hasReactiveStrike: true,
                reactionAvailable: true);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, targetPos, alive: true, speedFeet: 25);

            var cell = ctx.Policy.SelectStepCell(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 1);

            Assert.IsTrue(cell.HasValue);
            Assert.AreEqual(5, GridDistancePF2e.DistanceFeetXZ(actorPos, cell.Value), "Step must stay adjacent.");
            Assert.Greater(GridDistancePF2e.DistanceFeetXZ(threatPos, cell.Value), 5, "Selected cell should leave the hostile Reactive Strike reach.");
            Assert.LessOrEqual(
                GridDistancePF2e.DistanceFeetXZ(cell.Value, targetPos),
                GridDistancePF2e.DistanceFeetXZ(actorPos, targetPos),
                "Step should not move farther away from the target.");
        }

        [Test]
        public void SelectStepCell_WhenNoSaferCell_PrefersMeleeSetupCell()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(5, 5));

            var actorPos = new Vector3Int(1, 0, 1);
            var targetPos = new Vector3Int(2, 0, 2);
            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, actorPos, alive: true, speedFeet: 25);
            RegisterEntity(
                ctx.Registry,
                ctx.Occupancy,
                Team.Player,
                new Vector3Int(0, 0, 1),
                alive: true,
                speedFeet: 25,
                currentHp: 20,
                hasReactiveStrike: true,
                reactionAvailable: true);
            RegisterEntity(
                ctx.Registry,
                ctx.Occupancy,
                Team.Player,
                new Vector3Int(3, 0, 1),
                alive: true,
                speedFeet: 25,
                currentHp: 20,
                hasReactiveStrike: true,
                reactionAvailable: true);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, targetPos, alive: true, speedFeet: 25);

            var cell = ctx.Policy.SelectStepCell(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 1);

            Assert.IsTrue(cell.HasValue);
            Assert.AreEqual(5, GridDistancePF2e.DistanceFeetXZ(actorPos, cell.Value), "Step must stay adjacent.");
            Assert.LessOrEqual(
                GridDistancePF2e.DistanceFeetXZ(cell.Value, targetPos),
                5,
                "When no safer step exists, AI should at least step into melee setup.");
        }

        [Test]
        public void SelectStepCell_WhenHazardousOptionExists_PrefersSafeAlternative()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(5, 5));

            var actorPos = new Vector3Int(1, 0, 1);
            var threatPos = new Vector3Int(0, 0, 1);
            var targetPos = new Vector3Int(3, 0, 1);
            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, actorPos, alive: true, speedFeet: 25);
            RegisterEntity(
                ctx.Registry,
                ctx.Occupancy,
                Team.Player,
                threatPos,
                alive: true,
                speedFeet: 25,
                currentHp: 20,
                hasReactiveStrike: true,
                reactionAvailable: true);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, targetPos, alive: true, speedFeet: 25);
            ctx.GridManager.Data.SetCell(new Vector3Int(2, 0, 1), CellData.CreateWalkable(CellTerrain.Hazardous));

            var cell = ctx.Policy.SelectStepCell(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 1);

            Assert.IsTrue(cell.HasValue);
            Assert.AreNotEqual(new Vector3Int(2, 0, 1), cell.Value, "AI should avoid the hazardous step option when a safe alternative exists.");
        }

        [Test]
        public void TrySelectSpellDecision_FearOnUnfrightenedTarget_ReturnsFear()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(5, 5));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

            ctx.Registry.Get(actor).KnowsFear = true;

            bool selected = ctx.Policy.TrySelectSpellDecision(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 2,
                out var decision);

            Assert.IsTrue(selected);
            Assert.AreEqual(SpellId.Fear, decision.spellId);
            Assert.AreEqual(2, decision.actionCount);
            Assert.AreEqual(target, decision.primaryTarget);
        }

        [Test]
        public void TrySelectSpellDecision_UrgentlyWoundedAlly_PicksHealBeforeOffense()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(8, 8));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25, currentHp: 6, maxHp: 6);
            var ally = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(3, 0, 1), alive: true, speedFeet: 25, currentHp: 3, maxHp: 6);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(5, 0, 1), alive: true, speedFeet: 25, currentHp: 10);

            var actorData = ctx.Registry.Get(actor);
            actorData.KnowsHeal = true;
            actorData.KnowsSnowball = true;
            actorData.KnowsFear = true;

            bool selected = ctx.Policy.TrySelectSpellDecision(
                actorData,
                ctx.Registry.Get(target),
                availableActions: 2,
                out var decision);

            Assert.IsTrue(selected);
            Assert.AreEqual(SpellId.Heal, decision.spellId);
            Assert.AreEqual(2, decision.actionCount);
            Assert.AreEqual(ally, decision.primaryTarget);
        }

        [Test]
        public void TrySelectSpellDecision_UrgentlyWoundedUndeadAlly_PicksHarm()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(8, 8));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25, currentHp: 6, maxHp: 6);
            var ally = RegisterEntity(
                ctx.Registry,
                ctx.Occupancy,
                Team.Enemy,
                new Vector3Int(3, 0, 1),
                alive: true,
                speedFeet: 25,
                currentHp: 2,
                maxHp: 12,
                vitalityAffinity: VitalityAffinity.Undead);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(5, 0, 1), alive: true, speedFeet: 25, currentHp: 10);

            var actorData = ctx.Registry.Get(actor);
            actorData.KnowsHarm = true;
            actorData.KnowsSnowball = true;

            bool selected = ctx.Policy.TrySelectSpellDecision(
                actorData,
                ctx.Registry.Get(target),
                availableActions: 2,
                out var decision);

            Assert.IsTrue(selected);
            Assert.AreEqual(SpellId.Harm, decision.spellId);
            Assert.AreEqual(2, decision.actionCount);
            Assert.AreEqual(ally, decision.primaryTarget);
        }

        [Test]
        public void TrySelectSpellDecision_MinorChipDamage_DoesNotSpendTurnOnHeal()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(8, 8));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25, currentHp: 6, maxHp: 6);
            RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(3, 0, 1), alive: true, speedFeet: 25, currentHp: 5, maxHp: 6);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(5, 0, 1), alive: true, speedFeet: 25, currentHp: 10);

            var actorData = ctx.Registry.Get(actor);
            actorData.KnowsHeal = true;
            actorData.KnowsSnowball = true;

            bool selected = ctx.Policy.TrySelectSpellDecision(
                actorData,
                ctx.Registry.Get(target),
                availableActions: 2,
                out var decision);

            Assert.IsTrue(selected);
            Assert.AreEqual(SpellId.Snowball, decision.spellId);
            Assert.AreEqual(target, decision.primaryTarget);
        }

        [Test]
        public void TrySelectDefensiveDecision_OneActionLeftWithPhysicalShield_RaisesShield()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(6, 6));
            var shieldDef = ScriptableObject.CreateInstance<ShieldDefinition>();

            try
            {
                shieldDef.acBonus = 2;
                shieldDef.hardness = 3;
                shieldDef.maxHP = 12;

                var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25);
                var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

                ctx.Registry.Get(actor).EquippedShield = ShieldInstance.CreateEquipped(shieldDef);

                bool selected = ctx.Policy.TrySelectDefensiveDecision(
                    ctx.Registry.Get(actor),
                    ctx.Registry.Get(target),
                    availableActions: 1,
                    out var decision);

                Assert.IsTrue(selected);
                Assert.AreEqual(AIDefensiveActionKind.RaisePhysicalShield, decision.actionKind);
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        [Test]
        public void TrySelectDefensiveDecision_OneActionLeftWithShieldCantrip_PicksStandardShield()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(6, 6));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

            ctx.Registry.Get(actor).KnowsStandardShieldCantrip = true;

            bool selected = ctx.Policy.TrySelectDefensiveDecision(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 1,
                out var decision);

            Assert.IsTrue(selected);
            Assert.AreEqual(AIDefensiveActionKind.CastShieldSpell, decision.actionKind);
            Assert.AreEqual(RaiseShieldSpellMode.Standard, decision.shieldSpellMode);
        }

        [Test]
        public void TrySelectDefensiveDecision_WithMoreThanOneAction_RemainsOffensive()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(6, 6));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

            ctx.Registry.Get(actor).KnowsStandardShieldCantrip = true;

            bool selected = ctx.Policy.TrySelectDefensiveDecision(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 2,
                out _);

            Assert.IsFalse(selected);
        }

        [Test]
        public void TrySelectSkillDecision_TwoActionsAndValidTarget_PicksDemoralize()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(6, 6));

            var actor = RegisterEntity(
                ctx.Registry,
                ctx.Occupancy,
                Team.Enemy,
                new Vector3Int(1, 0, 1),
                alive: true,
                speedFeet: 25,
                charisma: 12,
                intimidationProf: ProficiencyRank.Trained);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(3, 0, 1), alive: true, speedFeet: 25);

            bool selected = ctx.Policy.TrySelectSkillDecision(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 2,
                out var decision);

            Assert.IsTrue(selected);
            Assert.AreEqual(AISkillActionKind.Demoralize, decision.actionKind);
            Assert.AreEqual(target, decision.primaryTarget);
        }

        [Test]
        public void TrySelectSkillDecision_AlreadyFrightenedTarget_SkipsDemoralize()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(6, 6));

            var actor = RegisterEntity(
                ctx.Registry,
                ctx.Occupancy,
                Team.Enemy,
                new Vector3Int(1, 0, 1),
                alive: true,
                speedFeet: 25,
                charisma: 12,
                intimidationProf: ProficiencyRank.Trained);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);
            ctx.Registry.Get(target).Conditions.Add(new ActiveCondition(ConditionType.Frightened, value: 1, remainingRounds: -1));

            bool selected = ctx.Policy.TrySelectSkillDecision(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 3,
                out _);

            Assert.IsFalse(selected);
        }

        [Test]
        public void TrySelectSkillDecision_NegativeIntimidationModifier_SkipsDemoralize()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(6, 6));

            var actor = RegisterEntity(
                ctx.Registry,
                ctx.Occupancy,
                Team.Enemy,
                new Vector3Int(1, 0, 1),
                alive: true,
                speedFeet: 25,
                charisma: 8,
                intimidationProf: ProficiencyRank.Untrained);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

            bool selected = ctx.Policy.TrySelectSkillDecision(
                ctx.Registry.Get(actor),
                ctx.Registry.Get(target),
                availableActions: 3,
                out _);

            Assert.IsFalse(selected);
        }

        [Test]
        public void TrySelectSkillDecision_TripWeaponOnStandingTarget_PicksTrip()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(6, 6));
            var tripWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();

            try
            {
                tripWeapon.category = WeaponCategory.Martial;
                tripWeapon.reachFeet = 5;
                tripWeapon.isRanged = false;
                tripWeapon.traits = WeaponTraitFlags.Trip;

                var actor = RegisterEntity(
                    ctx.Registry,
                    ctx.Occupancy,
                    Team.Enemy,
                    new Vector3Int(1, 0, 1),
                    alive: true,
                    speedFeet: 25);
                var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

                var actorData = ctx.Registry.Get(actor);
                actorData.Level = 1;
                actorData.Strength = 16;
                actorData.AthleticsProf = ProficiencyRank.Trained;
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = tripWeapon,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                bool selected = ctx.Policy.TrySelectSkillDecision(
                    actorData,
                    ctx.Registry.Get(target),
                    availableActions: 3,
                    out var decision);

                Assert.IsTrue(selected);
                Assert.AreEqual(AISkillActionKind.Trip, decision.actionKind);
                Assert.AreEqual(target, decision.primaryTarget);
            }
            finally
            {
                Object.DestroyImmediate(tripWeapon);
            }
        }

        [Test]
        public void TrySelectSkillDecision_GrappleWeaponOnProneTarget_PicksGrapple()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(6, 6));
            var grappleWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();

            try
            {
                grappleWeapon.category = WeaponCategory.Martial;
                grappleWeapon.reachFeet = 5;
                grappleWeapon.isRanged = false;
                grappleWeapon.traits = WeaponTraitFlags.Grapple;

                var actor = RegisterEntity(
                    ctx.Registry,
                    ctx.Occupancy,
                    Team.Enemy,
                    new Vector3Int(1, 0, 1),
                    alive: true,
                    speedFeet: 25);
                var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);
                ctx.Registry.Get(target).Conditions.Add(new ActiveCondition(ConditionType.Prone, value: 0, remainingRounds: -1));

                var actorData = ctx.Registry.Get(actor);
                actorData.Level = 1;
                actorData.Strength = 16;
                actorData.AthleticsProf = ProficiencyRank.Trained;
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = grappleWeapon,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                bool selected = ctx.Policy.TrySelectSkillDecision(
                    actorData,
                    ctx.Registry.Get(target),
                    availableActions: 3,
                    out var decision);

                Assert.IsTrue(selected);
                Assert.AreEqual(AISkillActionKind.Grapple, decision.actionKind);
                Assert.AreEqual(target, decision.primaryTarget);
            }
            finally
            {
                Object.DestroyImmediate(grappleWeapon);
            }
        }

        [Test]
        public void TrySelectSkillDecision_MapApplied_FallsBackToDemoralize()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(6, 6));
            var tripWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();

            try
            {
                tripWeapon.category = WeaponCategory.Martial;
                tripWeapon.reachFeet = 5;
                tripWeapon.isRanged = false;
                tripWeapon.traits = WeaponTraitFlags.Trip;

                var actor = RegisterEntity(
                    ctx.Registry,
                    ctx.Occupancy,
                    Team.Enemy,
                    new Vector3Int(1, 0, 1),
                    alive: true,
                    speedFeet: 25,
                    charisma: 12,
                    intimidationProf: ProficiencyRank.Trained);
                var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

                var actorData = ctx.Registry.Get(actor);
                actorData.Level = 1;
                actorData.Strength = 16;
                actorData.AthleticsProf = ProficiencyRank.Trained;
                actorData.MAPCount = 1;
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = tripWeapon,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                bool selected = ctx.Policy.TrySelectSkillDecision(
                    actorData,
                    ctx.Registry.Get(target),
                    availableActions: 2,
                    out var decision);

                Assert.IsTrue(selected);
                Assert.AreEqual(AISkillActionKind.Demoralize, decision.actionKind);
                Assert.AreEqual(target, decision.primaryTarget);
            }
            finally
            {
                Object.DestroyImmediate(tripWeapon);
            }
        }

        [Test]
        public void TrySelectSkillDecision_ReachShoveTargetStillInReach_PicksShove()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(8, 8));
            var shoveWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();

            try
            {
                shoveWeapon.category = WeaponCategory.Martial;
                shoveWeapon.reachFeet = 10;
                shoveWeapon.isRanged = false;
                shoveWeapon.traits = WeaponTraitFlags.Shove;

                var actor = RegisterEntity(
                    ctx.Registry,
                    ctx.Occupancy,
                    Team.Enemy,
                    new Vector3Int(1, 0, 1),
                    alive: true,
                    speedFeet: 25,
                    charisma: 12,
                    intimidationProf: ProficiencyRank.Trained);
                var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

                var actorData = ctx.Registry.Get(actor);
                actorData.Level = 1;
                actorData.Strength = 16;
                actorData.AthleticsProf = ProficiencyRank.Trained;
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = shoveWeapon,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                bool selected = ctx.Policy.TrySelectSkillDecision(
                    actorData,
                    ctx.Registry.Get(target),
                    availableActions: 3,
                    out var decision);

                Assert.IsTrue(selected);
                Assert.AreEqual(AISkillActionKind.Shove, decision.actionKind);
                Assert.AreEqual(target, decision.primaryTarget);
            }
            finally
            {
                Object.DestroyImmediate(shoveWeapon);
            }
        }

        [Test]
        public void TrySelectSkillDecision_RepositionOpensAdditionalEnemyThreat_PicksReposition()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(8, 8));
            var repositionWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            var allyWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();

            try
            {
                repositionWeapon.category = WeaponCategory.Martial;
                repositionWeapon.reachFeet = 10;
                repositionWeapon.isRanged = false;
                repositionWeapon.traits = WeaponTraitFlags.Reposition;

                allyWeapon.category = WeaponCategory.Martial;
                allyWeapon.reachFeet = 5;
                allyWeapon.isRanged = false;

                var actor = RegisterEntity(
                    ctx.Registry,
                    ctx.Occupancy,
                    Team.Enemy,
                    new Vector3Int(1, 0, 1),
                    alive: true,
                    speedFeet: 25);
                var ally = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(4, 0, 1), alive: true, speedFeet: 25);
                var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

                var actorData = ctx.Registry.Get(actor);
                actorData.Level = 1;
                actorData.Strength = 16;
                actorData.AthleticsProf = ProficiencyRank.Trained;
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = repositionWeapon,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                ctx.Registry.Get(ally).EquippedWeapon = new WeaponInstance
                {
                    def = allyWeapon,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                bool selected = ctx.Policy.TrySelectSkillDecision(
                    actorData,
                    ctx.Registry.Get(target),
                    availableActions: 3,
                    out var decision);

                Assert.IsTrue(selected);
                Assert.AreEqual(AISkillActionKind.Reposition, decision.actionKind);
                Assert.AreEqual(target, decision.primaryTarget);
                Assert.IsTrue(decision.HasDestinationCell);
                Assert.AreEqual(new Vector3Int(3, 0, 0), decision.destinationCell);
            }
            finally
            {
                Object.DestroyImmediate(repositionWeapon);
                Object.DestroyImmediate(allyWeapon);
            }
        }

        [Test]
        public void TrySelectSkillDecision_FiveFootShoveFallsBackToDemoralize()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(8, 8));
            var shoveWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();

            try
            {
                shoveWeapon.category = WeaponCategory.Martial;
                shoveWeapon.reachFeet = 5;
                shoveWeapon.isRanged = false;
                shoveWeapon.traits = WeaponTraitFlags.Shove;

                var actor = RegisterEntity(
                    ctx.Registry,
                    ctx.Occupancy,
                    Team.Enemy,
                    new Vector3Int(1, 0, 1),
                    alive: true,
                    speedFeet: 25,
                    charisma: 12,
                    intimidationProf: ProficiencyRank.Trained);
                var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

                var actorData = ctx.Registry.Get(actor);
                actorData.Level = 1;
                actorData.Strength = 16;
                actorData.AthleticsProf = ProficiencyRank.Trained;
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = shoveWeapon,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                bool selected = ctx.Policy.TrySelectSkillDecision(
                    actorData,
                    ctx.Registry.Get(target),
                    availableActions: 3,
                    out var decision);

                Assert.IsTrue(selected);
                Assert.AreEqual(AISkillActionKind.Demoralize, decision.actionKind);
                Assert.AreEqual(target, decision.primaryTarget);
            }
            finally
            {
                Object.DestroyImmediate(shoveWeapon);
            }
        }

        [Test]
        public void TrySelectSkillDecision_HazardousFiveFootShove_PicksShove()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(8, 8));
            var shoveWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();

            try
            {
                shoveWeapon.category = WeaponCategory.Martial;
                shoveWeapon.reachFeet = 5;
                shoveWeapon.isRanged = false;
                shoveWeapon.traits = WeaponTraitFlags.Shove;

                var actor = RegisterEntity(
                    ctx.Registry,
                    ctx.Occupancy,
                    Team.Enemy,
                    new Vector3Int(1, 0, 1),
                    alive: true,
                    speedFeet: 25,
                    charisma: 12,
                    intimidationProf: ProficiencyRank.Trained);
                var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);
                ctx.GridManager.Data.SetCell(new Vector3Int(3, 0, 1), CellData.CreateWalkable(CellTerrain.Hazardous));

                var actorData = ctx.Registry.Get(actor);
                actorData.Level = 1;
                actorData.Strength = 16;
                actorData.AthleticsProf = ProficiencyRank.Trained;
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = shoveWeapon,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                bool selected = ctx.Policy.TrySelectSkillDecision(
                    actorData,
                    ctx.Registry.Get(target),
                    availableActions: 3,
                    out var decision);

                Assert.IsTrue(selected);
                Assert.AreEqual(AISkillActionKind.Shove, decision.actionKind);
            }
            finally
            {
                Object.DestroyImmediate(shoveWeapon);
            }
        }

        [Test]
        public void TrySelectSkillDecision_HazardousRepositionWithoutExtraThreat_PicksReposition()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(8, 8));
            var repositionWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();

            try
            {
                repositionWeapon.category = WeaponCategory.Martial;
                repositionWeapon.reachFeet = 10;
                repositionWeapon.isRanged = false;
                repositionWeapon.traits = WeaponTraitFlags.Reposition;

                var actor = RegisterEntity(
                    ctx.Registry,
                    ctx.Occupancy,
                    Team.Enemy,
                    new Vector3Int(1, 0, 1),
                    alive: true,
                    speedFeet: 25);
                var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);
                ctx.GridManager.Data.SetCell(new Vector3Int(3, 0, 1), CellData.CreateWalkable(CellTerrain.Hazardous));

                var actorData = ctx.Registry.Get(actor);
                actorData.Level = 1;
                actorData.Strength = 16;
                actorData.AthleticsProf = ProficiencyRank.Trained;
                actorData.EquippedWeapon = new WeaponInstance
                {
                    def = repositionWeapon,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                bool selected = ctx.Policy.TrySelectSkillDecision(
                    actorData,
                    ctx.Registry.Get(target),
                    availableActions: 3,
                    out var decision);

                Assert.IsTrue(selected);
                Assert.AreEqual(AISkillActionKind.Reposition, decision.actionKind);
                Assert.AreEqual(new Vector3Int(3, 0, 1), decision.destinationCell);
            }
            finally
            {
                Object.DestroyImmediate(repositionWeapon);
            }
        }

        [Test]
        public void TrySelectSpellDecision_ControlledMeleeTargetAfterAttack_SkipsFear()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(6, 6));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

            var actorData = ctx.Registry.Get(actor);
            actorData.KnowsFear = true;
            actorData.MAPCount = 1;
            ctx.Registry.Get(target).Conditions.Add(new ActiveCondition(ConditionType.Prone, value: 0, remainingRounds: -1));

            bool selected = ctx.Policy.TrySelectSpellDecision(
                actorData,
                ctx.Registry.Get(target),
                availableActions: 2,
                out _);

            Assert.IsFalse(selected);
        }

        [Test]
        public void TrySelectDefensiveDecision_ControlledMeleeTargetAfterAttack_SkipsShieldFallback()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(6, 6));
            var shieldDef = ScriptableObject.CreateInstance<ShieldDefinition>();

            try
            {
                shieldDef.acBonus = 2;
                shieldDef.hardness = 3;
                shieldDef.maxHP = 12;

                var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25, currentHp: 8, maxHp: 20);
                var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

                var actorData = ctx.Registry.Get(actor);
                actorData.MAPCount = 1;
                actorData.EquippedShield = ShieldInstance.CreateEquipped(shieldDef);
                ctx.Registry.Get(target).Conditions.Add(new ActiveCondition(ConditionType.Prone, value: 0, remainingRounds: -1));

                bool selected = ctx.Policy.TrySelectDefensiveDecision(
                    actorData,
                    ctx.Registry.Get(target),
                    availableActions: 1,
                    out _);

                Assert.IsFalse(selected);
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        [Test]
        public void TrySelectSkillDecision_ControlledMeleeTargetAfterAttack_SkipsDemoralize()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(6, 6));

            var actor = RegisterEntity(
                ctx.Registry,
                ctx.Occupancy,
                Team.Enemy,
                new Vector3Int(1, 0, 1),
                alive: true,
                speedFeet: 25,
                charisma: 12,
                intimidationProf: ProficiencyRank.Trained);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

            var actorData = ctx.Registry.Get(actor);
            actorData.MAPCount = 1;
            ctx.Registry.Get(target).Conditions.Add(new ActiveCondition(ConditionType.Prone, value: 0, remainingRounds: -1));

            bool selected = ctx.Policy.TrySelectSkillDecision(
                actorData,
                ctx.Registry.Get(target),
                availableActions: 2,
                out _);

            Assert.IsFalse(selected);
        }

        [Test]
        public void TrySelectSpellDecision_TwoValidArcTargets_PrefersElectricArc()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(8, 8));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(4, 0, 1), alive: true, speedFeet: 25, currentHp: 10);
            var second = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(4, 0, 2), alive: true, speedFeet: 25, currentHp: 6);

            var actorData = ctx.Registry.Get(actor);
            actorData.KnowsElectricArc = true;
            actorData.KnowsSnowball = true;
            actorData.KnowsForceBarrage = true;

            bool selected = ctx.Policy.TrySelectSpellDecision(
                actorData,
                ctx.Registry.Get(target),
                availableActions: 3,
                out var decision);

            Assert.IsTrue(selected);
            Assert.AreEqual(SpellId.ElectricArc, decision.spellId);
            Assert.AreEqual(2, decision.actionCount);
            Assert.AreEqual(target, decision.primaryTarget);
            Assert.AreEqual(second, decision.secondaryTarget);
        }

        [Test]
        public void TrySelectSpellDecision_TwoTargetsInCleanCone_PrefersBurningHands()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(8, 8));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(1, 0, 3), alive: true, speedFeet: 25, currentHp: 12);
            RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 3), alive: true, speedFeet: 25, currentHp: 8);

            var actorData = ctx.Registry.Get(actor);
            actorData.KnowsBurningHands = true;
            actorData.KnowsElectricArc = true;
            actorData.KnowsSnowball = true;

            bool selected = ctx.Policy.TrySelectSpellDecision(
                actorData,
                ctx.Registry.Get(target),
                availableActions: 2,
                out var decision);

            Assert.IsTrue(selected);
            Assert.AreEqual(SpellId.BurningHands, decision.spellId);
            Assert.AreEqual(2, decision.actionCount);
            Assert.AreEqual(target, decision.primaryTarget);
            Assert.IsTrue(decision.HasAimCell);
        }

        [Test]
        public void TrySelectSpellDecision_AlliesInBurningHandsCone_FallsBackToSnowball()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(8, 8));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(1, 0, 3), alive: true, speedFeet: 25, currentHp: 12);
            RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 3), alive: true, speedFeet: 25, currentHp: 8);
            RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(0, 0, 3), alive: true, speedFeet: 25, currentHp: 10);

            var actorData = ctx.Registry.Get(actor);
            actorData.KnowsBurningHands = true;
            actorData.KnowsSnowball = true;

            bool selected = ctx.Policy.TrySelectSpellDecision(
                actorData,
                ctx.Registry.Get(target),
                availableActions: 2,
                out var decision);

            Assert.IsTrue(selected);
            Assert.AreEqual(SpellId.Snowball, decision.spellId);
            Assert.AreEqual(target, decision.primaryTarget);
        }

        [Test]
        public void TrySelectSpellDecision_SingleRangedTarget_FallsBackToSnowball()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(8, 8));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(4, 0, 1), alive: true, speedFeet: 25, currentHp: 10);

            var actorData = ctx.Registry.Get(actor);
            actorData.KnowsSnowball = true;
            actorData.KnowsForceBarrage = true;

            bool selected = ctx.Policy.TrySelectSpellDecision(
                actorData,
                ctx.Registry.Get(target),
                availableActions: 2,
                out var decision);

            Assert.IsTrue(selected);
            Assert.AreEqual(SpellId.Snowball, decision.spellId);
            Assert.AreEqual(2, decision.actionCount);
            Assert.AreEqual(target, decision.primaryTarget);
        }

        [Test]
        public void TrySelectSpellDecision_OnlyForceBarrageAvailable_UsesRemainingActions()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(8, 8));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(6, 0, 1), alive: true, speedFeet: 25, currentHp: 10);

            var actorData = ctx.Registry.Get(actor);
            actorData.KnowsForceBarrage = true;

            bool selected = ctx.Policy.TrySelectSpellDecision(
                actorData,
                ctx.Registry.Get(target),
                availableActions: 3,
                out var decision);

            Assert.IsTrue(selected);
            Assert.AreEqual(SpellId.ForceBarrage, decision.spellId);
            Assert.AreEqual(3, decision.actionCount);
            Assert.AreEqual(target, decision.primaryTarget);
        }

        [Test]
        public void TrySelectSpellDecision_InMeleeWithAlreadyFrightenedTarget_DoesNotPickRangedSpell()
        {
            using var ctx = new PolicyContext(CreateSquareGrid(5, 5));

            var actor = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Enemy, new Vector3Int(1, 0, 1), alive: true, speedFeet: 25);
            var target = RegisterEntity(ctx.Registry, ctx.Occupancy, Team.Player, new Vector3Int(2, 0, 1), alive: true, speedFeet: 25);

            var actorData = ctx.Registry.Get(actor);
            actorData.KnowsFear = true;
            actorData.KnowsSnowball = true;
            actorData.KnowsForceBarrage = true;
            ctx.Registry.Get(target).Conditions.Add(new ActiveCondition(ConditionType.Frightened, value: 1, remainingRounds: -1));

            bool selected = ctx.Policy.TrySelectSpellDecision(
                actorData,
                ctx.Registry.Get(target),
                availableActions: 3,
                out _);

            Assert.IsFalse(selected);
        }

        private static EntityHandle RegisterEntity(
            EntityRegistry registry,
            OccupancyMap occupancy,
            Team team,
            Vector3Int pos,
            bool alive = true,
            int speedFeet = 25,
            int currentHp = 20,
            int maxHp = 20,
            bool hasReactiveStrike = false,
            bool reactionAvailable = true,
            VitalityAffinity vitalityAffinity = VitalityAffinity.Living,
            int charisma = 10,
            ProficiencyRank intimidationProf = ProficiencyRank.Untrained)
        {
            var data = new EntityData
            {
                Name = $"{team}_{pos}",
                Team = team,
                Size = CreatureSize.Medium,
                Charisma = charisma,
                IntimidationProf = intimidationProf,
                MaxHP = Mathf.Max(1, maxHp),
                CurrentHP = alive ? Mathf.Clamp(currentHp, 1, Mathf.Max(1, maxHp)) : 0,
                Speed = speedFeet,
                GridPosition = pos,
                HasReactiveStrike = hasReactiveStrike,
                ReactionAvailable = reactionAvailable,
                VitalityAffinity = vitalityAffinity,
                EquippedWeapon = new WeaponInstance
                {
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                }
            };

            var handle = registry.Register(data);
            occupancy.Place(handle, pos, data.SizeCells);
            return handle;
        }

        private static GridData CreateLineGrid(int length)
        {
            var grid = new GridData(1f, 1f, 16);
            for (int x = 0; x < length; x++)
                grid.SetCell(new Vector3Int(x, 0, 0), CellData.CreateWalkable());
            return grid;
        }

        private static GridData CreateSquareGrid(int width, int height)
        {
            var grid = new GridData(1f, 1f, 16);
            for (int x = 0; x < width; x++)
                for (int z = 0; z < height; z++)
                    grid.SetCell(new Vector3Int(x, 0, z), CellData.CreateWalkable());
            return grid;
        }

        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            string fieldName = $"<{propertyName}>k__BackingField";
            var field = target.GetType().GetField(fieldName, flags);
            Assert.IsNotNull(field, $"Missing backing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private sealed class PolicyContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            public readonly GameObject GridGo;
            public readonly GameObject EntityGo;
            public readonly GridManager GridManager;
            public readonly EntityRegistry Registry;
            public readonly OccupancyMap Occupancy;
            public readonly SimpleMeleeDecisionPolicy Policy;

            public PolicyContext(GridData gridData)
            {
                // Harness objects intentionally skip inspector wiring.
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                GridGo = new GameObject("GridManager_Test");
                GridManager = GridGo.AddComponent<GridManager>();
                SetAutoPropertyBackingField(GridManager, "Data", gridData);

                EntityGo = new GameObject("EntityManager_Test");
                var entityManager = EntityGo.AddComponent<EntityManager>();

                Registry = new EntityRegistry();
                Occupancy = new OccupancyMap(Registry);
                var pathfinding = new GridPathfinding();

                SetAutoPropertyBackingField(entityManager, "Registry", Registry);
                SetAutoPropertyBackingField(entityManager, "Occupancy", Occupancy);
                SetAutoPropertyBackingField(entityManager, "Pathfinding", pathfinding);

                Policy = new SimpleMeleeDecisionPolicy(entityManager, GridManager);
            }

            public void Dispose()
            {
                if (EntityGo != null) Object.DestroyImmediate(EntityGo);
                if (GridGo != null) Object.DestroyImmediate(GridGo);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }
    }
}
