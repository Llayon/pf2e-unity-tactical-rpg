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

## Agent Prompt Contract (Copy/Paste)
1. Goal
2. Scope (allowed files/folders)
3. Non-goals
4. Definition of Done
5. Validation commands
6. Output format: Summary / Files changed / Risks / Next steps
