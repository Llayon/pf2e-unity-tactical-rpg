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
- Runtime subscriber migration is complete for core UI/controllers: consumers now listen through typed `CombatEventBus` channels; thin adapters (`TurnManagerTypedForwarder`, `ConditionTickForwarder`) remain by design.
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
| Typed event routing | Partial | Runtime consumers moved to typed `CombatEventBus`; legacy/deprecation cleanup verification is pending (`T-013`) |
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
- Cross-module/runtime subscribers (UI, visualizers, flow controllers) should listen to `CombatEventBus` typed events, not `TurnManager` events.

## Do Not Break Contracts and Assumptions
- Build settings keep `Assets/Scenes/SampleScene.unity` as bootstrap scene (index 0); `Assets/Scenes/EncounterFlowPrefabScene.unity` is index 1 for cross-scene UI reuse coverage.
- `master` branch protection requires successful `EditMode` and `PlayMode` checks before merge.
- `TurnManager` action execution contract: use `BeginActionExecution` + `CompleteActionWithCost` for atomic cost/state transitions.
- `TurnManager` action lock tracking contract: if execution starts, lock metadata (`ExecutingActor`, `ExecutingActionSource`, duration) must be reset on completion/rollback/combat end.
- `TurnManager` combat-end contract: only typed result path (`OnCombatEndedWithResult` / `EncounterResult`) is supported.
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
- Deprecated `TurnManagerLogForwarder` is retained only as a disabled compatibility stub for existing scenes; do not use it for new work.

## Next 3 Recommended Tasks (Small, High Value)
1. Complete verification for typed-event consolidation (`T-013`): run EditMode + PlayMode and capture evidence.
2. Optionally remove deprecated `TurnManagerLogForwarder` script file and scene object entries after one release cycle (currently disabled only).
3. Continue AI target-priority work (`T-006..T-008`) after typed-event regression pass is green.

## LLM-First Delivery Workflow (Multi-Agent)
### Operating Model (for non-programmer project owner)
- Product owner responsibilities: define outcome, approve scope, review playable behavior, and accept/reject by Definition of Done (DoD).
- Agent responsibilities: produce plan/code/tests in constrained scopes; avoid making product decisions outside assigned task contracts.
- Repository remains the single source of truth (not chat history): track roadmap, task state, and decisions in versioned markdown files.

### Unified Task Contract (use with every agent)
Use the same prompt structure for all agents to reduce drift:
1. **Goal**: one clear outcome statement.
2. **Scope**: allowed folders/files and systems.
3. **Non-goals**: explicit exclusions.
4. **DoD**: acceptance criteria in observable gameplay/behavior terms.
5. **Validation**: required checks/tests to run and report.
6. **Output format**: summary, changed files, risks, and follow-up tasks.

### Agent Role Split
- **Planner Agent**: decomposes feature into implementation steps, identifies architecture risks, and proposes task order.
- **Builder Agent**: performs implementation exactly within approved scope.
- **Verifier Agent**: independently runs checks/review against DoD and reports failures with reproduction steps.

Recommended sequence per feature: **Plan -> Build -> Verify -> Fix -> Merge**.

### Token-Budget Strategy
- Low-context agents: micro-tasks (single script/class or one bug fix).
- Mid-context agents: feature slices touching a few related files.
- High-context agents: architecture/refactor work crossing multiple systems.

Rule: if an agent exceeds context budget, split by subsystem boundary (`Core`, `Grid`, `TurnSystem`, `Presentation`) instead of continuing in one oversized pass.

### Branch and Merge Protocol
- One feature branch per task card.
- One commit theme per PR (avoid mixed concerns).
- Require verification evidence in PR body (commands + pass/fail output + warnings).
- Prefer fast review loops: small PRs merged frequently over large periodic dumps.

### Minimal Planning Artifacts
- `Docs/ROADMAP.md`: phase-level objectives and milestone outcomes.
- `Docs/TASKS.md`: prioritized backlog with status (`todo`, `in_progress`, `verify`, `done`) and assigned agent role.
- Update both files when priorities or acceptance criteria change.

### Suggested Weekly Cadence
1. Prioritize 3-5 small MVP-critical tasks.
2. Run Planner agent once on the weekly set.
3. Execute Builder/Verifier loop per task.
4. Demo current playable state and collect issues.
5. Re-plan next week based on verification failures and demo feedback.

## Project Memory Maintenance Rule
Whenever systems are added or behavior changes, update this file in the same change set with: what changed, scope impact, new assumptions, and checklist status.
