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
        private const string SourceFontPath = "Assets/Fonts/Source_Sans_3/static/SourceSans3-Regular.ttf";
        private const string TargetFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Source Sans 3 SDF.asset";
        private const string LegacyMaterialPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Source Sans 3 SDF Material.mat";
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasWidth = 1024;
        private const int AtlasHeight = 1024;

        [MenuItem("PF2e/Tools/Create Or Update Source Sans 3 TMP Asset")]
        public static void CreateOrUpdate()
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[SourceSans3FontAssetCreator] Missing source font at '{SourceFontPath}'.");
                return;
            }

            FontEngine.InitializeFontEngine();
            if (FontEngine.LoadFontFace(sourceFont, 90) != FontEngineError.Success)
            {
                Debug.LogError($"[SourceSans3FontAssetCreator] Unable to load font face for '{SourceFontPath}'. Check import settings and Include Font Data.");
                return;
            }

            AssetDatabase.DeleteAsset(LegacyMaterialPath);
            AssetDatabase.DeleteAsset(TargetFontAssetPath);

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
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
                Debug.LogError("[SourceSans3FontAssetCreator] TMP_FontAsset.CreateFontAsset returned null.");
                return;
            }

            fontAsset.name = "Source Sans 3 SDF";
            AssetDatabase.CreateAsset(fontAsset, TargetFontAssetPath);

            Texture2D[] atlasTextures = fontAsset.atlasTextures;
            if (atlasTextures != null)
            {
                foreach (Texture2D atlasTexture in atlasTextures)
                {
                    if (atlasTexture == null)
                    {
                        continue;
                    }

                    atlasTexture.name = "Source Sans 3 SDF Atlas";
                    AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
                    EditorUtility.SetDirty(atlasTexture);
                }
            }

            Material material = fontAsset.material;
            if (material != null)
            {
                material.name = "Source Sans 3 SDF Material";
                material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.03f);
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.09f);
                material.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0.14f);
                material.SetFloat(ShaderUtilities.ID_WeightBold, 1.0f);
                material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.92f));
                AssetDatabase.AddObjectToAsset(material, fontAsset);
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
            EditorUtility.SetDirty(fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!string.IsNullOrEmpty(missingCharacters))
            {
                Debug.LogWarning($"[SourceSans3FontAssetCreator] Missing characters while baking static atlas: {missingCharacters}");
            }

            Debug.Log("[SourceSans3FontAssetCreator] Recreated Source Sans 3 TMP font asset.");
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
}
