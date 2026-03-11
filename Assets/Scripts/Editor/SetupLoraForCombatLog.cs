using UnityEditor;
using UnityEngine;
using TMPro;

public static class SetupLoraForCombatLog
{
    private const string FontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Lora SDF.asset";
    private const string OutlineMatPath = "Assets/Fonts/Lora SDF Outline.mat";

    [MenuItem("Tools/PF2e/Setup Lora for Combat Log")]
    public static void Setup()
    {
        LoraFontAssetCreator.CreateOrUpdate();

        // Step 1: Create or load Lora SDF font asset
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            Debug.LogError($"[SetupLora] Missing font asset at {FontAssetPath} after LoraFontAssetCreator pass.");
            return;
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
