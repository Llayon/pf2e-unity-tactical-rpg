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
            eventBus.OnShieldBlockResolvedTyped += HandleShieldBlockResolved;
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                eventBus.OnStrikeResolved -= HandleStrikeResolved;
                eventBus.OnShieldBlockResolvedTyped -= HandleShieldBlockResolved;
            }
        }

        private void HandleStrikeResolved(in StrikeResolvedEvent e)
        {
            var targetData = entityManager.Registry.Get(e.target);
            string targetName = targetData?.Name ?? "Unknown";

            // 1. Attack roll line (always published)
            eventBus.Publish(e.attacker,
                $"strikes {targetName} with {e.weaponName} — " +
                $"{RollBreakdownFormatter.FormatRoll(e.attackRoll)} " +
                $"[{BuildAttackBreakdown(e)}] " +
                $"vs {e.defenseSource.ToShortLabel()} {e.dc}" +
                (e.coverAcBonus != 0 ? $" + COVER({e.coverAcBonus:+#;-#;0})" : string.Empty) +
                $" → {e.acDegree}",
                CombatLogCategory.Attack);

            // 2. Hit/miss determination: use degree, NOT damage
            bool isHit = (e.degree == DegreeOfSuccess.Success || e.degree == DegreeOfSuccess.CriticalSuccess);
            bool concealmentFailed = e.concealmentCheckRequired && !e.concealmentFlatCheckPassed;

            if (concealmentFailed)
            {
                eventBus.Publish(
                    e.attacker,
                    $"{GetWouldHitVerb(e.acDegree)} {targetName}, but concealment DC 5 flat check d20({e.concealmentRoll.naturalRoll}) failed (miss).",
                    CombatLogCategory.Attack);
            }
            else if (isHit)
            {
                if (e.damage > 0)
                {
                    eventBus.Publish(e.attacker,
                        $"deals {e.damage} {e.damageType} damage to {targetName}" +
                        BuildCritTraitBreakdown(e) +
                        $" (HP {e.hpBefore}→{e.hpAfter})",
                        CombatLogCategory.Attack);
                }
                else
                {
                    eventBus.Publish(e.attacker,
                        $"hits {targetName} but deals no damage (HP {e.hpBefore}→{e.hpAfter})",
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

        private void HandleShieldBlockResolved(in ShieldBlockResolvedEvent e)
        {
            if (e.damageReduction <= 0 && e.shieldSelfDamage <= 0)
            {
                eventBus.Publish(
                    e.reactor,
                    "uses Shield Block, but no damage is prevented.",
                    CombatLogCategory.Attack);
                return;
            }

            eventBus.Publish(
                e.reactor,
                $"uses Shield Block — reduces {e.damageReduction} damage (incoming {e.incomingDamage}); " +
                $"shield takes {e.shieldSelfDamage} (HP {e.shieldHpBefore}→{e.shieldHpAfter})",
                CombatLogCategory.Attack);

            if (e.shieldHpBefore > 0 && e.shieldHpAfter <= 0)
            {
                eventBus.Publish(
                    e.reactor,
                    "shield is broken.",
                    CombatLogCategory.Attack);
            }
        }

        private static string BuildCritTraitBreakdown(in StrikeResolvedEvent e)
        {
            if (e.fatalBonusDamage <= 0 && e.deadlyBonusDamage <= 0)
                return string.Empty;

            if (e.fatalBonusDamage > 0 && e.deadlyBonusDamage > 0)
                return $" (FATAL+{e.fatalBonusDamage}, DEADLY+{e.deadlyBonusDamage})";

            if (e.fatalBonusDamage > 0)
                return $" (FATAL+{e.fatalBonusDamage})";

            return $" (DEADLY+{e.deadlyBonusDamage})";
        }

        private static string GetWouldHitVerb(DegreeOfSuccess acDegree)
        {
            return acDegree == DegreeOfSuccess.CriticalSuccess
                ? "would critically hit"
                : "would hit";
        }

        private static string BuildAttackBreakdown(in StrikeResolvedEvent e)
        {
            return
                $"atk({e.attackBonus}) + MAP({e.mapPenalty})" +
                (e.rangePenalty != 0 ? $" + RNG({e.rangePenalty})" : string.Empty) +
                (e.volleyPenalty != 0 ? $" + VOLLEY({e.volleyPenalty})" : string.Empty);
        }
    }
}
