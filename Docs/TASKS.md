# PF2e Tactical RPG — Task Board (LLM-First)

Status legend: `todo` | `in_progress` | `verify` | `done`

| ID | Priority | Status | Agent Role | Task | DoD |
| --- | --- | --- | --- | --- | --- |
| T-001 | P0 | todo | Planner | Define weekly implementation plan for top 3 MVP combat improvements | Task breakdown has scope, risk, and validation commands |
| T-002 | P0 | todo | Builder | Add one incremental AI behavior improvement without breaking current melee baseline | New behavior covered by EditMode/PlayMode checks and no combat deadlocks |
| T-003 | P0 | todo | Verifier | Validate encounter flow and combat-end UX regressions | Test evidence attached with pass/fail per command |
| T-004 | P1 | todo | Builder | Prepare second-scene wiring path for `EncounterFlowPanel.prefab` | Scene wiring documented + smoke validation steps |
| T-005 | P1 | todo | Verifier | Audit `TurnManager` action-lock invariants on latest changes | Repro case list + no stuck lock in test run |

## Agent Prompt Contract (Copy/Paste)
1. Goal
2. Scope (allowed files/folders)
3. Non-goals
4. Definition of Done
5. Validation commands
6. Output format: Summary / Files changed / Risks / Next steps
