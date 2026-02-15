using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;
using PF2e.Grid;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Implements the PF2e Stride action: find path → update data → animate → spend action.
    /// Does NOT read input. Called by TurnInputController (Step 5).
    /// Wire all dependencies via Inspector (no FindObjectOfType).
    /// </summary>
    public class StrideAction : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private EntityMover entityMover;
        [SerializeField] private GridManager gridManager;

        private readonly List<Vector3Int> pathBuffer = new List<Vector3Int>();
        private bool strideInProgress = false;

        public bool StrideInProgress => strideInProgress;

        /// <summary>
        /// Returns true if a Stride can be initiated right now (no specific target needed).
        /// </summary>
        public bool CanStride()
        {
            if (turnManager == null || entityManager == null || entityMover == null || gridManager == null)
                return false;
            if (gridManager.Data == null)
                return false;
            if (!turnManager.IsPlayerTurn)
                return false;
            if (entityMover.IsMoving)
                return false;
            if (strideInProgress)
                return false;

            var handle = turnManager.CurrentEntity;
            if (!handle.IsValid)
                return false;
            if (!turnManager.CanAct(handle))
                return false;

            return true;
        }

        /// <summary>
        /// Attempt to Stride to targetCell. Returns true if movement was successfully started.
        /// Called by TurnInputController on cell click.
        /// </summary>
        public bool TryStride(Vector3Int targetCell)
        {
            // a) Pre-conditions
            if (!CanStride()) return false;

            // b) Current entity data
            var handle = turnManager.CurrentEntity;
            var data = entityManager.Registry.Get(handle);
            if (data == null) return false;

            // c) Same cell — nothing to do
            if (targetCell == data.GridPosition) return false;

            // d) Build movement profile
            var profile = new MovementProfile
            {
                moveType = MovementType.Walk,
                speedFeet = data.Speed,
                creatureSizeCells = data.SizeCells,
                ignoresDifficultTerrain = false
            };

            // e) Find path (occupancy-aware)
            pathBuffer.Clear();
            bool found = entityManager.Pathfinding.FindPath(
                gridManager.Data,
                data.GridPosition,
                targetCell,
                profile,
                handle,
                entityManager.Occupancy,
                pathBuffer,
                out int totalCost);
            if (!found) return false;

            // f) Path must fit within one action (one Speed budget)
            if (totalCost > data.Speed) return false;

            // g) Target cell must be occupiable
            if (!entityManager.Occupancy.CanOccupy(targetCell, handle)) return false;

            // h) Execute Stride

            // 1) Lock input — state → ExecutingAction
            turnManager.BeginActionExecution();
            strideInProgress = true;

            // 2) Update occupancy data BEFORE animation (other systems see new position immediately)
            bool moveSuccess = entityManager.Occupancy.Move(handle, targetCell, data.SizeCells);
            if (!moveSuccess)
            {
                Debug.LogWarning($"StrideAction: Occupancy.Move failed for {handle.Id} to {targetCell}", this);

                // Rollback state only (data not changed — Move returned false)
                turnManager.ActionCompleted();
                strideInProgress = false;
                return false;
            }

            data.GridPosition = targetCell;

            // 3) Start animation (EntityMover copies pathBuffer internally)
            entityMover.MoveAlongPath(handle, pathBuffer, onComplete: () =>
            {
                OnStrideComplete(handle);
            });

            return true;
        }

        /// <summary>
        /// Called by EntityMover when animation finishes.
        /// Atomically spends 1 action and restores turn state (auto-EndTurn if drained).
        /// </summary>
        private void OnStrideComplete(EntityHandle handle)
        {
            strideInProgress = false;
            turnManager.CompleteActionWithCost(1);
        }
    }
}
