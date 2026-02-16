using System;
using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;


namespace PF2e.Grid
{
    /// <summary>
    /// A* pathfinding with (pos, diagonalParity) state + Dijkstra flood-fill.
    /// Instance class with reusable buffers — zero allocation per call.
    /// Heuristic: parity-aware Chebyshev (admissible + consistent).
    /// Tie-breaking: among equal f, prefer diagonal steps (cleaner-looking paths).
    /// </summary>
    public class GridPathfinding
    {
        // A* state = (position, diagonalParity)
        // diagonalParity: false = next diagonal costs 5ft, true = next diagonal costs 10ft

        private struct PathNode
        {
            public Vector3Int pos;
            public bool parity;
            public int gCostAtEnqueue; // For lazy deletion: skip if stale
        }

        /// <summary>
        /// Composite priority: f first, then prefer diagonal steps (fewer total steps).
        /// </summary>
        private readonly struct PathPriority : IComparable<PathPriority>
        {
            public readonly int f;
            public readonly int tieBreak; // 0 = diagonal/start, 1 = cardinal/vertical

            public PathPriority(int f, int tieBreak = 0) { this.f = f; this.tieBreak = tieBreak; }

            public int CompareTo(PathPriority other)
            {
                int cmp = f.CompareTo(other.f);
                return cmp != 0 ? cmp : tieBreak.CompareTo(other.tieBreak);
            }
        }

        private static int StepTypeTieBreak(NeighborType type)
        {
            return type == NeighborType.Diagonal ? 0 : 1;
        }

        // Reusable buffers
        private readonly MinBinaryHeap<PathNode, PathPriority> openSet = new();
        private readonly HashSet<(Vector3Int, bool)> closedSet = new();
        private readonly Dictionary<(Vector3Int, bool), (Vector3Int, bool)> cameFrom = new();
        private readonly Dictionary<(Vector3Int, bool), int> costSoFar = new();
        private readonly List<NeighborInfo> neighborBuffer = new(16);

        // Reusable buffer for flood-fill
        private readonly Dictionary<Vector3Int, int> minCostPerPos = new();

        // Zone cache
        private int cachedGridVersion = -1;
        private Vector3Int cachedOrigin;
        private MovementProfile cachedProfile;
        private int cachedBudgetFeet;
        private Dictionary<Vector3Int, int> cachedZone;

