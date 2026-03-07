using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;

public static class SetupLoraForCombatLog
{
    private const string FontTTFPath = "Assets/Fonts/Lora/Lora-VariableFont_wght.ttf";
    private const string FontAssetPath = "Assets/Fonts/Lora SDF.asset";
    private const string OutlineMatPath = "Assets/Fonts/Lora SDF Outline.mat";

    [MenuItem("Tools/PF2e/Setup Lora for Combat Log")]
    public static void Setup()
    {
        // Step 1: Create or load Lora SDF font asset
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontTTFPath);
            if (font == null)
            {
                Debug.LogError($"[SetupLora] Font not found at {FontTTFPath}");
                return;
            }

            fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
            if (fontAsset == null)
            {
                Debug.LogError("[SetupLora] Failed to create TMP_FontAsset.");
                return;
            }

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            if (fontAsset.atlasTexture != null)
            {
                fontAsset.atlasTexture.name = "Lora SDF Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "Lora SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SetupLora] Created font asset: {FontAssetPath}");
        }
        else
        {
            Debug.Log($"[SetupLora] Font asset already exists: {FontAssetPath}");
        }

        // Step 2: Create outline + shadow material preset
        var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath);
        if (outlineMat == null)
        {
            outlineMat = new Material(fontAsset.material);
            outlineMat.name = "Lora SDF Outline";
            AssetDatabase.CreateAsset(outlineMat, OutlineMatPath);
        }
        else
        {
            outlineMat.CopyPropertiesFromMaterial(fontAsset.material);
        }

        outlineMat.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);

        // Outline — slightly thinner for serif (serifs + thick outline = muddy)
        outlineMat.EnableKeyword("OUTLINE_ON");
        outlineMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.08f);
        outlineMat.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0.06f);
        outlineMat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.85f));

        // Drop shadow
        outlineMat.EnableKeyword("UNDERLAY_ON");
        outlineMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.4f);
        outlineMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.4f);
        outlineMat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.0f);
        outlineMat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
        outlineMat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.5f));

        EditorUtility.SetDirty(outlineMat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SetupLora] Outline material saved: {OutlineMatPath}");

        // Step 3: Apply to all combat log TMP components in scene
        int count = 0;
        foreach (var tmp in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (EditorUtility.IsPersistent(tmp)) continue;

            // Change components using Exo2 or old Lora SDF
            if (tmp.font != null && (tmp.font.name.Contains("Exo2") || tmp.font.name.Contains("Lora")))
            {
                tmp.font = fontAsset;
                tmp.fontSharedMaterial = outlineMat;
                EditorUtility.SetDirty(tmp);
                EditorUtility.SetDirty(tmp.gameObject);
                count++;
            }
        }

        Debug.Log($"[SetupLora] Applied Lora + outline to {count} TMP components. Save the scene!");
    }
}
