using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class ReadyStrikePlayModeTests
    {
        private const string SampleSceneName = "SampleScene";
        private const float TimeoutSeconds = 8f;

        private TurnManager turnManager;
        private EntityManager entityManager;
        private CombatEventBus eventBus;
        private PlayerActionExecutor playerActionExecutor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene(SampleSceneName);
            yield return WaitUntilOrTimeout(
                () => SceneManager.GetActiveScene().name == SampleSceneName,
                TimeoutSeconds,
                "SampleScene did not load.");

            yield return null;

            turnManager = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
            entityManager = UnityEngine.Object.FindFirstObjectByType<EntityManager>();
            eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            playerActionExecutor = UnityEngine.Object.FindFirstObjectByType<PlayerActionExecutor>();

            Assert.IsNotNull(turnManager, "TurnManager not found.");
            Assert.IsNotNull(entityManager, "EntityManager not found.");
            Assert.IsNotNull(eventBus, "CombatEventBus not found.");
            Assert.IsNotNull(playerActionExecutor, "PlayerActionExecutor not found.");

            EnsureReadyStrikeEventBinderPresent();

            yield return WaitUntilOrTimeout(
                () => entityManager.Registry != null && entityManager.Registry.Count >= 4,
                TimeoutSeconds,
                "Entity registry was not populated.");
        }

        [UnityTest]
        public IEnumerator GT_P32_PM_430_ReadyStrike_Melee_TriggersWhenEnemyMovesWithinReach()
        {
            var fighter = GetEntityByName("Fighter");
            var wizard = GetEntityByName("Wizard");
            var goblin1 = GetEntityByName("Goblin_1");
            var goblin2 = GetEntityByName("Goblin_2");

            ConfigureDeterministicInitiative(fighter, wizard, goblin1, goblin2);

            MoveEntityToCellSilent(fighter, new Vector3Int(1, 0, 1));
            MoveEntityToCellSilent(goblin1, new Vector3Int(2, 0, 2)); // in melee trigger range

            turnManager.StartCombat();
            yield return AdvanceToActorTurn(fighter.Handle, TimeoutSeconds, "Fighter did not get turn.");

            Assert.IsTrue(playerActionExecutor.TryExecuteReadyStrike(), "Fighter could not prepare Ready Strike.");
            Assert.IsTrue(turnManager.HasReadiedStrike(fighter.Handle), "Ready Strike record missing for fighter.");

            turnManager.EndTurn();
            yield return AdvanceToActorTurn(goblin1.Handle, TimeoutSeconds, "Goblin_1 turn did not arrive.");

            var from = goblin1.GridPosition;
            var to = new Vector3Int(1, 0, 2); // still within fighter melee reach
            MoveEntityToCellSilent(goblin1, to);
            eventBus.PublishEntityMoved(goblin1.Handle, from, to, forced: false);
            yield return null;

            Assert.IsFalse(turnManager.HasReadiedStrike(fighter.Handle), "Ready Strike should be consumed by movement trigger.");
            Assert.IsFalse(fighter.ReactionAvailable, "Ready Strike movement trigger should spend reaction.");
        }

        [UnityTest]
        public IEnumerator GT_P32_PM_431_ReadyStrike_AttackStart_TriggersWhenEnemyAttacksAllyInRange()
        {
            var fighter = GetEntityByName("Fighter");
            var wizard = GetEntityByName("Wizard");
            var goblin1 = GetEntityByName("Goblin_1");
            var goblin2 = GetEntityByName("Goblin_2");

            ConfigureDeterministicInitiative(fighter, wizard, goblin1, goblin2);

            MoveEntityToCellSilent(fighter, new Vector3Int(1, 0, 1));
            MoveEntityToCellSilent(wizard, new Vector3Int(2, 0, 1));
            MoveEntityToCellSilent(goblin1, new Vector3Int(2, 0, 2)); // within fighter reach and able to attack wizard

            turnManager.StartCombat();
            yield return AdvanceToActorTurn(fighter.Handle, TimeoutSeconds, "Fighter did not get turn.");

            Assert.IsTrue(playerActionExecutor.TryExecuteReadyStrike(), "Fighter could not prepare Ready Strike.");
            Assert.IsTrue(turnManager.HasReadiedStrike(fighter.Handle), "Ready Strike record missing for fighter.");

            turnManager.EndTurn();
            yield return AdvanceToActorTurn(goblin1.Handle, TimeoutSeconds, "Goblin_1 turn did not arrive.");

            eventBus.PublishStrikePreDamage(
                attacker: goblin1.Handle,
                target: wizard.Handle,
                naturalRoll: 15,
                total: 20,
                dc: 18,
                degree: DegreeOfSuccess.Success,
                damageRolled: 5,
                damageType: DamageType.Piercing);
            yield return null;

            Assert.IsFalse(turnManager.HasReadiedStrike(fighter.Handle), "Ready Strike should be consumed by attack-start trigger.");
            Assert.IsFalse(fighter.ReactionAvailable, "Ready Strike attack-start trigger should spend reaction.");
        }

        [UnityTest]
        public IEnumerator GT_P32_PM_432_ReadyStrike_Ranged_TriggersWhenEnemyMovesInsideFirstIncrement()
        {
            var fighter = GetEntityByName("Fighter");
            var wizard = GetEntityByName("Wizard");
            var goblin1 = GetEntityByName("Goblin_1");
            var goblin2 = GetEntityByName("Goblin_2");

            // Wizard goes first to prepare Ready Strike with bow.
            wizard.Wisdom = 5000;
            fighter.Wisdom = 4000;
            goblin1.Wisdom = -4000;
            goblin2.Wisdom = -5000;

            Assert.IsTrue(wizard.EquippedWeapon.IsRanged, "Wizard must have ranged weapon for this scenario.");

            MoveEntityToCellSilent(wizard, new Vector3Int(1, 0, 5));
            MoveEntityToCellSilent(goblin1, new Vector3Int(8, 0, 5)); // within first increment (60 ft)

            turnManager.StartCombat();
            yield return AdvanceToActorTurn(wizard.Handle, TimeoutSeconds, "Wizard did not get turn.");

            Assert.IsTrue(playerActionExecutor.TryExecuteReadyStrike(), "Wizard could not prepare Ready Strike.");
            Assert.IsTrue(turnManager.HasReadiedStrike(wizard.Handle), "Ready Strike record missing for wizard.");

            turnManager.EndTurn();
            yield return AdvanceToActorTurn(goblin1.Handle, TimeoutSeconds, "Goblin_1 turn did not arrive.");

            var from = goblin1.GridPosition;
            var to = new Vector3Int(8, 0, 4); // movement while still inside first increment
            MoveEntityToCellSilent(goblin1, to);
            eventBus.PublishEntityMoved(goblin1.Handle, from, to, forced: false);
            yield return null;

            Assert.IsFalse(turnManager.HasReadiedStrike(wizard.Handle), "Ranged Ready Strike should be consumed by movement trigger inside first increment.");
            Assert.IsFalse(wizard.ReactionAvailable, "Ranged Ready Strike trigger should spend reaction.");
        }

        [UnityTest]
        public IEnumerator GT_P32_PM_433_ReadyStrike_ForcedMovement_DoesNotTrigger()
        {
            var fighter = GetEntityByName("Fighter");
            var wizard = GetEntityByName("Wizard");
            var goblin1 = GetEntityByName("Goblin_1");
            var goblin2 = GetEntityByName("Goblin_2");

            ConfigureDeterministicInitiative(fighter, wizard, goblin1, goblin2);

            MoveEntityToCellSilent(fighter, new Vector3Int(1, 0, 1));
            MoveEntityToCellSilent(goblin1, new Vector3Int(3, 0, 2)); // outside melee trigger range

            turnManager.StartCombat();
            yield return AdvanceToActorTurn(fighter.Handle, TimeoutSeconds, "Fighter did not get turn.");

            Assert.IsTrue(playerActionExecutor.TryExecuteReadyStrike(), "Fighter could not prepare Ready Strike.");
            Assert.IsTrue(turnManager.HasReadiedStrike(fighter.Handle), "Ready Strike record missing for fighter.");

            turnManager.EndTurn();
            yield return AdvanceToActorTurn(goblin1.Handle, TimeoutSeconds, "Goblin_1 turn did not arrive.");

            var from = goblin1.GridPosition;
            var to = new Vector3Int(1, 0, 2); // would enter fighter reach, but forced movement must not trigger Ready
            MoveEntityToCellSilent(goblin1, to);
            eventBus.PublishEntityMoved(goblin1.Handle, from, to, forced: true);
            yield return null;

            Assert.IsTrue(turnManager.HasReadiedStrike(fighter.Handle), "Forced movement must not consume Ready Strike.");
            Assert.IsTrue(fighter.ReactionAvailable, "Forced movement must not consume reaction.");
        }

        [UnityTest]
        public IEnumerator GT_P32_PM_434_ReadyStrike_EndCombat_ClearsPreparedState()
        {
            var fighter = GetEntityByName("Fighter");
            var wizard = GetEntityByName("Wizard");
            var goblin1 = GetEntityByName("Goblin_1");
            var goblin2 = GetEntityByName("Goblin_2");

            ConfigureDeterministicInitiative(fighter, wizard, goblin1, goblin2);

            turnManager.StartCombat();
            yield return AdvanceToActorTurn(fighter.Handle, TimeoutSeconds, "Fighter did not get turn.");

            Assert.IsTrue(playerActionExecutor.TryExecuteReadyStrike(), "Fighter could not prepare Ready Strike.");
            Assert.IsTrue(turnManager.HasReadiedStrike(fighter.Handle), "Ready Strike record missing for fighter.");

            turnManager.EndCombat();
            yield return null;

            Assert.IsFalse(turnManager.HasReadiedStrike(fighter.Handle), "EndCombat must clear prepared Ready Strike state.");
            Assert.AreEqual(0, turnManager.ReadiedStrikeCount, "Ready prepared count must reset on combat end.");
        }

        [UnityTest]
        public IEnumerator GT_P32_PM_435_ReadyStrike_ExpiresAtNextActorTurnStart_WithoutTrigger()
        {
            var fighter = GetEntityByName("Fighter");
            var wizard = GetEntityByName("Wizard");
            var goblin1 = GetEntityByName("Goblin_1");
            var goblin2 = GetEntityByName("Goblin_2");

            ConfigureDeterministicInitiative(fighter, wizard, goblin1, goblin2);

            // Keep enemies far enough to avoid movement/attack triggers before fighter's next turn.
            MoveEntityToCellSilent(fighter, new Vector3Int(1, 0, 1));
            MoveEntityToCellSilent(wizard, new Vector3Int(1, 0, 5));
            MoveEntityToCellSilent(goblin1, new Vector3Int(9, 0, 9));
            MoveEntityToCellSilent(goblin2, new Vector3Int(9, 0, 8));

            turnManager.StartCombat();
            yield return AdvanceToActorTurn(fighter.Handle, TimeoutSeconds, "Fighter did not get turn.");

            Assert.IsTrue(playerActionExecutor.TryExecuteReadyStrike(), "Fighter could not prepare Ready Strike.");
            Assert.IsTrue(turnManager.HasReadiedStrike(fighter.Handle), "Ready Strike record missing for fighter.");

            turnManager.EndTurn();

            // Advance full round back to fighter.
            yield return AdvanceToActorTurn(fighter.Handle, TimeoutSeconds, "Fighter next turn did not arrive.");

            Assert.IsFalse(turnManager.HasReadiedStrike(fighter.Handle), "Prepared Ready Strike should expire at actor's next turn start.");
        }

        [UnityTest]
        public IEnumerator GT_P32_PM_436_ReadyStrike_AntiRecursion_EnemyCounterReadyDoesNotCascadeInSameTriggerScope()
        {
            var fighter = GetEntityByName("Fighter");
            var wizard = GetEntityByName("Wizard");
            var goblin1 = GetEntityByName("Goblin_1");
            var goblin2 = GetEntityByName("Goblin_2");

            ConfigureDeterministicInitiative(fighter, wizard, goblin1, goblin2);

            MoveEntityToCellSilent(fighter, new Vector3Int(1, 0, 1));
            MoveEntityToCellSilent(wizard, new Vector3Int(2, 0, 1));
            MoveEntityToCellSilent(goblin1, new Vector3Int(2, 0, 2)); // in reach for both sides

            // Stabilize scenario: goblin must survive fighter's readied strike to verify counter-ready suppression.
            goblin1.MaxHP = 200;
            goblin1.CurrentHP = 200;

            turnManager.StartCombat();
            yield return AdvanceToActorTurn(fighter.Handle, TimeoutSeconds, "Fighter did not get turn.");

            fighter.ReactionAvailable = true;
            goblin1.ReactionAvailable = true;

            Assert.IsTrue(playerActionExecutor.TryExecuteReadyStrike(), "Fighter could not prepare Ready Strike.");
            Assert.IsTrue(turnManager.TryPrepareReadiedStrike(goblin1.Handle, turnManager.RoundNumber), "Goblin_1 could not prepare Ready Strike.");
            Assert.IsTrue(turnManager.HasReadiedStrike(fighter.Handle), "Ready Strike record missing for fighter.");
            Assert.IsTrue(turnManager.HasReadiedStrike(goblin1.Handle), "Ready Strike record missing for goblin.");

            eventBus.PublishStrikePreDamage(
                attacker: goblin1.Handle,
                target: wizard.Handle,
                naturalRoll: 15,
                total: 20,
                dc: 18,
                degree: DegreeOfSuccess.Success,
                damageRolled: 5,
                damageType: DamageType.Piercing);
            yield return null;

            Assert.IsFalse(turnManager.HasReadiedStrike(fighter.Handle), "Fighter ready should be consumed by root attack-start trigger.");
            Assert.IsFalse(fighter.ReactionAvailable, "Fighter reaction should be spent by ready trigger.");
            Assert.IsTrue(turnManager.HasReadiedStrike(goblin1.Handle), "Goblin ready should remain; counter-ready cascade in same trigger scope must be suppressed.");
            Assert.IsTrue(goblin1.ReactionAvailable, "Goblin reaction should remain available when counter-ready is suppressed.");
        }

        private static void ConfigureDeterministicInitiative(
            EntityData fighter,
            EntityData wizard,
            EntityData goblin1,
            EntityData goblin2)
        {
            fighter.Wisdom = 5000;
            wizard.Wisdom = 4000;
            goblin1.Wisdom = -4000;
            goblin2.Wisdom = -5000;
        }

        private void EnsureReadyStrikeEventBinderPresent()
        {
            if (turnManager == null)
                return;

            var binder = turnManager.GetComponent<ReadyStrikeEventBinder>();
            if (binder == null)
                turnManager.gameObject.AddComponent<ReadyStrikeEventBinder>();
        }

        private EntityData GetEntityByName(string name)
        {
            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null) continue;
                if (string.Equals(data.Name, name, StringComparison.Ordinal))
                    return data;
            }

            Assert.Fail($"Entity '{name}' not found.");
            return null;
        }

        private void MoveEntityToCellSilent(EntityData data, Vector3Int targetCell)
        {
            Assert.IsNotNull(data, "MoveEntityToCellSilent received null EntityData.");
            if (data.GridPosition == targetCell)
                return;

            bool moved = entityManager.Occupancy.Move(data.Handle, targetCell, data.SizeCells);
            Assert.IsTrue(moved, $"Failed moving '{data.Name}' to {targetCell}.");

            data.GridPosition = targetCell;
            var view = entityManager.GetView(data.Handle);
            if (view != null)
                view.transform.position = entityManager.GetEntityWorldPosition(targetCell);
        }

        private IEnumerator AdvanceToActorTurn(EntityHandle actor, float timeoutSeconds, string timeoutReason)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!(turnManager.CurrentEntity == actor
                     && (turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn)))
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Timeout after {timeoutSeconds:0.##}s: {timeoutReason}");

                if (turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn)
                    turnManager.EndTurn();

                yield return null;
            }
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> predicate, float timeoutSeconds, string timeoutReason)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Timeout after {timeoutSeconds:0.##}s: {timeoutReason}");

                yield return null;
            }
        }
    }
}
