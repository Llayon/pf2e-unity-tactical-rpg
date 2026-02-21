# PF2e Tactical RPG — Task Board (LLM-First)

Status legend: `todo` | `in_progress` | `verify` | `done`

| ID | Priority | Status | Agent Role | Task | DoD |
| --- | --- | --- | --- | --- | --- |
| T-001 | P0 | done | Planner | Define weekly implementation plan for top 3 MVP combat improvements | Task breakdown has scope, risk, and validation commands |
| T-002 | P0 | todo | Builder | Add one incremental AI behavior improvement without breaking current melee baseline | New behavior covered by EditMode/PlayMode checks and no combat deadlocks |
| T-003 | P0 | done | Verifier | Validate encounter flow and combat-end UX regressions | Test evidence attached with pass/fail per command |
| T-004 | P1 | done | Builder | Prepare second-scene wiring path for `EncounterFlowPanel.prefab` | Scene wiring documented + smoke validation steps |
| T-005 | P1 | done | Verifier | Audit `TurnManager` action-lock invariants on latest changes | Repro case list + no stuck lock in test run |
| T-006 | P0 | done | Planner | Define scope for AI target-priority upgrade (low HP focus + no-progress bail-out) | Task contract names impacted files, invariants, and validation commands |
| T-007 | P0 | done | Builder | Implement AI target-priority MVP upgrade in `SimpleMeleeAIDecision`/`AITurnController` | Enemy target selection is deterministic and avoids no-progress turn loops |
| T-008 | P1 | done | Verifier | Add deterministic EditMode coverage for AI target-priority and no-progress guards | New tests fail on regressions and pass in CI |
| T-009 | P0 | done | Planner | Lock typed-events migration contract and update memory/roadmap/task docs | Project memory + roadmap + task board reflect typed-only direction and migration sequence |
| T-010 | P0 | done | Builder | Migrate UI/gameplay subscribers from `TurnManager` events to `CombatEventBus` typed channels | `InitiativeBarController`, `MovementZoneVisualizer`, `TargetingController`, and encounter flow UI subscribe via typed bus |
| T-011 | P0 | done | Builder | Deprecate and remove legacy `OnCombatEnded` path + `TurnManagerLogForwarder` | No runtime references to legacy combat-end event or legacy log forwarder remain |
| T-012 | P1 | done | Builder | Align scene validator and editor autofix with typed-only event path | Validator/autofix stop requiring deprecated forwarder and cover typed forwarders/components |
| T-013 | P1 | done | Verifier | Add/refresh regression checks for typed event flow and combat-end UX | EditMode/PlayMode checks prove encounter end + turn UI still react correctly after migration |
| T-014 | P1 | done | Builder | Remove temporary `AITurnController` legacy TurnManager fallback and enforce strict bus wiring | `AITurnController` listens only to typed bus events; scene wiring/validator/autofix updated; tests stay green |
| T-015 | P1 | done | Verifier | Add PlayMode regression for blocked enemy no-progress handling | New PlayMode scenario proves blocked enemy turn exits quickly and does not leave `TurnManager` in `ExecutingAction` |
| T-016 | P1 | done | Builder | Add sticky target lock per enemy turn (reacquire only when target invalid) | Enemy AI keeps current target through the turn, only reacquires on invalid target; deterministic EditMode tests cover lock behavior |
| T-017 | P1 | done | Verifier | Add PlayMode E2E regression for sticky target lock under dynamic retarget pressure | New PlayMode scenario proves enemy keeps locked target for the same turn even when another player becomes a better candidate after first strike |
| T-018 | P0 | done | Builder | Complete typed turn-event migration by removing runtime `TurnManager` subscribers | `TurnManager` now publishes typed turn events directly to `CombatEventBus`; `TurnManagerTypedForwarder` is deprecated/disabled and validator-autofix enforces `TurnManager.eventBus` wiring |
| T-019 | P0 | done | Builder | Remove deprecated turn forwarder components and compatibility stubs | Deprecated `TurnManagerTypedForwarder`/`TurnManagerLogForwarder` components removed from authored scenes; stub scripts deleted; validator/autofix no longer depends on them |
| T-020 | P1 | done | Verifier | Add EditMode proof that `TurnManager` publishes typed lifecycle events directly to `CombatEventBus` | New EditMode test validates direct bus publication for combat start / initiative / round / turn / actions lifecycle without adapter components |
| T-021 | P1 | done | Verifier | Add EditMode proof for `EndTurn` direct typed publish path | New EditMode test validates direct `TurnEnded` + `ConditionsTicked` publication from `TurnManager.EndTurn` to `CombatEventBus` |
| T-022 | P1 | done | Verifier | Add PlayMode event-order regression for `EndTurn` lifecycle | New PlayMode test validates typed event order `ConditionsTicked -> TurnEnded -> TurnStarted(next)` during real scene flow |
| T-023 | P1 | done | Verifier | Add PlayMode initiative payload integrity regression on typed bus | New PlayMode test validates `OnInitiativeRolledTyped` payload count/uniqueness/team composition/sort order under real scene flow |
| T-024 | P1 | done | Verifier | Add PlayMode consistency regression for combat-end payload and panel text mapping | New PlayMode test validates payload-to-UI text consistency across live victory (`CheckVictory`) and manual abort (`EndCombat`) flows |
| T-025 | P1 | done | Verifier | Add explicit PlayMode defeat consistency contract-case for combat-end payload mapping | New PlayMode test validates `EncounterResult.Defeat` maps to correct panel title/subtitle in live `CheckVictory` flow |
| T-026 | P1 | done | Builder | Extract encounter-end text mapping to pure helper + cover with EditMode tests | `EncounterEndTextMap` is runtime source-of-truth for `EncounterResult -> title/subtitle`; EditMode test covers Victory/Defeat/Aborted/Unknown mappings |
| T-027 | P1 | done | Builder | Extract combat-end log message mapping to pure helper + cover with EditMode tests | `EncounterEndLogMessageMap` is source-of-truth for `EncounterResult -> TurnLogForwarder` message mapping; EditMode tests cover Victory/Defeat/Aborted/Unknown |
| T-028 | P1 | done | Builder | Introduce AI decision policy seam (`IAIDecisionPolicy`) with behavior parity | `AITurnController` keeps orchestration + sticky lock; decision selection routes through `SimpleMeleeDecisionPolicy`; EditMode policy tests added; no gameplay behavior drift |
| T-029 | P0 | done | Builder | Introduce `ConditionService` as single mutation entrypoint with caller-owned delta buffers | `TurnManager` and `StandAction` mutate conditions via `ConditionService`; start-turn stunned removal now emits typed `ConditionChanged`; new EditMode service/event tests pass |
| T-030 | P1 | done | Builder | Unify condition tick payload to `ConditionDelta` and deprecate presentation-domain bridge | `ConditionsTickedEvent` now carries `ConditionDelta`; `ConditionTickForwarder` is inert/deprecated and validator warns if present |
| T-031 | P1 | done | Builder | Tighten condition guardrails by removing legacy direct condition mutations | Remaining legacy callers migrated to `ConditionService`; `EntityData.AddCondition/RemoveCondition` scope tightened to `internal` |

## Agent Prompt Contract (Copy/Paste)
1. Goal
2. Scope (allowed files/folders)
3. Non-goals
4. Definition of Done
5. Validation commands
6. Output format: Summary / Files changed / Risks / Next steps
