using UnityEngine;

namespace PF2e.Core
{
    /// <summary>
    /// Pure attack check resolution for strikes.
    /// Does NOT mutate MAPCount or any entity state.
    /// </summary>
    public static class AttackResolver
    {
        public static StrikeCheckResult ResolveStrike(
            EntityData attacker,
            EntityData target,
            bool requireSameElevation,
            IRng rng)
        {
            if (attacker == null || target == null)
                return StrikeCheckResult.Failed(StrikeFailureReason.AttackerDead);

            if (!attacker.IsAlive)
                return StrikeCheckResult.Failed(StrikeFailureReason.AttackerDead);

            if (!target.IsAlive)
                return StrikeCheckResult.Failed(StrikeFailureReason.TargetDead);

            if (attacker.Team == target.Team)
                return StrikeCheckResult.Failed(StrikeFailureReason.SameTeam);

            // Note: self-target check requires EntityHandle comparison in caller
            // (we don't have handles here, only data)

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(attacker.GridPosition, target.GridPosition);
            int rangePenalty = 0;

            if (attacker.EquippedWeapon.IsRanged)
            {
                int incrementFeet = attacker.EquippedWeapon.def != null ? attacker.EquippedWeapon.def.rangeIncrementFeet : 0;
                int maxIncrements = attacker.EquippedWeapon.def != null ? attacker.EquippedWeapon.def.maxRangeIncrements : 0;
                int maxRangeFeet = incrementFeet * maxIncrements;

                if (incrementFeet <= 0 || maxIncrements <= 0 || distanceFeet > maxRangeFeet)
                    return StrikeCheckResult.Failed(StrikeFailureReason.OutOfRange);

                int increments = incrementFeet > 0 ? Mathf.Max(0, (distanceFeet - 1) / incrementFeet) : 0;
                rangePenalty = -increments * 2;
            }
            else
            {
                if (requireSameElevation && attacker.GridPosition.y != target.GridPosition.y)
                    return StrikeCheckResult.Failed(StrikeFailureReason.ElevationMismatch);

                int reachFeet = attacker.EquippedWeapon.ReachFeet;
                if (distanceFeet > reachFeet)
                    return StrikeCheckResult.Failed(StrikeFailureReason.OutOfRange);
            }

            // Perform attack roll
            if (rng == null)
                rng = UnityRng.Shared;

            int nat = rng.RollD20();
            int atkBonus = attacker.GetAttackBonus(attacker.EquippedWeapon);
            int mapPenalty = attacker.GetMAPPenalty(attacker.EquippedWeapon);
            int total = nat + atkBonus + mapPenalty + rangePenalty;

            int ac = target.EffectiveAC;
            var degree = DegreeOfSuccessResolver.Resolve(total, nat, ac);

            return StrikeCheckResult.Success(nat, atkBonus, mapPenalty, total, ac, degree, rangePenalty);
        }

        public static StrikeCheckResult ResolveMeleeStrike(
            EntityData attacker,
            EntityData target,
            bool requireSameElevation,
            IRng rng)
        {
            // Legacy wrapper name kept for compatibility with older callers/tests.
            return ResolveStrike(attacker, target, requireSameElevation, rng);
        }
    }
}
