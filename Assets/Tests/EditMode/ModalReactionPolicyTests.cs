using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class ModalReactionPolicyTests
    {
        private static ReactionOption MakeShieldBlockOption(EntityHandle entity)
        {
            return new ReactionOption(entity, ReactionType.ShieldBlock, ReactionTriggerPhase.PostHit);
        }

        private static EntityData MakeReactor(Team team, ReactionPreference pref)
        {
            var shieldDef = ScriptableObject.CreateInstance<ShieldDefinition>();
            shieldDef.itemName = "TestShield";
            shieldDef.acBonus = 2;
            shieldDef.hardness = 5;
            shieldDef.maxHP = 20;

            var shield = ShieldInstance.CreateEquipped(shieldDef);
            shield.isRaised = true;

            return new EntityData
            {
                Team = team,
                Size = CreatureSize.Medium,
                MaxHP = 20,
                CurrentHP = 20,
                ReactionAvailable = true,
                ShieldBlockPreference = pref,
                EquippedShield = shield
            };
        }

        [Test]
        public void NonPlayerReactor_ReturnsTrueSynchronously()
        {
            var policy = new ModalReactionPolicy(null);
            var reactor = MakeReactor(Team.Enemy, ReactionPreference.AutoBlock);
            var option = MakeShieldBlockOption(new EntityHandle(1));

            bool? decided = null;
            policy.DecideReaction(option, reactor, 10, v => decided = v);

            Assert.IsTrue(decided.HasValue, "Callback should fire synchronously.");
            Assert.IsTrue(decided.Value);

            // Cleanup SO
            if (reactor.EquippedShield.def != null)
                Object.DestroyImmediate(reactor.EquippedShield.def);
        }

        [Test]
        public void PlayerAutoBlock_ReturnsTrueSynchronously()
        {
            var policy = new ModalReactionPolicy(null);
            var reactor = MakeReactor(Team.Player, ReactionPreference.AutoBlock);
            var option = MakeShieldBlockOption(new EntityHandle(1));

            bool? decided = null;
            policy.DecideReaction(option, reactor, 10, v => decided = v);

            Assert.IsTrue(decided.HasValue);
            Assert.IsTrue(decided.Value);

            if (reactor.EquippedShield.def != null)
                Object.DestroyImmediate(reactor.EquippedShield.def);
        }

        [Test]
        public void PlayerNever_ReturnsFalseSynchronously()
        {
            var policy = new ModalReactionPolicy(null);
            var reactor = MakeReactor(Team.Player, ReactionPreference.Never);
            var option = MakeShieldBlockOption(new EntityHandle(1));

            bool? decided = null;
            policy.DecideReaction(option, reactor, 10, v => decided = v);

            Assert.IsTrue(decided.HasValue);
            Assert.IsFalse(decided.Value);

            if (reactor.EquippedShield.def != null)
                Object.DestroyImmediate(reactor.EquippedShield.def);
        }

        [Test]
        public void PlayerAlwaysAsk_NoPromptController_ReturnsFalseWithWarning()
        {
            var policy = new ModalReactionPolicy(null);
            var reactor = MakeReactor(Team.Player, ReactionPreference.AlwaysAsk);
            var option = MakeShieldBlockOption(new EntityHandle(1));

            LogAssert.Expect(LogType.Warning, "[ModalReactionPolicy] ReactionPromptController is null. Auto-declining.");

            bool? decided = null;
            policy.DecideReaction(option, reactor, 10, v => decided = v);

            Assert.IsTrue(decided.HasValue);
            Assert.IsFalse(decided.Value);

            if (reactor.EquippedShield.def != null)
                Object.DestroyImmediate(reactor.EquippedShield.def);
        }

        [Test]
        public void NullReactor_ReturnsFalseSynchronously()
        {
            var policy = new ModalReactionPolicy(null);
            var option = MakeShieldBlockOption(new EntityHandle(1));

            bool? decided = null;
            policy.DecideReaction(option, null, 10, v => decided = v);

            Assert.IsTrue(decided.HasValue);
            Assert.IsFalse(decided.Value);
        }

        [Test]
        public void NonShieldBlockType_ReturnsFalseSynchronously()
        {
            var policy = new ModalReactionPolicy(null);
            var reactor = MakeReactor(Team.Player, ReactionPreference.AutoBlock);
            var option = new ReactionOption(new EntityHandle(1), ReactionType.None, ReactionTriggerPhase.PostHit);

            bool? decided = null;
            policy.DecideReaction(option, reactor, 10, v => decided = v);

            Assert.IsTrue(decided.HasValue);
            Assert.IsFalse(decided.Value);

            if (reactor.EquippedShield.def != null)
                Object.DestroyImmediate(reactor.EquippedShield.def);
        }
    }
}
