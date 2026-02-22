namespace PF2e.Core
{
    public readonly struct ReactionOption
    {
        public readonly EntityHandle entity;
        public readonly ReactionType type;
        public readonly ReactionTriggerPhase phase;

        public ReactionOption(EntityHandle entity, ReactionType type, ReactionTriggerPhase phase)
        {
            this.entity = entity;
            this.type = type;
            this.phase = phase;
        }
    }
}
