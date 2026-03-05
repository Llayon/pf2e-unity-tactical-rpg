using System;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Runtime coordinator for Ready Strike state + trigger handling.
    /// Keeps TurnManager at state-machine level while delegating Ready internals.
    /// </summary>
    public sealed class ReadyStrikeRuntimeCoordinator
    {
        private readonly ReadyStrikeService readyStrikeService = new();
        private readonly ReadyStrikeTriggerOrchestrator triggerOrchestrator = new();
        private readonly ReadyStrikeTriggerExecutor triggerExecutor = new();

        public int PreparedCount => readyStrikeService.PreparedCount;
        public bool IsResolvingTrigger => triggerExecutor.IsResolving;
        public TriggerWindowLedger TriggerWindowLedger => readyStrikeService.TriggerWindowLedger;

        public bool HasPrepared(EntityHandle actor)
        {
            return readyStrikeService.HasPrepared(actor);
        }

        public bool TryGetPreparedTriggerMode(EntityHandle actor, out ReadyTriggerMode triggerMode)
        {
            return readyStrikeService.TryGetTriggerMode(actor, out triggerMode);
        }

        public bool TryPrepare(
            EntityHandle actor,
            int preparedRound,
            EntityData actorData,
            Func<EntityHandle, bool> canUseReaction,
            ReadyTriggerMode triggerMode = ReadyTriggerMode.Any)
        {
            if (!actor.IsValid)
                return false;
            if (actorData == null || !actorData.IsAlive)
                return false;
            if (canUseReaction == null || !canUseReaction(actor))
                return false;

            return readyStrikeService.TryPrepare(actor, preparedRound, triggerMode);
        }

        public void ClearAll()
        {
            readyStrikeService.ClearAll();
            triggerOrchestrator.ClearTransientState();
        }

        public void ExpirePreparedAtTurnStart(EntityHandle actor, CombatEventBus eventBus)
        {
            if (!actor.IsValid)
                return;
            if (!readyStrikeService.TryRemovePrepared(actor))
                return;

            eventBus?.Publish(actor, "readied Strike expires at turn start.", CombatLogCategory.Turn);
        }

        public void HandleEntityMoved(
            in EntityMovedEvent e,
            TurnState state,
            EntityManager entityManager,
            StrikeAction strikeAction,
            Func<EntityHandle, int> findInitiativeIndex,
            Func<EntityHandle, bool> canUseReaction,
            CombatEventBus eventBus)
        {
            if (!CanProcessRuntimeEvents(state, entityManager, strikeAction))
                return;
            if (findInitiativeIndex == null || canUseReaction == null)
                return;

            triggerOrchestrator.HandleEntityMoved(
                in e,
                readyStrikeService,
                entityManager,
                strikeAction,
                findInitiativeIndex,
                (actor, target, triggerReason, triggerWindowToken) => triggerExecutor.Resolve(
                    actor,
                    target,
                    triggerReason,
                    readyStrikeService,
                    entityManager,
                    strikeAction,
                    eventBus,
                    canUseReaction,
                    triggerWindowToken));
        }

        public void HandleStrikePreDamage(
            in StrikePreDamageEvent e,
            TurnState state,
            EntityManager entityManager,
            StrikeAction strikeAction,
            Func<EntityHandle, int> findInitiativeIndex,
            Func<EntityHandle, bool> canUseReaction,
            CombatEventBus eventBus)
        {
            if (triggerExecutor.IsResolving)
                return;
            if (!CanProcessRuntimeEvents(state, entityManager, strikeAction))
                return;
            if (findInitiativeIndex == null || canUseReaction == null)
                return;

            triggerOrchestrator.HandleStrikePreDamage(
                in e,
                readyStrikeService,
                entityManager,
                strikeAction,
                findInitiativeIndex,
                (actor, target, triggerReason, triggerWindowToken) => triggerExecutor.Resolve(
                    actor,
                    target,
                    triggerReason,
                    readyStrikeService,
                    entityManager,
                    strikeAction,
                    eventBus,
                    canUseReaction,
                    triggerWindowToken));
        }

        private bool CanProcessRuntimeEvents(
            TurnState state,
            EntityManager entityManager,
            StrikeAction strikeAction)
        {
            if (state == TurnState.Inactive || state == TurnState.RollingInitiative || state == TurnState.CombatOver)
                return false;
            if (readyStrikeService.PreparedCount <= 0)
                return false;
            if (entityManager == null || entityManager.Registry == null)
                return false;
            if (strikeAction == null)
                return false;

            return true;
        }
    }
}
