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
            string rawTargetName = targetData?.Name ?? "Unknown";
            var targetTeam = targetData?.Team ?? Team.Neutral;
            string targetName = CombatLogRichText.EntityName(rawTargetName, targetTeam);
            string sourceLabel = string.IsNullOrEmpty(e.sourceActionName) ? "Damage" : e.sourceActionName;

            string critPrefix = e.isCritical
                ? $"<color={CombatLogRichText.CritSuccessColor}><b>critical</b></color> "
                : string.Empty;
            string damageLine =
                $"{CombatLogRichText.Weapon(sourceLabel)} {CombatLogRichText.Verb("deals")} {CombatLogRichText.DamageAmountAndType(e.amount, e.damageType)} {critPrefix}{CombatLogRichText.Verb("damage to")} {targetName} {CombatLogRichText.Hp(e.hpBefore, e.hpAfter)}";

            if (e.source.IsValid)
                eventBus.Publish(e.source, damageLine, CombatLogCategory.Attack);
            else
                eventBus.PublishSystem(damageLine, CombatLogCategory.Attack);

            if (e.targetDefeated)
                eventBus.Publish(e.target, CombatLogRichText.Defeated(), CombatLogCategory.Condition);
        }
    }
}
