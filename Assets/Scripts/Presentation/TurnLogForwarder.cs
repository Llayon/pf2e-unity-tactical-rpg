using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.Presentation
{
    /// <summary>
    /// Converts typed turn events to string log entries.
    /// Typed turn events → string log.
    /// </summary>
    public class TurnLogForwarder : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;

        private EntityHandle lastActor = EntityHandle.None;
        private int lastActions = -1;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null)
                Debug.LogError("[TurnLogForwarder] Missing CombatEventBus", this);
            if (entityManager == null)
                Debug.LogError("[TurnLogForwarder] Missing EntityManager", this);
        }
#endif

        private void OnEnable()
        {
            if (eventBus == null || entityManager == null)
            {
                Debug.LogError("[TurnLogForwarder] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnCombatStartedTyped += OnCombatStartedTyped;
            eventBus.OnCombatEndedTyped += OnCombatEndedTyped;
            eventBus.OnInitiativeRolledTyped += OnInitiativeRolledTyped;
            eventBus.OnRoundStartedTyped += OnRoundStartedTyped;
            eventBus.OnTurnStartedTyped += OnTurnStartedTyped;
            eventBus.OnTurnEndedTyped += OnTurnEndedTyped;
            eventBus.OnActionsChangedTyped += OnActionsChangedTyped;
        }

        private void OnDisable()
        {
            if (eventBus == null) return;
            eventBus.OnCombatStartedTyped -= OnCombatStartedTyped;
            eventBus.OnCombatEndedTyped -= OnCombatEndedTyped;
            eventBus.OnInitiativeRolledTyped -= OnInitiativeRolledTyped;
            eventBus.OnRoundStartedTyped -= OnRoundStartedTyped;
            eventBus.OnTurnStartedTyped -= OnTurnStartedTyped;
            eventBus.OnTurnEndedTyped -= OnTurnEndedTyped;
            eventBus.OnActionsChangedTyped -= OnActionsChangedTyped;
        }

        private void OnCombatStartedTyped(in CombatStartedEvent e)
        {
            lastActor = EntityHandle.None;
            lastActions = -1;
            eventBus.PublishSystem("Combat started.", CombatLogCategory.CombatStart);
        }

        private void OnCombatEndedTyped(in CombatEndedEvent e)
        {
            string message = EncounterEndLogMessageMap.For(e.result);
            eventBus.PublishSystem(message, CombatLogCategory.CombatEnd);
            lastActor = EntityHandle.None;
            lastActions = -1;
        }

        private void OnRoundStartedTyped(in RoundStartedEvent e)
        {
            eventBus.PublishSystem($"Round {e.round} begins.", CombatLogCategory.Turn);
        }

        private void OnInitiativeRolledTyped(in InitiativeRolledEvent e)
        {
            if (entityManager == null || entityManager.Registry == null || e.order == null)
                return;

            for (int i = 0; i < e.order.Count; i++)
            {
                var entry = e.order[i];
                if (!entry.Handle.IsValid)
                    continue;

                eventBus.Publish(
                    entry.Handle,
                    $"rolls initiative d20({entry.Roll.naturalRoll}) + {entry.Roll.source.ToShortLabel()}({FormatSigned(entry.Roll.modifier)}) = {entry.Total}",
                    CombatLogCategory.Turn);
            }
        }

        private void OnTurnStartedTyped(in TurnStartedEvent e)
        {
            lastActor = e.actor;
            lastActions = e.actionsAtStart;

            var data = entityManager.Registry.Get(e.actor);
            string name = data?.Name ?? e.actor.ToString();
            string team = data != null ? data.Team.ToString() : "-";

            eventBus.PublishSystem($"{name} ({team}) starts turn. Actions: {Mathf.Clamp(e.actionsAtStart, 0, 3)}/3", CombatLogCategory.Turn);
        }

        private void OnTurnEndedTyped(in TurnEndedEvent e)
        {
            var data = entityManager.Registry.Get(e.actor);
            string name = data?.Name ?? e.actor.ToString();
            string team = data != null ? data.Team.ToString() : "-";

            eventBus.PublishSystem($"{name} ({team}) ends turn.", CombatLogCategory.Turn);

            if (e.actor == lastActor)
            {
                lastActor = EntityHandle.None;
                lastActions = -1;
            }
        }

        private void OnActionsChangedTyped(in ActionsChangedEvent e)
        {
            if (!e.actor.IsValid) return;

            if (e.actor != lastActor)
            {
                lastActor = e.actor;
                lastActions = e.remaining;
                return;
            }

            if (lastActions >= 0 && e.remaining < lastActions)
            {
                int spent = lastActions - e.remaining;
                eventBus.Publish(e.actor,
                    $"spends {spent} action(s). Remaining {Mathf.Clamp(e.remaining, 0, 3)}/3",
                    CombatLogCategory.Turn);
            }

            lastActions = e.remaining;
        }

        private static string FormatSigned(int value)
        {
            return value >= 0 ? $"+{value}" : value.ToString();
        }
    }
}
