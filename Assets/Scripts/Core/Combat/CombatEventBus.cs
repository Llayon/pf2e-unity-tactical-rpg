using System;
using UnityEngine;

namespace PF2e.Core
{
    public enum CombatLogCategory : byte
    {
        System,
        CombatStart,
        CombatEnd,
        Turn,
        Movement,
        Attack,
        Spell,
        Condition,
        Debug
    }

    public readonly struct CombatLogEntry
    {
        public readonly EntityHandle Actor;
        public readonly CombatLogCategory Category;
        public readonly string Message;

        public CombatLogEntry(EntityHandle actor, string message, CombatLogCategory category = CombatLogCategory.Debug)
        {
            Actor = actor;
            Message = message;
            Category = category;
        }
    }

    /// <summary>
    /// Scene-level event bus for combat log entries.
    /// Inspector-only wiring (no globals).
    /// Hybrid architecture: typed events (StrikeResolvedEvent) + string log entries.
    /// </summary>
    public class CombatEventBus : MonoBehaviour
    {
        public delegate void StrikeResolvedHandler(in StrikeResolvedEvent e);

        public event Action<CombatLogEntry> OnLogEntry;
        public event StrikeResolvedHandler OnStrikeResolved;

        public void Publish(CombatLogEntry entry)
        {
            OnLogEntry?.Invoke(entry);
        }

        /// <summary>
        /// IMPORTANT CONTRACT:
        /// Message must NOT include actor name; the log consumer adds it.
        /// Good: "strides x2 → (3,0,5)"
        /// Bad:  "Fighter strides x2 → (3,0,5)"
        /// </summary>
        public void Publish(EntityHandle actor, string message, CombatLogCategory category = CombatLogCategory.Debug)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Dev-guard: warn if publishers violate contract
            if (!string.IsNullOrEmpty(message))
            {
                if (message.Contains(": "))
                    Debug.LogWarning($"[CombatEventBus] Message contains ': '. Do NOT include actor name prefix in message. Message='{message}'");

                string actorStr = actor.ToString(); // "Entity#ID"
                if (!string.IsNullOrEmpty(actorStr) && message.StartsWith(actorStr))
                    Debug.LogWarning($"[CombatEventBus] Message starts with actor handle '{actorStr}'. Do NOT include actor prefix. Message='{message}'");
            }
#endif

            OnLogEntry?.Invoke(new CombatLogEntry(actor, message, category));
        }

        public void PublishSystem(string message)
        {
            PublishSystem(message, CombatLogCategory.System);
        }

        public void PublishSystem(string message, CombatLogCategory category)
        {
            OnLogEntry?.Invoke(new CombatLogEntry(EntityHandle.None, message, category));
        }

        /// <summary>
        /// Publish typed strike event. Forwarder converts to string log.
        /// </summary>
        public void PublishStrikeResolved(in StrikeResolvedEvent ev)
        {
            OnStrikeResolved?.Invoke(in ev);
        }

        #region Typed: Turn
        public delegate void CombatStartedHandler(in CombatStartedEvent e);
        public delegate void CombatEndedHandler(in CombatEndedEvent e);
        public delegate void RoundStartedHandler(in RoundStartedEvent e);
        public delegate void TurnStartedHandler(in TurnStartedEvent e);
        public delegate void TurnEndedHandler(in TurnEndedEvent e);
        public delegate void ActionsChangedHandler(in ActionsChangedEvent e);

        public event CombatStartedHandler OnCombatStartedTyped;
        public event CombatEndedHandler OnCombatEndedTyped;
        public event RoundStartedHandler OnRoundStartedTyped;
        public event TurnStartedHandler OnTurnStartedTyped;
        public event TurnEndedHandler OnTurnEndedTyped;
        public event ActionsChangedHandler OnActionsChangedTyped;

        public void PublishCombatStarted()
        {
            var e = default(CombatStartedEvent);
            OnCombatStartedTyped?.Invoke(in e);
        }

        public void PublishCombatEnded(EncounterResult result = EncounterResult.Aborted)
        {
            var e = new CombatEndedEvent(result);
            OnCombatEndedTyped?.Invoke(in e);
        }

        public void PublishRoundStarted(int round)
        {
            var e = new RoundStartedEvent(round);
            OnRoundStartedTyped?.Invoke(in e);
        }

        public void PublishTurnStarted(EntityHandle actor, int actionsAtStart)
        {
            var e = new TurnStartedEvent(actor, actionsAtStart);
            OnTurnStartedTyped?.Invoke(in e);
        }

        public void PublishTurnEnded(EntityHandle actor)
        {
            var e = new TurnEndedEvent(actor);
            OnTurnEndedTyped?.Invoke(in e);
        }

        public void PublishActionsChanged(EntityHandle actor, int remaining)
        {
            var e = new ActionsChangedEvent(actor, remaining);
            OnActionsChangedTyped?.Invoke(in e);
        }
        #endregion

        #region Typed: Stride
        public delegate void StrideStartedHandler(in StrideStartedEvent e);
        public delegate void StrideCompletedHandler(in StrideCompletedEvent e);

        public event StrideStartedHandler OnStrideStartedTyped;
        public event StrideCompletedHandler OnStrideCompletedTyped;

        public void PublishStrideStarted(EntityHandle actor, Vector3Int from, Vector3Int to, int actionsCost)
        {
            var e = new StrideStartedEvent(actor, from, to, actionsCost);
            OnStrideStartedTyped?.Invoke(in e);
        }

        public void PublishStrideCompleted(EntityHandle actor, Vector3Int to, int actionsCost)
        {
            var e = new StrideCompletedEvent(actor, to, actionsCost);
            OnStrideCompletedTyped?.Invoke(in e);
        }
        
        #region Typed: Condition

        public delegate void ConditionChangedHandler(in ConditionChangedEvent e);
        public event ConditionChangedHandler OnConditionChangedTyped;

        public void PublishConditionChanged(
            EntityHandle entity, ConditionType type,
            ConditionChangeType changeType, int oldValue, int newValue)
        {
            var e = new ConditionChangedEvent(entity, type, changeType, oldValue, newValue);
            OnConditionChangedTyped?.Invoke(in e);
        }

        #endregion

        #region Typed: Entity lifecycle

        public delegate void EntityDefeatedHandler(in EntityDefeatedEvent e);
        public event EntityDefeatedHandler OnEntityDefeated;

        public void PublishEntityDefeated(EntityHandle handle)
        {
            var e = new EntityDefeatedEvent(handle);
            OnEntityDefeated?.Invoke(in e);
        }

        #endregion
#endregion
    }
}
