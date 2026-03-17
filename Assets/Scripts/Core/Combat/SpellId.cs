namespace PF2e.Core
{
    public enum SpellId : byte
    {
        ForceBarrage = 0,
        ElectricArc = 1,
        Snowball = 2,
        BurningHands = 3
    }

    public enum SpellTargetingKind : byte
    {
        MultiShardCreature = 0,
        ChainCreature = 1,
        SingleCreature = 2,
        ConeCells = 3
    }

    public enum SpellResolutionKind : byte
    {
        AutoHitDamage = 0,
        BasicSaveDamage = 1,
        SpellAttackDamage = 2,
        BasicSaveAreaDamage = 3
    }
}
