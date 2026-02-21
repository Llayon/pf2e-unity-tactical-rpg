using System.Collections.Generic;
using UnityEngine;

namespace PF2e.Core
{
    /// <summary>
    /// Single mutation entrypoint for condition state changes.
    /// Domain-only service: mutates EntityData and writes deltas into caller-owned buffers.
    /// </summary>
    public sealed class ConditionService
    {
        /// <summary>
        /// Backward-compatible alias. Prefer AddOrRefresh for new code.
        /// </summary>
        public void Apply(EntityData entity, ConditionType type, int value, int rounds, List<ConditionDelta> outDeltas)
        {
            AddOrRefresh(entity, type, value, rounds, outDeltas);
        }

        /// <summary>
        /// Add a condition or refresh an existing instance.
        /// Value and duration are merged independently.
        /// </summary>
        public void AddOrRefresh(EntityData entity, ConditionType type, int value, int rounds, List<ConditionDelta> outDeltas)
        {
            if (entity == null || outDeltas == null) return;

            int incomingValue = NormalizeValue(type, value);
            int incomingRounds = NormalizeRounds(rounds);

            for (int i = 0; i < entity.Conditions.Count; i++)
            {
                var cond = entity.Conditions[i];
                if (cond.Type != type) continue;

                int oldValue = cond.Value;
                int oldRounds = cond.RemainingRounds;
                int mergedValue = Mathf.Max(cond.Value, incomingValue);
                int mergedRounds = MergeRounds(cond.RemainingRounds, incomingRounds);
                bool valueChanged = mergedValue != oldValue;
                bool roundsChanged = mergedRounds != oldRounds;

                if (!valueChanged && !roundsChanged)
                    return;

                cond.Value = mergedValue;
                cond.RemainingRounds = mergedRounds;

                outDeltas.Add(new ConditionDelta(
                    entity.Handle,
                    type,
                    valueChanged ? ConditionChangeType.ValueChanged : ConditionChangeType.DurationChanged,
                    oldValue,
                    cond.Value,
                    oldRounds,
                    cond.RemainingRounds));
                return;
            }

            entity.Conditions.Add(new ActiveCondition(type, incomingValue, incomingRounds));
            outDeltas.Add(new ConditionDelta(
                entity.Handle,
                type,
                ConditionChangeType.Added,
                0,
                incomingValue,
                0,
                incomingRounds));
        }

        public void Remove(EntityData entity, ConditionType type, List<ConditionDelta> outDeltas)
        {
            if (entity == null || outDeltas == null) return;

            for (int i = entity.Conditions.Count - 1; i >= 0; i--)
            {
                var cond = entity.Conditions[i];
                if (cond.Type != type) continue;

                outDeltas.Add(new ConditionDelta(
                    entity.Handle,
                    type,
                    ConditionChangeType.Removed,
                    cond.Value,
                    0,
                    cond.RemainingRounds,
                    0));

                entity.Conditions.RemoveAt(i);
            }
        }

        public void TickStartTurn(EntityData entity, List<ConditionDelta> outDeltas)
        {
            if (entity == null || outDeltas == null) return;

            entity.ActionsRemaining = 3;
            entity.MAPCount = 0;
            entity.ReactionAvailable = true;

            int slowed = entity.GetConditionValue(ConditionType.Slowed);
            entity.ActionsRemaining -= slowed;

            int stunned = entity.GetConditionValue(ConditionType.Stunned);
            entity.ActionsRemaining -= stunned;
            entity.ActionsRemaining = Mathf.Max(0, entity.ActionsRemaining);

            if (stunned > 0)
                Remove(entity, ConditionType.Stunned, outDeltas);
        }

        public void TickEndTurn(EntityData entity, List<ConditionDelta> outDeltas)
        {
            if (entity == null || outDeltas == null) return;

            for (int i = entity.Conditions.Count - 1; i >= 0; i--)
            {
                var cond = entity.Conditions[i];

                bool decrementValue = ConditionRules.AutoDecrementsAtEndOfTurn(cond.Type) && cond.Value > 0;
                bool decrementRounds = cond.RemainingRounds > 0;
                if (!decrementValue && !decrementRounds) continue;

                int oldValue = cond.Value;
                int oldRounds = cond.RemainingRounds;

                if (cond.TickDown(decrementValue, decrementRounds))
                {
                    outDeltas.Add(new ConditionDelta(
                        entity.Handle,
                        cond.Type,
                        ConditionChangeType.Removed,
                        oldValue,
                        0,
                        oldRounds,
                        0));

                    entity.Conditions.RemoveAt(i);
                    continue;
                }

                bool valueChanged = cond.Value != oldValue;
                bool roundsChanged = cond.RemainingRounds != oldRounds;
                if (!valueChanged && !roundsChanged) continue;

                outDeltas.Add(new ConditionDelta(
                    entity.Handle,
                    cond.Type,
                    valueChanged ? ConditionChangeType.ValueChanged : ConditionChangeType.DurationChanged,
                    oldValue,
                    cond.Value,
                    oldRounds,
                    cond.RemainingRounds));
            }
        }

        private static int NormalizeValue(ConditionType type, int value)
        {
            if (!ConditionRules.IsValued(type))
                return 0;

            return Mathf.Max(0, value);
        }

        private static int NormalizeRounds(int rounds)
        {
            return rounds < 0 ? -1 : rounds;
        }

        private static int MergeRounds(int currentRounds, int incomingRounds)
        {
            if (currentRounds < 0 || incomingRounds < 0)
                return -1;

            return Mathf.Max(currentRounds, incomingRounds);
        }
    }
}
