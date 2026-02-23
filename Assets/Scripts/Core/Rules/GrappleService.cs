using System.Collections.Generic;

namespace PF2e.Core
{
    /// <summary>
    /// Owns source-scoped grapple relations (MVP one-to-one) and coordinates condition application/removal.
    /// Plain C# service: no MonoBehaviour, no Unity object dependencies.
    /// </summary>
    public sealed class GrappleService
    {
        private struct GrappleRelation
        {
            public EntityHandle Grappler;
            public EntityHandle Target;
            public GrappleHoldState HoldState;
            public int TurnEndsUntilExpire;
        }

        private readonly ConditionService conditionService = new();
        private readonly Dictionary<EntityHandle, GrappleRelation> byGrappler = new();
        private readonly Dictionary<EntityHandle, EntityHandle> grapplerByTarget = new();
        private readonly List<EntityHandle> tempGrapplers = new();

        public int ActiveRelationCount => byGrappler.Count;

        public bool TryGetRelationByGrappler(EntityHandle grappler, out GrappleRelationView relation)
        {
            if (byGrappler.TryGetValue(grappler, out var stored))
            {
                relation = ToView(stored);
                return true;
            }

            relation = default;
            return false;
        }

        public bool TryGetRelationByTarget(EntityHandle target, out GrappleRelationView relation)
        {
            if (grapplerByTarget.TryGetValue(target, out var grappler) && byGrappler.TryGetValue(grappler, out var stored))
            {
                relation = ToView(stored);
                return true;
            }

            relation = default;
            return false;
        }

        public bool HasExactRelation(EntityHandle grappler, EntityHandle target)
        {
            return byGrappler.TryGetValue(grappler, out var stored) && stored.Target == target;
        }

        public void ApplyOrRefresh(
            EntityData grapplerData,
            EntityData targetData,
            GrappleHoldState hold,
            EntityRegistry registry,
            List<ConditionDelta> outDeltas)
        {
            if (grapplerData == null || targetData == null || registry == null || outDeltas == null) return;
            if (!grapplerData.Handle.IsValid || !targetData.Handle.IsValid) return;
            if (hold == GrappleHoldState.None) return;

            var grappler = grapplerData.Handle;
            var target = targetData.Handle;

            // Enforce one-to-one relation ownership in the MVP model.
            if (byGrappler.TryGetValue(grappler, out var existingByGrappler) && existingByGrappler.Target != target)
                ReleaseExact(grappler, registry, outDeltas);

            if (grapplerByTarget.TryGetValue(target, out var existingGrappler) && existingGrappler != grappler)
                ReleaseExact(existingGrappler, registry, outDeltas, expectedTarget: target);

            var relation = new GrappleRelation
            {
                Grappler = grappler,
                Target = target,
                HoldState = hold,
                TurnEndsUntilExpire = 2 // until end of grappler's next turn
            };

            byGrappler[grappler] = relation;
            grapplerByTarget[target] = grappler;

            ApplyHoldConditions(targetData, hold, outDeltas);
        }

        public bool ReleaseExact(
            EntityHandle grappler,
            EntityRegistry registry,
            List<ConditionDelta> outDeltas,
            EntityHandle expectedTarget = default)
        {
            if (!grappler.IsValid || registry == null || outDeltas == null) return false;
            if (!byGrappler.TryGetValue(grappler, out var relation)) return false;
            if (expectedTarget.IsValid && relation.Target != expectedTarget) return false;

            byGrappler.Remove(grappler);
            if (grapplerByTarget.TryGetValue(relation.Target, out var mapped) && mapped == grappler)
                grapplerByTarget.Remove(relation.Target);

            var targetData = registry.Get(relation.Target);
            if (targetData != null)
            {
                conditionService.Remove(targetData, ConditionType.Grabbed, outDeltas);
                conditionService.Remove(targetData, ConditionType.Restrained, outDeltas);
            }

            return true;
        }

        public bool ReleaseByTarget(EntityHandle target, EntityRegistry registry, List<ConditionDelta> outDeltas)
        {
            if (!target.IsValid) return false;
            if (!grapplerByTarget.TryGetValue(target, out var grappler)) return false;
            return ReleaseExact(grappler, registry, outDeltas, expectedTarget: target);
        }

        public void OnTurnEnded(EntityHandle turnEntity, EntityRegistry registry, List<ConditionDelta> outDeltas)
        {
            if (!turnEntity.IsValid || registry == null || outDeltas == null) return;
            if (!byGrappler.TryGetValue(turnEntity, out var relation)) return;

            relation.TurnEndsUntilExpire--;
            if (relation.TurnEndsUntilExpire <= 0)
            {
                ReleaseExact(turnEntity, registry, outDeltas);
                return;
            }

            byGrappler[turnEntity] = relation;
        }

        public void OnEntityMoved(EntityHandle mover, EntityRegistry registry, List<ConditionDelta> outDeltas)
        {
            if (!mover.IsValid || registry == null || outDeltas == null) return;

            // RAW: grapple ends if the grappler moves. Target movement is handled separately by grapple/immobilized rules.
            if (byGrappler.ContainsKey(mover))
                ReleaseExact(mover, registry, outDeltas);
        }

        public void ClearAll(EntityRegistry registry, List<ConditionDelta> outDeltas)
        {
            if (registry == null || outDeltas == null) return;
            if (byGrappler.Count == 0)
            {
                grapplerByTarget.Clear();
                return;
            }

            tempGrapplers.Clear();
            foreach (var kvp in byGrappler)
                tempGrapplers.Add(kvp.Key);

            for (int i = 0; i < tempGrapplers.Count; i++)
                ReleaseExact(tempGrapplers[i], registry, outDeltas);

            tempGrapplers.Clear();
            byGrappler.Clear();
            grapplerByTarget.Clear();
        }

        private void ApplyHoldConditions(EntityData targetData, GrappleHoldState hold, List<ConditionDelta> outDeltas)
        {
            switch (hold)
            {
                case GrappleHoldState.Grabbed:
                    conditionService.Remove(targetData, ConditionType.Restrained, outDeltas);
                    conditionService.AddOrRefresh(targetData, ConditionType.Grabbed, value: 0, rounds: -1, outDeltas);
                    break;

                case GrappleHoldState.Restrained:
                    conditionService.Remove(targetData, ConditionType.Grabbed, outDeltas);
                    conditionService.AddOrRefresh(targetData, ConditionType.Restrained, value: 0, rounds: -1, outDeltas);
                    break;
            }
        }

        private static GrappleRelationView ToView(in GrappleRelation relation)
        {
            return new GrappleRelationView(relation.Grappler, relation.Target, relation.HoldState, relation.TurnEndsUntilExpire);
        }
    }
}
