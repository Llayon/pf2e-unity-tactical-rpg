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
            eventBus.OnJumpResolvedTyped += OnJumpResolvedTyped;
        }

        private void OnDisable()
        {
            if (eventBus == null) return;
            eventBus.OnCombatStartedTyped -= OnCombatStartedTyped;
            eventBus.OnCombatEndedTyped -= OnCombatEndedTyped;
            eventBus.OnInitiativeRolledTyped -= OnInitiativeRolledTyped;
            eventBus.OnRoundStartedTyped -= OnRoundStartedTyped;
            eventBus.OnTurnStartedTyped -= OnTurnStartedTyped;
            eventBus.OnJumpResolvedTyped -= OnJumpResolvedTyped;
        }

        private void OnCombatStartedTyped(in CombatStartedEvent e)
        {
            lastActor = EntityHandle.None;
            lastActions = -1;
            eventBus.PublishSystem(CombatLogRichText.Round("Combat started."), CombatLogCategory.CombatStart);
        }

        private void OnCombatEndedTyped(in CombatEndedEvent e)
        {
            string message = EncounterEndLogMessageMap.For(e.result);
            eventBus.PublishSystem(CombatLogRichText.Round(message), CombatLogCategory.CombatEnd);
            lastActor = EntityHandle.None;
            lastActions = -1;
        }

        private void OnRoundStartedTyped(in RoundStartedEvent e)
        {
            eventBus.PublishSystem(CombatLogRichText.Round($"Round {e.round} begins."), CombatLogCategory.Turn);
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
                    $"{CombatLogRichText.Verb("rolls initiative:")} {RollBreakdownFormatter.FormatRoll(entry.Roll)}",
                    CombatLogCategory.Turn);
            }
        }

        private void OnTurnStartedTyped(in TurnStartedEvent e)
        {
            lastActor = e.actor;
            lastActions = e.actionsAtStart;

            var data = entityManager.Registry.Get(e.actor);
            string rawName = data?.Name ?? e.actor.ToString();
            var team = data?.Team ?? Team.Neutral;

            int actions = Mathf.Clamp(e.actionsAtStart, 0, 3);
            string diamonds = CombatLogRichText.ActionCost(actions);
            eventBus.PublishSystem(
                $"{CombatLogRichText.Round("—")} {CombatLogRichText.EntityName(rawName, team)} {diamonds} {CombatLogRichText.Round("—")}",
                CombatLogCategory.Turn);
        }

        private void OnJumpResolvedTyped(in JumpResolvedEvent e)
        {
            string jumpKind = e.jumpType switch
            {
                JumpType.Leap => "Leap",
                JumpType.LongJump => "Long Jump",
                JumpType.HighJump => "High Jump",
                _ => "Jump"
            };

            string checkPart = e.hasCheck
                ? $" ({RollBreakdownFormatter.FormatRoll(e.checkRoll)} vs DC {e.dc} -> {e.degree})"
                : string.Empty;

            string outcome = e.movedToLanding
                ? $"to {e.landingCell}"
                : "but fails to reach the destination";

            string pronePart = e.becameProne ? ", falls prone" : string.Empty;
            eventBus.Publish(
                e.actor,
                $"{CombatLogRichText.Verb($"{jumpKind} {outcome}{checkPart}{pronePart}.")}",
                CombatLogCategory.Movement);
        }
    }
}
