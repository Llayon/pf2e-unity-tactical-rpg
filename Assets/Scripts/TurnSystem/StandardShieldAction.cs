using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public class StandardShieldAction : MonoBehaviour
    {
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        public const int ActionCost = 1;
        public const int BaseAcBonus = 1;
        public const int BaseHardness = 5;
        public const int BaseMaxHP = 1; // Spell always ends on Shield Block.
        public const int BlockCooldownRounds = 100; // 10 minutes.

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (entityManager == null) Debug.LogWarning("[StandardShieldAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[StandardShieldAction] Missing CombatEventBus", this);
        }
#endif

        public void InjectDependencies(EntityManager entityManager, CombatEventBus eventBus)
        {
            this.entityManager = entityManager;
            this.eventBus = eventBus;
        }

        public bool CanCastStandardShield(EntityHandle actor)
        {
            if (!actor.IsValid || entityManager == null || entityManager.Registry == null)
                return false;

            var data = entityManager.Registry.Get(actor);
            if (data == null || !data.IsAlive)
                return false;

            return data.CanCastStandardShield;
        }

        public bool TryCastStandardShield(EntityHandle actor)
        {
            if (!CanCastStandardShield(actor))
                return false;

            var data = entityManager.Registry.Get(actor);
            if (data == null)
                return false;

            int cantripRank = ComputeCantripRankForLevel(data.Level);
            int hardness = ComputeHardnessForRank(cantripRank);

            if (!data.ActivateStandardShield(BaseAcBonus, hardness, BaseMaxHP))
                return false;

            eventBus?.PublishShieldRaised(actor, data.StandardShieldAcBonus, data.StandardShieldCurrentHP, data.StandardShieldMaxHP);
            eventBus?.Publish(
                actor,
                $"casts Shield (+1 AC, Hardness {hardness} until start of next turn).",
                CombatLogCategory.Spell);
            return true;
        }

        public static int ComputeCantripRankForLevel(int level)
        {
            int normalizedLevel = Mathf.Max(1, level);
            return Mathf.Clamp((normalizedLevel + 1) / 2, 1, 10);
        }

        public static int ComputeHardnessForLevel(int level)
        {
            return ComputeHardnessForRank(ComputeCantripRankForLevel(level));
        }

        public static int ComputeHardnessForRank(int cantripRank)
        {
            int normalizedRank = Mathf.Max(1, cantripRank);
            int heightenedSteps = Mathf.Max(0, (normalizedRank - 1) / 2);
            return BaseHardness + (heightenedSteps * 5);
        }
    }
}
