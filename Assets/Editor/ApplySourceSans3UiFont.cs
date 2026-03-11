#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApplySourceSans3UiFont
{
    private const string MenuPath = "PF2e/UI/Apply Source Sans 3 As Default UI Font";
    private const string SourceFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Source Sans 3 SDF.asset";
    private const string LiberationFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string ResourceLoraFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Lora SDF.asset";
    private static readonly string[] SceneSearchRoots = { "Assets/Scenes" };
    private static readonly string[] PrefabSearchRoots = { "Assets/Prefabs" };

    [MenuItem(MenuPath)]
    public static void Apply()
    {
        var sourceFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SourceFontPath);
        var liberationFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationFontPath);
        var resourceLoraFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ResourceLoraFontPath);

        if (sourceFont == null)
        {
            Debug.LogError($"[ApplySourceSans3UiFont] Missing source font asset at '{SourceFontPath}'.");
            return;
        }

        if (liberationFont == null)
        {
            Debug.LogError($"[ApplySourceSans3UiFont] Missing LiberationSans font asset at '{LiberationFontPath}'.");
            return;
        }

        var legacyFonts = new HashSet<TMP_FontAsset> { liberationFont };
        if (resourceLoraFont != null)
        {
            legacyFonts.Add(resourceLoraFont);
        }

        TMP_Settings.defaultFontAsset = sourceFont;
        EditorUtility.SetDirty(TMP_Settings.instance);

        string originalScenePath = SceneManager.GetActiveScene().path;
        int sceneChanges = ApplyToAllScenes(sourceFont, legacyFonts);

        if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(originalScenePath))
        {
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }

        int prefabChanges = ApplyToPrefabs(sourceFont, legacyFonts);

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.Refresh();

        Debug.Log($"[ApplySourceSans3UiFont] Updated {sceneChanges} scene TMP texts and {prefabChanges} prefab TMP texts. TMP default font is now Source Sans 3.", TMP_Settings.instance);
    }

    private static int ApplyToAllScenes(TMP_FontAsset sourceFont, HashSet<TMP_FontAsset> legacyFonts)
    {
        int changed = 0;
        var sceneGuids = AssetDatabase.FindAssets("t:Scene", SceneSearchRoots);

        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            if (ShouldSkipScene(path))
            {
                continue;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            changed += ApplyToScene(scene, sourceFont, legacyFonts);
            EditorSceneManager.SaveScene(scene);
        }

        return changed;
    }

    private static int ApplyToScene(Scene scene, TMP_FontAsset sourceFont, HashSet<TMP_FontAsset> legacyFonts)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning($"[ApplySourceSans3UiFont] Scene '{scene.path}' is not loaded. Scene pass skipped.");
            return 0;
        }

        int changed = 0;
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var texts = roots[i].GetComponentsInChildren<TMP_Text>(true);
            for (int j = 0; j < texts.Length; j++)
            {
                if (!ShouldReplaceText(texts[j], legacyFonts))
                    continue;

                ApplyFont(texts[j], sourceFont);
                changed++;
            }
        }

        if (changed > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        return changed;
    }

    private static int ApplyToPrefabs(TMP_FontAsset sourceFont, HashSet<TMP_FontAsset> legacyFonts)
    {
        int changed = 0;
        var guids = AssetDatabase.FindAssets("t:Prefab", PrefabSearchRoots);
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var root = PrefabUtility.LoadPrefabContents(path);
            bool prefabDirty = false;

            try
            {
                var texts = root.GetComponentsInChildren<TMP_Text>(true);
                for (int j = 0; j < texts.Length; j++)
                {
                    if (!ShouldReplaceText(texts[j], legacyFonts))
                        continue;

                    ApplyFont(texts[j], sourceFont);
                    changed++;
                    prefabDirty = true;
                }

                if (prefabDirty)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return changed;
    }

    private static bool ShouldReplaceText(TMP_Text text, HashSet<TMP_FontAsset> legacyFonts)
    {
        if (text == null || text.font == null)
            return false;

        return legacyFonts.Contains(text.font);
    }

    private static bool ShouldSkipScene(string path)
    {
        return path.Contains("/_Recovery/") || path.Contains("\\_Recovery\\");
    }

    private static void ApplyFont(TMP_Text text, TMP_FontAsset sourceFont)
    {
        Undo.RecordObject(text, "Apply Source Sans 3 UI Font");
        text.font = sourceFont;
        text.fontSharedMaterial = sourceFont.material;

        var serializedObject = new SerializedObject(text);
        var sharedMaterialProp = serializedObject.FindProperty("m_sharedMaterial");
        var fontMaterialProp = serializedObject.FindProperty("m_fontMaterial");
        var fontSharedMaterialsProp = serializedObject.FindProperty("m_fontSharedMaterials");
        var fontMaterialsProp = serializedObject.FindProperty("m_fontMaterials");

        if (sharedMaterialProp != null)
            sharedMaterialProp.objectReferenceValue = sourceFont.material;
        if (fontMaterialProp != null)
            fontMaterialProp.objectReferenceValue = null;
        if (fontSharedMaterialsProp != null)
            fontSharedMaterialsProp.ClearArray();
        if (fontMaterialsProp != null)
            fontMaterialsProp.ClearArray();

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        text.UpdateMeshPadding();
        text.SetAllDirty();
        EditorUtility.SetDirty(text);
    }
}
#endif
