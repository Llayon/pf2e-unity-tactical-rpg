using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public class ShieldBlockAction : MonoBehaviour
    {
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[ShieldBlockAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[ShieldBlockAction] Missing CombatEventBus", this);
        }
#endif

        public bool Execute(
            EntityHandle reactor,
            int incomingDamage,
            in ShieldBlockResult result,
            ShieldBlockSource source = ShieldBlockSource.PhysicalShield,
            EntityHandle triggerSource = default)
        {
            if (!reactor.IsValid || entityManager == null || entityManager.Registry == null)
                return false;

            var data = entityManager.Registry.Get(reactor);
            if (data == null || !data.IsAlive)
                return false;

            int hpBefore;
            int hpAfter;

            switch (source)
            {
                case ShieldBlockSource.GlassShield:
                    hpBefore = data.GlassShieldCurrentHP;
                    data.ApplyGlassShieldSelfDamageAndDispel(result.shieldSelfDamage, GlassShieldAction.BlockCooldownRounds);
                    hpAfter = data.GlassShieldCurrentHP;
                    eventBus?.Publish(reactor, "Glass Shield shatters; recast blocked for 10 minutes.", CombatLogCategory.Spell);
                    TryResolveGlassShieldShardBurst(reactor, triggerSource, data);
                    break;

                case ShieldBlockSource.PhysicalShield:
                default:
                    hpBefore = data.EquippedShield.currentHP;
                    data.ApplyShieldDamage(result.shieldSelfDamage);
                    hpAfter = data.EquippedShield.currentHP;
                    break;
            }

            data.ReactionAvailable = false;

            eventBus?.PublishShieldBlockResolved(
                reactor,
                incomingDamage,
                result.targetDamageReduction,
                result.shieldSelfDamage,
                hpBefore,
                hpAfter);

            return true;
        }

        private void TryResolveGlassShieldShardBurst(EntityHandle reactor, EntityHandle breaker, EntityData reactorData)
        {
            if (!breaker.IsValid)
                return;
            if (entityManager == null || entityManager.Registry == null || reactorData == null)
                return;

            var breakerData = entityManager.Registry.Get(breaker);
            if (breakerData == null || !breakerData.IsAlive)
                return;

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(reactorData.GridPosition, breakerData.GridPosition);
            if (distanceFeet > 5)
                return;

            int shardDice = GlassShieldAction.ComputeShardDiceForLevel(reactorData.Level);
            int rawDamage = 0;
            for (int i = 0; i < shardDice; i++)
                rawDamage += UnityRng.Shared.RollDie(GlassShieldAction.ShardDieSides);

            int dc = GlassShieldAction.ComputeReflexDc(reactorData);
            var save = CheckResolver.RollSave(breakerData, SaveType.Reflex, dc, UnityRng.Shared);
            int finalDamage = ApplyBasicSave(rawDamage, save.degree);

            string breakerName = string.IsNullOrWhiteSpace(breakerData.Name)
                ? $"Entity#{breaker.Id}"
                : breakerData.Name;

            eventBus?.Publish(
                reactor,
                $"Glass Shield shards vs {breakerName}: {rawDamage} piercing, basic Reflex DC {dc} -> {save.degree}, dealt {finalDamage}.",
                CombatLogCategory.Spell);

            if (finalDamage <= 0)
                return;

            DamageApplicationService.ApplyDamage(
                source: reactor,
                target: breaker,
                amount: finalDamage,
                damageType: DamageType.Piercing,
                sourceActionName: "Glass Shield (Shards)",
                isCritical: save.degree == DegreeOfSuccess.CriticalFailure,
                entityManager: entityManager,
                eventBus: eventBus);
        }

        private static int ApplyBasicSave(int damage, DegreeOfSuccess degree)
        {
            if (damage <= 0)
                return 0;

            return degree switch
            {
                DegreeOfSuccess.CriticalSuccess => 0,
                DegreeOfSuccess.Success => damage / 2,
                DegreeOfSuccess.Failure => damage,
                DegreeOfSuccess.CriticalFailure => damage * 2,
                _ => damage
            };
        }
    }
}
