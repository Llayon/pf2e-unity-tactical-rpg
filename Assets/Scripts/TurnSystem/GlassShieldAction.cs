using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public class GlassShieldAction : MonoBehaviour
    {
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        public const int ActionCost = 1;
        public const int BaseAcBonus = 1;
        public const int BaseHardness = 2;
        public const int BaseMaxHP = 1; // Spell always ends on Shield Block in this implementation.
        public const int BlockCooldownRounds = 100; // 10 minutes.

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (entityManager == null) Debug.LogWarning("[GlassShieldAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[GlassShieldAction] Missing CombatEventBus", this);
        }
#endif

        public void InjectDependencies(EntityManager entityManager, CombatEventBus eventBus)
        {
            this.entityManager = entityManager;
            this.eventBus = eventBus;
        }

        public bool CanCastGlassShield(EntityHandle actor)
        {
            if (!actor.IsValid || entityManager == null || entityManager.Registry == null)
                return false;

            var data = entityManager.Registry.Get(actor);
            if (data == null || !data.IsAlive)
                return false;

            return data.CanCastGlassShield;
        }

        public bool TryCastGlassShield(EntityHandle actor)
        {
            if (!CanCastGlassShield(actor))
                return false;

            var data = entityManager.Registry.Get(actor);
            if (data == null)
                return false;

            if (!data.ActivateGlassShield(BaseAcBonus, BaseHardness, BaseMaxHP))
                return false;

            eventBus?.PublishShieldRaised(actor, data.GlassShieldAcBonus, data.GlassShieldCurrentHP, data.GlassShieldMaxHP);
            eventBus?.Publish(actor, "casts Glass Shield (+1 AC until start of next turn).", CombatLogCategory.Spell);
            return true;
        }
    }
}
