using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Ready Strike basic action (PF2e MVP):
    /// - spends 2 actions now,
    /// - stores a single readied strike target,
    /// - strike is attempted later as a reaction when trigger fires.
    /// Current trigger model is movement-based (Overwatch-like UX).
    /// </summary>
    public class ReadyStrikeAction : MonoBehaviour
    {
        public const int ActionCost = 2;

        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private StrikeAction strikeAction;
        [SerializeField] private CombatEventBus eventBus;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (turnManager == null) Debug.LogWarning("[ReadyStrikeAction] Missing TurnManager", this);
            if (entityManager == null) Debug.LogWarning("[ReadyStrikeAction] Missing EntityManager", this);
            if (strikeAction == null) Debug.LogWarning("[ReadyStrikeAction] Missing StrikeAction", this);
            if (eventBus == null) Debug.LogWarning("[ReadyStrikeAction] Missing CombatEventBus", this);
        }
#endif

        public void InjectDependencies(
            TurnManager turnManager,
            EntityManager entityManager,
            StrikeAction strikeAction,
            CombatEventBus eventBus)
        {
            if (turnManager != null) this.turnManager = turnManager;
            if (entityManager != null) this.entityManager = entityManager;
            if (strikeAction != null) this.strikeAction = strikeAction;
            if (eventBus != null) this.eventBus = eventBus;
        }

        public TargetingFailureReason GetReadyStrikeTargetFailure(EntityHandle actor, EntityHandle target)
        {
            if (strikeAction == null)
                return TargetingFailureReason.InvalidState;

            return strikeAction.GetStrikeTargetFailure(actor, target);
        }

        public bool TryPrepareReadiedStrike(EntityHandle actor, EntityHandle target, int preparedRound)
        {
            if (!actor.IsValid || !target.IsValid)
                return false;
            if (turnManager == null || entityManager == null || entityManager.Registry == null || strikeAction == null)
                return false;

            if (GetReadyStrikeTargetFailure(actor, target) != TargetingFailureReason.None)
                return false;

            if (!turnManager.TryPrepareReadiedStrike(actor, target, preparedRound))
                return false;

            var targetData = entityManager.Registry.Get(target);
            string targetName = targetData != null && !string.IsNullOrWhiteSpace(targetData.Name)
                ? targetData.Name
                : $"Entity#{target.Id}";

            eventBus?.Publish(
                actor,
                $"readies Strike against {targetName} (trigger: target moves).",
                CombatLogCategory.Turn);
            return true;
        }
    }
}
