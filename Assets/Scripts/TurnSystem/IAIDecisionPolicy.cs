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
        bool TrySelectSpellDecision(EntityData actor, EntityData target, int availableActions, out AISpellDecision decision);
        bool TrySelectSkillDecision(EntityData actor, EntityData target, int availableActions, out AISkillDecision decision);
        bool TrySelectDefensiveDecision(EntityData actor, EntityData target, int availableActions, out AIDefensiveDecision decision);
        Vector3Int? SelectStepCell(EntityData actor, EntityData target, int availableActions);
        Vector3Int? SelectStrideCell(EntityData actor, EntityData target, int availableActions);
    }
}
