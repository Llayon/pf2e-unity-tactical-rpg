using UnityEngine;

namespace PF2e.Core
{
    public abstract class ItemDefinition : ScriptableObject
    {
        public string itemName;
        public int itemLevel = 0;
        public ItemRarity rarity = ItemRarity.Common;

        // Project convention:
        // priceCopper: 100 = 1 gp, 10 = 1 sp, 1 = 1 cp
        public int priceCopper = 0;

        // bulkTenth: 1 = Light (L), 10 = 1 Bulk
        public int bulkTenth = 0;
    }
}
