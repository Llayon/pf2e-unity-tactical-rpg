using System.Collections.Generic;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Prepares Delay-specific initiative row data (legal marker eligibility and delayed actor grouping/sorting)
    /// so InitiativeBarController can stay focused on rendering slots.
    /// </summary>
    internal sealed class DelayInitiativeRowPlanner
    {
        private readonly List<EntityData> buffer = new List<EntityData>(8);

        private TurnManager turnManager;
        private EntityManager entityManager;

        public void Bind(TurnManager turnManager, EntityManager entityManager)
        {
            this.turnManager = turnManager;
            this.entityManager = entityManager;
        }

        public bool ShouldAppendPlacementMarker(EntityHandle anchorHandle)
        {
            if (turnManager == null)
                return false;
            if (!turnManager.IsDelayPlacementSelectionOpen)
                return false;

            return turnManager.IsValidDelayAnchorForCurrentTurn(anchorHandle);
        }

        public IReadOnlyList<EntityData> CollectDelayedAnchoredTo(
            EntityHandle anchorHandle,
            HashSet<EntityHandle> alreadyAppended)
        {
            buffer.Clear();

            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return buffer;

            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null || !data.Handle.IsValid)
                    continue;
                if (!turnManager.IsDelayed(data.Handle))
                    continue;
                if (alreadyAppended != null && alreadyAppended.Contains(data.Handle))
                    continue;
                if (!turnManager.TryGetDelayedPlannedAnchor(data.Handle, out var plannedAnchor))
                    continue;
                if (plannedAnchor != anchorHandle)
                    continue;

                buffer.Add(data);
            }

            buffer.Sort(static (a, b) => a.Handle.Id.CompareTo(b.Handle.Id));
            return buffer;
        }

        public IReadOnlyList<EntityData> CollectRemainingDelayed(HashSet<EntityHandle> alreadyAppended)
        {
            buffer.Clear();

            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return buffer;

            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null || !data.Handle.IsValid)
                    continue;
                if (!turnManager.IsDelayed(data.Handle))
                    continue;
                if (alreadyAppended != null && alreadyAppended.Contains(data.Handle))
                    continue;

                buffer.Add(data);
            }

            buffer.Sort(static (a, b) => a.Handle.Id.CompareTo(b.Handle.Id));
            return buffer;
        }
    }
}
