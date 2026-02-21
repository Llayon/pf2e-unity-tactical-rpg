using UnityEngine;
using PF2e.Core;

namespace PF2e.Presentation
{
    public class ConditionTickForwarder : MonoBehaviour
    {
        [SerializeField] private CombatEventBus eventBus;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null) Debug.LogWarning("[ConditionTickForwarder] Deprecated component still present with missing CombatEventBus.", this);
        }
#endif

        private void OnEnable()
        {
            // Deprecated: keep component inert for scene compatibility.
            enabled = false;
        }

        private void OnDisable()
        {
            // no-op
        }
    }
}
