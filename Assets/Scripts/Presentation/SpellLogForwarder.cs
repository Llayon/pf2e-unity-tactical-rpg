using System.Text;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.Presentation
{
    /// <summary>
    /// Converts typed spell slice resolution events into combat log lines with tooltip payloads.
    /// </summary>
    public class SpellLogForwarder : MonoBehaviour
    {
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null)
                Debug.LogError("[SpellLogForwarder] Missing CombatEventBus", this);
            if (entityManager == null)
                Debug.LogError("[SpellLogForwarder] Missing EntityManager", this);
        }
#endif

        private void OnEnable()
        {
            if (eventBus == null || entityManager == null)
            {
                Debug.LogError("[SpellLogForwarder] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnSpellResolvedTyped += HandleSpellResolved;
        }

        private void OnDisable()
        {
            if (eventBus != null)
                eventBus.OnSpellResolvedTyped -= HandleSpellResolved;
        }

        private void HandleSpellResolved(in SpellResolvedEvent e)
        {
            var definition = SpellCatalog.Get(e.spellId);
            string spellLink = CombatLogLinkHelper.Link(
                CombatLogLinkTokens.Result,
                CombatLogRichText.Weapon(definition.displayName));

            string summary = e.spellId switch
            {
                SpellId.ForceBarrage => BuildForceBarrageSummary(in e),
                SpellId.ElectricArc => BuildElectricArcSummary(in e),
                SpellId.Snowball => BuildSnowballSummary(in e),
                SpellId.BurningHands => BuildBurningHandsSummary(in e),
                SpellId.Fear => BuildFearSummary(in e),
                SpellId.Heal => BuildHealSummary(in e),
                SpellId.Harm => BuildHarmSummary(in e),
                _ => CombatLogRichText.Verb("resolves.")
            };

            var tooltipPayload = new CombatLogTooltipPayload(new[]
            {
                new TooltipEntry(
                    CombatLogLinkTokens.Result,
                    definition.displayName,
                    BuildTooltipBody(in e),
                    TooltipLayoutProfile.Extended)
            });

            eventBus.Publish(
                e.caster,
                $"{CombatLogRichText.ActionCost(Mathf.Clamp(e.actionCost, 1, 3))} {CombatLogRichText.Verb("casts")} {spellLink} {CombatLogRichText.Verb("—")} {summary}",
                CombatLogCategory.Spell,
                tooltipPayload);
        }

        private string BuildForceBarrageSummary(in SpellResolvedEvent e)
        {
            var sb = new StringBuilder(256);
            for (int i = 0; i < e.targetOutcomes.Length; i++)
            {
                if (i > 0)
                    sb.Append(CombatLogRichText.Verb("; "));

                ref readonly var outcome = ref e.targetOutcomes[i];
                sb.Append(GetTargetName(outcome.target, rich: true));
                sb.Append(' ');

                if (outcome.appliedDamage > 0)
                {
                    sb.Append(CombatLogRichText.Verb("takes"));
                    sb.Append(' ');
                    sb.Append(CombatLogRichText.DamageAmountAndType(outcome.appliedDamage, DamageType.Force));
                }
                else
                {
                    sb.Append(CombatLogRichText.Verb("takes no damage"));
                }
            }

            sb.Append('.');
            return sb.ToString();
        }

        private string BuildElectricArcSummary(in SpellResolvedEvent e)
        {
            var sb = new StringBuilder(256);
            for (int i = 0; i < e.targetOutcomes.Length; i++)
            {
                if (i > 0)
                    sb.Append(CombatLogRichText.Verb("; "));

                ref readonly var outcome = ref e.targetOutcomes[i];
                sb.Append(GetTargetName(outcome.target, rich: true));
                sb.Append(' ');

                if (outcome.saveResult.HasValue)
                {
                    sb.Append(CombatLogRichText.DegreeShort(outcome.saveResult.Value.degree));
                    sb.Append(CombatLogRichText.Verb(", "));
                }

                if (outcome.appliedDamage > 0)
                    sb.Append(CombatLogRichText.DamageAmountAndType(outcome.appliedDamage, DamageType.Electricity));
                else
                    sb.Append(CombatLogRichText.Verb("no damage"));
            }

            sb.Append('.');
            return sb.ToString();
        }

        private string BuildSnowballSummary(in SpellResolvedEvent e)
        {
            if (e.targetOutcomes == null || e.targetOutcomes.Length <= 0)
                return CombatLogRichText.Verb("no effect.");

            ref readonly var outcome = ref e.targetOutcomes[0];
            var sb = new StringBuilder(192);
            sb.Append(GetTargetName(outcome.target, rich: true));
            sb.Append(' ');

            if (outcome.attackResult.HasValue)
            {
                sb.Append(CombatLogRichText.DegreeShort(outcome.attackResult.Value.degree));
                sb.Append(CombatLogRichText.Verb(", "));
            }

            if (outcome.appliedDamage > 0)
            {
                sb.Append(CombatLogRichText.DamageAmountAndType(outcome.appliedDamage, DamageType.Cold));
                if (outcome.appliedConditionType == ConditionType.SpeedPenalty && outcome.appliedConditionValue > 0)
                {
                    sb.Append(CombatLogRichText.Verb(", speed "));
                    sb.Append(CombatLogRichText.Verb($"-{outcome.appliedConditionValue} ft"));
                }
            }
            else
            {
                sb.Append(CombatLogRichText.Verb("no effect"));
            }

            sb.Append('.');
            return sb.ToString();
        }

        private string BuildBurningHandsSummary(in SpellResolvedEvent e)
        {
            var sb = new StringBuilder(256);
            for (int i = 0; i < e.targetOutcomes.Length; i++)
            {
                if (i > 0)
                    sb.Append(CombatLogRichText.Verb("; "));

                ref readonly var outcome = ref e.targetOutcomes[i];
                sb.Append(GetTargetName(outcome.target, rich: true));
                sb.Append(' ');

                if (outcome.saveResult.HasValue)
                {
                    sb.Append(CombatLogRichText.DegreeShort(outcome.saveResult.Value.degree));
                    sb.Append(CombatLogRichText.Verb(", "));
                }

                if (outcome.appliedDamage > 0)
                    sb.Append(CombatLogRichText.DamageAmountAndType(outcome.appliedDamage, DamageType.Fire));
                else
                    sb.Append(CombatLogRichText.Verb("no damage"));
            }

            if (e.targetOutcomes.Length <= 0)
                sb.Append(CombatLogRichText.Verb("the cone hits no creatures"));

            sb.Append('.');
            return sb.ToString();
        }

        private string BuildFearSummary(in SpellResolvedEvent e)
        {
            if (e.targetOutcomes == null || e.targetOutcomes.Length <= 0)
                return CombatLogRichText.Verb("no effect.");

            ref readonly var outcome = ref e.targetOutcomes[0];
            var sb = new StringBuilder(192);
            sb.Append(GetTargetName(outcome.target, rich: true));
            sb.Append(' ');

            if (outcome.saveResult.HasValue)
            {
                sb.Append(CombatLogRichText.DegreeShort(outcome.saveResult.Value.degree));
                sb.Append(CombatLogRichText.Verb(", "));
            }

            if (outcome.appliedConditionType == ConditionType.Frightened && outcome.appliedConditionValue > 0)
            {
                sb.Append(CombatLogRichText.Verb("gains "));
                sb.Append(CombatLogRichText.ConditionGain(BuildFearEffectSummary(in outcome)));
            }
            else
            {
                sb.Append(CombatLogRichText.Verb("no effect"));
            }

            sb.Append('.');
            return sb.ToString();
        }

        private string BuildHealSummary(in SpellResolvedEvent e)
        {
            if (e.targetOutcomes == null || e.targetOutcomes.Length <= 0)
                return CombatLogRichText.Verb("no effect.");

            var sb = new StringBuilder(256);
            for (int i = 0; i < e.targetOutcomes.Length; i++)
            {
                if (i > 0)
                    sb.Append(CombatLogRichText.Verb("; "));

                ref readonly var outcome = ref e.targetOutcomes[i];
                sb.Append(GetTargetName(outcome.target, rich: true));
                sb.Append(' ');

                if (outcome.saveResult.HasValue)
                {
                    sb.Append(CombatLogRichText.DegreeShort(outcome.saveResult.Value.degree));
                    sb.Append(CombatLogRichText.Verb(", "));

                    if (outcome.appliedDamage > 0)
                        sb.Append(CombatLogRichText.DamageAmountAndType(outcome.appliedDamage, DamageType.Vitality));
                    else
                        sb.Append(CombatLogRichText.Verb("no damage"));
                }
                else if (outcome.appliedHealing > 0)
                {
                    sb.Append(CombatLogRichText.Verb("regains "));
                    sb.Append(CombatLogRichText.HealAmount(outcome.appliedHealing));
                    sb.Append(CombatLogRichText.Verb(" HP"));
                }
                else
                {
                    sb.Append(CombatLogRichText.Verb("gains no healing"));
                }
            }

            sb.Append('.');
            return sb.ToString();
        }

        private string BuildHarmSummary(in SpellResolvedEvent e)
        {
            if (e.targetOutcomes == null || e.targetOutcomes.Length <= 0)
                return CombatLogRichText.Verb("no effect.");

            var sb = new StringBuilder(256);
            for (int i = 0; i < e.targetOutcomes.Length; i++)
            {
                if (i > 0)
                    sb.Append(CombatLogRichText.Verb("; "));

                ref readonly var outcome = ref e.targetOutcomes[i];
                sb.Append(GetTargetName(outcome.target, rich: true));
                sb.Append(' ');

                if (outcome.saveResult.HasValue)
                {
                    sb.Append(CombatLogRichText.DegreeShort(outcome.saveResult.Value.degree));
                    sb.Append(CombatLogRichText.Verb(", "));

                    if (outcome.appliedDamage > 0)
                        sb.Append(CombatLogRichText.DamageAmountAndType(outcome.appliedDamage, DamageType.Void));
                    else
                        sb.Append(CombatLogRichText.Verb("no damage"));
                }
                else if (outcome.appliedHealing > 0)
                {
                    sb.Append(CombatLogRichText.Verb("regains "));
                    sb.Append(CombatLogRichText.HealAmount(outcome.appliedHealing));
                    sb.Append(CombatLogRichText.Verb(" HP"));
                }
                else
                {
                    sb.Append(CombatLogRichText.Verb("no effect"));
                }
            }

            sb.Append('.');
            return sb.ToString();
        }

        private string BuildTooltipBody(in SpellResolvedEvent e)
        {
            string[] targetLines = new string[e.targetOutcomes.Length];
            for (int i = 0; i < e.targetOutcomes.Length; i++)
            {
                ref readonly var outcome = ref e.targetOutcomes[i];
                string targetName = GetTargetName(outcome.target, rich: false);

                targetLines[i] = e.spellId switch
                {
                    SpellId.ForceBarrage => $"{targetName}: {outcome.shardCount} shard(s) [{FormatShardRolls(outcome.shardRolls)}] => {outcome.appliedDamage} force ({outcome.hpBefore}->{outcome.hpAfter} HP)",
                    SpellId.ElectricArc => $"{targetName}: {FormatSaveResult(outcome.saveResult)} => {outcome.appliedDamage} electricity ({outcome.hpBefore}->{outcome.hpAfter} HP)",
                    SpellId.Snowball => $"{targetName}: {FormatAttackResult(outcome.attackResult)} => {BuildSnowballEffectSummary(in outcome)} ({outcome.hpBefore}->{outcome.hpAfter} HP)",
                    SpellId.BurningHands => $"{targetName}: {FormatSaveResult(outcome.saveResult)} => {outcome.appliedDamage} fire ({outcome.hpBefore}->{outcome.hpAfter} HP)",
                    SpellId.Fear => $"{targetName}: {FormatSaveResult(outcome.saveResult)} => {BuildFearEffectSummary(in outcome)}",
                    SpellId.Heal => BuildHealTooltipLine(targetName, in outcome),
                    SpellId.Harm => BuildHarmTooltipLine(targetName, in outcome),
                    _ => targetName
                };
            }

            return e.spellId switch
            {
                SpellId.ForceBarrage => TooltipTextBuilder.ForceBarrageBreakdown(e.actionCost, targetLines),
                SpellId.ElectricArc => TooltipTextBuilder.ElectricArcBreakdown(e.spellDc, e.rolledDamage, targetLines),
                SpellId.Snowball => TooltipTextBuilder.SnowballBreakdown(e.spellAttackModifier, e.rolledDamage, targetLines),
                SpellId.BurningHands => TooltipTextBuilder.BurningHandsBreakdown(e.spellDc, e.rolledDamage, targetLines),
                SpellId.Fear => TooltipTextBuilder.FearBreakdown(e.spellDc, targetLines),
                SpellId.Heal => TooltipTextBuilder.HealBreakdown(e.actionCost, e.spellDc, e.rolledDamage, targetLines),
                SpellId.Harm => TooltipTextBuilder.HarmBreakdown(e.actionCost, e.spellDc, e.rolledDamage, targetLines),
                _ => string.Empty
            };
        }

        private string GetTargetName(EntityHandle target, bool rich)
        {
            var targetData = entityManager.Registry != null ? entityManager.Registry.Get(target) : null;
            string rawName = targetData?.Name ?? "Unknown";
            if (!rich)
                return rawName;

            var team = targetData?.Team ?? Team.Neutral;
            return CombatLogRichText.EntityName(rawName, team);
        }

        private static string FormatShardRolls(int[] shardRolls)
        {
            if (shardRolls == null || shardRolls.Length == 0)
                return "-";

            return string.Join(", ", shardRolls);
        }

        private static string FormatSaveResult(CheckResult? saveResult)
        {
            if (!saveResult.HasValue)
                return "no save";

            var result = saveResult.Value;
            return $"{TooltipTextBuilder.FormatDegreeLabel(result.degree)} ({result.total} vs DC {result.dc}, rolled {result.naturalRoll})";
        }

        private static string FormatAttackResult(CheckResult? attackResult)
        {
            if (!attackResult.HasValue)
                return "no attack roll";

            var result = attackResult.Value;
            return $"{TooltipTextBuilder.FormatDegreeLabel(result.degree)} ({result.total} vs AC {result.dc}, rolled {result.naturalRoll})";
        }

        private static string BuildSnowballEffectSummary(in SpellResolvedTargetOutcome outcome)
        {
            if (outcome.appliedDamage <= 0)
                return "no effect";

            var sb = new StringBuilder(64);
            sb.Append(outcome.appliedDamage);
            sb.Append(" cold");

            if (outcome.appliedConditionType == ConditionType.SpeedPenalty && outcome.appliedConditionValue > 0)
            {
                sb.Append(", speed -");
                sb.Append(outcome.appliedConditionValue);
                sb.Append(" ft");
            }

            return sb.ToString();
        }

        private static string BuildFearEffectSummary(in SpellResolvedTargetOutcome outcome)
        {
            if (outcome.appliedConditionType == ConditionType.Frightened && outcome.appliedConditionValue > 0)
            {
                if (outcome.saveResult.HasValue && outcome.saveResult.Value.degree == DegreeOfSuccess.CriticalFailure)
                    return $"frightened {outcome.appliedConditionValue}, fleeing 1 round";

                return $"frightened {outcome.appliedConditionValue}";
            }

            return "no effect";
        }

        private static string BuildHealTooltipLine(string targetName, in SpellResolvedTargetOutcome outcome)
        {
            if (outcome.saveResult.HasValue)
                return $"{targetName}: {FormatSaveResult(outcome.saveResult)} => {outcome.appliedDamage} vitality ({outcome.hpBefore}->{outcome.hpAfter} HP)";

            return $"{targetName}: healed {outcome.appliedHealing} HP ({outcome.hpBefore}->{outcome.hpAfter} HP)";
        }

        private static string BuildHarmTooltipLine(string targetName, in SpellResolvedTargetOutcome outcome)
        {
            if (outcome.saveResult.HasValue)
                return $"{targetName}: {FormatSaveResult(outcome.saveResult)} => {outcome.appliedDamage} void ({outcome.hpBefore}->{outcome.hpAfter} HP)";

            return $"{targetName}: healed {outcome.appliedHealing} HP ({outcome.hpBefore}->{outcome.hpAfter} HP)";
        }
    }
}
