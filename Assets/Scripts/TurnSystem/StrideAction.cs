using System;
using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;
using PF2e.Grid;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Phase 10.X FINAL: Atomic Stride execution (build+execute in one method).
    /// NO TurnManager dependency (PlayerActionExecutor owns turn state).
    /// Callback without closure (method group).
    /// Inspector-only wiring.
    /// </summary>
    public class StrideAction : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private EntityMover entityMover;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private CombatEventBus eventBus;

        private readonly List<Vector3Int> pathBuffer = new List<Vector3Int>();
        private readonly List<Vector3Int> stridePathSnapshot = new List<Vector3Int>();
        private readonly List<int> strideActionBoundaries = new List<int>(8);

        private bool strideInProgress = false;
        private EntityHandle pendingActor;
        private Action<int> pendingOnComplete;
        private int pendingCost;
        private Vector3Int pendingDestination;

        /// <summary>
        /// Fired when a Stride begins movement.
        /// Args: entity, path snapshot (read-only), action boundary indices, actionsCost.
        /// </summary>
        public event Action<EntityHandle, IReadOnlyList<Vector3Int>, IReadOnlyList<int>, int> OnStrideStarted;

        public bool StrideInProgress => strideInProgress;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[StrideAction] Missing EntityManager", this);
            if (entityMover == null) Debug.LogError("[StrideAction] Missing EntityMover", this);
            if (gridManager == null) Debug.LogError("[StrideAction] Missing GridManager", this);
            if (eventBus == null)
                Debug.LogWarning("[StrideAction] CombatEventBus not assigned. Movement works, but CombatLog will miss Stride entries.", this);
        }
