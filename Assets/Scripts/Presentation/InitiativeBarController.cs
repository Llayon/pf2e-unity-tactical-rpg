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
        private readonly struct VisibleSlotEntry
        {
            public VisibleSlotEntry(EntityData data, bool isDelayed)
            {
                Data = data;
                IsDelayed = isDelayed;
            }

            public EntityData Data { get; }
            public bool IsDelayed { get; }
        }

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

        [Header("Frame Sprites")]
        [SerializeField] private Sprite playerFrameSprite;
        [SerializeField] private Sprite enemyFrameSprite;

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
        private readonly HashSet<EntityHandle> actedThisRound = new HashSet<EntityHandle>();
        private RectTransform slotPoolContainer;
        private DelayPlacementMarkerOverlayPresenter delayMarkerOverlayPresenter;
        private DelayPlacementPromptPresenter delayPromptPresenter;
        private DelayPlacementInteractionCoordinator delayPlacementInteractionCoordinator;
        private DelayInitiativeRowPlanner delayInitiativeRowPlanner;

        private void OnEnable()
        {
            ApplyRuntimeVisualStyle();

            if (eventBus != null)
            {
                eventBus.OnCombatStartedTyped += HandleCombatStarted;
                eventBus.OnCombatEndedTyped += HandleCombatEnded;
                eventBus.OnRoundStartedTyped += HandleRoundStarted;
                eventBus.OnTurnStartedTyped += HandleTurnStarted;
                eventBus.OnTurnEndedTyped += HandleTurnEnded;
                eventBus.OnStrikeResolved  += HandleStrikeResolved;
                eventBus.OnDamageAppliedTyped += HandleDamageApplied;
                eventBus.OnEntityDefeated  += HandleEntityDefeated;
                eventBus.OnDelayPlacementSelectionChangedTyped += HandleDelayPlacementSelectionChanged;
                eventBus.OnDelayReturnWindowOpenedTyped += HandleDelayReturnWindowOpened;
                eventBus.OnDelayReturnWindowClosedTyped += HandleDelayReturnWindowClosed;
                eventBus.OnDelayedTurnEnteredTyped += HandleDelayedTurnChanged;
                eventBus.OnDelayedTurnResumedTyped += HandleDelayedTurnChanged;
                eventBus.OnDelayedTurnExpiredTyped += HandleDelayedTurnChanged;
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
                eventBus.OnTurnEndedTyped -= HandleTurnEnded;
                eventBus.OnStrikeResolved  -= HandleStrikeResolved;
                eventBus.OnDamageAppliedTyped -= HandleDamageApplied;
                eventBus.OnEntityDefeated  -= HandleEntityDefeated;
                eventBus.OnDelayPlacementSelectionChangedTyped -= HandleDelayPlacementSelectionChanged;
                eventBus.OnDelayReturnWindowOpenedTyped -= HandleDelayReturnWindowOpened;
                eventBus.OnDelayReturnWindowClosedTyped -= HandleDelayReturnWindowClosed;
                eventBus.OnDelayedTurnEnteredTyped -= HandleDelayedTurnChanged;
                eventBus.OnDelayedTurnResumedTyped -= HandleDelayedTurnChanged;
                eventBus.OnDelayedTurnExpiredTyped -= HandleDelayedTurnChanged;
            }
        }

        // ─── Event Handlers ───────────────────────────────────────────────────

        private void HandleCombatStarted(in CombatStartedEvent e)
        {
            if (turnManager == null) return;

            EnsureRuntimeUiReferences();
            SetPanelVisible(true);
            actedThisRound.Clear();
            if (roundLabel != null)
                roundLabel.SetText("Round {0}", turnManager.RoundNumber);
            HideDelayPlacementPrompt();
            BuildSlots(turnManager.InitiativeOrder);
            RefreshSlotVisuals();
        }

        private void HandleCombatEnded(in CombatEndedEvent e)
        {
            actedThisRound.Clear();
            SetPanelVisible(false);
            HideDelayPlacementPrompt();
            ClearSlotsToPool();
        }

        private void HandleRoundStarted(in RoundStartedEvent e)
        {
            actedThisRound.Clear();
            if (roundLabel != null)
                roundLabel.SetText("Round {0}", e.round);
            if (turnManager != null)
                BuildSlots(turnManager.InitiativeOrder);
            RefreshSlotVisuals();
            if (turnManager == null || !turnManager.IsDelayPlacementSelectionOpen)
                HideDelayPlacementPrompt();
        }

        private void HandleTurnStarted(in TurnStartedEvent e)
        {
            _ = e;
            RefreshSlotVisuals();
        }

        private void HandleTurnEnded(in TurnEndedEvent e)
        {
            if (e.actor.IsValid)
                actedThisRound.Add(e.actor);

            RefreshSlotVisuals();
        }

        private void HandleDelayPlacementSelectionChanged(in DelayPlacementSelectionChangedEvent e)
        {
            _ = e;
            RefreshDelayPlacementUi();
        }

        private void HandleDelayReturnWindowOpened(in DelayReturnWindowOpenedEvent e)
        {
            _ = e;
            RefreshDelayReturnWindowUi();
        }

        private void HandleDelayReturnWindowClosed(in DelayReturnWindowClosedEvent e)
        {
            _ = e;
            RefreshDelayReturnWindowUi();
        }

        private void HandleDelayedTurnChanged(in DelayedTurnEnteredEvent e)
        {
            _ = e;
            RefreshDelayedActorsUi();
        }

        private void HandleDelayedTurnChanged(in DelayedTurnResumedEvent e)
        {
            _ = e;
            RefreshDelayedActorsUi();
        }

        private void HandleDelayedTurnChanged(in DelayedTurnExpiredEvent e)
        {
            _ = e;
            RefreshDelayedActorsUi();
        }

        private void RefreshDelayPlacementUi()
        {
            if (turnManager == null)
                return;

            BuildSlots(turnManager.InitiativeOrder);
            RefreshSlotVisuals();
        }

        private void RefreshDelayReturnWindowUi()
        {
            RefreshSlotVisuals();
        }

        private void RefreshDelayedActorsUi()
        {
            if (turnManager == null)
                return;

            BuildSlots(turnManager.InitiativeOrder);
            RefreshSlotVisuals();
        }

        private void HandleStrikeResolved(in StrikeResolvedEvent e)
        {
            if (entityManager == null || entityManager.Registry == null) return;
            if (!slotByHandle.TryGetValue(e.target, out var slot)) return;

            var data = entityManager.Registry.Get(e.target);
            if (data == null) return;

            slot.RefreshHP(data.CurrentHP, data.MaxHP, data.IsAlive);
        }

        private void HandleDamageApplied(in DamageAppliedEvent e)
        {
            if (entityManager == null || entityManager.Registry == null) return;
            if (!slotByHandle.TryGetValue(e.target, out var slot)) return;

            var data = entityManager.Registry.Get(e.target);
            if (data == null) return;

            slot.RefreshHP(data.CurrentHP, data.MaxHP, data.IsAlive);
        }

        private void HandleEntityDefeated(in EntityDefeatedEvent e)
        {
            actedThisRound.Remove(e.handle);

            if (turnManager != null)
            {
                BuildSlots(turnManager.InitiativeOrder);
                RefreshSlotVisuals();
                return;
            }

            if (slotByHandle.TryGetValue(e.handle, out var slot))
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

            var visibleSlots = BuildVisibleSlotSequence(order);
            var duplicateOrdinals = BuildDuplicateOrdinals(visibleSlots);

            for (int i = 0; i < visibleSlots.Count; i++)
            {
                var entry = visibleSlots[i];
                var data = entry.Data;
                CreateOrRefreshSlot(data, entry.IsDelayed, GetDuplicateBadgeText(data.Handle, duplicateOrdinals));
                if (!entry.IsDelayed)
                    AppendInsertionMarkerIfNeeded(data.Handle);
            }

            var slotsRect = slotsContainer as RectTransform;
            if (slotsRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(slotsRect);

            ApplyActiveVisualOffsets();
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
            while (slotPool.Count > 0)
            {
                var pooledSlot = slotPool.Pop();
                if (pooledSlot != null)
                    return pooledSlot;
            }

            var inst = Instantiate(slotPrefab, GetOrCreateSlotPoolContainer());
            inst.gameObject.name = "InitiativeSlot";
            inst.gameObject.SetActive(false);
            return inst;
        }

        private RectTransform GetOrCreateSlotPoolContainer()
        {
            if (slotPoolContainer != null)
                return slotPoolContainer;

            var parent = transform as RectTransform;
            var poolRoot = new GameObject("_InitiativeSlotPool", typeof(RectTransform));
            poolRoot.SetActive(false);

            var poolRect = poolRoot.GetComponent<RectTransform>();
            poolRect.SetParent(parent, false);
            poolRect.anchorMin = new Vector2(0f, 0f);
            poolRect.anchorMax = new Vector2(0f, 0f);
            poolRect.pivot = new Vector2(0f, 0f);
            poolRect.anchoredPosition = Vector2.zero;
            poolRect.sizeDelta = Vector2.zero;

            slotPoolContainer = poolRect;
            return slotPoolContainer;
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
                s.SetActedThisRound(false);
                s.SetDelayed(false);
                s.SetDefeated(false);
                s.gameObject.SetActive(false);
                s.transform.SetParent(GetOrCreateSlotPoolContainer(), false);
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
            {
                ApplyActiveVisualOffsets();
                return;
            }

            if (idx >= 0 && idx < turnManager.InitiativeOrder.Count)
            {
                var handle = turnManager.InitiativeOrder[idx].Handle;
                if (slotByHandle.TryGetValue(handle, out var slot))
                    slot.SetHighlight(true);
            }

            ApplyActiveVisualOffsets();
        }

        private void RefreshSlotVisuals()
        {
            if (entityManager == null || entityManager.Registry == null)
            {
                UpdateHighlight();
                return;
            }

            for (int i = 0; i < activeSlots.Count; i++)
            {
                var slot = activeSlots[i];
                if (slot == null)
                    continue;

                var data = entityManager.Registry.Get(slot.Handle);
                if (data == null)
                    continue;

                slot.RefreshHP(data.CurrentHP, data.MaxHP, data.IsAlive);
                slot.SetDefeated(!data.IsAlive);
                slot.SetActedThisRound(actedThisRound.Contains(slot.Handle));
            }

            UpdateHighlight();
            AutoSizePanelToContent();
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
            ApplyRuntimeVisualStyle();
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

        private void ApplyActiveVisualOffsets()
        {
            if (activeSlots.Count <= 0)
                return;

            int activeIndex = GetActiveVisualSlotIndex();
            float slotWidth = GetSlotLayoutWidth();
            if (slotWidth <= 0f)
                return;

            float activeScale = activeIndex >= 0 && activeIndex < activeSlots.Count
                ? activeSlots[activeIndex].ActiveScaleFactor
                : 1f;

            for (int i = 0; i < activeSlots.Count; i++)
            {
                var slot = activeSlots[i];
                if (slot == null)
                    continue;

                slot.SetVisualOffsetX(GetVisualOffsetX(i, activeIndex, slotWidth, activeScale));
            }
        }

        private int GetActiveVisualSlotIndex()
        {
            if (turnManager == null || turnManager.State == TurnState.DelayReturnWindow)
                return -1;

            int idx = turnManager.CurrentIndex;
            if (idx < 0 || idx >= turnManager.InitiativeOrder.Count)
                return -1;

            var handle = turnManager.InitiativeOrder[idx].Handle;
            if (!slotByHandle.TryGetValue(handle, out var activeSlot) || activeSlot == null)
                return -1;

            return activeSlots.IndexOf(activeSlot);
        }

        private float GetSlotLayoutWidth()
        {
            for (int i = 0; i < activeSlots.Count; i++)
            {
                var slot = activeSlots[i];
                if (slot == null)
                    continue;

                if (slot.TryGetComponent<LayoutElement>(out var layoutElement) && layoutElement.preferredWidth > 0f)
                    return layoutElement.preferredWidth;

                if (slot.transform is RectTransform rect)
                    return rect.rect.width;
            }

            return 0f;
        }

        internal static float GetVisualOffsetX(int slotIndex, int activeIndex, float slotWidth, float activeScaleFactor)
        {
            if (slotIndex < 0 || activeIndex < 0 || slotWidth <= 0f || activeScaleFactor <= 1f)
                return 0f;

            float extraWidth = slotWidth * (activeScaleFactor - 1f);
            if (slotIndex < activeIndex)
                return 0f;

            if (slotIndex == activeIndex)
                return extraWidth * 0.5f;

            return extraWidth;
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

        private void CreateOrRefreshSlot(EntityData data, bool isDelayed, string duplicateBadgeText)
        {
            if (data == null || !data.Handle.IsValid || !data.IsAlive)
                return;

            var slot = GetSlot();
            slot.transform.SetParent(slotsContainer, false);
            slot.transform.SetAsLastSibling();
            slot.gameObject.SetActive(true);
            slot.OnClicked -= HandleSlotClicked;
            slot.OnClicked += HandleSlotClicked;

            var frameSprite = data.Team == Team.Player ? playerFrameSprite :
                              data.Team == Team.Enemy  ? enemyFrameSprite  : null;
            slot.SetupStatic(data.Handle, data.Name, data.Team, data.Portrait, frameSprite, duplicateBadgeText);
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

        private void AppendDelayedSlotsAnchoredTo(EntityHandle anchorHandle, IReadOnlyDictionary<EntityHandle, int> duplicateOrdinals)
        {
            if (delayInitiativeRowPlanner == null)
                return;

            var delayedAnchored = delayInitiativeRowPlanner.CollectDelayedAnchoredTo(anchorHandle, appendedDelayedHandles);
            for (int i = 0; i < delayedAnchored.Count; i++)
                CreateOrRefreshSlot(
                    delayedAnchored[i],
                    isDelayed: true,
                    GetDuplicateBadgeText(delayedAnchored[i].Handle, duplicateOrdinals));
        }

        private void AppendRemainingDelayedSlots(IReadOnlyDictionary<EntityHandle, int> duplicateOrdinals)
        {
            if (delayInitiativeRowPlanner == null)
                return;

            var remainingDelayed = delayInitiativeRowPlanner.CollectRemainingDelayed(appendedDelayedHandles);
            for (int i = 0; i < remainingDelayed.Count; i++)
                CreateOrRefreshSlot(
                    remainingDelayed[i],
                    isDelayed: true,
                    GetDuplicateBadgeText(remainingDelayed[i].Handle, duplicateOrdinals));
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

            BuildSlots(turnManager.InitiativeOrder);
            RefreshSlotVisuals();
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

        public bool TryGetTurnOptionsAnchorRect(out RectTransform anchorRect)
        {
            anchorRect = null;
            if (turnManager == null)
                return false;

            EntityHandle anchorHandle = EntityHandle.None;
            if (turnManager.IsDelayReturnWindowOpen)
                anchorHandle = turnManager.DelayReturnWindowAfterActor;

            if (!anchorHandle.IsValid)
                anchorHandle = turnManager.CurrentEntity;

            if (anchorHandle.IsValid && slotByHandle.TryGetValue(anchorHandle, out var anchorSlot) && anchorSlot != null)
            {
                anchorRect = anchorSlot.transform as RectTransform;
                if (anchorRect != null)
                    return true;
            }

            if (activeSlots.Count > 0 && activeSlots[0] != null)
            {
                anchorRect = activeSlots[0].transform as RectTransform;
                return anchorRect != null;
            }

            return false;
        }

        private void ApplyRuntimeVisualStyle()
        {
            CombatUiTypography.ApplyTitle(roundLabel, 14.5f, 0.1f, CombatUiPalette.HudTextPrimaryColor);
            CombatUiTypography.ApplySecondary(delayPlacementPromptLabel, 11f, 0.08f, CombatUiPalette.HudTextSecondaryColor);

            if (delayPlacementPromptBackground != null)
                delayPlacementPromptBackground.color = CombatUiPalette.HudPanelBackgroundColor;
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

        private List<VisibleSlotEntry> BuildVisibleSlotSequence(IReadOnlyList<InitiativeEntry> order)
        {
            var visibleActors = new List<VisibleSlotEntry>(order != null ? order.Count * 2 : 0);
            if (order == null || entityManager == null || entityManager.Registry == null)
                return visibleActors;

            var plannedDelayedHandles = new HashSet<EntityHandle>();
            for (int i = 0; i < order.Count; i++)
            {
                var handle = order[i].Handle;
                var data = entityManager.Registry.Get(handle);
                if (data == null || !data.IsAlive)
                    continue;

                visibleActors.Add(new VisibleSlotEntry(data, false));

                if (delayInitiativeRowPlanner == null)
                    continue;

                var delayedAnchored = delayInitiativeRowPlanner.CollectDelayedAnchoredTo(handle, plannedDelayedHandles);
                for (int delayedIndex = 0; delayedIndex < delayedAnchored.Count; delayedIndex++)
                {
                    var delayedActor = delayedAnchored[delayedIndex];
                    if (delayedActor == null || !delayedActor.IsAlive)
                        continue;

                    visibleActors.Add(new VisibleSlotEntry(delayedActor, true));
                }
            }

            if (delayInitiativeRowPlanner != null)
            {
                var remainingDelayed = delayInitiativeRowPlanner.CollectRemainingDelayed(plannedDelayedHandles);
                for (int i = 0; i < remainingDelayed.Count; i++)
                {
                    var delayedActor = remainingDelayed[i];
                    if (delayedActor == null || !delayedActor.IsAlive)
                        continue;

                    visibleActors.Add(new VisibleSlotEntry(delayedActor, true));
                }
            }

            return visibleActors;
        }

        private static Dictionary<EntityHandle, int> BuildDuplicateOrdinals(IReadOnlyList<EntityData> visibleActors)
        {
            var visibleEntries = new List<VisibleSlotEntry>(visibleActors != null ? visibleActors.Count : 0);
            if (visibleActors != null)
            {
                for (int i = 0; i < visibleActors.Count; i++)
                {
                    var actor = visibleActors[i];
                    if (actor == null)
                        continue;

                    visibleEntries.Add(new VisibleSlotEntry(actor, false));
                }
            }

            return BuildDuplicateOrdinals(visibleEntries);
        }

        private static Dictionary<EntityHandle, int> BuildDuplicateOrdinals(IReadOnlyList<VisibleSlotEntry> visibleActors)
        {
            var totalsByGroup = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var ordinalsByHandle = new Dictionary<EntityHandle, int>();
            var assignedByGroup = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < visibleActors.Count; i++)
            {
                string groupKey = NormalizeDuplicateIdentityKey(visibleActors[i].Data);
                if (string.IsNullOrEmpty(groupKey))
                    continue;

                totalsByGroup.TryGetValue(groupKey, out int total);
                totalsByGroup[groupKey] = total + 1;
            }

            for (int i = 0; i < visibleActors.Count; i++)
            {
                var actor = visibleActors[i].Data;
                string groupKey = NormalizeDuplicateIdentityKey(actor);
                if (string.IsNullOrEmpty(groupKey))
                    continue;

                if (!totalsByGroup.TryGetValue(groupKey, out int total) || total <= 1)
                    continue;

                assignedByGroup.TryGetValue(groupKey, out int nextOrdinal);
                nextOrdinal += 1;
                assignedByGroup[groupKey] = nextOrdinal;
                ordinalsByHandle[actor.Handle] = nextOrdinal;
            }

            return ordinalsByHandle;
        }

        private static string GetDuplicateBadgeText(EntityHandle handle, IReadOnlyDictionary<EntityHandle, int> duplicateOrdinals)
        {
            if (duplicateOrdinals == null || !duplicateOrdinals.TryGetValue(handle, out int ordinal) || ordinal <= 0)
                return null;

            return ordinal.ToString();
        }

        private static string NormalizeDuplicateIdentityKey(EntityData actor)
        {
            if (actor == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(actor.EncounterActorId))
                return StripTrailingOrdinalSuffix(actor.EncounterActorId);

            return StripTrailingOrdinalSuffix(actor.Name);
        }

        private static string StripTrailingOrdinalSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string trimmed = value.Trim();
            int digitStart = trimmed.Length;
            while (digitStart > 0 && char.IsDigit(trimmed[digitStart - 1]))
                digitStart--;

            if (digitStart == trimmed.Length)
                return trimmed;

            int suffixStart = digitStart;
            while (suffixStart > 0)
            {
                char c = trimmed[suffixStart - 1];
                if (c != '_' && c != '-' && c != ' ')
                    break;

                suffixStart--;
            }

            string stripped = trimmed.Substring(0, suffixStart).TrimEnd('_', '-', ' ');
            return string.IsNullOrEmpty(stripped) ? trimmed : stripped;
        }
    }
}
