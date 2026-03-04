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
        private readonly HashSet<EntityHandle> consumedInScope = new();
        private int triggerScopeDepth;

        public int PreparedCount => preparedByActor.Count;
        public IEnumerable<EntityHandle> PreparedActors => preparedByActor.Keys;

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
            consumedInScope.Clear();
            triggerScopeDepth = 0;
        }

        public void BeginTriggerScope()
        {
            if (triggerScopeDepth == 0)
                consumedInScope.Clear();

            triggerScopeDepth++;
        }

        public void EndTriggerScope()
        {
            if (triggerScopeDepth <= 0)
                return;

            triggerScopeDepth--;
            if (triggerScopeDepth == 0)
                consumedInScope.Clear();
        }

        public bool TryConsumeReactionInScope(
            EntityHandle actor,
            EntityData actorData,
            Func<EntityHandle, bool> canUseReaction)
        {
            return ReactionBroker.TryConsumeReadyReactionInScope(
                actor,
                actorData,
                canUseReaction,
                consumedInScope);
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
