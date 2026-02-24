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
    public class StrikePhaseTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void StrikePhaseResult_FromAttackRoll_SetsPreHitFields()
        {
            var result = StrikePhaseResult.FromAttackRoll(
                new EntityHandle(1),
                new EntityHandle(2),
                "Sword",
                naturalRoll: 14,
                attackBonus: 7,
                mapPenalty: -5,
                total: 16);

            Assert.AreEqual(1, result.attacker.Id);
            Assert.AreEqual(2, result.target.Id);
            Assert.AreEqual("Sword", result.weaponName);
            Assert.AreEqual(14, result.naturalRoll);
            Assert.AreEqual(7, result.attackBonus);
            Assert.AreEqual(-5, result.mapPenalty);
            Assert.AreEqual(0, result.rangePenalty);
            Assert.AreEqual(16, result.total);
            Assert.AreEqual(0, result.dc);
            Assert.AreEqual(DegreeOfSuccess.CriticalFailure, result.degree);
            Assert.AreEqual(0, result.damageRolled);
            Assert.IsFalse(result.damageDealt);
        }

        [Test]
        public void StrikePhaseResult_WithHitAndDamage_PreservesPreHitFields()
        {
            var baseResult = StrikePhaseResult.FromAttackRoll(
                new EntityHandle(10),
                new EntityHandle(11),
                "Axe",
                naturalRoll: 9,
                attackBonus: 6,
                mapPenalty: 0,
                total: 15);

            var resolved = baseResult.WithHitAndDamage(
                dc: 18,
                degree: DegreeOfSuccess.Failure,
                damageRolled: 0,
                damageType: DamageType.Slashing,
                damageDealt: false);

            Assert.AreEqual(baseResult.attacker, resolved.attacker);
            Assert.AreEqual(baseResult.target, resolved.target);
            Assert.AreEqual(baseResult.weaponName, resolved.weaponName);
            Assert.AreEqual(baseResult.naturalRoll, resolved.naturalRoll);
            Assert.AreEqual(baseResult.total, resolved.total);
            Assert.AreEqual(baseResult.rangePenalty, resolved.rangePenalty);
            Assert.AreEqual(18, resolved.dc);
            Assert.AreEqual(DegreeOfSuccess.Failure, resolved.degree);
            Assert.AreEqual(0, resolved.damageRolled);
            Assert.AreEqual(DamageType.Slashing, resolved.damageType);
        }

        [Test]
        public void ResolveAttackRoll_InvalidStrike_ReturnsNullAndDoesNotIncrementMap()
        {
            using var ctx = new StrikeContext();
            var weapon = ctx.CreateWeaponDef();

            var attacker = ctx.RegisterEntity(Team.Player, new Vector3Int(0, 0, 0), weaponDef: weapon, level: 1, strength: 10);
            var target = ctx.RegisterEntity(Team.Player, new Vector3Int(1, 0, 0), weaponDef: weapon, level: 1, strength: 10);

            var attackerData = ctx.Registry.Get(attacker);
            Assert.AreEqual(0, attackerData.MAPCount);

            var phase = ctx.StrikeAction.ResolveAttackRoll(attacker, target, new FixedRng(new[] { 10 }));
            Assert.IsFalse(phase.HasValue);
            Assert.AreEqual(0, attackerData.MAPCount);
        }

        [Test]
        public void ResolveAttackRoll_Success_ReturnsRollDataAndIncrementsMap()
        {
            using var ctx = new StrikeContext();
            var weapon = ctx.CreateWeaponDef();

            var attacker = ctx.RegisterEntity(Team.Player, new Vector3Int(0, 0, 0), weaponDef: weapon, level: 1, strength: 10);
            var target = ctx.RegisterEntity(Team.Enemy, new Vector3Int(1, 0, 0), weaponDef: weapon, level: 1, strength: 10);

            var attackerData = ctx.Registry.Get(attacker);
            int expectedAttackBonus = attackerData.GetAttackBonus(attackerData.EquippedWeapon);
            int expectedMap = attackerData.GetMAPPenalty(attackerData.EquippedWeapon);

            var phase = ctx.StrikeAction.ResolveAttackRoll(attacker, target, new FixedRng(new[] { 12 }));
            Assert.IsTrue(phase.HasValue);

            var result = phase.Value;
            Assert.AreEqual(12, result.naturalRoll);
            Assert.AreEqual(expectedAttackBonus, result.attackBonus);
            Assert.AreEqual(expectedMap, result.mapPenalty);
            Assert.AreEqual(0, result.rangePenalty);
            Assert.AreEqual(12 + expectedAttackBonus + expectedMap, result.total);
            Assert.AreEqual(1, attackerData.MAPCount);
            Assert.AreEqual(0, result.dc);
            Assert.IsFalse(result.damageDealt);
        }

        [Test]
        public void ResolveAttackRoll_RangedWeapon_FirstIncrement_NoRangePenalty()
        {
            using var ctx = new StrikeContext();
            var bow = ctx.CreateWeaponDef(isRanged: true, rangeIncrementFeet: 60, maxRangeIncrements: 6);

            var attacker = ctx.RegisterEntity(Team.Player, new Vector3Int(0, 0, 0), weaponDef: bow, level: 1, dexterity: 16);
            var target = ctx.RegisterEntity(Team.Enemy, new Vector3Int(12, 0, 0), weaponDef: bow, level: 1); // 60 ft

            var attackerData = ctx.Registry.Get(attacker);
            int expectedAttackBonus = attackerData.GetAttackBonus(attackerData.EquippedWeapon);
            int expectedMap = attackerData.GetMAPPenalty(attackerData.EquippedWeapon);

            var phase = ctx.StrikeAction.ResolveAttackRoll(attacker, target, new FixedRng(new[] { 11 }));
            Assert.IsTrue(phase.HasValue);

            var result = phase.Value;
            Assert.AreEqual(0, result.rangePenalty);
            Assert.AreEqual(11 + expectedAttackBonus + expectedMap, result.total);
        }

        [Test]
        public void ResolveAttackRoll_RangedWeapon_SecondIncrement_AppliesRangePenalty_AndStacksWithMap()
        {
            using var ctx = new StrikeContext();
            var bow = ctx.CreateWeaponDef(isRanged: true, rangeIncrementFeet: 60, maxRangeIncrements: 6);

            var attacker = ctx.RegisterEntity(Team.Player, new Vector3Int(0, 0, 0), weaponDef: bow, level: 1, dexterity: 16);
            var target = ctx.RegisterEntity(Team.Enemy, new Vector3Int(13, 0, 0), weaponDef: bow, level: 1); // 65 ft

            var attackerData = ctx.Registry.Get(attacker);
            attackerData.MAPCount = 1;
            int expectedAttackBonus = attackerData.GetAttackBonus(attackerData.EquippedWeapon);
            int expectedMap = attackerData.GetMAPPenalty(attackerData.EquippedWeapon);

            var phase = ctx.StrikeAction.ResolveAttackRoll(attacker, target, new FixedRng(new[] { 11 }));
            Assert.IsTrue(phase.HasValue);

            var result = phase.Value;
            Assert.AreEqual(-2, result.rangePenalty);
            Assert.AreEqual(11 + expectedAttackBonus + expectedMap - 2, result.total);
            Assert.AreEqual(2, attackerData.MAPCount); // incremented from pre-set 1
        }

        [Test]
        public void ResolveAttackRoll_RangedWeapon_MaxRangeBoundary_Allowed_BeyondMaxRangeRejected()
        {
            using var ctx = new StrikeContext();
            var bow = ctx.CreateWeaponDef(isRanged: true, rangeIncrementFeet: 60, maxRangeIncrements: 2);

            var attacker = ctx.RegisterEntity(Team.Player, new Vector3Int(0, 0, 0), weaponDef: bow, level: 1, dexterity: 16);
            var targetAtMax = ctx.RegisterEntity(Team.Enemy, new Vector3Int(24, 1, 0), weaponDef: bow, level: 1); // 120 ft, different elevation
            var targetBeyond = ctx.RegisterEntity(Team.Enemy, new Vector3Int(25, 2, 0), weaponDef: bow, level: 1); // 125 ft, different elevation

            var atMax = ctx.StrikeAction.ResolveAttackRoll(attacker, targetAtMax, new FixedRng(new[] { 10 }));
            Assert.IsTrue(atMax.HasValue, "Ranged strike should allow target at max range and ignore elevation mismatch.");

            var beyond = ctx.StrikeAction.ResolveAttackRoll(attacker, targetBeyond, new FixedRng(new[] { 10 }));
            Assert.IsFalse(beyond.HasValue);
        }

        [Test]
        public void GetStrikeTargetFailure_UnarmedFallsBackToMeleeElevationValidation()
        {
            using var ctx = new StrikeContext();
            var enemyWeapon = ctx.CreateWeaponDef();

            var attacker = ctx.RegisterEntity(Team.Player, new Vector3Int(0, 0, 0), weaponDef: null, level: 1);
            var target = ctx.RegisterEntity(Team.Enemy, new Vector3Int(1, 1, 0), weaponDef: enemyWeapon, level: 1);

            var reason = ctx.StrikeAction.GetStrikeTargetFailure(attacker, target);
            Assert.AreEqual(TargetingFailureReason.WrongElevation, reason);
        }

        [Test]
        public void ResolveAttackRoll_UnarmedOrNullWeapon_UsesMeleePath()
        {
            using var ctx = new StrikeContext();
            var enemyWeapon = ctx.CreateWeaponDef(isRanged: true, rangeIncrementFeet: 60, maxRangeIncrements: 6);

            var attacker = ctx.RegisterEntity(Team.Player, new Vector3Int(0, 0, 0), weaponDef: null, level: 1);
            var targetDifferentElevationAdjacent = ctx.RegisterEntity(Team.Enemy, new Vector3Int(1, 1, 0), weaponDef: enemyWeapon, level: 1);
            var targetSameElevationAdjacent = ctx.RegisterEntity(Team.Enemy, new Vector3Int(1, 0, 0), weaponDef: enemyWeapon, level: 1);

            var rejected = ctx.StrikeAction.ResolveAttackRoll(attacker, targetDifferentElevationAdjacent, new FixedRng(new[] { 10 }));
            Assert.IsFalse(rejected.HasValue, "Unarmed strike should still use melee validation (elevation matters).");

            var accepted = ctx.StrikeAction.ResolveAttackRoll(attacker, targetSameElevationAdjacent, new FixedRng(new[] { 10 }));
            Assert.IsTrue(accepted.HasValue, "Unarmed strike should follow melee path and work in adjacent same-elevation target.");
            Assert.AreEqual(0, accepted.Value.rangePenalty);
        }

        [Test]
        public void DetermineHitAndDamage_UsesCurrentTargetAcAndPublishesPreDamage()
        {
            using var ctx = new StrikeContext();
            var weapon = ctx.CreateWeaponDef(diceCount: 1, dieSides: 6);
            var shield = ctx.CreateShieldDef(acBonus: 2, hardness: 3, maxHP: 12);

            var attacker = ctx.RegisterEntity(Team.Player, new Vector3Int(0, 0, 0), weaponDef: weapon, level: 1, strength: 10);
            var target = ctx.RegisterEntity(
                Team.Enemy,
                new Vector3Int(1, 0, 0),
                weaponDef: weapon,
                level: 1,
                strength: 10,
                shield: ShieldInstance.CreateEquipped(shield));

            var phase = ctx.StrikeAction.ResolveAttackRoll(attacker, target, new FixedRng(new[] { 10 }, new[] { 3 }));
            Assert.IsTrue(phase.HasValue);

            // Simulate pre-hit state change between phase 1 and phase 3.
            var targetData = ctx.Registry.Get(target);
            targetData.SetShieldRaised(true);

            int preDamageCount = 0;
            StrikePreDamageEvent preDamageEvent = default;
            ctx.EventBus.OnStrikePreDamageTyped += OnPreDamage;

            try
            {
                var resolved = ctx.StrikeAction.DetermineHitAndDamage(
                    phase.Value,
                    target,
                    new FixedRng(new[] { 10 }, new[] { 3 }));

                Assert.AreEqual(targetData.EffectiveAC, resolved.dc);
                Assert.AreEqual(1, preDamageCount);
                Assert.AreEqual(resolved.total, preDamageEvent.total);
                Assert.AreEqual(resolved.dc, preDamageEvent.dc);
                Assert.AreEqual(resolved.damageRolled, preDamageEvent.damageRolled);
            }
            finally
            {
                ctx.EventBus.OnStrikePreDamageTyped -= OnPreDamage;
            }

            void OnPreDamage(in StrikePreDamageEvent e)
            {
                preDamageCount++;
                preDamageEvent = e;
            }
        }

        [Test]
        public void DetermineHitAndDamage_NaturalOne_DowngradesDegree()
        {
            using var ctx = new StrikeContext();
            var weapon = ctx.CreateWeaponDef();

            var attacker = ctx.RegisterEntity(Team.Player, new Vector3Int(0, 0, 0), weaponDef: weapon, level: 10, strength: 18);
            var target = ctx.RegisterEntity(Team.Enemy, new Vector3Int(1, 0, 0), weaponDef: weapon, level: 1, strength: 10);

            var phase = ctx.StrikeAction.ResolveAttackRoll(attacker, target, new FixedRng(new[] { 1 }));
            Assert.IsTrue(phase.HasValue);

            var resolved = ctx.StrikeAction.DetermineHitAndDamage(phase.Value, target, new FixedRng(new[] { 1 }));
            Assert.AreEqual(DegreeOfSuccess.Failure, resolved.degree);
            Assert.IsFalse(resolved.damageDealt);
        }

        [Test]
        public void ApplyStrikeDamage_WithReduction_AppliesClampedDamageAndPublishesResolvedEvent()
        {
            using var ctx = new StrikeContext();
            var weapon = ctx.CreateWeaponDef();
            var attacker = ctx.RegisterEntity(Team.Player, new Vector3Int(0, 0, 0), weaponDef: weapon);
            var target = ctx.RegisterEntity(Team.Enemy, new Vector3Int(1, 0, 0), weaponDef: weapon, currentHp: 20);

            var phase = StrikePhaseResult.FromAttackRoll(attacker, target, "Sword", 12, 8, 0, 20)
                .WithHitAndDamage(15, DegreeOfSuccess.Success, 7, DamageType.Slashing, damageDealt: true);

            int resolvedCount = 0;
            StrikeResolvedEvent resolvedEvent = default;
            ctx.EventBus.OnStrikeResolved += OnResolved;

            try
            {
                bool applied = ctx.StrikeAction.ApplyStrikeDamage(phase, damageReduction: 3);
                Assert.IsTrue(applied);

                var targetData = ctx.Registry.Get(target);
                Assert.AreEqual(16, targetData.CurrentHP);
                Assert.AreEqual(1, resolvedCount);
                Assert.AreEqual(4, resolvedEvent.damage);
                Assert.AreEqual(20, resolvedEvent.hpBefore);
                Assert.AreEqual(16, resolvedEvent.hpAfter);
            }
            finally
            {
                ctx.EventBus.OnStrikeResolved -= OnResolved;
            }

            void OnResolved(in StrikeResolvedEvent e)
            {
                resolvedCount++;
                resolvedEvent = e;
            }
        }

        [Test]
        public void ApplyStrikeDamage_ReductionAtLeastDamage_LeavesHpUnchanged()
        {
            using var ctx = new StrikeContext();
            var weapon = ctx.CreateWeaponDef();
            var attacker = ctx.RegisterEntity(Team.Player, new Vector3Int(0, 0, 0), weaponDef: weapon);
            var target = ctx.RegisterEntity(Team.Enemy, new Vector3Int(1, 0, 0), weaponDef: weapon, currentHp: 20);

            var phase = StrikePhaseResult.FromAttackRoll(attacker, target, "Sword", 15, 8, 0, 23)
                .WithHitAndDamage(15, DegreeOfSuccess.Success, 5, DamageType.Slashing, damageDealt: true);

            bool applied = ctx.StrikeAction.ApplyStrikeDamage(phase, damageReduction: 9);
            Assert.IsTrue(applied);
            Assert.AreEqual(20, ctx.Registry.Get(target).CurrentHP);
        }

        [Test]
        public void TryStrike_BackwardCompatibleFlow_PublishesBothEventsAndIncrementsMap()
        {
            using var ctx = new StrikeContext();
            var weapon = ctx.CreateWeaponDef(diceCount: 1, dieSides: 6);

            var attacker = ctx.RegisterEntity(Team.Player, new Vector3Int(0, 0, 0), weaponDef: weapon, level: 1, strength: 10);
            var target = ctx.RegisterEntity(Team.Enemy, new Vector3Int(1, 0, 0), weaponDef: weapon, currentHp: 20);

            int preDamageCount = 0;
            int resolvedCount = 0;
            ctx.EventBus.OnStrikePreDamageTyped += OnPreDamage;
            ctx.EventBus.OnStrikeResolved += OnResolved;

            try
            {
                bool performed = ctx.StrikeAction.TryStrike(attacker, target);
                Assert.IsTrue(performed);
                Assert.AreEqual(1, preDamageCount);
                Assert.AreEqual(1, resolvedCount);
                Assert.AreEqual(1, ctx.Registry.Get(attacker).MAPCount);
            }
            finally
            {
                ctx.EventBus.OnStrikePreDamageTyped -= OnPreDamage;
                ctx.EventBus.OnStrikeResolved -= OnResolved;
            }

            void OnPreDamage(in StrikePreDamageEvent e) => preDamageCount++;
            void OnResolved(in StrikeResolvedEvent e) => resolvedCount++;
        }

        private sealed class StrikeContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly List<WeaponDefinition> weaponDefs = new();
            private readonly List<ShieldDefinition> shieldDefs = new();
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject strikeActionGo;

            public readonly CombatEventBus EventBus;
            public readonly EntityManager EntityManager;
            public readonly StrikeAction StrikeAction;
            public readonly EntityRegistry Registry;

            public StrikeContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("EventBus_Test");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("EntityManager_Test");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                strikeActionGo = new GameObject("StrikeAction_Test");
                StrikeAction = strikeActionGo.AddComponent<StrikeAction>();

                SetPrivateField(StrikeAction, "entityManager", EntityManager);
                SetPrivateField(StrikeAction, "eventBus", EventBus);
            }

            public WeaponDefinition CreateWeaponDef(
                int diceCount = 1,
                int dieSides = 6,
                int reachFeet = 5,
                bool isRanged = false,
                int rangeIncrementFeet = 0,
                int maxRangeIncrements = 0)
            {
                var def = ScriptableObject.CreateInstance<WeaponDefinition>();
                def.itemName = "Test Weapon";
                def.diceCount = diceCount;
                def.dieSides = dieSides;
                def.reachFeet = reachFeet;
                def.isRanged = isRanged;
                def.rangeIncrementFeet = rangeIncrementFeet;
                def.maxRangeIncrements = maxRangeIncrements;
                def.damageType = DamageType.Slashing;
                weaponDefs.Add(def);
                return def;
            }

            public ShieldDefinition CreateShieldDef(int acBonus, int hardness, int maxHP)
            {
                var def = ScriptableObject.CreateInstance<ShieldDefinition>();
                def.itemName = "Test Shield";
                def.acBonus = acBonus;
                def.hardness = hardness;
                def.maxHP = maxHP;
                shieldDefs.Add(def);
                return def;
            }

            public EntityHandle RegisterEntity(
                Team team,
                Vector3Int gridPosition,
                WeaponDefinition weaponDef,
                int level = 1,
                int strength = 10,
                int dexterity = 10,
                int currentHp = 20,
                ShieldInstance shield = default)
            {
                var data = new EntityData
                {
                    Name = $"{team}_{gridPosition}",
                    Team = team,
                    Size = CreatureSize.Medium,
                    Level = level,
                    MaxHP = currentHp,
                    CurrentHP = currentHp,
                    Speed = 25,
                    GridPosition = gridPosition,
                    Strength = strength,
                    Dexterity = dexterity,
                    EquippedWeapon = new WeaponInstance
                    {
                        def = weaponDef,
                        potencyBonus = 0,
                        strikingRank = StrikingRuneRank.None
                    },
                    EquippedShield = shield
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

                for (int i = 0; i < shieldDefs.Count; i++)
                {
                    if (shieldDefs[i] != null)
                        Object.DestroyImmediate(shieldDefs[i]);
                }

                if (strikeActionGo != null) Object.DestroyImmediate(strikeActionGo);
                if (entityManagerGo != null) Object.DestroyImmediate(entityManagerGo);
                if (eventBusGo != null) Object.DestroyImmediate(eventBusGo);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private sealed class FixedRng : IRng
        {
            private readonly Queue<int> d20;
            private readonly Queue<int> dice;

            public FixedRng(IEnumerable<int> d20Rolls, IEnumerable<int> dieRolls = null)
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
