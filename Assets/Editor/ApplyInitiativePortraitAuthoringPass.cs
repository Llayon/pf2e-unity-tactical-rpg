#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PF2e.Data;
using PF2e.Managers;
using PF2e.Presentation;

public static class ApplyInitiativePortraitAuthoringPass
{
    private const string InitiativeSlotPrefabPath = "Assets/Prefabs/InitiativeSlot.prefab";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string EncounterFlowPrefabScenePath = "Assets/Scenes/EncounterFlowPrefabScene.unity";
    private const string PortraitLibraryAssetPath = "Assets/ScriptableObjects/EncounterActorPortraitLibrary.asset";

    private const string FighterPortraitPath = "Assets/UI/Sprites/newfighterportrait.png";
    private const string WizardPortraitPath = "Assets/UI/Sprites/newwizardportrait.png";
    private const string GoblinPortraitPath = "Assets/UI/Sprites/newgoblinportrait.png";
    private const string PlayerFramePath = "Assets/UI/Sprites/newpcramka.png";
    private const string EnemyFramePath = "Assets/UI/Sprites/newenemyramka.png";

    private static readonly Vector2 SlotSize = new(73f, 90f);
    private static readonly Color HpStripBackgroundColor = new(0f, 0f, 0f, 0.55f);
    private static readonly Color HpStripFillColor = new(0.38f, 0.86f, 0.43f, 1f);
    private static readonly Color DamageOverlayColor = new(0.7f, 0.05f, 0.05f, 0.35f);

