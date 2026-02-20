using UnityEngine;

namespace PF2e.Presentation
{
    /// <summary>
    /// Legacy bridge kept only for backward scene compatibility.
    /// Replaced by TurnManagerTypedForwarder + TurnLogForwarder.
    /// </summary>
    [System.Obsolete("TurnManagerLogForwarder is deprecated. Use TurnManagerTypedForwarder + TurnLogForwarder.", false)]
    public class TurnManagerLogForwarder : MonoBehaviour
    {
        private void OnEnable()
        {
            Debug.LogWarning(
                "[TurnManagerLogForwarder] Deprecated component is active. " +
                "Disable/remove it and use TurnManagerTypedForwarder + TurnLogForwarder.",
                this);
            enabled = false;
        }
    }
}
