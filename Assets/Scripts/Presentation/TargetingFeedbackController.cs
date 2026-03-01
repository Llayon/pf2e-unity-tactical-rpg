using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.Presentation.Entity;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// World-space target feedback for targeting modes.
    /// Highlights eligible targets and applies valid/invalid hover tint using TargetingController preview validation.
    /// </summary>
    public class TargetingFeedbackController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private TargetingController targetingController;
        [SerializeField] private PlayerActionExecutor actionExecutor;
        [SerializeField] private CellHighlightPool cellHighlightPool;

        [Header("Reposition Cell Highlight")]
        [SerializeField] private Color repositionDestinationColor = new Color(0.25f, 0.8f, 1f, 0.35f);

        private readonly HashSet<EntityHandle> eligibleHandles = new HashSet<EntityHandle>();
        private readonly Dictionary<EntityHandle, TargetingTintController> tintCache = new Dictionary<EntityHandle, TargetingTintController>();
        private readonly List<GameObject> repositionCellHighlights = new List<GameObject>();
        private readonly List<Vector3Int> repositionDestinationsBuffer = new List<Vector3Int>();

        private EntityHandle? hoveredEntity;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null) Debug.LogWarning("[TargetingFeedback] Missing CombatEventBus", this);
            if (entityManager == null) Debug.LogWarning("[TargetingFeedback] Missing EntityManager", this);
            if (gridManager == null) Debug.LogWarning("[TargetingFeedback] Missing GridManager", this);
            if (targetingController == null) Debug.LogWarning("[TargetingFeedback] Missing TargetingController", this);
            if (actionExecutor == null) Debug.LogWarning("[TargetingFeedback] Missing PlayerActionExecutor", this);
            if (cellHighlightPool == null) Debug.LogWarning("[TargetingFeedback] Missing CellHighlightPool", this);
        }
