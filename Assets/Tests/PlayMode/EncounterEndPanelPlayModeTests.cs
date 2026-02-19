using System;
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PF2e.Core;
using PF2e.Managers;
using PF2e.Presentation;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class EncounterEndPanelPlayModeTests
    {
        private const string SampleSceneName = "SampleScene";
        private const float DefaultTimeoutSeconds = 5f;
        private const float ActionDrivenTimeoutSeconds = 12f;

        private TurnManager turnManager;
        private EntityManager entityManager;
        private CombatEventBus eventBus;
        private PlayerActionExecutor playerActionExecutor;
        private EncounterEndPanelController panelController;
        private CanvasGroup panelCanvasGroup;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI subtitleText;
        private Button restartButton;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene(SampleSceneName);
            yield return WaitUntilOrTimeout(
                () => SceneManager.GetActiveScene().name == SampleSceneName,
                DefaultTimeoutSeconds,
                "SampleScene did not load in setup.");

            // Let scene objects run Awake/OnEnable.
            yield return null;
            ResolveSceneReferences();
            yield return WaitUntilOrTimeout(
                () => entityManager.Registry != null && entityManager.Registry.Count >= 2,
                DefaultTimeoutSeconds,
                "EntityManager registry was not populated in SampleScene setup.");
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_001_Victory_ShowsPanel()
        {
            EncounterResult received = EncounterResult.Unknown;
            bool eventReceived = false;

            void Handler(in CombatEndedEvent e)
            {
                received = e.result;
                eventReceived = true;
            }

            eventBus.OnCombatEndedTyped += Handler;
            try
            {
                turnManager.EndCombat(EncounterResult.Victory);

                yield return WaitUntilOrTimeout(
                    () => eventReceived,
                    DefaultTimeoutSeconds,
                    "Did not receive CombatEndedEvent for Victory.");
                yield return WaitUntilOrTimeout(
                    () => IsPanelVisible(panelCanvasGroup),
                    DefaultTimeoutSeconds,
                    "Encounter end panel did not become visible for Victory.");

                Assert.AreEqual(EncounterResult.Victory, received);
                Assert.AreEqual("Victory", titleText.text);
                Assert.AreEqual("All enemies defeated.", subtitleText.text);
            }
            finally
            {
                eventBus.OnCombatEndedTyped -= Handler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_002_Defeat_ShowsPanel()
        {
            EncounterResult received = EncounterResult.Unknown;
            bool eventReceived = false;

            void Handler(in CombatEndedEvent e)
            {
                received = e.result;
                eventReceived = true;
            }

            eventBus.OnCombatEndedTyped += Handler;
            try
            {
                turnManager.EndCombat(EncounterResult.Defeat);

                yield return WaitUntilOrTimeout(
                    () => eventReceived,
                    DefaultTimeoutSeconds,
                    "Did not receive CombatEndedEvent for Defeat.");
                yield return WaitUntilOrTimeout(
                    () => IsPanelVisible(panelCanvasGroup),
                    DefaultTimeoutSeconds,
                    "Encounter end panel did not become visible for Defeat.");

                Assert.AreEqual(EncounterResult.Defeat, received);
                Assert.AreEqual("Defeat", titleText.text);
                Assert.AreEqual("All players defeated.", subtitleText.text);
            }
            finally
            {
                eventBus.OnCombatEndedTyped -= Handler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_003_Aborted_ShowsGenericPanel()
        {
            EncounterResult received = EncounterResult.Unknown;
            bool eventReceived = false;

            void Handler(in CombatEndedEvent e)
            {
                received = e.result;
                eventReceived = true;
            }

            eventBus.OnCombatEndedTyped += Handler;
            try
            {
                turnManager.EndCombat();

                yield return WaitUntilOrTimeout(
                    () => eventReceived,
                    DefaultTimeoutSeconds,
                    "Did not receive CombatEndedEvent for Aborted.");
                yield return WaitUntilOrTimeout(
                    () => IsPanelVisible(panelCanvasGroup),
                    DefaultTimeoutSeconds,
                    "Encounter end panel did not become visible for Aborted.");

                Assert.AreEqual(EncounterResult.Aborted, received);
                Assert.AreEqual("Encounter Ended", titleText.text);
                Assert.AreEqual("Combat was ended manually.", subtitleText.text);
            }
            finally
            {
                eventBus.OnCombatEndedTyped -= Handler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_004_Restart_ReloadsScene()
        {
            int previousTurnManagerId = turnManager.GetInstanceID();

            turnManager.EndCombat(EncounterResult.Victory);
            yield return WaitUntilOrTimeout(
                () => IsPanelVisible(panelCanvasGroup),
                DefaultTimeoutSeconds,
                "Encounter end panel did not become visible before restart.");

            restartButton.onClick.Invoke();

            yield return WaitUntilOrTimeout(
                () =>
                {
                    if (SceneManager.GetActiveScene().name != SampleSceneName)
                        return false;

                    var candidate = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
                    return candidate != null && candidate.GetInstanceID() != previousTurnManagerId;
                },
                DefaultTimeoutSeconds,
                "Restart did not reload scene with a new TurnManager instance.");

            ResolveSceneReferences();
            Assert.IsFalse(IsPanelVisible(panelCanvasGroup),
                "Encounter end panel should be hidden after scene reload.");
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_101_LiveFlow_VictoryFromCheckVictory_ShowsPanel()
        {
            EncounterResult received = EncounterResult.Unknown;
            bool eventReceived = false;

            void Handler(in CombatEndedEvent e)
            {
                received = e.result;
                eventReceived = true;
            }

            eventBus.OnCombatEndedTyped += Handler;
            try
            {
                yield return StartCombatAndWaitForTurnState();

                WipeTeam(Team.Enemy);
                turnManager.EndTurn();

                yield return WaitUntilOrTimeout(
                    () => eventReceived,
                    DefaultTimeoutSeconds,
                    "CombatEndedEvent was not raised for live-flow victory.");
                yield return WaitUntilOrTimeout(
                    () => IsPanelVisible(panelCanvasGroup),
                    DefaultTimeoutSeconds,
                    "Encounter panel did not appear for live-flow victory.");

                Assert.AreEqual(EncounterResult.Victory, received);
                Assert.AreEqual(TurnState.Inactive, turnManager.State);
                Assert.AreEqual("Victory", titleText.text);
                Assert.AreEqual("All enemies defeated.", subtitleText.text);
            }
            finally
            {
                eventBus.OnCombatEndedTyped -= Handler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_102_LiveFlow_DefeatFromCheckVictory_ShowsPanel()
        {
            EncounterResult received = EncounterResult.Unknown;
            bool eventReceived = false;

            void Handler(in CombatEndedEvent e)
            {
                received = e.result;
                eventReceived = true;
            }

            eventBus.OnCombatEndedTyped += Handler;
            try
            {
                yield return StartCombatAndWaitForTurnState();

                WipeTeam(Team.Player);
                turnManager.EndTurn();

                yield return WaitUntilOrTimeout(
                    () => eventReceived,
                    DefaultTimeoutSeconds,
                    "CombatEndedEvent was not raised for live-flow defeat.");
                yield return WaitUntilOrTimeout(
                    () => IsPanelVisible(panelCanvasGroup),
                    DefaultTimeoutSeconds,
                    "Encounter panel did not appear for live-flow defeat.");

                Assert.AreEqual(EncounterResult.Defeat, received);
                Assert.AreEqual(TurnState.Inactive, turnManager.State);
                Assert.AreEqual("Defeat", titleText.text);
                Assert.AreEqual("All players defeated.", subtitleText.text);
            }
            finally
            {
                eventBus.OnCombatEndedTyped -= Handler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_201_ActionDriven_PlayerStrike_Victory_ShowsPanel()
        {
            EncounterResult received = EncounterResult.Unknown;
            bool eventReceived = false;

            void Handler(in CombatEndedEvent e)
            {
                received = e.result;
                eventReceived = true;
            }

            eventBus.OnCombatEndedTyped += Handler;
            try
            {
                var fighter = GetEntityByName("Fighter");
                var wizard = GetEntityByName("Wizard");
                var goblin1 = GetEntityByName("Goblin_1");
                var goblin2 = GetEntityByName("Goblin_2");

                // Keep exactly one enemy alive for deterministic victory check.
                wizard.Team = Team.Player;
                goblin2.Team = Team.Player;

                // Force initiative + hit reliability regardless RNG roll.
                fighter.Wisdom = 5000;
                fighter.Level = 20;
                fighter.Strength = 100;
                goblin1.Wisdom = 1;
                goblin1.CurrentHP = 1;

                MoveEntityToCell(goblin1, fighter.GridPosition + Vector3Int.right);

                turnManager.StartCombat();

                yield return WaitUntilOrTimeout(
                    () => turnManager.State == TurnState.PlayerTurn && turnManager.CurrentEntity == fighter.Handle,
                    ActionDrivenTimeoutSeconds,
                    "Fighter did not become current actor for action-driven victory test.");

                bool strikeStarted = playerActionExecutor.TryExecuteStrike(goblin1.Handle);
                Assert.IsTrue(strikeStarted, "PlayerActionExecutor failed to execute strike in action-driven victory test.");

                // CheckVictory runs on EndTurn in current architecture.
                turnManager.EndTurn();

                yield return WaitUntilOrTimeout(
                    () => eventReceived,
                    ActionDrivenTimeoutSeconds,
                    "CombatEndedEvent was not raised for action-driven victory.");
                yield return WaitUntilOrTimeout(
                    () => IsPanelVisible(panelCanvasGroup),
                    ActionDrivenTimeoutSeconds,
                    "Encounter panel did not appear for action-driven victory.");

                Assert.AreEqual(EncounterResult.Victory, received);
                Assert.AreEqual("Victory", titleText.text);
                Assert.AreEqual("All enemies defeated.", subtitleText.text);
                Assert.AreEqual(TurnState.Inactive, turnManager.State);
            }
            finally
            {
                eventBus.OnCombatEndedTyped -= Handler;
            }
        }

        [UnityTest]
        public IEnumerator GT_P17_PM_202_ActionDriven_EnemyAIStrike_Defeat_ShowsPanel()
        {
            EncounterResult received = EncounterResult.Unknown;
            bool eventReceived = false;

            void Handler(in CombatEndedEvent e)
            {
                received = e.result;
                eventReceived = true;
            }

            eventBus.OnCombatEndedTyped += Handler;
            try
            {
                var fighter = GetEntityByName("Fighter");
                var wizard = GetEntityByName("Wizard");
                var goblin1 = GetEntityByName("Goblin_1");
                var goblin2 = GetEntityByName("Goblin_2");

                // Keep exactly one player alive for deterministic defeat check.
                wizard.Team = Team.Enemy;

                // Force enemy first + guaranteed hit/kill through real AI Strike path.
                fighter.Wisdom = 1;
                fighter.CurrentHP = 1;
                goblin1.Wisdom = 5000;
                goblin1.Level = 20;
                goblin1.Strength = 100;
                goblin2.Wisdom = 1;

                MoveEntityToCell(goblin1, fighter.GridPosition + Vector3Int.right);

                turnManager.StartCombat();

                yield return WaitUntilOrTimeout(
                    () => eventReceived,
                    ActionDrivenTimeoutSeconds,
                    "CombatEndedEvent was not raised for action-driven defeat.");
                yield return WaitUntilOrTimeout(
                    () => IsPanelVisible(panelCanvasGroup),
                    ActionDrivenTimeoutSeconds,
                    "Encounter panel did not appear for action-driven defeat.");

                Assert.AreEqual(EncounterResult.Defeat, received);
                Assert.AreEqual("Defeat", titleText.text);
                Assert.AreEqual("All players defeated.", subtitleText.text);
                Assert.AreEqual(TurnState.Inactive, turnManager.State);
            }
            finally
            {
                eventBus.OnCombatEndedTyped -= Handler;
            }
        }

        private void ResolveSceneReferences()
        {
            turnManager = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
            entityManager = UnityEngine.Object.FindFirstObjectByType<EntityManager>();
            eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            playerActionExecutor = UnityEngine.Object.FindFirstObjectByType<PlayerActionExecutor>();
            panelController = UnityEngine.Object.FindFirstObjectByType<EncounterEndPanelController>();

            Assert.IsNotNull(turnManager, "TurnManager not found in SampleScene.");
            Assert.IsNotNull(entityManager, "EntityManager not found in SampleScene.");
            Assert.IsNotNull(eventBus, "CombatEventBus not found in SampleScene.");
            Assert.IsNotNull(playerActionExecutor, "PlayerActionExecutor not found in SampleScene.");
            Assert.IsNotNull(panelController, "EncounterEndPanelController not found in SampleScene.");

            var panelGo = GameObject.Find("Canvas/EncounterEndPanel");
            Assert.IsNotNull(panelGo, "Canvas/EncounterEndPanel not found.");
            panelCanvasGroup = panelGo.GetComponent<CanvasGroup>();
            Assert.IsNotNull(panelCanvasGroup, "Canvas/EncounterEndPanel missing CanvasGroup.");

            titleText = GameObject.Find("Canvas/EncounterEndPanel/TitleText")?.GetComponent<TextMeshProUGUI>();
            subtitleText = GameObject.Find("Canvas/EncounterEndPanel/SubtitleText")?.GetComponent<TextMeshProUGUI>();
            restartButton = GameObject.Find("Canvas/EncounterEndPanel/RestartButton")?.GetComponent<Button>();

            Assert.IsNotNull(titleText, "TitleText not found or missing TextMeshProUGUI.");
            Assert.IsNotNull(subtitleText, "SubtitleText not found or missing TextMeshProUGUI.");
            Assert.IsNotNull(restartButton, "RestartButton not found or missing Button.");
        }

        private IEnumerator StartCombatAndWaitForTurnState()
        {
            turnManager.StartCombat();

            yield return WaitUntilOrTimeout(
                () => turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn,
                DefaultTimeoutSeconds,
                "TurnManager did not enter a turn state after StartCombat.");
        }

        private void WipeTeam(Team team)
        {
            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null || !data.IsAlive) continue;
                if (data.Team != team) continue;

                data.CurrentHP = 0;
                entityManager.HandleDeath(data.Handle);
            }
        }

        private EntityData GetEntityByName(string name)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(name), "Entity name must be provided.");

            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null) continue;
                if (string.Equals(data.Name, name, StringComparison.Ordinal))
                    return data;
            }

            Assert.Fail($"Entity '{name}' not found in registry.");
            return null;
        }

        private void MoveEntityToCell(EntityData data, Vector3Int targetCell)
        {
            Assert.IsNotNull(data, "MoveEntityToCell received null EntityData.");

            if (data.GridPosition == targetCell)
                return;

            bool moved = entityManager.Occupancy.Move(data.Handle, targetCell, data.SizeCells);
            Assert.IsTrue(moved,
                $"Failed to move '{data.Name}' to {targetCell} in test setup.");

            data.GridPosition = targetCell;

            var view = entityManager.GetView(data.Handle);
            if (view != null)
                view.transform.position = entityManager.GetEntityWorldPosition(targetCell);
        }

        private static bool IsPanelVisible(CanvasGroup canvasGroup)
        {
            return canvasGroup != null
                && canvasGroup.alpha > 0.99f
                && canvasGroup.interactable
                && canvasGroup.blocksRaycasts;
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
