using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    public class InitiativeBarController : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        [Header("UI")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI roundLabel;
        [SerializeField] private GameObject delayPlacementPromptRoot;
        [SerializeField] private TextMeshProUGUI delayPlacementPromptLabel;
        [SerializeField] private Image delayPlacementPromptBackground;
        [SerializeField] private Transform slotsContainer;
        [SerializeField] private RectTransform markersOverlayContainer;
        [SerializeField] private InitiativeSlot slotPrefab;
        [SerializeField] private InitiativeInsertionMarker insertionMarkerPrefab;

        [Header("Panel Layout")]
        [SerializeField] private bool autoSizePanelToSlotsContent = true;
        [SerializeField] private float minPanelWidth = 180f;
        [SerializeField] private float maxPanelWidth = 900f;
        [SerializeField] private float panelContentPaddingX = 12f;

        [Header("Delay Prompt Layout")]
        [SerializeField] private float delayPromptMinWidth = 220f;
        [SerializeField] private float delayPromptMaxWidth = 460f;
        [SerializeField] private float delayPromptTextPaddingX = 28f;
        [SerializeField] private float delayPromptOffsetY = 4f;

        private readonly List<InitiativeSlot> activeSlots = new List<InitiativeSlot>(32);
        private readonly Stack<InitiativeSlot> slotPool = new Stack<InitiativeSlot>(32);
        private readonly Dictionary<EntityHandle, InitiativeSlot> slotByHandle
            = new Dictionary<EntityHandle, InitiativeSlot>();
        private readonly HashSet<EntityHandle> appendedDelayedHandles = new HashSet<EntityHandle>();
        private DelayPlacementMarkerOverlayPresenter delayMarkerOverlayPresenter;
        private DelayPlacementPromptPresenter delayPromptPresenter;
        private DelayPlacementInteractionCoordinator delayPlacementInteractionCoordinator;
        private DelayInitiativeRowPlanner delayInitiativeRowPlanner;

        private void OnEnable()
        {
            if (eventBus != null)
            {
                eventBus.OnCombatStartedTyped += HandleCombatStarted;
                eventBus.OnCombatEndedTyped += HandleCombatEnded;
                eventBus.OnRoundStartedTyped += HandleRoundStarted;
                eventBus.OnTurnStartedTyped += HandleTurnStarted;
                eventBus.OnDelayPlacementSelectionChangedTyped += HandleDelayPlacementSelectionChanged;
                eventBus.OnDelayReturnWindowOpenedTyped += HandleDelayReturnWindowOpened;
                eventBus.OnDelayReturnWindowClosedTyped += HandleDelayReturnWindowClosed;
                eventBus.OnStrikeResolved  += HandleStrikeResolved;
                eventBus.OnEntityDefeated  += HandleEntityDefeated;
            }

            SetPanelVisible(false);
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                eventBus.OnCombatStartedTyped -= HandleCombatStarted;
                eventBus.OnCombatEndedTyped -= HandleCombatEnded;
                eventBus.OnRoundStartedTyped -= HandleRoundStarted;
                eventBus.OnTurnStartedTyped -= HandleTurnStarted;
                eventBus.OnDelayPlacementSelectionChangedTyped -= HandleDelayPlacementSelectionChanged;
                eventBus.OnDelayReturnWindowOpenedTyped -= HandleDelayReturnWindowOpened;
                eventBus.OnDelayReturnWindowClosedTyped -= HandleDelayReturnWindowClosed;
                eventBus.OnStrikeResolved  -= HandleStrikeResolved;
                eventBus.OnEntityDefeated  -= HandleEntityDefeated;
            }
        }

        // ─── Event Handlers ───────────────────────────────────────────────────

        private void HandleCombatStarted(in CombatStartedEvent e)
        {
            if (turnManager == null) return;

            EnsureRuntimeUiReferences();
            SetPanelVisible(true);
            if (roundLabel != null)
                roundLabel.SetText("Round {0}", turnManager.RoundNumber);
            HideDelayPlacementPrompt();
            BuildSlots(turnManager.InitiativeOrder);
            UpdateHighlight();
        }

        private void HandleCombatEnded(in CombatEndedEvent e)
        {
            SetPanelVisible(false);
            HideDelayPlacementPrompt();
            ClearSlotsToPool();
        }

        private void HandleRoundStarted(in RoundStartedEvent e)
        {
            if (roundLabel != null)
                roundLabel.SetText("Round {0}", e.round);
            AutoSizePanelToContent();
            MarkMarkersOverlayDirty();
            if (turnManager == null || !turnManager.IsDelayPlacementSelectionOpen)
                HideDelayPlacementPrompt();
        }

        private void HandleTurnStarted(in TurnStartedEvent e)
        {
            if (turnManager != null)
                BuildSlots(turnManager.InitiativeOrder);
            UpdateHighlight();
        }

        private void HandleDelayPlacementSelectionChanged(in DelayPlacementSelectionChangedEvent e)
        {
            if (turnManager == null)
                return;

            BuildSlots(turnManager.InitiativeOrder);
            UpdateHighlight();
            RefreshDelayPlacementHintLabel();
        }

        private void HandleDelayReturnWindowOpened(in DelayReturnWindowOpenedEvent e)
        {
            UpdateHighlight(); // clears active slot highlight while inter-turn Delay window is open
        }

        private void HandleDelayReturnWindowClosed(in DelayReturnWindowClosedEvent e)
        {
            UpdateHighlight();
        }

        private void HandleStrikeResolved(in StrikeResolvedEvent e)
        {
            if (entityManager == null || entityManager.Registry == null) return;
            if (!slotByHandle.TryGetValue(e.target, out var slot)) return;

            var data = entityManager.Registry.Get(e.target);
            if (data == null) return;

            slot.RefreshHP(data.CurrentHP, data.MaxHP, data.IsAlive);
        }

        private void HandleEntityDefeated(in EntityDefeatedEvent e)
        {
            if (!slotByHandle.TryGetValue(e.handle, out var slot)) return;
            slot.SetDefeated(true);
        }

        // ─── Slot Management ──────────────────────────────────────────────────

        private void BuildSlots(IReadOnlyList<InitiativeEntry> order)
        {
            EnsureRuntimeUiReferences();
            ClearSlotsToPool();
            slotByHandle.Clear();
            appendedDelayedHandles.Clear();

            if (order == null || entityManager == null || entityManager.Registry == null) return;

            for (int i = 0; i < order.Count; i++)
            {
                var handle = order[i].Handle;
                var data = entityManager.Registry.Get(handle);
                if (data == null) continue;

                CreateOrRefreshSlot(data, isDelayed: false);
                AppendInsertionMarkerIfNeeded(handle);
                AppendDelayedSlotsAnchoredTo(handle);
            }

            AppendRemainingDelayedSlots();

            var slotsRect = slotsContainer as RectTransform;
            if (slotsRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(slotsRect);

            AutoSizePanelToContent();
            RepositionInsertionMarkers();
            RefreshDelayPlacementHintLabel();
            if (turnManager != null && turnManager.IsDelayPlacementSelectionOpen)
                delayMarkerOverlayPresenter?.MarkDirtyIfAny();
            else
                delayMarkerOverlayPresenter?.ClearDirty();
        }

        private InitiativeSlot GetSlot()
        {
            if (slotPool.Count > 0)
                return slotPool.Pop();

            // Parent to slotsContainer immediately — never orphan a UI element outside Canvas
            var inst = Instantiate(slotPrefab, slotsContainer);
            inst.gameObject.name = "InitiativeSlot";
            inst.gameObject.SetActive(false);
            return inst;
        }

        private void ClearSlotsToPool()
        {
            ClearInsertionMarkersToPool();

            for (int i = 0; i < activeSlots.Count; i++)
            {
                var s = activeSlots[i];
                if (s == null) continue;

                s.OnClicked -= HandleSlotClicked;
                s.SetHighlight(false);
                s.gameObject.SetActive(false);
                // Keep under slotsContainer — stays inside Canvas hierarchy
                slotPool.Push(s);
            }
            activeSlots.Clear();
            slotByHandle.Clear();
        }

        private void ClearInsertionMarkersToPool()
        {
            delayMarkerOverlayPresenter?.ClearToPool();
            delayPlacementInteractionCoordinator?.ClearHoverState();
        }

        private void UpdateHighlight()
        {
            if (turnManager == null) return;

            int idx = turnManager.CurrentIndex;

            for (int i = 0; i < activeSlots.Count; i++)
                activeSlots[i].SetHighlight(false);

            if (turnManager.State == TurnState.DelayReturnWindow)
                return;

            if (idx >= 0 && idx < turnManager.InitiativeOrder.Count)
            {
                var handle = turnManager.InitiativeOrder[idx].Handle;
                if (slotByHandle.TryGetValue(handle, out var slot))
                    slot.SetHighlight(true);
            }
        }

        private void SetPanelVisible(bool visible)
        {
            if (panelRoot != null)
                panelRoot.SetActive(visible);
        }

        private void EnsureRuntimeUiReferences()
        {
            EnsureMarkersOverlayContainer();
            EnsureDelayPlacementPromptPresenter();
            EnsureDelayPlacementInteractionCoordinator();
            EnsureDelayPlacementMarkerOverlayPresenter();
            EnsureDelayInitiativeRowPlanner();
        }

        private Transform GetMarkersOverlayParent()
        {
            EnsureMarkersOverlayContainer();
            if (markersOverlayContainer != null)
                return markersOverlayContainer;

            return slotsContainer;
        }

        private void EnsureMarkersOverlayContainer()
        {
            var slotsRect = slotsContainer as RectTransform;
            if (slotsRect == null || slotsRect.parent == null)
                return;

            if (markersOverlayContainer != null)
            {
                CopyRectTransformLayout(slotsRect, markersOverlayContainer);
                return;
            }

            var go = new GameObject("DelayMarkersOverlay", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(slotsRect.parent, false);
            CopyRectTransformLayout(slotsRect, rect);
            rect.SetSiblingIndex(slotsRect.GetSiblingIndex() + 1);
            markersOverlayContainer = rect;
        }

        private void EnsureDelayPlacementPromptPresenter()
        {
            if (delayPromptPresenter == null)
            {
                delayPromptPresenter = new DelayPlacementPromptPresenter(
                    delayPromptMinWidth,
                    delayPromptMaxWidth,
                    delayPromptTextPaddingX,
                    delayPromptOffsetY);
            }
            delayPromptPresenter.Bind(
                panelRoot,
                roundLabel,
                delayPlacementPromptRoot,
                delayPlacementPromptLabel,
                delayPlacementPromptBackground);
            delayPromptPresenter.EnsureView();
            SyncDelayPromptRefsFromPresenter();
        }

        private void EnsureDelayPlacementInteractionCoordinator()
        {
            if (delayPlacementInteractionCoordinator == null)
            {
                delayPlacementInteractionCoordinator = new DelayPlacementInteractionCoordinator();
                delayPlacementInteractionCoordinator.OnDelayPlacementCommitted += HandleDelayPlacementCommitted;
            }

            delayPlacementInteractionCoordinator.Bind(turnManager, entityManager, delayPromptPresenter);
        }

        private void EnsureDelayPlacementMarkerOverlayPresenter()
        {
            if (delayMarkerOverlayPresenter != null)
                return;

            if (delayPlacementInteractionCoordinator == null)
            {
                Debug.LogWarning("[InitiativeBarController] Delay interaction coordinator missing before marker presenter init.", this);
                return;
            }

            delayMarkerOverlayPresenter = new DelayPlacementMarkerOverlayPresenter();
            delayMarkerOverlayPresenter.OnMarkerClicked += delayPlacementInteractionCoordinator.HandleMarkerClicked;
            delayMarkerOverlayPresenter.OnMarkerHoverEntered += delayPlacementInteractionCoordinator.HandleMarkerHoverEntered;
            delayMarkerOverlayPresenter.OnMarkerHoverExited += delayPlacementInteractionCoordinator.HandleMarkerHoverExited;
        }

        private void EnsureDelayInitiativeRowPlanner()
        {
            if (delayInitiativeRowPlanner == null)
                delayInitiativeRowPlanner = new DelayInitiativeRowPlanner();

            delayInitiativeRowPlanner.Bind(turnManager, entityManager);
        }

        private static void CopyRectTransformLayout(RectTransform source, RectTransform target)
        {
            if (source == null || target == null)
                return;

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localScale = Vector3.one;
            target.localRotation = Quaternion.identity;
        }

        private void AutoSizePanelToContent()
        {
            if (!autoSizePanelToSlotsContent)
                return;
            if (panelRoot == null || slotsContainer == null)
                return;

            var panelRect = panelRoot.transform as RectTransform;
            var slotsRect = slotsContainer as RectTransform;
            if (panelRect == null || slotsRect == null)
                return;

            float targetWidth = ComputePanelContentWidthFromBounds(panelRect);
            if (targetWidth <= 0f)
            {
                float slotsContentWidth = ComputeSlotsContentWidth();
                float leftInset = slotsRect.offsetMin.x;
                float rightInset = Mathf.Max(0f, -slotsRect.offsetMax.x);
                targetWidth = leftInset + rightInset + slotsContentWidth;
                if (roundLabel != null)
                    targetWidth = Mathf.Max(targetWidth, GetRoundLabelPreferredWidth() + 16f);
            }

            targetWidth = Mathf.Clamp(targetWidth + panelContentPaddingX, minPanelWidth, maxPanelWidth);

            var size = panelRect.sizeDelta;
            if (Mathf.Abs(size.x - targetWidth) < 0.5f)
                return;

            size.x = targetWidth;
            panelRect.sizeDelta = size;

            if (markersOverlayContainer != null)
                CopyRectTransformLayout(slotsRect, markersOverlayContainer);
        }

        private float ComputeSlotsContentWidth()
        {
            if (activeSlots.Count <= 0)
                return 0f;

            float width = 0f;
            float spacing = 0f;
            float paddingLeft = 0f;
            float paddingRight = 0f;

            if (slotsContainer != null && slotsContainer.TryGetComponent<HorizontalLayoutGroup>(out var h))
            {
                spacing = h.spacing;
                paddingLeft = h.padding.left;
                paddingRight = h.padding.right;
            }

            width += paddingLeft + paddingRight;

            for (int i = 0; i < activeSlots.Count; i++)
            {
                if (i > 0)
                    width += spacing;

                var slot = activeSlots[i];
                if (slot == null)
                    continue;

                if (slot.TryGetComponent<LayoutElement>(out var le) && le.preferredWidth > 0f)
                    width += le.preferredWidth;
                else if (slot.transform is RectTransform rect)
                    width += rect.rect.width;
            }

            return width;
        }

        private float ComputePanelContentWidthFromBounds(RectTransform panelRect)
        {
            if (panelRect == null)
                return 0f;

            bool hasBounds = false;
            Bounds combinedBounds = default;

            if (roundLabel != null)
            {
                var labelRect = roundLabel.rectTransform;
                if (labelRect != null)
                {
                    var labelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panelRect, labelRect);
                    combinedBounds = labelBounds;
                    hasBounds = true;
                }
            }

            for (int i = 0; i < activeSlots.Count; i++)
            {
                var slot = activeSlots[i];
                if (slot == null)
                    continue;

                var slotRect = slot.transform as RectTransform;
                if (slotRect == null)
                    continue;

                var slotBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panelRect, slotRect);
                if (!hasBounds)
                {
                    combinedBounds = slotBounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(slotBounds.min);
                    combinedBounds.Encapsulate(slotBounds.max);
                }
            }

            return hasBounds ? Mathf.Max(0f, combinedBounds.size.x) : 0f;
        }

        private float GetRoundLabelPreferredWidth()
        {
            if (roundLabel == null)
                return 0f;

            string text = roundLabel.text;
            if (string.IsNullOrEmpty(text))
                return 0f;

            Vector2 preferred = roundLabel.GetPreferredValues(text);
            return Mathf.Max(0f, preferred.x);
        }

        private void RepositionInsertionMarkers()
        {
            EnsureDelayPlacementMarkerOverlayPresenter();
            delayMarkerOverlayPresenter?.RepositionMarkers(
                markersOverlayContainer,
                slotsContainer,
                slotByHandle);
        }

        private void MarkMarkersOverlayDirty()
        {
            EnsureDelayPlacementMarkerOverlayPresenter();
            delayMarkerOverlayPresenter?.MarkDirtyIfAny();
        }

        private void CreateOrRefreshSlot(EntityData data, bool isDelayed)
        {
            if (data == null || !data.Handle.IsValid)
                return;

            var slot = GetSlot();
            slot.transform.SetParent(slotsContainer, false);
            slot.gameObject.SetActive(true);
            slot.OnClicked -= HandleSlotClicked;
            slot.OnClicked += HandleSlotClicked;

            slot.SetupStatic(data.Handle, data.Name, data.Team);
            slot.RefreshHP(data.CurrentHP, data.MaxHP, data.IsAlive);
            slot.SetDelayed(isDelayed);

            activeSlots.Add(slot);
            slotByHandle[data.Handle] = slot;
            if (isDelayed)
                appendedDelayedHandles.Add(data.Handle);
        }

        private void AppendInsertionMarkerIfNeeded(EntityHandle anchorHandle)
        {
            if (delayInitiativeRowPlanner == null)
                return;
            if (!delayInitiativeRowPlanner.ShouldAppendPlacementMarker(anchorHandle))
                return;

            EnsureDelayPlacementMarkerOverlayPresenter();
            delayMarkerOverlayPresenter?.AddMarker(
                anchorHandle,
                canSelect: true,
                GetMarkersOverlayParent(),
                insertionMarkerPrefab);
        }

        private void AppendDelayedSlotsAnchoredTo(EntityHandle anchorHandle)
        {
            if (delayInitiativeRowPlanner == null)
                return;

            var delayedAnchored = delayInitiativeRowPlanner.CollectDelayedAnchoredTo(anchorHandle, appendedDelayedHandles);
            for (int i = 0; i < delayedAnchored.Count; i++)
                CreateOrRefreshSlot(delayedAnchored[i], isDelayed: true);
        }

        private void AppendRemainingDelayedSlots()
        {
            if (delayInitiativeRowPlanner == null)
                return;

            var remainingDelayed = delayInitiativeRowPlanner.CollectRemainingDelayed(appendedDelayedHandles);
            for (int i = 0; i < remainingDelayed.Count; i++)
                CreateOrRefreshSlot(remainingDelayed[i], isDelayed: true);
        }

        private void HandleSlotClicked(InitiativeSlot slot)
        {
            if (slot == null || turnManager == null)
                return;

            if (turnManager.IsDelayPlacementSelectionOpen)
            {
                // WotR-style delay placement uses insertion markers between portraits, not portrait clicks.
                return;
            }
        }

        private void HandleDelayPlacementCommitted()
        {
            if (turnManager == null)
                return;

            // TurnStarted will also rebuild, but refresh immediately keeps the click responsive.
            BuildSlots(turnManager.InitiativeOrder);
            UpdateHighlight();
        }

        private void LateUpdate()
        {
            if (delayMarkerOverlayPresenter == null || !delayMarkerOverlayPresenter.IsDirty)
                return;
            if (turnManager == null)
            {
                delayMarkerOverlayPresenter.ClearDirty();
                return;
            }
            if (!turnManager.IsDelayPlacementSelectionOpen || !delayMarkerOverlayPresenter.HasActiveMarkers)
            {
                delayMarkerOverlayPresenter.ClearDirty();
                return;
            }

            RepositionInsertionMarkers();
        }

        private void RefreshDelayPlacementHintLabel()
        {
            EnsureDelayPlacementPromptPresenter();
            EnsureDelayPlacementInteractionCoordinator();
            delayPlacementInteractionCoordinator?.RefreshPromptForCurrentState();
        }

        private void HideDelayPlacementPrompt()
        {
            if (delayPlacementInteractionCoordinator == null && delayPromptPresenter == null && delayPlacementPromptRoot == null)
                return;

            EnsureDelayPlacementPromptPresenter();
            EnsureDelayPlacementInteractionCoordinator();
            delayPlacementInteractionCoordinator?.HidePrompt();
        }

        private void SyncDelayPromptRefsFromPresenter()
        {
            if (delayPromptPresenter == null)
                return;

            delayPlacementPromptRoot = delayPromptPresenter.PromptRoot;
            delayPlacementPromptLabel = delayPromptPresenter.PromptLabel;
            delayPlacementPromptBackground = delayPromptPresenter.PromptBackground;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (turnManager == null)
                Debug.LogWarning("[InitiativeBarController] TurnManager not assigned.", this);
            if (entityManager == null)
                Debug.LogWarning("[InitiativeBarController] EntityManager not assigned.", this);
            if (eventBus == null)
                Debug.LogWarning("[InitiativeBarController] CombatEventBus not assigned.", this);
        }
#endif
    }
}
