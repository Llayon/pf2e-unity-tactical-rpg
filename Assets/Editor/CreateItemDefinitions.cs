#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using PF2e.Core;
using System.IO;

public static class CreateItemDefinitions
{
    [MenuItem("Tools/PF2e/Create Item Definitions")]
    public static void CreateAllDefinitions()
    {
        // Ensure directories exist
        string weaponPath = "Assets/Data/Items/Weapons";
        string armorPath = "Assets/Data/Items/Armor";

        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder("Assets/Data/Items"))
            AssetDatabase.CreateFolder("Assets/Data", "Items");
        if (!AssetDatabase.IsValidFolder(weaponPath))
            AssetDatabase.CreateFolder("Assets/Data/Items", "Weapons");
        if (!AssetDatabase.IsValidFolder(armorPath))
            AssetDatabase.CreateFolder("Assets/Data/Items", "Armor");

        // Create Weapons
        CreateFighterWeapon(weaponPath);
        CreateWizardWeapon(weaponPath);
        CreateGoblinWeapon(weaponPath);

        // Create Armor
        CreateFighterArmor(armorPath);
        CreateWizardArmor(armorPath);
        CreateGoblinArmor(armorPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Item Definitions Created",
            "All weapon and armor definitions have been created!\n\n" +
            "Location: Assets/Data/Items/\n\n" +
            "Next: Assign them to EntityManager in the Inspector.", "OK");
    }

    private static void CreateFighterWeapon(string path)
    {
        var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        weapon.itemName = "Longsword";
        weapon.itemLevel = 0;
        weapon.rarity = ItemRarity.Common;
        weapon.priceCopper = 1000;
        weapon.bulkTenth = 10;
        weapon.category = WeaponCategory.Martial;
        weapon.group = WeaponGroup.Sword;
        weapon.hands = WeaponHands.One;
        weapon.diceCount = 1;
        weapon.dieSides = 8;
        weapon.damageType = DamageType.Slashing;
        weapon.reachFeet = 5;
        weapon.isRanged = false;
        weapon.traits = WeaponTraitFlags.None;
        weapon.usesAmmo = false;

        AssetDatabase.CreateAsset(weapon, $"{path}/FighterWeapon.asset");
        Debug.Log("[ItemDefinitions] Created FighterWeapon (Longsword)");
    }

    private static void CreateWizardWeapon(string path)
    {
        var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        weapon.itemName = "Dagger";
        weapon.itemLevel = 0;
        weapon.rarity = ItemRarity.Common;
        weapon.priceCopper = 20;
        weapon.bulkTenth = 1;
        weapon.category = WeaponCategory.Simple;
        weapon.group = WeaponGroup.Knife;
        weapon.hands = WeaponHands.One;
        weapon.diceCount = 1;
        weapon.dieSides = 4;
        weapon.damageType = DamageType.Piercing;
        weapon.reachFeet = 5;
        weapon.isRanged = false;
        weapon.traits = WeaponTraitFlags.Agile | WeaponTraitFlags.Finesse;
        weapon.usesAmmo = false;

        AssetDatabase.CreateAsset(weapon, $"{path}/WizardWeapon.asset");
        Debug.Log("[ItemDefinitions] Created WizardWeapon (Dagger)");
    }

    private static void CreateGoblinWeapon(string path)
    {
        var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        weapon.itemName = "Dogslicer";
        weapon.itemLevel = 0;
        weapon.rarity = ItemRarity.Uncommon;
        weapon.priceCopper = 10;
        weapon.bulkTenth = 5;
        weapon.category = WeaponCategory.Martial;
        weapon.group = WeaponGroup.Sword;
        weapon.hands = WeaponHands.One;
        weapon.diceCount = 1;
        weapon.dieSides = 6;
        weapon.damageType = DamageType.Slashing;
        weapon.reachFeet = 5;
        weapon.isRanged = false;
        weapon.traits = WeaponTraitFlags.Agile;
        weapon.usesAmmo = false;

        AssetDatabase.CreateAsset(weapon, $"{path}/GoblinWeapon.asset");
        Debug.Log("[ItemDefinitions] Created GoblinWeapon (Dogslicer)");
    }

    private static void CreateFighterArmor(string path)
    {
        var armor = ScriptableObject.CreateInstance<ArmorDefinition>();
        armor.itemName = "Breastplate";
        armor.itemLevel = 0;
        armor.rarity = ItemRarity.Common;
        armor.priceCopper = 800;
        armor.bulkTenth = 20;
        armor.category = ArmorCategory.Medium;
        armor.group = ArmorGroup.Plate;
        armor.acBonus = 4;
        armor.dexCap = 1;
        armor.checkPenalty = -2;
        armor.speedPenaltyFeet = -5;
        armor.strengthReq = 14;

        AssetDatabase.CreateAsset(armor, $"{path}/FighterArmor.asset");
        Debug.Log("[ItemDefinitions] Created FighterArmor (Breastplate)");
    }

    private static void CreateWizardArmor(string path)
    {
        var armor = ScriptableObject.CreateInstance<ArmorDefinition>();
        armor.itemName = "Unarmored";
        armor.itemLevel = 0;
        armor.rarity = ItemRarity.Common;
        armor.priceCopper = 0;
        armor.bulkTenth = 0;
        armor.category = ArmorCategory.Unarmored;
        armor.group = ArmorGroup.Cloth;
        armor.acBonus = 0;
        armor.dexCap = 99;
        armor.checkPenalty = 0;
        armor.speedPenaltyFeet = 0;
        armor.strengthReq = 0;

        AssetDatabase.CreateAsset(armor, $"{path}/WizardArmor.asset");
        Debug.Log("[ItemDefinitions] Created WizardArmor (Unarmored)");
    }

    private static void CreateGoblinArmor(string path)
    {
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

        AssetDatabase.CreateAsset(armor, $"{path}/GoblinArmor.asset");
        Debug.Log("[ItemDefinitions] Created GoblinArmor (Leather)");
    }
}
#endif
