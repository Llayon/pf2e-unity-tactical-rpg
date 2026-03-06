using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;


public static class CreateExo2FontAsset
{
    [MenuItem("Tools/Create Exo 2 TMP Font Asset")]
    public static void Create()
    {
        var fontPath = "Assets/Fonts/Exo2-VariableFont_wght.ttf";
        var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null)
        {
            Debug.LogError($"[CreateExo2] Font not found at {fontPath}");
            return;
        }

        // Create TMP font asset with default settings (SDF, sampling 90, padding 9, atlas 512)
        var fontAsset = TMP_FontAsset.CreateFontAsset(
            font,
            90,   // sampling point size
            9,    // padding
            GlyphRenderMode.SDFAA,
            512,  // atlas width
            512   // atlas height
        );

        if (fontAsset == null)
        {
            Debug.LogError("[CreateExo2] Failed to create TMP_FontAsset.");
            return;
        }

        var outputPath = "Assets/Fonts/Exo2 SDF.asset";
        AssetDatabase.CreateAsset(fontAsset, outputPath);

        // Also save the atlas texture
        if (fontAsset.atlasTexture != null)
        {
            fontAsset.atlasTexture.name = "Exo2 SDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        }

        // Save material
        if (fontAsset.material != null)
        {
            fontAsset.material.name = "Exo2 SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CreateExo2] TMP Font Asset created at {outputPath}");
        EditorGUIUtility.PingObject(fontAsset);
    }
}
