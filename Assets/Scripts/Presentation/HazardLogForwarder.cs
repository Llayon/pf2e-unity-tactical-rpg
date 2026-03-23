using System.Collections.Generic;
using PF2e.Core;
using PF2e.Managers;
using UnityEngine;

namespace PF2e.Presentation
{
    public class HazardLogForwarder : MonoBehaviour
    {
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null)
                Debug.LogError("[HazardLogForwarder] Missing CombatEventBus", this);
            if (entityManager == null)
                Debug.LogError("[HazardLogForwarder] Missing EntityManager", this);
        }
#endif

        private void OnEnable()
        {
            if (eventBus == null || entityManager == null)
            {
                Debug.LogError("[HazardLogForwarder] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnHazardTriggeredTyped += HandleHazardTriggered;
        }

        private void OnDisable()
        {
            if (eventBus != null)
                eventBus.OnHazardTriggeredTyped -= HandleHazardTriggered;
        }

        private void HandleHazardTriggered(in HazardTriggeredEvent e)
        {
            string hazardName = string.IsNullOrWhiteSpace(e.hazardName) ? "Hazard" : e.hazardName;
            string hazardLink = CombatLogLinkHelper.Link(
                CombatLogLinkTokens.Result,
                CombatLogRichText.Weapon(hazardName));

            string summary = BuildSummary(in e);
            var tooltipPayload = new CombatLogTooltipPayload(new[]
            {
                new TooltipEntry(
                    CombatLogLinkTokens.Result,
                    hazardName,
                    BuildTooltipBody(in e),
                    TooltipLayoutProfile.Extended)
            });

            eventBus.Publish(
                e.target,
                $"{CombatLogRichText.Verb("triggers")} {hazardLink} {CombatLogRichText.Verb("—")} {summary}",
                CombatLogCategory.ActionResult,
                tooltipPayload);
        }

        private string BuildSummary(in HazardTriggeredEvent e)
        {
            var segments = new List<string>(5);
            bool hasOutcome = false;

            if (e.saveResult.HasValue)
                segments.Add(CombatLogRichText.DegreeShort(e.saveResult.Value.degree));

            if (e.appliedDamage > 0)
            {
                segments.Add(CombatLogRichText.DamageAmountAndType(e.appliedDamage, e.damageType));
                hasOutcome = true;
            }

            if (e.movedCells > 0)
            {
                segments.Add(CombatLogRichText.Verb($"{(e.pulledTowardOrigin ? "pulled" : "pushed")} {e.movedCells} cell{(e.movedCells == 1 ? string.Empty : "s")}"));
                hasOutcome = true;
            }

            if (e.primaryConditionType.HasValue)
            {
                segments.Add(CombatLogRichText.ConditionGain(BuildConditionLabel(e.primaryConditionType.Value, e.primaryConditionValue)));
                hasOutcome = true;
            }

            if (e.secondaryConditionType.HasValue)
            {
                segments.Add(CombatLogRichText.ConditionGain(BuildConditionLabel(e.secondaryConditionType.Value, e.secondaryConditionValue)));
                hasOutcome = true;
            }

            if (!hasOutcome)
                segments.Add(CombatLogRichText.Verb("no effect"));

            return string.Join(CombatLogRichText.Verb(", "), segments) + ".";
        }

        private string BuildTooltipBody(in HazardTriggeredEvent e)
        {
            string[] detailLines = BuildDetailLines(in e);
            string contextLine = BuildContextLine(in e);
            return TooltipTextBuilder.HazardBreakdown(e.hazardName, contextLine, detailLines);
        }

        private string BuildContextLine(in HazardTriggeredEvent e)
        {
            var parts = new List<string>(4)
            {
                $"cell {e.hazardCell}"
            };

            if (e.saveType.HasValue && e.saveResult.HasValue)
                parts.Add($"{e.saveType.Value} DC {e.saveResult.Value.dc}");

            if (e.rolledDamage > 0)
                parts.Add($"rolled {e.rolledDamage} {e.damageType.ToString().ToLowerInvariant()}");

            if (e.movedCells > 0)
                parts.Add($"{(e.pulledTowardOrigin ? "pull" : "push")} x{e.movedCells}");

            return string.Join(" • ", parts);
        }

        private string[] BuildDetailLines(in HazardTriggeredEvent e)
        {
            var lines = new List<string>(5);

            if (e.saveType.HasValue && e.saveResult.HasValue)
            {
                var save = e.saveResult.Value;
                lines.Add($"{e.saveType.Value}: {TooltipTextBuilder.FormatDegreeLabel(save.degree)} ({save.total} vs DC {save.dc}, rolled {save.naturalRoll})");
            }

            if (e.appliedDamage > 0)
                lines.Add($"Damage: {e.appliedDamage} {e.damageType.ToString().ToLowerInvariant()} ({e.hpBefore}->{e.hpAfter} HP)");
            else
                lines.Add($"HP: {e.hpBefore}->{e.hpAfter}");

            if (e.movedCells > 0)
                lines.Add($"Forced movement: {(e.pulledTowardOrigin ? "pulled" : "pushed")} {e.movedCells} cell{(e.movedCells == 1 ? string.Empty : "s")} to {e.positionAfter}");

            if (e.primaryConditionType.HasValue || e.secondaryConditionType.HasValue)
            {
                var conditionParts = new List<string>(2);
                if (e.primaryConditionType.HasValue)
                    conditionParts.Add(BuildConditionLabel(e.primaryConditionType.Value, e.primaryConditionValue));
                if (e.secondaryConditionType.HasValue)
                    conditionParts.Add(BuildConditionLabel(e.secondaryConditionType.Value, e.secondaryConditionValue));
                lines.Add($"Conditions: {string.Join(", ", conditionParts)}");
            }

            if (e.targetDefeated)
                lines.Add("Target defeated");

            return lines.ToArray();
        }

        private static string BuildConditionLabel(ConditionType conditionType, int value)
        {
            string conditionName = ConditionRules.DisplayName(conditionType);
            if (conditionType == ConditionType.SpeedPenalty && value > 0)
                return $"{conditionName} {value} ft";

            if (ConditionRules.IsValued(conditionType) && value > 0)
                return $"{conditionName} {value}";

            return conditionName;
        }
    }
}
