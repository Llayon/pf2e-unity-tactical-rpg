using System.Collections.Generic;

namespace PF2e.Core
{
    public static class ConditionRules
    {
        public static bool IsValued(ConditionType type) => type switch
        {
            ConditionType.Frightened or ConditionType.Sickened or
            ConditionType.Stunned or ConditionType.Slowed or
            ConditionType.Wounded or ConditionType.Dying or
            ConditionType.Doomed => true,
            _ => false
        };

        // MVP: Sickened auto-ticks (RAW requires Fort save — future).
        public static bool AutoDecrementsAtEndOfTurn(ConditionType type) => type switch
        {
            ConditionType.Frightened => true,
            ConditionType.Sickened => true,
            _ => false
        };

        public static string DisplayName(ConditionType type) => type switch
        {
            ConditionType.OffGuard => "off-guard",
            _ => type.ToString().ToLowerInvariant()
        };

        /// <summary>
        /// PF2e stacking for current MVP combat slice:
        /// - Status penalties (Frightened/Sickened) do not stack: use max.
        /// - Circumstance penalties do not stack per target metric.
        /// - Attack: prone gives circumstance -2.
        /// - AC: off-guard or prone gives circumstance -2 (prone implies off-guard for AC context).
        /// </summary>
        public static void ComputeAttackAndAcPenalties(
            IReadOnlyList<ActiveCondition> conditions,
            out int attackPenalty,
            out int acPenalty)
        {
            int statusPenalty = 0;
            bool hasProne = false;
            bool hasOffGuard = false;

            if (conditions != null)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    var condition = conditions[i];
                    switch (condition.Type)
                    {
                        case ConditionType.Frightened:
                        case ConditionType.Sickened:
                            if (condition.Value > statusPenalty)
                                statusPenalty = condition.Value;
                            break;
                        case ConditionType.Prone:
                            hasProne = true;
                            break;
                        case ConditionType.OffGuard:
                            hasOffGuard = true;
                            break;
                    }
                }
            }

            int attackCircumstancePenalty = hasProne ? 2 : 0;
            int acCircumstancePenalty = (hasOffGuard || hasProne) ? 2 : 0;

            attackPenalty = statusPenalty + attackCircumstancePenalty;
            acPenalty = statusPenalty + acCircumstancePenalty;
        }
    }
}
