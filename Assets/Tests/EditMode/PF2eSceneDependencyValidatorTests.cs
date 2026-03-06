using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PF2e.Core;
using PF2e.Managers;
using PF2e.Presentation;
using PF2e.TurnSystem;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PF2e.Tests
{
    [TestFixture]
    public class PF2eSceneDependencyValidatorTests
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string EncounterFlowScenePath = "Assets/Scenes/EncounterFlowPrefabScene.unity";

        [Test]
        public void AutoFixWorkflowGuard_WhenActiveSceneIsCleanNonSample_ReturnsToSampleScene()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");
            Assert.IsTrue(System.IO.File.Exists(EncounterFlowScenePath), $"Missing scene: {EncounterFlowScenePath}");

            EditorSceneManager.OpenScene(EncounterFlowScenePath, OpenSceneMode.Single);
            var activeBefore = SceneManager.GetActiveScene();
            Assert.AreEqual(EncounterFlowScenePath, activeBefore.path);
            Assert.IsFalse(activeBefore.isDirty, "Test precondition: active non-sample scene must be clean.");

            InvokePrivateValidatorMethod("TryReturnToSampleSceneIfSafe");

            var activeAfter = SceneManager.GetActiveScene();
            Assert.AreEqual(SampleScenePath, activeAfter.path);
        }

        [Test]
        public void SampleScene_Smoke_WiresCriticalCombatUiControllers()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<ActionBarController>(),
                "SampleScene must contain ActionBarController.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<InitiativeBarController>(),
                "SampleScene must contain InitiativeBarController.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<TurnUIController>(FindObjectsInactive.Include),
                "SampleScene must contain TurnUIController.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<CombatLogController>(),
                "SampleScene must contain CombatLogController.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<UnitPanelController>(),
                "SampleScene must contain UnitPanelController.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<TurnEconomyController>(),
                "SampleScene must contain TurnEconomyController.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<TurnOptionsPresenter>(),
                "SampleScene must contain TurnOptionsPresenter.");
        }

        [Test]
        public void SampleScene_AidUiAndExecutor_Wired()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            var actionBar = UnityEngine.Object.FindFirstObjectByType<ActionBarController>();
            var executor = UnityEngine.Object.FindFirstObjectByType<PlayerActionExecutor>();
            var aidAction = UnityEngine.Object.FindFirstObjectByType<AidAction>();

            Assert.IsNotNull(eventBus, "SampleScene must contain CombatEventBus.");
            Assert.IsNotNull(actionBar, "SampleScene must contain ActionBarController.");
            Assert.IsNotNull(executor, "SampleScene must contain PlayerActionExecutor.");

            SetPrivateField(actionBar, "aidButton", null);
            SetPrivateField(actionBar, "aidHighlight", null);
            SetPrivateField(actionBar, "aidPreparedIndicatorRoot", null);
            SetPrivateField(actionBar, "aidPreparedIndicatorLabel", null);

            var staleAidButton = actionBar.transform.Find("AidButton");
            if (staleAidButton != null)
            {
                UnityEngine.Object.DestroyImmediate(staleAidButton.gameObject);
            }

            // Scene/tooling policy: validator autofix is responsible for Aid UI creation/wiring in legacy scenes.
            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);
            InvokePrivateInstanceMethod(executor, "ResolveOptionalReferences");

            var aidButton = GetPrivateField<Button>(actionBar, "aidButton");
            var aidHighlight = GetPrivateField<Image>(actionBar, "aidHighlight");
            var aidPreparedBadgeRoot = GetPrivateField<GameObject>(actionBar, "aidPreparedIndicatorRoot");
            var aidPreparedBadgeLabel = GetPrivateField<Component>(actionBar, "aidPreparedIndicatorLabel");
            Assert.IsNotNull(aidButton, "ActionBarController must have Aid button wired by scene/autofix.");
            Assert.IsNotNull(aidHighlight, "ActionBarController must have Aid highlight wired by scene/autofix.");
            Assert.IsNotNull(aidPreparedBadgeRoot, "ActionBarController must have Aid prepared badge wired by scene/autofix.");
            Assert.IsNotNull(aidPreparedBadgeLabel, "ActionBarController must have Aid prepared badge label wired by scene/autofix.");
            Assert.AreEqual("AidButton", aidButton.gameObject.name, "Resolved Aid button should use canonical name AidButton.");
            Assert.AreEqual("AidPreparedBadge", aidPreparedBadgeRoot.name, "Aid prepared badge should use canonical name AidPreparedBadge.");
            Assert.AreSame(aidButton.transform, aidPreparedBadgeRoot.transform.parent, "Aid prepared badge must be attached to Aid button.");
            Assert.IsNotNull(aidButton.transform.Find("AidPreparedBadge"), "Aid button hierarchy must contain AidPreparedBadge child.");
            Assert.IsTrue(aidPreparedBadgeLabel.transform.IsChildOf(aidPreparedBadgeRoot.transform), "Aid prepared badge label must be under AidPreparedBadge.");

            var resolvedAidAction = GetPrivateField<AidAction>(executor, "aidAction");
            var wiredEventBus = GetPrivateField<CombatEventBus>(executor, "eventBus");
            Assert.IsNotNull(resolvedAidAction, "PlayerActionExecutor must resolve AidAction (wired or runtime fallback).");
            Assert.AreSame(eventBus, wiredEventBus, "PlayerActionExecutor.eventBus must be wired/resolved.");

            if (aidAction != null)
                Assert.AreSame(aidAction, resolvedAidAction, "When AidAction exists in scene, executor should reuse it.");
        }

        [Test]
        public void SampleScene_TurnOptionsUiAndDeps_Wired()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            var turnManager = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
            var entityManager = UnityEngine.Object.FindFirstObjectByType<EntityManager>();
            var actionExecutor = UnityEngine.Object.FindFirstObjectByType<PlayerActionExecutor>();
            var targetingController = UnityEngine.Object.FindFirstObjectByType<TargetingController>();
            var initiativeBar = UnityEngine.Object.FindFirstObjectByType<InitiativeBarController>();
            var presenter = UnityEngine.Object.FindFirstObjectByType<TurnOptionsPresenter>();

            Assert.IsNotNull(eventBus, "SampleScene must contain CombatEventBus.");
            Assert.IsNotNull(turnManager, "SampleScene must contain TurnManager.");
            Assert.IsNotNull(entityManager, "SampleScene must contain EntityManager.");
            Assert.IsNotNull(actionExecutor, "SampleScene must contain PlayerActionExecutor.");
            Assert.IsNotNull(targetingController, "SampleScene must contain TargetingController.");
            Assert.IsNotNull(initiativeBar, "SampleScene must contain InitiativeBarController.");
            Assert.IsNotNull(presenter, "SampleScene must contain TurnOptionsPresenter.");

            SetPrivateField(presenter, "eventBus", null);
            SetPrivateField(presenter, "turnManager", null);
            SetPrivateField(presenter, "entityManager", null);
            SetPrivateField(presenter, "actionExecutor", null);
            SetPrivateField(presenter, "targetingController", null);
            SetPrivateField(presenter, "initiativeBarController", null);
            SetPrivateField(presenter, "launcherCanvasGroup", null);
            SetPrivateField(presenter, "launcherRoot", null);
            SetPrivateField(presenter, "launcherButton", null);
            SetPrivateField(presenter, "panelRoot", null);
            SetPrivateField(presenter, "readyMoveButton", null);
            SetPrivateField(presenter, "readyAttackButton", null);
            SetPrivateField(presenter, "readyAnyButton", null);
            SetPrivateField(presenter, "delayButton", null);
            SetPrivateField(presenter, "returnNowButton", null);
            SetPrivateField(presenter, "skipButton", null);

            var staleLauncher = presenter.transform.Find("TurnOptionsLauncher");
            if (staleLauncher != null)
                UnityEngine.Object.DestroyImmediate(staleLauncher.gameObject);
            var stalePanel = presenter.transform.Find("TurnOptionsPanel");
            if (stalePanel != null)
                UnityEngine.Object.DestroyImmediate(stalePanel.gameObject);

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            Assert.AreSame(eventBus, GetPrivateField<CombatEventBus>(presenter, "eventBus"));
            Assert.AreSame(turnManager, GetPrivateField<TurnManager>(presenter, "turnManager"));
            Assert.AreSame(entityManager, GetPrivateField<EntityManager>(presenter, "entityManager"));
            Assert.AreSame(actionExecutor, GetPrivateField<PlayerActionExecutor>(presenter, "actionExecutor"));
            Assert.AreSame(targetingController, GetPrivateField<TargetingController>(presenter, "targetingController"));
            Assert.AreSame(initiativeBar, GetPrivateField<InitiativeBarController>(presenter, "initiativeBarController"));

            var launcherCanvasGroup = GetPrivateField<CanvasGroup>(presenter, "launcherCanvasGroup");
            var launcherRoot = GetPrivateField<RectTransform>(presenter, "launcherRoot");
            var launcherButton = GetPrivateField<Button>(presenter, "launcherButton");
            var panelRoot = GetPrivateField<RectTransform>(presenter, "panelRoot");
            var readyMoveButton = GetPrivateField<Button>(presenter, "readyMoveButton");
            var readyAttackButton = GetPrivateField<Button>(presenter, "readyAttackButton");
            var readyAnyButton = GetPrivateField<Button>(presenter, "readyAnyButton");
            var delayButton = GetPrivateField<Button>(presenter, "delayButton");
            var returnNowButton = GetPrivateField<Button>(presenter, "returnNowButton");
            var skipButton = GetPrivateField<Button>(presenter, "skipButton");

            Assert.AreSame(presenter.GetComponent<CanvasGroup>(), launcherCanvasGroup, "launcherCanvasGroup must reference presenter CanvasGroup.");
            Assert.AreEqual("TurnOptionsLauncher", launcherRoot.gameObject.name);
            Assert.AreEqual("TurnOptionsPanel", panelRoot.gameObject.name);
            Assert.AreSame(presenter.transform, launcherRoot.transform.parent);
            Assert.AreSame(presenter.transform, panelRoot.transform.parent);
            Assert.AreSame(launcherRoot, launcherButton.transform);

            Assert.AreEqual("ReadyMoveButton", readyMoveButton.gameObject.name);
            Assert.AreEqual("ReadyAttackButton", readyAttackButton.gameObject.name);
            Assert.AreEqual("ReadyAnyButton", readyAnyButton.gameObject.name);
            Assert.AreEqual("DelayButton", delayButton.gameObject.name);
            Assert.AreEqual("ReturnNowButton", returnNowButton.gameObject.name);
            Assert.AreEqual("SkipButton", skipButton.gameObject.name);
            Assert.AreSame(panelRoot, readyMoveButton.transform.parent);
            Assert.AreSame(panelRoot, readyAttackButton.transform.parent);
            Assert.AreSame(panelRoot, readyAnyButton.transform.parent);
            Assert.AreSame(panelRoot, delayButton.transform.parent);
            Assert.AreSame(panelRoot, returnNowButton.transform.parent);
            Assert.AreSame(panelRoot, skipButton.transform.parent);
        }

        [Test]
        public void SampleScene_CastSpellModeUi_Wired()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var actionBar = UnityEngine.Object.FindFirstObjectByType<ActionBarController>();
            Assert.IsNotNull(actionBar, "SampleScene must contain ActionBarController.");

            SetPrivateField(actionBar, "castSpellButton", null);
            SetPrivateField(actionBar, "castSpellButtonLabel", null);
            SetPrivateField(actionBar, "castSpellModeSelectorRoot", null);
            SetPrivateField(actionBar, "castSpellModeStandardButton", null);
            SetPrivateField(actionBar, "castSpellModeGlassButton", null);
            SetPrivateField(actionBar, "castSpellHighlight", null);

            var staleCastButton = actionBar.transform.Find("CastSpellButton");
            if (staleCastButton != null)
                UnityEngine.Object.DestroyImmediate(staleCastButton.gameObject);

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var castButton = GetPrivateField<Button>(actionBar, "castSpellButton");
            var castButtonLabel = GetPrivateField<Component>(actionBar, "castSpellButtonLabel");
            var castModeRoot = GetPrivateField<RectTransform>(actionBar, "castSpellModeSelectorRoot");
            var castModeStandardButton = GetPrivateField<Button>(actionBar, "castSpellModeStandardButton");
            var castModeGlassButton = GetPrivateField<Button>(actionBar, "castSpellModeGlassButton");
            var castHighlight = GetPrivateField<Image>(actionBar, "castSpellHighlight");

            Assert.AreEqual("CastSpellButton", castButton.gameObject.name);
            Assert.IsTrue(castButtonLabel.transform.IsChildOf(castButton.transform), "Cast label must remain under CastSpellButton hierarchy.");
            Assert.AreEqual("CastSpellModeSelector", castModeRoot.gameObject.name);
            Assert.AreSame(castButton.transform, castModeRoot.transform.parent, "Cast mode selector must be attached to CastSpellButton.");
            Assert.AreEqual("CastSpellModeStandardButton", castModeStandardButton.gameObject.name);
            Assert.AreEqual("CastSpellModeGlassButton", castModeGlassButton.gameObject.name);
            Assert.AreSame(castModeRoot, castModeStandardButton.transform.parent);
            Assert.AreSame(castModeRoot, castModeGlassButton.transform.parent);
            Assert.AreEqual("ActiveHighlight", castHighlight.gameObject.name);
            Assert.AreSame(castButton.transform, castHighlight.transform.parent);
        }

        [Test]
        public void SampleScene_LauncherLayoutRefs_Wired()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var actionBar = UnityEngine.Object.FindFirstObjectByType<ActionBarController>();
            Assert.IsNotNull(actionBar, "SampleScene must contain ActionBarController.");

            SetPrivateField(actionBar, "tacticsLauncherButton", null);
            SetPrivateField(actionBar, "strikePopupRoot", null);
            SetPrivateField(actionBar, "tacticsPopupRoot", null);
            SetPrivateField(actionBar, "strikePopupStrikeButton", null);

            var staleTacticsLauncherButton = actionBar.transform.Find("TacticsLauncherButton");
            if (staleTacticsLauncherButton != null)
                UnityEngine.Object.DestroyImmediate(staleTacticsLauncherButton.gameObject);

            var staleStrikePopupRoot = actionBar.transform.Find("StrikePopupRoot");
            if (staleStrikePopupRoot != null)
                UnityEngine.Object.DestroyImmediate(staleStrikePopupRoot.gameObject);

            var staleTacticsPopupRoot = actionBar.transform.Find("TacticsPopupRoot");
            if (staleTacticsPopupRoot != null)
                UnityEngine.Object.DestroyImmediate(staleTacticsPopupRoot.gameObject);

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var tacticsLauncherButton = GetPrivateField<Button>(actionBar, "tacticsLauncherButton");
            var strikePopupRoot = GetPrivateField<RectTransform>(actionBar, "strikePopupRoot");
            var tacticsPopupRoot = GetPrivateField<RectTransform>(actionBar, "tacticsPopupRoot");
            var strikePopupStrikeButton = GetPrivateField<Button>(actionBar, "strikePopupStrikeButton");

            Assert.AreEqual("TacticsLauncherButton", tacticsLauncherButton.gameObject.name);
            Assert.AreEqual("StrikePopupRoot", strikePopupRoot.gameObject.name);
            Assert.AreEqual("TacticsPopupRoot", tacticsPopupRoot.gameObject.name);
            Assert.AreEqual("StrikePopupStrikeButton", strikePopupStrikeButton.gameObject.name);

            Assert.AreSame(actionBar.transform, tacticsLauncherButton.transform.parent, "Tactics launcher button must be under ActionBar.");
            Assert.AreSame(actionBar.transform, strikePopupRoot.transform.parent, "StrikePopupRoot must be under ActionBar.");
            Assert.AreSame(actionBar.transform, tacticsPopupRoot.transform.parent, "TacticsPopupRoot must be under ActionBar.");
            Assert.AreSame(strikePopupRoot, strikePopupStrikeButton.transform.parent, "StrikePopupStrikeButton must be under StrikePopupRoot.");
        }

        [Test]
        public void SampleScene_StrikePopupHeaders_WiredByAutoFix()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var actionBar = UnityEngine.Object.FindFirstObjectByType<ActionBarController>();
            Assert.IsNotNull(actionBar, "SampleScene must contain ActionBarController.");

            var strikePopupRoot = actionBar.transform.Find("StrikePopupRoot");
            Assert.IsNotNull(strikePopupRoot, "SampleScene must contain StrikePopupRoot under ActionBar.");

            var attacksHeader = strikePopupRoot.Find("AttacksHeader");
            if (attacksHeader != null)
                UnityEngine.Object.DestroyImmediate(attacksHeader.gameObject);

            var maneuversHeader = strikePopupRoot.Find("ManeuversHeader");
            if (maneuversHeader != null)
                UnityEngine.Object.DestroyImmediate(maneuversHeader.gameObject);

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            attacksHeader = strikePopupRoot.Find("AttacksHeader");
            maneuversHeader = strikePopupRoot.Find("ManeuversHeader");

            Assert.IsNotNull(attacksHeader, "AutoFix must create StrikePopupRoot/AttacksHeader.");
            Assert.IsNotNull(maneuversHeader, "AutoFix must create StrikePopupRoot/ManeuversHeader.");

            var attacksLayout = attacksHeader.GetComponent<LayoutElement>();
            var maneuversLayout = maneuversHeader.GetComponent<LayoutElement>();
            Assert.IsNotNull(attacksLayout, "AttacksHeader must have LayoutElement.");
            Assert.IsNotNull(maneuversLayout, "ManeuversHeader must have LayoutElement.");

            var tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Assert.IsNotNull(tmpType, "TMPro.TextMeshProUGUI type not found.");

            var attacksLabel = attacksHeader.Find("Label");
            var maneuversLabel = maneuversHeader.Find("Label");
            Assert.IsNotNull(attacksLabel, "AttacksHeader must have Label child.");
            Assert.IsNotNull(maneuversLabel, "ManeuversHeader must have Label child.");

            var attacksText = attacksLabel.GetComponent(tmpType);
            var maneuversText = maneuversLabel.GetComponent(tmpType);
            Assert.IsNotNull(attacksText, "AttacksHeader label must be TMP.");
            Assert.IsNotNull(maneuversText, "ManeuversHeader label must be TMP.");

            var attacksTextValue = GetComponentText(attacksText);
            var maneuversTextValue = GetComponentText(maneuversText);
            Assert.AreEqual("Attacks:", attacksTextValue);
            Assert.AreEqual("Maneuvers:", maneuversTextValue);
        }

        [Test]
        [TestMustExpectAllLogs(false)]
        public void ValidateActionBarController_AidButtonAssigned_MissingAidRefs_EmitsErrors()
        {
            bool oldIgnoreFailingLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("ActionBarValidatorAidContractTest");
            try
            {
                var eventBus = root.AddComponent<CombatEventBus>();
                var turnManager = root.AddComponent<TurnManager>();
                var actionExecutor = root.AddComponent<PlayerActionExecutor>();
                var targetingController = root.AddComponent<TargetingController>();
                var canvasGroup = root.AddComponent<CanvasGroup>();
                var actionBar = root.AddComponent<ActionBarController>();

                var aidButton = CreateButton("AidButton", root.transform);

                SetPrivateField(actionBar, "eventBus", eventBus);
                SetPrivateField(actionBar, "turnManager", turnManager);
                SetPrivateField(actionBar, "actionExecutor", actionExecutor);
                SetPrivateField(actionBar, "targetingController", targetingController);
                SetPrivateField(actionBar, "canvasGroup", canvasGroup);

                SetPrivateField(actionBar, "aidButton", aidButton);
                SetPrivateField(actionBar, "aidHighlight", null);
                SetPrivateField(actionBar, "aidPreparedIndicatorRoot", null);
                SetPrivateField(actionBar, "aidPreparedIndicatorLabel", null);

                InvokePrivateValidateActionBarController(actionBar, out int errors, out int warnings);

                Assert.GreaterOrEqual(
                    errors,
                    3,
                    "When AidButton is assigned, missing aidHighlight/aidPreparedIndicatorRoot/aidPreparedIndicatorLabel must be validator errors.");
                Assert.GreaterOrEqual(warnings, 1, "Optional missing action-bar slots should still report warnings.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreFailingLogs;
            }
        }

        [Test]
        [TestMustExpectAllLogs(false)]
        public void ValidateTurnOptionsPresenter_MissingUiRefs_EmitsErrors()
        {
            bool oldIgnoreFailingLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("TurnOptionsValidatorContractTest");
            try
            {
                var eventBus = root.AddComponent<CombatEventBus>();
                var turnManager = root.AddComponent<TurnManager>();
                var entityManager = root.AddComponent<EntityManager>();
                var actionExecutor = root.AddComponent<PlayerActionExecutor>();
                var targetingController = root.AddComponent<TargetingController>();
                var initiativeBar = root.AddComponent<InitiativeBarController>();
                var presenter = root.AddComponent<TurnOptionsPresenter>();

                SetPrivateField(presenter, "eventBus", eventBus);
                SetPrivateField(presenter, "turnManager", turnManager);
                SetPrivateField(presenter, "entityManager", entityManager);
                SetPrivateField(presenter, "actionExecutor", actionExecutor);
                SetPrivateField(presenter, "targetingController", targetingController);
                SetPrivateField(presenter, "initiativeBarController", initiativeBar);

                SetPrivateField(presenter, "launcherCanvasGroup", null);
                SetPrivateField(presenter, "launcherRoot", null);
                SetPrivateField(presenter, "launcherButton", null);
                SetPrivateField(presenter, "panelRoot", null);
                SetPrivateField(presenter, "readyMoveButton", null);
                SetPrivateField(presenter, "readyAttackButton", null);
                SetPrivateField(presenter, "readyAnyButton", null);
                SetPrivateField(presenter, "delayButton", null);
                SetPrivateField(presenter, "returnNowButton", null);
                SetPrivateField(presenter, "skipButton", null);

                InvokePrivateValidateTurnOptionsPresenter(presenter, out int errors, out int warnings);

                Assert.GreaterOrEqual(errors, 10, "Missing TurnOptions UI refs must be validator errors.");
                Assert.AreEqual(0, warnings, "TurnOptions validator currently treats all required refs as errors.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreFailingLogs;
            }
        }

        [Test]
        public void AutoFix_WhenDelayUiOrchestratorMissing_CreatesAndWiresIt()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            var turnOptions = UnityEngine.Object.FindFirstObjectByType<TurnOptionsPresenter>();
            var initiativeBar = UnityEngine.Object.FindFirstObjectByType<InitiativeBarController>();
            Assert.IsNotNull(eventBus, "SampleScene must contain CombatEventBus.");
            Assert.IsNotNull(turnOptions, "SampleScene must contain TurnOptionsPresenter.");
            Assert.IsNotNull(initiativeBar, "SampleScene must contain InitiativeBarController.");

            var existing = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(existing[i]);
            }

            Assert.AreEqual(
                0,
                UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None).Length,
                "Test precondition: DelayUiOrchestrator must be missing before autofix.");

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var after = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            Assert.AreEqual(1, after.Length, "AutoFix must create exactly one DelayUiOrchestrator.");

            var orchestrator = after[0];
            Assert.AreSame(eventBus, GetPrivateField<CombatEventBus>(orchestrator, "eventBus"));
            Assert.AreSame(turnOptions, GetPrivateField<TurnOptionsPresenter>(orchestrator, "turnOptionsPresenter"));
            Assert.AreSame(initiativeBar, GetPrivateField<InitiativeBarController>(orchestrator, "initiativeBarController"));
        }

        [Test]
        public void AutoFix_WhenDelayUiOrchestratorAlreadyExists_DoesNotCreateDuplicate()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var existing = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(existing[i]);
            }

            var singletonGo = new GameObject("DelayUiOrchestrator_IdempotencyTest");
            singletonGo.AddComponent<DelayUiOrchestrator>();
            Assert.AreEqual(
                1,
                UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None).Length,
                "Test precondition: exactly one DelayUiOrchestrator must exist before autofix.");

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var after = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            Assert.AreEqual(1, after.Length, "AutoFix must keep DelayUiOrchestrator singleton idempotent.");
        }

        [Test]
        public void AutoFix_WhenDelayUiOrchestratorExistsWithNullRefs_RewiresWithoutDuplicate()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            var turnOptions = UnityEngine.Object.FindFirstObjectByType<TurnOptionsPresenter>();
            var initiativeBar = UnityEngine.Object.FindFirstObjectByType<InitiativeBarController>();
            Assert.IsNotNull(eventBus, "SampleScene must contain CombatEventBus.");
            Assert.IsNotNull(turnOptions, "SampleScene must contain TurnOptionsPresenter.");
            Assert.IsNotNull(initiativeBar, "SampleScene must contain InitiativeBarController.");

            var all = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            if (all.Length == 0)
            {
                var go = new GameObject("DelayUiOrchestrator_RewireTest");
                go.AddComponent<DelayUiOrchestrator>();
                all = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            }

            for (int i = 1; i < all.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(all[i]);
            }

            var orchestrator = UnityEngine.Object.FindFirstObjectByType<DelayUiOrchestrator>();
            Assert.IsNotNull(orchestrator, "Test precondition: one DelayUiOrchestrator must exist.");

            SetPrivateField(orchestrator, "eventBus", null);
            SetPrivateField(orchestrator, "turnOptionsPresenter", null);
            SetPrivateField(orchestrator, "initiativeBarController", null);

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var after = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            Assert.AreEqual(1, after.Length, "AutoFix must not create duplicate DelayUiOrchestrator while rewiring.");

            var rewired = after[0];
            Assert.AreSame(eventBus, GetPrivateField<CombatEventBus>(rewired, "eventBus"));
            Assert.AreSame(turnOptions, GetPrivateField<TurnOptionsPresenter>(rewired, "turnOptionsPresenter"));
            Assert.AreSame(initiativeBar, GetPrivateField<InitiativeBarController>(rewired, "initiativeBarController"));
        }

        [Test]
        public void AutoFix_WhenReadyStrikeEventBinderMissing_CreatesAndWiresOnTurnManager()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var turnManager = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
            var eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            Assert.IsNotNull(turnManager, "SampleScene must contain TurnManager.");
            Assert.IsNotNull(eventBus, "SampleScene must contain CombatEventBus.");

            var existing = UnityEngine.Object.FindObjectsByType<ReadyStrikeEventBinder>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(existing[i]);
            }

            Assert.AreEqual(
                0,
                UnityEngine.Object.FindObjectsByType<ReadyStrikeEventBinder>(FindObjectsSortMode.None).Length,
                "Test precondition: ReadyStrikeEventBinder must be missing before autofix.");

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var after = UnityEngine.Object.FindObjectsByType<ReadyStrikeEventBinder>(FindObjectsSortMode.None);
            Assert.AreEqual(1, after.Length, "AutoFix must create exactly one ReadyStrikeEventBinder.");

            var binder = after[0];
            Assert.AreSame(turnManager.gameObject, binder.gameObject, "ReadyStrikeEventBinder must be created on TurnManager GameObject.");
            Assert.AreSame(turnManager, GetPrivateField<TurnManager>(binder, "turnManager"));
            Assert.AreSame(eventBus, GetPrivateField<CombatEventBus>(binder, "eventBus"));
        }

        [Test]
        public void Validator_MissingEncounterActorId_OnAliveCombatants_EmitsWarnings()
        {
            var entities = new List<EntityData>
            {
                new EntityData
                {
                    Name = "Fighter",
                    Team = Team.Player,
                    CurrentHP = 20,
                    EncounterActorId = ""
                },
                new EntityData
                {
                    Name = "Goblin_1",
                    Team = Team.Enemy,
                    CurrentHP = 6,
                    EncounterActorId = "   "
                },
                new EntityData
                {
                    Name = "DeadGoblin",
                    Team = Team.Enemy,
                    CurrentHP = 0,
                    EncounterActorId = ""
                },
                new EntityData
                {
                    Name = "NeutralNpc",
                    Team = Team.Neutral,
                    CurrentHP = 10,
                    EncounterActorId = ""
                }
            };

            LogAssert.Expect(LogType.Warning, new Regex(@"\[PF2eValidator\] Entity 'Fighter' \(Player\) has empty EncounterActorId\."));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[PF2eValidator\] Entity 'Goblin_1' \(Enemy\) has empty EncounterActorId\."));

            int warnings = InvokePrivateValidatorMethodForEncounterActorIds(entities);
            Assert.AreEqual(2, warnings, "Only alive Player/Enemy entities with empty EncounterActorId should be warned.");
        }

        private static Button CreateButton(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Button>();
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        private static Component CreateTmpLabel(string name, Transform parent, string text)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Assert.IsNotNull(tmpType, "TMPro.TextMeshProUGUI type not found.");

            var label = go.AddComponent(tmpType) as Component;
            Assert.IsNotNull(label, "Failed to add TMP label.");

            var textProperty = tmpType.GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            textProperty?.SetValue(label, text);

            return label;
        }

        private static string GetComponentText(Component textComponent)
        {
            if (textComponent == null)
                return string.Empty;

            var textProperty = textComponent.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            if (textProperty != null)
            {
                return textProperty.GetValue(textComponent) as string ?? string.Empty;
            }

            return string.Empty;
        }

        private static void InvokePrivateValidatorMethod(string methodName)
        {
            var validatorType = FindTypeByName("PF2eSceneDependencyValidator");
            Assert.IsNotNull(validatorType, "PF2eSceneDependencyValidator type not found.");

            var method = validatorType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, $"{methodName} not found on PF2eSceneDependencyValidator.");

            method.Invoke(null, null);
        }

        private static void InvokePrivateValidatorMethodWithBoolArg(string methodName, bool arg)
        {
            var validatorType = FindTypeByName("PF2eSceneDependencyValidator");
            Assert.IsNotNull(validatorType, "PF2eSceneDependencyValidator type not found.");

            var method = validatorType.GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(bool) },
                null);
            Assert.IsNotNull(method, $"{methodName}(bool) not found on PF2eSceneDependencyValidator.");

            method.Invoke(null, new object[] { arg });
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");

            var value = field.GetValue(target) as T;
            Assert.IsNotNull(value, $"Field '{fieldName}' is null on {target.GetType().Name}.");
            return value;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateInstanceMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"{methodName} not found on {target.GetType().Name}.");
            method.Invoke(target, null);
        }

        private static void InvokePrivateValidateActionBarController(ActionBarController actionBar, out int errors, out int warnings)
        {
            var validatorType = FindTypeByName("PF2eSceneDependencyValidator");
            Assert.IsNotNull(validatorType, "PF2eSceneDependencyValidator type not found.");

            var method = validatorType.GetMethod("ValidateActionBarController", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "ValidateActionBarController not found on PF2eSceneDependencyValidator.");

            object[] args = { actionBar, 0, 0 };
            method.Invoke(null, args);

            errors = (int)args[1];
            warnings = (int)args[2];
        }

        private static void InvokePrivateValidateTurnOptionsPresenter(TurnOptionsPresenter presenter, out int errors, out int warnings)
        {
            var validatorType = FindTypeByName("PF2eSceneDependencyValidator");
            Assert.IsNotNull(validatorType, "PF2eSceneDependencyValidator type not found.");

            var method = validatorType.GetMethod("ValidateTurnOptionsPresenter", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "ValidateTurnOptionsPresenter not found on PF2eSceneDependencyValidator.");

            object[] args = { presenter, 0, 0 };
            method.Invoke(null, args);

            errors = (int)args[1];
            warnings = (int)args[2];
        }

        private static Type FindTypeByName(string typeName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException e)
                    {
                        return e.Types.Where(t => t != null);
                    }
                })
                .FirstOrDefault(t => t != null && t.Name == typeName);
        }

        private static int InvokePrivateValidatorMethodForEncounterActorIds(IEnumerable<EntityData> entities)
        {
            var validatorType = FindTypeByName("PF2eSceneDependencyValidator");
            Assert.IsNotNull(validatorType, "PF2eSceneDependencyValidator type not found.");

            var method = validatorType.GetMethod(
                "WarnMissingEncounterActorIds",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(IEnumerable<EntityData>), typeof(UnityEngine.Object) },
                null);
            Assert.IsNotNull(method, "WarnMissingEncounterActorIds(IEnumerable<EntityData>, Object) not found.");

            object result = method.Invoke(null, new object[] { entities, null });
            Assert.IsNotNull(result, "WarnMissingEncounterActorIds returned null.");
            return (int)result;
        }
    }
}
