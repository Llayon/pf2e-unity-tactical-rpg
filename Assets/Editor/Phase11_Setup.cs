#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using PF2e.TurnSystem;
using PF2e.Managers;
using PF2e.Core;

/// <summary>
/// Phase 11 setup: creates StrikeAction and wires dependencies.
/// Run via Tools > PF2e > Phase 11 Setup (Strike).
/// </summary>
public static class Phase11_Setup
{
    [MenuItem("Tools/PF2e/Phase 11 Setup (Strike)")]
    public static void SetupPhase11()
    {
        bool changed = false;

        // 1. Find CombatController GameObject
        var combatController = GameObject.Find("CombatController");
        if (combatController == null)
        {
            Debug.LogError("[Phase11_Setup] CombatController GameObject not found. Cannot add StrikeAction.");
            EditorUtility.DisplayDialog("Phase 11 Setup",
                "CombatController GameObject not found!\n\nCreate it first or rename your combat GameObject to 'CombatController'.", "OK");
            return;
        }

        // 2. Find or add StrikeAction
        var strikeAction = combatController.GetComponent<StrikeAction>();
        if (strikeAction == null)
        {
            strikeAction = combatController.AddComponent<StrikeAction>();
            Undo.RegisterCreatedObjectUndo(strikeAction, "Add StrikeAction");
            Debug.Log("[Phase11_Setup] Added StrikeAction to CombatController");
            changed = true;
        }

        // 3. Find dependencies
        var entityManager = Object.FindAnyObjectByType<EntityManager>();
        var eventBus = Object.FindAnyObjectByType<CombatEventBus>();

        // 4. Wire StrikeAction
        var saSO = new SerializedObject(strikeAction);

        var emProp = saSO.FindProperty("entityManager");
        if (emProp.objectReferenceValue == null && entityManager != null)
        {
            emProp.objectReferenceValue = entityManager;
            changed = true;
            Debug.Log("[Phase11_Setup] Wired StrikeAction.entityManager");
        }

        var busProp = saSO.FindProperty("eventBus");
        if (busProp.objectReferenceValue == null && eventBus != null)
        {
            busProp.objectReferenceValue = eventBus;
            changed = true;
            Debug.Log("[Phase11_Setup] Wired StrikeAction.eventBus");
        }

        saSO.ApplyModifiedPropertiesWithoutUndo();
        if (changed) EditorUtility.SetDirty(strikeAction);

        // 5. Wire PlayerActionExecutor.strikeAction
        var executor = combatController.GetComponent<PlayerActionExecutor>();
        if (executor != null)
        {
            var exSO = new SerializedObject(executor);
            var strikeRefProp = exSO.FindProperty("strikeAction");
            if (strikeRefProp.objectReferenceValue == null && strikeAction != null)
            {
                strikeRefProp.objectReferenceValue = strikeAction;
                changed = true;
                Debug.Log("[Phase11_Setup] Wired PlayerActionExecutor.strikeAction");
            }
            exSO.ApplyModifiedPropertiesWithoutUndo();
            if (changed) EditorUtility.SetDirty(executor);
        }

        // 6. Auto-fix all dependencies
        Debug.Log("[Phase11_Setup] Running auto-fix...");
        PF2eSceneDependencyValidator.AutoFix_Safe();

        if (changed)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[Phase11_Setup] Phase 11 setup complete. Scene marked dirty. Save scene (Ctrl+S).");
        }
        else
        {
            Debug.Log("[Phase11_Setup] Already set up (no changes needed).");
        }

        EditorUtility.DisplayDialog("Phase 11 Setup (Strike)",
            "StrikeAction setup complete!\n\n" +
            "Next steps:\n" +
            "1. Tools > PF2e > Validate Scene Dependencies\n" +
            "2. Play mode: press C to start combat\n" +
            "3. Click adjacent enemy to attack\n" +
            "4. Check CombatLog for strike rolls and damage", "OK");
    }
}
#endif
