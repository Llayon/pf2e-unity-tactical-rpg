using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Owns the shared GrappleService instance and applies grapple lifecycle rules from typed combat events.
    /// Subscribes to logical turn-end and movement commit events (not animation completion).
    /// </summary>
    public class GrappleLifecycleController : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        private readonly GrappleService service = new();
        private readonly List<ConditionDelta> conditionDeltaBuffer = new();
        private bool subscribed;

        public GrappleService Service
        {
            get
            {
                TrySubscribe();
                return service;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[GrappleLifecycle] Missing EntityManager", this);
            if (eventBus == null) Debug.LogError("[GrappleLifecycle] Missing CombatEventBus", this);
        }
#endif

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (!subscribed || eventBus == null) return;

            eventBus.OnTurnEndedTyped -= OnTurnEnded;
            eventBus.OnCombatEndedTyped -= OnCombatEnded;
            eventBus.OnEntityMovedTyped -= OnEntityMoved;
            subscribed = false;
        }

        private void TrySubscribe()
        {
            if (subscribed) return;
            if (eventBus == null) return;

            eventBus.OnTurnEndedTyped += OnTurnEnded;
            eventBus.OnCombatEndedTyped += OnCombatEnded;
            eventBus.OnEntityMovedTyped += OnEntityMoved;
            subscribed = true;
        }

        private void OnTurnEnded(in TurnEndedEvent e)
        {
            if (entityManager == null || entityManager.Registry == null) return;

            conditionDeltaBuffer.Clear();
            service.OnTurnEnded(e.actor, entityManager.Registry, conditionDeltaBuffer);
            PublishConditionDeltas();
        }

        private void OnCombatEnded(in CombatEndedEvent e)
        {
            if (entityManager == null || entityManager.Registry == null) return;

            conditionDeltaBuffer.Clear();
            service.ClearAll(entityManager.Registry, conditionDeltaBuffer);
            PublishConditionDeltas();
        }

        private void OnEntityMoved(in EntityMovedEvent e)
        {
            if (entityManager == null || entityManager.Registry == null) return;

            conditionDeltaBuffer.Clear();
            service.OnEntityMoved(e.entity, entityManager.Registry, conditionDeltaBuffer);
            PublishConditionDeltas();
        }

        private void PublishConditionDeltas()
        {
            if (eventBus == null || conditionDeltaBuffer.Count == 0) return;

            for (int i = 0; i < conditionDeltaBuffer.Count; i++)
            {
                var delta = conditionDeltaBuffer[i];
                eventBus.PublishConditionChanged(
                    delta.entity,
                    delta.type,
                    delta.changeType,
                    delta.oldValue,
                    delta.newValue,
                    delta.oldRemainingRounds,
                    delta.newRemainingRounds);
            }
        }
    }
}
