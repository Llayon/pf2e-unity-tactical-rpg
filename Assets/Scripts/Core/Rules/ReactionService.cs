using System;
using System.Collections.Generic;
using UnityEngine;
using PF2e.TurnSystem;

namespace PF2e.Core
{
    /// <summary>
    /// Deterministic reaction eligibility collector.
    /// Iterates initiative order directly and writes into caller-owned buffer.
    /// </summary>
    public static class ReactionService
    {
        public static void CollectEligibleReactions(
            ReactionTriggerPhase phase,
            EntityHandle triggerSource,
            EntityHandle triggerTarget,
            IReadOnlyList<InitiativeEntry> initiativeOrder,
            Func<EntityHandle, EntityData> getEntity,
            List<ReactionOption> outOptions)
        {
            if (outOptions == null)
                return;

            outOptions.Clear();

            if (initiativeOrder == null || getEntity == null)
                return;

            // Reserved for future expansion (AoO/counters involving source actor).
            _ = triggerSource;

            for (int i = 0; i < initiativeOrder.Count; i++)
            {
                var handle = initiativeOrder[i].Handle;
                if (!handle.IsValid)
                    continue;

                var entity = getEntity(handle);
                if (!IsEligible(phase, handle, triggerTarget, entity))
                    continue;

                outOptions.Add(new ReactionOption(handle, ReactionType.ShieldBlock, phase));
            }

            // MVP guardrail: Shield Block is self-only and should produce at most one option.
            if (outOptions.Count > 1)
            {
                var first = outOptions[0];
                Debug.LogWarning(
                    $"[ReactionService] More than one eligible reaction found ({outOptions.Count}) for phase {phase}. " +
                    "MVP guard keeps only the first option.");
                outOptions.Clear();
                outOptions.Add(first);
            }
        }

        private static bool IsEligible(
            ReactionTriggerPhase phase,
            EntityHandle handle,
            EntityHandle triggerTarget,
            EntityData entity)
        {
            if (phase != ReactionTriggerPhase.PostHit)
                return false;

            // Shield Block is self-only in PF2e.
            if (handle != triggerTarget)
                return false;

            if (entity == null || !entity.IsAlive)
                return false;

            if (!entity.ReactionAvailable)
                return false;

            var shield = entity.EquippedShield;
            if (!shield.IsEquipped || shield.IsBroken || !shield.isRaised)
                return false;

            return true;
        }
    }
}
