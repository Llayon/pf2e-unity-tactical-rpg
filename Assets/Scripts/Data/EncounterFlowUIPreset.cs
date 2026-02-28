using UnityEngine;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Data
{
    [CreateAssetMenu(fileName = "EncounterFlowUIPreset", menuName = "PF2e/UI/Encounter Flow Preset")]
    public class EncounterFlowUIPreset : ScriptableObject
    {
        [Header("Runtime Fallback")]
        public bool autoCreateRuntimeButtons = true;
        public RectTransform encounterFlowPanelPrefab;

        [Header("Encounter Rules")]
        public InitiativeCheckMode initiativeCheckMode = InitiativeCheckMode.Perception;
        public SkillType initiativeSkill = SkillType.Stealth;
    }
}
