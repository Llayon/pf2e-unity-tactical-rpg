using UnityEngine;

namespace PF2e.Core
{
    public readonly struct StrideStartedEvent
    {
        public readonly EntityHandle actor;
        public readonly Vector3Int from;
        public readonly Vector3Int to;
        public readonly int actionsCost;

        public StrideStartedEvent(EntityHandle actor, Vector3Int from, Vector3Int to, int actionsCost)
        {
            this.actor = actor;
            this.from = from;
            this.to = to;
            this.actionsCost = actionsCost;
        }
    }

    public readonly struct StrideCompletedEvent
    {
        public readonly EntityHandle actor;
        public readonly Vector3Int to;
        public readonly int actionsCost;

        public StrideCompletedEvent(EntityHandle actor, Vector3Int to, int actionsCost)
        {
            this.actor = actor;
            this.to = to;
            this.actionsCost = actionsCost;
        }
    }
}
