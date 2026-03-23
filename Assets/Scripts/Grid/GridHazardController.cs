using System.Collections.Generic;
using PF2e.Core;
using PF2e.TurnSystem;
using UnityEngine;

namespace PF2e.Grid
{
    [RequireComponent(typeof(GridManager))]
    public sealed class GridHazardController : MonoBehaviour
    {
        private const float DefaultTelegraphYOffset = 0.035f;
        private const float DefaultTelegraphInset = 0.18f;

        [Header("Dependencies")]
        [SerializeField] private GridManager gridManager;

        [Header("Hazards")]
        [SerializeField] private List<GridHazardDefinition> hazards = new()
        {
            new GridHazardDefinition(
                "Spike Trap",
                new Vector3Int(4, 0, 2),
                entryDamage: 4,
                damageType: DamageType.Piercing,
                aiPressure: 180,
                telegraphColor: new Color(0.95f, 0.25f, 0.1f, 0.72f)),
            new GridHazardDefinition(
                "Burning Coals",
                new Vector3Int(4, 0, 5),
                entryDamage: 3,
                damageType: DamageType.Fire,
                aiPressure: 150,
                telegraphColor: new Color(1f, 0.45f, 0.08f, 0.66f))
        };

        [Header("Telegraph")]
        [SerializeField] private bool showTelegraphs = true;
        [SerializeField] private float telegraphYOffset = DefaultTelegraphYOffset;
        [SerializeField] private float telegraphInset = DefaultTelegraphInset;

        private readonly Dictionary<Vector3Int, GridHazardInfo> hazardLookup = new();
        private readonly List<GameObject> telegraphObjects = new();
        private readonly List<Material> telegraphRuntimeMaterials = new();
        private Material telegraphMaterial;
        private Mesh telegraphMesh;

        public IReadOnlyList<GridHazardDefinition> Hazards => hazards;

        private void Awake()
        {
            if (gridManager == null)
                gridManager = GetComponent<GridManager>();
        }

        private void Start()
        {
            ApplyHazardsNow();
        }

        public bool TryGetHazard(Vector3Int cell, out GridHazardInfo hazard)
        {
            return hazardLookup.TryGetValue(cell, out hazard);
        }

        public void ApplyHazardsNow()
        {
            if (gridManager == null || gridManager.Data == null)
                return;

            hazardLookup.Clear();

            for (int i = 0; i < hazards.Count; i++)
            {
                var definition = hazards[i];
                if (!TryBuildInfo(definition, out var info))
                    continue;

                hazardLookup[info.cell] = info;
                ApplyHazardTerrain(info.cell);
            }

            RebuildTelegraphs();
            gridManager.RaiseGridChanged();
        }

