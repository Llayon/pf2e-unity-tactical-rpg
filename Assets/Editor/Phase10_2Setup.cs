#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;
using PF2e.Presentation;

/// <summary>
/// Phase 10.2 setup: creates TurnManagerLogForwarder and unwires CombatLogController from TurnManager.
/// Run via Tools > PF2e > Phase 10.2 Setup.
/// </summary>
public static class Phase10_2Setup
{
    [MenuItem("Tools/PF2e/Phase 10.2 Setup (Bus-only Log)")]
    public static void SetupPhase10_2()
    {
        bool changed = false;

        // 1. Find or create TurnManagerLogForwarder GameObject
        var existingForwarder = Object.FindAnyObjectByType<TurnManagerLogForwarder>();
        if (existingForwarder == null)
        {
            var forwarderGO = new GameObject("TurnManagerLogForwarder");
            existingForwarder = forwarderGO.AddComponent<TurnManagerLogForwarder>();
            Undo.RegisterCreatedObjectUndo(forwarderGO, "Create TurnManagerLogForwarder");
            Debug.Log("[Phase10.2Setup] Created TurnManagerLogForwarder GameObject");
            changed = true;
        }
        else
        {
            Debug.Log($"[Phase10.2Setup] TurnManagerLogForwarder already exists at: {GetPath(existingForwarder.transform)}");
        }

        // 2. Wire TurnManagerLogForwarder dependencies
        var turnManager = Object.FindAnyObjectByType<TurnManager>();
        var entityManager = Object.FindAnyObjectByType<EntityManager>();
        var eventBus = Object.FindAnyObjectByType<CombatEventBus>();

        var forwarderSO = new SerializedObject(existingForwarder);

        var tmProp = forwarderSO.FindProperty("turnManager");
        if (tmProp.objectReferenceValue == null && turnManager != null)
        {
            tmProp.objectReferenceValue = turnManager;
            changed = true;
            Debug.Log("[Phase10.2Setup] Wired TurnManagerLogForwarder.turnManager");
        }

        var emProp = forwarderSO.FindProperty("entityManager");
        if (emProp.objectReferenceValue == null && entityManager != null)
        {
            emProp.objectReferenceValue = entityManager;
            changed = true;
            Debug.Log("[Phase10.2Setup] Wired TurnManagerLogForwarder.entityManager");
        }

        var ebProp = forwarderSO.FindProperty("eventBus");
        if (ebProp.objectReferenceValue == null && eventBus != null)
        {
            ebProp.objectReferenceValue = eventBus;
            changed = true;
            Debug.Log("[Phase10.2Setup] Wired TurnManagerLogForwarder.eventBus");
        }

        forwarderSO.ApplyModifiedPropertiesWithoutUndo();
        if (changed) EditorUtility.SetDirty(existingForwarder);

        // 3. Unwire CombatLogController.turnManager (if exists)
        var combatLogController = Object.FindAnyObjectByType<CombatLogController>();
        if (combatLogController != null)
        {
            var logSO = new SerializedObject(combatLogController);
            var tmLogProp = logSO.FindProperty("turnManager");

            // Check if field exists (Phase 10.1 had it, Phase 10.2 doesn't)
            if (tmLogProp != null && tmLogProp.objectReferenceValue != null)
            {
                Debug.LogWarning("[Phase10.2Setup] CombatLogController still has old 'turnManager' field. This will cause compilation error after code update. Remove it manually or recompile first.");
            }
            else
            {
                Debug.Log("[Phase10.2Setup] CombatLogController already bus-only (no turnManager field)");
            }
        }

        if (changed)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[Phase10.2Setup] Phase 10.2 setup complete. Scene marked dirty. Save scene (Ctrl+S).");
        }
        else
        {
            Debug.Log("[Phase10.2Setup] Nothing to fix (already set up).");
        }

        EditorUtility.DisplayDialog("Phase 10.2 Setup",
            "Bus-only Combat Log setup complete!\n\n" +
            "Next steps:\n" +
            "1. Tools > PF2e > Validate Scene Dependencies\n" +
            "2. Play mode: press C to test combat log\n" +
            "3. Verify that TurnManager events forward through bus", "OK");
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
}
#endif
