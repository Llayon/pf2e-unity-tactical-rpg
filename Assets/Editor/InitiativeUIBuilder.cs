#if UNITY_EDITOR
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PF2e.Presentation;

public static class InitiativeUIBuilder
{
    private const string InitiativeSlotPrefabPath = "Assets/Prefabs/InitiativeSlot.prefab";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("Tools/PF2e/Build Initiative Delay UI (Badge + Prompt)")]
    public static void BuildInitiativeDelayUi()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[InitiativeUIBuilder] Exit Play Mode before running builder.");
            return;
        }

        int changes = 0;
        changes += EnsureInitiativeSlotDelayedBadgePrefab();
        changes += EnsureSceneInitiativeDelayPromptUi();

        if (changes > 0)
        {
            Debug.Log($"[InitiativeUIBuilder] Done. Applied changes: {changes}.");
        }
        else
        {
            Debug.Log("[InitiativeUIBuilder] Done. Nothing to change.");
        }
    }

    private static int EnsureInitiativeSlotDelayedBadgePrefab()
    {
        int changes = 0;
        var prefabRoot = PrefabUtility.LoadPrefabContents(InitiativeSlotPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"[InitiativeUIBuilder] Failed to load prefab: {InitiativeSlotPrefabPath}");
            return 0;
        }

        try
        {
            var slot = prefabRoot.GetComponent<InitiativeSlot>();
            if (slot == null)
            {
                Debug.LogError($"[InitiativeUIBuilder] InitiativeSlot component missing in prefab: {InitiativeSlotPrefabPath}");
                return 0;
            }

            var rootRect = prefabRoot.transform as RectTransform;
            if (rootRect == null)
            {
                Debug.LogError("[InitiativeUIBuilder] InitiativeSlot prefab root is not a RectTransform.");
                return 0;
            }

            // Remove stale MCP-created stub if present (plain Transform child).
            var stub = rootRect.Find("DelayedBadge_Stub");
            if (stub != null)
            {
                Object.DestroyImmediate(stub.gameObject);
                changes++;
            }

            var badgeRect = rootRect.Find("DelayedBadge") as RectTransform;
            if (badgeRect == null)
            {
                var badgeGo = new GameObject(
                    "DelayedBadge",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                badgeRect = badgeGo.GetComponent<RectTransform>();
                badgeRect.SetParent(rootRect, false);
                badgeRect.anchorMin = new Vector2(1f, 1f);
                badgeRect.anchorMax = new Vector2(1f, 1f);
                badgeRect.pivot = new Vector2(1f, 1f);
                badgeRect.anchoredPosition = new Vector2(-2f, -2f);
                badgeRect.sizeDelta = new Vector2(24f, 14f);
                changes++;
            }

            var badgeImage = badgeRect.GetComponent<Image>();
            if (badgeImage == null)
            {
                badgeImage = badgeRect.gameObject.AddComponent<Image>();
                changes++;
            }

            badgeImage.raycastTarget = false;
            badgeImage.color = new Color(0.95f, 0.85f, 0.25f, 0.95f);

            if (badgeRect.GetComponent<Outline>() == null)
            {
                var outline = badgeRect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
                outline.effectDistance = new Vector2(1f, -1f);
                changes++;
            }

            var labelRect = badgeRect.Find("Label") as RectTransform;
            if (labelRect == null)
            {
                var labelGo = new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                labelRect = labelGo.GetComponent<RectTransform>();
                labelRect.SetParent(badgeRect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                changes++;
            }

            var label = labelRect.GetComponent<TextMeshProUGUI>();
            if (label == null)
            {
                label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
                changes++;
            }

            ConfigureBadgeLabelFromNameText(rootRect, label);

            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.fontSize = 9f;
            label.color = new Color(0.1f, 0.08f, 0.03f, 1f);
            label.SetText("DLY");

            if (label.GetComponent<Shadow>() == null)
            {
                var shadow = label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(1f, 1f, 1f, 0.2f);
                shadow.effectDistance = new Vector2(0f, 0f);
                changes++;
            }

            badgeRect.gameObject.SetActive(false);

            changes += AssignFieldIfDifferent(slot, "delayedBadgeRoot", badgeRect.gameObject);
            changes += AssignFieldIfDifferent(slot, "delayedBadgeBackground", badgeImage);
            changes += AssignFieldIfDifferent(slot, "delayedBadgeText", label);
            changes += AssignBoolFieldIfDifferent(slot, "appendDelayedNameSuffixFallback", false);

            if (changes > 0)
            {
                EditorUtility.SetDirty(slot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, InitiativeSlotPrefabPath);
            }

            return changes;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureBadgeLabelFromNameText(RectTransform slotRoot, TextMeshProUGUI badgeLabel)
    {
        if (slotRoot == null || badgeLabel == null)
            return;

        var template = slotRoot.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (template == null)
            return;

        if (template.font != null)
            badgeLabel.font = template.font;
        if (template.fontSharedMaterial != null)
            badgeLabel.fontSharedMaterial = template.fontSharedMaterial;

        badgeLabel.characterSpacing = template.characterSpacing;
        badgeLabel.wordSpacing = template.wordSpacing;
        badgeLabel.lineSpacing = template.lineSpacing;
        badgeLabel.paragraphSpacing = template.paragraphSpacing;
        badgeLabel.enableKerning = template.enableKerning;
        badgeLabel.extraPadding = template.extraPadding;
    }

    private static int EnsureSceneInitiativeDelayPromptUi()
    {
        int changes = 0;

        var bar = Object.FindFirstObjectByType<InitiativeBarController>();
        if (bar == null)
        {
            Debug.LogWarning("[InitiativeUIBuilder] InitiativeBarController not found in active scene.");
            return 0;
        }

        var panelRoot = GetFieldValue<GameObject>(bar, "panelRoot");
        if (panelRoot == null)
        {
            var fallback = GameObject.Find("Canvas/InitiativeBarPanel");
            if (fallback != null)
            {
                panelRoot = fallback;
                changes += AssignFieldIfDifferent(bar, "panelRoot", panelRoot);
            }
        }

        if (panelRoot == null)
        {
            Debug.LogWarning("[InitiativeUIBuilder] InitiativeBarPanel not found.");
            return changes;
        }

        var panelRect = panelRoot.transform as RectTransform;
        if (panelRect == null)
        {
            Debug.LogWarning("[InitiativeUIBuilder] InitiativeBarPanel is not a RectTransform.");
            return changes;
        }

        var roundLabel = GetFieldValue<TextMeshProUGUI>(bar, "roundLabel");
        if (roundLabel == null)
        {
            roundLabel = panelRoot.transform.Find("RoundLabel")?.GetComponent<TextMeshProUGUI>();
            if (roundLabel != null)
                changes += AssignFieldIfDifferent(bar, "roundLabel", roundLabel);
        }

        var slotsRect = GetFieldValue<Transform>(bar, "slotsContainer") as RectTransform;
        if (slotsRect == null)
        {
            slotsRect = panelRoot.transform.Find("SlotsContainer") as RectTransform;
            if (slotsRect != null)
                changes += AssignFieldIfDifferent(bar, "slotsContainer", slotsRect);
        }

        if (slotsRect != null)
        {
            var overlayRect = panelRoot.transform.Find("DelayMarkersOverlay") as RectTransform;
            if (overlayRect == null)
            {
                var overlayGo = new GameObject("DelayMarkersOverlay", typeof(RectTransform));
                overlayRect = overlayGo.GetComponent<RectTransform>();
                overlayRect.SetParent(panelRect, false);
                changes++;
            }

            CopyRectTransformLayout(slotsRect, overlayRect);
            overlayRect.SetSiblingIndex(slotsRect.GetSiblingIndex() + 1);
            changes += AssignFieldIfDifferent(bar, "markersOverlayContainer", overlayRect);
        }

        var bannerRect = panelRoot.transform.Find("DelayPlacementPromptBanner") as RectTransform;
        if (bannerRect == null)
        {
            var bannerGo = new GameObject(
                "DelayPlacementPromptBanner",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            bannerRect = bannerGo.GetComponent<RectTransform>();
            bannerRect.SetParent(panelRect, false);
            changes++;
        }

        bannerRect.anchorMin = new Vector2(0.5f, 1f);
        bannerRect.anchorMax = new Vector2(0.5f, 1f);
        bannerRect.pivot = new Vector2(0.5f, 1f);
        bannerRect.anchoredPosition = new Vector2(0f, -4f);
        bannerRect.sizeDelta = new Vector2(460f, 24f);
        bannerRect.SetAsLastSibling();

        var bannerImage = bannerRect.GetComponent<Image>();
        if (bannerImage == null)
        {
            bannerImage = bannerRect.gameObject.AddComponent<Image>();
            changes++;
        }
        bannerImage.raycastTarget = false;
        bannerImage.color = new Color(0.07f, 0.07f, 0.1f, 0.92f);

        var bannerOutline = bannerRect.GetComponent<Outline>();
        if (bannerOutline == null)
        {
            bannerOutline = bannerRect.gameObject.AddComponent<Outline>();
            changes++;
        }
        bannerOutline.effectColor = new Color(1f, 0.9f, 0.45f, 0.45f);
        bannerOutline.effectDistance = new Vector2(1f, -1f);

        var labelRect = bannerRect.Find("DelayPlacementPromptLabel") as RectTransform;
        if (labelRect == null)
        {
            var labelGo = new GameObject(
                "DelayPlacementPromptLabel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.SetParent(bannerRect, false);
            changes++;
        }

        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 2f);
        labelRect.offsetMax = new Vector2(-10f, -2f);

        var label = labelRect.GetComponent<TextMeshProUGUI>();
        if (label == null)
        {
            label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            changes++;
        }

        ConfigurePromptLabelFromRoundLabel(roundLabel, label);
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.98f, 0.95f, 0.82f, 1f);
        label.SetText("Choose delay position (between portraits)");

        var labelShadow = label.GetComponent<Shadow>();
        if (labelShadow == null)
        {
            labelShadow = label.gameObject.AddComponent<Shadow>();
            changes++;
        }
        labelShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        labelShadow.effectDistance = new Vector2(1f, -1f);

        bannerRect.gameObject.SetActive(false);

        changes += AssignFieldIfDifferent(bar, "delayPlacementPromptRoot", bannerRect.gameObject);
        changes += AssignFieldIfDifferent(bar, "delayPlacementPromptBackground", bannerImage);
        changes += AssignFieldIfDifferent(bar, "delayPlacementPromptLabel", label);

        if (changes > 0)
        {
            EditorUtility.SetDirty(bar);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        return changes;
    }

    private static void CopyRectTransformLayout(RectTransform source, RectTransform target)
    {
        if (source == null || target == null)
            return;

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localScale = Vector3.one;
        target.localRotation = Quaternion.identity;
    }

    private static void ConfigurePromptLabelFromRoundLabel(TextMeshProUGUI roundLabel, TextMeshProUGUI promptLabel)
    {
        if (roundLabel == null || promptLabel == null)
            return;

        if (roundLabel.font != null)
            promptLabel.font = roundLabel.font;
        if (roundLabel.fontSharedMaterial != null)
            promptLabel.fontSharedMaterial = roundLabel.fontSharedMaterial;

        promptLabel.fontSize = roundLabel.fontSize;
        promptLabel.characterSpacing = roundLabel.characterSpacing;
        promptLabel.wordSpacing = roundLabel.wordSpacing;
        promptLabel.lineSpacing = roundLabel.lineSpacing;
        promptLabel.paragraphSpacing = roundLabel.paragraphSpacing;
        promptLabel.enableKerning = roundLabel.enableKerning;
        promptLabel.extraPadding = roundLabel.extraPadding;
    }

    private static T GetFieldValue<T>(Component target, string fieldName) where T : UnityEngine.Object
    {
        if (target == null)
            return null;

        var field = target.GetType().GetField(fieldName, Flags);
        if (field == null)
            return null;

        return field.GetValue(target) as T;
    }

    private static int AssignFieldIfDifferent(Component target, string fieldName, UnityEngine.Object value)
    {
        if (target == null || value == null)
            return 0;

        var field = target.GetType().GetField(fieldName, Flags);
        if (field == null || !field.FieldType.IsAssignableFrom(value.GetType()))
            return 0;

        var current = field.GetValue(target) as UnityEngine.Object;
        if (current == value)
            return 0;

        Undo.RecordObject(target, $"Wire {target.GetType().Name}.{fieldName}");
        field.SetValue(target, value);
        EditorUtility.SetDirty(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        return 1;
    }

    private static int AssignBoolFieldIfDifferent(Component target, string fieldName, bool value)
    {
        if (target == null)
            return 0;

        var field = target.GetType().GetField(fieldName, Flags);
        if (field == null || field.FieldType != typeof(bool))
            return 0;

        bool current = (bool)field.GetValue(target);
        if (current == value)
            return 0;

        Undo.RecordObject(target, $"Wire {target.GetType().Name}.{fieldName}");
        field.SetValue(target, value);
        EditorUtility.SetDirty(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        return 1;
    }
}
#endif
