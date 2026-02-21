# PF2e Tactical RPG — LLM-First Roadmap

## Phase 1 — Stable Vertical Slice (Done)
- Playable encounter in `SampleScene` with clear start/end flow.
- Reliable turn loop (initiative, 3 actions, enemy turn execution).
- MVP combat feedback (initiative bar, combat log, damage popups, encounter result panel).

## Phase 2 — Rules Depth (In Progress)
- Expand strike/check resolution coverage (degrees of success, condition interactions).
- Add additional enemy profiles and action variants.
- Improve AI decision quality while keeping deterministic tests.
- Consolidate runtime event architecture to typed `CombatEventBus` channels and retire legacy TurnManager log adapters. (Done)
- Enforce strict AI typed-event wiring (remove `AITurnController` direct TurnManager subscription fallback). (Done)

## Phase 3 — Content & UX Polish (Planned)
- Add more encounter layouts and data-driven authoring flow.
- Improve turn clarity and targeting UX.
- Balance pass for encounter duration and difficulty curve.

## Phase 4 — Release Hardening (Planned)
- Regression-focused EditMode/PlayMode expansion.
- CI reliability and branch protections.
- Bugfix/stability sprint and release candidate checklist.
