using System.Collections.Generic;
using UnityEngine;

namespace PF2e.Core
{
    /// <summary>
    /// Tracks which entities occupy which grid cells.
    /// PF2e rules:
    /// - CanTraverse: ally = pass through, enemy = blocked, neutral = pass
    /// - CanOccupy: only empty cells or own cells (can't stop on ally)
    /// </summary>
    public class OccupancyMap
    {
        private readonly Dictionary<Vector3Int, EntityHandle> _occupied
            = new Dictionary<Vector3Int, EntityHandle>();

        private readonly Dictionary<EntityHandle, List<Vector3Int>> _entityCells
            = new Dictionary<EntityHandle, List<Vector3Int>>();

        private readonly EntityRegistry _registry;

        public OccupancyMap(EntityRegistry registry)
        {
            _registry = registry;
        }

        public bool Place(EntityHandle entity, Vector3Int anchor, int sizeCells = 1)
        {
            var cells = GetFootprint(anchor, sizeCells);
            foreach (var cell in cells)
            {
                if (_occupied.TryGetValue(cell, out var existing) && existing != entity)
                    return false;
            }

            if (_entityCells.ContainsKey(entity))
                Remove(entity);

            _entityCells[entity] = cells;
            foreach (var cell in cells)
                _occupied[cell] = entity;

            return true;
        }

        public void Remove(EntityHandle entity)
        {
            if (!_entityCells.TryGetValue(entity, out var cells))
                return;

            foreach (var cell in cells)
            {
                if (_occupied.TryGetValue(cell, out var occupant) && occupant == entity)
                    _occupied.Remove(cell);
            }
            _entityCells.Remove(entity);
        }

        public bool Move(EntityHandle entity, Vector3Int newAnchor, int sizeCells = 1)
        {
            List<Vector3Int> oldCells = null;
            if (_entityCells.TryGetValue(entity, out var existing))
                oldCells = new List<Vector3Int>(existing);

            Remove(entity);

            if (Place(entity, newAnchor, sizeCells))
                return true;

            if (oldCells != null)
            {
                _entityCells[entity] = oldCells;
                foreach (var cell in oldCells)
                    _occupied[cell] = entity;
            }
            return false;
        }

        public bool IsOccupied(Vector3Int cell) => _occupied.ContainsKey(cell);

        public EntityHandle GetOccupant(Vector3Int cell)
        {
            _occupied.TryGetValue(cell, out var handle);
            return handle;
        }

        public bool CanTraverse(Vector3Int cell, EntityHandle mover)
        {
            if (!_occupied.TryGetValue(cell, out var occupant))
                return true;

            if (occupant == mover)
                return true;

            var moverData = _registry.Get(mover);
            var occupantData = _registry.Get(occupant);

            if (moverData == null || occupantData == null)
                return true;

            if (moverData.Team == occupantData.Team)
                return true;

            if (occupantData.Team == Team.Neutral)
                return true;

            return false;
        }

        public bool CanOccupy(Vector3Int cell, EntityHandle mover)
        {
            if (!_occupied.TryGetValue(cell, out var occupant))
                return true;

            return occupant == mover;
        }

        public bool CanOccupyFootprint(Vector3Int anchor, int sizeCells, EntityHandle mover)
        {
            var cells = GetFootprint(anchor, sizeCells);
            foreach (var cell in cells)
                if (!CanOccupy(cell, mover))
                    return false;
            return true;
        }

        public List<Vector3Int> GetOccupiedCells(EntityHandle entity)
        {
            if (_entityCells.TryGetValue(entity, out var cells))
                return new List<Vector3Int>(cells);
            return new List<Vector3Int>();
        }

        public static List<Vector3Int> GetFootprint(Vector3Int anchor, int sizeCells)
        {
            var result = new List<Vector3Int>(sizeCells * sizeCells);
            for (int x = 0; x < sizeCells; x++)
                for (int z = 0; z < sizeCells; z++)
                    result.Add(new Vector3Int(anchor.x + x, anchor.y, anchor.z + z));
            return result;
        }

        public int OccupiedCellCount => _occupied.Count;
    }
}
