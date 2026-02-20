# Project Memory
Last updated: 2026-02-20

## Vision
Build a small, playable, turn-based tactical PF2e combat slice in Unity where one player-controlled party can move on a grid, spend 3 actions, strike enemies, and finish encounters with clear visual feedback. Prioritize correctness of core turn/action/combat flow, maintainable architecture, and incremental delivery over full PF2e coverage.

## Vertical Slice Definition
- Primary playable scene: `Assets/Scenes/SampleScene.unity` with one encounter.
- Secondary wiring-validation scene: `Assets/Scenes/EncounterFlowPrefabScene.unity` (prefab-driven encounter flow UI fallback).
- Player can: start combat, move (Stride), Strike, Stand, end turn.
- Enemy side takes turns via simple melee AI (stand if prone, stride toward nearest player, strike in range).
- Combat presents: turn HUD, initiative bar, combat log, floating damage, and end-of-encounter panel.
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
- `Assets/Tests/PlayMode`: smoke tests for encounter-end UX and scene-level flows.
- Tooling/packages: URP, Input System, Unity Test Framework, `com.coplaydev.unity-mcp`; no Addressables package.

### B) Gameplay Loop Status (Current)
- Scene boots to `SampleScene`; test grid and test entities spawn from inspector-wired managers.
- `EncounterFlowPrefabScene` validates cross-scene reuse of `EncounterFlowPanel.prefab` via runtime auto-create path.
- Controls currently in code: encounter flow buttons (`Start Encounter` / `End Encounter`) as primary path, `C`/`X` as editor/development fallback, left-click cell/entity, `Space` end turn, `Esc` cancel targeting, `WASD/QE/Scroll` camera, `G` grid toggle, `PageUp/PageDown` floor.
- Turn flow works: initiative roll, per-entity turns, 3 actions, action spending, condition end-turn ticks.
- Movement works: occupancy-aware, multi-stride pathing, 5/10 diagonal parity, movement zone/path preview, animated movement.
- Combat works at MVP level: melee strike resolution, MAP increment, damage apply, defeat hide + events.
- Enemy turns execute simple AI behavior; combat no longer auto-skips enemy turns.
- Victory/defeat ends combat immediately when one side (`Player` or `Enemy`) is wiped.
- End-of-encounter UI shows `Victory` / `Defeat` / `Encounter Ended`, with restart via scene reload.

### C) Key Dependencies and Risks
- High scene wiring coupling: `CombatController`, `TacticalGrid`, `EntityManager`, `CombatEventBus` must all be correctly referenced.
- Runtime dependency chain is tight (`TurnManager -> EntityManager -> GridManager`; input and visualizers depend on both).
- Hybrid event architecture (typed events + string log forwarders) can drift if contracts are broken.
- Encounter flow can be centralized via `Assets/Data/EncounterFlowUIPreset_RuntimeFallback.asset`; scenes opt in through `EncounterFlowController.useFlowPreset`.
- `EntityManager` mixes runtime orchestration with test spawning data responsibilities.

### D) Missing Foundations for Vertical Slice
- Post-encounter navigation beyond restart (return-to-menu/progression path).
- Explicit action bar/intent UI (targeting feedback still minimal).
- Broader integration coverage beyond current PlayMode smoke tests.

