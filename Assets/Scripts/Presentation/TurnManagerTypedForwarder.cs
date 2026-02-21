using UnityEngine;

namespace PF2e.Presentation
{
    /// <summary>
    /// Deprecated compatibility stub.
    /// Typed turn events are now published directly by TurnManager to CombatEventBus.
    /// </summary>
    [System.Obsolete("TurnManagerTypedForwarder is deprecated. TurnManager now publishes typed events directly to CombatEventBus.", false)]
    public class TurnManagerTypedForwarder : MonoBehaviour
    {
        private static bool warned;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning(
                    "[TurnManagerTypedForwarder] Deprecated component. Remove it from the scene; TurnManager now publishes typed events directly.",
                    this);
            }
        }
#endif

        private void OnEnable()
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning(
                    "[TurnManagerTypedForwarder] Deprecated component is enabled. Disabling it; forwarding now happens inside TurnManager.",
                    this);
            }

            enabled = false;
        }
    }
}
