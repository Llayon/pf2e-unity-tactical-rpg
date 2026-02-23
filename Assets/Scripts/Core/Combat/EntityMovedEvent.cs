using UnityEngine;

namespace PF2e.Core
{
    /// <summary>
    /// Logical movement commit event (not animation completion).
    /// Published when an entity's grid position is actually updated.
    /// </summary>
    public readonly struct EntityMovedEvent
    {
        public readonly EntityHandle entity;
        public readonly Vector3Int from;
        public readonly Vector3Int to;
        public readonly bool forced;

        public EntityMovedEvent(EntityHandle entity, Vector3Int from, Vector3Int to, bool forced)
        {
            this.entity = entity;
            this.from = from;
            this.to = to;
            this.forced = forced;
        }
    }
}
