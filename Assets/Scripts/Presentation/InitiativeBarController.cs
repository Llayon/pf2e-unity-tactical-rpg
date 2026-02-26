using System.Collections.Generic;
using UnityEngine;
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
        [SerializeField] private Transform slotsContainer;
        [SerializeField] private InitiativeSlot slotPrefab;

        private readonly List<InitiativeSlot> activeSlots = new List<InitiativeSlot>(32);
        private readonly Stack<InitiativeSlot> slotPool = new Stack<InitiativeSlot>(32);
        private readonly Dictionary<EntityHandle, InitiativeSlot> slotByHandle
            = new Dictionary<EntityHandle, InitiativeSlot>();
        private readonly HashSet<EntityHandle> appendedDelayedHandles = new HashSet<EntityHandle>();

        private void OnEnable()
        {
            if (eventBus != null)
            {
                eventBus.OnCombatStartedTyped += HandleCombatStarted;
                eventBus.OnCombatEndedTyped += HandleCombatEnded;
                eventBus.OnRoundStartedTyped += HandleRoundStarted;
                eventBus.OnTurnStartedTyped += HandleTurnStarted;
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
                eventBus.OnStrikeResolved  -= HandleStrikeResolved;
                eventBus.OnEntityDefeated  -= HandleEntityDefeated;
            }
        }

        // ─── Event Handlers ───────────────────────────────────────────────────

        private void HandleCombatStarted(in CombatStartedEvent e)
        {
            if (turnManager == null) return;

            SetPanelVisible(true);
            if (roundLabel != null)
                roundLabel.SetText("Round {0}", turnManager.RoundNumber);
            BuildSlots(turnManager.InitiativeOrder);
            UpdateHighlight();
        }

        private void HandleCombatEnded(in CombatEndedEvent e)
        {
            SetPanelVisible(false);
            ClearSlotsToPool();
        }

        private void HandleRoundStarted(in RoundStartedEvent e)
        {
            if (roundLabel != null)
                roundLabel.SetText("Round {0}", e.round);
        }

        private void HandleTurnStarted(in TurnStartedEvent e)
        {
            if (turnManager != null)
                BuildSlots(turnManager.InitiativeOrder);
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
                AppendDelayedSlotsAnchoredTo(handle);
            }

            AppendRemainingDelayedSlots();
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

        private readonly List<EntityData> delayedEntityBuffer = new List<EntityData>(8);

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

        private void AppendDelayedSlotsAnchoredTo(EntityHandle anchorHandle)
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return;

            delayedEntityBuffer.Clear();
            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null || !data.Handle.IsValid) continue;
                if (!turnManager.IsDelayed(data.Handle)) continue;
                if (appendedDelayedHandles.Contains(data.Handle)) continue;
                if (!turnManager.TryGetDelayedPlannedAnchor(data.Handle, out var plannedAnchor)) continue;
                if (plannedAnchor != anchorHandle) continue;

                delayedEntityBuffer.Add(data);
            }

            delayedEntityBuffer.Sort(static (a, b) => a.Handle.Id.CompareTo(b.Handle.Id));
            for (int i = 0; i < delayedEntityBuffer.Count; i++)
                CreateOrRefreshSlot(delayedEntityBuffer[i], isDelayed: true);
        }

        private void AppendRemainingDelayedSlots()
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return;

            delayedEntityBuffer.Clear();
            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null || !data.Handle.IsValid) continue;
                if (!turnManager.IsDelayed(data.Handle)) continue;
                if (appendedDelayedHandles.Contains(data.Handle)) continue;
                delayedEntityBuffer.Add(data);
            }

            delayedEntityBuffer.Sort(static (a, b) => a.Handle.Id.CompareTo(b.Handle.Id));
            for (int i = 0; i < delayedEntityBuffer.Count; i++)
                CreateOrRefreshSlot(delayedEntityBuffer[i], isDelayed: true);
        }

        private void HandleSlotClicked(InitiativeSlot slot)
        {
            if (slot == null || turnManager == null)
                return;

            if (!turnManager.IsDelayPlacementSelectionOpen)
                return;

            if (!turnManager.TryDelayCurrentTurnAfterActor(slot.Handle))
                return;

            // TurnStarted will also rebuild, but refresh immediately keeps the click responsive.
            BuildSlots(turnManager.InitiativeOrder);
            UpdateHighlight();
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
