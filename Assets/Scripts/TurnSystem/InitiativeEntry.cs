using PF2e.Core;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Holds initiative roll data for one combatant.
    /// Sort descending by Total to get the correct turn order.
    /// </summary>
    [System.Serializable]
    public struct InitiativeEntry
    {
        public EntityHandle Handle;
        public CheckRoll Roll;
        public bool IsPlayer;

        public int Total => Roll.total;
    }
}
