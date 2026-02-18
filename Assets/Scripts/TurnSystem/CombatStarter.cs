using UnityEngine;
using UnityEngine.InputSystem;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Debug helper: press C to start combat, X to end combat.
    /// Attach to the same GameObject as TurnManager (CombatController).
    /// </summary>
    public class CombatStarter : MonoBehaviour
    {
        [SerializeField] private TurnManager turnManager;

        private bool isReady = false;

        private void Start()
        {
            // Wait one frame to ensure all Awake() methods have run
            isReady = true;
        }

        private void Update()
        {
            if (!isReady) return;

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
        }
    }
}
