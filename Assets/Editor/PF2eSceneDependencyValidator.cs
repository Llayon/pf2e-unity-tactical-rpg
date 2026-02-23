#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.Presentation;
using PF2e.TurnSystem;

public static class PF2eSceneDependencyValidator
{
    private delegate void ValidatorDelegate<T>(T obj, ref int errors, ref int warnings);

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("Tools/PF2e/Validate Scene Dependencies")]
    public static void Validate_WithDialog()
    {
        RunValidation(showDialog: true);
    }

    [MenuItem("Tools/PF2e/Validate Scene Dependencies (No Dialog)")]
    public static void Validate_NoDialog()
    {
        RunValidation(showDialog: false);
    }

    [MenuItem("Tools/PF2e/Auto-Fix Scene Dependencies (Safe)")]
    public static void AutoFix_Safe()
    {
        RunAutoFix();
    }

    /// <summary>
    /// For Unity batchmode: -executeMethod PF2eSceneDependencyValidator.Validate_NoDialog_ExecuteMethod
    /// </summary>
    public static void Validate_NoDialog_ExecuteMethod()
    {
        RunValidation(showDialog: false);
    }

    private static void RunValidation(bool showDialog)
    {
        int errors = 0;
        int warnings = 0;

        Debug.Log($"[PF2eValidator] Validating scene: {SceneManager.GetActiveScene().name}");

        // ---- Required field checks (all instances in scene) ----
        errors += ValidateAll<GridManager>(ValidateGridManager);
        errors += ValidateAll<EntityManager>(ValidateEntityManager);
        errors += ValidateAll<TurnManager>(ValidateTurnManager);
        errors += ValidateAll<CombatEventBus>(ValidateCombatEventBus);
        errors += ValidateAll<TurnLogForwarder>(ValidateTurnLogForwarder);
        errors += ValidateAll<StrideLogForwarder>(ValidateStrideLogForwarder);
        errors += ValidateAll<StrikeLogForwarder>(ValidateStrikeLogForwarder);
        errors += ValidateAll<SkillCheckLogForwarder>(ValidateSkillCheckLogForwarder);
        errors += ValidateAll<PlayerActionExecutor>(ValidatePlayerActionExecutor);
        errors += ValidateAll<StrideAction>(ValidateStrideAction);
        errors += ValidateAll<StrikeAction>(ValidateStrikeAction);
        errors += ValidateAll<TargetingController>(ValidateTargetingController);
        errors += ValidateAll<TurnInputController>(ValidateTurnInputController);
        errors += ValidateAll<EntityMover>(ValidateEntityMover);
        errors += ValidateAll<MovementZoneVisualizer>(ValidateMovementZoneVisualizer);
        errors += ValidateAll<GridInteraction>(ValidateGridInteraction);
        errors += ValidateAll<CombatStarter>(ValidateCombatStarter);
        errors += ValidateAll<TurnUIController>(ValidateTurnUIController);
        errors += ValidateAll<EncounterEndPanelController>(ValidateEncounterEndPanelController);
        errors += ValidateAll<EncounterFlowController>(ValidateEncounterFlowController);
        errors += ValidateAll<CombatLogController>(ValidateCombatLogController);
        errors += ValidateAll<FloatingDamageUI>(ValidateFloatingDamageUI);
        errors += ValidateAll<InitiativeBarController>(ValidateInitiativeBarController);
        errors += ValidateAll<ConditionLogForwarder>(ValidateConditionLogForwarder);
        errors += ValidateAll<StandAction>(ValidateStandAction);
                errors += ValidateAll<TripAction>(ValidateTripAction);
        errors += ValidateAll<ShoveAction>(ValidateShoveAction);
        errors += ValidateAll<GrappleAction>(ValidateGrappleAction);
        errors += ValidateAll<DemoralizeAction>(ValidateDemoralizeAction);
        errors += ValidateAll<RaiseShieldAction>(ValidateRaiseShieldAction);
        errors += ValidateAll<ShieldBlockAction>(ValidateShieldBlockAction);
        errors += ValidateAll<AITurnController>(ValidateAITurnController);
        errors += ValidateAll<ReactionPromptController>(ValidateReactionPromptController);

        // Strict singletons for core managers (combat scene expectation)
        errors += ErrorIfMoreThanOne<GridManager>();
        errors += ErrorIfMoreThanOne<EntityManager>();
        errors += ErrorIfMoreThanOne<TurnManager>();
        errors += ErrorIfMoreThanOne<CombatEventBus>();
        errors += ErrorIfMoreThanOne<TurnLogForwarder>();
        errors += ErrorIfMoreThanOne<StrideLogForwarder>();
        errors += ErrorIfMoreThanOne<StrikeLogForwarder>();
        errors += ErrorIfMoreThanOne<SkillCheckLogForwarder>();
        errors += ErrorIfMoreThanOne<FloatingDamageUI>();
        errors += ErrorIfMoreThanOne<EncounterEndPanelController>();
        errors += ErrorIfMoreThanOne<EncounterFlowController>();
        errors += ErrorIfMoreThanOne<InitiativeBarController>();
        errors += ErrorIfMoreThanOne<ConditionLogForwarder>();
        errors += ErrorIfMoreThanOne<StandAction>();
                errors += ErrorIfMoreThanOne<TripAction>();
        errors += ErrorIfMoreThanOne<ShoveAction>();
        errors += ErrorIfMoreThanOne<GrappleAction>();
        errors += ErrorIfMoreThanOne<DemoralizeAction>();
        errors += ErrorIfMoreThanOne<RaiseShieldAction>();
        errors += ErrorIfMoreThanOne<ShieldBlockAction>();
        errors += ErrorIfMoreThanOne<ReactionPromptController>();

        // Strict singletons for combat controller components (also expected 1 per scene)
        errors += ErrorIfMoreThanOne<PlayerActionExecutor>();
        errors += ErrorIfMoreThanOne<StrideAction>();
        errors += ErrorIfMoreThanOne<StrikeAction>();
        errors += ErrorIfMoreThanOne<EntityMover>();
        errors += ErrorIfMoreThanOne<TargetingController>();
        errors += ErrorIfMoreThanOne<TurnInputController>();
        errors += ErrorIfMoreThanOne<AITurnController>();

        // Presence warnings (scene might be incomplete / wrong setup)
        warnings += WarnIfNone<GridManager>();
        warnings += WarnIfNone<EntityManager>();
        warnings += WarnIfNone<TurnManager>();
        warnings += WarnIfNone<AITurnController>();
                warnings += WarnIfNone<TripAction>();
        warnings += WarnIfNone<ShoveAction>();
        warnings += WarnIfNone<GrappleAction>();
        warnings += WarnIfNone<DemoralizeAction>();
        warnings += WarnIfNone<RaiseShieldAction>();
        warnings += WarnIfNone<ShieldBlockAction>();
        warnings += WarnIfNone<ReactionPromptController>();
        warnings += WarnIfNone<EncounterEndPanelController>();
        warnings += WarnIfNone<EncounterFlowController>();
        warnings += WarnIfAny<ConditionTickForwarder>(
            "ConditionTickForwarder is deprecated and should be removed from scene.");

        string summary = $"[PF2eValidator] Done. Errors: {errors}, Warnings: {warnings}";
        if (errors > 0) Debug.LogError(summary);
        else if (warnings > 0) Debug.LogWarning(summary);
        else Debug.Log(summary);

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "PF2e Scene Dependency Validator",
                $"Scene: {SceneManager.GetActiveScene().name}\n\nErrors: {errors}\nWarnings: {warnings}\n\nCheck Console for details.",
                "OK");
        }
    }

    // ----------------- Validators per component -----------------

    private static void ValidateGridManager(GridManager gm, ref int errors, ref int warnings)
    {
        errors += RequireRef(gm, "gridConfig", "GridConfig");
    }

    private static void ValidateEntityManager(EntityManager em, ref int errors, ref int warnings)
    {
        errors += RequireRef(em, "gridManager", "GridManager");
        errors += RequireRef(em, "eventBus",    "CombatEventBus");
    }

    private static void ValidateTurnManager(TurnManager tm, ref int errors, ref int warnings)
    {
        errors += RequireRef(tm, "entityManager", "EntityManager");
        errors += RequireRef(tm, "eventBus", "CombatEventBus");
    }

    private static void ValidateCombatEventBus(CombatEventBus bus, ref int errors, ref int warnings)
    {
        // no required fields
    }

    private static void ValidatePlayerActionExecutor(PlayerActionExecutor ex, ref int errors, ref int warnings)
    {
        errors += RequireRef(ex, "turnManager", "TurnManager");
        errors += RequireRef(ex, "entityManager", "EntityManager");
        errors += RequireRef(ex, "strideAction", "StrideAction");
        errors += RequireRef(ex, "strikeAction", "StrikeAction");
        errors += RequireRef(ex, "shieldBlockAction", "ShieldBlockAction");
                warnings += WarnRef(ex, "tripAction", "TripAction");
        warnings += WarnRef(ex, "shoveAction", "ShoveAction");
        warnings += WarnRef(ex, "grappleAction", "GrappleAction");
        warnings += WarnRef(ex, "demoralizeAction", "DemoralizeAction");
        warnings += WarnRef(ex, "reactionPromptController", "ReactionPromptController");
    }

    private static void ValidateStrideAction(StrideAction sa, ref int errors, ref int warnings)
    {
        errors += RequireRef(sa, "entityManager", "EntityManager");
        errors += RequireRef(sa, "entityMover", "EntityMover");
        errors += RequireRef(sa, "gridManager", "GridManager");
        // eventBus is optional (warning only in OnValidate)
    }

    private static void ValidateStrikeAction(StrikeAction sa, ref int errors, ref int warnings)
    {
        errors += RequireRef(sa, "entityManager", "EntityManager");
        errors += RequireRef(sa, "eventBus", "CombatEventBus");
    }

    private static void ValidateTargetingController(TargetingController tc, ref int errors, ref int warnings)
    {
        errors += RequireRef(tc, "actionExecutor", "PlayerActionExecutor");
        errors += RequireRef(tc, "entityManager",  "EntityManager");
        errors += RequireRef(tc, "turnManager",    "TurnManager");
        errors += RequireRef(tc, "eventBus",       "CombatEventBus");
    }

    private static void ValidateTurnInputController(TurnInputController tic, ref int errors, ref int warnings)
    {
        errors += RequireRef(tic, "turnManager",         "TurnManager");
        errors += RequireRef(tic, "gridManager",         "GridManager");
        errors += RequireRef(tic, "actionExecutor",      "PlayerActionExecutor");
        errors += RequireRef(tic, "targetingController", "TargetingController");
    }

    private static void ValidateEntityMover(EntityMover mover, ref int errors, ref int warnings)
    {
        errors += RequireRef(mover, "entityManager", "EntityManager");
    }

    private static void ValidateMovementZoneVisualizer(MovementZoneVisualizer mz, ref int errors, ref int warnings)
    {
        errors += RequireRef(mz, "entityManager", "EntityManager");
        errors += RequireRef(mz, "gridManager", "GridManager");
        errors += RequireRef(mz, "highlightPool", "CellHighlightPool");
        errors += RequireRef(mz, "turnManager", "TurnManager");
        errors += RequireRef(mz, "eventBus", "CombatEventBus");
        errors += RequireRef(mz, "strideAction", "StrideAction");
        errors += RequireRef(mz, "entityMover", "EntityMover");
    }

    private static void ValidateGridInteraction(GridInteraction gi, ref int errors, ref int warnings)
    {
        errors += RequireRef(gi, "entityManager", "EntityManager");
        errors += RequireRef(gi, "turnManager", "TurnManager");
    }

    private static void ValidateCombatStarter(CombatStarter cs, ref int errors, ref int warnings)
    {
        errors += RequireRef(cs, "turnManager", "TurnManager");
    }

    private static void ValidateTurnUIController(TurnUIController ui, ref int errors, ref int warnings)
    {
        errors += RequireRef(ui, "eventBus", "CombatEventBus");
        errors += RequireRef(ui, "entityManager", "EntityManager");
        errors += RequireRef(ui, "turnInputController", "TurnInputController");

        errors += RequireRef(ui, "roundText", "TextMeshProUGUI");
        errors += RequireRef(ui, "actorText", "TextMeshProUGUI");
        errors += RequireRef(ui, "actionsText", "TextMeshProUGUI");
        errors += RequireRef(ui, "endTurnButton", "Button");
    }

    private static void ValidateEncounterEndPanelController(EncounterEndPanelController c, ref int errors, ref int warnings)
    {
        errors += RequireRef(c, "eventBus", "CombatEventBus");
        errors += RequireRef(c, "panelCanvasGroup", "CanvasGroup");
        errors += RequireRef(c, "titleText", "TextMeshProUGUI");
        errors += RequireRef(c, "subtitleText", "TextMeshProUGUI");
        errors += RequireRef(c, "restartButton", "Button");
        errors += RequireRef(c, "closeButton", "Button");
    }

    private static void ValidateEncounterFlowController(EncounterFlowController c, ref int errors, ref int warnings)
    {
        errors += RequireRef(c, "turnManager", "TurnManager");
        errors += RequireRef(c, "entityManager", "EntityManager");
        errors += RequireRef(c, "eventBus", "CombatEventBus");
        errors += RequireRef(c, "rootCanvas", "Canvas");
        errors += RequireRef(c, "startEncounterButton", "Button");
        errors += RequireRef(c, "endEncounterButton", "Button");
    }

    private static void ValidateStrikeLogForwarder(StrikeLogForwarder f, ref int errors, ref int warnings)
    {
        errors += RequireRef(f, "eventBus", "CombatEventBus");
        errors += RequireRef(f, "entityManager", "EntityManager");
    }

    private static void ValidateTurnLogForwarder(TurnLogForwarder f, ref int errors, ref int warnings)
    {
        errors += RequireRef(f, "eventBus", "CombatEventBus");
        errors += RequireRef(f, "entityManager", "EntityManager");
    }

    private static void ValidateStrideLogForwarder(StrideLogForwarder f, ref int errors, ref int warnings)
    {
        errors += RequireRef(f, "eventBus", "CombatEventBus");
    }

    private static void ValidateFloatingDamageUI(FloatingDamageUI f, ref int errors, ref int warnings)
    {
        errors += RequireRef(f, "eventBus",     "CombatEventBus");
        errors += RequireRef(f, "entityManager","EntityManager");
        // textPrefab is optional
    }

    private static void ValidateSkillCheckLogForwarder(SkillCheckLogForwarder f, ref int errors, ref int warnings)
    {
        errors += RequireRef(f, "eventBus", "CombatEventBus");
        errors += RequireRef(f, "entityManager", "EntityManager");
    }

    private static void ValidateInitiativeBarController(InitiativeBarController c, ref int errors, ref int warnings)
    {
        errors += RequireRef(c, "turnManager",    "TurnManager");
        errors += RequireRef(c, "entityManager",  "EntityManager");
        errors += RequireRef(c, "eventBus",       "CombatEventBus");
        errors += RequireRef(c, "panelRoot",      "GameObject");
        errors += RequireRef(c, "roundLabel",     "TextMeshProUGUI");
        errors += RequireRef(c, "slotsContainer", "Transform");
        errors += RequireRef(c, "slotPrefab",     "InitiativeSlot");
    }

    private static void ValidateConditionLogForwarder(ConditionLogForwarder f, ref int errors, ref int warnings)
    {
        errors += RequireRef(f, "eventBus", "CombatEventBus");
    }

    private static void ValidateStandAction(StandAction sa, ref int errors, ref int warnings)
    {
        errors += RequireRef(sa, "entityManager", "EntityManager");
    }

    private static void ValidateTripAction(TripAction ta, ref int errors, ref int warnings)
    {
        errors += RequireRef(ta, "entityManager", "EntityManager");
        warnings += WarnRef(ta, "eventBus", "CombatEventBus");
    }

    private static void ValidateShoveAction(ShoveAction sa, ref int errors, ref int warnings)
    {
        errors += RequireRef(sa, "entityManager", "EntityManager");
        warnings += WarnRef(sa, "eventBus", "CombatEventBus");
    }

    
    private static void ValidateGrappleAction(GrappleAction ga, ref int errors, ref int warnings)
    {
        errors += RequireRef(ga, "entityManager", "EntityManager");
        warnings += WarnRef(ga, "eventBus", "CombatEventBus");
    }

