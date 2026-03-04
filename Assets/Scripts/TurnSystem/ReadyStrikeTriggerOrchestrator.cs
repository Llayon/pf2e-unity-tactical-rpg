using System;
using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Event-driven orchestration for Ready Strike trigger candidate collection and dispatch.
    /// Keeps TurnManager focused on turn-state ownership.
    /// </summary>
    public sealed class ReadyStrikeTriggerOrchestrator
    {
        private readonly List<EntityHandle> triggerBuffer = new();
        private readonly List<EntityHandle> stalePreparedActorsBuffer = new();

        public void ClearTransientState()
        {
            triggerBuffer.Clear();
            stalePreparedActorsBuffer.Clear();
        }

        public void HandleEntityMoved(
            in EntityMovedEvent e,
            ReadyStrikeService readyStrikeService,
            EntityManager entityManager,
            StrikeAction strikeAction,
            Func<EntityHandle, int> findInitiativeIndex,
            Action<EntityHandle, EntityHandle, string> resolveTrigger)
        {
            if (e.forced || !e.entity.IsValid)
                return;
            if (readyStrikeService == null || readyStrikeService.PreparedCount <= 0)
                return;
            if (entityManager == null || entityManager.Registry == null || strikeAction == null)
                return;
            if (findInitiativeIndex == null || resolveTrigger == null)
                return;

            var movedEntity = e.entity;
            var movedData = entityManager.Registry.Get(movedEntity);
            if (movedData == null || !movedData.IsAlive)
                return;

            readyStrikeService.BeginTriggerScope();
            try
            {
                var fromCell = e.from;
                var toCell = e.to;

                ReactionBroker.CollectReadyTriggerCandidates(
                    readyStrikeService.PreparedActors,
                    handle => entityManager.Registry.Get(handle),
                    (actor, actorData) =>
                    {
                        if (!readyStrikeService.TryGetTriggerMode(actor, out var triggerMode))
                            return false;
                        if (!triggerMode.AllowsMovement())
                            return false;
                        if (actorData.Team == movedData.Team)
                            return false;
                        if (!ReadyStrikeTriggerPolicy.DidEnterStrikeRange(actorData, movedData, fromCell, toCell))
                            return false;
                        return strikeAction.GetStrikeTargetFailure(actor, movedEntity) == TargetingFailureReason.None;
                    },
                    triggerBuffer,
                    stalePreparedActorsBuffer);

                for (int i = 0; i < stalePreparedActorsBuffer.Count; i++)
                    readyStrikeService.TryRemovePrepared(stalePreparedActorsBuffer[i]);
                stalePreparedActorsBuffer.Clear();

                if (triggerBuffer.Count <= 0)
                    return;

                triggerBuffer.Sort((left, right) => CompareReadiedTriggerOrder(left, right, findInitiativeIndex));
                for (int i = 0; i < triggerBuffer.Count; i++)
                    resolveTrigger(triggerBuffer[i], movedEntity, "movement");

                triggerBuffer.Clear();
            }
            finally
            {
                readyStrikeService.EndTriggerScope();
            }
        }

        public void HandleStrikePreDamage(
            in StrikePreDamageEvent e,
            ReadyStrikeService readyStrikeService,
            EntityManager entityManager,
            StrikeAction strikeAction,
            Func<EntityHandle, int> findInitiativeIndex,
            Action<EntityHandle, EntityHandle, string> resolveTrigger)
        {
            if (!e.attacker.IsValid || !e.target.IsValid)
                return;
            if (readyStrikeService == null || readyStrikeService.PreparedCount <= 0)
                return;
            if (entityManager == null || entityManager.Registry == null || strikeAction == null)
                return;
            if (findInitiativeIndex == null || resolveTrigger == null)
                return;

            var attacker = e.attacker;
            var attackSourceData = entityManager.Registry.Get(attacker);
            var attackTargetData = entityManager.Registry.Get(e.target);
            if (attackSourceData == null || attackTargetData == null)
                return;
            if (!attackSourceData.IsAlive || !attackTargetData.IsAlive)
                return;

            readyStrikeService.BeginTriggerScope();
            try
            {
                ReactionBroker.CollectReadyTriggerCandidates(
                    readyStrikeService.PreparedActors,
                    handle => entityManager.Registry.Get(handle),
                    (actor, actorData) =>
                    {
                        if (!readyStrikeService.TryGetTriggerMode(actor, out var triggerMode))
                            return false;
                        if (!triggerMode.AllowsAttack())
                            return false;
                        if (actorData.Team == attackSourceData.Team)
                            return false;
                        if (!ReadyStrikeTriggerPolicy.IsWithinReadyStrikeTriggerRange(actorData, attackSourceData))
                            return false;
                        return strikeAction.GetStrikeTargetFailure(actor, attacker) == TargetingFailureReason.None;
                    },
                    triggerBuffer,
                    stalePreparedActorsBuffer);

                for (int i = 0; i < stalePreparedActorsBuffer.Count; i++)
                    readyStrikeService.TryRemovePrepared(stalePreparedActorsBuffer[i]);
                stalePreparedActorsBuffer.Clear();

                if (triggerBuffer.Count <= 0)
                    return;

                triggerBuffer.Sort((left, right) => CompareReadiedTriggerOrder(left, right, findInitiativeIndex));
                for (int i = 0; i < triggerBuffer.Count; i++)
                    resolveTrigger(triggerBuffer[i], attacker, "attack");

                triggerBuffer.Clear();
            }
            finally
            {
                readyStrikeService.EndTriggerScope();
            }
        }

        private static int CompareReadiedTriggerOrder(
            EntityHandle left,
            EntityHandle right,
            Func<EntityHandle, int> findInitiativeIndex)
        {
            int leftIndex = findInitiativeIndex(left);
            int rightIndex = findInitiativeIndex(right);
            if (leftIndex >= 0 && rightIndex >= 0 && leftIndex != rightIndex)
                return leftIndex.CompareTo(rightIndex);

            return left.Id.CompareTo(right.Id);
        }
    }
}
