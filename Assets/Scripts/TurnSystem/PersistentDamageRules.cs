using System.Collections.Generic;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public static class PersistentDamageRules
    {
        public const int FlatCheckDc = 15;
        public const string PersistentFireActionName = "Persistent fire";
        public const string PersistentAcidActionName = "Persistent acid";

        public static int ApplyEndTurnPersistentDamage(
            EntityHandle actor,
            EntityData data,
            EntityManager entityManager,
            CombatEventBus eventBus,
            ConditionService conditionService,
            List<ConditionDelta> conditionDeltaBuffer,
            IRng rng = null)
        {
            if (!actor.IsValid || data == null || entityManager == null || entityManager.Registry == null)
                return 0;
            if (conditionService == null || conditionDeltaBuffer == null)
                return 0;
            int appliedDamage = 0;

            appliedDamage += ApplyPersistentDamageForCondition(
                actor,
                data,
                ConditionType.PersistentFire,
                DamageType.Fire,
                PersistentFireActionName,
                entityManager,
                eventBus,
                conditionService,
                conditionDeltaBuffer,
                rng);

            if (!data.IsAlive)
                return appliedDamage;

            appliedDamage += ApplyPersistentDamageForCondition(
                actor,
                data,
                ConditionType.PersistentAcid,
                DamageType.Acid,
                PersistentAcidActionName,
                entityManager,
                eventBus,
                conditionService,
                conditionDeltaBuffer,
                rng);

            return appliedDamage;
        }

        private static int ApplyPersistentDamageForCondition(
            EntityHandle actor,
            EntityData data,
            ConditionType conditionType,
            DamageType damageType,
            string actionName,
            EntityManager entityManager,
            CombatEventBus eventBus,
            ConditionService conditionService,
            List<ConditionDelta> conditionDeltaBuffer,
            IRng rng)
        {
            if (!data.HasCondition(conditionType))
                return 0;

            int damagePerTick = data.GetConditionValue(conditionType);
            if (damagePerTick <= 0)
            {
                conditionService.Remove(data, conditionType, conditionDeltaBuffer);
                return 0;
            }

            int appliedDamage = DamageApplicationService.ApplyDamage(
                source: EntityHandle.None,
                target: actor,
                amount: damagePerTick,
                damageType: damageType,
                sourceActionName: actionName,
                isCritical: false,
                entityManager: entityManager,
                eventBus: eventBus);

            if (!data.IsAlive)
                return appliedDamage;

            int flatCheckRoll = (rng ?? UnityRng.Shared).RollD20();
            bool flatCheckSucceeded = flatCheckRoll >= FlatCheckDc;
            PublishFlatCheckLog(actor, actionName, flatCheckRoll, flatCheckSucceeded, eventBus);

            if (flatCheckSucceeded)
                conditionService.Remove(data, conditionType, conditionDeltaBuffer);

            return appliedDamage;
        }

        private static void PublishFlatCheckLog(
            EntityHandle actor,
            string actionName,
            int flatCheckRoll,
            bool flatCheckSucceeded,
            CombatEventBus eventBus)
        {
            if (eventBus == null)
                return;

            string result = flatCheckSucceeded ? "Success" : "Failure";
            eventBus.Publish(
                actor,
                $"rolls {actionName.ToLowerInvariant()} flat check d20({flatCheckRoll}) vs DC {FlatCheckDc} - {result}.",
                CombatLogCategory.ActionResult);
        }
    }
}
