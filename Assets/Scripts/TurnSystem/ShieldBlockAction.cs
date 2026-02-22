using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public class ShieldBlockAction : MonoBehaviour
    {
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[ShieldBlockAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[ShieldBlockAction] Missing CombatEventBus", this);
        }
#endif

        public bool Execute(EntityHandle reactor, int incomingDamage, in ShieldBlockResult result)
        {
            if (!reactor.IsValid || entityManager == null || entityManager.Registry == null)
                return false;

            var data = entityManager.Registry.Get(reactor);
            if (data == null || !data.IsAlive)
                return false;

            int hpBefore = data.EquippedShield.currentHP;
            data.ApplyShieldDamage(result.shieldSelfDamage);
            data.ReactionAvailable = false;
            int hpAfter = data.EquippedShield.currentHP;

            eventBus?.PublishShieldBlockResolved(
                reactor,
                incomingDamage,
                result.targetDamageReduction,
                result.shieldSelfDamage,
                hpBefore,
                hpAfter);

            return true;
        }
    }
}
