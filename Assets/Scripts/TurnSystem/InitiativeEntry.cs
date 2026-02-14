using PF2e.Core;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Holds initiative roll data for one combatant.
    /// Sort descending by SortValue to get the correct turn order.
    /// </summary>
    [System.Serializable]
    public struct InitiativeEntry
    {
        public EntityHandle Handle;
        public int Roll;      // d20 result (1–20)
        public int Modifier;  // Perception or other modifier; may be negative
        public bool IsPlayer; // PF2e tiebreak: player acts before enemy on equal initiative

        /// <summary>
        /// Composite sort key. Higher = acts earlier.
        /// Formula: Roll*1000 + Modifier*10 + (IsPlayer ? 1 : 0)
        /// Works correctly even with negative Modifier values.
        /// </summary>
        public int SortValue => Roll * 1000 + Modifier * 10 + (IsPlayer ? 1 : 0);
    }
}
