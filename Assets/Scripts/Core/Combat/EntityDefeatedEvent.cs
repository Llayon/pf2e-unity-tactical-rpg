namespace PF2e.Core
{
    public readonly struct EntityDefeatedEvent
    {
        public readonly EntityHandle handle;
        public EntityDefeatedEvent(EntityHandle handle) { this.handle = handle; }
    }
}
