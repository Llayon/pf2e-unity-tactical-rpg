using System;
using System.Collections.Generic;
using PF2e.Core;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Source-of-truth for prepared Ready Strike state and trigger-scope reaction consumption.
    /// </summary>
    public sealed class ReadyStrikeService
    {
        private readonly Dictionary<EntityHandle, PreparedReadyStrike> preparedByActor = new();
        private readonly TriggerWindowLedger triggerWindowLedger = new();
        private readonly List<TriggerWindowToken> openTriggerScopeTokens = new();

        public int PreparedCount => preparedByActor.Count;
        public IEnumerable<EntityHandle> PreparedActors => preparedByActor.Keys;
        public TriggerWindowLedger TriggerWindowLedger => triggerWindowLedger;

        public bool HasPrepared(EntityHandle actor)
        {
            return actor.IsValid && preparedByActor.ContainsKey(actor);
        }

        public bool TryPrepare(EntityHandle actor, int preparedRound, ReadyTriggerMode triggerMode = ReadyTriggerMode.Any)
        {
            if (!actor.IsValid)
                return false;

            preparedByActor[actor] = new PreparedReadyStrike(preparedRound, triggerMode);
            return true;
        }

        public bool TryGetTriggerMode(EntityHandle actor, out ReadyTriggerMode triggerMode)
        {
            triggerMode = ReadyTriggerMode.Any;
            if (!actor.IsValid)
                return false;
            if (!preparedByActor.TryGetValue(actor, out var prepared))
                return false;

            triggerMode = prepared.triggerMode;
            return true;
        }

        public bool TryRemovePrepared(EntityHandle actor)
        {
            if (!actor.IsValid)
                return false;

            return preparedByActor.Remove(actor);
        }

        public void ClearPrepared()
        {
            preparedByActor.Clear();
        }

        public void ClearAll()
        {
            preparedByActor.Clear();
            openTriggerScopeTokens.Clear();
            triggerWindowLedger.Clear();
        }

        public void BeginTriggerScope()
        {
            OpenTriggerWindow(TriggerWindowType.Unspecified);
        }

        public void EndTriggerScope()
        {
            if (openTriggerScopeTokens.Count <= 0)
                return;

            var token = openTriggerScopeTokens[openTriggerScopeTokens.Count - 1];
            CloseTriggerWindow(token);
        }

        public TriggerWindowToken OpenTriggerWindow(TriggerWindowType windowType)
        {
            var token = triggerWindowLedger.OpenWindow(windowType);
            openTriggerScopeTokens.Add(token);
            return token;
        }

        public void CloseTriggerWindow(TriggerWindowToken token)
        {
            if (!token.IsValid)
                return;

            triggerWindowLedger.CloseWindow(token);
            for (int i = openTriggerScopeTokens.Count - 1; i >= 0; i--)
            {
                if (!openTriggerScopeTokens[i].Equals(token))
                    continue;

                openTriggerScopeTokens.RemoveAt(i);
                break;
            }
        }

        public bool TryConsumeReactionInScope(
            EntityHandle actor,
            EntityData actorData,
            Func<EntityHandle, bool> canUseReaction)
        {
            if (!TryGetCurrentScopeToken(out var token))
                return false;

            return ReactionBroker.TryConsumeReadyReactionInWindow(
                actor,
                actorData,
                canUseReaction,
                triggerWindowLedger,
                token);
        }

        public bool TryConsumeReactionInWindow(
            TriggerWindowToken token,
            EntityHandle actor,
            EntityData actorData,
            Func<EntityHandle, bool> canUseReaction)
        {
            return ReactionBroker.TryConsumeReadyReactionInWindow(
                actor,
                actorData,
                canUseReaction,
                triggerWindowLedger,
                token);
        }

        public bool CanConsumeReactionInScope(
            EntityHandle actor,
            EntityData actorData,
            Func<EntityHandle, bool> canUseReaction)
        {
            if (!TryGetCurrentScopeToken(out var token))
                return false;

            return CanConsumeReactionInWindow(token, actor, actorData, canUseReaction);
        }

        public bool CanConsumeReactionInWindow(
            TriggerWindowToken token,
            EntityHandle actor,
            EntityData actorData,
            Func<EntityHandle, bool> canUseReaction)
        {
            if (!actor.IsValid || actorData == null || !actorData.IsAlive)
                return false;
            if (canUseReaction == null || !canUseReaction(actor))
                return false;
            if (!triggerWindowLedger.CanReact(token, actor))
                return false;

            return true;
        }

        private bool TryGetCurrentScopeToken(out TriggerWindowToken token)
        {
            token = default;
            if (openTriggerScopeTokens.Count <= 0)
                return false;

            token = openTriggerScopeTokens[openTriggerScopeTokens.Count - 1];
            return triggerWindowLedger.IsOpen(token);
        }

        private readonly struct PreparedReadyStrike
        {
            public readonly int preparedRound;
            public readonly ReadyTriggerMode triggerMode;

            public PreparedReadyStrike(int preparedRound, ReadyTriggerMode triggerMode)
            {
                this.preparedRound = preparedRound;
                this.triggerMode = triggerMode;
            }
        }
    }
}
