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
        public delegate void StrikePreDamageHandler(in StrikePreDamageEvent e);
        public delegate void StrikeResolvedHandler(in StrikeResolvedEvent e);
        public delegate void DamageAppliedHandler(in DamageAppliedEvent e);
        public delegate void SkillCheckResolvedHandler(in SkillCheckResolvedEvent e);
        public delegate void ShieldRaisedHandler(in ShieldRaisedEvent e);
        public delegate void ShieldBlockResolvedHandler(in ShieldBlockResolvedEvent e);

        public event Action<CombatLogEntry> OnLogEntry;
        public event StrikePreDamageHandler OnStrikePreDamageTyped;
        public event StrikeResolvedHandler OnStrikeResolved;
        public event DamageAppliedHandler OnDamageAppliedTyped;
        public event SkillCheckResolvedHandler OnSkillCheckResolvedTyped;
        public event ShieldRaisedHandler OnShieldRaisedTyped;
        public event ShieldBlockResolvedHandler OnShieldBlockResolvedTyped;

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

        public void PublishDamageApplied(in DamageAppliedEvent ev)
        {
            OnDamageAppliedTyped?.Invoke(in ev);
        }

        public void PublishSkillCheckResolved(in SkillCheckResolvedEvent ev)
        {
            OnSkillCheckResolvedTyped?.Invoke(in ev);
        }

        public void PublishStrikePreDamage(
            EntityHandle attacker,
            EntityHandle target,
            int naturalRoll,
            int total,
            int dc,
            DegreeOfSuccess degree,
            int damageRolled,
            DamageType damageType)
        {
            var e = new StrikePreDamageEvent(
                attacker,
                target,
                naturalRoll,
                total,
                dc,
                degree,
                damageRolled,
                damageType);

            OnStrikePreDamageTyped?.Invoke(in e);
        }

        public void PublishShieldRaised(EntityHandle actor, int acBonus, int shieldHP, int shieldMaxHP)
        {
            var e = new ShieldRaisedEvent(actor, acBonus, shieldHP, shieldMaxHP);
            OnShieldRaisedTyped?.Invoke(in e);
        }

        public void PublishShieldBlockResolved(
            EntityHandle reactor,
            int incomingDamage,
            int damageReduction,
            int shieldSelfDamage,
            int shieldHpBefore,
            int shieldHpAfter)
        {
            var e = new ShieldBlockResolvedEvent(
                reactor,
                incomingDamage,
                damageReduction,
                shieldSelfDamage,
                shieldHpBefore,
                shieldHpAfter);

            OnShieldBlockResolvedTyped?.Invoke(in e);
        }

        #region Typed: Turn
        public delegate void CombatStartedHandler(in CombatStartedEvent e);
        public delegate void CombatEndedHandler(in CombatEndedEvent e);
        public delegate void RoundStartedHandler(in RoundStartedEvent e);
        public delegate void TurnStartedHandler(in TurnStartedEvent e);
        public delegate void TurnEndedHandler(in TurnEndedEvent e);
        public delegate void ActionsChangedHandler(in ActionsChangedEvent e);
        public delegate void ConditionsTickedHandler(in ConditionsTickedEvent e);
        public delegate void InitiativeRolledHandler(in InitiativeRolledEvent e);

        public event CombatStartedHandler OnCombatStartedTyped;
        public event CombatEndedHandler OnCombatEndedTyped;
        public event RoundStartedHandler OnRoundStartedTyped;
        public event TurnStartedHandler OnTurnStartedTyped;
        public event TurnEndedHandler OnTurnEndedTyped;
        public event ActionsChangedHandler OnActionsChangedTyped;
        public event ConditionsTickedHandler OnConditionsTickedTyped;
        public event InitiativeRolledHandler OnInitiativeRolledTyped;

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

        public void PublishConditionsTicked(EntityHandle actor, System.Collections.Generic.IReadOnlyList<ConditionDelta> ticks)
        {
            var e = new ConditionsTickedEvent(actor, ticks);
            OnConditionsTickedTyped?.Invoke(in e);
        }

        public void PublishInitiativeRolled(System.Collections.Generic.IReadOnlyList<PF2e.TurnSystem.InitiativeEntry> order)
        {
            var e = new InitiativeRolledEvent(order);
            OnInitiativeRolledTyped?.Invoke(in e);
        }
        #endregion

        #region Typed: Stride
                public delegate void StrideStartedHandler(in StrideStartedEvent e);
        public delegate void StrideCompletedHandler(in StrideCompletedEvent e);
        public delegate void EntityMovedHandler(in EntityMovedEvent e);

                public event StrideStartedHandler OnStrideStartedTyped;
        public event StrideCompletedHandler OnStrideCompletedTyped;
        public event EntityMovedHandler OnEntityMovedTyped;

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
        public void PublishEntityMoved(EntityHandle entity, Vector3Int from, Vector3Int to, bool forced)
        {
            var e = new EntityMovedEvent(entity, from, to, forced);
            OnEntityMovedTyped?.Invoke(in e);
        }

        
        #region Typed: Condition

        public delegate void ConditionChangedHandler(in ConditionChangedEvent e);
        public event ConditionChangedHandler OnConditionChangedTyped;

        public void PublishConditionChanged(
            EntityHandle entity, ConditionType type,
            ConditionChangeType changeType, int oldValue, int newValue,
            int oldRemainingRounds = -1, int newRemainingRounds = -1)
        {
            var e = new ConditionChangedEvent(
                entity,
                type,
                changeType,
                oldValue,
                newValue,
                oldRemainingRounds,
                newRemainingRounds);
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
