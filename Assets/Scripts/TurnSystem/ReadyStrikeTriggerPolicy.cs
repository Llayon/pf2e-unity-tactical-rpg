using UnityEngine;
using PF2e.Core;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Pure trigger-range policy for Ready Strike.
    /// </summary>
    public static class ReadyStrikeTriggerPolicy
    {
        public static bool DidEnterStrikeRange(
            EntityData actorData,
            EntityData movedTargetData,
            Vector3Int from,
            Vector3Int to)
        {
            if (actorData == null || movedTargetData == null)
                return false;

            int distanceBefore = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, from);
            int distanceAfter = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, to);
            var weapon = actorData.EquippedWeapon;

            if (weapon.IsRanged)
            {
                // Ready Strike trigger for ranged uses entry into the first increment,
                // not entry into absolute max range.
                if (!TryGetRangedReadyTriggerDistanceFeet(weapon, out int triggerRange))
                    return false;

                bool enteredTriggerRange = distanceBefore > triggerRange && distanceAfter <= triggerRange;
                bool startedMovingInsideTriggerRange = distanceBefore <= triggerRange && from != to;
                return enteredTriggerRange || startedMovingInsideTriggerRange;
            }

            int reach = weapon.ReachFeet;
            bool enteredReach = distanceBefore > reach && distanceAfter <= reach;
            bool startedMovingInsideReach = distanceBefore <= reach && from != to;
            return enteredReach || startedMovingInsideReach;
        }

        public static bool IsWithinReadyStrikeTriggerRange(EntityData actorData, EntityData targetData)
        {
            if (actorData == null || targetData == null)
                return false;

            int distance = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, targetData.GridPosition);
            var weapon = actorData.EquippedWeapon;

            if (weapon.IsRanged)
            {
                if (!TryGetRangedReadyTriggerDistanceFeet(weapon, out int triggerRange))
                    return false;
                return distance <= triggerRange;
            }

            return distance <= weapon.ReachFeet;
        }

        public static bool TryGetRangedReadyTriggerDistanceFeet(WeaponInstance weapon, out int triggerRangeFeet)
        {
            triggerRangeFeet = 0;
            if (!weapon.IsRanged)
                return false;

            int incrementFeet = weapon.def != null ? weapon.def.rangeIncrementFeet : 0;
            if (incrementFeet <= 0)
                return false;

            triggerRangeFeet = incrementFeet;
            return triggerRangeFeet > 0;
        }
    }
}
