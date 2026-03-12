using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ApplySourceSans3TypographyPass
{
    private const string MenuPath = "PF2e/UI/Apply Source Sans 3 Typography Pass";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string InitiativeSlotPrefabPath = "Assets/Prefabs/InitiativeSlot.prefab";
    private const string EncounterFlowPrefabPath = "Assets/Prefabs/EncounterFlowPanel.prefab";

    [MenuItem(MenuPath)]
    private static void Apply()
    {
        int changes = 0;
        changes += ApplyToScene();
        changes += ApplyToPrefab(InitiativeSlotPrefabPath, StyleInitiativeSlotPrefab);
        changes += ApplyToPrefab(EncounterFlowPrefabPath, StyleEncounterFlowPanel);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TypographyPass] Applied Source Sans 3 typography pass. Changes: {changes}");
    }

    private static int ApplyToScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int changes = 0;

        changes += WithNamedObject(scene, "ActionBar", StyleActionBar);
        changes += WithNamedObject(scene, "TurnOptionsUI", StyleTurnOptions);
        changes += WithNamedObject(scene, "TurnEconomyHUD", StyleTurnEconomy);
        changes += WithNamedObject(scene, "EndTurnButton", StylePrimaryButtonRoot);
        changes += WithNamedObject(scene, "EncounterFlowPanel", StyleEncounterFlowPanel);
        changes += WithNamedObject(scene, "InitiativeBarPanel", StyleInitiativeBarPanel);
        changes += WithNamedObject(scene, "TargetingHintPanel", StyleTargetingHintPanel);
        changes += WithNamedObject(scene, "ReactionPromptPanel", StyleReactionPromptPanel);

        if (changes > 0)
            EditorSceneManager.SaveScene(scene);

        return changes;
    }

    private static int ApplyToPrefab(string prefabPath, Func<GameObject, int> styler)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            int changes = styler(root);
            if (changes > 0)
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return changes;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int WithNamedObject(UnityEngine.SceneManagement.Scene scene, string objectName, Func<GameObject, int> styler)
    {
        var target = FindByName(scene, objectName);
        if (target == null)
        {
            Debug.LogWarning($"[TypographyPass] Could not find '{objectName}' in {scene.path}");
            return 0;
        }

        return styler(target);
    }

    private static GameObject FindByName(UnityEngine.SceneManagement.Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindByName(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindByName(Transform root, string objectName)
    {
        if (string.Equals(root.name, objectName, StringComparison.Ordinal))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindByName(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static int StyleActionBar(GameObject root)
    {
        int changes = 0;
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
                continue;

            if (text.fontSize >= 15f)
                changes += ApplyText(text, 17f, 0.3f, FontWeight.Medium);
            else
                changes += ApplyText(text, 11.5f, 0.35f, FontWeight.Regular);
        }

        return changes;
    }

    private static int StyleTurnOptions(GameObject root)
    {
        int changes = 0;
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
                continue;

            float size = string.Equals(text.text, "...", StringComparison.Ordinal) ? 16f : 15f;
            float spacing = string.Equals(text.text, "...", StringComparison.Ordinal) ? 0.25f : 0.2f;
            changes += ApplyText(text, size, spacing, FontWeight.Medium);
        }

        return changes;
    }

    private static int StyleTurnEconomy(GameObject root)
    {
        int changes = 0;
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
                continue;

            if (string.Equals(text.text, "End Turn", StringComparison.Ordinal))
                changes += ApplyText(text, 20f, 0.35f, FontWeight.Medium);
            else
                changes += ApplyText(text, 12f, 0.15f, FontWeight.Regular);
        }

        return changes;
    }

    private static int StyleEncounterFlowPanel(GameObject root)
    {
        int changes = 0;
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
                continue;

            changes += ApplyText(text, 20f, 0.35f, FontWeight.Medium);
        }

        return changes;
    }

    private static int StylePrimaryButtonRoot(GameObject root)
    {
        int changes = 0;
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
                continue;

            changes += ApplyText(text, 20f, 0.35f, FontWeight.Medium);
        }

        return changes;
    }

    private static int StyleInitiativeBarPanel(GameObject root)
    {
        int changes = 0;
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
                continue;

            if (text.fontSize >= 20f)
                changes += ApplyText(text, 20f, 0.15f, FontWeight.Medium);
            else if (text.fontSize >= 14f)
                changes += ApplyText(text, 15f, 0.15f, FontWeight.Medium);
            else
                changes += ApplyText(text, 11f, 0.15f, FontWeight.Regular);
        }

        return changes;
    }

    private static int StyleTargetingHintPanel(GameObject root)
    {
        int changes = 0;
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
                continue;

            changes += ApplyText(text, 15f, 0.2f, FontWeight.Regular);
        }

        return changes;
    }

    private static int StyleReactionPromptPanel(GameObject root)
    {
        int changes = 0;
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
                continue;

            if (text.fontSize >= 20f)
                changes += ApplyText(text, 20f, 0.2f, FontWeight.Medium);
            else if (text.fontSize >= 14f)
                changes += ApplyText(text, 15f, 0.15f, FontWeight.Regular);
            else
                changes += ApplyText(text, 15f, 0.25f, FontWeight.Medium);
        }

        return changes;
    }

    private static int StyleInitiativeSlotPrefab(GameObject root)
    {
        int changes = 0;
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
                continue;

            if (string.Equals(text.text, "DLY", StringComparison.OrdinalIgnoreCase))
                changes += ApplyText(text, 10f, 0.3f, FontWeight.Medium);
            else
                changes += ApplyText(text, 13f, 0.1f, FontWeight.Medium);
        }

        return changes;
    }

    private static int ApplyText(TMP_Text text, float fontSize, float characterSpacing, FontWeight fontWeight)
    {
        int changes = 0;

        if (!Mathf.Approximately(text.fontSize, fontSize))
        {
            text.fontSize = fontSize;
            changes++;
        }

        if (!Mathf.Approximately(text.characterSpacing, characterSpacing))
        {
            text.characterSpacing = characterSpacing;
            changes++;
        }

        if (text.fontWeight != fontWeight)
        {
            text.fontWeight = fontWeight;
            changes++;
        }

        if (!text.enableKerning)
        {
            text.enableKerning = true;
            changes++;
        }

        if (changes > 0)
            EditorUtility.SetDirty(text);

        return changes;
    }
}
