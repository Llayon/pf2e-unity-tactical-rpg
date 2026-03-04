using System;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Executes a single Ready Strike trigger after orchestration chooses actor/target.
    /// Owns in-flight resolving state used to suppress nested strike pre-damage trigger chains.
    /// </summary>
    public sealed class ReadyStrikeTriggerExecutor
    {
        private bool isResolving;

        public bool IsResolving => isResolving;

        public void Resolve(
            EntityHandle actor,
            EntityHandle target,
            string triggerReason,
            ReadyStrikeService readyStrikeService,
            EntityManager entityManager,
            StrikeAction strikeAction,
            CombatEventBus eventBus,
            Func<EntityHandle, bool> canUseReaction)
        {
            if (!actor.IsValid || !target.IsValid)
                return;
            if (readyStrikeService == null || !readyStrikeService.HasPrepared(actor))
                return;
            if (entityManager == null || entityManager.Registry == null)
                return;
            if (strikeAction == null || canUseReaction == null)
                return;

            ReadyTriggerMode preparedTriggerMode = ReadyTriggerMode.Any;
            readyStrikeService.TryGetTriggerMode(actor, out preparedTriggerMode);

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive)
            {
                readyStrikeService.TryRemovePrepared(actor);
                return;
            }

            if (!readyStrikeService.TryConsumeReactionInScope(actor, actorData, canUseReaction))
                return;
            readyStrikeService.TryRemovePrepared(actor);

            bool wasResolving = isResolving;
            isResolving = true;
            try
            {
                ReactionBroker.TryExecuteReadiedStrike(
                    actor,
                    target,
                    triggerReason,
                    strikeAction,
                    eventBus,
                    handle => entityManager.Registry.Get(handle),
                    UnityRng.Shared,
                    preparedTriggerMode,
                    aidCircumstanceBonus: 0);
            }
            finally
            {
                isResolving = wasResolving;
            }
        }
    }
}
