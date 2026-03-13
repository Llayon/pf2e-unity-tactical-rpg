#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PF2e.Presentation;

public static class ApplySolastaStyleUiPolish
{
    private const string MenuPath = "PF2e/UI/Apply Solasta Style UI Polish";
    private const string ReloadMenuPath = "PF2e/UI/Reload SampleScene From Disk (Discard Unsaved)";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string InitiativeSlotPrefabPath = "Assets/Prefabs/InitiativeSlot.prefab";
    private const string EncounterFlowPrefabPath = "Assets/Prefabs/EncounterFlowPanel.prefab";
    private const string RegularFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Source Sans 3 SDF.asset";
    private const string LightFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Source Sans 3 Light SDF.asset";

    [MenuItem(MenuPath)]
    private static void Apply()
    {
        if (!EnsureSceneIsSafeToModify())
            return;

        TMP_FontAsset regularFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
        TMP_FontAsset lightFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LightFontPath);
        if (regularFont == null || lightFont == null)
        {
            Debug.LogError("[SolastaUiPolish] Missing Source Sans 3 TMP assets. Run font asset creation first.");
            return;
        }

        string originalScenePath = SceneManager.GetActiveScene().path;
        int changes = 0;

        changes += ApplyToScene(regularFont, lightFont);

        if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(originalScenePath))
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);

        changes += ApplyToPrefab(InitiativeSlotPrefabPath, root => StyleInitiativeSlotPrefab(root, regularFont, lightFont));
        changes += ApplyToPrefab(EncounterFlowPrefabPath, root => StyleEncounterFlowPanel(root, regularFont, lightFont));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SolastaUiPolish] Applied Solasta-style UI polish. Changes: {changes}");
    }

    [MenuItem(ReloadMenuPath)]
    private static void ReloadSampleSceneFromDiskDiscardUnsaved()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("[SolastaUiPolish] Reloaded SampleScene from disk, discarding unsaved in-memory changes.");
    }

    private static bool EnsureSceneIsSafeToModify()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (string.Equals(activeScene.path, ScenePath, StringComparison.OrdinalIgnoreCase) && activeScene.isDirty)
        {
            Debug.LogError("[SolastaUiPolish] Active SampleScene has unsaved in-memory changes. Reload SampleScene from disk before running this pass.");
            return false;
        }

        return true;
    }

    private static int ApplyToScene(TMP_FontAsset regularFont, TMP_FontAsset lightFont)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int changes = 0;

        changes += WithNamedObject(scene, "ActionBar", root => StyleActionBar(root, regularFont));
        changes += WithNamedObject(scene, "TurnOptionsUI", root => StyleTurnOptions(root, regularFont));
        changes += WithNamedObject(scene, "TurnEconomyHUD", root => StyleTurnEconomy(root, regularFont));
        changes += WithNamedObject(scene, "EncounterFlowPanel", root => StyleEncounterFlowPanel(root, regularFont, lightFont));
        changes += WithNamedObject(scene, "InitiativeBarPanel", root => StyleInitiativeBarPanel(root, regularFont, lightFont));
        changes += WithNamedObject(scene, "TargetingHintPanel", root => StyleTargetingHintPanel(root, lightFont));
        changes += WithNamedObject(scene, "ReactionPromptPanel", root => StyleReactionPromptPanel(root, regularFont, lightFont));
        changes += WithNamedObject(scene, "CombatLogHUD", root => StyleCombatLog(root, lightFont));
        changes += WithNamedObject(scene, "CombatLogTooltip", root => StyleCombatLogTooltip(root, regularFont, lightFont));

        if (changes > 0)
            EditorSceneManager.SaveScene(scene);

        return changes;
    }

    private static int ApplyToPrefab(string prefabPath, Func<GameObject, int> styler)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
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

    private static int WithNamedObject(Scene scene, string objectName, Func<GameObject, int> styler)
    {
        GameObject target = FindByName(scene, objectName);
        if (target == null)
        {
            Debug.LogWarning($"[SolastaUiPolish] Could not find '{objectName}' in {scene.path}");
            return 0;
        }

        return styler(target);
    }

    private static GameObject FindByName(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindByName(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindByName(Transform root, string objectName)
    {
        if (root == null)
            return null;

        if (string.Equals(root.name, objectName, StringComparison.Ordinal))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindByName(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static int StyleActionBar(GameObject root, TMP_FontAsset regularFont)
    {
        int changes = 0;
        foreach (TMP_Text text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            float fontSize = string.Equals(text.text, "...", StringComparison.Ordinal) ? 16f : 15.5f;
            changes += ApplyText(text, regularFont, fontSize, 0.12f, FontStyles.Normal, CombatUiPalette.HudButtonTextColor);
        }

        return changes;
    }

    private static int StyleTurnOptions(GameObject root, TMP_FontAsset regularFont)
    {
        int changes = 0;
        changes += ApplyImage(root.GetComponent<Image>(), CombatUiPalette.HudPanelBackgroundColor);

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            Image image = button.GetComponent<Image>();
            if (image != null)
                changes += ApplyImage(image, button.name.Contains("Launcher", StringComparison.OrdinalIgnoreCase)
                    ? CombatUiPalette.HudPanelSurfaceColor
                    : CombatUiPalette.HudButtonBackgroundColor);

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                float fontSize = string.Equals(label.text, "...", StringComparison.Ordinal) ? 16f : 15.5f;
                changes += ApplyText(label, regularFont, fontSize, 0.12f, FontStyles.Normal, CombatUiPalette.HudButtonTextColor);
            }
        }

        return changes;
    }

    private static int StyleTurnEconomy(GameObject root, TMP_FontAsset regularFont)
    {
        int changes = 0;
        changes += ApplyImage(root.GetComponent<Image>(), CombatUiPalette.HudPanelBackgroundColor);

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            changes += ApplyImage(button.GetComponent<Image>(), CombatUiPalette.HudButtonBackgroundColor);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            changes += ApplyText(label, regularFont, 18.5f, 0.12f, FontStyles.Normal, CombatUiPalette.HudButtonTextColor);
        }

        return changes;
    }

    private static int StyleEncounterFlowPanel(GameObject root, TMP_FontAsset regularFont, TMP_FontAsset lightFont)
    {
        int changes = 0;
        changes += ApplyImage(root.GetComponent<Image>(), CombatUiPalette.HudPanelBackgroundColor);

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            changes += ApplyImage(button.GetComponent<Image>(), CombatUiPalette.HudButtonBackgroundColor);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            changes += ApplyText(label, regularFont, 19f, 0.14f, FontStyles.Normal, CombatUiPalette.HudButtonTextColor);
        }

        foreach (TMP_Text text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.GetComponentInParent<Button>() != null)
                continue;

            changes += ApplyText(text, lightFont, 14f, 0.08f, FontStyles.Normal, CombatUiPalette.HudTextSecondaryColor);
        }

        return changes;
    }

    private static int StyleInitiativeBarPanel(GameObject root, TMP_FontAsset regularFont, TMP_FontAsset lightFont)
    {
        int changes = 0;

        foreach (TMP_Text text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
                continue;

            if (string.Equals(text.text, "DLY", StringComparison.OrdinalIgnoreCase))
            {
                changes += ApplyText(text, regularFont, 10f, 0.12f, FontStyles.Bold, CombatUiPalette.HudButtonSelectedTextColor);
            }
            else if (text.name.Contains("Prompt", StringComparison.OrdinalIgnoreCase))
            {
                changes += ApplyText(text, lightFont, 11f, 0.08f, FontStyles.Normal, CombatUiPalette.HudTextSecondaryColor);
            }
            else if (text.text.StartsWith("Round", StringComparison.OrdinalIgnoreCase))
            {
                changes += ApplyText(text, regularFont, 14.5f, 0.1f, FontStyles.Normal, CombatUiPalette.HudTextPrimaryColor);
            }
            else
            {
                changes += ApplyText(text, regularFont, 13.5f, 0.08f, FontStyles.Normal, CombatUiPalette.HudTextPrimaryColor);
            }
        }

        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.name.Contains("Prompt", StringComparison.OrdinalIgnoreCase))
                changes += ApplyImage(image, CombatUiPalette.HudPanelBackgroundColor);
        }

        return changes;
    }

    private static int StyleTargetingHintPanel(GameObject root, TMP_FontAsset lightFont)
    {
        int changes = 0;
        changes += ApplyImage(root.GetComponent<Image>(), CombatUiPalette.HudPanelBackgroundColor);

        foreach (TMP_Text text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            changes += ApplyText(text, lightFont, 14.5f, 0.08f, FontStyles.Normal, CombatUiPalette.HudTextPrimaryColor);

        return changes;
    }

    private static int StyleReactionPromptPanel(GameObject root, TMP_FontAsset regularFont, TMP_FontAsset lightFont)
    {
        int changes = 0;
        changes += ApplyImage(root.GetComponent<Image>(), CombatUiPalette.HudPanelBackgroundColor);

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            changes += ApplyImage(button.GetComponent<Image>(), CombatUiPalette.HudButtonBackgroundColor);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            changes += ApplyText(label, regularFont, 16f, 0.12f, FontStyles.Normal, CombatUiPalette.HudButtonTextColor);
        }

        foreach (TMP_Text text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.GetComponentInParent<Button>() != null)
                continue;

            if (text.name.Contains("Title", StringComparison.OrdinalIgnoreCase))
                changes += ApplyText(text, regularFont, 18f, 0.12f, FontStyles.Normal, CombatUiPalette.HudTextPrimaryColor);
            else
                changes += ApplyText(text, lightFont, 14f, 0.08f, FontStyles.Normal, CombatUiPalette.HudTextSecondaryColor);
        }

        return changes;
    }

    private static int StyleCombatLog(GameObject root, TMP_FontAsset lightFont)
    {
        int changes = 0;
        foreach (TMP_Text text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.name.Contains("Retention", StringComparison.OrdinalIgnoreCase))
                changes += ApplyText(text, lightFont, 12f, 0.05f, FontStyles.Italic, CombatUiPalette.HudTextMutedColor);
            else
                changes += ApplyText(text, lightFont, 15.5f, 0.08f, FontStyles.Normal, CombatUiPalette.HudTextPrimaryColor);
        }

        return changes;
    }

    private static int StyleCombatLogTooltip(GameObject root, TMP_FontAsset regularFont, TMP_FontAsset lightFont)
    {
        int changes = 0;
        changes += ApplyImage(root.GetComponent<Image>(), CombatUiPalette.TooltipBackgroundColor);

        TMP_Text title = FindByName(root.transform, "TooltipTitle")?.GetComponent<TMP_Text>();
        TMP_Text body = FindByName(root.transform, "TooltipBody")?.GetComponent<TMP_Text>();
        Image separator = FindByName(root.transform, "TooltipSeparator")?.GetComponent<Image>();

        changes += ApplyText(title, regularFont, 18f, 0.65f, FontStyles.Normal, CombatUiPalette.TooltipTitleColor);
        changes += ApplyText(body, lightFont, 16f, 0.65f, FontStyles.Normal, CombatUiPalette.TooltipBodyColor, 4f);
        changes += ApplyImage(separator, CombatUiPalette.TooltipDividerColor);
        return changes;
    }

    private static int StyleInitiativeSlotPrefab(GameObject root, TMP_FontAsset regularFont, TMP_FontAsset lightFont)
    {
        int changes = 0;
        foreach (TMP_Text text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (string.Equals(text.text, "DLY", StringComparison.OrdinalIgnoreCase) || text.name.Contains("Badge", StringComparison.OrdinalIgnoreCase))
                changes += ApplyText(text, regularFont, 10f, 0.12f, FontStyles.Bold, CombatUiPalette.HudButtonSelectedTextColor);
            else
                changes += ApplyText(text, regularFont, 13.5f, 0.08f, FontStyles.Normal, CombatUiPalette.HudTextPrimaryColor);
        }

        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.name.Contains("Background", StringComparison.OrdinalIgnoreCase) && image.transform.parent != root.transform)
                changes += ApplyImage(image, CombatUiPalette.HudButtonSelectedColor);
        }

        return changes;
    }

    private static int ApplyText(
        TMP_Text text,
        TMP_FontAsset font,
        float fontSize,
        float characterSpacing,
        FontStyles style,
        Color color,
        float lineSpacing = float.NaN)
    {
        if (text == null || font == null)
            return 0;

        int changes = 0;
        bool fontChanged = text.font != font || text.fontSharedMaterial != font.material;
        if (fontChanged)
        {
            ApplySourceSans3UiFont.ApplyFont(text, font);
            changes++;
        }

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

        if (!float.IsNaN(lineSpacing) && !Mathf.Approximately(text.lineSpacing, lineSpacing))
        {
            text.lineSpacing = lineSpacing;
            changes++;
        }

        if (text.fontStyle != style)
        {
            text.fontStyle = style;
            changes++;
        }

        if (!Approximately(text.color, color))
        {
            text.color = color;
            changes++;
        }

        if (!text.enableKerning)
        {
            text.enableKerning = true;
            changes++;
        }

        if (changes > 0)
        {
            EditorUtility.SetDirty(text);
            text.UpdateMeshPadding();
            text.SetAllDirty();
        }

        return changes;
    }

    private static int ApplyImage(Image image, Color color)
    {
        if (image == null)
            return 0;

        if (Approximately(image.color, color))
            return 0;

        image.color = color;
        EditorUtility.SetDirty(image);
        return 1;
    }

    private static bool Approximately(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) <= 0.001f
            && Mathf.Abs(a.g - b.g) <= 0.001f
            && Mathf.Abs(a.b - b.b) <= 0.001f
            && Mathf.Abs(a.a - b.a) <= 0.001f;
    }
}
#endif
