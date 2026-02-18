#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using PF2e.Core;

public static class CreateGoblinArmorOnly
{
    [MenuItem("Tools/PF2e/Create ONLY GoblinArmor (Fix)")]
    public static void CreateGoblinArmorOnly_Fix()
    {
        string path = "Assets/Data/Items/Armor/GoblinArmor.asset";

        // Check if already exists
        var existing = AssetDatabase.LoadAssetAtPath<ArmorDefinition>(path);
        if (existing != null)
        {
            EditorUtility.DisplayDialog("Already Exists",
                "GoblinArmor already exists at:\n" + path + "\n\nManually assign it to EntityManager.goblinArmorDef", "OK");
            EditorGUIUtility.PingObject(existing);
            return;
        }

        // Create new
        var armor = ScriptableObject.CreateInstance<ArmorDefinition>();
        armor.itemName = "Leather Armor";
        armor.itemLevel = 0;
        armor.rarity = ItemRarity.Common;
        armor.priceCopper = 200;
        armor.bulkTenth = 10;
        armor.category = ArmorCategory.Light;
        armor.group = ArmorGroup.Leather;
        armor.acBonus = 1;
        armor.dexCap = 4;
        armor.checkPenalty = -1;
        armor.speedPenaltyFeet = 0;
        armor.strengthReq = 10;

        AssetDatabase.CreateAsset(armor, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CreateGoblinArmor] Created: " + path);
        EditorUtility.DisplayDialog("GoblinArmor Created",
            "GoblinArmor (Leather) created successfully!\n\n" +
            "Location: " + path + "\n\n" +
            "Now assign it to EntityManager.goblinArmorDef in Inspector.", "OK");

        EditorGUIUtility.PingObject(armor);
    }
}
#endif
