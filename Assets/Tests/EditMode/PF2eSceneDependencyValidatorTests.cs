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
        public void SampleScene_ReadyModeUi_Wired()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var actionBar = UnityEngine.Object.FindFirstObjectByType<ActionBarController>();
            Assert.IsNotNull(actionBar, "SampleScene must contain ActionBarController.");

            SetPrivateField(actionBar, "readyButtonLabel", null);
            SetPrivateField(actionBar, "readyModeSelectorRoot", null);
            SetPrivateField(actionBar, "readyModeMoveButton", null);
            SetPrivateField(actionBar, "readyModeAttackButton", null);
            SetPrivateField(actionBar, "readyModeAnyButton", null);

            var readyButton = GetPrivateField<Button>(actionBar, "readyButton");
            if (readyButton != null)
            {
                var staleSelector = readyButton.transform.Find("ReadyModeSelector");
                if (staleSelector != null)
                    UnityEngine.Object.DestroyImmediate(staleSelector.gameObject);
            }

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var selectorRoot = GetPrivateField<RectTransform>(actionBar, "readyModeSelectorRoot");
            var moveButton = GetPrivateField<Button>(actionBar, "readyModeMoveButton");
            var attackButton = GetPrivateField<Button>(actionBar, "readyModeAttackButton");
            var anyButton = GetPrivateField<Button>(actionBar, "readyModeAnyButton");
            var readyLabel = GetPrivateField<Component>(actionBar, "readyButtonLabel");

            Assert.IsNotNull(readyLabel, "ActionBarController must have Ready button label wired by scene/autofix.");
            Assert.IsNotNull(selectorRoot, "ActionBarController must have Ready mode selector root wired by scene/autofix.");
            Assert.IsNotNull(moveButton, "ActionBarController must have Ready mode Move button wired by scene/autofix.");
            Assert.IsNotNull(attackButton, "ActionBarController must have Ready mode Attack button wired by scene/autofix.");
            Assert.IsNotNull(anyButton, "ActionBarController must have Ready mode Any button wired by scene/autofix.");
            Assert.IsTrue(readyLabel.transform.IsChildOf(readyButton.transform), "Ready label must remain under ReadyButton hierarchy.");
            Assert.AreEqual("ReadyModeSelector", selectorRoot.gameObject.name);
            Assert.AreEqual("ReadyModeMoveButton", moveButton.gameObject.name);
            Assert.AreEqual("ReadyModeAttackButton", attackButton.gameObject.name);
            Assert.AreEqual("ReadyModeAnyButton", anyButton.gameObject.name);
            Assert.AreSame(selectorRoot, moveButton.transform.parent);
            Assert.AreSame(selectorRoot, attackButton.transform.parent);
            Assert.AreSame(selectorRoot, anyButton.transform.parent);
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
        public void ValidateActionBarController_ReadyButtonAssigned_MissingReadyModeRefs_EmitsErrors()
        {
            bool oldIgnoreFailingLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("ActionBarValidatorReadyModeContractTest");
            try
            {
                var eventBus = root.AddComponent<CombatEventBus>();
                var turnManager = root.AddComponent<TurnManager>();
                var actionExecutor = root.AddComponent<PlayerActionExecutor>();
                var targetingController = root.AddComponent<TargetingController>();
                var canvasGroup = root.AddComponent<CanvasGroup>();
                var actionBar = root.AddComponent<ActionBarController>();

                var readyButtonGo = new GameObject("ReadyButton", typeof(RectTransform), typeof(Image), typeof(Button));
                readyButtonGo.transform.SetParent(root.transform, false);
                var readyButton = readyButtonGo.GetComponent<Button>();

                SetPrivateField(actionBar, "eventBus", eventBus);
                SetPrivateField(actionBar, "turnManager", turnManager);
                SetPrivateField(actionBar, "actionExecutor", actionExecutor);
                SetPrivateField(actionBar, "targetingController", targetingController);
                SetPrivateField(actionBar, "canvasGroup", canvasGroup);
                SetPrivateField(actionBar, "readyButton", readyButton);
                SetPrivateField(actionBar, "readyModeSelectorRoot", null);
                SetPrivateField(actionBar, "readyModeMoveButton", null);
                SetPrivateField(actionBar, "readyModeAttackButton", null);
                SetPrivateField(actionBar, "readyModeAnyButton", null);

                InvokePrivateValidateActionBarController(actionBar, out int errors, out int warnings);

                Assert.GreaterOrEqual(
                    errors,
                    6,
                    "When ReadyButton is assigned, missing Ready wiring refs (label/highlight/mode selector) must be validator errors.");
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
        public void ValidateActionBarController_TurnOptionsPresenterPresent_MissingReadyModeRefs_DoNotEmitErrors()
        {
            bool oldIgnoreFailingLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("ActionBarValidatorTurnOptionsContractTest");
            try
            {
                var eventBus = root.AddComponent<CombatEventBus>();
                var turnManager = root.AddComponent<TurnManager>();
                var entityManager = root.AddComponent<EntityManager>();
                var actionExecutor = root.AddComponent<PlayerActionExecutor>();
                var targetingController = root.AddComponent<TargetingController>();
                var canvasGroup = root.AddComponent<CanvasGroup>();
                var actionBar = root.AddComponent<ActionBarController>();
                root.AddComponent<TurnOptionsPresenter>();

                var aidButton = CreateButton("AidButton", root.transform);
                var aidHighlight = CreateImage("AidHighlight", aidButton.transform);
                var aidBadgeRoot = new GameObject("AidPreparedBadge", typeof(RectTransform));
                aidBadgeRoot.transform.SetParent(aidButton.transform, false);
                var aidBadgeLabel = CreateTmpLabel("Label", aidBadgeRoot.transform, string.Empty);

                var castButton = CreateButton("CastSpellButton", root.transform);
                var castButtonLabel = CreateTmpLabel("Label", castButton.transform, "Cast");
                var castSelector = new GameObject("CastSpellModeSelector", typeof(RectTransform)).GetComponent<RectTransform>();
                castSelector.transform.SetParent(castButton.transform, false);
                var castStandard = CreateButton("CastSpellModeStandardButton", castSelector);
                var castGlass = CreateButton("CastSpellModeGlassButton", castSelector);

                SetPrivateField(actionBar, "eventBus", eventBus);
                SetPrivateField(actionBar, "entityManager", entityManager);
                SetPrivateField(actionBar, "turnManager", turnManager);
                SetPrivateField(actionBar, "actionExecutor", actionExecutor);
                SetPrivateField(actionBar, "targetingController", targetingController);
                SetPrivateField(actionBar, "canvasGroup", canvasGroup);

                SetPrivateField(actionBar, "aidButton", aidButton);
                SetPrivateField(actionBar, "aidHighlight", aidHighlight);
                SetPrivateField(actionBar, "aidPreparedIndicatorRoot", aidBadgeRoot);
                SetPrivateField(actionBar, "aidPreparedIndicatorLabel", aidBadgeLabel);

                // Intentionally keep Ready refs null: TurnOptionsPresenter owns turn-management UI.
                SetPrivateField(actionBar, "readyButton", null);
                SetPrivateField(actionBar, "readyButtonLabel", null);
                SetPrivateField(actionBar, "readyHighlight", null);
                SetPrivateField(actionBar, "readyModeSelectorRoot", null);
                SetPrivateField(actionBar, "readyModeMoveButton", null);
                SetPrivateField(actionBar, "readyModeAttackButton", null);
                SetPrivateField(actionBar, "readyModeAnyButton", null);

                SetPrivateField(actionBar, "castSpellButton", castButton);
                SetPrivateField(actionBar, "castSpellButtonLabel", castButtonLabel);
                SetPrivateField(actionBar, "castSpellModeSelectorRoot", castSelector);
                SetPrivateField(actionBar, "castSpellModeStandardButton", castStandard);
                SetPrivateField(actionBar, "castSpellModeGlassButton", castGlass);

                InvokePrivateValidateActionBarController(actionBar, out int errors, out int warnings);

                Assert.AreEqual(0, errors, "With TurnOptionsPresenter present, missing Ready refs must not be validator errors.");
                Assert.GreaterOrEqual(warnings, 1, "Optional non-turn-management slots may still emit warnings.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreFailingLogs;
            }
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
                var readyButton = CreateButton("ReadyButton", root.transform);
                var readyLabel = CreateTmpLabel("Label", readyButton.transform, "Ready");
                var readyHighlight = CreateImage("ActiveHighlight", readyButton.transform);
                var selectorRoot = new GameObject("ReadyModeSelector", typeof(RectTransform)).GetComponent<RectTransform>();
                selectorRoot.transform.SetParent(readyButton.transform, false);
                var moveButton = CreateButton("ReadyModeMoveButton", selectorRoot);
                var attackButton = CreateButton("ReadyModeAttackButton", selectorRoot);
                var anyButton = CreateButton("ReadyModeAnyButton", selectorRoot);

                SetPrivateField(actionBar, "eventBus", eventBus);
                SetPrivateField(actionBar, "turnManager", turnManager);
                SetPrivateField(actionBar, "actionExecutor", actionExecutor);
                SetPrivateField(actionBar, "targetingController", targetingController);
                SetPrivateField(actionBar, "canvasGroup", canvasGroup);

                SetPrivateField(actionBar, "aidButton", aidButton);
                SetPrivateField(actionBar, "aidHighlight", null);
                SetPrivateField(actionBar, "aidPreparedIndicatorRoot", null);
                SetPrivateField(actionBar, "aidPreparedIndicatorLabel", null);

                SetPrivateField(actionBar, "readyButton", readyButton);
                SetPrivateField(actionBar, "readyButtonLabel", readyLabel);
                SetPrivateField(actionBar, "readyHighlight", readyHighlight);
                SetPrivateField(actionBar, "readyModeSelectorRoot", selectorRoot);
                SetPrivateField(actionBar, "readyModeMoveButton", moveButton);
                SetPrivateField(actionBar, "readyModeAttackButton", attackButton);
                SetPrivateField(actionBar, "readyModeAnyButton", anyButton);

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
        public void AutoFix_WhenDelayUiOrchestratorMissing_CreatesAndWiresIt()
        {
            Assert.IsTrue(System.IO.File.Exists(SampleScenePath), $"Missing scene: {SampleScenePath}");

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var eventBus = UnityEngine.Object.FindFirstObjectByType<CombatEventBus>();
            var actionBar = UnityEngine.Object.FindFirstObjectByType<ActionBarController>();
            var initiativeBar = UnityEngine.Object.FindFirstObjectByType<InitiativeBarController>();
            Assert.IsNotNull(eventBus, "SampleScene must contain CombatEventBus.");
            Assert.IsNotNull(actionBar, "SampleScene must contain ActionBarController.");
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
            Assert.AreSame(actionBar, GetPrivateField<ActionBarController>(orchestrator, "actionBarController"));
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
            var actionBar = UnityEngine.Object.FindFirstObjectByType<ActionBarController>();
            var initiativeBar = UnityEngine.Object.FindFirstObjectByType<InitiativeBarController>();
            Assert.IsNotNull(eventBus, "SampleScene must contain CombatEventBus.");
            Assert.IsNotNull(actionBar, "SampleScene must contain ActionBarController.");
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
            SetPrivateField(orchestrator, "actionBarController", null);
            SetPrivateField(orchestrator, "initiativeBarController", null);

            InvokePrivateValidatorMethodWithBoolArg("RunAutoFix", false);

            var after = UnityEngine.Object.FindObjectsByType<DelayUiOrchestrator>(FindObjectsSortMode.None);
            Assert.AreEqual(1, after.Length, "AutoFix must not create duplicate DelayUiOrchestrator while rewiring.");

            var rewired = after[0];
            Assert.AreSame(eventBus, GetPrivateField<CombatEventBus>(rewired, "eventBus"));
            Assert.AreSame(actionBar, GetPrivateField<ActionBarController>(rewired, "actionBarController"));
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
