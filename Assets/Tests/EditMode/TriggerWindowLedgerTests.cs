using NUnit.Framework;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class TriggerWindowLedgerTests
    {
        [Test]
        public void Window_TracksConsumption_PerToken()
        {
            var ledger = new TriggerWindowLedger();
            var actor = new EntityHandle(10);
            var token = ledger.OpenWindow(TriggerWindowType.AttackStart);

            Assert.IsTrue(ledger.IsOpen(token));
            Assert.IsTrue(ledger.CanReact(token, actor));
            Assert.IsTrue(ledger.MarkReacted(token, actor));
            Assert.IsFalse(ledger.CanReact(token, actor));
            Assert.IsFalse(ledger.MarkReacted(token, actor));
        }

        [Test]
        public void NestedWindows_HaveIndependentConsumedSets()
        {
            var ledger = new TriggerWindowLedger();
            var actor = new EntityHandle(10);

            var outer = ledger.OpenWindow(TriggerWindowType.GenericIncomingDamage);
            Assert.IsTrue(ledger.MarkReacted(outer, actor));

            var inner = ledger.OpenWindow(TriggerWindowType.PostHitDamage);
            Assert.IsTrue(
                ledger.CanReact(inner, actor),
                "Nested window must not inherit consumed actors from outer window.");
            Assert.IsTrue(ledger.MarkReacted(inner, actor));

            ledger.CloseWindow(inner);

            Assert.IsFalse(
                ledger.CanReact(outer, actor),
                "Outer window consumed state must survive nested close.");
            ledger.CloseWindow(outer);
        }

        [Test]
        public void CloseWindow_RemovesOnlyThatWindow()
        {
            var ledger = new TriggerWindowLedger();
            var outer = ledger.OpenWindow(TriggerWindowType.MovementEnter);
            var inner = ledger.OpenWindow(TriggerWindowType.AttackStart);

            ledger.CloseWindow(outer);

            Assert.IsFalse(ledger.IsOpen(outer));
            Assert.IsTrue(ledger.IsOpen(inner));
            Assert.IsTrue(ledger.TryGetCurrentWindow(out var current));
            Assert.AreEqual(inner, current);
        }
    }
}
