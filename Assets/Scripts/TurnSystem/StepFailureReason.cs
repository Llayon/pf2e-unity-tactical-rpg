namespace PF2e.TurnSystem
{
    public enum StepFailureReason : byte
    {
        None = 0,
        InvalidState,
        Prone,
        SpeedZero,
        NotAdjacent,
        Occupied,
        Impassable,
        DifficultTerrain
    }
}
