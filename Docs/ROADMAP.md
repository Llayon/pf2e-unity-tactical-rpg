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
- Add shield equipment + `RaiseShieldAction` with AC integration in `EntityData` derived stats cache pipeline. (Done)
- Split `StrikeAction` into phased strike flow (`ResolveAttackRoll` / `DetermineHitAndDamage` / `ApplyStrikeDamage`) with pre/post reaction extension points. (Done)
- Add deterministic `ReactionService` + `ShieldBlockRules` and integrate auto Shield Block reactions into player/AI strike flow (`19.5a`). (Done)
- Add modal Shield Block reaction prompt UX (`ReactionPromptController` + `ModalReactionPolicy`) with async enemy strike reaction window and timeout-safe lock release (`19.5b`). (Done)
- Wire SampleScene shield demo + validator/autofix support for `RaiseShieldAction` / `ShieldBlockAction` / `ReactionPromptController`, and surface Shield Block in combat log (`19.6`). (Done)
- Add generic check system (`SkillType` / `SaveType` / `CheckResolver`) and first skill action consumer (`TripAction`) with typed skill-check events (`Phase 21`). (Done)
- Add `DemoralizeAction` (`Intimidation` vs `Will DC`) with `Frightened` integration and tests (`Phase 22`). (Done)
- Add `ShoveAction` with forced movement (MVP cell-based push), `GrappleAction`, `EscapeAction`, and source-scoped grapple lifecycle (`GrappleService`/`GrappleLifecycleController`) plus targeting-mode wiring (`Phases 22.2–22.3.x`). (Done)
- Add generic non-strike damage foundation (`DamageAppliedEvent` + `DamageApplicationService`) and route `Trip` crit damage through it, including floating damage/log UX support (`Phases 24–24.1`). (Done)
- Make `Strike` weapon-aware (melee + ranged), add ranged range-increment penalties, and ship bow demo content in `SampleScene` (`Phase 25`). (Done)

## Phase 3 — Content & UX Polish (In Progress)
- Add more encounter layouts and data-driven authoring flow.
- Improve turn clarity and targeting UX.
- Add Action Bar UI (clickable actions + hotkey hints + targeting-mode highlight) with scene builder and validator support (`Phase 23`). (Done)
- Add world-space targeting feedback (`eligible` highlight + hover valid/invalid tint) and target reason hint panel powered by `TargetingController` preview reasons (`Phases 23.1–23.2`). (Done)
- Balance pass for encounter duration and difficulty curve.

## Phase 4 — Release Hardening (Planned)
- Regression-focused EditMode/PlayMode expansion.
- CI reliability and branch protections.
- Bugfix/stability sprint and release candidate checklist.
