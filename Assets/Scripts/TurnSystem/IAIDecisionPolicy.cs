using UnityEngine;
using PF2e.Core;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Decision seam for enemy AI. Pure decision methods only.
    /// Turn orchestration and sticky target lock are owned by AITurnController.
    /// </summary>
    public interface IAIDecisionPolicy
    {
        EntityHandle SelectTarget(EntityData actor);
        bool IsInMeleeRange(EntityData actor, EntityData target);
        Vector3Int? SelectStrideCell(EntityData actor, EntityData target, int availableActions);
    }
}
