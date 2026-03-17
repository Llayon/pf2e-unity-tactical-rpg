using UnityEngine;

namespace PF2e.Core
{
    public enum MovementTriggerKind : byte
    {
        Normal = 0,
        Step = 1,
        Forced = 2
    }

    /// <summary>
    /// Logical movement commit event (not animation completion).
    /// Published when an entity's grid position is actually updated.
    /// </summary>
    public readonly struct EntityMovedEvent
    {
        public readonly EntityHandle entity;
        public readonly Vector3Int from;
        public readonly Vector3Int to;
        public readonly MovementTriggerKind movementTriggerKind;
        public readonly bool forced;

        public EntityMovedEvent(EntityHandle entity, Vector3Int from, Vector3Int to, bool forced)
            : this(entity, from, to, forced ? MovementTriggerKind.Forced : MovementTriggerKind.Normal)
        {
        }

        public EntityMovedEvent(EntityHandle entity, Vector3Int from, Vector3Int to, MovementTriggerKind movementTriggerKind)
        {
            this.entity = entity;
            this.from = from;
            this.to = to;
            this.movementTriggerKind = movementTriggerKind;
            this.forced = movementTriggerKind == MovementTriggerKind.Forced;
        }
    }
}
