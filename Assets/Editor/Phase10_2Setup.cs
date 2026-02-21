#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Legacy menu item kept for discoverability.
/// Phase 10.2 forwarders were removed; use validator/autofix on current architecture.
/// </summary>
public static class Phase10_2Setup
{
    [MenuItem("Tools/PF2e/Phase 10.2 Setup (Deprecated)")]
    public static void SetupPhase10_2()
    {
        Debug.Log("[Phase10.2Setup] Deprecated workflow removed. Use Tools/PF2e/Auto-Fix Scene Dependencies (Safe) + Validate Scene Dependencies.");

        EditorUtility.DisplayDialog(
            "Phase 10.2 Setup (Deprecated)",
            "Phase 10.2 forwarders were removed.\n\nRun:\n- Tools > PF2e > Auto-Fix Scene Dependencies (Safe)\n- Tools > PF2e > Validate Scene Dependencies",
            "OK");
    }
}
#endif
