#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;
using PF2e.Presentation;

/// <summary>
/// One-time builder for Combat Log UI. Run via Tools > PF2e > Build Combat Log UI.
/// </summary>
public static class CombatLogUIBuilder
{
    [MenuItem("Tools/PF2e/Build Combat Log UI")]
    public static void BuildCombatLogUI()
    {
        // 1. Find or create Canvas
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
            Debug.Log("[CombatLogUIBuilder] Created Canvas");
        }

        // 2. Check if CombatLogHUD already exists
        var existing = canvas.transform.Find("CombatLogHUD");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Combat Log UI Builder",
                "CombatLogHUD already exists. Replace it?", "Yes", "Cancel"))
                return;

            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        // 3. Create CombatLogHUD panel
        var logHUD = new GameObject("CombatLogHUD");
        Undo.RegisterCreatedObjectUndo(logHUD, "Create CombatLogHUD");
        logHUD.transform.SetParent(canvas.transform, false);

        var hudRect = logHUD.AddComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0, 0);
        hudRect.anchorMax = new Vector2(0, 0);
        hudRect.pivot = new Vector2(0, 0);
        hudRect.anchoredPosition = new Vector2(20, 20);
        hudRect.sizeDelta = new Vector2(520, 260);

        var hudImage = logHUD.AddComponent<Image>();
        hudImage.color = new Color(0, 0, 0, 0.55f);

        var canvasGroup = logHUD.AddComponent<CanvasGroup>();

        // 4. Create ScrollView
        var scrollViewGO = new GameObject("CombatLogScroll");
        Undo.RegisterCreatedObjectUndo(scrollViewGO, "Create ScrollView");
        scrollViewGO.transform.SetParent(logHUD.transform, false);

        var scrollRect = scrollViewGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        var svRect = scrollViewGO.GetComponent<RectTransform>();
        svRect.anchorMin = Vector2.zero;
        svRect.anchorMax = Vector2.one;
        svRect.sizeDelta = Vector2.zero;
        svRect.anchoredPosition = Vector2.zero;

        // 5. Create Viewport
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollViewGO.transform, false);

        var viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;

        var viewportMask = viewportGO.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        var viewportImage = viewportGO.AddComponent<Image>();
        viewportImage.color = Color.white;

        scrollRect.viewport = viewportRect;

        // 6. Create Content
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);

        var contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.spacing = 4;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRect;

        // 7. Create LineTemplate
        var lineTemplateGO = new GameObject("LineTemplate");
        lineTemplateGO.transform.SetParent(contentGO.transform, false);

        var lineText = lineTemplateGO.AddComponent<TextMeshProUGUI>();
        lineText.text = "(template)";
        lineText.fontSize = 18;
        lineText.alignment = TextAlignmentOptions.Left;
        lineText.color = Color.white;

        var lineLE = lineTemplateGO.AddComponent<LayoutElement>();
        lineLE.minHeight = 26f;
        lineLE.preferredHeight = -1f; // let TMP preferred height drive wrapping lines

        lineTemplateGO.SetActive(false); // CRITICAL: template must be inactive

        // 8. Find singletons
        var turnManager = Object.FindAnyObjectByType<TurnManager>();
        var entityManager = Object.FindAnyObjectByType<EntityManager>();
        var eventBus = Object.FindAnyObjectByType<CombatEventBus>();

        // Add component
        var controller = logHUD.AddComponent<CombatLogController>();

        // Wire via SerializedObject
        var so = new SerializedObject(controller);
        so.FindProperty("turnManager").objectReferenceValue = turnManager;
        so.FindProperty("entityManager").objectReferenceValue = entityManager;
        so.FindProperty("eventBus").objectReferenceValue = eventBus;
        so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
        so.FindProperty("content").objectReferenceValue = contentRect;
        so.FindProperty("lineTemplate").objectReferenceValue = lineText;
        so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("hideWhenNotInCombat").boolValue = true;
        so.FindProperty("autoScrollToBottom").boolValue = true;
        so.FindProperty("maxLines").intValue = 80;
        so.FindProperty("showLineNumbers").boolValue = true;
        so.FindProperty("showTimestamps").boolValue = true;
        so.FindProperty("showRoundPrefix").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(logHUD);

        Debug.Log("[CombatLogUIBuilder] Combat Log UI created successfully!");

        string warnings = "";
        if (turnManager == null) warnings += "- TurnManager not found\n";
        if (entityManager == null) warnings += "- EntityManager not found\n";
        if (eventBus == null) warnings += "- CombatEventBus not found\n";

        if (string.IsNullOrEmpty(warnings))
        {
            EditorUtility.DisplayDialog("Combat Log UI Builder",
                "Combat Log UI created successfully!\n\nCombatLogHUD added to Canvas with all components wired.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Combat Log UI Builder",
                $"Combat Log UI created, but some dependencies are missing:\n\n{warnings}\nWire them manually in Inspector or create missing GameObjects.", "OK");
        }

        Selection.activeGameObject = logHUD;
    }
}
#endif
