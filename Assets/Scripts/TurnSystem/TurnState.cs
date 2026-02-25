namespace PF2e.TurnSystem
{
    /// <summary>
    /// Tracks the current phase of a combat encounter.
    /// </summary>
    public enum TurnState
    {
        Inactive         = 0, // Combat not started
        RollingInitiative = 1, // Initiative rolls in progress
        PlayerTurn       = 2, // Awaiting player input
        ExecutingAction  = 3, // Action in progress (blocks input)
        EnemyTurn        = 4, // AI processing
        CombatOver       = 5, // Encounter ended
        DelayReturnWindow = 6, // Between-turn delay return trigger window
    }
}
