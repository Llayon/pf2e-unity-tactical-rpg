using UnityEngine;

namespace PF2e.Core
{
    [CreateAssetMenu(fileName = "ArmorDef", menuName = "PF2e/Items/Armor Definition")]
    public class ArmorDefinition : ItemDefinition
    {
        public ArmorCategory category = ArmorCategory.Unarmored;
        public ArmorGroup group = ArmorGroup.Cloth;

        public int acBonus = 0;          // armor item bonus without potency
        public int dexCap = 99;          // unarmored default
        public int checkPenalty = 0;
        public int speedPenaltyFeet = 0;
        public int strengthReq = 0;
    }
}
