# PF2e Tactical RPG — LLM-First Roadmap

## Phase 1 — Stable Vertical Slice (Done)
- Playable encounter in `SampleScene` with clear start/end flow.
- Reliable turn loop (initiative, 3 actions, enemy turn execution).
- MVP combat feedback (initiative bar, combat log, damage popups, encounter result panel).

## Phase 2 — Rules Depth (In Progress)
- Expand strike/check resolution coverage (degrees of success, condition interactions).
- Add additional enemy profiles and action variants.
- Improve AI decision quality while keeping deterministic tests.
- Add AI decision-policy seam (`IAIDecisionPolicy`) to prepare Utility-AI migration without changing current behavior. (Done)
- Introduce condition mutation pipeline (`ConditionService`) and publish condition lifecycle via typed deltas from turn/domain layer. (Done)
- Migrate condition tick payloads to `ConditionDelta` and retire presentation-side condition domain bridge (`ConditionTickForwarder`). (Done)
- Tighten condition mutation guardrails by migrating legacy callsites and restricting `EntityData.AddCondition/RemoveCondition` to internal use. (Done)
- Expand condition lifecycle model to support independent `Value + RemainingRounds` semantics in `ConditionService` and typed deltas. (Done)
- Add strict snapshot-based `DerivedStatsCache` for `EntityData` (`EffectiveAC`, `ConditionPenaltyToAttack`) to prevent stale reads under public-field mutations; treat as architecture foundation before broader stacking/implied-rule expansion. (Done)
- Centralize current implied/stacking combat penalties in `ConditionRules` (status max, implied off-guard from prone, no double-count circumstance) with test-first coverage. (Done)
- Add PlayMode duration-condition regression coverage for real turn flow (`DurationChanged`, duration-expire removal, and condition-log consistency). (Done)
- Add PlayMode live-strike regression coverage for status/circumstance stacking semantics (max status and no double-count circumstance on DC). (Done)
- Migrate remaining EditMode turn-condition tests off legacy `EntityData.StartTurn/EndTurn` onto `ConditionService` tick APIs; keep legacy methods as compatibility-only wrappers. (Done)
- Consolidate runtime event architecture to typed `CombatEventBus` channels and retire legacy TurnManager log adapters. (Done)
- Enforce strict AI typed-event wiring (remove `AITurnController` direct TurnManager subscription fallback). (Done)
- Normalize encounter-end log mapping into standalone `EncounterEndLogMessageMap` (`one public type per file`). (Done)

## Phase 3 — Content & UX Polish (Planned)
- Add more encounter layouts and data-driven authoring flow.
- Improve turn clarity and targeting UX.
- Balance pass for encounter duration and difficulty curve.

## Phase 4 — Release Hardening (Planned)
- Regression-focused EditMode/PlayMode expansion.
- CI reliability and branch protections.
- Bugfix/stability sprint and release candidate checklist.
