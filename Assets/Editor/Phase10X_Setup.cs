#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using PF2e.TurnSystem;

/// <summary>
/// Phase 10.X FINAL setup: creates PlayerActionExecutor and wires dependencies.
/// Run via Tools > PF2e > Phase 10.X Setup (FINAL Architecture).
/// </summary>
public static class Phase10X_Setup
{
    [MenuItem("Tools/PF2e/Phase 10.X Setup (FINAL Architecture)")]
    public static void SetupPhase10X()
    {
        bool changed = false;

        // 1. Find CombatController GameObject
        var combatController = GameObject.Find("CombatController");
        if (combatController == null)
        {
            Debug.LogError("[Phase10X_Setup] CombatController GameObject not found. Cannot add PlayerActionExecutor.");
            EditorUtility.DisplayDialog("Phase 10.X Setup",
                "CombatController GameObject not found!\n\nCreate it first or rename your combat GameObject to 'CombatController'.", "OK");
            return;
        }

        // 2. Find or add PlayerActionExecutor
        var executor = combatController.GetComponent<PlayerActionExecutor>();
        if (executor == null)
        {
            executor = combatController.AddComponent<PlayerActionExecutor>();
            Undo.RegisterCreatedObjectUndo(executor, "Add PlayerActionExecutor");
            Debug.Log("[Phase10X_Setup] Added PlayerActionExecutor to CombatController");
            changed = true;
        }

        // 3. Find dependencies
        var turnManager = Object.FindAnyObjectByType<TurnManager>();
        var entityManager = Object.FindAnyObjectByType<PF2e.Managers.EntityManager>();
        var strideAction = Object.FindAnyObjectByType<StrideAction>();

        // 4. Wire executor
        var executorSO = new SerializedObject(executor);

        var tmProp = executorSO.FindProperty("turnManager");
        if (tmProp.objectReferenceValue == null && turnManager != null)
        {
            tmProp.objectReferenceValue = turnManager;
            changed = true;
            Debug.Log("[Phase10X_Setup] Wired PlayerActionExecutor.turnManager");
        }

        var emProp = executorSO.FindProperty("entityManager");
        if (emProp.objectReferenceValue == null && entityManager != null)
        {
            emProp.objectReferenceValue = entityManager;
            changed = true;
            Debug.Log("[Phase10X_Setup] Wired PlayerActionExecutor.entityManager");
        }

        var saProp = executorSO.FindProperty("strideAction");
        if (saProp.objectReferenceValue == null && strideAction != null)
        {
            saProp.objectReferenceValue = strideAction;
            changed = true;
            Debug.Log("[Phase10X_Setup] Wired PlayerActionExecutor.strideAction");
        }

        executorSO.ApplyModifiedPropertiesWithoutUndo();
        if (changed) EditorUtility.SetDirty(executor);

        // 5. Auto-fix all dependencies
        Debug.Log("[Phase10X_Setup] Running auto-fix...");
        PF2eSceneDependencyValidator.AutoFix_Safe();

        if (changed)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[Phase10X_Setup] Phase 10.X setup complete. Scene marked dirty. Save scene (Ctrl+S).");
        }
        else
        {
            Debug.Log("[Phase10X_Setup] Already set up (no changes needed).");
        }

        EditorUtility.DisplayDialog("Phase 10.X Setup (FINAL)",
            "PlayerActionExecutor setup complete!\n\n" +
            "Next steps:\n" +
            "1. Tools > PF2e > Validate Scene Dependencies\n" +
            "2. Play mode: test movement + entity clicking\n" +
            "3. Check dev watchdog (stuck action timeout = 30s)", "OK");
    }
}
#endif
