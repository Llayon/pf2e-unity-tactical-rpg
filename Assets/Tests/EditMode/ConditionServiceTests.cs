using System.Collections.Generic;
using NUnit.Framework;
using PF2e.Core;

namespace PF2e.Tests
{
    [TestFixture]
    public class ConditionServiceTests
    {
        [Test]
        public void Apply_NewCondition_EmitsAddedDelta_WithDuration()
        {
            var service = new ConditionService();
            var actor = CreateEntity();
            var deltas = new List<ConditionDelta>(4);

            service.Apply(actor, ConditionType.Slowed, value: 1, rounds: 3, deltas);

            Assert.AreEqual(1, deltas.Count);
            Assert.AreEqual(ConditionChangeType.Added, deltas[0].changeType);
            Assert.AreEqual(0, deltas[0].oldValue);
            Assert.AreEqual(1, deltas[0].newValue);
            Assert.AreEqual(0, deltas[0].oldRemainingRounds);
            Assert.AreEqual(3, deltas[0].newRemainingRounds);
            Assert.AreEqual(1, actor.GetConditionValue(ConditionType.Slowed));
            Assert.AreEqual(3, actor.Conditions[0].RemainingRounds);
        }

        [Test]
        public void Apply_HigherValueOnExisting_EmitsValueChanged()
        {
            var service = new ConditionService();
            var actor = CreateEntity();
            var deltas = new List<ConditionDelta>(4);

            service.Apply(actor, ConditionType.Frightened, value: 1, rounds: 2, deltas);
            deltas.Clear();

            service.Apply(actor, ConditionType.Frightened, value: 3, rounds: 1, deltas);

            Assert.AreEqual(1, deltas.Count);
            Assert.AreEqual(ConditionChangeType.ValueChanged, deltas[0].changeType);
            Assert.AreEqual(1, deltas[0].oldValue);
            Assert.AreEqual(3, deltas[0].newValue);
            Assert.AreEqual(2, deltas[0].oldRemainingRounds);
            Assert.AreEqual(2, deltas[0].newRemainingRounds, "Duration should keep the longer value.");
            Assert.AreEqual(3, actor.GetConditionValue(ConditionType.Frightened));
        }

        [Test]
        public void Apply_DurationRefreshOnly_EmitsDurationChanged()
        {
            var service = new ConditionService();
            var actor = CreateEntity();
            var deltas = new List<ConditionDelta>(4);

            service.Apply(actor, ConditionType.Slowed, value: 1, rounds: 2, deltas);
            deltas.Clear();

            service.Apply(actor, ConditionType.Slowed, value: 1, rounds: 4, deltas);

            Assert.AreEqual(1, deltas.Count);
            Assert.AreEqual(ConditionChangeType.DurationChanged, deltas[0].changeType);
            Assert.AreEqual(1, deltas[0].oldValue);
            Assert.AreEqual(1, deltas[0].newValue);
            Assert.AreEqual(2, deltas[0].oldRemainingRounds);
            Assert.AreEqual(4, deltas[0].newRemainingRounds);
            Assert.AreEqual(4, actor.Conditions[0].RemainingRounds);
        }

        [Test]
        public void Remove_Existing_EmitsRemovedDelta()
        {
            var service = new ConditionService();
            var actor = CreateEntity();
            var deltas = new List<ConditionDelta>(4);

            service.Apply(actor, ConditionType.Prone, value: 0, rounds: 2, deltas);
            deltas.Clear();

            service.Remove(actor, ConditionType.Prone, deltas);

            Assert.AreEqual(1, deltas.Count);
            Assert.AreEqual(ConditionType.Prone, deltas[0].type);
            Assert.AreEqual(ConditionChangeType.Removed, deltas[0].changeType);
            Assert.AreEqual(2, deltas[0].oldRemainingRounds);
            Assert.AreEqual(0, deltas[0].newRemainingRounds);
            Assert.IsFalse(actor.HasCondition(ConditionType.Prone));
        }

        [Test]
        public void TickStartTurn_Stunned_RemovesAndEmitsDelta()
        {
            var service = new ConditionService();
            var actor = CreateEntity();
            var deltas = new List<ConditionDelta>(4);

            service.Apply(actor, ConditionType.Stunned, value: 2, rounds: -1, deltas);
            deltas.Clear();

            service.TickStartTurn(actor, deltas);

            Assert.AreEqual(1, deltas.Count);
            Assert.AreEqual(ConditionType.Stunned, deltas[0].type);
            Assert.AreEqual(ConditionChangeType.Removed, deltas[0].changeType);
            Assert.AreEqual(-1, deltas[0].oldRemainingRounds);
            Assert.AreEqual(0, deltas[0].newRemainingRounds);
            Assert.IsFalse(actor.HasCondition(ConditionType.Stunned));
            Assert.AreEqual(1, actor.ActionsRemaining, "Stunned 2 should reduce actions from 3 to 1 before removal.");
        }

