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
        public void Apply(EntityData entity, ConditionType type, int value, int rounds, List<ConditionDelta> outDeltas)
        {
            if (entity == null || outDeltas == null) return;

            for (int i = 0; i < entity.Conditions.Count; i++)
            {
                var cond = entity.Conditions[i];
                if (cond.Type != type) continue;

                int oldValue = cond.Value;
                if (value > cond.Value)
                {
                    cond.Value = value;
                    outDeltas.Add(new ConditionDelta(
                        entity.Handle,
                        type,
                        ConditionChangeType.ValueChanged,
                        oldValue,
                        cond.Value));
                }
                return;
            }

            entity.Conditions.Add(new ActiveCondition(type, value, rounds));
            outDeltas.Add(new ConditionDelta(
                entity.Handle,
                type,
                ConditionChangeType.Added,
                0,
                Mathf.Max(0, value)));
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
                if (!ConditionRules.AutoDecrementsAtEndOfTurn(cond.Type)) continue;
                if (cond.Value <= 0) continue;

                int oldValue = cond.Value;
                if (cond.TickDown())
                {
                    entity.Conditions.RemoveAt(i);
                    outDeltas.Add(new ConditionDelta(
                        entity.Handle,
                        cond.Type,
                        ConditionChangeType.Removed,
                        oldValue,
                        0));
                }
                else
                {
                    outDeltas.Add(new ConditionDelta(
                        entity.Handle,
                        cond.Type,
                        ConditionChangeType.ValueChanged,
                        oldValue,
                        cond.Value));
                }
            }
        }
    }
}
