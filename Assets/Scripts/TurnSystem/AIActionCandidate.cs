using UnityEngine;
using PF2e.Core;

namespace PF2e.TurnSystem
{
    public enum AIActionCandidateKind
    {
        None = 0,
        Spell = 1,
        Skill = 2,
        Defensive = 3,
        Strike = 4,
        Step = 5,
        Stride = 6
    }

    public readonly struct AIActionCandidate
    {
        public readonly AIActionCandidateKind kind;
        public readonly int score;
        public readonly EntityHandle target;
        public readonly Vector3Int cell;
        public readonly bool hasCell;
        public readonly AISpellDecision spellDecision;
        public readonly AISkillDecision skillDecision;
        public readonly AIDefensiveDecision defensiveDecision;

        private AIActionCandidate(
            AIActionCandidateKind kind,
            int score,
            EntityHandle target,
            Vector3Int cell,
            bool hasCell,
            AISpellDecision spellDecision,
            AISkillDecision skillDecision,
            AIDefensiveDecision defensiveDecision)
        {
            this.kind = kind;
            this.score = score;
            this.target = target;
            this.cell = cell;
            this.hasCell = hasCell;
            this.spellDecision = spellDecision;
            this.skillDecision = skillDecision;
            this.defensiveDecision = defensiveDecision;
        }

        public bool IsValid => kind != AIActionCandidateKind.None;
        public bool HasCell => hasCell;

        public static AIActionCandidate Spell(int score, AISpellDecision decision)
        {
            return new AIActionCandidate(
                AIActionCandidateKind.Spell,
                score,
                decision.primaryTarget,
                decision.aimCell,
                decision.HasAimCell,
                decision,
                default,
                default);
        }

        public static AIActionCandidate Skill(int score, AISkillDecision decision)
        {
            return new AIActionCandidate(
                AIActionCandidateKind.Skill,
                score,
                decision.primaryTarget,
                decision.destinationCell,
                decision.HasDestinationCell,
                default,
                decision,
                default);
        }

        public static AIActionCandidate Defensive(int score, AIDefensiveDecision decision)
        {
            return new AIActionCandidate(
                AIActionCandidateKind.Defensive,
                score,
                EntityHandle.None,
                default,
                false,
                default,
                default,
                decision);
        }

        public static AIActionCandidate Strike(int score, EntityHandle target)
        {
            return new AIActionCandidate(
                AIActionCandidateKind.Strike,
                score,
                target,
                default,
                false,
                default,
                default,
                default);
        }

        public static AIActionCandidate Step(int score, Vector3Int cell)
        {
            return new AIActionCandidate(
                AIActionCandidateKind.Step,
                score,
                EntityHandle.None,
                cell,
                true,
                default,
                default,
                default);
        }

        public static AIActionCandidate Stride(int score, Vector3Int cell)
        {
            return new AIActionCandidate(
                AIActionCandidateKind.Stride,
                score,
                EntityHandle.None,
                cell,
                true,
                default,
                default,
                default);
        }
    }
}
