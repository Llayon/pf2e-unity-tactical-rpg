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
            string rawTargetName = targetData?.Name ?? "Unknown";
            var targetTeam = targetData?.Team ?? Team.Neutral;
            string targetName = CombatLogRichText.EntityName(rawTargetName, targetTeam);

            // 1. Attack roll line (always published)
            eventBus.Publish(e.attacker,
                $"{CombatLogRichText.ActionCost(1)} {CombatLogRichText.Verb("strikes")} {targetName} {CombatLogRichText.Verb("with")} {CombatLogRichText.Weapon(e.weaponName)}{CombatLogRichText.Verb(",")} " +
                $"{CombatLogRichText.Verb(RollBreakdownFormatter.FormatRoll(e.attackRoll))} " +
                $"{CombatLogRichText.Verb("[" + RollBreakdownFormatter.FormatStrikeAttackBreakdown(e.attackBonus, e.mapPenalty, e.rangePenalty, e.volleyPenalty, e.aidCircumstanceBonus) + "]")} " +
                $"{CombatLogRichText.Verb("vs")} {CombatLogRichText.Verb(RollBreakdownFormatter.FormatDefenseWithCover(e.defenseSource, e.dc, e.coverAcBonus))}" +
                $" → {CombatLogRichText.Degree(e.acDegree)}",
                CombatLogCategory.Attack);

            // 2. Hit/miss determination: use degree, NOT damage
            bool isHit = (e.degree == DegreeOfSuccess.Success || e.degree == DegreeOfSuccess.CriticalSuccess);
            bool concealmentFailed = e.concealmentCheckRequired && !e.concealmentFlatCheckPassed;

            if (concealmentFailed)
            {
                eventBus.Publish(
                    e.attacker,
                    $"{CombatLogRichText.Verb(GetWouldHitVerb(e.acDegree))} {targetName}{CombatLogRichText.Verb($", but concealment DC 5 flat check d20({e.concealmentRoll.naturalRoll}) failed")} {CombatLogRichText.DegreeShort(DegreeOfSuccess.Failure)}.",
                    CombatLogCategory.Attack);
            }
            else if (isHit)
            {
                if (e.damage > 0)
                {
                    eventBus.Publish(e.attacker,
                        $"{CombatLogRichText.Verb("deals")} {CombatLogRichText.Damage(e.damage)} {CombatLogRichText.DmgType(e.damageType)} {CombatLogRichText.Verb("damage to")} {targetName}" +
                        BuildCritTraitBreakdown(e) +
                        $" {CombatLogRichText.Hp(e.hpBefore, e.hpAfter)}",
                        CombatLogCategory.Attack);
                }
                else
                {
                    eventBus.Publish(e.attacker,
                        $"{CombatLogRichText.Verb("hits")} {targetName} {CombatLogRichText.Verb("but deals no damage")} {CombatLogRichText.Hp(e.hpBefore, e.hpAfter)}",
                        CombatLogCategory.Attack);
                }
            }
            else
            {
                eventBus.Publish(
                    e.attacker,
                    $"{CombatLogRichText.Verb("misses")} {targetName}.",
                    CombatLogCategory.Attack);
            }
            // 3. Defeat notification (if applicable)
            if (e.targetDefeated)
            {
                eventBus.PublishSystem(CombatLogRichText.Defeated(rawTargetName));
            }
        }

        private void HandleShieldBlockResolved(in ShieldBlockResolvedEvent e)
        {
            if (e.damageReduction <= 0 && e.shieldSelfDamage <= 0)
            {
                eventBus.Publish(
                    e.reactor,
                    $"{CombatLogRichText.Verb("uses")} {CombatLogRichText.Weapon("Shield Block")}{CombatLogRichText.Verb(", but no damage is prevented.")}",
                    CombatLogCategory.Attack);
                return;
            }

            eventBus.Publish(
                e.reactor,
                $"{CombatLogRichText.Verb("uses")} {CombatLogRichText.Weapon("Shield Block")} {CombatLogRichText.Verb("— reduces")} {CombatLogRichText.Damage(e.damageReduction)} {CombatLogRichText.Verb($"damage (incoming {e.incomingDamage}); shield takes")} {e.shieldSelfDamage} {CombatLogRichText.Verb($"(HP {e.shieldHpBefore}→{e.shieldHpAfter})")}",
                CombatLogCategory.Attack);

            if (e.shieldHpBefore > 0 && e.shieldHpAfter <= 0)
            {
                eventBus.Publish(
                    e.reactor,
                    $"<color={CombatLogRichText.DefeatedColor}><b>shield is broken!</b></color>",
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
    }
}
