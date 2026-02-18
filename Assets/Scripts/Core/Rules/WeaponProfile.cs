using UnityEngine;

namespace PF2e.Core
{
    [System.Serializable]
    public struct WeaponProfile
    {
        public string name;

        [Header("Category / Rarity / Group")]
        public WeaponCategory category;
        public ItemRarity rarity;
        public WeaponGroup group;

        [Header("Hands / Bulk / Price")]
        public WeaponHands hands;
        public int bulkTenth;   // 1 = L bulk, 10 = 1 Bulk
        public int priceCopper; // 100 = 1 gp (project convention)

        [Header("Damage (base)")]
        public int diceCount;   // number of dice
        public int dieSides;    // d4/d6/d8/...
        public DamageType damageType;

        [Header("Reach / Range")]
        public int reachFeet;
        public bool isRanged;
        public int rangeIncrementFeet;
        public int maxRangeIncrements;

        [Header("Traits")]
        public WeaponTraitFlags traits;

        [Header("Fundamental Runes")]
        public int potencyBonus;               // 0..3 item bonus to attack
        public StrikingRuneRank strikingRank;  // None/Striking/Greater/Major

        [Header("Ammo (future)")]
        public bool usesAmmo;
        public AmmoType ammoType;
        public int ammoPerShot;

        public bool HasTrait(WeaponTraitFlags t) => (traits & t) != 0;

        public int GetPotencyBonusClamped() => Mathf.Clamp(potencyBonus, 0, 3);
        public int GetDamageDiceMultiplier() => strikingRank.DamageDiceMultiplier();
        public int GetEffectiveDiceCount() => Mathf.Max(0, diceCount) * GetDamageDiceMultiplier();

        public static WeaponProfile CreateMelee(
            string name,
            WeaponCategory category,
            ItemRarity rarity,
            WeaponGroup group,
            WeaponHands hands,
            int bulkTenth,
            int priceCopper,
            int diceCount, int dieSides,
            DamageType damageType,
            int reachFeet,
            WeaponTraitFlags traits = WeaponTraitFlags.None)
        {
            return new WeaponProfile
            {
                name = name,
                category = category,
                rarity = rarity,
                group = group,

                hands = hands,
                bulkTenth = Mathf.Max(0, bulkTenth),
                priceCopper = Mathf.Max(0, priceCopper),

                diceCount = Mathf.Max(0, diceCount),
                dieSides = Mathf.Max(0, dieSides),
                damageType = damageType,

                reachFeet = Mathf.Max(0, reachFeet),
                isRanged = false,
                rangeIncrementFeet = 0,
                maxRangeIncrements = 0,

                traits = traits,

                potencyBonus = 0,
                strikingRank = StrikingRuneRank.None,

                usesAmmo = false,
                ammoType = AmmoType.None,
                ammoPerShot = 0
            };
        }

        public static WeaponProfile CreateRanged(
            string name,
            WeaponCategory category,
            ItemRarity rarity,
            WeaponGroup group,
            WeaponHands hands,
            int bulkTenth,
            int priceCopper,
            int diceCount, int dieSides,
            DamageType damageType,
            int rangeIncrementFeet,
            int maxRangeIncrements,
            AmmoType ammoType,
            int ammoPerShot = 1,
            WeaponTraitFlags traits = WeaponTraitFlags.None)
        {
            return new WeaponProfile
            {
                name = name,
                category = category,
                rarity = rarity,
                group = group,

                hands = hands,
                bulkTenth = Mathf.Max(0, bulkTenth),
                priceCopper = Mathf.Max(0, priceCopper),

                diceCount = Mathf.Max(0, diceCount),
                dieSides = Mathf.Max(0, dieSides),
                damageType = damageType,

                reachFeet = 5,
                isRanged = true,
                rangeIncrementFeet = Mathf.Max(0, rangeIncrementFeet),
                maxRangeIncrements = Mathf.Max(1, maxRangeIncrements),

                traits = traits,

                potencyBonus = 0,
                strikingRank = StrikingRuneRank.None,

                usesAmmo = ammoType != AmmoType.None,
                ammoType = ammoType,
                ammoPerShot = Mathf.Max(1, ammoPerShot)
            };
        }
    }
}
