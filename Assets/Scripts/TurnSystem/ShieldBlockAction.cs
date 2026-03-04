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

        public bool Execute(
            EntityHandle reactor,
            int incomingDamage,
            in ShieldBlockResult result,
            ShieldBlockSource source = ShieldBlockSource.PhysicalShield)
        {
            if (!reactor.IsValid || entityManager == null || entityManager.Registry == null)
                return false;

            var data = entityManager.Registry.Get(reactor);
            if (data == null || !data.IsAlive)
                return false;

            int hpBefore;
            int hpAfter;

            switch (source)
            {
                case ShieldBlockSource.GlassShield:
                    hpBefore = data.GlassShieldCurrentHP;
                    data.ApplyGlassShieldSelfDamageAndDispel(result.shieldSelfDamage, GlassShieldAction.BlockCooldownRounds);
                    hpAfter = data.GlassShieldCurrentHP;
                    eventBus?.Publish(reactor, "Glass Shield shatters; recast blocked for 10 minutes.", CombatLogCategory.Spell);
                    break;

                case ShieldBlockSource.PhysicalShield:
                default:
                    hpBefore = data.EquippedShield.currentHP;
                    data.ApplyShieldDamage(result.shieldSelfDamage);
                    hpAfter = data.EquippedShield.currentHP;
                    break;
            }

            data.ReactionAvailable = false;

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
