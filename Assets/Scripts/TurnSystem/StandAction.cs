using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public class StandAction : MonoBehaviour
    {
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        public const int ActionCost = 1;

        public bool CanStand(EntityHandle actor)
        {
            if (!actor.IsValid || entityManager == null || entityManager.Registry == null)
                return false;
            var data = entityManager.Registry.Get(actor);
            return data != null && data.IsAlive && data.HasCondition(ConditionType.Prone);
        }

        public bool TryStand(EntityHandle actor)
        {
            if (!CanStand(actor)) return false;
            var data = entityManager.Registry.Get(actor);
            data.RemoveCondition(ConditionType.Prone);

            if (eventBus != null)
                eventBus.PublishConditionChanged(
                    actor, ConditionType.Prone,
                    ConditionChangeType.Removed, 0, 0);

            return true;
        }
    }
}
