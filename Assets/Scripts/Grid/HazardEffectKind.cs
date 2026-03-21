namespace PF2e.Grid
{
    public enum HazardEffectKind : byte
    {
        FlatDamage = 0,
        BasicSaveDamage = 1,
        ProneOnEntry = 2,
        DamageAndProneOnFailure = 3,
        PersistentFireOnEntry = 4,
        PersistentFireOnFailedSave = 5,
        BasicSaveDamageAndPersistentFireOnFailure = 6,
        ProneAndPersistentFireOnFailedSave = 7,
        PushOnFailedSave = 8,
        BasicSaveDamageAndPushOnFailedSave = 9,
        PullOnFailedSave = 10,
        BasicSaveDamageAndPullOnFailedSave = 11,
        ProneAndPullOnFailedSave = 12,
        PullAndPersistentFireOnFailedSave = 13,
        ProneAndPullAndPersistentFireOnFailedSave = 14,
        ProneAndPushAndPersistentFireOnFailedSave = 15,
        PersistentAcidOnFailedSave = 16,
        BasicSaveDamageAndPersistentAcidOnFailure = 17,
        ProneAndPersistentAcidOnFailedSave = 18,
        BasicSaveDamageAndProneAndPushOnFailedSave = 19,
        BasicSaveDamageAndProneAndPullOnFailedSave = 20
    }
}
