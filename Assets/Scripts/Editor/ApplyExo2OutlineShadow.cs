using UnityEditor;
using UnityEngine;
using TMPro;

public static class ApplyExo2OutlineShadow
{
    private const string PresetPath = "Assets/Fonts/Exo2 SDF Outline.mat";

    [MenuItem("Tools/Apply Exo 2 Outline + Shadow")]
    public static void Apply()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Exo2 SDF.asset");
        if (fontAsset == null || fontAsset.material == null)
        {
            Debug.LogError("[ApplyExo2OutlineShadow] Exo2 SDF font asset or material not found.");
            return;
        }

        // Create a standalone material preset (copy of the font material)
        var existing = AssetDatabase.LoadAssetAtPath<Material>(PresetPath);
        Material mat;
        if (existing != null)
        {
            mat = existing;
            mat.CopyPropertiesFromMaterial(fontAsset.material);
        }
        else
        {
            mat = new Material(fontAsset.material);
            mat.name = "Exo2 SDF Outline";
        }

        // Must reference the same shader + atlas
        mat.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);

        // Outline — thin, soft, dark
        mat.EnableKeyword("OUTLINE_ON");
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0.08f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.85f));

        // Underlay (drop shadow) — subtle offset down-right
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.4f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.4f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
        mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.5f));

        if (existing == null)
            AssetDatabase.CreateAsset(mat, PresetPath);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Find ALL TMP_Text in scene (including inactive) using Exo2 SDF and assign the preset
        int count = 0;
        foreach (var tmp in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            // Skip assets (prefabs etc), only scene objects
            if (EditorUtility.IsPersistent(tmp)) continue;
            if (tmp.font == fontAsset)
            {
                tmp.fontSharedMaterial = mat;
                EditorUtility.SetDirty(tmp);
                EditorUtility.SetDirty(tmp.gameObject);
                count++;
            }
        }

        Debug.Log($"[ApplyExo2OutlineShadow] Material preset saved to {PresetPath}. Applied to {count} TMP components.");
    }
}
