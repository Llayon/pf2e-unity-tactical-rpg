#if UNITY_EDITOR
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.Presentation;
using PF2e.TurnSystem;

public static class ActionBarUIBuilder
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("Tools/PF2e/Build Action Bar")]
    public static void BuildActionBar()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ActionBarBuilder] No Canvas found in scene. Create/load SampleScene Canvas first.");
            return;
        }

        bool createdActionBar = false;
        var controller = Object.FindFirstObjectByType<ActionBarController>();
        GameObject root;
        if (controller == null)
        {
            root = CreateActionBarRoot(canvas.transform);
            controller = root.GetComponent<ActionBarController>();
            createdActionBar = true;

            // Create buttons and wire slots/highlights.
            CreateButtonSlot(root.transform, controller, "StrikeButton", "Strike", "LMB", "strikeButton", "strikeHighlight");
            CreateButtonSlot(root.transform, controller, "TripButton", "Trip", "T", "tripButton", "tripHighlight");
            CreateButtonSlot(root.transform, controller, "ShoveButton", "Shove", "H", "shoveButton", "shoveHighlight");
            CreateButtonSlot(root.transform, controller, "GrappleButton", "Grapple", "J", "grappleButton", "grappleHighlight");
            CreateButtonSlot(root.transform, controller, "DemoralizeButton", "Demoralize", "Y", "demoralizeButton", "demoralizeHighlight");
            CreateButtonSlot(root.transform, controller, "EscapeButton", "Escape", "K", "escapeButton", "escapeHighlight");
            CreateButtonSlot(root.transform, controller, "RaiseShieldButton", "Shield", "R", "raiseShieldButton", "raiseShieldHighlight");
            CreateButtonSlot(root.transform, controller, "StandButton", "Stand", "—", "standButton", "standHighlight");
        }
        else
        {
            root = controller.gameObject;
            Debug.Log("[ActionBarBuilder] Reusing existing ActionBarController and ensuring hint panel/wiring.", controller);
        }

        // Wire dependencies if found.
        AssignIfFieldExists(controller, "eventBus", Object.FindFirstObjectByType<CombatEventBus>());
        AssignIfFieldExists(controller, "entityManager", Object.FindFirstObjectByType<EntityManager>());
        AssignIfFieldExists(controller, "turnManager", Object.FindFirstObjectByType<TurnManager>());
        AssignIfFieldExists(controller, "actionExecutor", Object.FindFirstObjectByType<PlayerActionExecutor>());
        AssignIfFieldExists(controller, "targetingController", Object.FindFirstObjectByType<TargetingController>());
        AssignIfFieldExists(controller, "canvasGroup", root.GetComponent<CanvasGroup>());

        EnsureTargetingHintPanel(root.transform);

        Selection.activeObject = root;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(createdActionBar
            ? "[ActionBarBuilder] Built Action Bar UI hierarchy (including TargetingHintPanel) and wired references."
            : "[ActionBarBuilder] Refreshed ActionBar wiring and ensured TargetingHintPanel exists.",
            root);
    }

    private static GameObject CreateActionBarRoot(Transform canvasTransform)
    {
        var root = new GameObject(
            "ActionBar",
            typeof(RectTransform),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(HorizontalLayoutGroup),
            typeof(ContentSizeFitter),
            typeof(ActionBarController));

        Undo.RegisterCreatedObjectUndo(root, "Build Action Bar");
        root.transform.SetParent(canvasTransform, false);

        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 16f);
        rect.sizeDelta = new Vector2(0f, 0f);

        var bg = root.GetComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.20f, 0.90f);
        bg.raycastTarget = true;

        var cg = root.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var h = root.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 6f;
        h.padding = new RectOffset(8, 8, 8, 8);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = false;
        h.childControlHeight = false;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        var fitter = root.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return root;
    }

    private static void CreateButtonSlot(
        Transform parent,
        ActionBarController controller,
        string objectName,
        string label,
        string hotkey,
        string buttonFieldName,
        string highlightFieldName)
    {
        var buttonGo = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(buttonGo, $"Create {objectName}");
        buttonGo.transform.SetParent(parent, false);

        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(90f, 50f);

        var layout = buttonGo.GetComponent<LayoutElement>();
        layout.preferredWidth = 90f;
        layout.preferredHeight = 50f;
        layout.minHeight = 50f;

        var buttonImage = buttonGo.GetComponent<Image>();
        buttonImage.color = new Color(0.18f, 0.23f, 0.30f, 0.92f);

        var button = buttonGo.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        button.colors = colors;

        var labelGo = CreateTmpText(buttonGo.transform, "Label", label, 18f, Color.white);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.34f);
        labelRect.anchorMax = new Vector2(1f, 0.92f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var hotkeyGo = CreateTmpText(buttonGo.transform, "HotkeyHint", hotkey, 12f, new Color(0.6f, 0.6f, 0.6f, 1f));
        var hotkeyRect = hotkeyGo.GetComponent<RectTransform>();
        hotkeyRect.anchorMin = new Vector2(0f, 0.05f);
        hotkeyRect.anchorMax = new Vector2(1f, 0.35f);
        hotkeyRect.offsetMin = Vector2.zero;
        hotkeyRect.offsetMax = Vector2.zero;

        var highlightGo = new GameObject("ActiveHighlight", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(highlightGo, "Create ActiveHighlight");
        highlightGo.transform.SetParent(buttonGo.transform, false);
        var highlightRect = highlightGo.GetComponent<RectTransform>();
        highlightRect.anchorMin = Vector2.zero;
        highlightRect.anchorMax = Vector2.one;
        highlightRect.offsetMin = Vector2.zero;
        highlightRect.offsetMax = Vector2.zero;

        var highlightImage = highlightGo.GetComponent<Image>();
        highlightImage.color = new Color(1f, 0.85f, 0f, 0.25f);
        highlightImage.raycastTarget = false;
        highlightGo.SetActive(false);
        highlightGo.transform.SetAsFirstSibling();

        AssignIfFieldExists(controller, buttonFieldName, button);
        AssignIfFieldExists(controller, highlightFieldName, highlightImage);
    }

    private static void EnsureTargetingHintPanel(Transform actionBarRoot)
    {
        if (actionBarRoot == null) return;

        var panel = actionBarRoot.Find("TargetingHintPanel");
        if (panel == null)
        {
            var panelGo = new GameObject(
                "TargetingHintPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(LayoutElement),
                typeof(TargetingHintController));
            Undo.RegisterCreatedObjectUndo(panelGo, "Create TargetingHintPanel");
            panelGo.transform.SetParent(actionBarRoot, false);
            panel = panelGo.transform;

            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 6f);
            panelRect.sizeDelta = new Vector2(420f, 30f);

            var layout = panelGo.GetComponent<LayoutElement>();
            layout.ignoreLayout = true;

            var image = panelGo.GetComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
            image.raycastTarget = false;

            var cg = panelGo.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var hintText = CreateTmpText(panelGo.transform, "HintText", string.Empty, 14f, new Color(0.9f, 0.9f, 0.95f, 1f));
            var hintRect = hintText.GetComponent<RectTransform>();
            hintRect.anchorMin = Vector2.zero;
            hintRect.anchorMax = Vector2.one;
            hintRect.offsetMin = new Vector2(8f, 2f);
            hintRect.offsetMax = new Vector2(-8f, -2f);
        }

        var hintController = panel.GetComponent<TargetingHintController>();
        if (hintController == null)
            hintController = Undo.AddComponent<TargetingHintController>(panel.gameObject);

        AssignIfFieldExists(hintController, "eventBus", Object.FindFirstObjectByType<CombatEventBus>());
        AssignIfFieldExists(hintController, "turnManager", Object.FindFirstObjectByType<TurnManager>());
        AssignIfFieldExists(hintController, "gridManager", Object.FindFirstObjectByType<GridManager>());
        AssignIfFieldExists(hintController, "targetingController", Object.FindFirstObjectByType<TargetingController>());
        AssignIfFieldExists(hintController, "canvasGroup", panel.GetComponent<CanvasGroup>());
        AssignIfFieldExists(hintController, "backgroundImage", panel.GetComponent<Image>());

        var hintTextTransform = panel.Find("HintText");
        if (hintTextTransform != null)
        {
            var tmp = hintTextTransform.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                AssignIfFieldExists(hintController, "hintText", tmp);
        }
    }

    private static GameObject CreateTmpText(Transform parent, string name, string text, float fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        return go;
    }

    private static void AssignIfFieldExists(Component target, string fieldName, Object value)
    {
        if (target == null || value == null) return;
        var field = target.GetType().GetField(fieldName, Flags);
        if (field == null) return;
        if (!field.FieldType.IsAssignableFrom(value.GetType())) return;

        Undo.RecordObject(target, $"Assign {target.GetType().Name}.{fieldName}");
        field.SetValue(target, value);
        EditorUtility.SetDirty(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
#endif
