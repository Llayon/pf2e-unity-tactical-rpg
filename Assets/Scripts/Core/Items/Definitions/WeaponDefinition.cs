using UnityEngine;

namespace PF2e.Core
{
    [CreateAssetMenu(fileName = "WeaponDef", menuName = "PF2e/Items/Weapon Definition")]
    public class WeaponDefinition : ItemDefinition
    {
        public WeaponCategory category = WeaponCategory.Simple;
        public WeaponGroup group = WeaponGroup.Club;
        public WeaponHands hands = WeaponHands.One;

        public int diceCount = 1;
        public int dieSides = 6;
        public DamageType damageType = DamageType.Slashing;

        public int reachFeet = 5;

        public bool isRanged = false;
        public int rangeIncrementFeet = 0;
        public int maxRangeIncrements = 0;

        public WeaponTraitFlags traits = WeaponTraitFlags.None;
        public bool hasDeadly = false;
        public int deadlyDieSides = 0;

        public bool usesAmmo = false;
        public AmmoType ammoType = AmmoType.None;
        public int ammoPerShot = 1;
    }
}
