using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PF2e.Core;
using PF2e.Managers;
using PF2e.Presentation;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class DelayUiPointerPlayModeTests
    {
        private const string SampleSceneName = "SampleScene";
        private const float DefaultTimeoutSeconds = 8f;
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        private TurnManager turnManager;
        private EntityManager entityManager;
        private ActionBarController actionBar;
        private EventSystem eventSystem;
        private GameObject createdEventSystem;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene(SampleSceneName);
            yield return WaitUntilOrTimeout(
                () => SceneManager.GetActiveScene().name == SampleSceneName,
                DefaultTimeoutSeconds,
                "SampleScene did not load.");

            yield return null;
            ResolveSceneReferences();

            yield return WaitUntilOrTimeout(
                () => entityManager.Registry != null && entityManager.Registry.Count >= 2,
                DefaultTimeoutSeconds,
                "EntityManager registry was not populated.");

            eventSystem = EnsureEventSystem();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (createdEventSystem != null)
                UnityEngine.Object.Destroy(createdEventSystem);

            yield return null;
        }

        [UnityTest]
        public IEnumerator GT_P29_PM_417_DelayButton_PointerClick_TogglesPlacementSelection()
        {
            ConfigureDeterministicDelayInitiative();
            turnManager.StartCombat();
            yield return AdvanceToNextPlayerTurn(DefaultTimeoutSeconds, "Did not reach player turn for Delay toggle test.");

            Assert.IsTrue(turnManager.CanDelayCurrentTurn(), "Setup invalid: Delay must be available at turn start.");

            var delayButton = GetActionBarButton("delayButton");
            Assert.IsNotNull(delayButton, "Delay button is not wired in ActionBarController.");
            Assert.IsTrue(delayButton.gameObject.activeInHierarchy);
            Assert.IsTrue(delayButton.interactable);

            PointerClick(delayButton.gameObject);
            yield return null;
            Assert.IsTrue(turnManager.IsDelayPlacementSelectionOpen, "First Delay pointer click should open placement selection.");

            PointerClick(delayButton.gameObject);
            yield return null;
            Assert.IsFalse(turnManager.IsDelayPlacementSelectionOpen, "Second Delay pointer click should cancel placement selection.");
        }

        [UnityTest]
        public IEnumerator GT_P29_PM_418_InsertionMarker_PointerClick_CommitsPlannedDelay()
        {
            ConfigureDeterministicDelayInitiative();
            turnManager.StartCombat();
            yield return AdvanceToNextPlayerTurn(DefaultTimeoutSeconds, "Did not reach player turn for marker click test.");

            var delayedActor = turnManager.CurrentEntity;
            Assert.IsTrue(delayedActor.IsValid);

            Assert.IsTrue(
                TryGetLatestEnemyAnchorAfterCurrentActor(out var plannedAnchor),
                "Setup invalid: no enemy anchor available after current player.");

            var delayButton = GetActionBarButton("delayButton");
            Assert.IsNotNull(delayButton);
            PointerClick(delayButton.gameObject);

            yield return WaitUntilOrTimeout(
                () => turnManager.IsDelayPlacementSelectionOpen,
                DefaultTimeoutSeconds,
                "Delay placement selection did not open after pointer click.");

            InitiativeInsertionMarker targetMarker = null;
            yield return WaitUntilOrTimeout(
                () =>
                {
                    targetMarker = FindActiveMarkerForAnchor(plannedAnchor);
                    return targetMarker != null;
                },
                DefaultTimeoutSeconds,
                "Did not find active insertion marker for planned anchor.");

            PointerClick(targetMarker.gameObject);
            yield return null;

            Assert.IsFalse(turnManager.IsDelayPlacementSelectionOpen, "Marker click should commit and close placement selection.");
            Assert.IsTrue(turnManager.IsDelayed(delayedActor), "Marker click should place actor into delayed state.");
            Assert.IsTrue(turnManager.TryGetDelayedPlannedAnchor(delayedActor, out var recordedAnchor));
            Assert.AreEqual(plannedAnchor, recordedAnchor, "Recorded planned anchor mismatch after marker click.");
        }

        [UnityTest]
        public IEnumerator GT_P29_PM_419_ReturnNowButton_PointerClick_ResumesManualDelayedActor()
        {
            ConfigureDeterministicDelayInitiative();
            turnManager.StartCombat();
            yield return AdvanceToNextPlayerTurn(DefaultTimeoutSeconds, "Did not reach player turn for ReturnNow test.");

            var delayedActor = turnManager.CurrentEntity;
            Assert.IsTrue(turnManager.TryDelayCurrentTurn(), "Setup failed: manual delay did not start.");

            yield return WaitUntilOrTimeout(
                () => (turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn)
                    && turnManager.CurrentEntity != delayedActor,
                DefaultTimeoutSeconds,
                "Did not reach next actor after manual delay.");

            var windowAnchor = turnManager.CurrentEntity;
            turnManager.EndTurn();

            yield return WaitUntilOrTimeout(
                () => turnManager.IsDelayReturnWindowOpen && turnManager.State == TurnState.DelayReturnWindow,
                DefaultTimeoutSeconds,
                "Delay return window did not open.");

            var returnNowButton = GetActionBarButton("returnNowButton");
            Assert.IsNotNull(returnNowButton, "ReturnNow button is not wired.");
            Assert.IsTrue(returnNowButton.gameObject.activeInHierarchy);
            Assert.IsTrue(returnNowButton.interactable);

            PointerClick(returnNowButton.gameObject);
            yield return null;

            Assert.IsFalse(turnManager.IsDelayReturnWindowOpen);
            Assert.AreEqual(TurnState.PlayerTurn, turnManager.State);
            Assert.AreEqual(delayedActor, turnManager.CurrentEntity);
            Assert.IsFalse(turnManager.IsDelayed(delayedActor));
        }

        [UnityTest]
        public IEnumerator GT_P29_PM_420_SkipButton_PointerClick_ClosesWindow_WithoutResumingDelayedActor()
        {
            ConfigureDeterministicDelayInitiative();
            turnManager.StartCombat();
            yield return AdvanceToNextPlayerTurn(DefaultTimeoutSeconds, "Did not reach player turn for Skip test.");

            var delayedActor = turnManager.CurrentEntity;
            Assert.IsTrue(turnManager.TryDelayCurrentTurn(), "Setup failed: manual delay did not start.");

            yield return WaitUntilOrTimeout(
                () => (turnManager.State == TurnState.PlayerTurn || turnManager.State == TurnState.EnemyTurn)
                    && turnManager.CurrentEntity != delayedActor,
                DefaultTimeoutSeconds,
                "Did not reach next actor after manual delay.");

            turnManager.EndTurn();

            yield return WaitUntilOrTimeout(
                () => turnManager.IsDelayReturnWindowOpen && turnManager.State == TurnState.DelayReturnWindow,
                DefaultTimeoutSeconds,
                "Delay return window did not open.");

            var skipButton = GetActionBarButton("skipDelayWindowButton");
            Assert.IsNotNull(skipButton, "Skip button is not wired.");
            Assert.IsTrue(skipButton.gameObject.activeInHierarchy);
            Assert.IsTrue(skipButton.interactable);

            PointerClick(skipButton.gameObject);
            yield return null;

            Assert.IsFalse(turnManager.IsDelayReturnWindowOpen, "Skip pointer click should close DelayReturnWindow.");
            Assert.IsTrue(turnManager.IsDelayed(delayedActor), "Skip should keep delayed actor pending.");
            Assert.AreNotEqual(delayedActor, turnManager.CurrentEntity, "Skip should continue initiative flow to another actor.");
        }

        private void ResolveSceneReferences()
        {
            turnManager = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
            entityManager = UnityEngine.Object.FindFirstObjectByType<EntityManager>();
            actionBar = UnityEngine.Object.FindFirstObjectByType<ActionBarController>();

            Assert.IsNotNull(turnManager, "TurnManager not found.");
            Assert.IsNotNull(entityManager, "EntityManager not found.");
            Assert.IsNotNull(actionBar, "ActionBarController not found.");
        }

        private EventSystem EnsureEventSystem()
        {
            var es = EventSystem.current;
            if (es != null)
                return es;

            createdEventSystem = new GameObject("DelayUiPointerTestsEventSystem");
            es = createdEventSystem.AddComponent<EventSystem>();
            createdEventSystem.AddComponent<StandaloneInputModule>();
            return es;
        }

        private void ConfigureDeterministicDelayInitiative()
        {
            var fighter = GetEntityByName("Fighter");
            var wizard = GetEntityByName("Wizard");
            var goblin1 = GetEntityByName("Goblin_1");
            var goblin2 = GetEntityByName("Goblin_2");

            // Extreme spread to make initiative order stable despite d20 roll variance.
            fighter.Wisdom = 200000;
            wizard.Wisdom = 199000;
            goblin1.Wisdom = -199000;
            goblin2.Wisdom = -200000;
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

        private bool TryGetLatestEnemyAnchorAfterCurrentActor(out EntityHandle anchor)
        {
            anchor = EntityHandle.None;
            var actor = turnManager.CurrentEntity;
            if (!actor.IsValid) return false;

            var order = turnManager.InitiativeOrder;
            int idx = -1;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i].Handle == actor)
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0) return false;

            for (int i = idx + 1; i < order.Count; i++)
            {
                var handle = order[i].Handle;
                var data = entityManager.Registry.Get(handle);
                if (data == null || !data.IsAlive || data.Team != Team.Enemy)
                    continue;

                anchor = handle;
            }

            return anchor.IsValid;
        }

        private InitiativeInsertionMarker FindActiveMarkerForAnchor(EntityHandle anchor)
        {
            var markers = UnityEngine.Object.FindObjectsByType<InitiativeInsertionMarker>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null || !marker.gameObject.activeInHierarchy)
                    continue;
                if (marker.AnchorHandle != anchor)
                    continue;

                return marker;
            }

            return null;
        }

        private Button GetActionBarButton(string privateFieldName)
        {
            var field = actionBar.GetType().GetField(privateFieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing ActionBarController field '{privateFieldName}'.");
            return field.GetValue(actionBar) as Button;
        }

        private void PointerClick(GameObject target)
        {
            Assert.IsNotNull(target, "Pointer click target is null.");
            Assert.IsNotNull(eventSystem, "EventSystem is missing.");
            Assert.IsTrue(target.activeInHierarchy, $"Pointer click target '{target.name}' is not active.");

            var pointer = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
                position = Vector2.zero
            };

            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        }

        private IEnumerator AdvanceToNextPlayerTurn(float timeoutSeconds, string timeoutReason)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (turnManager.State != TurnState.PlayerTurn)
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Timeout after {timeoutSeconds:0.##}s: {timeoutReason}");

                if (turnManager.State == TurnState.EnemyTurn)
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
