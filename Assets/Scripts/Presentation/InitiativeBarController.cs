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

            if (order == null || entityManager == null || entityManager.Registry == null) return;

            for (int i = 0; i < order.Count; i++)
            {
                var handle = order[i].Handle;
                var data = entityManager.Registry.Get(handle);
                if (data == null) continue;

                var slot = GetSlot();
                slot.transform.SetParent(slotsContainer, false);
                slot.gameObject.SetActive(true);

                slot.SetupStatic(handle, data.Name, data.Team);
                slot.RefreshHP(data.CurrentHP, data.MaxHP, data.IsAlive);

                activeSlots.Add(slot);
                slotByHandle[handle] = slot;
            }
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
