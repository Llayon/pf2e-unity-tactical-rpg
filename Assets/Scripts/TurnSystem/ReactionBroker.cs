using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Shared post-hit reaction arbitration for strike damage reduction (Shield Block in MVP).
    /// Keeps Player/AI execution paths behavior-consistent.
    /// </summary>
    public static class ReactionBroker
    {
        public static int ResolvePostHitReductionSync(
            StrikePhaseResult resolved,
            IReadOnlyList<InitiativeEntry> initiativeOrder,
            Func<EntityHandle, EntityData> getEntity,
            Func<EntityHandle, bool> canUseReaction,
            IReactionDecisionPolicy reactionPolicy,
            ShieldBlockAction shieldBlockAction,
            List<ReactionOption> reactionBuffer,
            string ownerTag)
        {
            if (!resolved.damageDealt || resolved.damageRolled <= 0)
                return 0;
            if (reactionPolicy == null)
                return 0;

            if (!TrySelectShieldBlockCandidate(
                    resolved,
                    initiativeOrder,
                    getEntity,
                    canUseReaction,
                    reactionBuffer,
                    out var option,
                    out var reactorData))
            {
                return 0;
            }

            bool? decided = null;
            reactionPolicy.DecideReaction(
                option,
                reactorData,
                resolved.damageRolled,
                result => decided = result);

            if (!decided.HasValue)
            {
                Debug.LogWarning("[Reaction] DecideReaction did not invoke callback synchronously. Treating as decline.");
                return 0;
            }

            if (!decided.Value)
                return 0;

            return ExecuteShieldBlock(option.entity, reactorData, resolved.damageRolled, shieldBlockAction, ownerTag);
        }

        public static IEnumerator ResolvePostHitReductionAsync(
            StrikePhaseResult resolved,
            IReadOnlyList<InitiativeEntry> initiativeOrder,
            Func<EntityHandle, EntityData> getEntity,
            Func<EntityHandle, bool> canUseReaction,
            IReactionDecisionPolicy reactionPolicy,
            ShieldBlockAction shieldBlockAction,
            List<ReactionOption> reactionBuffer,
            Func<bool> shouldAbortWaiting,
            float timeoutSeconds,
            Action forceClosePrompt,
            Action<int> setResult,
            string ownerTag)
        {
            if (!resolved.damageDealt || resolved.damageRolled <= 0)
            {
                setResult?.Invoke(0);
                yield break;
            }

            if (reactionPolicy == null)
            {
                setResult?.Invoke(0);
                yield break;
            }

            if (!TrySelectShieldBlockCandidate(
                    resolved,
                    initiativeOrder,
                    getEntity,
                    canUseReaction,
                    reactionBuffer,
                    out var option,
                    out var reactorData))
            {
                setResult?.Invoke(0);
                yield break;
            }

            bool? decided = null;
            reactionPolicy.DecideReaction(
                option,
                reactorData,
                resolved.damageRolled,
                result => decided = result);

            if (!decided.HasValue)
            {
                float startedAt = Time.time;
                while (!decided.HasValue)
                {
                    if (shouldAbortWaiting != null && shouldAbortWaiting())
                    {
                        forceClosePrompt?.Invoke();
                        setResult?.Invoke(0);
                        yield break;
                    }

                    if (Time.time - startedAt > timeoutSeconds)
                    {
                        Debug.LogWarning($"[{ownerTag}] Reaction decision timeout. Auto-declining.");
                        forceClosePrompt?.Invoke();
                        decided = false;
                        break;
                    }

                    yield return null;
                }
            }

            if (decided != true)
            {
                setResult?.Invoke(0);
                yield break;
            }

            int reduction = ExecuteShieldBlock(option.entity, reactorData, resolved.damageRolled, shieldBlockAction, ownerTag);
            setResult?.Invoke(reduction);
        }

        private static bool TrySelectShieldBlockCandidate(
            StrikePhaseResult resolved,
            IReadOnlyList<InitiativeEntry> initiativeOrder,
            Func<EntityHandle, EntityData> getEntity,
            Func<EntityHandle, bool> canUseReaction,
            List<ReactionOption> reactionBuffer,
            out ReactionOption option,
            out EntityData reactorData)
        {
            option = default;
            reactorData = null;

            if (initiativeOrder == null || getEntity == null || canUseReaction == null || reactionBuffer == null)
                return false;

            reactionBuffer.Clear();
            ReactionService.CollectEligibleReactions(
                ReactionTriggerPhase.PostHit,
                resolved.attacker,
                resolved.target,
                initiativeOrder,
                getEntity,
                reactionBuffer);

            if (reactionBuffer.Count <= 0)
                return false;

            option = reactionBuffer[0];
            reactorData = getEntity(option.entity);
            if (reactorData == null || !reactorData.IsAlive)
                return false;
            if (!canUseReaction(option.entity))
                return false;

            return true;
        }

        private static int ExecuteShieldBlock(
            EntityHandle reactor,
            EntityData reactorData,
            int incomingDamage,
            ShieldBlockAction shieldBlockAction,
            string ownerTag)
        {
            var result = ShieldBlockRules.Calculate(reactorData.EquippedShield, incomingDamage);
            if (shieldBlockAction == null)
            {
                Debug.LogWarning($"[{ownerTag}] ShieldBlockAction is missing. Skipping Shield Block reaction.");
                return 0;
            }

            return shieldBlockAction.Execute(reactor, incomingDamage, in result)
                ? result.targetDamageReduction
                : 0;
        }
    }
}