    [MenuItem("PF2e/UI/Apply Initiative Portrait Authoring")]
    public static void Apply()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.isDirty)
        {
            Debug.LogError($"[InitiativePortraitAuthoring] Active scene '{activeScene.path}' has unsaved changes. Reload or save before applying portrait authoring.");
            return;
        }

        string originalScenePath = activeScene.path;
        try
        {
            var fighterPortrait = LoadPreparedSprite(FighterPortraitPath, "fighter portrait");
            var wizardPortrait = LoadPreparedSprite(WizardPortraitPath, "wizard portrait");
            var goblinPortrait = LoadPreparedSprite(GoblinPortraitPath, "goblin portrait");
            var playerFrame = LoadPreparedSprite(PlayerFramePath, "player frame");
            var enemyFrame = LoadPreparedSprite(EnemyFramePath, "enemy frame");

            var portraitLibrary = EnsurePortraitLibrary(fighterPortrait, wizardPortrait, goblinPortrait);
            ApplyInitiativeSlotPrefab();
            ApplyScenePortraitBindings(SampleScenePath, portraitLibrary, playerFrame, enemyFrame);

            if (File.Exists(EncounterFlowPrefabScenePath))
                ApplyScenePortraitBindings(EncounterFlowPrefabScenePath, portraitLibrary, playerFrame, enemyFrame);

            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(originalScenePath))
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);

            Debug.Log("[InitiativePortraitAuthoring] Portrait authoring pass applied.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[InitiativePortraitAuthoring] Failed: {ex}");
            throw;
        }
    }

    private static Sprite LoadPreparedSprite(string assetPath, string label)
    {
        EnsureSpriteImportSettings(assetPath);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
            throw new MissingReferenceException($"[InitiativePortraitAuthoring] Could not load {label} sprite at '{assetPath}'.");

        return sprite;
    }

    private static void EnsureSpriteImportSettings(string assetPath)
    {
        if (!File.Exists(assetPath))
            throw new FileNotFoundException($"[InitiativePortraitAuthoring] Missing sprite asset '{assetPath}'.");

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            throw new MissingReferenceException($"[InitiativePortraitAuthoring] No TextureImporter for '{assetPath}'.");

        bool changed = false;
        changed |= SetIfDifferent(() => importer.textureType, value => importer.textureType = value, TextureImporterType.Sprite);
        changed |= SetIfDifferent(() => importer.spriteImportMode, value => importer.spriteImportMode = value, SpriteImportMode.Single);
        changed |= SetIfDifferent(() => importer.alphaIsTransparency, value => importer.alphaIsTransparency = value, true);
        changed |= SetIfDifferent(() => importer.mipmapEnabled, value => importer.mipmapEnabled = value, false);
        changed |= SetIfDifferent(() => importer.spritePixelsPerUnit, value => importer.spritePixelsPerUnit = value, 100f);
        changed |= SetIfDifferent(() => importer.spriteBorder, value => importer.spriteBorder = value, Vector4.zero);
        changed |= SetIfDifferent(() => importer.wrapMode, value => importer.wrapMode = value, TextureWrapMode.Clamp);
        changed |= SetIfDifferent(() => importer.filterMode, value => importer.filterMode = value, FilterMode.Bilinear);

        if (changed)
            importer.SaveAndReimport();
    }

    private static bool SetIfDifferent<T>(System.Func<T> getter, System.Action<T> setter, T expected)
    {
        if (Equals(getter(), expected))
            return false;

        setter(expected);
        return true;
    }

    private static EncounterActorPortraitLibrary EnsurePortraitLibrary(Sprite fighterPortrait, Sprite wizardPortrait, Sprite goblinPortrait)
    {
        var library = AssetDatabase.LoadAssetAtPath<EncounterActorPortraitLibrary>(PortraitLibraryAssetPath);
        if (library == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PortraitLibraryAssetPath) ?? "Assets");
            library = ScriptableObject.CreateInstance<EncounterActorPortraitLibrary>();
            AssetDatabase.CreateAsset(library, PortraitLibraryAssetPath);
        }

        var serialized = new SerializedObject(library);
        var entries = serialized.FindProperty("entries");
        entries.ClearArray();
        SetEntry(entries, 0, "fighter", fighterPortrait);
        SetEntry(entries, 1, "wizard", wizardPortrait);
        SetEntry(entries, 2, "goblin_1", goblinPortrait);
        SetEntry(entries, 3, "goblin_2", goblinPortrait);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);
        return library;
    }

    private static void SetEntry(SerializedProperty entries, int index, string actorId, Sprite portrait)
    {
        entries.InsertArrayElementAtIndex(index);
        var entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("actorId").stringValue = actorId;
        entry.FindPropertyRelative("portraitSprite").objectReferenceValue = portrait;
    }

    private static void ApplyScenePortraitBindings(
        string scenePath,
        EncounterActorPortraitLibrary portraitLibrary,
        Sprite playerFrame,
        Sprite enemyFrame)
    {
        if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
            return;

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        EntityManager entityManager = null;
        InitiativeBarController initiativeBarController = null;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (entityManager == null)
                entityManager = roots[i].GetComponentInChildren<EntityManager>(true);
            if (initiativeBarController == null)
                initiativeBarController = roots[i].GetComponentInChildren<InitiativeBarController>(true);
        }

        if (entityManager != null)
        {
            var serializedEntityManager = new SerializedObject(entityManager);
            serializedEntityManager.FindProperty("portraitLibrary").objectReferenceValue = portraitLibrary;
            serializedEntityManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(entityManager);
        }

        if (initiativeBarController != null)
        {
            var serializedInitiativeBar = new SerializedObject(initiativeBarController);
            serializedInitiativeBar.FindProperty("playerFrameSprite").objectReferenceValue = playerFrame;
            serializedInitiativeBar.FindProperty("enemyFrameSprite").objectReferenceValue = enemyFrame;
            serializedInitiativeBar.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(initiativeBarController);

            var slotsContainer = serializedInitiativeBar.FindProperty("slotsContainer").objectReferenceValue as Transform;
            if (slotsContainer != null && slotsContainer.TryGetComponent<HorizontalLayoutGroup>(out var slotsLayout))
            {
                slotsLayout.spacing = 2f;
                EditorUtility.SetDirty(slotsLayout);
            }
        }

        EditorSceneManager.SaveScene(scene);
    }

    private static void ApplyInitiativeSlotPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(InitiativeSlotPrefabPath);
        try
        {
            var slot = root.GetComponent<InitiativeSlot>();
            if (slot == null)
                throw new MissingComponentException($"InitiativeSlot component missing on prefab {InitiativeSlotPrefabPath}.");

            var rootRect = root.GetComponent<RectTransform>();
            var rootLayout = GetOrAddComponent<LayoutElement>(root);
            var nameText = GetRequiredChildComponent<TMP_Text>(root.transform, "NameText");
            var hpBarBackgroundRect = GetRequiredChild(root.transform, "HPBarBackground") as RectTransform;
            var hpBarBackgroundImage = GetRequiredChildComponent<Image>(root.transform, "HPBarBackground");
            var hpBarFill = GetRequiredChildComponent<Image>(root.transform, "HPBarBackground/HPBarFill");
            var activeHighlight = GetRequiredChild(root.transform, "ActiveHighlight").gameObject;
            var delayedBadgeRoot = GetRequiredChild(root.transform, "DelayedBadge").gameObject;
            var delayedBadgeBackground = delayedBadgeRoot.GetComponent<Image>();
            var delayedBadgeText = delayedBadgeRoot.GetComponentInChildren<TMP_Text>(true);

            rootRect.sizeDelta = SlotSize;
            rootLayout.minWidth = SlotSize.x;
            rootLayout.preferredWidth = SlotSize.x;
            rootLayout.flexibleWidth = 0f;
            rootLayout.minHeight = SlotSize.y;
            rootLayout.preferredHeight = SlotSize.y;
            rootLayout.flexibleHeight = 0f;

            if (hpBarBackgroundRect != null)
            {
                hpBarBackgroundRect.anchorMin = new Vector2(0f, 0f);
                hpBarBackgroundRect.anchorMax = new Vector2(1f, 0f);
                hpBarBackgroundRect.pivot = new Vector2(0.5f, 0f);
                hpBarBackgroundRect.anchoredPosition = new Vector2(0f, 2f);
                hpBarBackgroundRect.sizeDelta = new Vector2(-6f, 4f);
            }

            hpBarBackgroundImage.color = HpStripBackgroundColor;
            hpBarBackgroundImage.raycastTarget = false;
            hpBarFill.color = HpStripFillColor;
            hpBarFill.raycastTarget = false;

            var portraitMask = EnsureRectChild(root.transform, "PortraitMask");
            portraitMask.anchorMin = Vector2.zero;
            portraitMask.anchorMax = Vector2.one;
            portraitMask.offsetMin = Vector2.zero;
            portraitMask.offsetMax = Vector2.zero;
            GetOrAddComponent<RectMask2D>(portraitMask.gameObject);
            portraitMask.SetAsFirstSibling();

            var portrait = EnsureImageChild(portraitMask, "Portrait");
            portrait.rectTransform.anchorMin = Vector2.zero;
            portrait.rectTransform.anchorMax = Vector2.one;
            portrait.rectTransform.offsetMin = Vector2.zero;
            portrait.rectTransform.offsetMax = Vector2.zero;
            portrait.preserveAspect = false;
            portrait.raycastTarget = false;
            portrait.color = Color.white;
            portrait.gameObject.SetActive(false);
            var portraitAspectFitter = GetOrAddComponent<AspectRatioFitter>(portrait.gameObject);
            portraitAspectFitter.aspectMode = AspectRatioFitter.AspectMode.None;
            portraitAspectFitter.aspectRatio = 1f;

            var damageOverlay = EnsureImageChild(portraitMask, "DamageOverlay");
            damageOverlay.rectTransform.anchorMin = Vector2.zero;
            damageOverlay.rectTransform.anchorMax = Vector2.one;
            damageOverlay.rectTransform.offsetMin = Vector2.zero;
            damageOverlay.rectTransform.offsetMax = Vector2.zero;
            damageOverlay.raycastTarget = false;
            damageOverlay.type = Image.Type.Filled;
            damageOverlay.fillMethod = Image.FillMethod.Vertical;
            damageOverlay.fillOrigin = (int)Image.OriginVertical.Bottom;
            damageOverlay.fillAmount = 0f;
            damageOverlay.gameObject.SetActive(false);
            damageOverlay.transform.SetAsLastSibling();

            var frame = EnsureImageChild(root.transform, "Frame");
            frame.rectTransform.anchorMin = Vector2.zero;
            frame.rectTransform.anchorMax = Vector2.one;
            frame.rectTransform.offsetMin = Vector2.zero;
            frame.rectTransform.offsetMax = Vector2.zero;
            frame.type = Image.Type.Simple;
            frame.pixelsPerUnitMultiplier = 1f;
            frame.preserveAspect = false;
            frame.raycastTarget = false;
            frame.color = Color.white;
            frame.gameObject.SetActive(false);
            frame.transform.SetAsLastSibling();

            var duplicateBadgeRoot = EnsureRectChild(root.transform, "DuplicateBadge");
            duplicateBadgeRoot.anchorMin = new Vector2(0f, 1f);
            duplicateBadgeRoot.anchorMax = new Vector2(0f, 1f);
            duplicateBadgeRoot.pivot = new Vector2(0f, 1f);
            duplicateBadgeRoot.anchoredPosition = new Vector2(2f, -2f);
            duplicateBadgeRoot.sizeDelta = new Vector2(16f, 14f);
            duplicateBadgeRoot.gameObject.SetActive(false);
            duplicateBadgeRoot.transform.SetAsLastSibling();

            var duplicateBadgeBackground = GetOrAddComponent<Image>(duplicateBadgeRoot.gameObject);
            duplicateBadgeBackground.raycastTarget = false;
            var duplicateBadgeText = EnsureBadgeLabel(duplicateBadgeRoot, delayedBadgeText);
            duplicateBadgeText.gameObject.name = "Label";

            delayedBadgeRoot.transform.SetAsLastSibling();

            var serialized = new SerializedObject(slot);
            serialized.FindProperty("nameText").objectReferenceValue = nameText;
            serialized.FindProperty("hpBarFill").objectReferenceValue = hpBarFill;
            serialized.FindProperty("background").objectReferenceValue = root.GetComponent<Image>();
            serialized.FindProperty("portraitMaskRect").objectReferenceValue = portraitMask;
            serialized.FindProperty("portraitImage").objectReferenceValue = portrait;
            serialized.FindProperty("portraitAspectFitter").objectReferenceValue = portraitAspectFitter;
            serialized.FindProperty("damageOverlay").objectReferenceValue = damageOverlay;
            serialized.FindProperty("frameImage").objectReferenceValue = frame;
            serialized.FindProperty("activeHighlight").objectReferenceValue = activeHighlight;
            serialized.FindProperty("duplicateBadgeRoot").objectReferenceValue = duplicateBadgeRoot.gameObject;
            serialized.FindProperty("duplicateBadgeBackground").objectReferenceValue = duplicateBadgeBackground;
            serialized.FindProperty("duplicateBadgeText").objectReferenceValue = duplicateBadgeText;
            serialized.FindProperty("delayedBadgeRoot").objectReferenceValue = delayedBadgeRoot;
            serialized.FindProperty("delayedBadgeBackground").objectReferenceValue = delayedBadgeBackground;
            serialized.FindProperty("delayedBadgeText").objectReferenceValue = delayedBadgeText;
            serialized.FindProperty("playerPortraitMaskOffsetMin").vector2Value = Vector2.zero;
            serialized.FindProperty("playerPortraitMaskOffsetMax").vector2Value = Vector2.zero;
            serialized.FindProperty("enemyPortraitMaskOffsetMin").vector2Value = Vector2.zero;
            serialized.FindProperty("enemyPortraitMaskOffsetMax").vector2Value = Vector2.zero;
            serialized.FindProperty("neutralPortraitMaskOffsetMin").vector2Value = Vector2.zero;
            serialized.FindProperty("neutralPortraitMaskOffsetMax").vector2Value = Vector2.zero;
            serialized.FindProperty("fixedPreferredWidth").floatValue = SlotSize.x;
            serialized.FindProperty("fixedPreferredHeight").floatValue = SlotSize.y;
            serialized.FindProperty("activeScaleFactor").floatValue = 1f;
            serialized.FindProperty("damageOverlayColor").colorValue = DamageOverlayColor;
            serialized.FindProperty("actedFrameTint").colorValue = new Color(0.72f, 0.76f, 0.8f, 1f);
            serialized.FindProperty("actedPortraitTint").colorValue = new Color(0.72f, 0.72f, 0.72f, 1f);
            serialized.FindProperty("actedAlphaMultiplier").floatValue = 0.62f;
            serialized.FindProperty("hpStripHighColor").colorValue = new Color(0.38f, 0.86f, 0.43f, 1f);
            serialized.FindProperty("hpStripMidColor").colorValue = new Color(0.95f, 0.8f, 0.28f, 1f);
            serialized.FindProperty("hpStripLowColor").colorValue = new Color(0.93f, 0.31f, 0.26f, 1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(slot);
            PrefabUtility.SaveAsPrefabAsset(root, InitiativeSlotPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static RectTransform EnsureRectChild(Transform parent, string childName)
    {
        var child = parent.Find(childName) as RectTransform;
        if (child != null)
            return child;

        var go = new GameObject(childName, typeof(RectTransform));
        child = go.GetComponent<RectTransform>();
        child.SetParent(parent, false);
        child.localScale = Vector3.one;
        child.localRotation = Quaternion.identity;
        child.anchoredPosition3D = Vector3.zero;
        return child;
    }

    private static Image EnsureImageChild(Transform parent, string childName)
    {
        var rect = EnsureRectChild(parent, childName);
        return GetOrAddComponent<Image>(rect.gameObject);
    }

    private static TMP_Text EnsureBadgeLabel(RectTransform badgeRoot, TMP_Text template)
    {
        var existing = badgeRoot.Find("Label");
        if (existing != null)
        {
            var existingText = existing.GetComponent<TMP_Text>();
            if (existingText != null)
                return existingText;
        }

        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(badgeRoot, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 9f;
        text.fontStyle = FontStyles.Bold;
        text.text = "1";

        if (template != null)
        {
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
        }

        return text;
    }

    private static Transform GetRequiredChild(Transform root, string childPath)
    {
        var child = root.Find(childPath);
        if (child == null)
            throw new MissingReferenceException($"Required child '{childPath}' not found under {root.name}.");

        return child;
    }

    private static T GetRequiredChildComponent<T>(Transform root, string childPath) where T : Component
    {
        var child = GetRequiredChild(root, childPath);
        var component = child.GetComponent<T>();
        if (component == null)
            throw new MissingComponentException($"Required component {typeof(T).Name} missing on child '{childPath}'.");

        return component;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        if (component == null)
            component = target.AddComponent<T>();

        return component;
    }
}
#endif
