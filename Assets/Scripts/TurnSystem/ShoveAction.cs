using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// PF2e Shove (MVP):
    /// - Athletics (untrained) vs Fortitude DC
    /// - Attack trait (MAP applies and increments)
    /// - Forced movement uses a grid-cell approximation:
    ///   Success = push up to 1 cell, CritSuccess = up to 2 cells
    ///   in the normalized (sign dx/dz) away-from-actor direction.
    /// Known gaps: free-hand model (weapon Shove trait only), follow-up Stride rider.
    /// </summary>
    public class ShoveAction : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        [Header("Rules")]
        [SerializeField] private bool requireSameElevation = true;

        private readonly ConditionService conditionService = new();
        private readonly List<ConditionDelta> conditionDeltaBuffer = new();

        public const int ActionCost = 1;
        private const string ActionName = "Shove";

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[ShoveAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[ShoveAction] Missing CombatEventBus", this);
        }
#endif

        public bool CanShove(EntityHandle actor, EntityHandle target)
        {
            if (!actor.IsValid || !target.IsValid) return false;
            if (entityManager == null || entityManager.Registry == null) return false;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null) return false;
            if (!actorData.IsAlive || !targetData.IsAlive) return false;
            if (actorData.Team == targetData.Team) return false;
            if (actorData.EquippedWeapon.IsRanged) return false;
            if (requireSameElevation && actorData.GridPosition.y != targetData.GridPosition.y) return false;

            int sizeDelta = (int)targetData.Size - (int)actorData.Size;
            if (sizeDelta > 1) return false;

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(actorData.GridPosition, targetData.GridPosition);
            if (distanceFeet > actorData.EquippedWeapon.ReachFeet) return false;

            // MVP rule gap: free-hand model is not implemented yet, so only Shove-trait weapons are eligible.
            bool hasShoveTrait = (actorData.EquippedWeapon.Traits & WeaponTraitFlags.Shove) != 0;
            if (!hasShoveTrait) return false;

            return true;
        }

        public DegreeOfSuccess? TryShove(EntityHandle actor, EntityHandle target, IRng rng = null)
        {
            if (!CanShove(actor, target)) return null;
            if (entityManager == null || entityManager.Registry == null) return null;

            var actorData = entityManager.Registry.Get(actor);
            var targetData = entityManager.Registry.Get(target);
            if (actorData == null || targetData == null) return null;

            rng ??= UnityRng.Shared;

            int skillModifier = actorData.GetSkillModifier(SkillType.Athletics);
            int mapPenalty = actorData.GetMAPPenalty(actorData.EquippedWeapon);
            int effectiveModifier = skillModifier + mapPenalty;

            // Shove has the Attack trait and increases MAP after the attempt is declared.
            actorData.MAPCount++;

            int dc = targetData.GetSaveDC(SaveType.Fortitude);
            var result = CheckResolver.RollCheck(effectiveModifier, dc, rng);

            conditionDeltaBuffer.Clear();

            switch (result.degree)
            {
                case DegreeOfSuccess.CriticalSuccess:
                    TryApplyForcedPush(actorData, targetHandle: target, targetData, maxCells: 2);
                    break;

                case DegreeOfSuccess.Success:
                    TryApplyForcedPush(actorData, targetHandle: target, targetData, maxCells: 1);
                    break;

                case DegreeOfSuccess.CriticalFailure:
                    conditionService.AddOrRefresh(actorData, ConditionType.Prone, value: 0, rounds: -1, conditionDeltaBuffer);
                    break;
            }

            PublishConditionDeltas();

            if (eventBus != null)
            {
                var ev = new SkillCheckResolvedEvent(
                    actor,
                    target,
                    SkillType.Athletics,
                    result.naturalRoll,
                    result.modifier,
                    result.total,
                    result.dc,
                    result.degree,
                    ActionName);
                eventBus.PublishSkillCheckResolved(in ev);
            }

            return result.degree;
        }

        private int TryApplyForcedPush(EntityData actorData, EntityHandle targetHandle, EntityData targetData, int maxCells)
        {
            if (actorData == null || targetData == null) return 0;
            if (maxCells <= 0) return 0;

            Vector3Int step = GetNormalizedPushDirection(actorData.GridPosition, targetData.GridPosition);
            if (step.x == 0 && step.z == 0) return 0;

            // Phase 22.2 MVP: grid-cell displacement, not feet-accurate diagonal parity.
            // Move step-by-step and stop early if blocked.
            int moved = 0;
            for (int i = 0; i < maxCells; i++)
            {
                Vector3Int next = targetData.GridPosition + step;
                if (!entityManager.TryMoveEntityImmediate(targetHandle, next))
                    break;
                moved++;
            }

            return moved;
        }

        private static Vector3Int GetNormalizedPushDirection(Vector3Int actorCell, Vector3Int targetCell)
        {
            int dx = targetCell.x - actorCell.x;
            int dz = targetCell.z - actorCell.z;

            int dirX = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int dirZ = dz == 0 ? 0 : (dz > 0 ? 1 : -1);
            return new Vector3Int(dirX, 0, dirZ);
        }

        private void PublishConditionDeltas()
        {
            if (eventBus == null || conditionDeltaBuffer.Count == 0) return;

            for (int i = 0; i < conditionDeltaBuffer.Count; i++)
            {
                var delta = conditionDeltaBuffer[i];
                eventBus.PublishConditionChanged(
                    delta.entity,
                    delta.type,
                    delta.changeType,
                    delta.oldValue,
                    delta.newValue,
                    delta.oldRemainingRounds,
                    delta.newRemainingRounds);
            }
        }
    }
}
