using PF2e.Core;
using UnityEngine;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Subscribes Ready Strike trigger listeners to CombatEventBus and forwards typed events to TurnManager.
    /// Keeps TurnManager focused on turn orchestration and runtime state.
    /// </summary>
    public sealed class ReadyStrikeEventBinder : MonoBehaviour
    {
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private CombatEventBus eventBus;

        private bool subscribed;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (turnManager == null)
                turnManager = GetComponent<TurnManager>();
        }
#endif

        private void OnEnable()
        {
            ResolveDependenciesIfMissing();
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        internal void Configure(TurnManager manager, CombatEventBus bus)
        {
            if (subscribed && ((manager != null && manager != turnManager) || (bus != null && bus != eventBus)))
                Unsubscribe();

            if (manager != null)
                turnManager = manager;

            if (bus != null)
                eventBus = bus;

            if (!isActiveAndEnabled)
                return;

            ResolveDependenciesIfMissing();
            TrySubscribe();
        }

        private void ResolveDependenciesIfMissing()
        {
            if (turnManager == null)
                turnManager = GetComponent<TurnManager>();

            if (eventBus == null)
                eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
        }

        private void TrySubscribe()
        {
            if (subscribed)
                return;

            if (turnManager == null || eventBus == null)
                return;

            eventBus.OnEntityMovedTyped += HandleEntityMoved;
            eventBus.OnStrikePreDamageTyped += HandleStrikePreDamage;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || eventBus == null)
                return;

            eventBus.OnEntityMovedTyped -= HandleEntityMoved;
            eventBus.OnStrikePreDamageTyped -= HandleStrikePreDamage;
            subscribed = false;
        }

        private void HandleEntityMoved(in EntityMovedEvent e)
        {
            turnManager?.HandleEntityMoved(in e);
        }

        private void HandleStrikePreDamage(in StrikePreDamageEvent e)
        {
            turnManager?.HandleStrikePreDamage(in e);
        }
    }
}
