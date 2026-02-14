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
    }
}
