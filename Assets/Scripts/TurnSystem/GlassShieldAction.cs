using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public class GlassShieldAction : MonoBehaviour
    {
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        public const int ActionCost = 1;
        public const int BaseAcBonus = 1;
        public const int BaseHardness = 2;
        public const int BaseMaxHP = 1; // Spell always ends on Shield Block in this implementation.
        public const int BaseShardDiceCount = 1;
        public const int ShardDieSides = 4;
        public const int BlockCooldownRounds = 100; // 10 minutes.

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (entityManager == null) Debug.LogWarning("[GlassShieldAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[GlassShieldAction] Missing CombatEventBus", this);
        }
#endif

        public void InjectDependencies(EntityManager entityManager, CombatEventBus eventBus)
        {
            this.entityManager = entityManager;
            this.eventBus = eventBus;
        }

        public bool CanCastGlassShield(EntityHandle actor)
        {
            if (!actor.IsValid || entityManager == null || entityManager.Registry == null)
                return false;

            var data = entityManager.Registry.Get(actor);
            if (data == null || !data.IsAlive)
                return false;

            return data.CanCastGlassShield;
        }

        public bool TryCastGlassShield(EntityHandle actor)
        {
            if (!CanCastGlassShield(actor))
                return false;

            var data = entityManager.Registry.Get(actor);
            if (data == null)
                return false;

            int cantripRank = ComputeCantripRankForLevel(data.Level);
            int hardness = ComputeHardnessForRank(cantripRank);
            int shardDice = ComputeShardDiceForRank(cantripRank);

            if (!data.ActivateGlassShield(BaseAcBonus, hardness, BaseMaxHP))
                return false;

            eventBus?.PublishShieldRaised(actor, data.GlassShieldAcBonus, data.GlassShieldCurrentHP, data.GlassShieldMaxHP);
            eventBus?.Publish(
                actor,
                $"casts Glass Shield (+1 AC, Hardness {hardness}, shards {shardDice}d4 until start of next turn).",
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

        public static int ComputeShardDiceForLevel(int level)
        {
            return ComputeShardDiceForRank(ComputeCantripRankForLevel(level));
        }

        public static int ComputeReflexDc(EntityData caster)
        {
            if (caster == null)
                return 10;

            // MVP contract: until full spellcasting subsystem lands, use a deterministic proxy DC:
            // 10 + level + trained(2) + INT mod.
            int trainedBaseline = 2;
            return 10 + Mathf.Max(0, caster.Level) + trainedBaseline + caster.IntMod;
        }

        private static int ComputeHardnessForRank(int cantripRank)
        {
            if (cantripRank >= 9) return 12;
            if (cantripRank >= 7) return 10;
            if (cantripRank >= 5) return 7;
            if (cantripRank >= 3) return 4;
            return BaseHardness;
        }

        private static int ComputeShardDiceForRank(int cantripRank)
        {
            if (cantripRank >= 9) return 6;
            if (cantripRank >= 7) return 5;
            if (cantripRank >= 5) return 4;
            if (cantripRank >= 3) return 3;
            return BaseShardDiceCount;
        }
    }
}
