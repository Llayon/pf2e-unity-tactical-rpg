namespace PF2e.Core
{
    public enum SpellId : byte
    {
        ForceBarrage = 0,
        ElectricArc = 1
    }

    public enum SpellTargetingKind : byte
    {
        MultiShardCreature = 0,
        ChainCreature = 1
    }

    public enum SpellResolutionKind : byte
    {
        AutoHitDamage = 0,
        BasicSaveDamage = 1
    }
}
