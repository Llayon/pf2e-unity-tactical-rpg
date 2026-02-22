using PF2e.Core;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// MVP reaction policy: accept every eligible reaction immediately.
    /// </summary>
    public sealed class AutoShieldBlockPolicy : IReactionDecisionPolicy
    {
        public void DecideReaction(ReactionOption option, EntityData reactor, int incomingDamage, System.Action<bool> onDecided)
        {
            _ = option;
            _ = reactor;
            _ = incomingDamage;
            onDecided?.Invoke(true);
        }
    }
}
