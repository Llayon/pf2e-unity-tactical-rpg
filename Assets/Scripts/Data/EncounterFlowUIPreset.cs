using UnityEngine;

namespace PF2e.Data
{
    [CreateAssetMenu(fileName = "EncounterFlowUIPreset", menuName = "PF2e/UI/Encounter Flow Preset")]
    public class EncounterFlowUIPreset : ScriptableObject
    {
        [Header("Runtime Fallback")]
        public bool autoCreateRuntimeButtons = true;
        public RectTransform encounterFlowPanelPrefab;
    }
}