        /// <summary>
        /// Find shortest path from → to. Returns false if no path exists.
        /// outPath includes start and end. totalCost is in feet.
        /// Note: profile.speedFeet is NOT enforced here — path has no budget limit.
        /// Speed limit is enforced by GetMovementZone (budgetFeet parameter).
        /// </summary>
        public bool FindPath(GridData grid, Vector3Int from, Vector3Int to,
            MovementProfile profile, List<Vector3Int> outPath, out int totalCost)
        {
            outPath.Clear();
            totalCost = 0;

            if (!grid.IsCellPassable(from, profile.moveType) ||
                !grid.IsCellPassable(to, profile.moveType)) return false;
            if (from == to)
            {
                outPath.Add(from);
                return true;
            }

            openSet.Clear();
            closedSet.Clear();
            cameFrom.Clear();
            costSoFar.Clear();

            // PF2e rule: first diagonal always costs 5ft → start with parity=false only
            var startState = (from, false);
            costSoFar[startState] = 0;

            int h = Heuristic(from, false, to);
            openSet.Enqueue(new PathNode { pos = from, parity = false, gCostAtEnqueue = 0 },
                new PathPriority(h, 0));

            while (openSet.Count > 0)
            {
                var node = openSet.Dequeue();
                var state = (node.pos, node.parity);

                // Lazy deletion: skip stale entries
                if (closedSet.Contains(state)) continue;
                if (!costSoFar.TryGetValue(state, out int currentCost) ||
                    node.gCostAtEnqueue > currentCost)
                    continue;

                // Goal reached
                if (node.pos == to)
                {
                    totalCost = currentCost;
                    ReconstructPath(state, outPath);
                    return true;
                }

                closedSet.Add(state);

                grid.GetNeighbors(node.pos, profile.moveType, neighborBuffer);

                foreach (var neighbor in neighborBuffer)
                {
                    if (!grid.TryGetCell(neighbor.pos, out var targetCell)) continue;

                    bool newParity = neighbor.type == NeighborType.Diagonal
                        ? !node.parity
                        : node.parity;

                    int stepCost = MovementCostEvaluator.GetStepCost(
                        targetCell, neighbor, node.parity, profile);

                    int newCost = currentCost + stepCost;
                    var newState = (neighbor.pos, newParity);

                    if (closedSet.Contains(newState)) continue;

                    if (!costSoFar.TryGetValue(newState, out int existingCost) ||
                        newCost < existingCost)
                    {
                        costSoFar[newState] = newCost;
                        cameFrom[newState] = state;
                        int f = newCost + Heuristic(neighbor.pos, newParity, to);
                        openSet.Enqueue(new PathNode
                        {
                            pos = neighbor.pos,
                            parity = newParity,
                            gCostAtEnqueue = newCost
                        }, new PathPriority(f, StepTypeTieBreak(neighbor.type)));
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Find shortest path with occupancy awareness.
        /// PF2e rules: ally cells can be traversed but not stopped on; enemy cells block.
        /// </summary>
        public bool FindPath(GridData grid, Vector3Int from, Vector3Int to,
            MovementProfile profile, EntityHandle mover, OccupancyMap occupancy,
            List<Vector3Int> outPath, out int totalCost)
        {
            outPath.Clear();
            totalCost = 0;

            if (!grid.IsCellPassable(from, profile.moveType) ||
                !grid.IsCellPassable(to, profile.moveType)) return false;
            if (from == to)
            {
                outPath.Add(from);
                return true;
            }

            openSet.Clear();
            closedSet.Clear();
            cameFrom.Clear();
            costSoFar.Clear();

            var startState = (from, false);
            costSoFar[startState] = 0;

            int h = Heuristic(from, false, to);
            openSet.Enqueue(new PathNode { pos = from, parity = false, gCostAtEnqueue = 0 },
                new PathPriority(h, 0));

            while (openSet.Count > 0)
            {
                var node = openSet.Dequeue();
                var state = (node.pos, node.parity);

                // Lazy deletion: skip stale entries
                if (closedSet.Contains(state)) continue;
                if (!costSoFar.TryGetValue(state, out int currentCost) ||
                    node.gCostAtEnqueue > currentCost)
                    continue;

                // Goal reached — check if we can stop here
                if (node.pos == to)
                {
                    if (occupancy != null && !occupancy.CanOccupy(to, mover))
                    {
                        // Can't stop on goal (e.g. ally there). Let A* try other states.
                        closedSet.Add(state);
                        continue;
                    }

                    totalCost = currentCost;
                    ReconstructPath(state, outPath);
                    return true;
                }

                closedSet.Add(state);

                grid.GetNeighbors(node.pos, profile.moveType, neighborBuffer);

                foreach (var neighbor in neighborBuffer)
                {
                    if (!grid.TryGetCell(neighbor.pos, out var targetCell)) continue;

                    // Occupancy: can we traverse this cell?
                    if (occupancy != null && !occupancy.CanTraverse(neighbor.pos, mover))
                        continue;

                    bool newParity = neighbor.type == NeighborType.Diagonal
                        ? !node.parity
                        : node.parity;

                    int stepCost = MovementCostEvaluator.GetStepCost(
                        targetCell, neighbor, node.parity, profile);

                    int newCost = currentCost + stepCost;
                    var newState = (neighbor.pos, newParity);

                    if (closedSet.Contains(newState)) continue;

                    if (!costSoFar.TryGetValue(newState, out int existingCost) ||
                        newCost < existingCost)
                    {
                        costSoFar[newState] = newCost;
                        cameFrom[newState] = state;
                        int f = newCost + Heuristic(neighbor.pos, newParity, to);
                        openSet.Enqueue(new PathNode
                        {
                            pos = neighbor.pos,
                            parity = newParity,
                            gCostAtEnqueue = newCost
                        }, new PathPriority(f, StepTypeTieBreak(neighbor.type)));
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Dijkstra flood-fill: compute all reachable cells within budgetFeet.
        /// Output: outZone[pos] = minimum cost to reach pos.
        /// </summary>
        public void GetMovementZone(GridData grid, Vector3Int origin,
            MovementProfile profile, int budgetFeet, Dictionary<Vector3Int, int> outZone)
        {
            outZone.Clear();

            // Check cache
            if (cachedZone != null &&
                cachedGridVersion == grid.Version &&
                cachedOrigin == origin &&
                cachedProfile == profile &&
                cachedBudgetFeet == budgetFeet)
            {
                // Cache hit — copy
                foreach (var kvp in cachedZone)
                    outZone[kvp.Key] = kvp.Value;
                return;
            }

            if (!grid.IsCellPassable(origin, profile.moveType)) return;

            openSet.Clear();
            costSoFar.Clear();
            closedSet.Clear();

            // PF2e rule: first diagonal always costs 5ft → start with parity=false only
            var startState = (origin, false);
            costSoFar[startState] = 0;

            openSet.Enqueue(new PathNode { pos = origin, parity = false, gCostAtEnqueue = 0 },
                new PathPriority(0, 0));

            // Track minimum cost per position across both parities
            minCostPerPos.Clear();
            minCostPerPos[origin] = 0;

            while (openSet.Count > 0)
            {
                var node = openSet.Dequeue();
                var state = (node.pos, node.parity);

                // Lazy deletion: skip stale or already-closed entries
                if (closedSet.Contains(state)) continue;
                if (!costSoFar.TryGetValue(state, out int currentCost) ||
                    node.gCostAtEnqueue > currentCost)
                    continue;

                closedSet.Add(state);

                grid.GetNeighbors(node.pos, profile.moveType, neighborBuffer);

                foreach (var neighbor in neighborBuffer)
                {
                    if (!grid.TryGetCell(neighbor.pos, out var targetCell)) continue;

                    bool newParity = neighbor.type == NeighborType.Diagonal
                        ? !node.parity
                        : node.parity;

                    int stepCost = MovementCostEvaluator.GetStepCost(
                        targetCell, neighbor, node.parity, profile);

                    int newCost = currentCost + stepCost;
                    if (newCost > budgetFeet) continue;

                    var newState = (neighbor.pos, newParity);

                    if (closedSet.Contains(newState)) continue;

                    if (!costSoFar.TryGetValue(newState, out int existingCost) ||
                        newCost < existingCost)
                    {
                        costSoFar[newState] = newCost;
                        openSet.Enqueue(new PathNode
                        {
                            pos = neighbor.pos,
                            parity = newParity,
                            gCostAtEnqueue = newCost
                        }, new PathPriority(newCost, StepTypeTieBreak(neighbor.type)));

                        // Track minimum across parities
                        if (!minCostPerPos.TryGetValue(neighbor.pos, out int minSoFar) ||
                            newCost < minSoFar)
                        {
                            minCostPerPos[neighbor.pos] = newCost;
                        }
                    }
                }
            }

            // Output = min cost per position
            foreach (var kvp in minCostPerPos)
                outZone[kvp.Key] = kvp.Value;

            // Update cache (reuse dictionary if possible)
            cachedGridVersion = grid.Version;
            cachedOrigin = origin;
            cachedProfile = profile;
            cachedBudgetFeet = budgetFeet;
            if (cachedZone == null)
                cachedZone = new Dictionary<Vector3Int, int>(outZone);
            else
            {
                cachedZone.Clear();
                foreach (var kvp in outZone)
                    cachedZone[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Dijkstra flood-fill with PF2e occupancy rules.
        /// Expand by CanTraverse (allies/neutrals passable, enemies block).
        /// Result filtered by CanOccupy (can't stop on ally/enemy).
        /// Origin always included. No caching (occupancy changes each turn).
        /// </summary>
        public void GetMovementZone(
            GridData grid,
            Vector3Int origin,
            MovementProfile profile,
            int budgetFeet,
            EntityHandle mover,
            OccupancyMap occupancy,
            Dictionary<Vector3Int, int> outZone)
        {
            outZone.Clear();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (occupancy != null)
            {
                var occ = occupancy.GetOccupant(origin);
                if (occ.IsValid && occ != mover)
                    Debug.LogWarning($"[GetMovementZone] Origin {origin} occupied by {occ}, not by mover {mover}. Possible integration error.");
            }
#endif

            if (!grid.IsCellPassable(origin, profile.moveType)) return;

            openSet.Clear();
            costSoFar.Clear();
            closedSet.Clear();
            minCostPerPos.Clear();

            var startState = (origin, false);
            costSoFar[startState] = 0;

            openSet.Enqueue(
                new PathNode { pos = origin, parity = false, gCostAtEnqueue = 0 },
                new PathPriority(0, 0));

            minCostPerPos[origin] = 0;

            while (openSet.Count > 0)
            {
                var node = openSet.Dequeue();
                var state = (node.pos, node.parity);

                if (closedSet.Contains(state)) continue;
                if (!costSoFar.TryGetValue(state, out int currentCost) ||
                    node.gCostAtEnqueue > currentCost)
                    continue;

                closedSet.Add(state);

                grid.GetNeighbors(node.pos, profile.moveType, neighborBuffer);

                foreach (var neighbor in neighborBuffer)
                {
                    if (!grid.TryGetCell(neighbor.pos, out var targetCell)) continue;

                    // NOTE: Occupancy checked only on neighbor.pos.
                    // Diagonal between two occupied cardinals is allowed if target diagonal is traversable (PF2e RAW).
                    if (occupancy != null && !occupancy.CanTraverse(neighbor.pos, mover))
                        continue;

                    bool newParity = neighbor.type == NeighborType.Diagonal
                        ? !node.parity
                        : node.parity;

                    int stepCost = MovementCostEvaluator.GetStepCost(
                        targetCell, neighbor, node.parity, profile);

                    int newCost = currentCost + stepCost;
                    if (newCost > budgetFeet) continue;

                    var newState = (neighbor.pos, newParity);

                    if (closedSet.Contains(newState)) continue;

                    if (!costSoFar.TryGetValue(newState, out int existingCost) ||
                        newCost < existingCost)
                    {
                        costSoFar[newState] = newCost;
                        openSet.Enqueue(new PathNode
                        {
                            pos = neighbor.pos,
                            parity = newParity,
                            gCostAtEnqueue = newCost
                        }, new PathPriority(newCost, StepTypeTieBreak(neighbor.type)));

                        if (!minCostPerPos.TryGetValue(neighbor.pos, out int minSoFar) ||
                            newCost < minSoFar)
                        {
                            minCostPerPos[neighbor.pos] = newCost;
                        }
                    }
                }
            }

            // Build result: only cells where mover can stop
            foreach (var kvp in minCostPerPos)
            {
                Vector3Int pos = kvp.Key;

                if (pos == origin)
                {
                    outZone[pos] = kvp.Value;
                    continue;
                }

                if (occupancy != null && !occupancy.CanOccupy(pos, mover))
                    continue;

                outZone[pos] = kvp.Value;
            }
        }

        /// <summary>
        /// Parity-aware heuristic: exact optimal cost in obstacle-free flat grid.
        /// Admissible (h ≤ h*) and consistent (h(s) ≤ cost(s→s') + h(s')).
        /// Proof: diagonal step changes h by exactly step cost; cardinal by at most 5.
        /// </summary>
        private static int Heuristic(Vector3Int a, bool parity, Vector3Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            int dz = Mathf.Abs(a.z - b.z);
            int diags = Mathf.Min(dx, dz);
            int straight = Mathf.Abs(dx - dz);

            // Exact diagonal cost from current parity
            int diagCost;
            if (!parity)
            {
                // parity=false: 5,10,5,10... → ceil(d/2)*5 + floor(d/2)*10
                diagCost = ((diags + 1) / 2) * 5 + (diags / 2) * 10;
            }
            else
            {
                // parity=true: 10,5,10,5... → ceil(d/2)*10 + floor(d/2)*5
                diagCost = ((diags + 1) / 2) * 10 + (diags / 2) * 5;
            }

            return diagCost + (straight + dy) * 5;
        }

        private void ReconstructPath((Vector3Int, bool) endState, List<Vector3Int> outPath)
        {
            var state = endState;
            while (cameFrom.ContainsKey(state))
            {
                outPath.Add(state.Item1);
                state = cameFrom[state];
            }
            outPath.Add(state.Item1); // start
            outPath.Reverse();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Phase 9.5: Multi-Stride (action-based) movement for PF2e (Solasta-like)
        // Zone + pathfinding across 1..N Stride actions, with diagonal parity reset
        // on each new Stride.
        // Stores cameFrom + best stop-states so UI can reconstruct hover path in O(path).
        // ═══════════════════════════════════════════════════════════════════════

        private const int ActionPenalty = 1_000_000;

        private readonly struct ActionState : IEquatable<ActionState>
        {
            public readonly Vector3Int pos;
            public readonly bool parity;         // diagonal parity INSIDE CURRENT stride
            public readonly byte actionsUsed;    // Stride actions started/used (0..maxActions)
            public readonly ushort feetInStride; // feet spent in current stride (0..speed)

            public ActionState(Vector3Int pos, bool parity, byte actionsUsed, int feetInStride)
            {
                this.pos = pos;
                this.parity = parity;
                this.actionsUsed = actionsUsed;
                this.feetInStride = (ushort)Mathf.Clamp(feetInStride, 0, ushort.MaxValue);
            }

            public bool Equals(ActionState other)
                => pos == other.pos
                   && parity == other.parity
                   && actionsUsed == other.actionsUsed
                   && feetInStride == other.feetInStride;

            public override bool Equals(object obj) => obj is ActionState o && Equals(o);
            public override int GetHashCode() => HashCode.Combine(pos, parity, actionsUsed, feetInStride);
        }

        private struct ActionNode
        {
            public ActionState state;
            public int packedAtEnqueue; // lazy deletion
        }

        private static int PackCost(byte actionsUsed, int feetTotal)
            => actionsUsed * ActionPenalty + feetTotal;

        private static int UnpackFeet(int packed) => packed % ActionPenalty;

        // Reusable buffers (separate from normal A*/Dijkstra buffers)
        private readonly MinBinaryHeap<ActionNode, int> actionOpenSet = new();
        private readonly HashSet<ActionState> actionClosed = new();
        private readonly Dictionary<ActionState, int> actionCostSoFar = new();
        private readonly Dictionary<ActionState, ActionState> actionCameFrom = new();
        private readonly List<Vector3Int> actionPathReconstructBuffer = new(128);
        private readonly List<ActionState> actionStateReconstructBuffer = new(128);

        // For UI reconstruction: best stop-state per position from the last zone build
        private readonly Dictionary<Vector3Int, ActionState> zoneBestState = new();

        /// <summary>
        /// Dijkstra over ActionState:
        /// - primary objective: minimize actionsUsed
        /// - secondary: minimize total feet
        /// Priority = PackCost(actionsUsed, totalFeet).
        ///
        /// Output: outMinActions[pos] = minimum number of actions needed to STOP on pos.
        /// Stores cameFrom + zoneBestState for ReconstructPathFromZone().
        /// </summary>
        public void GetMovementZoneByActions(
            GridData grid, Vector3Int origin, MovementProfile profile,
            int maxActions, EntityHandle mover, OccupancyMap occupancy,
            Dictionary<Vector3Int, int> outMinActions)
        {
            outMinActions.Clear();

            actionOpenSet.Clear();
            actionClosed.Clear();
            actionCostSoFar.Clear();
            actionCameFrom.Clear();
            zoneBestState.Clear();

            maxActions = Mathf.Clamp(maxActions, 0, 3);

            if (!grid.IsCellPassable(origin, profile.moveType)) return;

            // Origin always included
            outMinActions[origin] = 0;

            // Save origin as reconstructable anchor (path length will be 1)
            var startState = new ActionState(origin, parity: false, actionsUsed: 0, feetInStride: 0);
            zoneBestState[origin] = startState;

            if (maxActions <= 0) return;

            int startPacked = PackCost(0, 0);
            actionCostSoFar[startState] = startPacked;
            actionOpenSet.Enqueue(new ActionNode { state = startState, packedAtEnqueue = startPacked }, startPacked);

            while (actionOpenSet.Count > 0)
            {
                var node = actionOpenSet.Dequeue();
                var s = node.state;

                if (actionClosed.Contains(s)) continue;

                if (!actionCostSoFar.TryGetValue(s, out int currentPacked) ||
                    node.packedAtEnqueue > currentPacked)
                    continue;

                actionClosed.Add(s);

                bool canStopHere = (occupancy == null) || occupancy.CanOccupy(s.pos, mover);

                // Record STOP cells (not origin) — at dequeue = confirmed optimal
                if (s.pos != origin && s.actionsUsed > 0 && canStopHere)
                {
                    int actions = s.actionsUsed;

                    if (!outMinActions.TryGetValue(s.pos, out int bestActions) || actions < bestActions)
                        outMinActions[s.pos] = actions;

                    // zoneBestState stores the best packed-cost stop state (for shortest path preview)
                    if (!zoneBestState.TryGetValue(s.pos, out var bestState))
                    {
                        zoneBestState[s.pos] = s;
                    }
                    else
                    {
                        int bestPacked = int.MaxValue;
                        if (actionCostSoFar.TryGetValue(bestState, out int bp)) bestPacked = bp;
                        if (currentPacked < bestPacked)
                            zoneBestState[s.pos] = s;
                    }
                }

                // "Reset edge": end current Stride early, start a new Stride (parity resets),
                // only if we can actually STOP here.
                if (s.actionsUsed > 0 &&
                    s.actionsUsed < maxActions &&
                    s.feetInStride > 0 &&
                    canStopHere)
                {
                    var resetState = new ActionState(
                        s.pos,
                        parity: false,
                        actionsUsed: (byte)(s.actionsUsed + 1),
                        feetInStride: 0);

                    int resetPacked = currentPacked + ActionPenalty;

                    if (!actionClosed.Contains(resetState))
                    {
                        if (!actionCostSoFar.TryGetValue(resetState, out int oldReset) || resetPacked < oldReset)
                        {
                            actionCostSoFar[resetState] = resetPacked;
                            actionCameFrom[resetState] = s;
                            actionOpenSet.Enqueue(
                                new ActionNode { state = resetState, packedAtEnqueue = resetPacked },
                                resetPacked);
                        }
                    }
                }

                // Expand movement steps
                grid.GetNeighbors(s.pos, profile.moveType, neighborBuffer);

                foreach (var n in neighborBuffer)
                {
                    if (!grid.TryGetCell(n.pos, out var targetCell)) continue;

                    if (occupancy != null && !occupancy.CanTraverse(n.pos, mover))
                        continue;

                    int stepCost = MovementCostEvaluator.GetStepCost(
                        targetCell, n,
                        diagonalParity: s.parity,
                        profile);

                    // If no Stride started yet, the first move step starts action #1
                    byte nextActionsUsed = (s.actionsUsed == 0) ? (byte)1 : s.actionsUsed;
                    if (nextActionsUsed > maxActions) continue;

                    int actionStartPenalty = (s.actionsUsed == 0) ? ActionPenalty : 0;

                    int nextFeetInStride = (s.actionsUsed == 0)
                        ? stepCost
                        : (s.feetInStride + stepCost);

                    if (nextFeetInStride > profile.speedFeet) continue;

                    bool nextParity = (n.type == NeighborType.Diagonal) ? !s.parity : s.parity;

                    var ns = new ActionState(n.pos, nextParity, nextActionsUsed, nextFeetInStride);

                    int nextPacked = currentPacked + actionStartPenalty + stepCost;

                    if (!actionClosed.Contains(ns))
                    {
                        if (!actionCostSoFar.TryGetValue(ns, out int oldPacked) || nextPacked < oldPacked)
                        {
                            actionCostSoFar[ns] = nextPacked;
                            actionCameFrom[ns] = s;
                            actionOpenSet.Enqueue(
                                new ActionNode { state = ns, packedAtEnqueue = nextPacked },
                                nextPacked);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Finds a path from->to within maxActions Strides.
        /// Does NOT preserve zoneBestState/cameFrom for preview (it clears them).
        /// Intended for execution (StrideAction).
        /// </summary>
        public bool FindPathByActions(
            GridData grid, Vector3Int from, Vector3Int to,
            MovementProfile profile, EntityHandle mover, OccupancyMap occupancy,
            int maxActions, List<Vector3Int> outPath,
            out int actionsCost, out int totalFeetCost)
        {
            outPath.Clear();
            actionsCost = 0;
            totalFeetCost = 0;

            maxActions = Mathf.Clamp(maxActions, 0, 3);

            if (from == to)
            {
                outPath.Add(from);
                return true;
            }

            if (maxActions <= 0) return false;

            if (!grid.IsCellPassable(from, profile.moveType) ||
                !grid.IsCellPassable(to, profile.moveType))
                return false;

            // Must be able to STOP on goal
            if (occupancy != null && !occupancy.CanOccupy(to, mover))
                return false;

            // Clears preview caches — this method is for execution, not preview
            actionOpenSet.Clear();
            actionClosed.Clear();
            actionCostSoFar.Clear();
            actionCameFrom.Clear();
            zoneBestState.Clear();

            var startState = new ActionState(from, parity: false, actionsUsed: 0, feetInStride: 0);
            int startPacked = PackCost(0, 0);
            actionCostSoFar[startState] = startPacked;
            actionOpenSet.Enqueue(new ActionNode { state = startState, packedAtEnqueue = startPacked }, startPacked);

            while (actionOpenSet.Count > 0)
            {
                var node = actionOpenSet.Dequeue();
                var s = node.state;

                if (actionClosed.Contains(s)) continue;

                if (!actionCostSoFar.TryGetValue(s, out int currentPacked) ||
                    node.packedAtEnqueue > currentPacked)
                    continue;

                // Goal reached (must have started at least 1 action)
                if (s.pos == to && s.actionsUsed > 0 && s.actionsUsed <= maxActions)
                {
                    actionsCost = s.actionsUsed;
                    totalFeetCost = UnpackFeet(currentPacked);
                    ReconstructActionPath(s, outPath);
                    return outPath.Count >= 2;
                }

                actionClosed.Add(s);

                bool canStopHere = (occupancy == null) || occupancy.CanOccupy(s.pos, mover);

                // Reset edge
                if (s.actionsUsed > 0 &&
                    s.actionsUsed < maxActions &&
                    s.feetInStride > 0 &&
                    canStopHere)
                {
                    var resetState = new ActionState(
                        s.pos,
                        parity: false,
                        actionsUsed: (byte)(s.actionsUsed + 1),
                        feetInStride: 0);

                    int resetPacked = currentPacked + ActionPenalty;

                    if (!actionClosed.Contains(resetState))
                    {
                        if (!actionCostSoFar.TryGetValue(resetState, out int oldReset) || resetPacked < oldReset)
                        {
                            actionCostSoFar[resetState] = resetPacked;
                            actionCameFrom[resetState] = s;
                            actionOpenSet.Enqueue(
                                new ActionNode { state = resetState, packedAtEnqueue = resetPacked },
                                resetPacked);
                        }
                    }
                }

                // Movement edges
                grid.GetNeighbors(s.pos, profile.moveType, neighborBuffer);

                foreach (var n in neighborBuffer)
                {
                    if (!grid.TryGetCell(n.pos, out var targetCell)) continue;

                    if (occupancy != null && !occupancy.CanTraverse(n.pos, mover))
                        continue;

                    int stepCost = MovementCostEvaluator.GetStepCost(
                        targetCell, n,
                        diagonalParity: s.parity,
                        profile);

                    byte nextActionsUsed = (s.actionsUsed == 0) ? (byte)1 : s.actionsUsed;
                    if (nextActionsUsed > maxActions) continue;

                    int actionStartPenalty = (s.actionsUsed == 0) ? ActionPenalty : 0;

                    int nextFeetInStride = (s.actionsUsed == 0)
                        ? stepCost
                        : (s.feetInStride + stepCost);

                    if (nextFeetInStride > profile.speedFeet) continue;

                    bool nextParity = (n.type == NeighborType.Diagonal) ? !s.parity : s.parity;

                    var ns = new ActionState(n.pos, nextParity, nextActionsUsed, nextFeetInStride);

                    int nextPacked = currentPacked + actionStartPenalty + stepCost;

                    if (!actionClosed.Contains(ns))
                    {
                        if (!actionCostSoFar.TryGetValue(ns, out int oldPacked) || nextPacked < oldPacked)
                        {
                            actionCostSoFar[ns] = nextPacked;
                            actionCameFrom[ns] = s;
                            actionOpenSet.Enqueue(
                                new ActionNode { state = ns, packedAtEnqueue = nextPacked },
                                nextPacked);
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// O(path_length) reconstruction for hover preview (no boundaries).
        /// Uses the cached results from the LAST GetMovementZoneByActions call.
        /// Does NOT run any search.
        /// </summary>
        public bool ReconstructPathFromZone(
            Vector3Int target, List<Vector3Int> outPath, out int outActionsCost)
        {
            return ReconstructPathFromZone(target, outPath, out outActionsCost, null);
        }

        /// <summary>
        /// O(path_length) reconstruction with Stride boundary indices.
        /// outActionBoundaries[k] = index in outPath where Stride (k+1) begins.
        /// E.g. [0, 6, 12] → Stride1 cells [0..5], Stride2 [6..11], Stride3 [12..].
        /// </summary>
        public bool ReconstructPathFromZone(
            Vector3Int target,
            List<Vector3Int> outPath,
            out int outActionsCost,
            List<int> outActionBoundaries)
        {
            outPath.Clear();
            outActionsCost = 0;
            outActionBoundaries?.Clear();

            if (!zoneBestState.TryGetValue(target, out var endState))
                return false;

            outActionsCost = endState.actionsUsed;

            ReconstructActionPath(endState, outPath, outActionBoundaries);
            return outPath.Count >= 2;
        }

        /// <summary>
        /// Shared reconstruction (no boundaries). Used by FindPathByActions.
        /// </summary>
        private void ReconstructActionPath(ActionState end, List<Vector3Int> outPath)
        {
            ReconstructActionPath(end, outPath, null);
        }

        /// <summary>
        /// Reconstruction with optional Stride boundary detection.
        /// Removes consecutive duplicates (reset edges) and records boundary indices.
        /// Uses reusable actionStateReconstructBuffer — zero GC.
        /// </summary>
        private void ReconstructActionPath(ActionState end, List<Vector3Int> outPath, List<int> outActionBoundaries)
        {
            // Reconstruct ActionState chain (end -> start), then reverse
            actionStateReconstructBuffer.Clear();

            var s = end;
            actionStateReconstructBuffer.Add(s);

            while (actionCameFrom.TryGetValue(s, out var prev))
            {
                s = prev;
                actionStateReconstructBuffer.Add(s);
            }

            actionStateReconstructBuffer.Reverse();

            outPath.Clear();
            outActionBoundaries?.Clear();
            outActionBoundaries?.Add(0); // Stride 1 starts at outPath[0]

            bool hasLast = false;
            Vector3Int lastPos = default;

            for (int i = 0; i < actionStateReconstructBuffer.Count; i++)
            {
                var pos = actionStateReconstructBuffer[i].pos;

                if (!hasLast)
                {
                    outPath.Add(pos);
                    lastPos = pos;
                    hasLast = true;
                    continue;
                }

                if (pos == lastPos)
                {
                    // Reset-edge duplicate (end Stride N / start Stride N+1).
                    // Boundary index = the NEXT unique cell index, not the shared cell.
                    if (outActionBoundaries != null)
                    {
                        int boundaryIndex = outPath.Count;
                        if (outActionBoundaries.Count == 0 || outActionBoundaries[outActionBoundaries.Count - 1] != boundaryIndex)
                            outActionBoundaries.Add(boundaryIndex);
                    }
                    continue;
                }

                outPath.Add(pos);
                lastPos = pos;
            }

            // Trim any boundary that points past end (reset at very end of path)
            if (outActionBoundaries != null)
            {
                for (int k = outActionBoundaries.Count - 1; k >= 0; k--)
                {
                    if (outActionBoundaries[k] < 0 || outActionBoundaries[k] >= outPath.Count)
                        outActionBoundaries.RemoveAt(k);
                }
            }
        }
    }
}