private static void ValidateDemoralizeAction(DemoralizeAction da, ref int errors, ref int warnings)
    {
        errors += RequireRef(da, "entityManager", "EntityManager");
        warnings += WarnRef(da, "eventBus", "CombatEventBus");
    }

    private static void ValidateRaiseShieldAction(RaiseShieldAction sa, ref int errors, ref int warnings)
    {
        errors += RequireRef(sa, "entityManager", "EntityManager");
        warnings += WarnRef(sa, "eventBus", "CombatEventBus");
    }

    private static void ValidateShieldBlockAction(ShieldBlockAction sa, ref int errors, ref int warnings)
    {
        errors += RequireRef(sa, "entityManager", "EntityManager");
        warnings += WarnRef(sa, "eventBus", "CombatEventBus");
    }

    private static void ValidateAITurnController(AITurnController ai, ref int errors, ref int warnings)
    {
        errors += RequireRef(ai, "turnManager", "TurnManager");
        errors += RequireRef(ai, "eventBus", "CombatEventBus");
        errors += RequireRef(ai, "entityManager", "EntityManager");
        errors += RequireRef(ai, "gridManager", "GridManager");
        errors += RequireRef(ai, "strideAction", "StrideAction");
        errors += RequireRef(ai, "strikeAction", "StrikeAction");
        errors += RequireRef(ai, "standAction", "StandAction");
        errors += RequireRef(ai, "shieldBlockAction", "ShieldBlockAction");
        warnings += WarnRef(ai, "reactionPromptController", "ReactionPromptController");
    }

    private static void ValidateReactionPromptController(ReactionPromptController prompt, ref int errors, ref int warnings)
    {
        errors += RequireRef(prompt, "rootPanel", "GameObject");
        errors += RequireRef(prompt, "yesButton", "Button");
        errors += RequireRef(prompt, "noButton", "Button");
        warnings += WarnRef(prompt, "titleText", "TMP_Text");
        warnings += WarnRef(prompt, "bodyText", "TMP_Text");
    }

    private static void ValidateCombatLogController(CombatLogController log, ref int errors, ref int warnings)
    {
        errors += RequireRef(log, "entityManager", "EntityManager");
        errors += RequireRef(log, "eventBus", "CombatEventBus");

        errors += RequireRef(log, "scrollRect", "ScrollRect");
        errors += RequireRef(log, "content", "RectTransform");
        errors += RequireRef(log, "lineTemplate", "TextMeshProUGUI");
    }

    // ----------------- Helpers -----------------

    private static int ValidateAll<T>(ValidatorDelegate<T> validate) where T : UnityEngine.Object
    {
        var all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        int errors = 0;
        int warnings = 0;

        foreach (var obj in all)
        {
            validate(obj, ref errors, ref warnings);
        }
        return errors;
    }

    private static int ErrorIfMoreThanOne<T>() where T : UnityEngine.Object
    {
        var all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        if (all == null || all.Length <= 1) return 0;

        Debug.LogError($"[PF2eValidator] More than one {typeof(T).Name} found in scene ({all.Length}). " +
                       "This project expects exactly one per scene in inspector-only mode.");

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is Component c)
                Debug.LogError($"[PF2eValidator]   {typeof(T).Name} #{i + 1}: {GetPath(c.transform)}", c);
            else
                Debug.LogError($"[PF2eValidator]   {typeof(T).Name} #{i + 1}: {all[i].name}", all[i]);
        }
        return 1;
    }

    private static int WarnIfNone<T>() where T : UnityEngine.Object
    {
        var all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        if (all == null || all.Length == 0)
        {
            Debug.LogWarning($"[PF2eValidator] No {typeof(T).Name} found in scene.");
            return 1;
        }
        return 0;
    }

    private static int WarnIfAny<T>(string message) where T : UnityEngine.Object
    {
        var all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        if (all == null || all.Length == 0) return 0;

        int activeCount = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is Behaviour behaviour && !behaviour.enabled)
                continue;
            activeCount++;
        }

        if (activeCount <= 0) return 0;

        Debug.LogWarning($"[PF2eValidator] {message} Found: {activeCount}.");
        return 1;
    }

    private static int RequireRef(Component c, string fieldName, string friendlyTypeName)
    {
        var t = c.GetType();
        var f = t.GetField(fieldName, Flags);
        if (f == null)
        {
            Debug.LogWarning($"[PF2eValidator] {t.Name} on {GetPath(c.transform)}: field '{fieldName}' not found (renamed?).");
            return 0; // not an error; validator outdated
        }

        var value = f.GetValue(c);
        if (value == null || (value is UnityEngine.Object uo && uo == null))
        {
            Debug.LogError($"[PF2eValidator] Missing reference on {t.Name} ({GetPath(c.transform)}): '{fieldName}' ({friendlyTypeName}) is not assigned in Inspector.", c);
            return 1;
        }

        return 0;
    }

    private static int WarnRef(Component c, string fieldName, string friendlyTypeName)
    {
        var t = c.GetType();
        var f = t.GetField(fieldName, Flags);
        if (f == null)
        {
            Debug.LogWarning($"[PF2eValidator] {t.Name} on {GetPath(c.transform)}: field '{fieldName}' not found (renamed?).");
            return 0;
        }

        var value = f.GetValue(c);
        if (value == null || (value is UnityEngine.Object uo && uo == null))
        {
            Debug.LogWarning(
                $"[PF2eValidator] Missing optional reference on {t.Name} ({GetPath(c.transform)}): '{fieldName}' ({friendlyTypeName}) is not assigned in Inspector.",
                c);
            return 1;
        }

        return 0;
    }

    private static string GetPath(Transform t)
    {
        if (t == null) return "<null>";
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    // ----------------- Auto-Fix -----------------

    private static void RunAutoFix()
    {
        Debug.Log($"[PF2eAutoFix] Auto-fix starting. Scene: {SceneManager.GetActiveScene().name}");

        int fixedCount = 0;

        // Core singletons: must be exactly one
        if (!TryGetSingleton(out GridManager gridManager)) return;
        if (!TryGetSingleton(out EntityManager entityManager)) return;
        if (!TryGetSingleton(out TurnManager turnManager)) return;

        // Optional singletons (combat controller). If missing, skip related fixes.
        TryGetSingleton(out CombatEventBus eventBus, logIfMissing: false);
        TryGetSingleton(out PlayerActionExecutor actionExecutor, logIfMissing: false);
        TryGetSingleton(out StrideAction strideAction, logIfMissing: false);
        TryGetSingleton(out StrikeAction strikeActionSingleton, logIfMissing: false);
                TryGetSingleton(out TripAction tripActionSingleton, logIfMissing: false);
        TryGetSingleton(out ShoveAction shoveActionSingleton, logIfMissing: false);
        TryGetSingleton(out GrappleAction grappleActionSingleton, logIfMissing: false);
        TryGetSingleton(out DemoralizeAction demoralizeActionSingleton, logIfMissing: false);
        TryGetSingleton(out RaiseShieldAction raiseShieldActionSingleton, logIfMissing: false);
        TryGetSingleton(out ShieldBlockAction shieldBlockActionSingleton, logIfMissing: false);
        TryGetSingleton(out EntityMover entityMover, logIfMissing: false);
        TryGetSingleton(out TargetingController targetingController, logIfMissing: false);
        TryGetSingleton(out TurnInputController turnInputController, logIfMissing: false);
        TryGetSingleton(out CellHighlightPool highlightPool, logIfMissing: false);
        TryGetSingleton(out Canvas rootCanvas, logIfMissing: false);
        TryGetSingleton(out ReactionPromptController reactionPromptControllerSingleton, logIfMissing: false);

        // Fix null references only

        fixedCount += FixAll<EntityManager>("gridManager", gridManager);
        if (eventBus != null)
            fixedCount += FixAll<EntityManager>("eventBus", eventBus);

        fixedCount += FixAll<TurnManager>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<TurnManager>("eventBus", eventBus);

        fixedCount += FixAll<GridInteraction>("entityManager", entityManager);
        fixedCount += FixAll<GridInteraction>("turnManager", turnManager);

        fixedCount += FixAll<EntityMover>("entityManager", entityManager);

        // PlayerActionExecutor (Phase 10.X FINAL)
        fixedCount += FixAll<PlayerActionExecutor>("turnManager", turnManager);
        fixedCount += FixAll<PlayerActionExecutor>("entityManager", entityManager);
        if (strideAction != null)
            fixedCount += FixAll<PlayerActionExecutor>("strideAction", strideAction);
        if (tripActionSingleton != null)
            fixedCount += FixAll<PlayerActionExecutor>("tripAction", tripActionSingleton);
                if (shoveActionSingleton != null)
            fixedCount += FixAll<PlayerActionExecutor>("shoveAction", shoveActionSingleton);
        if (grappleActionSingleton != null)
            fixedCount += FixAll<PlayerActionExecutor>("grappleAction", grappleActionSingleton);
        if (demoralizeActionSingleton != null)
            fixedCount += FixAll<PlayerActionExecutor>("demoralizeAction", demoralizeActionSingleton);
        if (raiseShieldActionSingleton != null)
            fixedCount += FixAll<PlayerActionExecutor>("raiseShieldAction", raiseShieldActionSingleton);
        if (shieldBlockActionSingleton != null)
            fixedCount += FixAll<PlayerActionExecutor>("shieldBlockAction", shieldBlockActionSingleton);
        if (reactionPromptControllerSingleton != null)
            fixedCount += FixAll<PlayerActionExecutor>("reactionPromptController", reactionPromptControllerSingleton);

        // StrideAction (no TurnManager dependency in Phase 10.X)
        fixedCount += FixAll<StrideAction>("entityManager", entityManager);
        fixedCount += FixAll<StrideAction>("gridManager", gridManager);
        if (entityMover != null)
            fixedCount += FixAll<StrideAction>("entityMover", entityMover);
        if (eventBus != null)
            fixedCount += FixAll<StrideAction>("eventBus", eventBus);

        // TargetingController (Phase 12)
        fixedCount += FixAll<TargetingController>("entityManager", entityManager);
        fixedCount += FixAll<TargetingController>("turnManager", turnManager);
        if (actionExecutor != null)
            fixedCount += FixAll<TargetingController>("actionExecutor", actionExecutor);
        if (eventBus != null)
            fixedCount += FixAll<TargetingController>("eventBus", eventBus);

        // TurnInputController (delegates to TargetingController)
        fixedCount += FixAll<TurnInputController>("turnManager", turnManager);
        fixedCount += FixAll<TurnInputController>("gridManager", gridManager);
        if (actionExecutor != null)
            fixedCount += FixAll<TurnInputController>("actionExecutor", actionExecutor);
        if (targetingController != null)
            fixedCount += FixAll<TurnInputController>("targetingController", targetingController);

        fixedCount += FixAll<MovementZoneVisualizer>("entityManager", entityManager);
        fixedCount += FixAll<MovementZoneVisualizer>("gridManager", gridManager);
        fixedCount += FixAll<MovementZoneVisualizer>("turnManager", turnManager);
        if (eventBus != null)
            fixedCount += FixAll<MovementZoneVisualizer>("eventBus", eventBus);
        if (highlightPool != null)
            fixedCount += FixAll<MovementZoneVisualizer>("highlightPool", highlightPool);
        if (strideAction != null)
            fixedCount += FixAll<MovementZoneVisualizer>("strideAction", strideAction);
        if (entityMover != null)
            fixedCount += FixAll<MovementZoneVisualizer>("entityMover", entityMover);

        fixedCount += FixAll<CombatStarter>("turnManager", turnManager);

        // TurnUIController (bus-driven; UI fields assigned manually)
        fixedCount += FixAll<TurnUIController>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<TurnUIController>("eventBus", eventBus);
        if (turnInputController != null)
            fixedCount += FixAll<TurnUIController>("turnInputController", turnInputController);

        // EncounterEndPanelController (Phase 17)
        if (eventBus != null)
            fixedCount += FixAll<EncounterEndPanelController>("eventBus", eventBus);

        // EncounterFlowController (Phase 17.5)
        fixedCount += FixAll<EncounterFlowController>("turnManager", turnManager);
        fixedCount += FixAll<EncounterFlowController>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<EncounterFlowController>("eventBus", eventBus);
        if (rootCanvas != null)
            fixedCount += FixAll<EncounterFlowController>("rootCanvas", rootCanvas);

        // StrikeLogForwarder (Phase 11.TypedEvents - typed strike events)
        fixedCount += FixAll<StrikeLogForwarder>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<StrikeLogForwarder>("eventBus", eventBus);

        // SkillCheckLogForwarder (Phase 21 - typed skill checks to string)
        fixedCount += FixAll<SkillCheckLogForwarder>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<SkillCheckLogForwarder>("eventBus", eventBus);

        // TurnLogForwarder (Phase 11.TypedEvents-B - typed turn to string)
        fixedCount += FixAll<TurnLogForwarder>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<TurnLogForwarder>("eventBus", eventBus);

        // StrideLogForwarder (Phase 11.TypedEvents-B - typed stride to string)
        if (eventBus != null)
            fixedCount += FixAll<StrideLogForwarder>("eventBus", eventBus);

        // CombatLogController (bus-only; UI fields assigned manually)
        fixedCount += FixAll<CombatLogController>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<CombatLogController>("eventBus", eventBus);

        // FloatingDamageUI (Phase FloatingDamageUI — visual payoff for typed strike events)
        fixedCount += FixAll<FloatingDamageUI>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<FloatingDamageUI>("eventBus", eventBus);

        // StandAction (Phase 15A)
        fixedCount += FixAll<StandAction>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<StandAction>("eventBus", eventBus);

        // RaiseShieldAction / ShieldBlockAction (Phase 19)
        fixedCount += FixAll<RaiseShieldAction>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<RaiseShieldAction>("eventBus", eventBus);
        fixedCount += FixAll<ShieldBlockAction>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<ShieldBlockAction>("eventBus", eventBus);

        // ConditionLogForwarder (Phase 15B)
        if (eventBus != null)
            fixedCount += FixAll<ConditionLogForwarder>("eventBus", eventBus);

        // PlayerActionExecutor standAction (Phase 15A)
        TryGetSingleton(out StandAction standActionSingleton, logIfMissing: false);
        if (standActionSingleton != null)
            fixedCount += FixAll<PlayerActionExecutor>("standAction", standActionSingleton);

        // TripAction / DemoralizeAction (Phase 22)
        fixedCount += FixAll<TripAction>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<TripAction>("eventBus", eventBus);
                fixedCount += FixAll<ShoveAction>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<ShoveAction>("eventBus", eventBus);
        fixedCount += FixAll<GrappleAction>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<GrappleAction>("eventBus", eventBus);
        fixedCount += FixAll<DemoralizeAction>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<DemoralizeAction>("eventBus", eventBus);

        // AITurnController (Phase 16)
        fixedCount += FixAll<AITurnController>("turnManager", turnManager);
        if (eventBus != null)
            fixedCount += FixAll<AITurnController>("eventBus", eventBus);
        fixedCount += FixAll<AITurnController>("entityManager", entityManager);
        fixedCount += FixAll<AITurnController>("gridManager", gridManager);
        if (strideAction != null)
            fixedCount += FixAll<AITurnController>("strideAction", strideAction);
        if (strikeActionSingleton != null)
            fixedCount += FixAll<AITurnController>("strikeAction", strikeActionSingleton);
        if (standActionSingleton != null)
            fixedCount += FixAll<AITurnController>("standAction", standActionSingleton);
        if (shieldBlockActionSingleton != null)
            fixedCount += FixAll<AITurnController>("shieldBlockAction", shieldBlockActionSingleton);
        if (reactionPromptControllerSingleton != null)
            fixedCount += FixAll<AITurnController>("reactionPromptController", reactionPromptControllerSingleton);

        // InitiativeBarController (Phase 14B)
        fixedCount += FixAll<InitiativeBarController>("turnManager", turnManager);
        fixedCount += FixAll<InitiativeBarController>("entityManager", entityManager);
        if (eventBus != null)
            fixedCount += FixAll<InitiativeBarController>("eventBus", eventBus);

        if (fixedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[PF2eAutoFix] Done. Fixed assignments: {fixedCount}. Scene marked dirty.");
        }
        else
        {
            Debug.Log("[PF2eAutoFix] Done. Nothing to fix (all references already assigned).");
        }
    }

    private static bool TryGetSingleton<T>(out T instance, bool logIfMissing = true) where T : UnityEngine.Object
    {
        instance = null;
        var all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);

        if (all == null || all.Length == 0)
        {
            if (logIfMissing)
                Debug.LogError($"[PF2eAutoFix] Missing required singleton: {typeof(T).Name}.");
            return false;
        }

        if (all.Length > 1)
        {
            Debug.LogError($"[PF2eAutoFix] More than one {typeof(T).Name} found ({all.Length}). Auto-fix requires exactly one.");
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is Component c)
                    Debug.LogError($"[PF2eAutoFix]   {typeof(T).Name} #{i + 1}: {GetPath(c.transform)}", c);
                else
                    Debug.LogError($"[PF2eAutoFix]   {typeof(T).Name} #{i + 1}: {all[i].name}", all[i]);
            }
            return false;
        }

        instance = all[0];
        return true;
    }

    private static int FixAll<TTarget>(string fieldName, UnityEngine.Object value) where TTarget : Component
    {
        if (value == null) return 0;

        int fixedCount = 0;
        var all = UnityEngine.Object.FindObjectsByType<TTarget>(FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
            fixedCount += TryAssignIfNull(all[i], fieldName, value);

        return fixedCount;
    }

    private static int DisableAllIfEnabled<TTarget>() where TTarget : Behaviour
    {
        int fixedCount = 0;
        var all = UnityEngine.Object.FindObjectsByType<TTarget>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var target = all[i];
            if (target == null || !target.enabled) continue;
            Undo.RecordObject(target, $"Disable {typeof(TTarget).Name}");
            target.enabled = false;
            EditorUtility.SetDirty(target);
            fixedCount++;
        }
        return fixedCount;
    }

    private static int TryAssignIfNull(Component target, string fieldName, UnityEngine.Object value)
    {
        if (target == null || value == null) return 0;

        var t = target.GetType();
        var f = t.GetField(fieldName, Flags);
        if (f == null) return 0;

        var current = f.GetValue(target);
        if (current != null && (!(current is UnityEngine.Object uo) || uo != null))
            return 0;

        if (!f.FieldType.IsAssignableFrom(value.GetType()))
            return 0;

        Undo.RecordObject(target, $"PF2e AutoFix {t.Name}.{fieldName}");
        f.SetValue(target, value);
        EditorUtility.SetDirty(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);

        Debug.Log($"[PF2eAutoFix] Assigned {t.Name} ({GetPath(target.transform)}): {fieldName} = {value.name}", target);
        return 1;
    }
}
#endif