        private bool TryBuildInfo(in GridHazardDefinition definition, out GridHazardInfo info)
        {
            info = default;

            string displayName = string.IsNullOrWhiteSpace(definition.displayName)
                ? "Hazard"
                : definition.displayName.Trim();
            int entryDamage = Mathf.Max(1, definition.entryDamage);
            int aiPressure = definition.aiPressure > 0
                ? definition.aiPressure
                : HazardousTerrainRules.DefaultHazardousTerrainPressure;
            int saveDc = definition.saveDc > 0 ? definition.saveDc : 15;
            int persistentDamage = definition.persistentDamage;
            int forcedMoveCells = definition.forcedMoveCells;
            int forcedMoveElevationPerCell = definition.forcedMoveElevationPerCell;
            DamageType damageType = definition.damageType;
            Color telegraphColor = definition.telegraphColor.a > 0f
                ? definition.telegraphColor
                : new Color(1f, 0.32f, 0.12f, 0.7f);

            switch (definition.effectKind)
            {
                case HazardEffectKind.ProneOnEntry:
                    entryDamage = 0;
                    break;
                case HazardEffectKind.PersistentFireOnEntry:
                    entryDamage = Mathf.Max(1, definition.entryDamage);
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    break;
                case HazardEffectKind.BasicSaveDamage:
                case HazardEffectKind.DamageAndProneOnFailure:
                case HazardEffectKind.PersistentFireOnFailedSave:
                    entryDamage = Mathf.Max(1, definition.entryDamage);
                    break;
                case HazardEffectKind.BasicSaveDamageAndPersistentFireOnFailure:
                    entryDamage = Mathf.Max(1, definition.entryDamage);
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    break;
                case HazardEffectKind.ProneAndPersistentFireOnFailedSave:
                    entryDamage = 0;
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    break;
                case HazardEffectKind.PushOnFailedSave:
                    entryDamage = 0;
                    persistentDamage = 0;
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    break;
                case HazardEffectKind.BasicSaveDamageAndPushOnFailedSave:
                    entryDamage = Mathf.Max(1, definition.entryDamage);
                    persistentDamage = 0;
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    break;
                case HazardEffectKind.BasicSaveDamageAndProneAndPushOnFailedSave:
                    entryDamage = Mathf.Max(1, definition.entryDamage);
                    persistentDamage = 0;
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    break;
                case HazardEffectKind.ProneAndPushAndPersistentFireOnFailedSave:
                    entryDamage = 0;
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    break;
                case HazardEffectKind.PullOnFailedSave:
                    entryDamage = 0;
                    persistentDamage = 0;
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    break;
                case HazardEffectKind.BasicSaveDamageAndPullOnFailedSave:
                    entryDamage = Mathf.Max(1, definition.entryDamage);
                    persistentDamage = 0;
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    break;
                case HazardEffectKind.BasicSaveDamageAndProneAndPullOnFailedSave:
                    entryDamage = Mathf.Max(1, definition.entryDamage);
                    persistentDamage = 0;
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    break;
                case HazardEffectKind.ProneAndPullOnFailedSave:
                    entryDamage = 0;
                    persistentDamage = 0;
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    break;
                case HazardEffectKind.PullAndPersistentFireOnFailedSave:
                    entryDamage = 0;
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    break;
                case HazardEffectKind.ProneAndPullAndPersistentFireOnFailedSave:
                    entryDamage = 0;
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    break;
                case HazardEffectKind.PersistentAcidOnFailedSave:
                    entryDamage = 0;
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    damageType = DamageType.Acid;
                    break;
                case HazardEffectKind.BasicSaveDamageAndPersistentAcidOnFailure:
                    entryDamage = Mathf.Max(1, definition.entryDamage);
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    damageType = DamageType.Acid;
                    break;
                case HazardEffectKind.ProneAndPersistentAcidOnFailedSave:
                    entryDamage = 0;
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    damageType = DamageType.Acid;
                    break;
                case HazardEffectKind.PullAndPersistentAcidOnFailedSave:
                    entryDamage = 0;
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    damageType = DamageType.Acid;
                    break;
                case HazardEffectKind.PushAndPersistentAcidOnFailedSave:
                    entryDamage = 0;
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    damageType = DamageType.Acid;
                    break;
                case HazardEffectKind.BasicSaveDamageAndPushAndPersistentAcidOnFailedSave:
                    entryDamage = Mathf.Max(1, definition.entryDamage);
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    damageType = DamageType.Acid;
                    break;
                case HazardEffectKind.BasicSaveDamageAndPullAndPersistentAcidOnFailedSave:
                    entryDamage = Mathf.Max(1, definition.entryDamage);
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    damageType = DamageType.Acid;
                    break;
                case HazardEffectKind.ProneAndPushAndPersistentAcidOnFailedSave:
                    entryDamage = 0;
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    damageType = DamageType.Acid;
                    break;
                case HazardEffectKind.ProneAndPullAndPersistentAcidOnFailedSave:
                    entryDamage = 0;
                    persistentDamage = Mathf.Max(1, definition.persistentDamage > 0 ? definition.persistentDamage : definition.entryDamage);
                    forcedMoveCells = Mathf.Max(1, definition.forcedMoveCells > 0 ? definition.forcedMoveCells : 1);
                    forcedMoveElevationPerCell = Mathf.Clamp(definition.forcedMoveElevationPerCell, -1, 1);
                    damageType = DamageType.Acid;
                    break;
                default:
                    entryDamage = Mathf.Max(1, definition.entryDamage);
                    break;
            }

            info = new GridHazardInfo(
                displayName,
                definition.cell,
                definition.effectKind,
                entryDamage,
                persistentDamage,
                forcedMoveCells,
                damageType,
                definition.saveType,
                saveDc,
                aiPressure,
                telegraphColor,
                forcedMoveElevationPerCell);
            return info.IsValid;
        }