        [Test]
        public void TickEndTurn_Frightened_DecrementsAndEmitsDelta()
        {
            var service = new ConditionService();
            var actor = CreateEntity();
            var deltas = new List<ConditionDelta>(4);

            service.Apply(actor, ConditionType.Frightened, value: 2, rounds: -1, deltas);
            deltas.Clear();

            service.TickEndTurn(actor, deltas);

            Assert.AreEqual(1, deltas.Count);
            Assert.AreEqual(ConditionType.Frightened, deltas[0].type);
            Assert.AreEqual(ConditionChangeType.ValueChanged, deltas[0].changeType);
            Assert.AreEqual(2, deltas[0].oldValue);
            Assert.AreEqual(1, deltas[0].newValue);
            Assert.AreEqual(-1, deltas[0].oldRemainingRounds);
            Assert.AreEqual(-1, deltas[0].newRemainingRounds);
            Assert.AreEqual(1, actor.GetConditionValue(ConditionType.Frightened));
        }

        [Test]
        public void TickEndTurn_DurationOnly_EmitsDurationChanged()
        {
            var service = new ConditionService();
            var actor = CreateEntity();
            var deltas = new List<ConditionDelta>(4);

            service.Apply(actor, ConditionType.Slowed, value: 1, rounds: 3, deltas);
            deltas.Clear();

            service.TickEndTurn(actor, deltas);

            Assert.AreEqual(1, deltas.Count);
            Assert.AreEqual(ConditionType.Slowed, deltas[0].type);
            Assert.AreEqual(ConditionChangeType.DurationChanged, deltas[0].changeType);
            Assert.AreEqual(1, deltas[0].oldValue);
            Assert.AreEqual(1, deltas[0].newValue);
            Assert.AreEqual(3, deltas[0].oldRemainingRounds);
            Assert.AreEqual(2, deltas[0].newRemainingRounds);
        }

        [Test]
        public void TickEndTurn_ValueAndDuration_BothDecrement()
        {
            var service = new ConditionService();
            var actor = CreateEntity();
            var deltas = new List<ConditionDelta>(4);

            service.Apply(actor, ConditionType.Frightened, value: 2, rounds: 2, deltas);
            deltas.Clear();

            service.TickEndTurn(actor, deltas);

            Assert.AreEqual(1, deltas.Count);
            Assert.AreEqual(ConditionChangeType.ValueChanged, deltas[0].changeType);
            Assert.AreEqual(2, deltas[0].oldValue);
            Assert.AreEqual(1, deltas[0].newValue);
            Assert.AreEqual(2, deltas[0].oldRemainingRounds);
            Assert.AreEqual(1, deltas[0].newRemainingRounds);
        }

        [Test]
        public void TickEndTurn_RemovesWhenDurationReachesZero()
        {
            var service = new ConditionService();
            var actor = CreateEntity();
            var deltas = new List<ConditionDelta>(4);

            service.Apply(actor, ConditionType.Slowed, value: 1, rounds: 1, deltas);
            deltas.Clear();

            service.TickEndTurn(actor, deltas);

            Assert.AreEqual(1, deltas.Count);
            Assert.AreEqual(ConditionChangeType.Removed, deltas[0].changeType);
            Assert.AreEqual(1, deltas[0].oldValue);
            Assert.AreEqual(0, deltas[0].newValue);
            Assert.AreEqual(1, deltas[0].oldRemainingRounds);
            Assert.AreEqual(0, deltas[0].newRemainingRounds);
            Assert.IsFalse(actor.HasCondition(ConditionType.Slowed));
        }

        [Test]
        public void TickEndTurn_RemovesInfiniteValuedConditionWhenValueExhausted()
        {
            var service = new ConditionService();
            var actor = CreateEntity();
            var deltas = new List<ConditionDelta>(4);

            service.Apply(actor, ConditionType.Frightened, value: 1, rounds: -1, deltas);
            deltas.Clear();

            service.TickEndTurn(actor, deltas);

            Assert.AreEqual(1, deltas.Count);
            Assert.AreEqual(ConditionChangeType.Removed, deltas[0].changeType);
            Assert.AreEqual(1, deltas[0].oldValue);
            Assert.AreEqual(0, deltas[0].newValue);
            Assert.AreEqual(-1, deltas[0].oldRemainingRounds);
            Assert.AreEqual(0, deltas[0].newRemainingRounds);
            Assert.IsFalse(actor.HasCondition(ConditionType.Frightened));
        }

        private static EntityData CreateEntity()
        {
            return new EntityData
            {
                Handle = new EntityHandle(777),
                Name = "ConditionTestActor",
                Team = Team.Player,
                MaxHP = 10,
                CurrentHP = 10,
                Strength = 10,
                Dexterity = 10,
                Constitution = 10,
                Intelligence = 10,
                Wisdom = 10,
                Charisma = 10
            };
        }
    }
}
