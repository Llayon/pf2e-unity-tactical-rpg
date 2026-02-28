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
    public class ReactionDecisionPolicyTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void AutoShieldBlockPolicy_DecideReaction_InvokesTrueSynchronously()
        {
            var policy = new AutoShieldBlockPolicy();
            bool? decided = null;

            policy.DecideReaction(
                new ReactionOption(new EntityHandle(1), ReactionType.ShieldBlock, ReactionTriggerPhase.PostHit),
                new EntityData(),
                incomingDamage: 7,
                onDecided: value => decided = value);

            Assert.AreEqual(true, decided);
        }

        [Test]
        public void PlayerExecutor_ResolvePostHitReactionReduction_WhenPolicyDoesNotCallback_ReturnsZeroAndWarns()
        {
            using var ctx = new ReactionPolicyContext();

            var phase = StrikePhaseResult.FromAttackRoll(
                ctx.Attacker,
                ctx.Target,
                "Sword",
                naturalRoll: 12,
                attackBonus: 8,
                mapPenalty: 0,
                total: 20).WithHitAndDamage(
                dc: 15,
                degree: DegreeOfSuccess.Success,
                damageRolled: 7,
                damageType: DamageType.Slashing,
                damageDealt: true);

            const string expectedWarning = "[Reaction] DecideReaction did not invoke callback synchronously. Treating as decline.";
            bool sawExpectedWarning = false;
            void Capture(string condition, string stackTrace, LogType type)
            {
                _ = stackTrace;
                if (type == LogType.Warning && condition == expectedWarning)
                    sawExpectedWarning = true;
            }

            Application.logMessageReceived += Capture;
            try
            {
                int reduction = ctx.InvokeResolvePostHitReactionReduction(phase);
                Assert.AreEqual(0, reduction);
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }

            Assert.IsTrue(sawExpectedWarning, "Expected sync fail-safe warning was not observed.");

            var targetData = ctx.Registry.Get(ctx.Target);
            Assert.IsTrue(targetData.ReactionAvailable, "Fail-safe decline should not spend reaction.");
        }

        private sealed class ReactionPolicyContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject turnManagerGo;
            private readonly GameObject executorGo;
            private readonly GameObject shieldBlockActionGo;
            private readonly GameObject strikeActionGo;
            private readonly ShieldDefinition shieldDef;
            private readonly WeaponDefinition weaponDef;

            private readonly MethodInfo resolveMethod;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public TurnManager TurnManager { get; }
            public PlayerActionExecutor Executor { get; }
            public EntityRegistry Registry { get; }
            public EntityHandle Attacker { get; }
            public EntityHandle Target { get; }

            public ReactionPolicyContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                eventBusGo = new GameObject("EventBus_ReactionPolicyTest");
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("EntityManager_ReactionPolicyTest");
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                turnManagerGo = new GameObject("TurnManager_ReactionPolicyTest");
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);

                shieldBlockActionGo = new GameObject("ShieldBlockAction_ReactionPolicyTest");
                var shieldBlockAction = shieldBlockActionGo.AddComponent<ShieldBlockAction>();
                SetPrivateField(shieldBlockAction, "entityManager", EntityManager);
                SetPrivateField(shieldBlockAction, "eventBus", EventBus);

                strikeActionGo = new GameObject("StrikeAction_ReactionPolicyTest");
                var strikeAction = strikeActionGo.AddComponent<StrikeAction>();
                SetPrivateField(strikeAction, "entityManager", EntityManager);
                SetPrivateField(strikeAction, "eventBus", EventBus);

                executorGo = new GameObject("Executor_ReactionPolicyTest");
                Executor = executorGo.AddComponent<PlayerActionExecutor>();
                SetPrivateField(Executor, "turnManager", TurnManager);
                SetPrivateField(Executor, "entityManager", EntityManager);
                SetPrivateField(Executor, "strikeAction", strikeAction);
                SetPrivateField(Executor, "shieldBlockAction", shieldBlockAction);
                SetPrivateField(Executor, "reactionPolicy", new NeverCallbackPolicy());

                weaponDef = ScriptableObject.CreateInstance<WeaponDefinition>();
                weaponDef.itemName = "Test Sword";
                weaponDef.diceCount = 1;
                weaponDef.dieSides = 6;
                weaponDef.reachFeet = 5;
                weaponDef.isRanged = false;
                weaponDef.damageType = DamageType.Slashing;

                shieldDef = ScriptableObject.CreateInstance<ShieldDefinition>();
                shieldDef.itemName = "Test Shield";
                shieldDef.acBonus = 2;
                shieldDef.hardness = 5;
                shieldDef.maxHP = 20;

                Attacker = Registry.Register(new EntityData
                {
                    Name = "Attacker",
                    Team = Team.Player,
                    Size = CreatureSize.Medium,
                    Level = 1,
                    MaxHP = 20,
                    CurrentHP = 20,
                    Speed = 25,
                    Strength = 10,
                    Dexterity = 10,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 100,
                    Charisma = 10,
                    EquippedWeapon = new WeaponInstance { def = weaponDef }
                });

                var raisedShield = ShieldInstance.CreateEquipped(shieldDef);
                raisedShield.isRaised = true;

                Target = Registry.Register(new EntityData
                {
                    Name = "Target",
                    Team = Team.Enemy,
                    Size = CreatureSize.Medium,
                    Level = 1,
                    MaxHP = 20,
                    CurrentHP = 20,
                    Speed = 25,
                    Strength = 10,
                    Dexterity = 10,
                    Constitution = 10,
                    Intelligence = 10,
                    Wisdom = 0,
                    Charisma = 10,
                    ReactionAvailable = true,
                    EquippedWeapon = new WeaponInstance { def = weaponDef },
                    EquippedShield = raisedShield
                });

                var attackerData = Registry.Get(Attacker);
                attackerData.ActionsRemaining = 3;
                attackerData.MAPCount = 0;
                attackerData.ReactionAvailable = true;

                var targetData = Registry.Get(Target);
                targetData.ActionsRemaining = 3;
                targetData.MAPCount = 0;
                targetData.ReactionAvailable = true;

                SetPrivateField(TurnManager, "initiativeOrder", new System.Collections.Generic.List<InitiativeEntry>
                {
                    new InitiativeEntry { Handle = Attacker, Roll = new CheckRoll(20, 45, CheckSource.Perception()), IsPlayer = true },
                    new InitiativeEntry { Handle = Target, Roll = new CheckRoll(1, -5, CheckSource.Perception()), IsPlayer = false }
                });
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);

                resolveMethod = typeof(PlayerActionExecutor).GetMethod("ResolvePostHitReactionReduction", InstanceNonPublic);
                Assert.IsNotNull(resolveMethod, "ResolvePostHitReactionReduction not found");
            }

            public int InvokeResolvePostHitReactionReduction(StrikePhaseResult phase)
            {
                return (int)resolveMethod.Invoke(Executor, new object[] { phase });
            }

            public void Dispose()
            {
                if (weaponDef != null) Object.DestroyImmediate(weaponDef);
                if (shieldDef != null) Object.DestroyImmediate(shieldDef);
                if (executorGo != null) Object.DestroyImmediate(executorGo);
                if (strikeActionGo != null) Object.DestroyImmediate(strikeActionGo);
                if (shieldBlockActionGo != null) Object.DestroyImmediate(shieldBlockActionGo);
                if (turnManagerGo != null) Object.DestroyImmediate(turnManagerGo);
                if (entityManagerGo != null) Object.DestroyImmediate(entityManagerGo);
                if (eventBusGo != null) Object.DestroyImmediate(eventBusGo);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
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
