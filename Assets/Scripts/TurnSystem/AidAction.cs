using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// PF2e Aid (MVP prepare step):
    /// - 1 action to prepare Aid for one ally in reach.
    /// - Generic preparation (not tied to a specific upcoming action).
    /// - Resolution/consumption is handled by AidService at check time.
    /// </summary>
    public class AidAction : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        [Header("Rules")]
        [SerializeField] private int defaultReachFeet = 5;

        public const int ActionCost = 1;
        private const string ActionName = "Aid";

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[AidAction] Missing EntityManager", this);
            if (eventBus == null) Debug.LogWarning("[AidAction] Missing CombatEventBus", this);
            if (defaultReachFeet < 5) defaultReachFeet = 5;
        }
#endif

        public TargetingFailureReason GetAidTargetFailure(EntityHandle helper, EntityHandle ally)
        {
            if (!helper.IsValid || !ally.IsValid) return TargetingFailureReason.InvalidTarget;
            if (entityManager == null || entityManager.Registry == null) return TargetingFailureReason.InvalidState;

            var helperData = entityManager.Registry.Get(helper);
            var allyData = entityManager.Registry.Get(ally);
            if (helperData == null || allyData == null) return TargetingFailureReason.InvalidTarget;
            if (!helperData.IsAlive || !allyData.IsAlive) return TargetingFailureReason.NotAlive;
            if (helper == ally) return TargetingFailureReason.SelfTarget;
            if (helperData.Team != allyData.Team) return TargetingFailureReason.WrongTeam;

            int distanceFeet = GridDistancePF2e.DistanceFeetXZ(helperData.GridPosition, allyData.GridPosition);
            if (distanceFeet > GetAidReachFeet(helperData)) return TargetingFailureReason.OutOfRange;

            return TargetingFailureReason.None;
        }

        public bool CanPrepareAid(EntityHandle helper, EntityHandle ally)
        {
            return GetAidTargetFailure(helper, ally) == TargetingFailureReason.None;
        }

        public bool TryPrepareAid(EntityHandle helper, EntityHandle ally, int roundNumber, AidService aidService)
        {
            if (!CanPrepareAid(helper, ally)) return false;
            if (aidService == null) return false;
            if (entityManager == null || entityManager.Registry == null) return false;

            bool prepared = aidService.PrepareAid(helper, ally, roundNumber);
            if (!prepared) return false;

            var allyData = entityManager.Registry.Get(ally);
            string allyName = allyData != null ? allyData.Name : $"Entity#{ally.Id}";
            eventBus?.Publish(helper, $"prepares Aid for {allyName}", CombatLogCategory.Turn);
            return true;
        }

        private int GetAidReachFeet(EntityData helperData)
        {
            if (helperData == null)
                return defaultReachFeet;

            if (helperData.EquippedWeapon.IsRanged)
                return defaultReachFeet;

            return Mathf.Max(defaultReachFeet, helperData.EquippedWeapon.ReachFeet);
        }
    }
}
