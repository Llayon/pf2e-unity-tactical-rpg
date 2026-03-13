using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PF2e.Editor
{
    public static class SourceSans3FontAssetCreator
    {
        private const string RegularSourceFontPath = "Assets/Fonts/Source_Sans_3/static/SourceSans3-Regular.ttf";
        private const string LightSourceFontPath = "Assets/Fonts/Source_Sans_3/static/SourceSans3-Light.ttf";
        private const string RegularTargetFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Source Sans 3 SDF.asset";
        private const string LightTargetFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Source Sans 3 Light SDF.asset";
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasWidth = 1024;
        private const int AtlasHeight = 1024;

        [MenuItem("PF2e/Tools/Create Or Update Source Sans 3 TMP Asset")]
        public static void CreateOrUpdate()
        {
            Font regularSourceFont = AssetDatabase.LoadAssetAtPath<Font>(RegularSourceFontPath);
            if (regularSourceFont == null)
            {
                Debug.LogError($"[SourceSans3FontAssetCreator] Missing source font at '{RegularSourceFontPath}'.");
                return;
            }

            Font lightSourceFont = AssetDatabase.LoadAssetAtPath<Font>(LightSourceFontPath);
            if (lightSourceFont == null)
            {
                Debug.LogError($"[SourceSans3FontAssetCreator] Missing source font at '{LightSourceFontPath}'.");
                return;
            }

            TMP_FontAsset regularFontAsset = CreateOrUpdateFontAsset(
                regularSourceFont,
                RegularSourceFontPath,
                RegularTargetFontAssetPath,
                "Source Sans 3 SDF",
                "Source Sans 3 SDF Atlas",
                "Source Sans 3 SDF Material",
                0.018f,
                0.07f,
                0.09f,
                0f,
                1.0f,
                new Color(0f, 0f, 0f, 0.92f));

            TMP_FontAsset lightFontAsset = CreateOrUpdateFontAsset(
                lightSourceFont,
                LightSourceFontPath,
                LightTargetFontAssetPath,
                "Source Sans 3 Light SDF",
                "Source Sans 3 Light SDF Atlas",
                "Source Sans 3 Light SDF Material",
                0.006f,
                0.035f,
                0.06f,
                -0.12f,
                0.45f,
                new Color(0f, 0f, 0f, 0.68f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (regularFontAsset == null || lightFontAsset == null)
            {
                return;
            }

            Debug.Log("[SourceSans3FontAssetCreator] Updated Source Sans 3 TMP assets (Regular + Light).");
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

        private static TMP_FontAsset CreateOrUpdateFontAsset(
            Font sourceFont,
            string sourceFontPath,
            string targetPath,
            string assetName,
            string atlasName,
            string materialName,
            float faceDilate,
            float outlineWidth,
            float outlineSoftness,
            float weightNormal,
            float weightBold,
            Color outlineColor)
        {
            FontEngine.InitializeFontEngine();
            if (FontEngine.LoadFontFace(sourceFont, SamplingPointSize) != FontEngineError.Success)
            {
                Debug.LogError($"[SourceSans3FontAssetCreator] Unable to load font face for '{sourceFontPath}'. Check import settings and Include Font Data.");
                return null;
            }

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
                    Debug.LogError($"[SourceSans3FontAssetCreator] TMP_FontAsset.CreateFontAsset returned null for '{sourceFontPath}'.");
                    return null;
                }

                fontAsset.name = assetName;
                AssetDatabase.CreateAsset(fontAsset, targetPath);
                AddSubAssets(fontAsset, atlasName, materialName);
            }

            ApplySubAssetNames(fontAsset, atlasName, materialName);
            ApplyStaticAtlas(fontAsset, sourceFontPath, atlasName, materialName);
            ConfigureMaterial(fontAsset, materialName, faceDilate, outlineWidth, outlineSoftness, weightNormal, weightBold, outlineColor);

            fontAsset.name = assetName;
            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        private static void ApplyStaticAtlas(TMP_FontAsset fontAsset, string sourceFontPath, string atlasName, string materialName)
        {
            string characterSet = BuildCharacterSet();
            fontAsset.TryAddCharacters(characterSet, out string missingCharacters, true);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.creationSettings = new FontAssetCreationSettings
            {
                sourceFontFileName = string.Empty,
                sourceFontFileGUID = AssetDatabase.AssetPathToGUID(sourceFontPath),
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
            ApplySubAssetNames(fontAsset, atlasName, materialName);

            if (!string.IsNullOrEmpty(missingCharacters))
            {
                Debug.LogWarning($"[SourceSans3FontAssetCreator] Missing characters while baking '{sourceFontPath}': {missingCharacters}");
            }
        }

        private static void ConfigureMaterial(
            TMP_FontAsset fontAsset,
            string materialName,
            float faceDilate,
            float outlineWidth,
            float outlineSoftness,
            float weightNormal,
            float weightBold,
            Color outlineColor)
        {
            Material material = fontAsset.material;
            if (material == null)
                return;

            material.name = materialName;
            material.SetFloat(ShaderUtilities.ID_FaceDilate, faceDilate);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
            material.SetFloat(ShaderUtilities.ID_OutlineSoftness, outlineSoftness);
            material.SetFloat(ShaderUtilities.ID_WeightNormal, weightNormal);
            material.SetFloat(ShaderUtilities.ID_WeightBold, weightBold);
            material.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
            if (fontAsset.atlasTexture != null)
                material.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);
            EditorUtility.SetDirty(material);
        }

        private static void AddSubAssets(TMP_FontAsset fontAsset, string atlasName, string materialName)
        {
            if (fontAsset.atlasTextures != null)
            {
                foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
                {
                    if (atlasTexture == null)
                        continue;

                    atlasTexture.name = atlasName;
                    AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
                    EditorUtility.SetDirty(atlasTexture);
                }
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = materialName;
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                EditorUtility.SetDirty(fontAsset.material);
            }
        }

        private static void ApplySubAssetNames(TMP_FontAsset fontAsset, string atlasName, string materialName)
        {
            if (fontAsset.atlasTextures != null)
            {
                foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
                {
                    if (atlasTexture == null)
                        continue;

                    if (!string.IsNullOrEmpty(atlasName))
                        atlasTexture.name = atlasName;
                    EditorUtility.SetDirty(atlasTexture);
                }
            }

            if (fontAsset.material != null)
            {
                if (!string.IsNullOrEmpty(materialName))
                    fontAsset.material.name = materialName;
                EditorUtility.SetDirty(fontAsset.material);
            }
        }
    }
}
