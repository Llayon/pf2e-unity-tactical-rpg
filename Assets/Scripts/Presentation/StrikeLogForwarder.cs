using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.Presentation
{
    /// <summary>
    /// Listens to typed StrikeResolvedEvent and converts to string log entries.
    /// Hybrid architecture: StrikeAction publishes typed event; this converts to CombatLog.
    /// </summary>
    public class StrikeLogForwarder : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null)
                Debug.LogError("[StrikeLogForwarder] Missing CombatEventBus", this);
            if (entityManager == null)
                Debug.LogError("[StrikeLogForwarder] Missing EntityManager", this);
        }
#endif

        private void OnEnable()
        {
            if (eventBus == null || entityManager == null)
            {
                Debug.LogError("[StrikeLogForwarder] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnStrikeResolved += HandleStrikeResolved;
        }

        private void OnDisable()
        {
            if (eventBus != null)
                eventBus.OnStrikeResolved -= HandleStrikeResolved;
        }

        private void HandleStrikeResolved(in StrikeResolvedEvent e)
        {
            var targetData = entityManager.Registry.Get(e.target);
            string targetName = targetData?.Name ?? "Unknown";

            // 1. Attack roll line (always published)
            eventBus.Publish(e.attacker,
                $"strikes {targetName} with {e.weaponName}: " +
                $"d20({e.naturalRoll}) + atk({e.attackBonus}) " +
                $"+ MAP({e.mapPenalty}) = {e.total} " +
                $"vs AC {e.dc} → {e.degree}",
                CombatLogCategory.Attack);

            // 2. Hit/miss determination: use degree, NOT damage
            bool isHit = (e.degree == DegreeOfSuccess.Success || e.degree == DegreeOfSuccess.CriticalSuccess);

            if (isHit)
            {
                if (e.damage > 0)
                {
                    eventBus.Publish(e.attacker,
                        $"deals {e.damage} {e.damageType} damage to {targetName} (HP: {e.hpBefore}→{e.hpAfter})",
                        CombatLogCategory.Attack);
                }
                else
                {
                    eventBus.Publish(e.attacker,
                        $"hits {targetName} but deals no damage. (HP: {e.hpBefore}→{e.hpAfter})",
                        CombatLogCategory.Attack);
                }
            }
            else
            {
                eventBus.Publish(e.attacker,
                    $"misses {targetName}.",
                    CombatLogCategory.Attack);
            }

            // 3. Defeat notification (if applicable)
            if (e.targetDefeated)
            {
                eventBus.PublishSystem($"{targetName} is defeated.");
            }
        }
    }
}
