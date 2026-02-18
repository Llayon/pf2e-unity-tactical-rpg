#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using PF2e.Core;
using PF2e.TurnSystem;

/// <summary>
/// Phase 10.1 setup: creates CombatEventBus GameObject and wires StrideAction.
/// Run via Tools > PF2e > Phase 10 Setup.
/// </summary>
public static class Phase10Setup
{
    [MenuItem("Tools/PF2e/Phase 10 Setup (Combat Event Bus)")]
    public static void SetupPhase10()
    {
        // 1. Find or create CombatEventBus GameObject
        var existingBus = Object.FindAnyObjectByType<CombatEventBus>();
        if (existingBus != null)
        {
            Debug.Log($"[Phase10Setup] CombatEventBus already exists at: {GetPath(existingBus.transform)}");
        }
        else
        {
            var busGO = new GameObject("CombatEventBus");
            busGO.AddComponent<CombatEventBus>();
            Undo.RegisterCreatedObjectUndo(busGO, "Create CombatEventBus");
            Debug.Log("[Phase10Setup] Created CombatEventBus GameObject");
            existingBus = busGO.GetComponent<CombatEventBus>();
        }

        // 2. Wire StrideAction.eventBus
        var strideAction = Object.FindAnyObjectByType<StrideAction>();
        if (strideAction != null)
        {
            var so = new UnityEditor.SerializedObject(strideAction);
            var prop = so.FindProperty("eventBus");
            if (prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = existingBus;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(strideAction);
                Debug.Log($"[Phase10Setup] Wired StrideAction.eventBus → {existingBus.name}");
            }
            else
            {
                Debug.Log("[Phase10Setup] StrideAction.eventBus already wired");
            }
        }
        else
        {
            Debug.LogWarning("[Phase10Setup] StrideAction not found in scene");
        }

        // 3. Save scene
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Phase 10 Setup",
            "CombatEventBus setup complete!\n\n" +
            "Next steps:\n" +
            "1. Tools > PF2e > Build Combat Log UI\n" +
            "2. Tools > PF2e > Validate Scene Dependencies\n" +
            "3. Play mode: press C to test combat log", "OK");
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
