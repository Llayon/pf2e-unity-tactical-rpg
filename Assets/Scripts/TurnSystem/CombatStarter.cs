using UnityEngine;
using UnityEngine.InputSystem;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Dev fallback only: press C to start combat, X to end combat.
    /// Primary encounter flow should come from UI controls.
    /// </summary>
    public class CombatStarter : MonoBehaviour
    {
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private bool allowDevHotkeys = true;

        private bool isReady = false;

        private void Start()
        {
            // Wait one frame to ensure all Awake() methods have run
            isReady = true;
        }

        private void Update()
        {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            return;
#else
            if (!isReady) return;
            if (!allowDevHotkeys) return;

            var kb = Keyboard.current;
            if (kb == null || turnManager == null) return;

            if (kb.cKey.wasPressedThisFrame && turnManager.State == TurnState.Inactive)
            {
                Debug.Log("[CombatStarter] Starting combat (C pressed)");
                turnManager.StartCombat();
            }

            if (kb.xKey.wasPressedThisFrame && turnManager.State != TurnState.Inactive)
            {
                Debug.Log("[CombatStarter] Ending combat (X pressed)");
                turnManager.EndCombat();
            }
#endif
        }
    }
}
