using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public class RaiseShieldAction : MonoBehaviour
    {
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        public const int ActionCost = 1;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[RaiseShieldAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[RaiseShieldAction] Missing CombatEventBus", this);
        }
#endif

        public bool CanRaiseShield(EntityHandle actor)
        {
            if (!actor.IsValid || entityManager == null || entityManager.Registry == null)
                return false;

            var data = entityManager.Registry.Get(actor);
            if (data == null || !data.IsAlive) return false;

            var shield = data.EquippedShield;
            if (!shield.IsEquipped) return false;
            if (shield.IsBroken) return false;
            if (shield.isRaised) return false;

            return true;
        }

        public bool TryRaiseShield(EntityHandle actor)
        {
            if (!CanRaiseShield(actor)) return false;

            var data = entityManager.Registry.Get(actor);
            if (data == null) return false;

            data.SetShieldRaised(true);
            var shield = data.EquippedShield;

            eventBus?.PublishShieldRaised(actor, shield.ACBonus, shield.currentHP, shield.MaxHP);
            return true;
        }
    }
}
