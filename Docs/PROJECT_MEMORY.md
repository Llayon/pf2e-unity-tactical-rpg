# Project Memory
Last updated: 2026-02-19

## Vision
Build a small, playable, turn-based tactical PF2e combat slice in Unity where one player-controlled party can move on a grid, spend 3 actions, strike enemies, and finish encounters with clear visual feedback. Prioritize correctness of core turn/action/combat flow, maintainable architecture, and incremental delivery over full PF2e coverage.

## Vertical Slice Definition
- Single scene (`Assets/Scenes/SampleScene.unity`) with one encounter.
- Player can: start combat, move (Stride), Strike, Stand, end turn.
- Enemy side takes turns via simple melee AI (stand if prone, stride toward nearest player, strike in range).
- Combat presents: turn HUD, initiative bar, combat log, floating damage.
- Basic PF2e rules included: 3-action economy, MAP, basic melee strike check, damage roll, simple conditions.

## Architecture Snapshot
### A) Current Folder Map + Responsibilities
- `Assets/Scripts/Core`: pure rules/data structures (entities, conditions, attack/damage math, occupancy, item models, event contracts).
- `Assets/Scripts/Grid`: grid data, pathfinding, rendering, floor controls, click/hover interaction.
- `Assets/Scripts/TurnSystem`: turn state machine, input routing, action execution (`StrideAction`, `StrikeAction`, `StandAction`), targeting, enemy AI orchestration (`AITurnController`) and pure AI decisions (`SimpleMeleeAIDecision`).
- `Assets/Scripts/Managers`: `EntityManager` scene orchestration, spawning test entities, view management.
- `Assets/Scripts/Presentation`: UI/controllers/log forwarders, initiative/floating damage visuals.
- `Assets/Scripts/Data`: ScriptableObject configs (`GridConfig`).
- `Assets/Tests/EditMode`: NUnit EditMode coverage for grid/pathfinding/occupancy/turn primitives.
- Tooling/packages: URP, Input System, Unity Test Framework, `com.coplaydev.unity-mcp`; no Addressables package.

### B) Gameplay Loop Status (Current)
- Scene boots to `SampleScene`; test grid and test entities spawn from inspector-wired managers.
- Controls currently in code: `C` start combat, `X` end combat, left-click cell/entity, `Space` end turn, `Esc` cancel targeting, `WASD/QE/Scroll` camera, `G` grid toggle, `PageUp/PageDown` floor.
- Turn flow works: initiative roll, per-entity turns, 3 actions, action spending, condition end-turn ticks.
- Movement works: occupancy-aware, multi-stride pathing, 5/10 diagonal parity, movement zone/path preview, animated movement.
- Combat works at MVP level: melee strike resolution, MAP increment, damage apply, defeat hide + events.
- Enemy turns execute simple AI behavior; combat no longer auto-skips enemy turns.
- Victory/defeat ends combat immediately when one side (`Player` or `Enemy`) is wiped.

### C) Key Dependencies and Risks
- High scene wiring coupling: `CombatController`, `TacticalGrid`, `EntityManager`, `CombatEventBus` must all be correctly referenced.
- Runtime dependency chain is tight (`TurnManager -> EntityManager -> GridManager`; input and visualizers depend on both).
- Hybrid event architecture (typed events + string log forwarders) can drift if contracts are broken.
- Combat start is debug-key driven (`CombatStarter`), not productized flow.
- `EntityManager` mixes runtime orchestration with test spawning data responsibilities.

### D) Missing Foundations for Vertical Slice
- Encounter win/lose state presentation and restart flow.
- Explicit action bar/intent UI (targeting feedback still minimal).
- Broader automated verification (PlayMode/integration tests).

## Current Systems Checklist
| System | Status | Notes |
| --- | --- | --- |
| Grid data/render/interaction | Done | Multi-floor test grid, hover/select/click, floor visibility control |
| Pathfinding + movement zones | Done | A*, occupancy-aware Dijkstra, action-based path search |
| Entity model + occupancy | Done | Registry, handles, occupancy rules, entity views |
| Turn state machine + actions economy | Done | Core loop works for both player and enemy turns |
| Player actions (Stride/Strike/Stand) | Partial | Core actions implemented; no broader action set |
| PF2e strike/damage basics | Partial | Melee-focused MVP; ranged/spells not implemented |
| Conditions | Partial | Basic list + tick rules; simplified behavior |
| Combat/UI presentation | Partial | Turn HUD, log, initiative, floating damage present |
| Data-driven content (SO assets) | Partial | Grid/camera/items exist; encounter authoring still manual |
| AI | Partial | Simple melee AI implemented; no advanced tactics/ranged/spell logic |
| Save/load/progression | Not started | No persistence layer |
| PlayMode/integration tests | Not started | EditMode only |

## Module Boundaries
- `PF2e.Core`: deterministic rules/data only. No UI concerns.
- `PF2e.Grid`: spatial model + pathfinding + grid-facing MonoBehaviours.
- `PF2e.TurnSystem`: turn orchestration, player action routing, and enemy turn orchestration.
- `PF2e.Managers`: scene composition/orchestration objects.
- `PF2e.Presentation`: view/UI/log adapters only; should not own combat rules.
- `PF2e.Data`: ScriptableObject config/authoring types.

## Conventions
- Namespaces follow folder domain (`PF2e.Core`, `PF2e.Grid`, `PF2e.TurnSystem`, etc.).
- Inspector-first wiring; avoid `FindObjectOfType` and hidden global state.
- ScriptableObjects for authored config/data (`Assets/Data/...`).
- Keep pure rule logic in plain C# classes/structs; keep scene behavior in MonoBehaviours.
- Add/extend typed bus events before adding UI-specific direct dependencies.

## Do Not Break Contracts and Assumptions
- `Assets/Scenes/SampleScene.unity` is the active/bootstrap scene in build settings.
- `TurnManager` action execution contract: use `BeginActionExecution` + `CompleteActionWithCost` for atomic cost/state transitions.
- `StrideAction` commits occupancy/entity position before animation; `EntityMover` is visual-only.
- `AITurnController` must release action locks on abort/timeout/disable to avoid `ExecutingAction` deadlocks.
- `CombatEventBus.Publish(actor, message)` messages must not include actor name prefix.
- `EntityHandle.None` means invalid handle (`Id == 0`).
- Grid/entity raycasts rely on layer consistency (`EntityView` objects share grid layer).

## Known Issues / TODOs
- AI is intentionally minimal: nearest-target melee only, same-elevation targeting, no tactical scoring.
- Combat start/end still debug key driven.
- Condition model has known simplification TODO (value + duration model evolution).
- Input System package exists, but most gameplay input is polled directly from keyboard/mouse.
- No CI/test pipeline checked in; no PlayMode tests.
- Duplicate-looking armor asset naming (`GoblinArmor_.asset`) should be normalized later.

## Next 3 Recommended Tasks (Small, High Value)
1. Add encounter end UX (victory/defeat panel + restart/return actions) on top of existing combat-end logic.
2. Add PlayMode smoke test(s) for enemy turn behavior and immediate victory/defeat transitions.
3. Extend AI from nearest-melee to basic priority rules (focus low HP, avoid no-progress turns, support ranged enemy profiles).

## Project Memory Maintenance Rule
Whenever systems are added or behavior changes, update this file in the same change set with: what changed, scope impact, new assumptions, and checklist status.
