using System.Collections.Generic;

namespace PF2e.Core
{
    /// <summary>
    /// Storage for all entities. Pure C#, no MonoBehaviour.
    /// Ids start at 1. Id 0 = EntityHandle.None.
    /// </summary>
    public class EntityRegistry
    {
        private readonly Dictionary<EntityHandle, EntityData> _entities
            = new Dictionary<EntityHandle, EntityData>();
        private int _nextId = 1;

        public EntityHandle Register(EntityData data)
        {
            var handle = new EntityHandle(_nextId++);
            data.Handle = handle;
            _entities[handle] = data;
            return handle;
        }

        public void Unregister(EntityHandle handle)
        {
            _entities.Remove(handle);
        }

        public EntityData Get(EntityHandle handle)
        {
            _entities.TryGetValue(handle, out var data);
            return data;
        }

        public bool Exists(EntityHandle handle)
        {
            return _entities.ContainsKey(handle);
        }

        public IEnumerable<EntityData> GetAll()
        {
            return _entities.Values;
        }

        public List<EntityData> GetByTeam(Team team)
        {
            var result = new List<EntityData>();
            foreach (var entity in _entities.Values)
                if (entity.Team == team)
                    result.Add(entity);
            return result;
        }

        public int Count => _entities.Count;
    }
}
