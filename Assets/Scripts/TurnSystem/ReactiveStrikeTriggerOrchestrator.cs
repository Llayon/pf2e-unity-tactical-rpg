using System;
using System.Collections.Generic;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Event-driven candidate collection for passive Reactive Strike triggers.
    /// </summary>
    public sealed class ReactiveStrikeTriggerOrchestrator
    {
        private readonly List<EntityHandle> triggerBuffer = new();

        public void ClearTransientState()
        {
            triggerBuffer.Clear();
        }

        public void HandleEntityMoved(
            in EntityMovedEvent e,
            IReadOnlyList<InitiativeEntry> initiativeOrder,
            EntityManager entityManager,
            StrikeAction strikeAction,
            Func<EntityHandle, bool> canUseReaction,
            Action<EntityHandle, EntityHandle, string, TriggerWindowToken> resolveTrigger,
            TriggerWindowToken triggerWindowToken)
        {
            if (!e.entity.IsValid)
                return;
            if (initiativeOrder == null || entityManager == null || entityManager.Registry == null || strikeAction == null)
                return;
            if (canUseReaction == null || resolveTrigger == null || !triggerWindowToken.IsValid)
                return;

            var movedEntity = e.entity;
            var movedData = entityManager.Registry.Get(movedEntity);
            if (movedData == null || !movedData.IsAlive)
                return;

            triggerBuffer.Clear();

            for (int i = 0; i < initiativeOrder.Count; i++)
            {
                var actor = initiativeOrder[i].Handle;
                if (!actor.IsValid)
                    continue;

                var actorData = entityManager.Registry.Get(actor);
                if (actorData == null || !actorData.IsAlive || !actorData.HasReactiveStrike)
                    continue;
                if (!canUseReaction(actor))
                    continue;
                if (actorData.Team == movedData.Team)
                    continue;
                if (!ReactiveStrikeTriggerPolicy.CanTriggerOnMovement(actorData, movedData, in e))
                    continue;
                if (strikeAction.GetStrikeTargetFailure(actor, movedEntity) != TargetingFailureReason.None)
                    continue;

                triggerBuffer.Add(actor);
            }

            for (int i = 0; i < triggerBuffer.Count; i++)
                resolveTrigger(triggerBuffer[i], movedEntity, "movement", triggerWindowToken);

            triggerBuffer.Clear();
        }

        public void HandleStrikePreDamage(
            in StrikePreDamageEvent e,
            IReadOnlyList<InitiativeEntry> initiativeOrder,
            EntityManager entityManager,
            StrikeAction strikeAction,
            Func<EntityHandle, bool> canUseReaction,
            Action<EntityHandle, EntityHandle, string, TriggerWindowToken> resolveTrigger,
            TriggerWindowToken triggerWindowToken)
        {
            if (!e.attacker.IsValid || !e.target.IsValid)
                return;
            if (initiativeOrder == null || entityManager == null || entityManager.Registry == null || strikeAction == null)
                return;
            if (canUseReaction == null || resolveTrigger == null || !triggerWindowToken.IsValid)
                return;

            var attackSource = e.attacker;
            var attackSourceData = entityManager.Registry.Get(attackSource);
            if (attackSourceData == null || !attackSourceData.IsAlive || !attackSourceData.EquippedWeapon.IsRanged)
                return;

            triggerBuffer.Clear();

            for (int i = 0; i < initiativeOrder.Count; i++)
            {
                var actor = initiativeOrder[i].Handle;
                if (!actor.IsValid)
                    continue;

                var actorData = entityManager.Registry.Get(actor);
                if (actorData == null || !actorData.IsAlive || !actorData.HasReactiveStrike)
                    continue;
                if (!canUseReaction(actor))
                    continue;
                if (actorData.Team == attackSourceData.Team)
                    continue;
                if (!ReactiveStrikeTriggerPolicy.CanTriggerOnRangedAttack(actorData, attackSourceData))
                    continue;
                if (strikeAction.GetStrikeTargetFailure(actor, attackSource) != TargetingFailureReason.None)
                    continue;

                triggerBuffer.Add(actor);
            }

            for (int i = 0; i < triggerBuffer.Count; i++)
                resolveTrigger(triggerBuffer[i], attackSource, "ranged attack", triggerWindowToken);

            triggerBuffer.Clear();
        }

        public bool HandleCombatActionStarted(
            in CombatActionStartedEvent e,
            IReadOnlyList<InitiativeEntry> initiativeOrder,
            EntityManager entityManager,
            StrikeAction strikeAction,
            Func<EntityHandle, bool> canUseReaction,
            Func<EntityHandle, EntityHandle, string, TriggerWindowToken, bool> resolveTrigger,
            TriggerWindowToken triggerWindowToken)
        {
            if (!e.actor.IsValid)
                return false;
            if (initiativeOrder == null || entityManager == null || entityManager.Registry == null || strikeAction == null)
                return false;
            if (canUseReaction == null || resolveTrigger == null || !triggerWindowToken.IsValid)
                return false;

            var actionSource = e.actor;
            var actionSourceData = entityManager.Registry.Get(actionSource);
            if (actionSourceData == null || !actionSourceData.IsAlive)
                return false;

            triggerBuffer.Clear();

            for (int i = 0; i < initiativeOrder.Count; i++)
            {
                var actor = initiativeOrder[i].Handle;
                if (!actor.IsValid)
                    continue;

                var actorData = entityManager.Registry.Get(actor);
                if (actorData == null || !actorData.IsAlive || !actorData.HasReactiveStrike)
                    continue;
                if (!canUseReaction(actor))
                    continue;
                if (actorData.Team == actionSourceData.Team)
                    continue;
                if (!ReactiveStrikeTriggerPolicy.CanTriggerOnActionStart(actorData, actionSourceData, in e))
                    continue;
                if (strikeAction.GetStrikeTargetFailure(actor, actionSource) != TargetingFailureReason.None)
                    continue;

                triggerBuffer.Add(actor);
            }

            string triggerReason = e.actionKind == CombatActionKind.Stand
                ? "standing"
                : $"casting {e.actionName}";

            bool interrupted = false;
            for (int i = 0; i < triggerBuffer.Count; i++)
            {
                if (resolveTrigger(triggerBuffer[i], actionSource, triggerReason, triggerWindowToken))
                {
                    interrupted = true;
                    break;
                }

                actionSourceData = entityManager.Registry.Get(actionSource);
                if (actionSourceData == null || !actionSourceData.IsAlive)
                    break;
            }

            triggerBuffer.Clear();
            return interrupted;
        }
    }
}
