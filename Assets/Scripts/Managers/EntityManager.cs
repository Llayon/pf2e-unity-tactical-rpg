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
        [SerializeField] private CombatEventBus eventBus;

        [Header("Test Setup")]
        [SerializeField] private bool spawnTestEntities = true;

        [Header("Test Item Definitions")]
        [SerializeField] private WeaponDefinition fighterWeaponDef;
        [SerializeField] private ArmorDefinition fighterArmorDef;
        [SerializeField] private ShieldDefinition fighterShieldDef;
        [SerializeField] private WeaponDefinition wizardWeaponDef;
        [SerializeField] private ArmorDefinition wizardArmorDef;
        [SerializeField] private WeaponDefinition goblinWeaponDef;
        [SerializeField] private ArmorDefinition goblinArmorDef;

        [Header("Reaction Preferences (Test Setup)")]
        [SerializeField] private ReactionPreference fighterShieldBlockPreference = ReactionPreference.AutoBlock;

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

        private readonly HashSet<EntityHandle> defeatedPublished = new HashSet<EntityHandle>();

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

            if (eventBus == null)
                Debug.LogWarning("[EntityManager] CombatEventBus not assigned. Death typed events won't fire.", this);

            if (spawnTestEntities)
            {
                if (fighterWeaponDef == null || fighterArmorDef == null)
                    Debug.LogWarning("[EntityManager] Fighter weapon/armor definitions not assigned.", this);
                if (fighterShieldDef == null)
                    Debug.LogWarning("[EntityManager] Fighter shield definition not assigned (Shield Block demo disabled).", this);
                if (wizardWeaponDef == null || wizardArmorDef == null)
                    Debug.LogWarning("[EntityManager] Wizard weapon/armor definitions not assigned.", this);
                if (goblinWeaponDef == null || goblinArmorDef == null)
                    Debug.LogWarning("[EntityManager] Goblin weapon/armor definitions not assigned.", this);
            }
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
            // Fighter with +1 Striking weapon and armor (from inspector defs)
            var fighter = new EntityData
            {
                Name = "Fighter", Team = Team.Player, Size = CreatureSize.Medium,
                Level = 1, MaxHP = 20, CurrentHP = 20, ArmorClass = 18, Speed = 25,
                Strength = 16, Dexterity = 12, Constitution = 14, Intelligence = 10, Wisdom = 12, Charisma = 8
            };
            fighter.SimpleWeaponProf = ProficiencyRank.Trained;
            fighter.MartialWeaponProf = ProficiencyRank.Trained;
            fighter.AdvancedWeaponProf = ProficiencyRank.Untrained;

            fighter.UnarmoredProf = ProficiencyRank.Trained;
            fighter.LightArmorProf = ProficiencyRank.Trained;
            fighter.MediumArmorProf = ProficiencyRank.Trained;
            fighter.HeavyArmorProf = ProficiencyRank.Untrained;

            fighter.EquippedWeapon = new WeaponInstance
            {
                def = fighterWeaponDef,
                potencyBonus = 1,
                strikingRank = StrikingRuneRank.Striking
            };
            fighter.EquippedArmor = new ArmorInstance
            {
                def = fighterArmorDef,
                potencyBonus = 0,
                resilient = ResilientRuneRank.None,
                broken = false
            };
            if (fighterShieldDef != null)
            {
                fighter.EquippedShield = ShieldInstance.CreateEquipped(fighterShieldDef);
            }
            fighter.ShieldBlockPreference = fighterShieldBlockPreference;
            CreateEntity(fighter, new Vector3Int(1, 0, 1));

            // Wizard with dagger and armor (from inspector defs)
            var wizard = new EntityData
            {
                Name = "Wizard", Team = Team.Player, Size = CreatureSize.Medium,
                Level = 1, MaxHP = 14, CurrentHP = 14, ArmorClass = 13, Speed = 25,
                Strength = 8, Dexterity = 14, Constitution = 12, Intelligence = 18, Wisdom = 12, Charisma = 10
            };
            wizard.SimpleWeaponProf = ProficiencyRank.Trained;
            wizard.MartialWeaponProf = ProficiencyRank.Untrained;
            wizard.AdvancedWeaponProf = ProficiencyRank.Untrained;

            wizard.UnarmoredProf = ProficiencyRank.Trained;
            wizard.LightArmorProf = ProficiencyRank.Untrained;
            wizard.MediumArmorProf = ProficiencyRank.Untrained;
            wizard.HeavyArmorProf = ProficiencyRank.Untrained;

            wizard.EquippedWeapon = new WeaponInstance
            {
                def = wizardWeaponDef,
                potencyBonus = 0,
                strikingRank = StrikingRuneRank.None
            };
            wizard.EquippedArmor = new ArmorInstance
            {
                def = wizardArmorDef,
                potencyBonus = 0,
                resilient = ResilientRuneRank.None,
                broken = false
            };
            CreateEntity(wizard, new Vector3Int(1, 0, 5));

            // Goblin_1 (from inspector defs)
            var goblin1 = new EntityData
            {
                Name = "Goblin_1", Team = Team.Enemy, Size = CreatureSize.Small,
                Level = -1, MaxHP = 6, CurrentHP = 6, ArmorClass = 16, Speed = 25,
                Strength = 12, Dexterity = 16, Constitution = 10, Intelligence = 10, Wisdom = 10, Charisma = 8
            };
            goblin1.MartialWeaponProf = ProficiencyRank.Trained;
            goblin1.SimpleWeaponProf = ProficiencyRank.Trained;
            goblin1.AdvancedWeaponProf = ProficiencyRank.Untrained;

            goblin1.UnarmoredProf = ProficiencyRank.Trained;
            goblin1.LightArmorProf = ProficiencyRank.Trained;
            goblin1.MediumArmorProf = ProficiencyRank.Untrained;
            goblin1.HeavyArmorProf = ProficiencyRank.Untrained;

            goblin1.EquippedWeapon = new WeaponInstance
            {
                def = goblinWeaponDef,
                potencyBonus = 0,
                strikingRank = StrikingRuneRank.None
            };
            goblin1.EquippedArmor = new ArmorInstance
            {
                def = goblinArmorDef,
                potencyBonus = 0,
                resilient = ResilientRuneRank.None,
                broken = false
            };
            CreateEntity(goblin1, new Vector3Int(6, 0, 2));

            // Goblin_2 (from inspector defs)
            var goblin2 = new EntityData
            {
                Name = "Goblin_2", Team = Team.Enemy, Size = CreatureSize.Small,
                Level = -1, MaxHP = 6, CurrentHP = 6, ArmorClass = 16, Speed = 25,
                Strength = 12, Dexterity = 16, Constitution = 10, Intelligence = 10, Wisdom = 10, Charisma = 8
            };
            goblin2.MartialWeaponProf = ProficiencyRank.Trained;
            goblin2.SimpleWeaponProf = ProficiencyRank.Trained;
            goblin2.AdvancedWeaponProf = ProficiencyRank.Untrained;

            goblin2.UnarmoredProf = ProficiencyRank.Trained;
            goblin2.LightArmorProf = ProficiencyRank.Trained;
            goblin2.MediumArmorProf = ProficiencyRank.Untrained;
            goblin2.HeavyArmorProf = ProficiencyRank.Untrained;

            goblin2.EquippedWeapon = new WeaponInstance
            {
                def = goblinWeaponDef,
                potencyBonus = 0,
                strikingRank = StrikingRuneRank.None
            };
            goblin2.EquippedArmor = new ArmorInstance
            {
                def = goblinArmorDef,
                potencyBonus = 0,
                resilient = ResilientRuneRank.None,
                broken = false
            };
            CreateEntity(goblin2, new Vector3Int(6, 0, 5));

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

        /// <summary>
        /// Immediate grid snap movement for forced movement and other non-animated paths.
        /// Updates Occupancy, EntityData.GridPosition, and snaps the view if present.
        /// Returns false if destination is invalid/blocked.
        /// </summary>
        public bool TryMoveEntityImmediate(EntityHandle handle, Vector3Int destination)
        {
            if (!handle.IsValid) return false;
            if (Registry == null || Occupancy == null || gridManager == null || gridManager.Data == null) return false;

            var data = Registry.Get(handle);
            if (data == null || !data.IsAlive) return false;

            // Forced movement in current slice only supports walkable grid cells.
            var footprint = OccupancyMap.GetFootprint(destination, data.SizeCells);
            for (int i = 0; i < footprint.Count; i++)
            {
                if (!gridManager.Data.IsCellPassable(footprint[i], MovementType.Walk))
                    return false;
            }

            if (!Occupancy.Move(handle, destination, data.SizeCells))
                return false;

            data.GridPosition = destination;

            var view = GetView(handle);
            if (view != null && view.gameObject != null)
                view.transform.position = GetEntityWorldPosition(destination);

            return true;
        }

        // ─── Death Handling ───

public void HandleDeath(EntityHandle handle)
        {
            if (!handle.IsValid) return;

            // Guard: prevent double-processing
            if (!defeatedPublished.Add(handle)) return;

            // Publish typed event before visual/state changes
            if (eventBus != null)
                eventBus.PublishEntityDefeated(handle);

            if (SelectedEntity == handle)
                DeselectEntity();

            if (Occupancy != null)
                Occupancy.Remove(handle);

            var view = GetView(handle);
            if (view != null && view.gameObject != null)
                view.gameObject.SetActive(false);
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
