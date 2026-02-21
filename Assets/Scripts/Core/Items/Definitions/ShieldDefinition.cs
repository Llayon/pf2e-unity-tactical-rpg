using UnityEngine;

namespace PF2e.Core
{
    [CreateAssetMenu(fileName = "ShieldDef", menuName = "PF2e/Items/Shield Definition")]
    public class ShieldDefinition : ItemDefinition
    {
        [Min(0)] public int acBonus = 2;
        [Min(0)] public int hardness = 3;
        [Min(0)] public int maxHP = 20;
    }
}
