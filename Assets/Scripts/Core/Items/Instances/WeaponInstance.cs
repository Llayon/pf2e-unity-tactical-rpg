using UnityEngine;

namespace PF2e.Core
{
    [System.Serializable]
    public struct WeaponInstance
    {
        public WeaponDefinition def;

        [Header("Fundamental runes")]
        public int potencyBonus;              // 0..3 item bonus to attack
        public StrikingRuneRank strikingRank; // x1..x4 dice

        public int Potency => Mathf.Clamp(potencyBonus, 0, 3);

        public int EffectiveDiceCount =>
            (def != null ? Mathf.Max(0, def.diceCount) : 0) * strikingRank.DamageDiceMultiplier();

        public int DieSides => def != null ? def.dieSides : 0;
        public DamageType DamageType => def != null ? def.damageType : DamageType.Slashing;

        public int ReachFeet => def != null ? Mathf.Max(5, def.reachFeet) : 5;
        public bool IsRanged => def != null && def.isRanged;
        public bool HasDeadly => def != null && def.hasDeadly && def.deadlyDieSides > 0;
        public int DeadlyDieSides => def != null ? Mathf.Max(0, def.deadlyDieSides) : 0;
        public bool HasFatal => def != null && def.hasFatal && def.fatalDieSides > 0;
        public int FatalDieSides => def != null ? Mathf.Max(0, def.fatalDieSides) : 0;

        public WeaponTraitFlags Traits => def != null ? def.traits : WeaponTraitFlags.None;
        public WeaponCategory Category => def != null ? def.category : WeaponCategory.Simple;

        public string Name => def != null ? def.itemName : "Weapon";
    }
}
