using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class LoraFontAssetCreator
{
    private const string SourceFontPath = "Assets/Fonts/Lora/Lora-VariableFont_wght.ttf";
    private const string ResourceFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Lora SDF.asset";
    private const string OutlineMatPath = "Assets/Fonts/Lora SDF Outline.mat";
    private const int SamplingPointSize = 90;
    private const int AtlasPadding = 8;
    private const int AtlasWidth = 1024;
    private const int AtlasHeight = 1024;

    [MenuItem("PF2e/Tools/Create Or Update Lora TMP Assets")]
    public static void CreateOrUpdate()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[LoraFontAssetCreator] Missing source font at '{SourceFontPath}'.");
            return;
        }

        TMP_FontAsset resourceFont = CreateOrUpdateFontAsset(ResourceFontAssetPath, sourceFont);

        if (resourceFont == null)
        {
            Debug.LogError("[LoraFontAssetCreator] Failed to create or update the Lora TMP asset.");
            return;
        }

        UpdateOutlineMaterial(resourceFont);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[LoraFontAssetCreator] Updated the shared Lora TMP asset and switched it to static atlas mode.");
    }

    private static TMP_FontAsset CreateOrUpdateFontAsset(string targetPath, Font sourceFont)
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetPath);
        if (fontAsset == null)
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                SamplingPointSize,
                AtlasPadding,
                GlyphRenderMode.SDFAA,
                AtlasWidth,
                AtlasHeight,
                AtlasPopulationMode.Dynamic,
                false);

            if (fontAsset == null)
            {
                return null;
            }

            fontAsset.name = "Lora SDF";
            AssetDatabase.CreateAsset(fontAsset, targetPath);
            AddSubAssets(fontAsset);
        }

        Texture2D[] atlasTextures = fontAsset.atlasTextures;
        if (atlasTextures != null)
        {
            foreach (Texture2D atlasTexture in atlasTextures)
            {
                if (atlasTexture == null)
                {
                    continue;
                }

                atlasTexture.name = "Lora SDF Atlas";
                EditorUtility.SetDirty(atlasTexture);
            }
        }

        Material material = fontAsset.material;
        if (material != null)
        {
            material.name = "Lora SDF Material";
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            material.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
            material.SetFloat(ShaderUtilities.ID_WeightBold, 0.75f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            if (fontAsset.atlasTexture != null)
            {
                material.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);
            }

            EditorUtility.SetDirty(material);
        }

        string characterSet = BuildCharacterSet();
        fontAsset.TryAddCharacters(characterSet, out string missingCharacters, true);
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
        fontAsset.creationSettings = new FontAssetCreationSettings
        {
            sourceFontFileName = string.Empty,
            sourceFontFileGUID = AssetDatabase.AssetPathToGUID(SourceFontPath),
            faceIndex = 0,
            pointSizeSamplingMode = 1,
            pointSize = SamplingPointSize,
            padding = AtlasPadding,
            paddingMode = 1,
            packingMode = 4,
            atlasWidth = AtlasWidth,
            atlasHeight = AtlasHeight,
            characterSetSelectionMode = 7,
            characterSequence = characterSet,
            referencedFontAssetGUID = string.Empty,
            referencedTextAssetGUID = string.Empty,
            fontStyle = 0,
            fontStyleModifier = 0,
            renderMode = (int)GlyphRenderMode.SDFAA,
            includeFontFeatures = true
        };
        fontAsset.ReadFontAssetDefinition();

        if (fontAsset.atlasTextures != null)
        {
            foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
            {
                if (atlasTexture == null)
                {
                    continue;
                }

                atlasTexture.name = "Lora SDF Atlas";
                EditorUtility.SetDirty(atlasTexture);
            }
        }

        fontAsset.name = "Lora SDF";
        EditorUtility.SetDirty(fontAsset);

        if (!string.IsNullOrEmpty(missingCharacters))
        {
            Debug.LogWarning($"[LoraFontAssetCreator] Missing characters while baking '{targetPath}': {missingCharacters}");
        }

        return fontAsset;
    }

    private static void AddSubAssets(TMP_FontAsset fontAsset)
    {
        if (fontAsset.atlasTextures != null)
        {
            foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
            {
                if (atlasTexture == null)
                {
                    continue;
                }

                atlasTexture.name = "Lora SDF Atlas";
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
                EditorUtility.SetDirty(atlasTexture);
            }
        }

        if (fontAsset.material != null)
        {
            fontAsset.material.name = "Lora SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            EditorUtility.SetDirty(fontAsset.material);
        }
    }

    private static void UpdateOutlineMaterial(TMP_FontAsset fontAsset)
    {
        Material outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath);
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

        if (fontAsset.atlasTexture != null)
        {
            outlineMat.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);
        }

        outlineMat.EnableKeyword("OUTLINE_ON");
        outlineMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.08f);
        outlineMat.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0.06f);
        outlineMat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.85f));
        outlineMat.EnableKeyword("UNDERLAY_ON");
        outlineMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.4f);
        outlineMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.4f);
        outlineMat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.0f);
        outlineMat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
        outlineMat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.5f));
        EditorUtility.SetDirty(outlineMat);
    }

    private static string BuildCharacterSet()
    {
        StringBuilder builder = new StringBuilder(512);
        AppendRange(builder, 32, 126);
        AppendRange(builder, 160, 255);
        AppendRange(builder, 8192, 8303);
        builder.Append('\u20AC');
        builder.Append('\u2122');
        builder.Append('\u2192');
        builder.Append('\u25A1');
        return builder.ToString();
    }

    private static void AppendRange(StringBuilder builder, int startInclusive, int endInclusive)
    {
        for (int i = startInclusive; i <= endInclusive; i++)
        {
            builder.Append((char)i);
        }
    }
}