#endif

        /// <summary>
        /// Atomic Stride execution: build path + validate + execute movement.
        /// Returns false if preconditions fail (no side effects).
        /// Returns true if movement started (occupancy/data updated, animation started).
        /// </summary>
        public bool TryExecuteStride(EntityHandle actor, Vector3Int targetCell, int availableActions, Action<int> onComplete)
        {
            if (!actor.IsValid) return false;
            if (strideInProgress) return false;
            if (entityMover != null && entityMover.IsMoving) return false;
            if (entityManager == null || entityManager.Registry == null) return false;
            if (gridManager == null || gridManager.Data == null) return false;

            var data = entityManager.Registry.Get(actor);
            if (data == null) return false;

            if (targetCell == data.GridPosition) return false;

            availableActions = Mathf.Clamp(availableActions, 0, 3);
            if (availableActions <= 0) return false;

            // Build movement profile
            var profile = new MovementProfile
            {
                moveType = MovementType.Walk,
                speedFeet = data.Speed,
                creatureSizeCells = data.SizeCells,
                ignoresDifficultTerrain = false
            };

            // Build path (occupancy-aware, multi-stride)
            pathBuffer.Clear();

            bool found = entityManager.Pathfinding.FindPathByActions(
                gridManager.Data,
                data.GridPosition,
                targetCell,
                profile,
                actor,
                entityManager.Occupancy,
                availableActions,
                pathBuffer,
                out int actionsCost,
                out int totalFeet);

            if (!found) return false;
            if (actionsCost <= 0 || actionsCost > availableActions) return false;

            // Validate destination occupancy
            if (!entityManager.Occupancy.CanOccupy(targetCell, actor)) return false;

            // Snapshot path + boundaries for committed path UI
            stridePathSnapshot.Clear();
            stridePathSnapshot.AddRange(pathBuffer);

            ComputeActionBoundariesForPath(gridManager.Data, profile, stridePathSnapshot, strideActionBoundaries);

            // Store completion callback (no closure; caller passes method group)
            pendingActor = actor;
            pendingOnComplete = onComplete;
            pendingCost = actionsCost;
            pendingDestination = targetCell;

            // Capture from position BEFORE updating data.GridPosition
            Vector3Int fromCell = data.GridPosition;

            // Fire UI event (committed path)
            OnStrideStarted?.Invoke(actor, stridePathSnapshot, strideActionBoundaries, actionsCost);

            // Publish typed stride event (no string log)
            eventBus?.PublishStrideStarted(actor, fromCell, targetCell, actionsCost);

            // Update occupancy + data BEFORE animation (atomic commit)
            bool moveSuccess = entityManager.Occupancy.Move(actor, targetCell, data.SizeCells);
            if (!moveSuccess)
            {
                // Rollback pending state
                pendingActor = EntityHandle.None;
                pendingOnComplete = null;
                pendingCost = 0;
                pendingDestination = default;
                return false;
            }

                        data.GridPosition = targetCell;

            // Logical movement commit event (used by systems such as grapple lifecycle).
            eventBus?.PublishEntityMoved(actor, fromCell, targetCell, forced: false);

            // Start animation
            strideInProgress = true;

            // No closure: method group instead of lambda
            entityMover.MoveAlongPath(actor, stridePathSnapshot, onComplete: OnStrideAnimationComplete);

            return true;
        }

        private void OnStrideAnimationComplete()
        {
            strideInProgress = false;

            // Publish typed stride completed event (no string log)
            if (eventBus != null && pendingActor.IsValid)
                eventBus.PublishStrideCompleted(pendingActor, pendingDestination, pendingCost);

            var destination = pendingDestination;
            pendingDestination = default;

            int cost = pendingCost;
            pendingCost = 0;

            var cb = pendingOnComplete;
            pendingOnComplete = null;

            var actor = pendingActor;
            pendingActor = EntityHandle.None;

            // Invoke callback (no closure; executor passed method group)
            cb?.Invoke(cost);
        }

        // ─── Boundary computation ────────────────────────────────────────────

        /// <summary>
        /// Compute action boundary indices for a given path.
        /// outBoundaries[k] = index in path where Stride (k+1) begins.
        /// </summary>
        private void ComputeActionBoundariesForPath(
            GridData grid, MovementProfile profile, List<Vector3Int> path, List<int> outBoundaries)
        {
            outBoundaries.Clear();
            if (path.Count < 2) return;

            outBoundaries.Add(0); // Stride 1 always starts at index 0

            int accumulatedFeet = 0;
            bool diagonalParity = false; // PF2e: first diagonal always costs 5ft

            for (int i = 1; i < path.Count; i++)
            {
                var fromCell = path[i - 1];
                var toCell = path[i];

                if (!TryGetNeighborInfo(grid, fromCell, toCell, profile.moveType, out var neighborInfo))
                {
                    // Fallback: estimate as cardinal step
                    neighborInfo = new NeighborInfo(toCell, NeighborType.Cardinal, 0);
                }

                if (!grid.TryGetCell(toCell, out var cellData))
                    cellData = default;

                int stepCost = MovementCostEvaluator.GetStepCost(cellData, neighborInfo, diagonalParity, profile);

                if (neighborInfo.type == NeighborType.Diagonal)
                    diagonalParity = !diagonalParity;

                accumulatedFeet += stepCost;

                // If accumulated cost exceeds speed, this step starts a new action
                if (accumulatedFeet > profile.speedFeet)
                {
                    outBoundaries.Add(i);
                    accumulatedFeet = stepCost; // this step belongs to the new action
                    // Parity does NOT reset between actions in PF2e (continuous movement)
                }
            }
        }

        /// <summary>
        /// Find the NeighborInfo for a specific from→to step by querying GetNeighbors.
        /// </summary>
        private bool TryGetNeighborInfo(
            GridData grid, Vector3Int from, Vector3Int to, MovementType moveType,
            out NeighborInfo result)
        {
            var neighborStepBuffer = new List<NeighborInfo>();
            grid.GetNeighbors(from, moveType, neighborStepBuffer);

            for (int i = 0; i < neighborStepBuffer.Count; i++)
            {
                if (neighborStepBuffer[i].pos == to)
                {
                    result = neighborStepBuffer[i];
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}