## Current Systems Checklist
| System | Status | Notes |
| --- | --- | --- |
| Grid data/render/interaction | Done | Multi-floor test grid, hover/select/click, floor visibility control |
| Pathfinding + movement zones | Done | A*, occupancy-aware Dijkstra, action-based path search |
| Entity model + occupancy | Done | Registry, handles, occupancy rules, entity views |
| Turn state machine + actions economy | Done | Core loop works for both player and enemy turns; action lock now tracks actor/source/duration with watchdog diagnostics |
| Player actions (Stride/Strike/Stand) | Partial | Core actions implemented; no broader action set |
| PF2e strike/damage basics | Partial | Melee-focused MVP; ranged/spells not implemented |
| Conditions | Partial | Basic list + tick rules; simplified behavior |
| Combat/UI presentation | Partial | Turn HUD, log, initiative, floating damage, and end-of-encounter panel are present; encounter flow panel is reusable and can be driven by shared preset |
| Data-driven content (SO assets) | Partial | Grid/camera/items exist; encounter flow runtime fallback now has a shared UI preset |
| AI | Partial | Simple melee AI implemented; no advanced tactics/ranged/spell logic |
| Save/load/progression | Not started | No persistence layer |
| PlayMode/integration tests | Partial | PlayMode covers encounter-end UX, live CheckVictory turn-flow, action-driven victory/defeat outcomes, encounter flow button start/end behavior, authored EncounterFlowController wiring, prefab-based auto-create fallback wiring, cross-scene prefab encounter-flow smoke coverage, and multi-round regression (movement + enemy AI + condition ticks); broader system-level coverage is still pending |
| TurnManager action-lock tests (EditMode) | Done | EditMode now verifies lock metadata lifecycle (begin/complete/endcombat) and executing-actor action-cost ownership |
| CI test automation | Done | GitHub Actions (`.github/workflows/unity-tests.yml`) runs EditMode + PlayMode on push/PR to `master`; branch protection on `master` requires `Unity Tests` |

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
- Build settings keep `Assets/Scenes/SampleScene.unity` as bootstrap scene (index 0); `Assets/Scenes/EncounterFlowPrefabScene.unity` is index 1 for cross-scene UI reuse coverage.
- `master` branch protection requires successful `Unity Tests` checks before merge.
- `TurnManager` action execution contract: use `BeginActionExecution` + `CompleteActionWithCost` for atomic cost/state transitions.
- `TurnManager` action lock tracking contract: if execution starts, lock metadata (`ExecutingActor`, `ExecutingActionSource`, duration) must be reset on completion/rollback/combat end.
- `TurnManager` combat-end contract: keep both `OnCombatEnded` (legacy) and `OnCombatEndedWithResult` (typed result path).
- `EncounterFlowController` defaults to authored references (`autoCreateRuntimeButtons=false`); runtime auto-create is fallback only.
- When `EncounterFlowController.useFlowPreset` is enabled, `flowPreset` becomes source-of-truth for runtime fallback fields.
- `StrideAction` commits occupancy/entity position before animation; `EntityMover` is visual-only.
- `AITurnController` must release action locks on abort/timeout/disable to avoid `ExecutingAction` deadlocks.
- `CombatEventBus.Publish(actor, message)` messages must not include actor name prefix.
- `EntityHandle.None` means invalid handle (`Id == 0`).
- Grid/entity raycasts rely on layer consistency (`EntityView` objects share grid layer).

## Known Issues / TODOs
- AI is intentionally minimal: nearest-target melee only, same-elevation targeting, no tactical scoring.
- `SampleScene` remains authored-reference first; `EncounterFlowPrefabScene` is the current preset-driven fallback example scene.
- Restart is scene-reload based (`SceneManager.LoadScene`) and intentionally simple for MVP.
- Condition model has known simplification TODO (value + duration model evolution).
- Input System package exists, but most gameplay input is polled directly from keyboard/mouse.
- CI requires repository-level `UNITY_LICENSE` secret; workflow fails fast when missing.
- PlayMode regression now covers multi-round movement/AI/condition-tick flow, but does not yet cover advanced combat domains (ranged/spells/reactions).
- Combat round regression deadlock assertions now combine lock duration with turn-progress signals to reduce CI timing flakes while still detecting real stuck locks.
- Duplicate-looking armor asset naming (`GoblinArmor_.asset`) should be normalized later.

## Next 3 Recommended Tasks (Small, High Value)
1. Extend AI from nearest-melee to basic priority rules (focus low HP, avoid no-progress turns, support ranged enemy profiles).
2. Add one more authored content scene and verify it can switch between authored/preset encounter-flow modes without code changes.
3. Add deterministic AI decision EditMode tests for low-HP focus and no-progress turn bail-out.

## Project Memory Maintenance Rule
Whenever systems are added or behavior changes, update this file in the same change set with: what changed, scope impact, new assumptions, and checklist status.
