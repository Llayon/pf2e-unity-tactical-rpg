using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ApplyPanelSkin
{
    [MenuItem("Tools/Apply Panel Skins")]
    public static void Apply()
    {
        var panelLarge = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Panel Large.png");
        var panelMedium = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Panel Medium.png");
        var panelSmall = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Panel Small.png");
        var actionBar = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/ActionBarPanel.png");

        if (panelLarge == null) Debug.LogError("[ApplyPanelSkin] Panel Large.png not found or not Sprite.");
        if (panelMedium == null) Debug.LogError("[ApplyPanelSkin] Panel Medium.png not found or not Sprite.");
        if (panelSmall == null) Debug.LogError("[ApplyPanelSkin] Panel Small.png not found or not Sprite.");
        if (actionBar == null) Debug.LogError("[ApplyPanelSkin] ActionBarPanel.png not found or not Sprite.");

        int applied = 0;

        // Panel Large: CombatLogHUD (+ future: Unit Panel, Cast picker)
        applied += ApplyTo("CombatLogHUD", panelLarge);

        // Panel Medium: TurnHUD, EncounterEndPanel, ReactionPromptPanel
        applied += ApplyTo("TurnHUD", panelMedium);
        applied += ApplyTo("EncounterEndPanel", panelMedium);
        applied += ApplyTo("ReactionPromptPanel", panelMedium);

        // Panel Small: EncounterFlowPanel, InitiativeBarPanel (+ future: Target Panel, tooltips)
        applied += ApplyTo("EncounterFlowPanel", panelSmall);
        applied += ApplyTo("InitiativeBarPanel", panelSmall);

        // ActionBarPanel: ActionBar
        applied += ApplyTo("ActionBar", actionBar);

        Debug.Log($"[ApplyPanelSkin] Done. Applied to {applied} panels.");
    }

    private static int ApplyTo(string panelName, Sprite sprite)
    {
        if (sprite == null) return 0;

        var go = FindInCanvas(panelName);
        if (go == null)
        {
            Debug.LogWarning($"[ApplyPanelSkin] '{panelName}' not found.");
            return 0;
        }

        var img = go.GetComponent<Image>();
        if (img == null)
        {
            Debug.LogWarning($"[ApplyPanelSkin] '{panelName}' has no Image component.");
            return 0;
        }

        Undo.RecordObject(img, "Apply Panel Skin");
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        EditorUtility.SetDirty(img);
        Debug.Log($"[ApplyPanelSkin] {panelName} <- {sprite.name}");
        return 1;
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
