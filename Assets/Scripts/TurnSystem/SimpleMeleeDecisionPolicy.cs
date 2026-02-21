using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Default MVP AI policy that mirrors existing simple melee behavior.
    /// </summary>
    public sealed class SimpleMeleeDecisionPolicy : IAIDecisionPolicy
    {
        private readonly EntityManager entityManager;
        private readonly GridManager gridManager;

        // Reused to avoid per-decision allocations.
        private readonly List<Vector3Int> pathBuffer = new(32);
        private readonly Dictionary<Vector3Int, int> zoneBuffer = new();

        public SimpleMeleeDecisionPolicy(EntityManager entityManager, GridManager gridManager)
        {
            this.entityManager = entityManager;
            this.gridManager = gridManager;
        }

        public EntityHandle SelectTarget(EntityData actor)
        {
            if (actor == null || entityManager == null || entityManager.Registry == null)
                return EntityHandle.None;

            return SimpleMeleeAIDecision.FindBestTarget(actor, entityManager.Registry.GetAll());
        }

        public bool IsInMeleeRange(EntityData actor, EntityData target)
        {
            return SimpleMeleeAIDecision.IsInMeleeRange(actor, target);
        }

        public Vector3Int? SelectStrideCell(EntityData actor, EntityData target, int availableActions)
        {
            if (actor == null || target == null)
                return null;
            if (gridManager == null || gridManager.Data == null)
                return null;
            if (entityManager == null || entityManager.Pathfinding == null || entityManager.Occupancy == null)
                return null;

            return SimpleMeleeAIDecision.FindBestMoveCell(
                gridManager.Data,
                entityManager.Pathfinding,
                entityManager.Occupancy,
                actor,
                target,
                availableActions,
                pathBuffer,
                zoneBuffer);
        }
    }
}
