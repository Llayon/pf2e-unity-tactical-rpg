using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;
using PF2e.Presentation.Entity;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Animates an entity along a grid path (visual only).
    /// Does NOT update OccupancyMap or EntityData.GridPosition —
    /// that is the caller's responsibility (StrideAction, Step 4).
    /// </summary>
    public class EntityMover : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private EntityManager entityManager;

        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 4.0f;  // world units per second
        [SerializeField] private float hopHeight = 0.08f; // subtle hop on horizontal segments

        [Header("Debug")]
        [SerializeField] private bool isMoving = false;

        private Coroutine moveRoutine;

        // ─── Public Properties ────────────────────────────────────────────────

        public bool IsMoving => isMoving;

        // ─── Events ───────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when the entity finishes traversing the full path.
        /// </summary>
        public event Action<EntityHandle, Vector3Int> OnMovementComplete;

        /// <summary>
        /// Fired when the entity enters each cell along the path (path[1]…path[N]).
        /// NOT fired for path[0] (start cell). Contract for future AoO checks.
        /// </summary>
        public event Action<EntityHandle, Vector3Int> OnCellReached;

        // ─── Public Methods ───────────────────────────────────────────────────

        /// <summary>
        /// Begin animated movement along the given grid path.
        /// Path must have at least 2 cells (start + at least one step).
        /// onComplete fires on successful finish or on early-exit (except busy case d).
        /// </summary>
        public void MoveAlongPath(EntityHandle handle, List<Vector3Int> gridPath, Action onComplete = null)
        {
            // a) Invalid handle
            if (!handle.IsValid)
            {
                Debug.LogWarning("EntityMover: invalid handle");
                onComplete?.Invoke();
                return;
            }

            // b) Missing dependencies
            if (entityManager == null || entityManager.Registry == null)
            {
                Debug.LogError("EntityMover: EntityManager or Registry not assigned!");
                onComplete?.Invoke();
                return;
            }

            // c) Path too short (not an error — nothing to move)
            if (gridPath == null || gridPath.Count < 2)
            {
                onComplete?.Invoke();
                return;
            }

            // d) Already moving — ignore; the active movement's callback will fire when done
            if (moveRoutine != null)
            {
                Debug.LogWarning("EntityMover: movement already in progress, ignoring");
                return; // intentionally no onComplete here
            }

            // e) Invalid speed
            if (moveSpeed <= 0.0001f)
            {
                Debug.LogError("EntityMover: moveSpeed <= 0!");
                onComplete?.Invoke();
                return;
            }

            moveRoutine = StartCoroutine(MoveCoroutine(handle, gridPath, onComplete));
        }

        /// <summary>
        /// Hard-cancel the current movement. Does NOT fire onComplete or OnMovementComplete.
        /// </summary>
        public void StopMovement()
        {
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
                moveRoutine = null;
            }
            // Unity does not guarantee finally blocks run after StopCoroutine,
            // so we reset state here explicitly.
            isMoving = false;
        }

        // ─── Private Coroutine ────────────────────────────────────────────────

        private IEnumerator MoveCoroutine(EntityHandle handle, List<Vector3Int> gridPath, Action onComplete)
        {
            // 1. Copy the path so caller can safely reuse their buffer
            var path = new List<Vector3Int>(gridPath);

            // 2. Guard against scene changes / late-start
            if (entityManager == null || entityManager.Registry == null)
            {
                moveRoutine = null;
                onComplete?.Invoke();
                yield break;
            }

            // 3. Resolve view
            var view = entityManager.GetView(handle);
            if (view == null)
            {
                Debug.LogWarning($"EntityMover: no view for {handle.Id}");
                moveRoutine = null;
                onComplete?.Invoke();
                yield break;
            }

            // 4. Dev check: view handle must match
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (view.Handle != handle)
                Debug.LogWarning($"EntityMover: view.Handle {view.Handle.Id} != requested {handle.Id}");
#endif

            // 5. Resolve data
            var data = entityManager.Registry.Get(handle);
            if (data == null)
            {
                Debug.LogWarning($"EntityMover: no data for {handle.Id}");
                moveRoutine = null;
                onComplete?.Invoke();
                yield break;
            }

            // 6. Dev check: path start should match current entity grid position
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (path[0] != data.GridPosition)
                Debug.LogWarning($"EntityMover: path start {path[0]} != entity position {data.GridPosition} for {handle.Id}");
#endif

            // 7. All checks passed — begin movement
            isMoving = true;

            try
            {
                // 8. Traverse each path segment
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector3Int fromCell = path[i];
                    Vector3Int toCell   = path[i + 1];

                    Vector3 startPos = entityManager.GetEntityWorldPosition(fromCell);
                    Vector3 endPos   = entityManager.GetEntityWorldPosition(toCell);

                    float distance = Vector3.Distance(startPos, endPos);
                    float duration = distance / moveSpeed;

                    // Snap immediately if segment is effectively zero length
                    if (duration <= 0.0001f)
                    {
                        view.transform.position = endPos;
                        OnCellReached?.Invoke(handle, toCell);
                        continue;
                    }

                    // Hop only on horizontal segments (vertical links already change Y)
                    bool isVerticalSegment = (fromCell.y != toCell.y);

                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float t       = Mathf.Clamp01(elapsed / duration);
                        float smoothT = t * t * (3f - 2f * t); // smoothstep

                        Vector3 pos = Vector3.Lerp(startPos, endPos, smoothT);
                        if (!isVerticalSegment)
                            pos.y += hopHeight * Mathf.Sin(t * Mathf.PI);

                        view.transform.position = pos;
                        yield return null;
                    }

                    // Snap to exact end position to prevent float drift
                    view.transform.position = endPos;
                    OnCellReached?.Invoke(handle, toCell);
                }

                // 9. Full path traversed
                Vector3Int finalCell = path[path.Count - 1];
                OnMovementComplete?.Invoke(handle, finalCell);
                onComplete?.Invoke();
            }
            finally
            {
                // Runs on normal completion. On StopCoroutine, Unity may skip this block —
                // StopMovement() therefore resets state independently.
                isMoving    = false;
                moveRoutine = null;
            }
        }
    }
}