#endif

        private void OnEnable()
        {
            if (eventBus == null || entityManager == null || gridManager == null || targetingController == null)
            {
                Debug.LogError("[TargetingFeedback] Missing dependencies", this);
                enabled = false;
                return;
            }

            targetingController.OnModeChanged += HandleModeChanged;
            gridManager.OnEntityHovered += HandleEntityHovered;
            gridManager.OnEntityUnhovered += HandleEntityUnhovered;

            eventBus.OnCombatStartedTyped += HandleCombatStarted;
            eventBus.OnCombatEndedTyped += HandleCombatEnded;
            eventBus.OnTurnStartedTyped += HandleTurnStarted;
            eventBus.OnTurnEndedTyped += HandleTurnEnded;
            eventBus.OnActionsChangedTyped += HandleActionsChanged;
            eventBus.OnConditionChangedTyped += HandleConditionChanged;
            eventBus.OnEntityMovedTyped += HandleEntityMoved;

            RecomputeVisuals();
        }

        private void OnDisable()
        {
            if (targetingController != null)
                targetingController.OnModeChanged -= HandleModeChanged;

            if (gridManager != null)
            {
                gridManager.OnEntityHovered -= HandleEntityHovered;
                gridManager.OnEntityUnhovered -= HandleEntityUnhovered;
            }

            if (eventBus != null)
            {
                eventBus.OnCombatStartedTyped -= HandleCombatStarted;
                eventBus.OnCombatEndedTyped -= HandleCombatEnded;
                eventBus.OnTurnStartedTyped -= HandleTurnStarted;
                eventBus.OnTurnEndedTyped -= HandleTurnEnded;
                eventBus.OnActionsChangedTyped -= HandleActionsChanged;
                eventBus.OnConditionChangedTyped -= HandleConditionChanged;
                eventBus.OnEntityMovedTyped -= HandleEntityMoved;
            }

            ClearAllVisualsAndState();
        }

        private void HandleModeChanged(TargetingMode mode)
        {
            if (mode == TargetingMode.None)
            {
                ClearAllVisualsAndState(clearHover: false);
                return;
            }

            RecomputeVisuals();
        }

        private void HandleEntityHovered(EntityHandle handle)
        {
            hoveredEntity = handle;
            if (IsTargetingActive())
                RecomputeVisuals();
        }

        private void HandleEntityUnhovered()
        {
            hoveredEntity = null;
            if (IsTargetingActive())
                RecomputeVisuals();
        }

        private void HandleCombatStarted(in CombatStartedEvent e)
        {
            if (IsTargetingActive())
                RecomputeVisuals();
        }

        private void HandleCombatEnded(in CombatEndedEvent e)
        {
            ClearAllVisualsAndState(clearHover: false);
        }

        private void HandleTurnStarted(in TurnStartedEvent e)
        {
            if (IsTargetingActive())
                RecomputeVisuals();
        }

        private void HandleTurnEnded(in TurnEndedEvent e)
        {
            ClearAllVisualsAndState(clearHover: false);
        }

        private void HandleActionsChanged(in ActionsChangedEvent e)
        {
            if (IsTargetingActive())
                RecomputeVisuals();
        }

        private void HandleConditionChanged(in ConditionChangedEvent e)
        {
            if (IsTargetingActive())
                RecomputeVisuals();
        }

        private void HandleEntityMoved(in EntityMovedEvent e)
        {
            if (IsTargetingActive())
                RecomputeVisuals();
        }

        private bool IsTargetingActive()
        {
            return targetingController != null && targetingController.ActiveMode != TargetingMode.None;
        }

        private void RecomputeVisuals()
        {
            ClearTrackedVisualStates();
            eligibleHandles.Clear();
            ClearRepositionCellHighlights();

            if (!IsTargetingActive())
                return;

            if (targetingController.IsRepositionSelectingCell)
            {
                ShowRepositionDestinationHighlights();
                return;
            }

            if (entityManager == null || entityManager.Registry == null)
                return;

            var eligibleTint = targetingController.ActiveMode == TargetingMode.Aid
                ? TargetingTintState.HoverValid
                : TargetingTintState.Eligible;

            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null || !data.IsAlive || !data.Handle.IsValid)
                    continue;

                if (targetingController.PreviewEntity(data.Handle) != TargetingResult.Success)
                    continue;

                eligibleHandles.Add(data.Handle);
                SetTintState(data.Handle, eligibleTint);
            }

            ApplyHoverState();
        }

        private void ApplyHoverState()
        {
            if (!hoveredEntity.HasValue || !hoveredEntity.Value.IsValid || !IsTargetingActive())
                return;

            var handle = hoveredEntity.Value;
            var result = targetingController.PreviewEntity(handle);

            if (result == TargetingResult.Success)
                SetTintState(handle, TargetingTintState.HoverValid);
            else if (result != TargetingResult.ModeNotSupported)
                SetTintState(handle, TargetingTintState.HoverInvalid);
        }

        private void SetTintState(EntityHandle handle, TargetingTintState state)
        {
            if (!TryGetTint(handle, out var tint) || tint == null)
                return;

            tint.SetState(state);
        }

        private bool TryGetTint(EntityHandle handle, out TargetingTintController tint)
        {
            if (tintCache.TryGetValue(handle, out tint) && tint != null)
                return true;

            tint = null;
            if (entityManager == null) return false;

            var view = entityManager.GetView(handle);
            if (view == null || view.gameObject == null)
                return false;

            if (!view.gameObject.activeInHierarchy)
                return false;

            tint = view.GetComponent<TargetingTintController>();
            if (tint == null)
                tint = view.gameObject.AddComponent<TargetingTintController>();

            if (tint == null)
                return false;

            tintCache[handle] = tint;
            return true;
        }

        private void ClearTrackedVisualStates()
        {
            foreach (var kvp in tintCache)
            {
                if (kvp.Value != null)
                    kvp.Value.Clear();
            }
        }

        private void ClearAllVisualsAndState(bool clearHover = true)
        {
            ClearTrackedVisualStates();
            ClearRepositionCellHighlights();
            eligibleHandles.Clear();
            if (clearHover)
                hoveredEntity = null;
        }

        private void ShowRepositionDestinationHighlights()
        {
            if (actionExecutor == null || cellHighlightPool == null || gridManager == null || gridManager.Data == null || gridManager.Config == null)
                return;

            if (!actionExecutor.TryGetPendingRepositionDestinations(repositionDestinationsBuffer))
                return;

            float cellSize = gridManager.Config.cellWorldSize;
            for (int i = 0; i < repositionDestinationsBuffer.Count; i++)
            {
                var worldPos = gridManager.Data.CellToWorld(repositionDestinationsBuffer[i]);
                repositionCellHighlights.Add(cellHighlightPool.ShowHighlight(worldPos, cellSize, repositionDestinationColor));
            }
        }

        private void ClearRepositionCellHighlights()
        {
            if (cellHighlightPool != null)
            {
                for (int i = 0; i < repositionCellHighlights.Count; i++)
                {
                    var go = repositionCellHighlights[i];
                    if (go != null)
                        cellHighlightPool.Return(go);
                }
            }

            repositionCellHighlights.Clear();
            repositionDestinationsBuffer.Clear();
        }
    }
}
