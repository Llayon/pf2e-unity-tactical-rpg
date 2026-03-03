using NUnit.Framework;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class ReadyStrikeServiceTests
    {
        [Test]
        public void Prepare_Has_Remove_CountContract()
        {
            var service = new ReadyStrikeService();
            var actor = new EntityHandle(1);

            Assert.AreEqual(0, service.PreparedCount);
            Assert.IsFalse(service.HasPrepared(actor));

            Assert.IsTrue(service.TryPrepare(actor, preparedRound: 1));
            Assert.IsTrue(service.HasPrepared(actor));
            Assert.AreEqual(1, service.PreparedCount);

            Assert.IsTrue(service.TryRemovePrepared(actor));
            Assert.IsFalse(service.HasPrepared(actor));
            Assert.AreEqual(0, service.PreparedCount);
        }

        [Test]
        public void TriggerScope_ConsumesAtMostOncePerScope_AndResetsNextScope()
        {
            var service = new ReadyStrikeService();
            var actor = new EntityHandle(1);
            var actorData = new EntityData
            {
                MaxHP = 10,
                CurrentHP = 10,
                ReactionAvailable = true
            };

            bool CanUseReaction(EntityHandle handle) => handle == actor && actorData.ReactionAvailable;

            service.BeginTriggerScope();
            Assert.IsTrue(service.TryConsumeReactionInScope(actor, actorData, CanUseReaction));

            actorData.ReactionAvailable = true; // emulate external refill attempt in same scope
            Assert.IsFalse(service.TryConsumeReactionInScope(actor, actorData, CanUseReaction));
            service.EndTriggerScope();

            actorData.ReactionAvailable = true;
            service.BeginTriggerScope();
            Assert.IsTrue(service.TryConsumeReactionInScope(actor, actorData, CanUseReaction));
            service.EndTriggerScope();
        }
    }
}
