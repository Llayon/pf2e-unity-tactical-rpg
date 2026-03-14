using System;
using System.Collections.Generic;
using UnityEngine;

namespace PF2e.Data
{
    [Serializable]
    public struct EncounterActorPortraitEntry
    {
        public string actorId;
        public Sprite portraitSprite;
    }

    [CreateAssetMenu(fileName = "EncounterActorPortraitLibrary", menuName = "PF2e/UI/Encounter Actor Portrait Library")]
    public class EncounterActorPortraitLibrary : ScriptableObject
    {
        [SerializeField] private List<EncounterActorPortraitEntry> entries = new();

        public Sprite Resolve(string encounterActorId)
        {
            string normalizedActorId = NormalizeActorId(encounterActorId);
            if (string.IsNullOrEmpty(normalizedActorId))
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                string candidateActorId = NormalizeActorId(entries[i].actorId);
                if (string.Equals(candidateActorId, normalizedActorId, StringComparison.OrdinalIgnoreCase))
                    return entries[i].portraitSprite;
            }

            return null;
        }

        private static string NormalizeActorId(string actorId)
        {
            return string.IsNullOrWhiteSpace(actorId) ? string.Empty : actorId.Trim();
        }
    }
}
