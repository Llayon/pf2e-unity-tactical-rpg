using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Pure policy that maps current actor/turn state to action-bar action availability.
    /// </summary>
    public sealed class ActionBarAvailabilityPolicy
    {
        public bool TryEvaluate(
            TurnManager turnManager,
            PlayerActionExecutor actionExecutor,
            EntityRegistry registry,
            out ActionBarAvailabilityState state)
        {
            state = default;

            if (turnManager == null || actionExecutor == null || registry == null)
                return false;

            bool canAct = turnManager.IsPlayerTurn
                       && !actionExecutor.IsBusy
                       && turnManager.ActionsRemaining > 0;
            if (!canAct)
                return false;

            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid)
                return false;

            var actorData = registry.Get(actor);
            if (actorData == null || !actorData.IsAlive)
                return false;

            state = BuildForActor(actorData);
            return true;
        }

        public ActionBarAvailabilityState BuildForActor(EntityData actorData)
        {
            if (actorData == null || !actorData.IsAlive)
                return default;

            return new ActionBarAvailabilityState(
                strikeInteractable: true,
                tripInteractable: HasWeaponTrait(actorData, WeaponTraitFlags.Trip),
                shoveInteractable: HasWeaponTrait(actorData, WeaponTraitFlags.Shove),
                grappleInteractable: HasWeaponTrait(actorData, WeaponTraitFlags.Grapple),
                // Reposition can be enabled via active grapple relation (not visible from EntityData),
                // so policy keeps broad pre-target gate and action/preview validates exact legality.
                repositionInteractable: true,
                demoralizeInteractable: true,
                escapeInteractable: IsGrabbedOrRestrained(actorData),
                // Aid remains selectable so player can receive contextual targeting feedback.
                aidInteractable: true,
                raiseShieldInteractable: CanRaiseShield(actorData),
                standInteractable: HasCondition(actorData, ConditionType.Prone));
        }

        private static bool HasWeaponTrait(EntityData data, WeaponTraitFlags trait)
        {
            if (data == null)
                return false;

            return (data.EquippedWeapon.Traits & trait) != 0;
        }

        private static bool IsGrabbedOrRestrained(EntityData data)
        {
            if (data == null)
                return false;

            return data.HasCondition(ConditionType.Grabbed) || data.HasCondition(ConditionType.Restrained);
        }

        private static bool CanRaiseShield(EntityData data)
        {
            if (data == null)
                return false;

            var shield = data.EquippedShield;
            if (!shield.IsEquipped)
                return false;
            if (shield.IsBroken)
                return false;
            if (shield.isRaised)
                return false;

            return true;
        }

        private static bool HasCondition(EntityData data, ConditionType type)
        {
            return data != null && data.HasCondition(type);
        }
    }
}
