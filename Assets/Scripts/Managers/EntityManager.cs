using System;
using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Presentation.Entity;

namespace PF2e.Managers
{
    public class EntityManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;

        [Header("Test Setup")]
        [SerializeField] private bool spawnTestEntities = true;

        [Header("Entity Visuals")]
        [SerializeField] private float entityHeight = 1.6f;
        [SerializeField] private float entityRadius = 0.35f;

        [SerializeField] private Color playerColor  = new Color(0.2f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color enemyColor   = new Color(0.8f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color neutralColor = new Color(0.85f, 0.85f, 0.2f, 1f);

        public EntityRegistry Registry    { get; private set; }
        public OccupancyMap   Occupancy   { get; private set; }
        public GridPathfinding Pathfinding { get; private set; }

        private readonly Dictionary<EntityHandle, EntityView> views
            = new Dictionary<EntityHandle, EntityView>();

        // ─── Selection ───
        public EntityHandle SelectedEntity { get; private set; }

        // ─── Events ───
        public event Action<EntityHandle> OnEntitySelected;
        public event Action OnEntityDeselected;

        private void Awake()
        {
            Registry   = new EntityRegistry();
            Occupancy  = new OccupancyMap(Registry);
            Pathfinding = new GridPathfinding();

            if (gridManager == null)
            {
                Debug.LogError("[EntityManager] GridManager is not assigned in Inspector. Disabling EntityManager.", this);
                enabled = false;
                return;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (gridManager == null)
                Debug.LogError("[EntityManager] Missing reference: GridManager. Assign it in Inspector.", this);
        }
#endif

        private void Start()
        {
            if (!spawnTestEntities) return;
            if (gridManager == null || gridManager.Data == null) return;

            SpawnTestEntities();
        }

        public Color GetTeamColor(Team team)
        {
            switch (team)
            {
                case Team.Player:  return playerColor;
                case Team.Enemy:   return enemyColor;
                case Team.Neutral: return neutralColor;
                default:           return Color.white;
            }
        }

        public Vector3 GetEntityWorldPosition(Vector3Int gridPos)
        {
            var cellCenter = gridManager.Data.CellToWorld(gridPos);
            return new Vector3(cellCenter.x, cellCenter.y + entityHeight * 0.5f, cellCenter.z);
        }

        public EntityHandle CreateEntity(EntityData data, Vector3Int gridPosition)
        {
            var handle = Registry.Register(data);
            data.GridPosition = gridPosition;

            if (!Occupancy.Place(handle, gridPosition, data.SizeCells))
            {
                Debug.LogWarning($"[EntityManager] Cannot place {data.Name} at {gridPosition} — occupied!");
                Registry.Unregister(handle);
                return EntityHandle.None;
            }

            var view = CreateView(handle, data);
            views[handle] = view;
            return handle;
        }

        private EntityView CreateView(EntityHandle handle, EntityData data)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Entity_{data.Name}_{handle.Id}";
            go.transform.SetParent(transform, false);

            // Set entity to same layer as grid so GridInteraction.gridLayerMask sees it
            if (gridManager != null)
                go.layer = gridManager.gameObject.layer;

            go.transform.position = GetEntityWorldPosition(data.GridPosition);

            float scaleY  = entityHeight / 2f;
            float scaleXZ = entityRadius * 2f;
            go.transform.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);

            var view = go.AddComponent<EntityView>();
            view.Initialize(handle, GetTeamColor(data.Team));
            return view;
        }

        private void SpawnTestEntities()
        {
            CreateEntity(new EntityData
            {
                Name = "Fighter", Team = Team.Player, Size = CreatureSize.Medium,
                Level = 1, MaxHP = 20, CurrentHP = 20, ArmorClass = 18, Speed = 25,
                Strength = 16, Dexterity = 12, Constitution = 14, Intelligence = 10, Wisdom = 12, Charisma = 8
            }, new Vector3Int(1, 0, 1));

            CreateEntity(new EntityData
            {
                Name = "Wizard", Team = Team.Player, Size = CreatureSize.Medium,
                Level = 1, MaxHP = 14, CurrentHP = 14, ArmorClass = 13, Speed = 25,
                Strength = 8, Dexterity = 14, Constitution = 12, Intelligence = 18, Wisdom = 12, Charisma = 10
            }, new Vector3Int(1, 0, 5));

            CreateEntity(new EntityData
            {
                Name = "Goblin_1", Team = Team.Enemy, Size = CreatureSize.Small,
                Level = -1, MaxHP = 6, CurrentHP = 6, ArmorClass = 16, Speed = 25,
                Strength = 12, Dexterity = 16, Constitution = 10, Intelligence = 10, Wisdom = 10, Charisma = 8
            }, new Vector3Int(6, 0, 2));

            CreateEntity(new EntityData
            {
                Name = "Goblin_2", Team = Team.Enemy, Size = CreatureSize.Small,
                Level = -1, MaxHP = 6, CurrentHP = 6, ArmorClass = 16, Speed = 25,
                Strength = 12, Dexterity = 16, Constitution = 10, Intelligence = 10, Wisdom = 10, Charisma = 8
            }, new Vector3Int(6, 0, 5));

            Debug.Log($"[EntityManager] Spawned {Registry.Count} test entities");
        }

        // ─── Selection ───

        public void SelectEntity(EntityHandle handle)
        {
            if (!handle.IsValid || !Registry.Exists(handle)) return;

            // Deselect previous
            if (SelectedEntity.IsValid && SelectedEntity != handle)
            {
                if (views.TryGetValue(SelectedEntity, out var prevView))
                    prevView.SetSelected(false);
            }

            SelectedEntity = handle;

            if (views.TryGetValue(handle, out var view))
                view.SetSelected(true);

            OnEntitySelected?.Invoke(handle);
        }

        public void DeselectEntity()
        {
            if (SelectedEntity.IsValid)
            {
                if (views.TryGetValue(SelectedEntity, out var view))
                    view.SetSelected(false);
            }

            SelectedEntity = EntityHandle.None;
            OnEntityDeselected?.Invoke();
        }

        // ─── Queries ───

        public EntityHandle GetEntityAt(Vector3Int gridPos)
        {
            return Occupancy.GetOccupant(gridPos);
        }

        public EntityView GetView(EntityHandle handle)
        {
            views.TryGetValue(handle, out var view);
            return view;
        }

        private void OnDestroy()
        {
            foreach (var kvp in views)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                    Destroy(kvp.Value.gameObject);
            }
            views.Clear();
        }
    }
}
