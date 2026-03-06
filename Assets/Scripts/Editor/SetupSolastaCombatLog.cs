using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public static class SetupSolastaCombatLog
{
    [MenuItem("Tools/Setup Solasta Combat Log")]
    public static void Setup()
    {
        // 1. Generate vertical gradient texture (opaque dark at top -> transparent at bottom)
        var gradientPath = "Assets/UI/Sprites/GradientFadeDown.png";
        CreateGradientTexture(gradientPath, 4, 64);
        AssetDatabase.Refresh();

        // Configure as sprite
        var importer = AssetImporter.GetAtPath(gradientPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        // 2. Find CombatLogHUD
        var logHUD = FindInCanvas("CombatLogHUD");
        if (logHUD == null)
        {
            Debug.LogError("[SolastaCombatLog] CombatLogHUD not found.");
            return;
        }

        // 3. Set background: no sprite, dark semi-transparent
        var bgImage = logHUD.GetComponent<Image>();
        if (bgImage != null)
        {
            Undo.RecordObject(bgImage, "Solasta Combat Log");
            bgImage.sprite = null;
            bgImage.type = Image.Type.Simple;
            bgImage.color = new Color(0.04f, 0.04f, 0.07f, 0.70f);
            bgImage.raycastTarget = false;
            EditorUtility.SetDirty(bgImage);
        }

        // 4. Create or find gradient fade overlay at top
        var fadeOverlay = logHUD.transform.Find("LogFadeOverlay");
        if (fadeOverlay == null)
        {
            var fadeGO = new GameObject("LogFadeOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fadeGO.transform.SetParent(logHUD.transform, false);
            fadeOverlay = fadeGO.transform;
            Undo.RegisterCreatedObjectUndo(fadeGO, "Create Log Fade Overlay");
        }

        // Position: fill top 40% of log panel
        var fadeRT = fadeOverlay.GetComponent<RectTransform>();
        fadeRT.anchorMin = new Vector2(0f, 0.6f);
        fadeRT.anchorMax = new Vector2(1f, 1f);
        fadeRT.pivot = new Vector2(0.5f, 1f);
        fadeRT.anchoredPosition = Vector2.zero;
        fadeRT.sizeDelta = Vector2.zero;

        // Set gradient sprite
        var gradientSprite = AssetDatabase.LoadAssetAtPath<Sprite>(gradientPath);
        var fadeImage = fadeOverlay.GetComponent<Image>();
        if (fadeImage != null && gradientSprite != null)
        {
            fadeImage.sprite = gradientSprite;
            fadeImage.type = Image.Type.Simple;
            fadeImage.color = new Color(0.04f, 0.04f, 0.07f, 1f);
            fadeImage.raycastTarget = false;
            fadeImage.preserveAspect = false;
            EditorUtility.SetDirty(fadeImage);
        }

        // Make sure overlay renders on top (last sibling)
        fadeOverlay.SetAsLastSibling();

        Debug.Log("[SolastaCombatLog] Done. Background + gradient fade applied.");
    }

    private static void CreateGradientTexture(string path, int width, int height)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++)
        {
            // y=0 is bottom (transparent), y=height-1 is top (opaque)
            float alpha = (float)y / (height - 1);
            var col = new Color(1f, 1f, 1f, alpha);
            for (int x = 0; x < width; x++)
                tex.SetPixel(x, y, col);
        }
        tex.Apply();

        var dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log($"[SolastaCombatLog] Gradient texture saved: {path}");
    }

    private static GameObject FindInCanvas(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) return go;

        foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (rt.name == name && rt.root.name == "Canvas")
                return rt.gameObject;
        }
        return null;
    }
}
