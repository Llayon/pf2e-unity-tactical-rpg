using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public class StrikeAction : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        [Header("Rules")]
        [SerializeField] private bool requireSameElevation = true;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[StrikeAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogError("[StrikeAction] Missing CombatEventBus", this);
        }
#endif

        public bool TryStrike(EntityHandle attacker, EntityHandle target)
        {
            if (!attacker.IsValid || !target.IsValid) return false;
            if (entityManager == null || entityManager.Registry == null) return false;

            var a = entityManager.Registry.Get(attacker);
            var t = entityManager.Registry.Get(target);
            if (a == null || t == null) return false;

            // 1. Resolve attack (pure)
            var check = AttackResolver.ResolveMeleeStrike(
                a, t, requireSameElevation, UnityRng.Shared);

            if (!check.performed) return false;

            // 2. MAP increment (exactly once, only here)
            a.MAPCount++;

            // 3. Resolve damage (pure)
            var weapon = a.EquippedWeapon;
            var dmg = DamageResolver.RollStrikeDamage(
                a, check.degree, UnityRng.Shared);

            // 4. Apply damage
            int damageDealt = 0;
            string damageType = "damage";
            int hpBefore = t.CurrentHP;

            if (dmg.dealt)
            {
                damageDealt = dmg.damage;
                damageType = (weapon.def != null) ? weapon.def.damageType.ToString() : "damage";

                t.CurrentHP -= damageDealt;
                if (t.CurrentHP < 0) t.CurrentHP = 0;
            }

            int hpAfter = t.CurrentHP;

            // 5. Build and publish typed event BEFORE HandleDeath
            bool defeated = (hpAfter <= 0);

            var ev = new StrikeResolvedEvent(
                attacker: attacker,
                target: target,
                weaponName: weapon.Name,
                naturalRoll: check.naturalRoll,
                attackBonus: check.attackBonus,
                mapPenalty: check.mapPenalty,
                total: check.total,
                dc: check.dc,
                degree: check.degree,
                damage: damageDealt,
                damageType: damageType,
                hpBefore: hpBefore,
                hpAfter: hpAfter,
                targetDefeated: defeated);

            eventBus?.PublishStrikeResolved(in ev);

            // 6. Handle death AFTER event published
            if (defeated)
            {
                entityManager.HandleDeath(target);
            }

            return true;
        }
    }
}
