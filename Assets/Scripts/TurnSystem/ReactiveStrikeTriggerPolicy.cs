using UnityEngine;
using PF2e.Core;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Pure trigger policy for fighter Reactive Strike.
    /// MVP scope:
    /// - move trigger only when the mover was already inside reach before the move;
    /// - ranged attack trigger only when the attacker is inside reach.
    /// </summary>
    public static class ReactiveStrikeTriggerPolicy
    {
        public static bool CanTriggerOnMovement(
            EntityData actorData,
            EntityData movedTargetData,
            in EntityMovedEvent e)
        {
            if (actorData == null || movedTargetData == null)
                return false;
            if (actorData.EquippedWeapon.IsRanged)
                return false;
            if (e.movementTriggerKind != MovementTriggerKind.Normal)
                return false;
            if (e.from == e.to)
                return false;

            int distanceBefore = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, e.from);
            return distanceBefore <= actorData.EquippedWeapon.ReachFeet;
        }

        public static bool CanTriggerOnRangedAttack(
            EntityData actorData,
            EntityData attackSourceData)
        {
            if (actorData == null || attackSourceData == null)
                return false;
            if (actorData.EquippedWeapon.IsRanged)
                return false;
            if (!attackSourceData.EquippedWeapon.IsRanged)
                return false;

            int distance = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, attackSourceData.GridPosition);
            return distance <= actorData.EquippedWeapon.ReachFeet;
        }

        public static bool CanTriggerOnActionStart(
            EntityData actorData,
            EntityData actionSourceData,
            in CombatActionStartedEvent e)
        {
            if (actorData == null || actionSourceData == null)
                return false;
            if (actorData.EquippedWeapon.IsRanged)
                return false;

            int distance = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, actionSourceData.GridPosition);
            if (distance > actorData.EquippedWeapon.ReachFeet)
                return false;

            if (e.actionKind == CombatActionKind.Stand)
                return true;

            return e.actionKind == CombatActionKind.Spell
                && e.HasTrait(CombatActionTraitFlags.Manipulate);
        }
    }
}
