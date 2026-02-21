using System.Collections.Generic;
using PF2e.Core;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Sticky target selector for one AI turn.
    /// Keeps current target while it remains valid; re-acquires only when invalid.
    /// </summary>
    public sealed class AITurnTargetLock
    {
        private EntityHandle lockedTarget = EntityHandle.None;

        public EntityHandle CurrentTarget => lockedTarget;

        public void Reset()
        {
            lockedTarget = EntityHandle.None;
        }

        public void Invalidate()
        {
            lockedTarget = EntityHandle.None;
        }

        public EntityHandle ResolveTarget(EntityData actor, IEnumerable<EntityData> allEntities)
        {
            if (actor == null || allEntities == null)
            {
                lockedTarget = EntityHandle.None;
                return EntityHandle.None;
            }

            if (lockedTarget.IsValid)
            {
                var lockedData = FindByHandle(allEntities, lockedTarget);
                if (IsValidTarget(actor, lockedData))
                    return lockedTarget;
            }

            lockedTarget = SimpleMeleeAIDecision.FindBestTarget(actor, allEntities);
            return lockedTarget;
        }

        public static bool IsValidTarget(EntityData actor, EntityData target)
        {
            if (actor == null || target == null) return false;
            if (!target.IsAlive) return false;
            if (target.Team != Team.Player) return false;
            if (target.GridPosition.y != actor.GridPosition.y) return false;
            return true;
        }

        private static EntityData FindByHandle(IEnumerable<EntityData> allEntities, EntityHandle handle)
        {
            if (!handle.IsValid) return null;

            foreach (var data in allEntities)
            {
                if (data == null) continue;
                if (data.Handle == handle) return data;
            }

            return null;
        }
    }
}
