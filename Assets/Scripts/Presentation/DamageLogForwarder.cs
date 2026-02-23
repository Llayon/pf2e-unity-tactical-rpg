using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.Presentation
{
    /// <summary>
    /// Converts generic DamageAppliedEvent (non-strike for now) into combat log lines.
    /// </summary>
    public class DamageLogForwarder : MonoBehaviour
    {
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null)
                Debug.LogError("[DamageLogForwarder] Missing CombatEventBus", this);
            if (entityManager == null)
                Debug.LogError("[DamageLogForwarder] Missing EntityManager", this);
        }
#endif

        private void OnEnable()
        {
            if (eventBus == null || entityManager == null)
            {
                Debug.LogError("[DamageLogForwarder] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            // NOTE: Strike damage currently logs through StrikeLogForwarder (OnStrikeResolved).
            // If Strike later migrates to DamageAppliedEvent, remove the strike damage log path
            // there to avoid duplicate damage log lines.
            eventBus.OnDamageAppliedTyped += HandleDamageApplied;
        }

        private void OnDisable()
        {
            if (eventBus != null)
                eventBus.OnDamageAppliedTyped -= HandleDamageApplied;
        }

        private void HandleDamageApplied(in DamageAppliedEvent e)
        {
            if (e.amount <= 0) return;

            var targetData = entityManager.Registry != null ? entityManager.Registry.Get(e.target) : null;
            string targetName = targetData?.Name ?? "Unknown";
            string sourceLabel = string.IsNullOrEmpty(e.sourceActionName) ? "Damage" : e.sourceActionName;

            string critPrefix = e.isCritical ? "critical " : string.Empty;
            string damageLine =
                $"{sourceLabel} deals {e.amount} {e.damageType} {critPrefix}damage to {targetName} (HP {e.hpBefore}→{e.hpAfter})";

            if (e.source.IsValid)
                eventBus.Publish(e.source, damageLine, CombatLogCategory.Attack);
            else
                eventBus.PublishSystem(damageLine, CombatLogCategory.Attack);

            if (e.targetDefeated)
                eventBus.PublishSystem($"{targetName} is defeated.");
        }
    }
}
