using System;
using PF2e.Core;

namespace PF2e.TurnSystem
{
    public interface IReactionDecisionPolicy
    {
        void DecideReaction(ReactionOption option, EntityData reactor, int incomingDamage, Action<bool> onDecided);
    }
}
