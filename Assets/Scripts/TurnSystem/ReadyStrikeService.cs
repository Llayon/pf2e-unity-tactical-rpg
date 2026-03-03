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
        private readonly Dictionary<EntityHandle, int> preparedByActor = new();
        private readonly HashSet<EntityHandle> consumedInScope = new();
        private int triggerScopeDepth;

        public int PreparedCount => preparedByActor.Count;
        public IEnumerable<EntityHandle> PreparedActors => preparedByActor.Keys;

        public bool HasPrepared(EntityHandle actor)
        {
            return actor.IsValid && preparedByActor.ContainsKey(actor);
        }

        public bool TryPrepare(EntityHandle actor, int preparedRound)
        {
            if (!actor.IsValid)
                return false;

            preparedByActor[actor] = preparedRound;
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
    }
}
