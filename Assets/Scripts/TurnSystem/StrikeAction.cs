using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public class StrikeAction : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        [Header("Rules")]
        [SerializeField] private bool requireSameElevation = true;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[StrikeAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogError("[StrikeAction] Missing CombatEventBus", this);
        }
#endif

        public bool TryStrike(EntityHandle attacker, EntityHandle target)
        {
            var phase = ResolveAttackRoll(attacker, target, UnityRng.Shared);
            if (!phase.HasValue) return false;

            var resolved = DetermineHitAndDamage(phase.Value, target, UnityRng.Shared);
            return ApplyStrikeDamage(resolved, damageReduction: 0);
        }

        public StrikePhaseResult? ResolveAttackRoll(EntityHandle attacker, EntityHandle target, IRng rng)
        {
            if (!attacker.IsValid || !target.IsValid) return null;
            if (!TryGetParticipants(attacker, target, out var attackerData, out var targetData))
                return null;
            if (!CanResolveStrike(attackerData, targetData))
                return null;

            if (rng == null)
                rng = UnityRng.Shared;

            int naturalRoll = rng.RollD20();
            int attackBonus = attackerData.GetAttackBonus(attackerData.EquippedWeapon);
            int mapPenalty = attackerData.GetMAPPenalty(attackerData.EquippedWeapon);
            int rangePenalty = ComputeRangedStrikePenalty(attackerData, targetData);
            int total = naturalRoll + attackBonus + mapPenalty + rangePenalty;

            // Contract: MAP increments once per strike attempt at phase 1.
            attackerData.MAPCount++;

            return StrikePhaseResult.FromAttackRoll(
                attacker,
                target,
                attackerData.EquippedWeapon.Name,
                naturalRoll,
                attackBonus,
                mapPenalty,
                total,
                rangePenalty);
        }

        public StrikePhaseResult DetermineHitAndDamage(StrikePhaseResult phase, EntityHandle target, IRng rng)
        {
            if (rng == null)
                rng = UnityRng.Shared;

            if (entityManager == null || entityManager.Registry == null)
                return phase.WithHitAndDamage(0, DegreeOfSuccess.CriticalFailure, 0, DamageType.Bludgeoning, false);

            var attackerData = entityManager.Registry.Get(phase.attacker);
            var targetData = entityManager.Registry.Get(target);

            // Guard: target/attacker can change between phases (future pre-hit reaction window).
            if (attackerData == null || targetData == null || !attackerData.IsAlive || !targetData.IsAlive)
                return phase.WithHitAndDamage(0, DegreeOfSuccess.CriticalFailure, 0, DamageType.Bludgeoning, false);

            int dc = targetData.EffectiveAC;
            var degree = DegreeOfSuccessResolver.Resolve(phase.total, phase.naturalRoll, dc);
            var damage = DamageResolver.RollStrikeDamage(attackerData, degree, rng);

            int damageRolled = damage.dealt ? damage.damage : 0;
            DamageType damageType = attackerData.EquippedWeapon.def != null
                ? attackerData.EquippedWeapon.def.damageType
                : DamageType.Bludgeoning;

            var resolved = phase.WithHitAndDamage(
                dc,
                degree,
                damageRolled,
                damageType,
                damage.dealt,
                deadlyBonusDamage: damage.deadlyBonusDamage);

            eventBus?.PublishStrikePreDamage(
                resolved.attacker,
                resolved.target,
                resolved.naturalRoll,
                resolved.total,
                resolved.dc,
                resolved.degree,
                resolved.damageRolled,
                resolved.damageType);

            return resolved;
        }

        public bool ApplyStrikeDamage(StrikePhaseResult phase, int damageReduction)
        {
            if (!phase.attacker.IsValid || !phase.target.IsValid) return false;
            if (!TryGetParticipants(phase.attacker, phase.target, out var attackerData, out var targetData))
                return false;
            if (!attackerData.IsAlive || !targetData.IsAlive)
                return false;

            int hpBefore = targetData.CurrentHP;
            int finalDamage = 0;

            if (phase.damageDealt && phase.damageRolled > 0)
            {
                finalDamage = Mathf.Max(0, phase.damageRolled - Mathf.Max(0, damageReduction));
                if (finalDamage > 0)
                {
                    targetData.CurrentHP -= finalDamage;
                    if (targetData.CurrentHP < 0) targetData.CurrentHP = 0;
                }
            }

            int hpAfter = targetData.CurrentHP;
            bool defeated = hpAfter <= 0;

            var resolvedEvent = new StrikeResolvedEvent(
                attacker: phase.attacker,
                target: phase.target,
                weaponName: phase.weaponName,
                naturalRoll: phase.naturalRoll,
                attackBonus: phase.attackBonus,
                mapPenalty: phase.mapPenalty,
                rangePenalty: phase.rangePenalty,
                total: phase.total,
                dc: phase.dc,
                degree: phase.degree,
                damage: finalDamage,
                damageType: phase.damageType,
                hpBefore: hpBefore,
                hpAfter: hpAfter,
                targetDefeated: defeated,
                deadlyBonusDamage: phase.deadlyBonusDamage);

            eventBus?.PublishStrikeResolved(in resolvedEvent);

            if (defeated)
                entityManager.HandleDeath(phase.target);

            return true;
        }

        public TargetingFailureReason GetStrikeTargetFailure(EntityHandle attacker, EntityHandle target)
        {
            if (!attacker.IsValid || !target.IsValid) return TargetingFailureReason.InvalidTarget;
            if (entityManager == null || entityManager.Registry == null) return TargetingFailureReason.InvalidState;

            var attackerData = entityManager.Registry.Get(attacker);
            var targetData = entityManager.Registry.Get(target);
            if (attackerData == null || targetData == null) return TargetingFailureReason.InvalidTarget;
            return GetStrikeTargetFailure(attacker, target, attackerData, targetData);
        }

        // Legacy wrapper retained for existing tests/callers during Strike rename.
        public TargetingFailureReason GetMeleeStrikeTargetFailure(EntityHandle attacker, EntityHandle target)
        {
            return GetStrikeTargetFailure(attacker, target);
        }

        private TargetingFailureReason GetStrikeTargetFailure(
            EntityHandle attacker,
            EntityHandle target,
            EntityData attackerData,
            EntityData targetData)
        {
            if (attackerData == null || targetData == null) return TargetingFailureReason.InvalidTarget;
            if (!attackerData.IsAlive || !targetData.IsAlive) return TargetingFailureReason.NotAlive;
            if (attacker == target) return TargetingFailureReason.SelfTarget;
            if (attackerData.Team == targetData.Team) return TargetingFailureReason.WrongTeam;

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(attackerData.GridPosition, targetData.GridPosition);
            var weapon = attackerData.EquippedWeapon;

            if (weapon.IsRanged)
            {
                if (!TryGetRangedMaxDistanceFeet(weapon, out int maxRangeFeet))
                    return TargetingFailureReason.OutOfRange;
                if (distanceFeet > maxRangeFeet)
                    return TargetingFailureReason.OutOfRange;
                return TargetingFailureReason.None;
            }

            if (requireSameElevation && attackerData.GridPosition.y != targetData.GridPosition.y)
                return TargetingFailureReason.WrongElevation;

            int reachFeet = weapon.ReachFeet;
            if (distanceFeet > reachFeet) return TargetingFailureReason.OutOfRange;

            return TargetingFailureReason.None;
        }

        private bool TryGetParticipants(
            EntityHandle attacker,
            EntityHandle target,
            out EntityData attackerData,
            out EntityData targetData)
        {
            attackerData = null;
            targetData = null;

            if (entityManager == null || entityManager.Registry == null)
                return false;

            attackerData = entityManager.Registry.Get(attacker);
            targetData = entityManager.Registry.Get(target);
            return attackerData != null && targetData != null;
        }

        private bool CanResolveStrike(EntityData attacker, EntityData target)
        {
            if (attacker == null || target == null) return false;
            if (!attacker.IsAlive || !target.IsAlive) return false;
            if (attacker.Team == target.Team) return false;

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(attacker.GridPosition, target.GridPosition);
            var weapon = attacker.EquippedWeapon;

            if (weapon.IsRanged)
            {
                if (!TryGetRangedMaxDistanceFeet(weapon, out int maxRangeFeet)) return false;
                return distanceFeet <= maxRangeFeet;
            }

            if (requireSameElevation && attacker.GridPosition.y != target.GridPosition.y) return false;
            int reachFeet = weapon.ReachFeet;
            return distanceFeet <= reachFeet;
        }

        // Legacy wrapper retained for compatibility with existing callers/tests during refactor.
        private bool CanResolveMeleeStrike(EntityData attacker, EntityData target)
        {
            return CanResolveStrike(attacker, target);
        }

        private static bool TryGetRangedMaxDistanceFeet(WeaponInstance weapon, out int maxRangeFeet)
        {
            maxRangeFeet = 0;
            if (!weapon.IsRanged) return false;

            int incrementFeet = weapon.def != null ? weapon.def.rangeIncrementFeet : 0;
            int maxIncrements = weapon.def != null ? weapon.def.maxRangeIncrements : 0;
            if (incrementFeet <= 0 || maxIncrements <= 0) return false;

            maxRangeFeet = incrementFeet * maxIncrements;
            return maxRangeFeet > 0;
        }

        private static int ComputeRangedStrikePenalty(EntityData attacker, EntityData target)
        {
            if (attacker == null || target == null) return 0;

            var weapon = attacker.EquippedWeapon;
            if (!weapon.IsRanged) return 0;

            int incrementFeet = weapon.def != null ? weapon.def.rangeIncrementFeet : 0;
            if (incrementFeet <= 0) return 0;

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(attacker.GridPosition, target.GridPosition);
            int increments = Mathf.Max(0, (distanceFeet - 1) / incrementFeet);
            return -increments * 2;
        }
    }
}