        private void ApplyHazardTerrain(Vector3Int cell)
        {
            if (gridManager == null || gridManager.Data == null)
                return;
            if (!gridManager.Data.TryGetCell(cell, out var cellData))
                return;
            if (cellData.terrain == CellTerrain.Impassable)
                return;
            if (cellData.terrain == CellTerrain.Hazardous)
                return;

            cellData.terrain = CellTerrain.Hazardous;
            gridManager.Data.SetCell(cell, cellData);
        }

        private void RebuildTelegraphs()
        {
            ClearTelegraphs();

            if (!showTelegraphs || gridManager == null || gridManager.Data == null)
                return;

            float cellSize = gridManager.Data.CellWorldSize;
            float insetScale = Mathf.Max(0.1f, cellSize - Mathf.Max(0f, telegraphInset));

            foreach (var pair in hazardLookup)
            {
                if (!gridManager.Data.HasCell(pair.Key))
                    continue;

                var telegraph = new GameObject(
                    $"HazardTelegraph_{pair.Value.displayName}_{pair.Key.x}_{pair.Key.y}_{pair.Key.z}",
                    typeof(MeshFilter),
                    typeof(MeshRenderer));
                telegraph.name = $"HazardTelegraph_{pair.Value.displayName}_{pair.Key.x}_{pair.Key.y}_{pair.Key.z}";
                telegraph.layer = gameObject.layer;
                telegraph.transform.SetParent(transform, false);
                telegraph.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                telegraph.transform.localScale = new Vector3(insetScale, insetScale, 1f);

                Vector3 worldCenter = gridManager.Data.CellToWorld(pair.Key);
                worldCenter.y = pair.Key.y * gridManager.Data.HeightStepWorld + telegraphYOffset;
                telegraph.transform.position = worldCenter;

                var meshFilter = telegraph.GetComponent<MeshFilter>();
                var renderer = telegraph.GetComponent<MeshRenderer>();
                meshFilter.sharedMesh = GetOrCreateTelegraphMesh();
                var runtimeMaterial = new Material(GetOrCreateTelegraphMaterial())
                {
                    name = $"GridHazardTelegraph_{pair.Value.displayName}_Mat"
                };
                ApplyTelegraphColor(runtimeMaterial, pair.Value.telegraphColor);
                renderer.sharedMaterial = runtimeMaterial;
                telegraphRuntimeMaterials.Add(runtimeMaterial);

                telegraphObjects.Add(telegraph);
            }
        }

        private Material GetOrCreateTelegraphMaterial()
        {
            if (telegraphMaterial != null)
                return telegraphMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            telegraphMaterial = new Material(shader)
            {
                name = "GridHazardTelegraph_Mat"
            };
            telegraphMaterial.SetFloat("_Surface", 1f);
            telegraphMaterial.SetFloat("_Blend", 0f);
            telegraphMaterial.SetFloat("_ZWrite", 0f);
            telegraphMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            telegraphMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            telegraphMaterial.SetColor("_BaseColor", Color.white);
            telegraphMaterial.renderQueue = 3001;
            telegraphMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            return telegraphMaterial;
        }

        private static void ApplyTelegraphColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        private Mesh GetOrCreateTelegraphMesh()
        {
            if (telegraphMesh != null)
                return telegraphMesh;

            telegraphMesh = new Mesh
            {
                name = "GridHazardTelegraph_Mesh"
            };
            telegraphMesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            });
            telegraphMesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            telegraphMesh.SetUVs(0, new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            });
            telegraphMesh.RecalculateBounds();
            telegraphMesh.RecalculateNormals();
            return telegraphMesh;
        }

        private void ClearTelegraphs()
        {
            for (int i = 0; i < telegraphObjects.Count; i++)
            {
                var go = telegraphObjects[i];
                if (go == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(go);
                else
                    DestroyImmediate(go);
            }

            telegraphObjects.Clear();

            for (int i = 0; i < telegraphRuntimeMaterials.Count; i++)
            {
                var mat = telegraphRuntimeMaterials[i];
                if (mat == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(mat);
                else
                    DestroyImmediate(mat);
            }

            telegraphRuntimeMaterials.Clear();
        }

        private void OnDestroy()
        {
            ClearTelegraphs();

            if (telegraphMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(telegraphMaterial);
                else
                    DestroyImmediate(telegraphMaterial);
            }

            if (telegraphMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(telegraphMesh);
                else
                    DestroyImmediate(telegraphMesh);
            }
        }
    }
}
