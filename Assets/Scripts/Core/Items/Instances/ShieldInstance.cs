using UnityEngine;

namespace PF2e.Core
{
    [System.Serializable]
    public struct ShieldInstance
    {
        public ShieldDefinition def;

        [Header("State")]
        public int potencyBonus; // future-proof field
        public int currentHP;
        public bool isRaised;

        public int Potency => Mathf.Clamp(potencyBonus, 0, 3);
        public int ACBonus => isRaised && def != null ? def.acBonus : 0;
        public int Hardness => def != null ? Mathf.Max(0, def.hardness) : 0;
        public int MaxHP => def != null ? Mathf.Max(0, def.maxHP) : 0;
        public bool IsBroken => def != null && currentHP <= 0;
        public bool IsEquipped => def != null;

        public static ShieldInstance CreateEquipped(ShieldDefinition definition)
        {
            if (definition == null) return default;

            return new ShieldInstance
            {
                def = definition,
                potencyBonus = 0,
                currentHP = Mathf.Max(0, definition.maxHP),
                isRaised = false
            };
        }
    }
}
