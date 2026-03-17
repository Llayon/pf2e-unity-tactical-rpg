using PF2e.Core;
using PF2e.Managers;
using UnityEngine;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Applies direct healing and publishes HealingAppliedEvent.
    /// </summary>
    public static class HealingApplicationService
    {
        public static int ApplyHealing(
            EntityHandle source,
            EntityHandle target,
            int amount,
            string sourceActionName,
            EntityManager entityManager,
            CombatEventBus eventBus)
        {
            if (entityManager == null || entityManager.Registry == null)
                return 0;
            if (!target.IsValid)
                return 0;

            int finalHealing = Mathf.Max(0, amount);
            if (finalHealing <= 0)
                return 0;

            var targetData = entityManager.Registry.Get(target);
            if (targetData == null || !targetData.IsAlive)
                return 0;

            int hpBefore = Mathf.Max(0, targetData.CurrentHP);
            int hpAfter = Mathf.Clamp(hpBefore + finalHealing, 0, Mathf.Max(0, targetData.MaxHP));
            int appliedHealing = hpAfter - hpBefore;
            if (appliedHealing <= 0)
                return 0;

            targetData.CurrentHP = hpAfter;

            eventBus?.PublishHealingApplied(new HealingAppliedEvent(
                source,
                target,
                appliedHealing,
                sourceActionName,
                hpBefore,
                hpAfter));

            return appliedHealing;
        }
    }
}
