using UnityEngine;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Adapter: forwards TurnManager "when" events into CombatEventBus "what" messages.
    /// Keeps TurnManager free of presentation dependencies.
    /// Inspector-only wiring.
    /// </summary>
    public class TurnManagerLogForwarder : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (turnManager == null) Debug.LogError("[TurnForwarder] Missing reference: TurnManager", this);
            if (entityManager == null) Debug.LogError("[TurnForwarder] Missing reference: EntityManager", this);
            if (eventBus == null) Debug.LogError("[TurnForwarder] Missing reference: CombatEventBus", this);
        }
#endif

        private void OnEnable()
        {
            if (turnManager == null || entityManager == null || eventBus == null)
            {
                Debug.LogError("[TurnForwarder] Missing dependencies. Disabling TurnManagerLogForwarder.", this);
                enabled = false;
                return;
            }

            turnManager.OnCombatStarted += HandleCombatStarted;
            turnManager.OnCombatEnded += HandleCombatEnded;
            turnManager.OnRoundStarted += HandleRoundStarted;
            turnManager.OnTurnStarted += HandleTurnStarted;
            turnManager.OnTurnEnded += HandleTurnEnded;
            turnManager.OnActionsChanged += HandleActionsChanged;
        }

        private void OnDisable()
        {
            if (turnManager == null) return;

            turnManager.OnCombatStarted -= HandleCombatStarted;
            turnManager.OnCombatEnded -= HandleCombatEnded;
            turnManager.OnRoundStarted -= HandleRoundStarted;
            turnManager.OnTurnStarted -= HandleTurnStarted;
            turnManager.OnTurnEnded -= HandleTurnEnded;
            turnManager.OnActionsChanged -= HandleActionsChanged;
        }

        private EntityHandle lastActor = EntityHandle.None;
        private int lastActions = -1;

        private void HandleCombatStarted()
        {
            lastActor = EntityHandle.None;
            lastActions = -1;
            eventBus.PublishSystem("Combat started.", CombatLogCategory.CombatStart);
        }

        private void HandleCombatEnded()
        {
            eventBus.PublishSystem("Combat ended.", CombatLogCategory.CombatEnd);
            lastActor = EntityHandle.None;
            lastActions = -1;
        }

        private void HandleRoundStarted(int round)
        {
            eventBus.PublishSystem($"Round {round} begins.");
        }

        private void HandleTurnStarted(EntityHandle actor)
        {
            lastActor = actor;
            lastActions = turnManager != null ? turnManager.ActionsRemaining : -1;

            var (name, team) = ResolveNameTeam(actor);

            // System message (no actor prefix; consumer will not add name)
            eventBus.PublishSystem($"{name} ({team}) starts turn. Actions: {ClampActions(lastActions)}/3");
        }

        private void HandleTurnEnded(EntityHandle actor)
        {
            var (name, team) = ResolveNameTeam(actor);
            eventBus.PublishSystem($"{name} ({team}) ends turn.");

            if (actor == lastActor)
            {
                lastActor = EntityHandle.None;
                lastActions = -1;
            }
        }

        private void HandleActionsChanged(int remaining)
        {
            if (turnManager == null) return;

            var current = turnManager.CurrentEntity;
            if (!current.IsValid) return;

            // Track actor switch
            if (current != lastActor)
            {
                lastActor = current;
                lastActions = remaining;
                return;
            }

            // Log only decreases
            if (lastActions >= 0 && remaining < lastActions)
            {
                int spent = lastActions - remaining;

                // Publish with actor handle so consumer prefixes name consistently
                eventBus.Publish(current,
                    $"spends {spent} action(s). Remaining: {ClampActions(remaining)}/3",
                    CombatLogCategory.Turn);
            }

            lastActions = remaining;
        }

        private (string name, string team) ResolveNameTeam(EntityHandle handle)
        {
            if (entityManager == null || entityManager.Registry == null || !handle.IsValid)
                return (handle.IsValid ? handle.ToString() : "-", "-");

            var data = entityManager.Registry.Get(handle);
            string name = data?.Name ?? handle.ToString();
            string team = data != null ? data.Team.ToString() : "-";
            return (name, team);
        }

        private static int ClampActions(int a) => Mathf.Clamp(a, 0, 3);
    }
}
