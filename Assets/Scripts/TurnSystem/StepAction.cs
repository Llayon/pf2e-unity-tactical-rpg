using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public class StepAction : MonoBehaviour
    {
        public const int ActionCost = 1;

        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        private readonly List<NeighborInfo> neighborBuffer = new();

        public void InjectDependencies(EntityManager entityManager, CombatEventBus eventBus)
        {
            if (entityManager != null)
                this.entityManager = entityManager;
            if (eventBus != null)
                this.eventBus = eventBus;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (entityManager == null) Debug.LogWarning("[StepAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[StepAction] Missing CombatEventBus", this);
        }
#endif

        public bool TryPreviewStep(EntityHandle actor, Vector3Int targetCell, out StepPreviewResult preview)
        {
            preview = StepPreviewResult.Invalid(StepFailureReason.InvalidState, targetCell);

            if (!actor.IsValid || entityManager == null || entityManager.Registry == null)
                return false;
            if (entityManager.GridData == null || entityManager.Occupancy == null)
                return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive)
                return false;
            if (actorData.HasCondition(ConditionType.Prone))
            {
                preview = StepPreviewResult.Invalid(StepFailureReason.Prone, targetCell);
                return false;
            }
            if (actorData.EffectiveSpeed <= 0)
            {
                preview = StepPreviewResult.Invalid(StepFailureReason.SpeedZero, targetCell);
                return false;
            }

            Vector3Int fromCell = actorData.GridPosition;
            if (targetCell == fromCell)
            {
                preview = StepPreviewResult.Invalid(StepFailureReason.NotAdjacent, targetCell);
                return false;
            }

            if (!entityManager.GridData.TryGetCell(targetCell, out var targetCellData)
                || !entityManager.GridData.IsCellPassable(targetCell, MovementType.Walk))
            {
                preview = StepPreviewResult.Invalid(StepFailureReason.Impassable, targetCell);
                return false;
            }

            if (!TryGetNeighbor(fromCell, targetCell, out var neighbor))
            {
                preview = StepPreviewResult.Invalid(StepFailureReason.NotAdjacent, targetCell);
                return false;
            }

            var profile = new MovementProfile
            {
                moveType = MovementType.Walk,
                speedFeet = actorData.EffectiveSpeed,
                creatureSizeCells = actorData.SizeCells,
                ignoresDifficultTerrain = false
            };

            int stepCost = MovementCostEvaluator.GetStepCost(
                targetCellData,
                neighbor,
                diagonalParity: false,
                profile);

            if (stepCost > GameConstants.CardinalCostFeet)
            {
                preview = StepPreviewResult.Invalid(
                    targetCellData.terrain == CellTerrain.Difficult || targetCellData.terrain == CellTerrain.GreaterDifficult
                        ? StepFailureReason.DifficultTerrain
                        : StepFailureReason.NotAdjacent,
                    targetCell);
                return false;
            }

            if (!entityManager.Occupancy.CanOccupyFootprint(targetCell, actorData.SizeCells, actor))
            {
                preview = StepPreviewResult.Invalid(StepFailureReason.Occupied, targetCell);
                return false;
            }

            preview = StepPreviewResult.Valid(targetCell, stepCost);
            return true;
        }

        public bool TryExecuteStep(EntityHandle actor, Vector3Int targetCell)
        {
            if (!TryPreviewStep(actor, targetCell, out _))
                return false;
            if (entityManager == null || entityManager.Registry == null || entityManager.Occupancy == null)
                return false;

            var actorData = entityManager.Registry.Get(actor);
            if (actorData == null || !actorData.IsAlive)
                return false;

            Vector3Int fromCell = actorData.GridPosition;
            if (!entityManager.Occupancy.Move(actor, targetCell, actorData.SizeCells))
                return false;

            actorData.GridPosition = targetCell;
            eventBus?.PublishEntityMoved(actor, fromCell, targetCell, MovementTriggerKind.Step);

            var view = entityManager.GetView(actor);
            if (view != null && view.gameObject != null)
                view.transform.position = entityManager.GetEntityWorldPosition(targetCell);

            return true;
        }

        private bool TryGetNeighbor(Vector3Int fromCell, Vector3Int targetCell, out NeighborInfo neighbor)
        {
            neighbor = default;
            entityManager.GridData.GetNeighbors(fromCell, MovementType.Walk, neighborBuffer);
            for (int i = 0; i < neighborBuffer.Count; i++)
            {
                if (neighborBuffer[i].pos != targetCell)
                    continue;

                neighbor = neighborBuffer[i];
                return true;
            }

            return false;
        }
    }
}
