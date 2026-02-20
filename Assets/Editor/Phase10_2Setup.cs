#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using PF2e.Presentation;

/// <summary>
/// Legacy helper kept for backward compatibility.
/// Current typed path: TurnManagerTypedForwarder + TurnLogForwarder.
/// </summary>
public static class Phase10_2Setup
{
    [MenuItem("Tools/PF2e/Phase 10.2 Setup (Deprecated)")]
    public static void SetupPhase10_2()
    {
        int disabledCount = 0;
        var legacyForwarders = Object.FindObjectsByType<TurnManagerLogForwarder>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var forwarder in legacyForwarders)
        {
            if (forwarder == null || !forwarder.enabled) continue;
            Undo.RecordObject(forwarder, "Disable TurnManagerLogForwarder");
            forwarder.enabled = false;
            EditorUtility.SetDirty(forwarder);
            disabledCount++;
        }

        if (disabledCount > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        Debug.Log(
            $"[Phase10.2Setup] Deprecated legacy forwarders disabled: {disabledCount}. " +
            "Use TurnManagerTypedForwarder + TurnLogForwarder.");

        EditorUtility.DisplayDialog(
            "Phase 10.2 Setup (Deprecated)",
            "Legacy TurnManagerLogForwarder is deprecated.\n\n" +
            "Use:\n" +
            "- TurnManagerTypedForwarder\n" +
            "- TurnLogForwarder\n\n" +
            "Run Tools > PF2e > Auto-Fix + Validate to wire typed dependencies.",
            "OK");
    }
}
#endif
