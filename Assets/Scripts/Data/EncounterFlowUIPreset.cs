using UnityEngine;
using PF2e.Core;
using PF2e.TurnSystem;
using System.Collections.Generic;

namespace PF2e.Data
{
    [System.Serializable]
    public struct InitiativeActorOverride
    {
        public string actorId;
        // Legacy authoring key. Keep temporary fallback to avoid breaking older preset data.
        public string actorName;
        public bool useSkillOverride;
        public SkillType skill;
    }

    [CreateAssetMenu(fileName = "EncounterFlowUIPreset", menuName = "PF2e/UI/Encounter Flow Preset")]
    public class EncounterFlowUIPreset : ScriptableObject
    {
        [Header("Runtime Fallback")]
        public bool autoCreateRuntimeButtons = true;
        public RectTransform encounterFlowPanelPrefab;

        [Header("Encounter Rules")]
        public InitiativeCheckMode initiativeCheckMode = InitiativeCheckMode.Perception;
        public SkillType initiativeSkill = SkillType.Stealth;
        public List<InitiativeActorOverride> actorInitiativeOverrides = new();
    }
}
