using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class ReactionBrokerTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ResolvePostHitReductionSync_WhenPolicyDoesNotCallback_ReturnsZeroAndWarns()
        {
            var shieldDef = CreateShieldDef();
            try
            {
                var attacker = new EntityHandle(1);
                var target = new EntityHandle(2);
                var phase = CreateDamagePhase(attacker, target);

                var entities = new Dictionary<EntityHandle, EntityData>
                {
                    [attacker] = CreateEntityWithShieldState(reactionAvailable: true, shieldDef, shieldRaised: false),
                    [target] = CreateEntityWithShieldState(reactionAvailable: true, shieldDef, shieldRaised: true)
                };

                var initiative = new List<InitiativeEntry>
                {
                    new InitiativeEntry { Handle = attacker, Roll = new CheckRoll(14, 0, CheckSource.Perception()), IsPlayer = true },
                    new InitiativeEntry { Handle = target, Roll = new CheckRoll(12, 0, CheckSource.Perception()), IsPlayer = false }
                };

                var buffer = new List<ReactionOption>(2);
                const string expectedWarning = "[Reaction] DecideReaction did not invoke callback synchronously. Treating as decline.";
                LogAssert.Expect(LogType.Warning, expectedWarning);

                int reduction = ReactionBroker.ResolvePostHitReductionSync(
                    phase,
                    initiative,
                    handle => entities.TryGetValue(handle, out var data) ? data : null,
                    handle => true,
                    new NeverCallbackPolicy(),
                    shieldBlockAction: null,
                    reactionBuffer: buffer,
                    ownerTag: "ReactionBrokerTest");

                Assert.AreEqual(0, reduction);
                Assert.IsTrue(entities[target].ReactionAvailable, "Declined/no-callback flow must not spend reaction.");
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        [Test]
        public void ResolvePostHitReductionAsync_WhenDecisionTimesOut_DeclinesAndClosesPrompt()
        {
            var shieldDef = CreateShieldDef();
            try
            {
                var attacker = new EntityHandle(1);
                var target = new EntityHandle(2);
                var phase = CreateDamagePhase(attacker, target);

                var entities = new Dictionary<EntityHandle, EntityData>
                {
                    [attacker] = CreateEntityWithShieldState(reactionAvailable: true, shieldDef, shieldRaised: false),
                    [target] = CreateEntityWithShieldState(reactionAvailable: true, shieldDef, shieldRaised: true)
                };

                var initiative = new List<InitiativeEntry>
                {
                    new InitiativeEntry { Handle = attacker, Roll = new CheckRoll(14, 0, CheckSource.Perception()), IsPlayer = true },
                    new InitiativeEntry { Handle = target, Roll = new CheckRoll(12, 0, CheckSource.Perception()), IsPlayer = false }
                };

                var buffer = new List<ReactionOption>(2);
                int forceCloseCount = 0;
                int? result = null;

                LogAssert.Expect(LogType.Warning, "[ReactionBrokerTest] Reaction decision timeout. Auto-declining.");

                var routine = ReactionBroker.ResolvePostHitReductionAsync(
                    phase,
                    initiative,
                    handle => entities.TryGetValue(handle, out var data) ? data : null,
                    handle => true,
                    new NeverCallbackPolicy(),
                    shieldBlockAction: null,
                    reactionBuffer: buffer,
                    shouldAbortWaiting: () => false,
                    timeoutSeconds: -1f,
                    forceClosePrompt: () => forceCloseCount++,
                    setResult: value => result = value,
                    ownerTag: "ReactionBrokerTest");

                DrainEnumerator(routine);

                Assert.AreEqual(0, result ?? int.MinValue);
                Assert.AreEqual(1, forceCloseCount);
                Assert.IsTrue(entities[target].ReactionAvailable, "Timed out decline must not spend reaction.");
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        [Test]
        public void ResolvePostHitReductionAsync_WhenAbortRequested_DeclinesAndClosesPromptWithoutTimeout()
        {
            var shieldDef = CreateShieldDef();
            try
            {
                var attacker = new EntityHandle(1);
                var target = new EntityHandle(2);
                var phase = CreateDamagePhase(attacker, target);

                var entities = new Dictionary<EntityHandle, EntityData>
                {
                    [attacker] = CreateEntityWithShieldState(reactionAvailable: true, shieldDef, shieldRaised: false),
                    [target] = CreateEntityWithShieldState(reactionAvailable: true, shieldDef, shieldRaised: true)
                };

                var initiative = new List<InitiativeEntry>
                {
                    new InitiativeEntry { Handle = attacker, Roll = new CheckRoll(14, 0, CheckSource.Perception()), IsPlayer = true },
                    new InitiativeEntry { Handle = target, Roll = new CheckRoll(12, 0, CheckSource.Perception()), IsPlayer = false }
                };

                var buffer = new List<ReactionOption>(2);
                int forceCloseCount = 0;
                int? result = null;

                var routine = ReactionBroker.ResolvePostHitReductionAsync(
                    phase,
                    initiative,
                    handle => entities.TryGetValue(handle, out var data) ? data : null,
                    handle => true,
                    new NeverCallbackPolicy(),
                    shieldBlockAction: null,
                    reactionBuffer: buffer,
                    shouldAbortWaiting: () => true,
                    timeoutSeconds: 30f,
                    forceClosePrompt: () => forceCloseCount++,
                    setResult: value => result = value,
                    ownerTag: "ReactionBrokerTest");

                DrainEnumerator(routine);

                Assert.AreEqual(0, result ?? int.MinValue);
                Assert.AreEqual(1, forceCloseCount);
                Assert.IsTrue(entities[target].ReactionAvailable, "Abort decline must not spend reaction.");
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        [Test]
        public void TryExecuteReadiedStrike_ValidTarget_PublishesTriggerLog_AndResolvesAttack()
        {
            using var ctx = new ReadyStrikeExecutionContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var target = ctx.RegisterEntity("Goblin", Team.Enemy, new Vector3Int(1, 0, 0), strength: 10);

            var logs = new List<CombatLogEntry>(4);
            void Capture(CombatLogEntry entry) => logs.Add(entry);
            ctx.EventBus.OnLogEntry += Capture;
            try
            {
                bool performed = ReactionBroker.TryExecuteReadiedStrike(
                    actor,
                    target,
                    triggerReason: "movement",
                    strikeAction: ctx.StrikeAction,
                    eventBus: ctx.EventBus,
                    getEntity: handle => ctx.Registry.Get(handle),
                    rng: new FixedRng(d20Rolls: new[] { 20 }, dieRolls: new[] { 4 }),
                    aidCircumstanceBonus: 0);

                Assert.IsTrue(performed, "Expected readied strike execution to complete.");
                Assert.IsTrue(logs.Exists(x => x.Actor == actor && x.Message.Contains("readied Strike triggers on Goblin movement.")));
                Assert.IsFalse(logs.Exists(x => x.Actor == actor && x.Message.Contains("readied Strike trigger resolves, but attack is no longer valid.")));
            }
            finally
            {
                ctx.EventBus.OnLogEntry -= Capture;
            }
        }

        [Test]
        public void TryExecuteReadiedStrike_InvalidTarget_PublishesInvalidLog_AndReturnsFalse()
        {
            using var ctx = new ReadyStrikeExecutionContext();

            var actor = ctx.RegisterEntity("Fighter", Team.Player, new Vector3Int(0, 0, 0), strength: 22);
            var ally = ctx.RegisterEntity("Wizard", Team.Player, new Vector3Int(1, 0, 0), strength: 10);
            var allyData = ctx.Registry.Get(ally);
            Assert.IsNotNull(allyData);
            int hpBefore = allyData.CurrentHP;

            var logs = new List<CombatLogEntry>(4);
            void Capture(CombatLogEntry entry) => logs.Add(entry);
            ctx.EventBus.OnLogEntry += Capture;
            try
            {
                bool performed = ReactionBroker.TryExecuteReadiedStrike(
                    actor,
                    ally,
                    triggerReason: "movement",
                    strikeAction: ctx.StrikeAction,
                    eventBus: ctx.EventBus,
                    getEntity: handle => ctx.Registry.Get(handle),
                    rng: new FixedRng(d20Rolls: new[] { 20 }, dieRolls: new[] { 4 }),
                    aidCircumstanceBonus: 0);

                Assert.IsFalse(performed, "Invalid readied strike target should fail.");
                Assert.AreEqual(hpBefore, allyData.CurrentHP, "Invalid target must not take damage.");
                Assert.IsTrue(logs.Exists(x => x.Actor == actor && x.Message.Contains("readied Strike triggers on Wizard movement.")));
                Assert.IsTrue(logs.Exists(x => x.Actor == actor && x.Message.Contains("readied Strike trigger resolves, but attack is no longer valid.")));
            }
            finally
            {
                ctx.EventBus.OnLogEntry -= Capture;
            }
        }

        [Test]
        public void CollectReadyTriggerCandidates_FiltersEligibleAndStaleActors()
        {
            var shieldDef = CreateShieldDef();
            try
            {
                var a = new EntityHandle(1);
                var b = new EntityHandle(2);
                var c = new EntityHandle(3);
                var d = new EntityHandle(4);

                var entities = new Dictionary<EntityHandle, EntityData>
                {
                    [a] = CreateEntityWithShieldState(reactionAvailable: true, shieldDef, shieldRaised: true),
                    [b] = CreateEntityWithShieldState(reactionAvailable: true, shieldDef, shieldRaised: true),
                    [c] = new EntityData { CurrentHP = 0, MaxHP = 10, ReactionAvailable = true }
                };

                var prepared = new List<EntityHandle> { a, b, c, d };
                var candidates = new List<EntityHandle>(4);
                var stale = new List<EntityHandle>(4);

                ReactionBroker.CollectReadyTriggerCandidates(
                    prepared,
                    handle => entities.TryGetValue(handle, out var data) ? data : null,
                    (actor, actorData) => actor == a || actor == b,
                    candidates,
                    stale);

                CollectionAssert.AreEqual(new[] { a, b }, candidates);
                CollectionAssert.AreEqual(new[] { c, d }, stale);
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        [Test]
        public void TryConsumeReadyReactionInScope_SpendsOncePerScope()
        {
            var shieldDef = CreateShieldDef();
            try
            {
                var actor = new EntityHandle(11);
                var actorData = CreateEntityWithShieldState(reactionAvailable: true, shieldDef, shieldRaised: true);
                var consumed = new HashSet<EntityHandle>();

                bool first = ReactionBroker.TryConsumeReadyReactionInScope(
                    actor,
                    actorData,
                    canUseReaction: _ => actorData.ReactionAvailable,
                    consumedInScope: consumed);
                bool second = ReactionBroker.TryConsumeReadyReactionInScope(
                    actor,
                    actorData,
                    canUseReaction: _ => actorData.ReactionAvailable,
                    consumedInScope: consumed);

                Assert.IsTrue(first);
                Assert.IsFalse(second);
                Assert.IsFalse(actorData.ReactionAvailable);
                Assert.IsTrue(consumed.Contains(actor));
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        private static void DrainEnumerator(IEnumerator routine, int maxSteps = 8)
        {
            Assert.IsNotNull(routine);
            int steps = 0;
            while (routine.MoveNext())
            {
                steps++;
                if (steps > maxSteps)
                    Assert.Fail("Enumerator did not complete within expected step budget.");
            }
        }

        private static StrikePhaseResult CreateDamagePhase(EntityHandle attacker, EntityHandle target)
        {
            return StrikePhaseResult.FromAttackRoll(
                    attacker,
                    target,
                    "Sword",
                    naturalRoll: 12,
                    attackBonus: 8,
                    mapPenalty: 0,
                    total: 20)
                .WithHitAndDamage(
                    dc: 15,
                    degree: DegreeOfSuccess.Success,
                    damageRolled: 7,
                    damageType: DamageType.Slashing,
                    damageDealt: true);
        }

        private static EntityData CreateEntityWithShieldState(
            bool reactionAvailable,
            ShieldDefinition shieldDef,
            bool shieldRaised)
        {
            var shield = ShieldInstance.CreateEquipped(shieldDef);
            shield.isRaised = shieldRaised;

            return new EntityData
            {
                Team = Team.Player,
                Size = CreatureSize.Medium,
                MaxHP = 10,
                CurrentHP = 10,
                ReactionAvailable = reactionAvailable,
                EquippedShield = shield
            };
        }

        private static ShieldDefinition CreateShieldDef()
        {
            var def = ScriptableObject.CreateInstance<ShieldDefinition>();
            def.itemName = "BrokerShield";
            def.acBonus = 2;
            def.hardness = 5;
            def.maxHP = 20;
            return def;
        }

        private sealed class NeverCallbackPolicy : IReactionDecisionPolicy
        {
            public void DecideReaction(ReactionOption option, EntityData reactor, int incomingDamage, System.Action<bool> onDecided)
            {
                _ = option;
                _ = reactor;
                _ = incomingDamage;
                _ = onDecided;
            }
        }

        private sealed class FixedRng : IRng
        {
            private readonly Queue<int> d20Rolls;
            private readonly Queue<int> dieRolls;

            public FixedRng(IEnumerable<int> d20Rolls = null, IEnumerable<int> dieRolls = null)
            {
                this.d20Rolls = new Queue<int>(d20Rolls ?? new[] { 10 });
                this.dieRolls = new Queue<int>(dieRolls ?? new[] { 1 });
            }

            public int RollD20()
            {
                return d20Rolls.Count > 0 ? d20Rolls.Dequeue() : 10;
            }

            public int RollDie(int sides)
            {
                _ = sides;
                return dieRolls.Count > 0 ? dieRolls.Dequeue() : 1;
            }
        }

        private sealed class ReadyStrikeExecutionContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public StrikeAction StrikeAction { get; }
            public EntityRegistry Registry { get; }

            public ReadyStrikeExecutionContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("ReactionBrokerTests_ReadyStrikeExecutionContext");

                var eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                var strikeActionGo = new GameObject("StrikeAction");
                strikeActionGo.transform.SetParent(root.transform);
                StrikeAction = strikeActionGo.AddComponent<StrikeAction>();
                SetPrivateField(StrikeAction, "entityManager", EntityManager);
                SetPrivateField(StrikeAction, "eventBus", EventBus);
            }

            public EntityHandle RegisterEntity(string name, Team team, Vector3Int position, int strength)
            {
                return Registry.Register(new EntityData
                {
                    Name = name,
                    Team = team,
                    Level = 1,
                    Strength = strength,
                    Dexterity = 14,
                    Constitution = 12,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    MaxHP = 30,
                    CurrentHP = 30,
                    GridPosition = position,
                    ActionsRemaining = 3
                });
            }

            public void Dispose()
            {
                if (root != null)
                    Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            string fieldName = $"<{propertyName}>k__BackingField";
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing auto-property backing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
