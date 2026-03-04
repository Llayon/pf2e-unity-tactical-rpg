using PF2e.Core;
using PF2e.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Applies direct damage outside the strike pipeline (Trip crit damage, future spells/hazards).
    /// Plain static helper: callers pass scene dependencies explicitly.
    /// </summary>
    public static class DamageApplicationService
    {
        /// <summary>
        /// Applies damage to target HP, clamps to 0, handles death, and publishes DamageAppliedEvent.
        /// Returns final applied damage (0 if invalid target or amount <= 0).
        /// </summary>
        public static int ApplyDamage(
            EntityHandle source,
            EntityHandle target,
            int amount,
            DamageType damageType,
            string sourceActionName,
            bool isCritical,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IReadOnlyList<InitiativeEntry> initiativeOrder = null,
            Func<EntityHandle, EntityData> getEntity = null,
            Func<EntityHandle, bool> canUseReaction = null,
            IReactionDecisionPolicy reactionPolicy = null,
            ShieldBlockAction shieldBlockAction = null,
            List<ReactionOption> reactionBuffer = null,
            ReactionTriggerPhase reactionPhase = ReactionTriggerPhase.PostHit,
            string reactionOwnerTag = "DamageApplicationService")
        {
            if (entityManager == null || entityManager.Registry == null) return 0;
            if (!target.IsValid) return 0;

            int finalDamage = Mathf.Max(0, amount);
            if (finalDamage <= 0) return 0;

            var targetData = entityManager.Registry.Get(target);
            if (targetData == null || !targetData.IsAlive) return 0;

            if (initiativeOrder != null
                && getEntity != null
                && canUseReaction != null
                && reactionPolicy != null
                && shieldBlockAction != null
                && reactionBuffer != null)
            {
                int damageReduction = ReactionBroker.ResolveIncomingDamageReductionSync(
                    triggerSource: source,
                    triggerTarget: target,
                    incomingDamage: finalDamage,
                    phase: reactionPhase,
                    initiativeOrder: initiativeOrder,
                    getEntity: getEntity,
                    canUseReaction: canUseReaction,
                    reactionPolicy: reactionPolicy,
                    shieldBlockAction: shieldBlockAction,
                    reactionBuffer: reactionBuffer,
                    ownerTag: reactionOwnerTag);
                finalDamage = Mathf.Max(0, finalDamage - Mathf.Max(0, damageReduction));
            }

            if (finalDamage <= 0) return 0;

            int hpBefore = Mathf.Max(0, targetData.CurrentHP);
            int hpAfter = Mathf.Max(0, hpBefore - finalDamage);
            int appliedDamage = hpBefore - hpAfter;
            if (appliedDamage <= 0) return 0;

            targetData.CurrentHP = hpAfter;

            bool targetDefeated = hpAfter <= 0;
            if (targetDefeated)
                entityManager.HandleDeath(target);

            if (eventBus != null)
            {
                var e = new DamageAppliedEvent(
                    source,
                    target,
                    appliedDamage,
                    damageType,
                    sourceActionName,
                    isCritical,
                    hpBefore,
                    hpAfter,
                    targetDefeated);
                eventBus.PublishDamageApplied(in e);
            }

            return appliedDamage;
        }
    }
}
