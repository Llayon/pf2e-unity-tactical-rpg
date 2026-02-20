#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using PF2e.Core;
using PF2e.TurnSystem;

/// <summary>
/// Auto-runs Phase 10 setup on Editor load if needed.
/// </summary>
[InitializeOnLoad]
public static class AutoSetupPhase10
{
    static AutoSetupPhase10()
    {
        // Never mutate scenes during CI/batch runs.
        if (Application.isBatchMode)
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!Application.isPlaying
                && activeScene.isLoaded
                && !string.IsNullOrEmpty(activeScene.path))
            {
                RunSetupIfNeeded();
            }
        };
    }

    private static void RunSetupIfNeeded()
    {
        if (Application.isBatchMode)
        {
            return;
        }

        // Check if CombatEventBus exists
        var existingBus = Object.FindAnyObjectByType<CombatEventBus>();
        bool busCreated = false;

        if (existingBus == null)
        {
            var busGO = new GameObject("CombatEventBus");
            existingBus = busGO.AddComponent<CombatEventBus>();
            Undo.RegisterCreatedObjectUndo(busGO, "Auto-create CombatEventBus");
            Debug.Log("[AutoSetupPhase10] Created CombatEventBus GameObject");
            busCreated = true;
        }

        // Wire StrideAction.eventBus
        var strideAction = Object.FindAnyObjectByType<StrideAction>();
        if (strideAction != null)
        {
            var so = new SerializedObject(strideAction);
            var prop = so.FindProperty("eventBus");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = existingBus;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(strideAction);
                Debug.Log("[AutoSetupPhase10] Wired StrideAction.eventBus");
                busCreated = true;
            }
        }

        // Check if CombatLogHUD exists
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            var existingLogHUD = canvas.transform.Find("CombatLogHUD");
            if (existingLogHUD == null)
            {
                // Run UI builder
                CombatLogUIBuilder.BuildCombatLogUI();
                busCreated = true;
            }
        }

        if (busCreated)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[AutoSetupPhase10] Phase 10 setup complete. Scene marked dirty. Save scene (Ctrl+S).");
        }
    }
}
#endif
