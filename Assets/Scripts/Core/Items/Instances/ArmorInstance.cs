using UnityEngine;

namespace PF2e.Core
{
    [System.Serializable]
    public struct ArmorInstance
    {
        public ArmorDefinition def;

        [Header("Fundamental runes")]
        public int potencyBonus;            // 0..3 item bonus to AC
        public ResilientRuneRank resilient; // future saves

        [Header("State (future)")]
        public bool broken;

        public int Potency => Mathf.Clamp(potencyBonus, 0, 3);

        public ArmorCategory Category => def != null ? def.category : ArmorCategory.Unarmored;
        public int DexCap => def != null ? def.dexCap : 99;

        // PF2e: armor item bonus to AC = acBonus + potency
        public int ItemACBonus => (def != null ? def.acBonus : 0) + Potency;

        public string Name => def != null ? def.itemName : "Armor";
    }
}
