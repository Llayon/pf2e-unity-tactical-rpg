using System;
using System.Collections.Generic;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Runtime coordinator for passive Reactive Strike triggers.
    /// </summary>
    public sealed class ReactiveStrikeRuntimeCoordinator
    {
        private readonly TriggerWindowLedger triggerWindowLedger = new();
        private readonly ReactiveStrikeTriggerOrchestrator triggerOrchestrator = new();
        private readonly ReactiveStrikeTriggerExecutor triggerExecutor = new();

        public void SetRngForTesting(IRng rng)
        {
            triggerExecutor.SetRngForTesting(rng);
        }

        public void ClearAll()
        {
            triggerWindowLedger.Clear();
            triggerOrchestrator.ClearTransientState();
        }

        public void HandleEntityMoved(
            in EntityMovedEvent e,
            TurnState state,
            IReadOnlyList<InitiativeEntry> initiativeOrder,
            EntityManager entityManager,
            StrikeAction strikeAction,
            Func<EntityHandle, bool> canUseReaction,
            CombatEventBus eventBus)
        {
            if (!CanProcessRuntimeEvents(state, initiativeOrder, entityManager, strikeAction, canUseReaction))
                return;

            var triggerWindowToken = triggerWindowLedger.OpenWindow(TriggerWindowType.MovementEnter);
            try
            {
                triggerOrchestrator.HandleEntityMoved(
                    in e,
                    initiativeOrder,
                    entityManager,
                    strikeAction,
                    canUseReaction,
                    (actor, target, reason, token) => triggerExecutor.Resolve(
                        actor,
                        target,
                        reason,
                        triggerWindowLedger,
                        token,
                        entityManager,
                        strikeAction,
                        eventBus,
                        canUseReaction),
                    triggerWindowToken);
            }
            finally
            {
                triggerWindowLedger.CloseWindow(triggerWindowToken);
            }
        }

        public void HandleStrikePreDamage(
            in StrikePreDamageEvent e,
            TurnState state,
            IReadOnlyList<InitiativeEntry> initiativeOrder,
            EntityManager entityManager,
            StrikeAction strikeAction,
            Func<EntityHandle, bool> canUseReaction,
            CombatEventBus eventBus)
        {
            if (triggerExecutor.IsResolving)
                return;
            if (!CanProcessRuntimeEvents(state, initiativeOrder, entityManager, strikeAction, canUseReaction))
                return;

            var triggerWindowToken = triggerWindowLedger.OpenWindow(TriggerWindowType.AttackStart);
            try
            {
                triggerOrchestrator.HandleStrikePreDamage(
                    in e,
                    initiativeOrder,
                    entityManager,
                    strikeAction,
                    canUseReaction,
                    (actor, target, reason, token) => triggerExecutor.Resolve(
                        actor,
                        target,
                        reason,
                        triggerWindowLedger,
                        token,
                        entityManager,
                        strikeAction,
                        eventBus,
                        canUseReaction),
                    triggerWindowToken);
            }
            finally
            {
                triggerWindowLedger.CloseWindow(triggerWindowToken);
            }
        }

        public bool HandleCombatActionStarted(
            in CombatActionStartedEvent e,
            TurnState state,
            IReadOnlyList<InitiativeEntry> initiativeOrder,
            EntityManager entityManager,
            StrikeAction strikeAction,
            Func<EntityHandle, bool> canUseReaction,
            CombatEventBus eventBus)
        {
            if (triggerExecutor.IsResolving)
                return false;
            if (!CanProcessRuntimeEvents(state, initiativeOrder, entityManager, strikeAction, canUseReaction))
                return false;

            var triggerWindowToken = triggerWindowLedger.OpenWindow(TriggerWindowType.ActionStart);
            try
            {
                var actionStarted = e;

                return triggerOrchestrator.HandleCombatActionStarted(
                    in e,
                    initiativeOrder,
                    entityManager,
                    strikeAction,
                    canUseReaction,
                    (actor, target, reason, token) => triggerExecutor.ResolveActionStart(
                        actor,
                        target,
                        reason,
                        actionStarted,
                        triggerWindowLedger,
                        token,
                        entityManager,
                        strikeAction,
                        eventBus,
                        canUseReaction),
                    triggerWindowToken);
            }
            finally
            {
                triggerWindowLedger.CloseWindow(triggerWindowToken);
            }
        }

        private static bool CanProcessRuntimeEvents(
            TurnState state,
            IReadOnlyList<InitiativeEntry> initiativeOrder,
            EntityManager entityManager,
            StrikeAction strikeAction,
            Func<EntityHandle, bool> canUseReaction)
        {
            if (state == TurnState.Inactive || state == TurnState.RollingInitiative || state == TurnState.CombatOver)
                return false;
            if (initiativeOrder == null || initiativeOrder.Count <= 0)
                return false;
            if (entityManager == null || entityManager.Registry == null)
                return false;
            if (strikeAction == null || canUseReaction == null)
                return false;

            return true;
        }
    }
}
