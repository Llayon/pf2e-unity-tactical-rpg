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
    public class TurnManagerDelayTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TurnManager_StartCombat_OpensDelayTriggerWindow_ForPlayerTurnStart()
        {
            var context = CreateCombatContext("TM_Delay_TriggerOpen");
            try
            {
                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State);
                Assert.IsTrue(context.turnManager.IsDelayTurnBeginTriggerOpen);
                Assert.IsTrue(context.turnManager.CanDelayCurrentTurn());
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_SetInitiativeRngForTesting_ControlsRollOrder()
        {
            var turnManagerGo = new GameObject("TM_Delay_Rng_TurnManager");
            var entityManagerGo = new GameObject("TM_Delay_Rng_EntityManager");

            try
            {
                var turnManager = turnManagerGo.AddComponent<TurnManager>();
                LogAssert.Expect(LogType.Error, "[EntityManager] Missing reference: GridManager. Assign it in Inspector.");
                var entityManager = entityManagerGo.AddComponent<EntityManager>();
                var registry = new EntityRegistry();

                SetPrivateField(turnManager, "entityManager", entityManager);
                SetAutoPropertyBackingField(entityManager, "Registry", registry);

                var player = CreateEntity("Player", Team.Player, wisdom: 10);
                var enemy = CreateEntity("Enemy", Team.Enemy, wisdom: 10);
                registry.Register(player);
                registry.Register(enemy);

                InvokeNonPublicInstanceMethod(
                    turnManager,
                    "SetInitiativeRngForTesting",
                    new FixedRng(new[] { 1, 20 }));

                turnManager.StartCombat();

                Assert.AreEqual(TurnState.EnemyTurn, turnManager.State);
                Assert.AreEqual(enemy.Handle, turnManager.CurrentEntity);
                Assert.AreEqual(20, turnManager.InitiativeOrder[0].Roll.naturalRoll);
                Assert.AreEqual(1, turnManager.InitiativeOrder[1].Roll.naturalRoll);
            }
            finally
            {
                if (turnManagerGo != null)
                    Object.DestroyImmediate(turnManagerGo);
                if (entityManagerGo != null)
                    Object.DestroyImmediate(entityManagerGo);
            }
        }

        [Test]
        public void TurnManager_StartCombat_DefaultInitiativeMode_UsesPerceptionSource()
        {
            var context = CreateCombatContext("TM_Initiative_DefaultPerception");
            try
            {
                Assert.Greater(context.turnManager.InitiativeOrder.Count, 0);

                for (int i = 0; i < context.turnManager.InitiativeOrder.Count; i++)
                {
                    var entry = context.turnManager.InitiativeOrder[i];
                    Assert.AreEqual(CheckSourceType.Perception, entry.Roll.source.type);
                }
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_StartCombat_SkillInitiativeMode_UsesConfiguredSkillSourceAndModifier()
        {
            var turnManagerGo = new GameObject("TM_Initiative_SkillMode_TurnManager");
            var entityManagerGo = new GameObject("TM_Initiative_SkillMode_EntityManager");

            try
            {
                var turnManager = turnManagerGo.AddComponent<TurnManager>();
                LogAssert.Expect(LogType.Error, "[EntityManager] Missing reference: GridManager. Assign it in Inspector.");
                var entityManager = entityManagerGo.AddComponent<EntityManager>();
                var registry = new EntityRegistry();

                SetPrivateField(turnManager, "entityManager", entityManager);
                SetAutoPropertyBackingField(entityManager, "Registry", registry);

                var player = CreateEntity("Player", Team.Player, wisdom: 10);
                player.Dexterity = 18; // Stealth key ability, +4
                registry.Register(player);

                var enemy = CreateEntity("Enemy", Team.Enemy, wisdom: 10);
                enemy.Dexterity = 12; // Stealth key ability, +1
                registry.Register(enemy);

                InvokeNonPublicInstanceMethod(
                    turnManager,
                    "SetInitiativeCheckModeForTesting",
                    InitiativeCheckMode.Skill,
                    SkillType.Stealth);

                InvokeNonPublicInstanceMethod(
                    turnManager,
                    "SetInitiativeRngForTesting",
                    new FixedRng(new[] { 10, 10 }));

                turnManager.StartCombat();

                var playerEntry = FindEntryByHandle(turnManager.InitiativeOrder, player.Handle);
                Assert.AreEqual(CheckSourceType.Skill, playerEntry.Roll.source.type);
                Assert.AreEqual(SkillType.Stealth, playerEntry.Roll.source.skill);
                Assert.AreEqual(player.GetSkillModifier(SkillType.Stealth), playerEntry.Roll.modifier);

                var enemyEntry = FindEntryByHandle(turnManager.InitiativeOrder, enemy.Handle);
                Assert.AreEqual(CheckSourceType.Skill, enemyEntry.Roll.source.type);
                Assert.AreEqual(SkillType.Stealth, enemyEntry.Roll.source.skill);
                Assert.AreEqual(enemy.GetSkillModifier(SkillType.Stealth), enemyEntry.Roll.modifier);
            }
            finally
            {
                if (turnManagerGo != null)
                    Object.DestroyImmediate(turnManagerGo);
                if (entityManagerGo != null)
                    Object.DestroyImmediate(entityManagerGo);
            }
        }

        [Test]
        public void TurnManager_BeginActionExecution_ClosesDelayTriggerWindow()
        {
            var context = CreateCombatContext("TM_Delay_TriggerClose");
            try
            {
                Assert.IsTrue(context.turnManager.IsDelayTurnBeginTriggerOpen);

                context.turnManager.BeginActionExecution(context.player.Handle, "Test.DelayClose");

                Assert.AreEqual(TurnState.ExecutingAction, context.turnManager.State);
                Assert.IsFalse(context.turnManager.IsDelayTurnBeginTriggerOpen);
                Assert.IsFalse(context.turnManager.CanDelayCurrentTurn());
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_TryDelayCurrentTurn_StoresDelayedActor_AdvancesToNextActor()
        {
            var context = CreateCombatContext("TM_Delay_Advance");
            try
            {
                var playerHandle = context.player.Handle;
                var enemyHandle = context.enemy.Handle;

                bool delayed = context.turnManager.TryDelayCurrentTurn();

                Assert.IsTrue(delayed);
                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State);
                Assert.AreEqual(enemyHandle, context.turnManager.CurrentEntity);
                Assert.IsTrue(context.turnManager.IsDelayed(playerHandle));
                Assert.AreEqual(1, context.turnManager.DelayedActorCount);
                Assert.IsFalse(ContainsHandle(context.turnManager.InitiativeOrder, playerHandle));
                Assert.IsTrue(ContainsHandle(context.turnManager.InitiativeOrder, enemyHandle));
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_TryDelayCurrentTurn_FrightenedTicksImmediately()
        {
            var context = CreateCombatContext("TM_Delay_FrightenedTick");
            try
            {
                context.player.Conditions.Add(new ActiveCondition(ConditionType.Frightened, value: 2, remainingRounds: -1));

                bool observedTick = false;
                EntityHandle observedActor = EntityHandle.None;
                int observedTickCount = -1;
                ConditionType observedFirstType = default;
                context.turnManager.OnConditionsTicked += e =>
                {
                    observedTick = true;
                    observedActor = e.actor;
                    observedTickCount = e.ticks.Count;
                    if (e.ticks.Count > 0)
                        observedFirstType = e.ticks[0].type;
                };

                bool delayed = context.turnManager.TryDelayCurrentTurn();

                Assert.IsTrue(delayed);
                Assert.AreEqual(1, context.player.GetConditionValue(ConditionType.Frightened));
                Assert.IsTrue(observedTick, "Delay should apply immediate modeled end-turn ticks (Frightened decay).");
                Assert.AreEqual(context.player.Handle, observedActor);
                Assert.AreEqual(1, observedTickCount);
                Assert.AreEqual(ConditionType.Frightened, observedFirstType);
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_EndCombat_ClearsDelayedStateAndTriggerWindow()
        {
            var context = CreateCombatContext("TM_Delay_Cleanup");
            try
            {
                Assert.IsTrue(context.turnManager.TryDelayCurrentTurn());
                Assert.AreEqual(1, context.turnManager.DelayedActorCount);
                Assert.IsTrue(context.turnManager.IsDelayed(context.player.Handle));
                Assert.IsTrue(context.turnManager.IsReactionSuppressedByDelay(context.player.Handle));

                context.turnManager.EndCombat();

                Assert.AreEqual(TurnState.Inactive, context.turnManager.State);
                Assert.AreEqual(0, context.turnManager.DelayedActorCount);
                Assert.IsFalse(context.turnManager.IsDelayed(context.player.Handle));
                Assert.IsFalse(context.turnManager.IsReactionSuppressedByDelay(context.player.Handle));
                Assert.IsFalse(context.turnManager.IsDelayTurnBeginTriggerOpen);
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_DelayedActor_AfterOtherTurnEnds_OpensDelayReturnWindow()
        {
            var context = CreateCombatContext("TM_Delay_ReturnWindow");
            try
            {
                Assert.IsTrue(context.turnManager.TryDelayCurrentTurn(), "Setup should delay player and pass turn to enemy.");
                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State);
                Assert.AreEqual(context.enemy.Handle, context.turnManager.CurrentEntity);

                context.turnManager.EndTurn();

                Assert.AreEqual(TurnState.DelayReturnWindow, context.turnManager.State);
                Assert.IsTrue(context.turnManager.IsDelayReturnWindowOpen);
                Assert.AreEqual(context.enemy.Handle, context.turnManager.DelayReturnWindowAfterActor);
                Assert.IsTrue(context.turnManager.IsDelayed(context.player.Handle));
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_DelayReturnWindow_Skip_ContinuesInitiativeFlow()
        {
            var context = CreateCombatContext("TM_Delay_ReturnSkip");
            try
            {
                Assert.IsTrue(context.turnManager.TryDelayCurrentTurn(), "Setup should delay player and pass turn to enemy.");
                Assert.AreEqual(1, context.turnManager.RoundNumber, "Combat should begin on round 1 in test setup.");

                context.turnManager.EndTurn();
                Assert.AreEqual(TurnState.DelayReturnWindow, context.turnManager.State);

                context.turnManager.SkipDelayReturnWindow();

                Assert.IsFalse(context.turnManager.IsDelayReturnWindowOpen);
                Assert.AreEqual(EntityHandle.None, context.turnManager.DelayReturnWindowAfterActor);
                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State, "With player removed and no resume yet, enemy starts next round.");
                Assert.AreEqual(2, context.turnManager.RoundNumber, "Skip should continue initiative progression.");
                Assert.AreEqual(context.enemy.Handle, context.turnManager.CurrentEntity);
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_TryReturnDelayedActor_ResumesTurn_PreservesFrightenedAfterDelayTick()
        {
            var context = CreateCombatContext("TM_Delay_ReturnResume");
            try
            {
                context.player.Conditions.Add(new ActiveCondition(ConditionType.Frightened, value: 2, remainingRounds: -1));

                Assert.IsTrue(context.turnManager.TryDelayCurrentTurn());
                Assert.AreEqual(1, context.player.GetConditionValue(ConditionType.Frightened), "Delay should apply immediate tick once.");
                Assert.IsTrue(context.turnManager.IsReactionSuppressedByDelay(context.player.Handle));
                Assert.IsFalse(context.turnManager.CanUseReaction(context.player.Handle));

                context.turnManager.EndTurn(); // enemy end -> opens return window
                Assert.AreEqual(TurnState.DelayReturnWindow, context.turnManager.State);

                bool resumed = context.turnManager.TryReturnDelayedActor(context.player.Handle);

                Assert.IsTrue(resumed);
                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State);
                Assert.AreEqual(context.player.Handle, context.turnManager.CurrentEntity);
                Assert.IsFalse(context.turnManager.IsDelayed(context.player.Handle));
                Assert.AreEqual(0, context.turnManager.DelayedActorCount);
                Assert.IsFalse(context.turnManager.IsDelayReturnWindowOpen);
                Assert.AreEqual(EntityHandle.None, context.turnManager.DelayReturnWindowAfterActor);
                Assert.IsFalse(context.turnManager.IsDelayTurnBeginTriggerOpen, "Resumed delayed turn is not a new 'turn begins' trigger window.");
                Assert.AreEqual(1, context.player.GetConditionValue(ConditionType.Frightened), "Resume must not reapply start-turn/end-turn ticks.");
                Assert.IsFalse(context.turnManager.IsReactionSuppressedByDelay(context.player.Handle));
                Assert.IsTrue(context.turnManager.CanUseReaction(context.player.Handle));
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_TryReturnDelayedActor_ChangesInitiativeOrderPermanently()
        {
            var context = CreateCombatContext("TM_Delay_ReturnOrder");
            try
            {
                Assert.IsTrue(context.turnManager.TryDelayCurrentTurn());
                context.turnManager.EndTurn(); // enemy end -> return window

                Assert.IsTrue(context.turnManager.TryReturnDelayedActor(context.player.Handle));
                Assert.AreEqual(2, context.turnManager.InitiativeOrder.Count);
                Assert.AreEqual(context.enemy.Handle, context.turnManager.InitiativeOrder[0].Handle);
                Assert.AreEqual(context.player.Handle, context.turnManager.InitiativeOrder[1].Handle);

                context.turnManager.EndTurn(); // finish resumed player turn

                Assert.AreEqual(2, context.turnManager.RoundNumber);
                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State);
                Assert.AreEqual(context.enemy.Handle, context.turnManager.CurrentEntity, "Permanent initiative should keep enemy before player.");
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_Delay_ExpiresAfterFullRoundWithoutReturn_StartsNormalTurnAtOriginalPosition()
        {
            var context = CreateCombatContext("TM_Delay_Expiry");
            try
            {
                Assert.IsTrue(context.turnManager.TryDelayCurrentTurn()); // enemy acts in round 1
                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State);

                context.turnManager.EndTurn(); // round 1 enemy end -> return window
                Assert.AreEqual(TurnState.DelayReturnWindow, context.turnManager.State);

                context.turnManager.SkipDelayReturnWindow(); // advance to round 2 enemy
                Assert.AreEqual(2, context.turnManager.RoundNumber);
                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State);
                Assert.AreEqual(context.enemy.Handle, context.turnManager.CurrentEntity);
                Assert.IsTrue(context.turnManager.IsDelayed(context.player.Handle));

                context.turnManager.EndTurn(); // round 2 enemy end -> delayed turn expires, normal player turn starts

                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State);
                Assert.AreEqual(context.player.Handle, context.turnManager.CurrentEntity);
                Assert.IsFalse(context.turnManager.IsDelayed(context.player.Handle));
                Assert.AreEqual(0, context.turnManager.DelayedActorCount);
                Assert.IsFalse(context.turnManager.IsDelayReturnWindowOpen);
                Assert.IsFalse(context.turnManager.IsReactionSuppressedByDelay(context.player.Handle));
                Assert.IsTrue(context.turnManager.IsDelayTurnBeginTriggerOpen, "Expired delayed turn should transition into a normal new turn at original position.");
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_DelayPlacementSelection_AnchorLaterActor_DelaysWithPlannedAnchor()
        {
            var context = CreateCombatContext("TM_Delay_PlannedAnchor");
            try
            {
                Assert.IsTrue(context.turnManager.TryBeginDelayPlacementSelection());
                Assert.IsTrue(context.turnManager.IsDelayPlacementSelectionOpen);
                Assert.IsTrue(context.turnManager.IsValidDelayAnchorForCurrentTurn(context.enemy.Handle));
                Assert.IsFalse(context.turnManager.IsValidDelayAnchorForCurrentTurn(context.player.Handle));

                Assert.IsTrue(context.turnManager.TryDelayCurrentTurnAfterActor(context.enemy.Handle));

                Assert.IsFalse(context.turnManager.IsDelayPlacementSelectionOpen);
                Assert.IsTrue(context.turnManager.IsDelayed(context.player.Handle));
                Assert.IsTrue(context.turnManager.TryGetDelayedPlannedAnchor(context.player.Handle, out var plannedAnchor));
                Assert.AreEqual(context.enemy.Handle, plannedAnchor);
                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State);
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_DelayWithPlannedAnchor_AutoResumesAfterAnchorTurn_WithoutReturnWindow()
        {
            var context = CreateCombatContext("TM_Delay_AutoResume");
            try
            {
                Assert.IsTrue(context.turnManager.TryBeginDelayPlacementSelection());
                Assert.IsTrue(context.turnManager.TryDelayCurrentTurnAfterActor(context.enemy.Handle));

                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State);
                Assert.AreEqual(context.enemy.Handle, context.turnManager.CurrentEntity);

                context.turnManager.EndTurn();

                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State, "Planned delay should auto-resume when anchor turn ends.");
                Assert.AreEqual(context.player.Handle, context.turnManager.CurrentEntity);
                Assert.IsFalse(context.turnManager.IsDelayReturnWindowOpen);
                Assert.IsFalse(context.turnManager.IsDelayed(context.player.Handle));
                Assert.AreEqual(0, context.turnManager.DelayedActorCount);
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_DelayWithPlannedAnchor_DoesNotOpenReturnWindowBeforeAnchorTurnEnds()
        {
            var context = CreateCombatContextWithTwoEnemies("TM_Delay_Planned_NoEarlyWindow");
            try
            {
                var firstEnemyHandle = context.turnManager.InitiativeOrder[1].Handle;
                var anchorEnemyHandle = context.turnManager.InitiativeOrder[2].Handle;

                Assert.IsTrue(context.turnManager.TryBeginDelayPlacementSelection());
                Assert.IsTrue(context.turnManager.TryDelayCurrentTurnAfterActor(anchorEnemyHandle));

                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State);
                Assert.AreEqual(firstEnemyHandle, context.turnManager.CurrentEntity, "First enemy should act after player delays.");

                context.turnManager.EndTurn(); // first enemy ends; planned anchor (enemy2) has not ended yet

                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State, "Delay return window should not open before planned anchor turn ends.");
                Assert.AreEqual(anchorEnemyHandle, context.turnManager.CurrentEntity);
                Assert.IsFalse(context.turnManager.IsDelayReturnWindowOpen);
                Assert.IsTrue(context.turnManager.IsDelayed(context.player.Handle));

                context.turnManager.EndTurn(); // planned anchor ends -> auto resume player

                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State);
                Assert.AreEqual(context.player.Handle, context.turnManager.CurrentEntity);
                Assert.IsFalse(context.turnManager.IsDelayReturnWindowOpen);
                Assert.IsFalse(context.turnManager.IsDelayed(context.player.Handle));
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_DelayWithPlannedAnchor_TwoPlayersTwoEnemies_DoesNotOpenReturnWindow_BeforeAnchorAndAutoResumesOnAnchor()
        {
            var context = CreateCombatContextWithTwoPlayersTwoEnemies("TM_Delay_Planned_2P2E");
            try
            {
                var fighterHandle = context.player.Handle;
                var wizardHandle = context.player2.Handle;
                var firstEnemyHandle = context.turnManager.InitiativeOrder[2].Handle;
                var anchorEnemyHandle = context.turnManager.InitiativeOrder[3].Handle;

                Assert.AreEqual(fighterHandle, context.turnManager.CurrentEntity, "Setup expects Fighter to act first.");
                context.turnManager.EndTurn(); // Fighter ends -> Wizard turn

                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State);
                Assert.AreEqual(wizardHandle, context.turnManager.CurrentEntity, "Wizard should act second in setup.");

                Assert.IsTrue(context.turnManager.TryBeginDelayPlacementSelection());
                Assert.IsTrue(context.turnManager.TryDelayCurrentTurnAfterActor(anchorEnemyHandle));

                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State);
                Assert.AreEqual(firstEnemyHandle, context.turnManager.CurrentEntity, "First enemy should act immediately after Wizard delays.");
                Assert.IsTrue(context.turnManager.IsDelayed(wizardHandle));
                Assert.IsTrue(context.turnManager.TryGetDelayedPlannedAnchor(wizardHandle, out var plannedAnchor));
                Assert.AreEqual(anchorEnemyHandle, plannedAnchor);

                context.turnManager.EndTurn(); // first enemy ends, anchor not yet reached

                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State, "Return window must not open before planned anchor turn ends.");
                Assert.AreEqual(anchorEnemyHandle, context.turnManager.CurrentEntity);
                Assert.IsFalse(context.turnManager.IsDelayReturnWindowOpen);

                context.turnManager.EndTurn(); // anchor enemy ends -> auto resume Wizard

                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State, "Planned delay should auto-resume on anchor end.");
                Assert.AreEqual(wizardHandle, context.turnManager.CurrentEntity);
                Assert.IsFalse(context.turnManager.IsDelayReturnWindowOpen);
                Assert.IsFalse(context.turnManager.IsDelayed(wizardHandle));
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_DelayWithSamePlannedAnchor_TwoPlayers_AutoResumesBothSequentially_WithoutReturnWindow()
        {
            var context = CreateCombatContextWithTwoPlayersTwoEnemies("TM_Delay_Planned_Chain");
            try
            {
                var fighterHandle = context.player.Handle;
                var wizardHandle = context.player2.Handle;
                var firstPlayerHandle = context.turnManager.InitiativeOrder[0].Handle;
                var secondPlayerHandle = context.turnManager.InitiativeOrder[1].Handle;
                var anchorEnemyHandle = context.turnManager.InitiativeOrder[3].Handle;
                var enemyAHandle = context.enemy.Handle;
                var enemyBHandle = context.enemy2.Handle;

                // Fighter delays after the later enemy.
                Assert.AreEqual(firstPlayerHandle, context.turnManager.CurrentEntity);
                Assert.IsTrue(context.turnManager.TryBeginDelayPlacementSelection());
                Assert.IsTrue(context.turnManager.TryDelayCurrentTurnAfterActor(anchorEnemyHandle));

                // Second player (now current) delays after the same later enemy.
                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State);
                Assert.AreEqual(secondPlayerHandle, context.turnManager.CurrentEntity);
                Assert.IsTrue(context.turnManager.TryBeginDelayPlacementSelection());
                Assert.IsTrue(context.turnManager.TryDelayCurrentTurnAfterActor(anchorEnemyHandle));

                // First enemy acts; no return window yet.
                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State);
                var firstActingEnemy = context.turnManager.CurrentEntity;
                Assert.IsTrue(firstActingEnemy == enemyAHandle || firstActingEnemy == enemyBHandle);
                context.turnManager.EndTurn();

                Assert.AreEqual(TurnState.EnemyTurn, context.turnManager.State);
                Assert.IsFalse(context.turnManager.IsDelayReturnWindowOpen);
                Assert.AreNotEqual(firstActingEnemy, context.turnManager.CurrentEntity, "Second enemy should act next after the first enemy ends.");
                Assert.IsTrue(context.turnManager.CurrentEntity == enemyAHandle || context.turnManager.CurrentEntity == enemyBHandle);

                // Anchor enemy ends -> first delayed player auto-resumes.
                context.turnManager.EndTurn();

                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State);
                Assert.IsFalse(context.turnManager.IsDelayReturnWindowOpen);
                Assert.IsTrue(context.turnManager.CurrentEntity == fighterHandle || context.turnManager.CurrentEntity == wizardHandle);

                var firstResumed = context.turnManager.CurrentEntity;
                var secondResumed = firstResumed == fighterHandle ? wizardHandle : fighterHandle;
                Assert.IsTrue(context.turnManager.IsDelayed(secondResumed), "Second planned delayed player should still be delayed pending chained auto-resume.");

                // First resumed player ends -> second delayed player auto-resumes (no manual return window).
                context.turnManager.EndTurn();

                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State);
                Assert.AreEqual(secondResumed, context.turnManager.CurrentEntity);
                Assert.IsFalse(context.turnManager.IsDelayReturnWindowOpen);
                Assert.IsFalse(context.turnManager.IsDelayed(secondResumed));
                Assert.AreEqual(0, context.turnManager.DelayedActorCount);
            }
            finally
            {
                DestroyContext(context);
            }
        }

        [Test]
        public void TurnManager_CanDelayCurrentTurn_Fails_WhenOnlyOneInitiativeEntry()
        {
            var context = CreateCombatContext("TM_Delay_OneActor");
            try
            {
                var order = GetPrivateField<List<InitiativeEntry>>(context.turnManager, "initiativeOrder");
                order.RemoveAll(e => e.Handle == context.enemy.Handle);
                Assert.AreEqual(1, order.Count, "Setup should simulate a single active initiative entry.");

                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State);
                Assert.IsTrue(context.turnManager.IsDelayTurnBeginTriggerOpen);
                Assert.IsFalse(context.turnManager.CanDelayCurrentTurn());
                Assert.IsFalse(context.turnManager.TryDelayCurrentTurn());
                Assert.AreEqual(TurnState.PlayerTurn, context.turnManager.State);
                Assert.AreEqual(context.player.Handle, context.turnManager.CurrentEntity);
            }
            finally
            {
                DestroyContext(context);
            }
        }

        private static bool ContainsHandle(IReadOnlyList<InitiativeEntry> order, EntityHandle handle)
        {
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i].Handle == handle)
                    return true;
            }

            return false;
        }

        private static InitiativeEntry FindEntryByHandle(IReadOnlyList<InitiativeEntry> order, EntityHandle handle)
        {
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i].Handle == handle)
                    return order[i];
            }

            Assert.Fail($"Could not find initiative entry for handle {handle.Id}.");
            return default;
        }

        private static TestCombatContext CreateCombatContext(string namePrefix)
        {
            var turnManagerGo = new GameObject($"{namePrefix}_TurnManager");
            var entityManagerGo = new GameObject($"{namePrefix}_EntityManager");

            var turnManager = turnManagerGo.AddComponent<TurnManager>();
            LogAssert.Expect(LogType.Error, "[EntityManager] Missing reference: GridManager. Assign it in Inspector.");
            var entityManager = entityManagerGo.AddComponent<EntityManager>();
            var registry = new EntityRegistry();

            SetPrivateField(turnManager, "entityManager", entityManager);
            SetAutoPropertyBackingField(entityManager, "Registry", registry);

            var player = CreateEntity("Player", Team.Player, 5000);
            registry.Register(player);
            var enemy = CreateEntity("Enemy", Team.Enemy, 10);
            registry.Register(enemy);

            turnManager.StartCombat();
            Assert.AreEqual(TurnState.PlayerTurn, turnManager.State, "Player should act first in test setup.");
            Assert.AreEqual(player.Handle, turnManager.CurrentEntity, "Current actor must be player in test setup.");

            return new TestCombatContext
            {
                turnManagerGo = turnManagerGo,
                entityManagerGo = entityManagerGo,
                turnManager = turnManager,
                player = player,
                enemy = enemy
            };
        }

        private static TestCombatContext CreateCombatContextWithTwoEnemies(string namePrefix)
        {
            var turnManagerGo = new GameObject($"{namePrefix}_TurnManager");
            var entityManagerGo = new GameObject($"{namePrefix}_EntityManager");

            var turnManager = turnManagerGo.AddComponent<TurnManager>();
            LogAssert.Expect(LogType.Error, "[EntityManager] Missing reference: GridManager. Assign it in Inspector.");
            var entityManager = entityManagerGo.AddComponent<EntityManager>();
            var registry = new EntityRegistry();

            SetPrivateField(turnManager, "entityManager", entityManager);
            SetAutoPropertyBackingField(entityManager, "Registry", registry);

            var player = CreateEntity("Player", Team.Player, 5000);
            registry.Register(player);
            var enemy = CreateEntity("EnemyA", Team.Enemy, 20);
            registry.Register(enemy);
            var enemy2 = CreateEntity("EnemyB", Team.Enemy, 10);
            registry.Register(enemy2);

            turnManager.StartCombat();
            Assert.AreEqual(TurnState.PlayerTurn, turnManager.State, "Player should act first in test setup.");
            Assert.AreEqual(player.Handle, turnManager.CurrentEntity, "Current actor must be player in test setup.");
            Assert.AreEqual(3, turnManager.InitiativeOrder.Count, "Expected 3 actors in initiative.");
            Assert.AreEqual(player.Handle, turnManager.InitiativeOrder[0].Handle);
            Assert.AreNotEqual(turnManager.InitiativeOrder[1].Handle, turnManager.InitiativeOrder[2].Handle);
            Assert.IsTrue(
                turnManager.InitiativeOrder[1].Handle == enemy.Handle || turnManager.InitiativeOrder[1].Handle == enemy2.Handle,
                "Second initiative slot should belong to one of the enemies.");
            Assert.IsTrue(
                turnManager.InitiativeOrder[2].Handle == enemy.Handle || turnManager.InitiativeOrder[2].Handle == enemy2.Handle,
                "Third initiative slot should belong to one of the enemies.");

            return new TestCombatContext
            {
                turnManagerGo = turnManagerGo,
                entityManagerGo = entityManagerGo,
                turnManager = turnManager,
                player = player,
                enemy = enemy,
                enemy2 = enemy2
            };
        }

        private static TestCombatContext CreateCombatContextWithTwoPlayersTwoEnemies(string namePrefix)
        {
            var turnManagerGo = new GameObject($"{namePrefix}_TurnManager");
            var entityManagerGo = new GameObject($"{namePrefix}_EntityManager");

            var turnManager = turnManagerGo.AddComponent<TurnManager>();
            LogAssert.Expect(LogType.Error, "[EntityManager] Missing reference: GridManager. Assign it in Inspector.");
            var entityManager = entityManagerGo.AddComponent<EntityManager>();
            var registry = new EntityRegistry();

            SetPrivateField(turnManager, "entityManager", entityManager);
            SetAutoPropertyBackingField(entityManager, "Registry", registry);

            // Keep player-player initiative order deterministic across random d20 rolls:
            // initiative total = d20 + modifier, so a large modifier gap keeps order stable.
            var fighter = CreateEntity("Fighter", Team.Player, 10000);
            registry.Register(fighter);
            var wizard = CreateEntity("Wizard", Team.Player, 6000);
            registry.Register(wizard);
            var enemyA = CreateEntity("EnemyA", Team.Enemy, 20);
            registry.Register(enemyA);
            var enemyB = CreateEntity("EnemyB", Team.Enemy, 10);
            registry.Register(enemyB);

            turnManager.StartCombat();

            Assert.AreEqual(TurnState.PlayerTurn, turnManager.State, "Player should act first in test setup.");
            Assert.AreEqual(4, turnManager.InitiativeOrder.Count, "Expected 4 actors in initiative.");
            Assert.AreEqual(fighter.Handle, turnManager.InitiativeOrder[0].Handle);
            Assert.AreEqual(wizard.Handle, turnManager.InitiativeOrder[1].Handle);
            Assert.IsTrue(
                turnManager.InitiativeOrder[2].Handle == enemyA.Handle || turnManager.InitiativeOrder[2].Handle == enemyB.Handle,
                "Third initiative slot should belong to one of the enemies.");
            Assert.IsTrue(
                turnManager.InitiativeOrder[3].Handle == enemyA.Handle || turnManager.InitiativeOrder[3].Handle == enemyB.Handle,
                "Fourth initiative slot should belong to one of the enemies.");

            return new TestCombatContext
            {
                turnManagerGo = turnManagerGo,
                entityManagerGo = entityManagerGo,
                turnManager = turnManager,
                player = fighter,
                player2 = wizard,
                enemy = enemyA,
                enemy2 = enemyB
            };
        }

        private static void DestroyContext(TestCombatContext context)
        {
            if (context.turnManagerGo != null)
                Object.DestroyImmediate(context.turnManagerGo);
            if (context.entityManagerGo != null)
                Object.DestroyImmediate(context.entityManagerGo);
        }

        private static EntityData CreateEntity(string name, Team team, int wisdom)
        {
            return new EntityData
            {
                Name = name,
                Team = team,
                MaxHP = 30,
                CurrentHP = 30,
                Speed = 25,
                Strength = 10,
                Dexterity = 10,
                Constitution = 10,
                Intelligence = 10,
                Wisdom = wisdom,
                Charisma = 10
            };
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}' on {target.GetType().Name}");
            return (T)field.GetValue(target);
        }

        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            var fieldName = $"<{propertyName}>k__BackingField";
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing backing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static object InvokeNonPublicInstanceMethod(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, InstanceNonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}");
            return method.Invoke(target, args);
        }

        private sealed class FixedRng : IRng
        {
            private readonly Queue<int> d20Queue;

            public FixedRng(IEnumerable<int> d20Rolls)
            {
                d20Queue = d20Rolls != null ? new Queue<int>(d20Rolls) : new Queue<int>();
            }

            public int RollD20()
            {
                if (d20Queue.Count <= 0)
                    return 10;

                return Mathf.Clamp(d20Queue.Dequeue(), 1, 20);
            }

            public int RollDie(int sides)
            {
                if (sides <= 0)
                    return 0;

                return 1;
            }
        }

        private sealed class TestCombatContext
        {
            public GameObject turnManagerGo;
            public GameObject entityManagerGo;
            public TurnManager turnManager;
            public EntityData player;
            public EntityData player2;
            public EntityData enemy;
            public EntityData enemy2;
        }
    }
}
