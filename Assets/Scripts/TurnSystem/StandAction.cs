using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public class StandAction : MonoBehaviour
    {
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;
        private readonly ConditionService conditionService = new();
        private readonly List<ConditionDelta> conditionDeltaBuffer = new();

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
            if (data == null) return false;

            conditionDeltaBuffer.Clear();
            conditionService.Remove(data, ConditionType.Prone, conditionDeltaBuffer);
            if (conditionDeltaBuffer.Count <= 0) return false;

            if (eventBus != null)
            {
                for (int i = 0; i < conditionDeltaBuffer.Count; i++)
                {
                    var delta = conditionDeltaBuffer[i];
                    eventBus.PublishConditionChanged(
                        delta.entity,
                        delta.type,
                        delta.changeType,
                        delta.oldValue,
                        delta.newValue);
                }
            }

            return true;
        }
    }
}
