using UnityEditor;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

public static class AddPF2eGlyphsToFont
{
    // Unicode code points we need:
    // U+25C6 = 9670  Black Diamond (◆) — action cost
    // U+25C7 = 9671  White Diamond (◇) — free action
    // U+21BB = 8635  Clockwise Open Circle Arrow (↻) — reaction
    private static readonly uint[] RequiredCodePoints = { 9670, 9671, 8635 };

    private const string Exo2Path = "Assets/Fonts/Exo2 SDF.asset";
    private const string FallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";

    [MenuItem("Tools/Add PF2e Glyphs to Exo2 SDF")]
    public static void AddGlyphs()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Exo2Path);
        if (fontAsset == null)
        {
            Debug.LogError("[AddPF2eGlyphs] Exo2 SDF.asset not found.");
            return;
        }

        // Step 1: Ensure LiberationSans fallback is wired up via API
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
        if (fallback != null)
        {
            if (fontAsset.fallbackFontAssetTable == null)
                fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();

            if (!fontAsset.fallbackFontAssetTable.Contains(fallback))
            {
                fontAsset.fallbackFontAssetTable.Add(fallback);
                Debug.Log("[AddPF2eGlyphs] Added LiberationSans SDF Fallback to Exo2 fallback table.");
            }
        }

        // Step 2: Try to add missing glyphs directly to Exo2 SDF atlas
        var missing = new List<uint>();
        foreach (uint cp in RequiredCodePoints)
        {
            if (!fontAsset.HasCharacter((int)cp))
                missing.Add(cp);
        }

        if (missing.Count == 0)
        {
            Debug.Log("[AddPF2eGlyphs] All PF2e glyphs already present in Exo2 SDF.");
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return;
        }

        Debug.Log($"[AddPF2eGlyphs] Missing {missing.Count} glyphs in Exo2. Attempting to add from source TTF...");

        bool ok = fontAsset.TryAddCharacters(missing.ToArray(), out uint[] missingAfter);

        if (ok && (missingAfter == null || missingAfter.Length == 0))
        {
            Debug.Log("[AddPF2eGlyphs] All glyphs added to Exo2 SDF atlas.");
        }
        else if (missingAfter != null && missingAfter.Length > 0)
        {
            string still = string.Join(", ", System.Array.ConvertAll(missingAfter, u => $"U+{u:X4}"));
            Debug.LogWarning($"[AddPF2eGlyphs] Not in Exo2 TTF: {still}. Trying LiberationSans fallback...");

            if (fallback != null)
            {
                bool ok2 = fallback.TryAddCharacters(missingAfter, out uint[] stillMissing2);
                if (ok2 && (stillMissing2 == null || stillMissing2.Length == 0))
                    Debug.Log("[AddPF2eGlyphs] Glyphs added to LiberationSans SDF Fallback atlas.");
                else
                {
                    string s2 = stillMissing2 != null
                        ? string.Join(", ", System.Array.ConvertAll(stillMissing2, u => $"U+{u:X4}"))
                        : "none";
                    Debug.LogError($"[AddPF2eGlyphs] Still missing in fallback: {s2}. Need a font with these symbols.");
                }
                EditorUtility.SetDirty(fallback);
            }
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("[AddPF2eGlyphs] Done.");
    }
}
