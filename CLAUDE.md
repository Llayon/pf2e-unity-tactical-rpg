# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PF2e tactical RPG (Solasta-style) built in Unity 6 (6000.3.2f1) with URP and New Input System. Turn-based combat with Pathfinder 2e rules: 3-action economy, MAP, diagonal parity, conditions, initiative.

## Build & Test Commands

This is a Unity project — there is no CLI build command. All compilation happens in the Unity Editor or CI.

### Running Tests

**Via Unity Editor:**
- Window > General > Test Runner
- EditMode and PlayMode tabs run separately

**Via CI (GitHub Actions):**
- Push/PR to `master` triggers `.github/workflows/unity-tests.yml`
- Runs EditMode and PlayMode in parallel via `game-ci/unity-test-runner@v4`
- Requires `UNITY_LICENSE` (or email/password/serial) secrets

**Via command line (local):**
```bash
# EditMode tests
"D:\Programms\Unity Hub\Editor\6000.3.2f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults test-results/editmode.xml -logFile -

# PlayMode tests
"D:\Programms\Unity Hub\Editor\6000.3.2f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults test-results/playmode.xml -logFile -
```

### Fixing Stale Compilation Errors

If Unity shows errors that don't match the code, delete `Library/ScriptAssemblies/*.dll` and force-refresh the asset database.

## Assembly Structure

| Assembly | Namespace | Location | Purpose |
|---|---|---|---|
| `PF2e` | `PF2e.*` | `Assets/Scripts/` | All runtime code |
| `PF2e.Tests.EditMode` | `PF2e.Tests` | `Assets/Tests/EditMode/` | NUnit edit-mode tests (Editor only) |
| `PF2e.Tests.PlayMode` | `PF2e.Tests` | `Assets/Tests/PlayMode/` | NUnit play-mode tests (scene integration) |

The main assembly references `Unity.InputSystem` and `Unity.TextMeshPro`. Tests reference `PF2e` + test runner assemblies.

## Architecture

### Module Boundaries (Namespace = Folder)

```
PF2e.Core       → Assets/Scripts/Core/       Pure rules, data, events (no MonoBehaviours for logic)
PF2e.Grid       → Assets/Scripts/Grid/       Spatial model, pathfinding, grid rendering
PF2e.TurnSystem → Assets/Scripts/TurnSystem/ Turn state machine, actions, input, AI
PF2e.Managers   → Assets/Scripts/Managers/   Scene orchestration (EntityManager)
PF2e.Presentation → Assets/Scripts/Presentation/ UI controllers, log forwarders, visuals
PF2e.Data       → Assets/Scripts/Data/       ScriptableObject config types
PF2e.Camera     → Assets/Scripts/Camera/     Orbit camera controller
```

### Data Flow

```
EntityData (pure C#) ← EntityRegistry ← EntityManager (MonoBehaviour, scene orchestration)
GridData (sparse Dict) ← GridManager ← GridInteraction (click/hover)
TurnManager (state machine) → PlayerActionExecutor → StrideAction/StrikeAction/StandAction
Events: TurnManager → TypedForwarder → typed event channel → LogForwarder → string → CombatEventBus
```

### Key Architectural Patterns

**Grid:** 3D sparse `Dictionary<Vector3Int, CellData>`. Chunked 16x16 rendering. Mutations only via `SetCell`/`SetEdge`/`AddVerticalLink` (version++ triggers dirty chunks).

**Pathfinding:** A* with `(position, diagonalParity)` state. Parity-aware Chebyshev heuristic (admissible + consistent). PF2e diagonal rule: 5ft/10ft alternating. Custom `MinBinaryHeap` because `PriorityQueue<T,P>` is NOT available in Unity 6 .NET.

**Entity system:** Pure C# `EntityData` stored in `EntityRegistry`. `EntityHandle` is a value-type ID (0 = None). `EntityView` MonoBehaviours are visual-only wrappers.

**Event architecture (hybrid):** Typed event channels (e.g., `StrikeResolvedEvent`, `ConditionChangedEvent`) for structured data. String-based `CombatEventBus` for the combat log. Forwarder adapters bridge typed events → log strings, keeping `TurnManager` free of presentation dependencies.

**Action execution:** `PlayerActionExecutor` owns `BeginActionExecution`/`CompleteActionWithCost` (atomic). Actions like `StrideAction` have NO direct `TurnManager` dependency. Dev watchdog (30s timeout) catches stuck actions.

**Dependency wiring:** Inspector-only `[SerializeField]` references. `GetComponent<>()` allowed only on same GameObject in `Reset`/`OnValidate`. **Never** `FindObjectOfType` / `FindAnyObjectByType`.

**Zero-alloc:** Caller-owned `List<T>` buffers for pathfinding/neighbors. Object pooling for UI elements. `MaterialPropertyBlock` lazy-init (never static field on MonoBehaviour).

## Do Not Break Contracts

- **CombatEventBus messages:** Publishers MUST NOT include actor name or ": " prefix. The consumer (`CombatLogController`) adds the actor name. Dev builds warn on violation.
- **Event subscriptions:** Always subscribe in `OnEnable`, unsubscribe in `OnDisable`. No exceptions.
- **OnEnable vs Awake ordering:** Don't fail-fast check properties initialized in another component's `Awake()` from your `OnEnable()`. Only validate inspector references in `OnEnable`; defer runtime state checks.
- **TurnManager action contract:** Always use `BeginActionExecution` + `CompleteActionWithCost` pair. Never bypass.
- **TurnManager combat-end:** Keep both `OnCombatEnded` (legacy) and `OnCombatEndedWithResult` (typed result). Both must fire.
- **StrideAction commits before animation:** Occupancy/entity position is committed immediately; `EntityMover` is visual-only movement.
- **AITurnController lock release:** Must release action locks on abort/timeout/disable to prevent `ExecutingAction` deadlocks.
- **EncounterFlowController:** Defaults to authored inspector references (`autoCreateRuntimeButtons=false`); runtime auto-create is fallback only.
- **EntityHandle.None:** `Id == 0` means invalid handle. Always check before use.
- **Layer consistency:** Grid/entity raycasts depend on `EntityView` objects sharing the Grid layer.
- **EffectiveAC in tests:** Set `Dexterity` + `Level` on EntityData (not `ArmorClass`), because `EffectiveAC` computes from the armor system. Example: Dex=16, Level=3 → BaseAC=18.
- **Pure rules in PF2e.Core:** No UI/MonoBehaviour logic. Add/extend typed bus events before adding UI-specific direct dependencies.

## Gameplay Controls

Primary: encounter flow buttons (`Start Encounter` / `End Encounter`). In-game: left-click cell/entity, `Space` end turn, `Esc` cancel targeting, `WASD/QE/Scroll` camera, `G` grid toggle, `PageUp/PageDown` floor. `C`/`X` are editor/dev fallback shortcuts.

## ScriptableObject Assets

Config assets live in `Assets/Data/`:
- `GridConfig_Default.asset` — grid visual/layout settings
- `CameraSettings_Default.asset` — camera orbit parameters
- `Data/Items/Weapons/` and `Data/Items/Armor/` — item definition SOs

## Scene

Single scene: `Assets/Scenes/SampleScene.unity`. Heavy inspector wiring between `TurnManager`, `EntityManager`, `GridManager`, `CombatEventBus`, and UI controllers.

## Living Documentation

`Docs/PROJECT_MEMORY.md` — canonical project status doc. Contains systems checklist, known issues, next tasks, and maintenance rule: update it in the same changeset when systems are added or behavior changes.
