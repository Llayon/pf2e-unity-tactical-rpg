using System;
using System.Collections.Generic;

namespace PF2e.Core
{
    /// <summary>
    /// Source-of-truth for prepared Aid state and Aid reaction resolution.
    /// </summary>
    public sealed class AidService
    {
        public const int DefaultAidDc = 15;

        private static readonly CheckSource AidStrikeSource = CheckSource.Custom("AID-ATK");

        // Contract: one active prepared Aid per ally.
        private readonly Dictionary<EntityHandle, AidPreparedRecord> preparedAidByAlly = new();
        private readonly Dictionary<EntityHandle, int> helperTurnStartCounts = new();
        private readonly List<EntityHandle> removeBuffer = new();

        public int PreparedAidCount => preparedAidByAlly.Count;

        public bool PrepareAid(EntityHandle helper, EntityHandle ally, int preparedRound)
        {
            if (!helper.IsValid || !ally.IsValid)
                return false;
            if (helper == ally)
                return false;

            int helperTurnStartCount = GetHelperTurnStartCount(helper);
            preparedAidByAlly[ally] = new AidPreparedRecord(
                helper,
                ally,
                preparedRound,
                helperTurnStartCount);
            return true;
        }

        public bool HasPreparedAidForAlly(EntityHandle ally)
        {
            return ally.IsValid && preparedAidByAlly.ContainsKey(ally);
        }

        public bool TryGetPreparedAidForAlly(EntityHandle ally, out AidPreparedRecord record)
        {
            if (!ally.IsValid)
            {
                record = default;
                return false;
            }

            return preparedAidByAlly.TryGetValue(ally, out record);
        }

        /// <summary>
        /// Should be called at start of each actor turn.
        /// Expires aids prepared by this actor on their previous turn.
        /// </summary>
        public int NotifyTurnStarted(EntityHandle actor)
        {
            if (!actor.IsValid)
                return 0;

            int nextTurnStartCount = GetHelperTurnStartCount(actor) + 1;
            helperTurnStartCounts[actor] = nextTurnStartCount;
            return ExpirePreparedAidForHelper(actor, nextTurnStartCount);
        }

        public bool TryConsumeAidForCheck(
            in AidCheckContext context,
            Func<EntityHandle, EntityData> getEntity,
            Func<EntityHandle, bool> canUseReaction,
            IRng rng,
            out AidOutcome outcome)
        {
            outcome = default;

            if (!context.ally.IsValid || getEntity == null)
                return false;
            if (!preparedAidByAlly.TryGetValue(context.ally, out var prepared))
                return false;

            var helperData = getEntity(prepared.helper);
            var allyData = getEntity(prepared.ally);
            if (helperData == null || allyData == null || !helperData.IsAlive || !allyData.IsAlive)
            {
                // Stale record safety cleanup.
                preparedAidByAlly.Remove(context.ally);
                return false;
            }

            if (canUseReaction != null && !canUseReaction(prepared.helper))
                return false;

            if (!TryResolveCheckInputs(helperData, in context, out int modifier, out var source, out var proficiency))
                return false;

            var check = CheckResolver.RollCheck(modifier, DefaultAidDc, in source, rng);
            int appliedModifier = ComputeAidModifier(check.degree, proficiency);

            helperData.ReactionAvailable = false;
            preparedAidByAlly.Remove(context.ally);

            outcome = new AidOutcome(
                prepared.helper,
                prepared.ally,
                context.checkType,
                context.skill,
                context.triggeringActionName,
                in check.roll,
                DefaultAidDc,
                check.degree,
                appliedModifier,
                reactionConsumed: true);
            return true;
        }

        public void ClearAll()
        {
            preparedAidByAlly.Clear();
            helperTurnStartCounts.Clear();
            removeBuffer.Clear();
        }

        private int GetHelperTurnStartCount(EntityHandle helper)
        {
            if (!helper.IsValid)
                return 0;

            return helperTurnStartCounts.TryGetValue(helper, out int count)
                ? count
                : 0;
        }

        private int ExpirePreparedAidForHelper(EntityHandle helper, int helperTurnStartCount)
        {
            if (!helper.IsValid || preparedAidByAlly.Count <= 0)
                return 0;

            removeBuffer.Clear();
            foreach (var kvp in preparedAidByAlly)
            {
                var record = kvp.Value;
                if (record.helper != helper)
                    continue;
                if (record.preparedOnHelperTurnStartCount >= helperTurnStartCount)
                    continue;

                removeBuffer.Add(kvp.Key);
            }

            int expiredCount = removeBuffer.Count;
            for (int i = 0; i < removeBuffer.Count; i++)
                preparedAidByAlly.Remove(removeBuffer[i]);

            removeBuffer.Clear();
            return expiredCount;
        }

        private static bool TryResolveCheckInputs(
            EntityData helperData,
            in AidCheckContext context,
            out int modifier,
            out CheckSource source,
            out ProficiencyRank proficiency)
        {
            modifier = 0;
            source = default;
            proficiency = ProficiencyRank.Untrained;

            switch (context.checkType)
            {
                case AidCheckType.Skill:
                    if (!context.skill.HasValue)
                        return false;

                    var skill = context.skill.Value;
                    modifier = helperData.GetSkillModifier(skill);
                    source = CheckSource.Skill(skill);
                    proficiency = helperData.GetSkillProfRank(skill);
                    return true;

                case AidCheckType.Strike:
                    modifier = helperData.GetAttackBonus(helperData.EquippedWeapon);
                    source = AidStrikeSource;
                    proficiency = helperData.GetWeaponProfRank(helperData.EquippedWeapon.Category);
                    return true;

                default:
                    return false;
            }
        }

        private static int ComputeAidModifier(DegreeOfSuccess degree, ProficiencyRank proficiency)
        {
            switch (degree)
            {
                case DegreeOfSuccess.CriticalSuccess:
                    if (proficiency >= ProficiencyRank.Legendary) return 4;
                    if (proficiency >= ProficiencyRank.Master) return 3;
                    return 2;

                case DegreeOfSuccess.Success:
                    return 1;

                case DegreeOfSuccess.CriticalFailure:
                    return -1;

                default:
                    return 0;
            }
        }
    }
}
