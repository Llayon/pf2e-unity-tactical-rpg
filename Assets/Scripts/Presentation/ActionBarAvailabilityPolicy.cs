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

            state = BuildForActor(
                actorData,
                turnManager.ActionsRemaining,
                !turnManager.HasReadiedStrike(actor));
            return true;
        }

        public ActionBarAvailabilityState BuildForActor(
            EntityData actorData,
            int actionsRemaining = 3,
            bool canPrepareReadyStrike = true)
        {
            if (actorData == null || !actorData.IsAlive)
                return default;

            if (actorData.HasCondition(ConditionType.Fleeing))
            {
                return new ActionBarAvailabilityState(
                    strikeInteractable: false,
                    jumpInteractable: false,
                    tripInteractable: false,
                    shoveInteractable: false,
                    grappleInteractable: false,
                    repositionInteractable: false,
                    demoralizeInteractable: false,
                    escapeInteractable: IsGrabbedOrRestrained(actorData),
                    aidInteractable: false,
                    readyInteractable: false,
                    castSpellInteractable: false,
                    raiseShieldInteractable: false,
                    guardVisible: IsGuardVisible(actorData),
                    stepInteractable: false,
                    stepVisible: false,
                    standInteractable: HasCondition(actorData, ConditionType.Prone),
                    standVisible: HasCondition(actorData, ConditionType.Prone));
            }

            return new ActionBarAvailabilityState(
                strikeInteractable: true,
                jumpInteractable: true,
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
                readyInteractable: canPrepareReadyStrike && actionsRemaining >= ReadyStrikeAction.ActionCost,
                castSpellInteractable: CanCastActionBarSpell(actorData, actionsRemaining),
                raiseShieldInteractable: CanRaisePhysicalShield(actorData),
                guardVisible: IsGuardVisible(actorData),
                stepInteractable: !HasCondition(actorData, ConditionType.Prone) && actorData.EffectiveSpeed > 0,
                stepVisible: !HasCondition(actorData, ConditionType.Prone),
                standInteractable: HasCondition(actorData, ConditionType.Prone),
                standVisible: HasCondition(actorData, ConditionType.Prone));
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

        private static bool CanRaisePhysicalShield(EntityData data)
        {
            if (data == null)
                return false;

            var shield = data.EquippedShield;
            return
                shield.IsEquipped
                && !shield.IsBroken
                && !shield.isRaised;
        }

        private static bool CanCastActionBarSpell(EntityData data, int actionsRemaining)
        {
            if (data == null)
                return false;

            bool canCastForceBarrage = data.KnowsForceBarrage && actionsRemaining >= SpellCatalog.Get(SpellId.ForceBarrage).minActionCost;
            bool canCastElectricArc = data.KnowsElectricArc && actionsRemaining >= SpellCatalog.Get(SpellId.ElectricArc).minActionCost;
            bool canCastSnowball = data.KnowsSnowball && actionsRemaining >= SpellCatalog.Get(SpellId.Snowball).minActionCost;
            bool canCastBurningHands = data.KnowsBurningHands && actionsRemaining >= SpellCatalog.Get(SpellId.BurningHands).minActionCost;
            bool canCastFear = data.KnowsFear && actionsRemaining >= SpellCatalog.Get(SpellId.Fear).minActionCost;
            bool canCastHeal = data.KnowsHeal && actionsRemaining >= SpellCatalog.Get(SpellId.Heal).minActionCost;
            bool canCastHarm = data.KnowsHarm && actionsRemaining >= SpellCatalog.Get(SpellId.Harm).minActionCost;
            return canCastForceBarrage || canCastElectricArc || canCastSnowball || canCastBurningHands || canCastFear || canCastHeal || canCastHarm;
        }

        private static bool IsGuardVisible(EntityData data)
        {
            if (data == null)
                return false;

            return data.EquippedShield.IsEquipped;
        }

        private static bool HasCondition(EntityData data, ConditionType type)
        {
            return data != null && data.HasCondition(type);
        }
    }
}
