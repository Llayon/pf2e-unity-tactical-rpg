using System;
using UnityEngine;
using PF2e.Core;
using PF2e.Presentation;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Reaction decision policy that routes based on reactor team and preference.
    /// - Non-Player reactor: auto-block (true) synchronously.
    /// - Player + AutoBlock: true synchronously.
    /// - Player + Never: false synchronously.
    /// - Player + AlwaysAsk: async via ReactionPromptController.
    /// </summary>
    public sealed class ModalReactionPolicy : IReactionDecisionPolicy
    {
        private readonly ReactionPromptController promptController;

        public ModalReactionPolicy(ReactionPromptController promptController)
        {
            this.promptController = promptController;
        }

        public void DecideReaction(ReactionOption option, EntityData reactor, int incomingDamage, Action<bool> onDecided)
        {
            if (onDecided == null) return;

            // Guard: invalid option or reactor.
            if (reactor == null || option.type != ReactionType.ShieldBlock)
            {
                onDecided(false);
                return;
            }

            // Non-player reactor: AI always auto-blocks.
            if (reactor.Team != Team.Player)
            {
                onDecided(true);
                return;
            }

            // Player reactor: route by preference.
            switch (reactor.ShieldBlockPreference)
            {
                case ReactionPreference.AutoBlock:
                    onDecided(true);
                    return;

                case ReactionPreference.Never:
                    onDecided(false);
                    return;

                case ReactionPreference.AlwaysAsk:
                    if (promptController == null)
                    {
                        Debug.LogWarning("[ModalReactionPolicy] ReactionPromptController is null. Auto-declining.");
                        onDecided(false);
                        return;
                    }

                    var shield = reactor.EquippedShield;
                    promptController.RequestShieldBlockPrompt(
                        option.entity,
                        incomingDamage,
                        shield.currentHP,
                        shield.MaxHP,
                        onDecided);
                    return;

                default:
                    Debug.LogWarning($"[ModalReactionPolicy] Unknown ReactionPreference {reactor.ShieldBlockPreference}. Auto-declining.");
                    onDecided(false);
                    return;
            }
        }
    }
}
