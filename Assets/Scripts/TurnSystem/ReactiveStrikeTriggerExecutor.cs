using System;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Executes a single Reactive Strike trigger and consumes the actor reaction.
    /// </summary>
    public sealed class ReactiveStrikeTriggerExecutor
    {
        private bool isResolving;
        private IRng attackRng;

        public bool IsResolving => isResolving;

        public void SetRngForTesting(IRng rng)
        {
            attackRng = rng;
        }

        public void Resolve(
            EntityHandle actor,
            EntityHandle target,
            string triggerReason,
            TriggerWindowLedger triggerWindowLedger,
            TriggerWindowToken triggerWindowToken,
            EntityManager entityManager,
            StrikeAction strikeAction,
            CombatEventBus eventBus,
            Func<EntityHandle, bool> canUseReaction)
        {
            _ = ResolveCore(
                actor,
                target,
                triggerReason,
                triggerWindowLedger,
                triggerWindowToken,
                entityManager,
                strikeAction,
                eventBus,
                canUseReaction);
        }

        public bool ResolveActionStart(
            EntityHandle actor,
            EntityHandle target,
            string triggerReason,
            CombatActionStartedEvent actionStarted,
            TriggerWindowLedger triggerWindowLedger,
            TriggerWindowToken triggerWindowToken,
            EntityManager entityManager,
            StrikeAction strikeAction,
            CombatEventBus eventBus,
            Func<EntityHandle, bool> canUseReaction)
        {
            var resolution = ResolveCore(
                actor,
                target,
                triggerReason,
                triggerWindowLedger,
                triggerWindowToken,
                entityManager,
                strikeAction,
                eventBus,
                canUseReaction);

            bool disrupted = resolution.executed
                && actionStarted.HasTrait(CombatActionTraitFlags.Manipulate)
                && resolution.degree == DegreeOfSuccess.CriticalSuccess;

            if (disrupted)
            {
                string actionLabel = string.IsNullOrWhiteSpace(actionStarted.actionName)
                    ? "the action"
                    : actionStarted.actionName;
                eventBus?.Publish(
                    actor,
                    $"Reactive Strike critically disrupts {actionLabel}.",
                    CombatLogCategory.Turn);
            }

            return disrupted;
        }

        private ReactiveStrikeResolution ResolveCore(
            EntityHandle actor,
            EntityHandle target,
            string triggerReason,
            TriggerWindowLedger triggerWindowLedger,
            TriggerWindowToken triggerWindowToken,
            EntityManager entityManager,
            StrikeAction strikeAction,
            CombatEventBus eventBus,
            Func<EntityHandle, bool> canUseReaction)
        {
            if (!actor.IsValid || !target.IsValid)
                return default;
            if (triggerWindowLedger == null || !triggerWindowToken.IsValid)
                return default;
            if (entityManager == null || entityManager.Registry == null || strikeAction == null || canUseReaction == null)
                return default;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive || !actorData.HasReactiveStrike)
                return default;
            if (!triggerWindowLedger.CanReact(triggerWindowToken, actor))
                return default;
            if (!canUseReaction(actor))
                return default;

            bool wasResolving = isResolving;
            isResolving = true;
            try
            {
                var resolution = ReactionBroker.TryExecuteReactiveStrike(
                    actor,
                    target,
                    triggerReason,
                    strikeAction,
                    eventBus,
                    handle => entityManager.Registry.Get(handle),
                    attackRng ?? UnityRng.Shared);

                if (!resolution.executed)
                    return default;

                ReactionBroker.TryConsumeReactionInWindow(
                    actor,
                    actorData,
                    canUseReaction,
                    triggerWindowLedger,
                    triggerWindowToken);

                return resolution;
            }
            finally
            {
                isResolving = wasResolving;
            }
        }
    }
}
