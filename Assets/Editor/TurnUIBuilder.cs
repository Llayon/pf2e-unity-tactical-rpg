#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using PF2e.Presentation;
using PF2e.TurnSystem;
using PF2e.Managers;

/// <summary>
/// One-time builder for Turn UI. Run via Tools > PF2e > Build Turn UI.
/// </summary>
public static class TurnUIBuilder
{
    [MenuItem("Tools/PF2e/Fix EventSystem (New Input System)")]
    public static void FixEventSystem()
    {
        var eventSystem = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            Debug.LogWarning("[TurnUIBuilder] No EventSystem found in scene.");
            return;
        }

        var oldModule = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (oldModule != null)
        {
            Undo.DestroyObjectImmediate(oldModule);
            Undo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(eventSystem.gameObject);
            Debug.Log("[TurnUIBuilder] Replaced StandaloneInputModule with InputSystemUIInputModule");
            EditorUtility.DisplayDialog("EventSystem Fixed",
                "StandaloneInputModule replaced with InputSystemUIInputModule.\n\nThe Input System errors should be gone now.", "OK");
        }
        else
        {
            var newModule = eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (newModule != null)
            {
                Debug.Log("[TurnUIBuilder] EventSystem already uses InputSystemUIInputModule.");
                EditorUtility.DisplayDialog("EventSystem OK",
                    "EventSystem already uses InputSystemUIInputModule.", "OK");
            }
            else
            {
                Undo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(eventSystem.gameObject);
                Debug.Log("[TurnUIBuilder] Added InputSystemUIInputModule to EventSystem");
                EditorUtility.DisplayDialog("EventSystem Fixed",
                    "InputSystemUIInputModule added to EventSystem.", "OK");
            }
        }
    }

    [MenuItem("Tools/PF2e/Build Turn UI")]
    public static void BuildTurnUI()
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
            Debug.Log("[TurnUIBuilder] Created Canvas");
        }

        // 2. Find or create EventSystem with New Input System support
        var eventSystem = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            // Use InputSystemUIInputModule for New Input System
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
            Debug.Log("[TurnUIBuilder] Created EventSystem with InputSystemUIInputModule");
        }
        else
        {
            // Fix existing EventSystem if needed
            var oldModule = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (oldModule != null)
            {
                Debug.LogWarning("[TurnUIBuilder] Removing old StandaloneInputModule and adding InputSystemUIInputModule");
                Undo.DestroyObjectImmediate(oldModule);
                Undo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(eventSystem.gameObject);
            }
        }

        // 3. Check if TurnHUD already exists
        var existing = canvas.transform.Find("TurnHUD");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Turn UI Builder",
                "TurnHUD already exists. Replace it?", "Yes", "Cancel"))
                return;

            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        // 4. Create TurnHUD panel
        var turnHUD = new GameObject("TurnHUD");
        Undo.RegisterCreatedObjectUndo(turnHUD, "Create TurnHUD");
        turnHUD.transform.SetParent(canvas.transform, false);

        var rect = turnHUD.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(20, -20);
        rect.sizeDelta = new Vector2(360, 0);

        var image = turnHUD.AddComponent<Image>();
        image.color = new Color(0, 0, 0, 0.55f);

        var canvasGroup = turnHUD.AddComponent<CanvasGroup>();

        var vlg = turnHUD.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.spacing = 8;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = turnHUD.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 5. Create text children
        var roundText = CreateTMPText("RoundText", turnHUD.transform, "Round: -", 22);
        var actorText = CreateTMPText("ActorText", turnHUD.transform, "Actor: -", 22);
        var actionsText = CreateTMPText("ActionsText", turnHUD.transform, "Actions: ---", 24);

        // 6. Create button
        var buttonGO = new GameObject("EndTurnButton");
        Undo.RegisterCreatedObjectUndo(buttonGO, "Create EndTurnButton");
        buttonGO.transform.SetParent(turnHUD.transform, false);

        var buttonRect = buttonGO.AddComponent<RectTransform>();
        var buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = Color.white;

        var button = buttonGO.AddComponent<Button>();

        var le = buttonGO.AddComponent<LayoutElement>();
        le.preferredHeight = 44;

        // Button text
        var btnTextGO = new GameObject("Text");
        btnTextGO.transform.SetParent(buttonGO.transform, false);
        var btnTextRect = btnTextGO.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        var btnText = btnTextGO.AddComponent<TextMeshProUGUI>();
        btnText.text = "End Turn";
        btnText.fontSize = 18;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.black;

        // 7. Find singletons first
        var turnManager = Object.FindAnyObjectByType<TurnManager>();
        var entityManager = Object.FindAnyObjectByType<EntityManager>();
        var turnInputController = Object.FindAnyObjectByType<TurnInputController>();

        // Add component
        var controller = turnHUD.AddComponent<TurnUIController>();

        // Wire via SerializedObject immediately after adding component
        var so = new SerializedObject(controller);
        so.FindProperty("turnManager").objectReferenceValue = turnManager;
        so.FindProperty("entityManager").objectReferenceValue = entityManager;
        so.FindProperty("turnInputController").objectReferenceValue = turnInputController;
        so.FindProperty("roundText").objectReferenceValue = roundText;
        so.FindProperty("actorText").objectReferenceValue = actorText;
        so.FindProperty("actionsText").objectReferenceValue = actionsText;
        so.FindProperty("endTurnButton").objectReferenceValue = button;
        so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("hideWhenNotInCombat").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(turnHUD);

        Debug.Log("[TurnUIBuilder] Turn UI created successfully!");
        EditorUtility.DisplayDialog("Turn UI Builder",
            "Turn UI created successfully!\n\nTurnHUD added to Canvas with all components wired.", "OK");

        Selection.activeGameObject = turnHUD;
    }

    private static TextMeshProUGUI CreateTMPText(string name, Transform parent, string text, float fontSize)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;

        return tmp;
    }
}
#endif
