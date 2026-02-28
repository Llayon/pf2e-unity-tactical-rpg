# Project Memory
Last updated: 2026-02-28

## Vision
Build a small, playable, turn-based tactical PF2e combat slice in Unity where one player-controlled party can move on a grid, spend 3 actions, strike enemies, and finish encounters with clear visual feedback. Prioritize correctness of core turn/action/combat flow, maintainable architecture, and incremental delivery over full PF2e coverage.

## Vertical Slice Definition
- Primary playable scene: `Assets/Scenes/SampleScene.unity` with one encounter.
- Secondary wiring-validation scene: `Assets/Scenes/EncounterFlowPrefabScene.unity` (prefab-driven encounter flow UI fallback).
- Player can: start combat, move (Stride), Strike, Trip, Shove, Grapple, Escape, Demoralize, Reposition, Delay, Stand, Raise Shield, end turn.
- Enemy side takes turns via simple melee AI (stand if prone, stride toward nearest player, strike in range; if no same-floor targets exist, target selection now falls back to any elevation).
- Combat presents: turn HUD, initiative bar, combat log (pooled TMP lines, wrap-aware row heights, retention cap `maxLines=80` with notice), floating damage, modal Shield Block reaction prompt, and end-of-encounter panel.
- Basic PF2e rules included: 3-action economy, MAP, weapon-aware strike check (melee/ranged, ranged LoS/cover/concealment MVP), damage roll, simple conditions.

## Architecture Snapshot
### A) Current Folder Map + Responsibilities
- `Assets/Scripts/Core`: pure rules/data structures (entities, condition rules/service/deltas, attack/damage math, occupancy, item models, event contracts).
- `Assets/Scripts/Grid`: grid data, pathfinding, rendering, floor controls, click/hover interaction.
- `Assets/Scripts/TurnSystem`: turn state machine, input routing, action execution (`StrideAction`, `StrikeAction`, `StandAction`, `RaiseShieldAction`, `ShieldBlockAction`, `TripAction`, `ShoveAction`, `GrappleAction`, `EscapeAction`, `DemoralizeAction`), phased strike/reaction windows, generic check system (`CheckResolver`), targeting, grapple lifecycle (`GrappleLifecycleController` + `GrappleService`), enemy AI orchestration (`AITurnController`), and AI decision seam (`IAIDecisionPolicy` + `SimpleMeleeDecisionPolicy`) plus reaction decision seam (`IReactionDecisionPolicy`, runtime default `ModalReactionPolicy`).
- `Assets/Scripts/Managers`: `EntityManager` scene orchestration, spawning test entities (now including optional fighter shield + reaction preference), view management.
- `Assets/Scripts/Presentation`: UI/controllers/log forwarders, initiative/floating damage visuals, action bar, targeting world tint feedback, targeting reason hint UI, Delay initiative/prompt composition helpers (`DelayPlacementPromptPresenter`, `DelayPlacementMarkerOverlayPresenter`, `DelayPlacementInteractionCoordinator`, `DelayInitiativeRowPlanner`), and top-level Delay UI mediation (`DelayUiOrchestrator`).
- `Assets/Scripts/Data`: ScriptableObject configs (`GridConfig`).
- `Assets/Tests/EditMode`: NUnit EditMode coverage for grid/pathfinding/occupancy/turn primitives.
- `Assets/Tests/PlayMode`: smoke tests for encounter-end UX and scene-level flows.
- Tooling/packages: URP, Input System, Unity Test Framework, `com.coplaydev.unity-mcp`; no Addressables package.

### B) Gameplay Loop Status (Current)
- Scene boots to `SampleScene`; test grid and test entities spawn from inspector-wired managers.
- `EncounterFlowPrefabScene` validates cross-scene reuse of `EncounterFlowPanel.prefab` via runtime auto-create path.
- Controls currently in code: encounter flow buttons (`Start Encounter` / `End Encounter`) as primary path, `C`/`X` as editor/development fallback, left-click cell/entity, action bar buttons (bottom-center), `Space` end turn, `Esc` cancel targeting, `R` Raise Shield, `T` Trip, `Y` Demoralize, `H` Shove, `J` Grapple, `K` Escape, `V` Reposition, `Delay` action button (turn-begin trigger only; opens initiative insertion markers between portraits), `WASD/QE/Scroll` camera, `G` grid toggle, `PageUp/PageDown` floor.
- Input/UI boundaries are hardened: grid hover/click handlers and camera wheel-zoom are ignored when pointer is over UI, preventing button click-through movement and accidental zoom while scrolling UI.
- Turn flow works: initiative roll, per-entity turns, 3 actions, action spending, and condition lifecycle processing via `ConditionService` (start/end turn deltas).
- Delay flow works at MVP: Delay can be declared only in the turn-begin trigger window, opens initiative insertion markers between portraits, stores planned anchor return, auto-resumes planned delayed turns after anchor actor end (including multi-delayed auto-chain), keeps manual `Return`/`Skip` fallback only for non-planned delayed actors, and suppresses reactions while delayed.
- Initiative now uses typed check primitives: `TurnManager.RollInitiative` rolls `CheckRoll` via `CheckResolver.RollPerception` (`PerceptionModifier`, not raw `WisMod`), orders by total (`natural + modifier`) with PF2e tie rule (adversary before player on equal result), and exposes roll-source breakdown in typed payload/log flow.
- Initiative source is now configurable in `TurnManager` (`InitiativeCheckMode`), with default `Perception` and optional skill-based rolls (`CheckResolver.RollSkill(...)`), while preserving Perception as the baseline contract.
- Encounter-level initiative source selection is wired through `EncounterFlowController`: Start Encounter applies configured mode/skill (or preset values) to `TurnManager` before `StartCombat`, keeping `Perception` as default authoring behavior.
- Per-entity initiative overrides are supported in `EntityData` (`UseInitiativeSkillOverride`, `InitiativeSkillOverride`): flagged actors roll initiative with that skill (e.g. sneaking actor uses `Stealth`), while all others keep encounter/default `Perception`.
- Movement works: occupancy-aware, multi-stride pathing, 5/10 diagonal parity, movement zone/path preview, animated movement.
- Combat works at MVP level: weapon-aware strike resolution (melee + ranged), MAP increment, ranged range-increment penalties, ranged line-of-sight validation + simple cover AC bonus support, ranged concealment flat-check miss support (`Concealed`, DC 5 flat check on would-hit), ranged `Volley` penalty support, strike crit math support for `Deadly` and `Fatal`, phased strike flow (pre/post reaction extension points), damage apply, defeat hide + events, and generic non-strike damage pipeline (`DamageAppliedEvent` + `DamageApplicationService`) currently used by `Trip` crit damage.
- Skill-action checks work at MVP level: generic `CheckResolver` + `SkillType`/`SaveType` + `SkillRules`, with `Trip`, `Shove`, `Grapple`, `Escape`, `Demoralize`, and `Reposition` implemented and wired through `PlayerActionExecutor`.
- Opposed-check foundation is available for future contested-roll mechanics: `CheckResolver.RollOpposedCheck(...)`, `OpposedCheckResult`, typed event `OpposedCheckResolvedEvent`, and `OpposedCheckLogForwarder` (current tactical actions still use PF2e check-vs-DC flow).
- Skill-check action payloads now use typed roll metadata: `SkillCheckResolvedEvent` carries `CheckRoll` (`source/natural/modifier/total`) plus explicit defense source (`CheckSource` for save/skill) and DC, allowing log/UI formatting without action-specific inference.
- Strike payloads now expose typed roll metadata end-to-end: `StrikePhaseResult`, `StrikePreDamageEvent`, and `StrikeResolvedEvent` include strike `CheckRoll` (`ATK`) plus defense source (`AC`), and concealment flat-check roll is available as typed roll metadata when concealment is required.
- Presentation now uses a shared roll-text formatter (`RollBreakdownFormatter`) for initiative/skill/strike/opposed logs and targeting valid-check hint formulas, keeping source labels and `d20 + mod = total` text style consistent across log and UI hint surfaces.
- Strike attack-math text (`atk/MAP/RNG/VOLLEY`) and defense-side cover text (`AC + COVER`) are now also centralized in `RollBreakdownFormatter`, reducing log-format drift risk between strike code paths.
- Shield flow works at MVP level: `Raise Shield` grants temporary AC via `EntityData` derived stats; `Shield Block` can reduce post-hit damage, damage the shield, and spend reaction.
- Grapple/Escape lifecycle works at MVP level: source-scoped relation state is owned by `GrappleService` (plain C#) and orchestrated by `GrappleLifecycleController`; holds expire at end of grappler's next turn, break on grappler movement, and can be released by `EscapeAction` success.
- Enemy strike flow can pause for player reaction decisions via `ReactionPromptController` (timeout/disable-safe auto-decline); player strike flow remains synchronous for current self-only Shield Block reactions.
- Enemy turns execute simple AI behavior; combat no longer auto-skips enemy turns.
- Targeting UX now includes Action Bar mode highlight plus world-space target feedback (`eligible` highlights + hover valid/invalid tint), a text hint panel explaining preview reasons using the same `TargetingController` validation path as confirm clicks (including non-blocking warning hints for ranged `Strike` risk states: cover `+2 AC` and concealment `DC 5` flat-check, with combined warning formatting), and a two-step targeting state machine for `Reposition` (target -> check -> destination cell on success).
- Victory/defeat ends combat immediately when one side (`Player` or `Enemy`) is wiped.
- End-of-encounter UI shows `Victory` / `Defeat` / `Encounter Ended`, with restart via scene reload.
- `SampleScene` now includes shield demo wiring (`FighterShield`, `ReactionPromptController`, fighter `ShieldBlockPreference = AlwaysAsk`), skill-action runtime wiring (`Trip`/`Shove`/`Grapple`/`Escape`/`Demoralize`/`Reposition`), Action Bar UI, targeting feedback/tint controllers, targeting hint panel, generic damage log forwarder, and a ranged strike demo path (`Shortbow` assigned to the wizard via `EntityManager` weapon config).

### C) Key Dependencies and Risks
- High scene wiring coupling: `CombatController`, `TacticalGrid`, `EntityManager`, `CombatEventBus` must all be correctly referenced.
- Runtime dependency chain is tight (`TurnManager -> EntityManager -> GridManager`; input and visualizers depend on both).
- Turn flow now uses typed payloads end-to-end: `TurnManager` publishes typed structs to its own events and directly to `CombatEventBus`; runtime consumers subscribe on typed bus channels.
- Check domain model now has reusable `CheckRoll` + `CheckSource` + `CheckComparer` primitives; `CheckResult` composes `CheckRoll` (`dc`/`degree` on top) while keeping backward-compatible accessors (`naturalRoll`, `modifier`, `total`).
- Delay UX is event-driven: `TurnManager` publishes typed delay channels (`DelayTurnBeginTriggerChanged`, `DelayPlacementSelectionChanged`, `DelayReturnWindowOpened/Closed`, `DelayedTurnEntered/Resumed/Expired`) and UI refresh is triggered from these events (no per-frame delay polling).
- Delay UI fanout can be centralized via `DelayUiOrchestrator` (`ActionBarController` + `InitiativeBarController`), while controllers keep internal typed-event fallback for scenes/tests without explicit orchestrator wiring.
- Scene tooling now understands Delay fanout wiring: `PF2eSceneDependencyValidator` validates `DelayUiOrchestrator` refs and `AutoFix` can create/wire it when both Action Bar and Initiative Bar are present.
- Scene tooling guard now has EditMode coverage: `PF2eSceneDependencyValidatorTests` verifies clean non-sample scene auto-returns to `SampleScene` through the workflow guard path.
- `SampleScene` UI baseline now has EditMode smoke coverage: `PF2eSceneDependencyValidatorTests` asserts presence of critical combat UI controllers (`ActionBarController`, `InitiativeBarController`, `TurnUIController`, `CombatLogController`).
- Delay orchestrator auto-fix now has EditMode regression coverage: `PF2eSceneDependencyValidatorTests` removes `DelayUiOrchestrator`, runs private `RunAutoFix(false)`, and verifies orchestrator recreation plus field wiring (`eventBus`, `actionBarController`, `initiativeBarController`).
- Delay orchestrator auto-fix idempotency is covered: `PF2eSceneDependencyValidatorTests` verifies `RunAutoFix(false)` does not create duplicate `DelayUiOrchestrator` when one already exists.
- Delay orchestrator remediation is covered: `PF2eSceneDependencyValidatorTests` nulls refs on an existing `DelayUiOrchestrator`, runs `RunAutoFix(false)`, and verifies refs are restored without duplicate creation.
- Condition flow is now centralized through `ConditionService`; future rule expansion risk is concentrated in one place (good), but expanding stacking/implied rules must preserve deterministic tests.
- Reaction UX introduces a mixed sync/async execution split: `AITurnController` enemy strike flow is coroutine-based for modal reaction windows, while `PlayerActionExecutor` strike flow remains sync for the current self-only Shield Block MVP.
- Targeting UX now depends on validation-path equivalence: `TargetingController.PreviewEntity(...)` / `PreviewEntityDetailed(...)` and confirm click routing must stay in sync (shared evaluation core) to avoid "green highlight but invalid click" drift.
- Grapple lifecycle is now stateful and source-scoped (`GrappleService` owned by `GrappleLifecycleController`); future changes to movement/turn/combat-end events must preserve grapple release hooks.
- `ReactionPromptController` is scene-optional in validator terms (warning-only if missing), but missing wiring degrades `AlwaysAsk` to auto-decline via `ModalReactionPolicy`.
- Encounter flow can be centralized via `Assets/Data/EncounterFlowUIPreset_RuntimeFallback.asset`; scenes opt in through `EncounterFlowController.useFlowPreset`.
- `EntityManager` mixes runtime orchestration with test spawning data responsibilities.

### D) Missing Foundations for Vertical Slice
- Post-encounter navigation beyond restart (return-to-menu/progression path).
- Explicit range/LoS preview overlays and richer invalid-target reason UX polish beyond current text hints/tints.
- Broader integration coverage beyond current PlayMode smoke tests.

## Current Systems Checklist
| System | Status | Notes |
| --- | --- | --- |
| Grid data/render/interaction | Done | Multi-floor test grid, hover/select/click, floor visibility control |
| Pathfinding + movement zones | Done | A*, occupancy-aware Dijkstra, action-based path search |
| Entity model + occupancy | Done | Registry, handles, occupancy rules, entity views |
| Turn state machine + actions economy | Done | Core loop works for both player and enemy turns; action lock tracks actor/source/duration with watchdog diagnostics; Delay turn-begin trigger, planned insertion, auto-resume chain, and fallback return-window state are integrated |
| Initiative model + ordering | Done | Initiative uses `PerceptionModifier` with typed `CheckRoll` payload (`natural/modifier/total/source`), total-based ordering, enemy-first tie policy, deterministic fallback, and typed combat log breakdown |
| Player actions (Stride/Strike/Stand/Raise Shield + skill actions) | Partial | `Strike` is weapon-aware (melee + ranged bow MVP with cover/concealment warning UX), `Delay` is implemented with planned insertion workflow, and `Trip`/`Shove`/`Grapple`/`Escape`/`Demoralize`/`Reposition` are playable; broader action set (spells, advanced athletics actions) still pending |
| Generic checks + skill actions | Partial | `CheckResolver`, skill/save proficiencies/modifiers, first tactical skill actions (including two-step `Reposition`), and generic opposed-check foundation (`RollOpposedCheck` + typed event/log path) are implemented; broader PF2e action surface and richer rule interactions still pending |
| PF2e strike/damage basics | Partial | Weapon-aware strike MVP (melee + ranged bow) with MAP, phased reaction windows, ranged range-increment penalties, ranged LoS + simple cover AC support, ranged concealment flat-check miss support, `Volley` trait penalty, and crit-trait support for `Deadly`/`Fatal`; generic non-strike damage event/application path exists for `Trip` crit damage; spells and advanced ranged rules (ammo/reload, hidden/undetected, greater concealment/vision interactions) not implemented |
| Reactions (Shield Block MVP) | Partial | Post-hit reaction window implemented with pure `ReactionService`/`ShieldBlockRules`, `ShieldBlockAction`, and modal prompt UX; only self-only Shield Block is supported |
| Conditions | Partial | `ConditionService` is the mutation entrypoint for turn/action flows with caller-owned `ConditionDelta` buffers; model supports independent `Value + RemainingRounds` tick semantics; `ConditionRules` now owns implied/stacking helpers for current combat penalties; `EntityData` uses strict snapshot-based derived-stat cache invalidation for AC/attack-penalty reads |
| Combat/UI presentation | Partial | Turn HUD, action bar, initiative bar with Delay insertion markers/prompt, targeting world feedback (eligible/hover tint), targeting reason hint panel (valid/invalid plus warning tone for ranged strike risk hints: cover `+2 AC` and concealment flat-check, including combined warning text), combat log (wrap-aware pooled rows + retention notice), generic non-strike damage log forwarder, floating damage, Shield Block modal prompt (`ReactionPromptController`), and end-of-encounter panel are present; end-panel text maps through `EncounterEndTextMap`; encounter flow panel is reusable and can be driven by shared preset |
| Typed event routing | Done | `TurnManager` source events are typed and published directly to `CombatEventBus`; runtime subscribers consume typed bus events |
| Generic damage event routing | Partial | `DamageAppliedEvent` + `DamageApplicationService` exist for non-strike damage (currently `Trip` crit damage); strike damage still uses `StrikeResolvedEvent` as primary UI/log path |
| Encounter-end text mapping | Done | `EncounterEndTextMap` is source-of-truth for `EncounterResult -> title/subtitle`, consumed by `EncounterEndPanelController` and covered by EditMode unit tests |
| Encounter-end log mapping | Done | `EncounterEndLogMessageMap` (`Assets/Scripts/Presentation/EncounterEndLogMessageMap.cs`) is source-of-truth for `EncounterResult -> combat-end log message`, consumed by `TurnLogForwarder` and covered by EditMode unit tests |
| Data-driven content (SO assets) | Partial | Grid/camera/items exist; encounter flow runtime fallback now has a shared UI preset |
| AI | Partial | Simple melee AI implemented with deterministic target priority (`distance -> HP -> handle`), same-elevation preference with any-elevation fallback, sticky per-turn target lock (reacquire only on invalid target), and no-progress bailout; `AITurnController` now routes decisions through `IAIDecisionPolicy` (`SimpleMeleeDecisionPolicy`) to preserve behavior while preparing Utility-AI migration; no advanced tactics/ranged usage/spell logic |
| Save/load/progression | Not started | No persistence layer |
| PlayMode/integration tests | Partial | PlayMode covers encounter-end UX, live CheckVictory turn-flow, action-driven victory/defeat outcomes, encounter flow button start/end behavior, authored EncounterFlowController wiring, prefab-based auto-create fallback wiring, cross-scene prefab encounter-flow smoke coverage, multi-round regression (movement + enemy AI + condition ticks), blocked-enemy regression (turn exits without `ExecutingAction` deadlock), sticky-target lock E2E regression (enemy does not retarget mid-turn), EndTurn typed-event order regression (`ConditionsTicked -> TurnEnded -> TurnStarted(next)`), initiative typed payload integrity regression (count/uniqueness/team composition/sort order), duration-condition lifecycle regressions (`DurationChanged` and duration-expire removal with matching log output), live status-stacking strike regressions (max status, single circumstance), `ReactionPromptController` PlayMode coverage (Yes/No/timeout/disable/double-request), enemy->player Shield Block prompt-timeout E2E lock-release regression (`ExecutingAction` timeout path releases cleanly), Delay planned-anchor auto-resume chain regressions, Delay manual `ReturnNow`/`Skip` window-flow regressions, Delay full-round expiry regression (`DelayedTurnExpired` + reinsertion contract), Delay pointer-level UI click regressions (Action Bar + insertion marker), Delay pointer-flow expiry regression (repeated `Skip` clicks until expiration), and combat-end payload-to-panel consistency regressions for live victory/defeat and manual abort; broader system-level coverage is still pending |
| Typed bus direct-publish tests (EditMode) | Done | EditMode now asserts direct `TurnManager -> CombatEventBus` lifecycle publish for `StartCombat` path and `EndTurn` path (`TurnEnded` + `ConditionsTicked`) without forwarder adapters |
| TurnManager action-lock tests (EditMode) | Done | EditMode now verifies lock metadata lifecycle (begin/complete/endcombat) and executing-actor action-cost ownership |
| CI test automation | Done | GitHub Actions (`.github/workflows/unity-tests.yml`) runs EditMode + PlayMode on push/PR to `master`; branch protection on `master` requires `Unity Tests` |

## Module Boundaries
- `PF2e.Core`: deterministic rules/data only. No UI concerns.
- `PF2e.Grid`: spatial model + pathfinding + grid-facing MonoBehaviours.
- `PF2e.TurnSystem`: turn orchestration, player action routing, and enemy turn orchestration; AI decisions flow through `IAIDecisionPolicy`, while lock/progress/action execution stay in `AITurnController`.
- `PF2e.Managers`: scene composition/orchestration objects.
- `PF2e.Presentation`: view/UI/log adapters only; should not own combat rules.
- `PF2e.Data`: ScriptableObject config/authoring types.
- Future feat/rule integration boundary (design guardrail): `EntityData` remains source-of-truth for persistent/base+derived character state, while resolvers (`AttackResolver`, `DamageResolver`, future check resolvers) own context-specific calculation (MAP, weapon/situational/conditional logic for a specific check).

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
- `TurnManager` typed turn-event contract: `eventBus` must be assigned; turn/combat lifecycle events are emitted both via `TurnManager` typed source events and direct `CombatEventBus` publish.
- Delay action contract (MVP): Delay is legal only in the explicit turn-begin trigger window (`delayTurnBeginTriggerOpen == true`) before any committed action; placement selection is explicit state and must close on action execution/cancel/turn transition.
- Delay planned-return contract: planned delays resume automatically after their selected anchor actor ends turn; if multiple delayed actors share the same anchor, they auto-chain sequentially without opening manual Return/Skip UI.
- Delay fallback contract: manual `DelayReturnWindow` (`Return`/`Skip`) remains only for non-planned delayed actors; planned-delay-only windows must auto-skip to avoid Owlcat-flow UX regressions.
- Delay reaction-suppression contract: delayed actors cannot use reactions until resumed/expired; suppression must clear on resume, expiry, and combat end cleanup.
- Delay typed-event contract: Delay UI state changes are published through typed delay channels on `CombatEventBus`; UI controllers must not poll `TurnManager` each frame for Delay state changes.
- Delay orchestrator contract: when both `ActionBarController` and `InitiativeBarController` exist in scene, validator should report missing `DelayUiOrchestrator`; `AutoFix` may create/wire it.
- Conditions mutation contract: gameplay/turn/action code must mutate conditions through `ConditionService` (caller-owned `List<ConditionDelta>` buffers), not direct `EntityData` mutation helpers.
- `EntityData.AddCondition/RemoveCondition` are internal guardrail APIs for core-only usage; cross-module/gameplay code must use `ConditionService`.
- `EntityData.StartTurn/EndTurn` are compatibility-only legacy helpers; gameplay/tests should prefer `ConditionService.TickStartTurn/TickEndTurn`.
- Condition lifecycle payload contract: `ConditionsTickedEvent` now uses `ConditionDelta` entries as canonical payload.
- Condition changed event contract: duration-only ticks use `ConditionChangeType.DurationChanged`, with `oldRemainingRounds/newRemainingRounds` populated on `ConditionChangedEvent`.
- Condition tick semantics contract: on end turn, value auto-decay and duration countdown are independent; remove on `RemainingRounds == 0`, or on valued infinite conditions when `Value <= 0`.
- Condition stacking/implied contract (current slice): status penalties from `Frightened/Sickened` use max (not sum); attack circumstance uses `Prone` only; AC circumstance uses `OffGuard || Prone` (no double-count; `Prone` implies off-guard for AC context).
- Derived-stat cache contract: `EntityData` validates `EffectiveAC`/`ConditionPenaltyToAttack` against strict snapshot/fingerprint (`Dexterity`, `Level`, armor state, condition fingerprint including `RemainingRounds`) to prevent stale reads under direct public-field mutations.
- Runtime subscriber contract: new systems should subscribe via `CombatEventBus`, not directly to `TurnManager`.
- Presentation/domain boundary contract: presentation components must not generate domain condition mutations/events; `ConditionTickForwarder` is deprecated and inert.
- End-of-encounter UI text contract: use `EncounterEndTextMap` for `EncounterResult` labels/messages instead of duplicating string switches in controllers/tests.
- End-of-encounter log text contract: use `EncounterEndLogMessageMap` for combat-end messages instead of duplicating string switches in forwarders/tests.
- `EncounterFlowController` defaults to authored references (`autoCreateRuntimeButtons=false`); runtime auto-create is fallback only.
- When `EncounterFlowController.useFlowPreset` is enabled, `flowPreset` becomes source-of-truth for runtime fallback fields.
- `StrideAction` commits occupancy/entity position before animation; `EntityMover` is visual-only.
- Logical movement event contract: typed `EntityMovedEvent` is published on logical move commit (currently `StrideAction` and `EntityManager.TryMoveEntityImmediate(...)`), not by `EntityMover` animation.
- Grapple relation contract: `GrappleService` (plain C#, owned by `GrappleLifecycleController`) is source-of-truth for grapple source-target relations; do not mirror this state into `EntityData`.
- Grapple cleanup contract: `GrappleLifecycleController` must clear service-owned relations on turn-end expiry, grappler movement, and combat end.
- Escape contract (MVP): `EscapeAction` uses best-of `Athletics`/`Acrobatics`, has Attack trait semantics (MAP applies + increments), and publishes `SkillCheckResolvedEvent` with the actually used skill.
- Reposition contract (MVP): `RepositionAction` uses a two-step flow (target select -> check -> destination cell only on success/crit success); `Esc` in destination phase means "no move, action spent" and forced movement can be 0 ft.
- Targeting preview contract: `TargetingController.PreviewEntityDetailed(...)` is the canonical source for target feedback/tints/hint text; UI layers must not duplicate action validation.
- Targeting feedback contract: `GridInteraction` publishes hovered entity transitions through `GridManager` hover events; `TargetingFeedbackController`/`TargetingHintController` are event-driven (no per-frame full validation scan).
- Combat log retention contract: `CombatLogController` keeps only last `maxLines` entries (currently 80) by recycling oldest pooled rows; older entries are intentionally dropped and communicated via retention notice label text.
- Combat log layout contract: each line row height must track TMP preferred wrapped height; pooled rows must be re-parented as last sibling on reuse to preserve scroll order.
- Input/UI gate contract: when pointer is over UI, `GridInteraction` must ignore hover/click world interaction and `TacticalCameraController` must ignore wheel zoom input.
- Generic damage UX contract: non-strike damage uses `DamageAppliedEvent`; `FloatingDamageUI`/`DamageLogForwarder` subscribe to this path. Strike damage still uses `OnStrikeResolved` path until an explicit strike migration is done (avoid duplicate UI/log lines).
- Weapon-aware strike contract (Phase 25+): `Strike` is the single action path for melee and ranged weapons; validation/runtime branch on equipped weapon (`weapon == null || !IsRanged` => melee, `IsRanged` => ranged). Ranged strikes ignore same-elevation checks in MVP, require grid LoS (`StrikeLineResolver`, supercover with permissive corner), fail preview/confirm with `NoLineOfSight` when blocked, apply simple cover AC bonus (+2) when line grants cover, and if the target has `Concealed` then require a DC 5 flat check on would-hit (`Success`/`CriticalSuccess` vs AC) with failure downgrading the final strike outcome to `Failure` (payload preserves `acDegree` for log/UI explanation); range-increment penalty (`-2` per increment after the first) and optional `Volley` close-range penalty still apply as normal.
- Parameterized weapon-trait contract (current pattern): boolean traits stay in `WeaponTraitFlags`; parameterized strike traits (`Deadly`, `Fatal`, `Volley`) are stored as typed fields on `WeaponDefinition` / `WeaponInstance` and integrated directly in strike math/log payloads. Generic trait processor remains intentionally deferred.
- `AITurnController` must release action locks on abort/timeout/disable to avoid `ExecutingAction` deadlocks.
- Enemy strike reaction-window contract: `AITurnController` may legitimately keep `TurnManager.State == ExecutingAction` while awaiting modal reaction input; timeout/abort/disable must resolve as decline and release the lock.
- Reaction policy runtime-default contract: `AITurnController` and `PlayerActionExecutor` instantiate `ModalReactionPolicy` (not `AutoShieldBlockPolicy`) for Shield Block decisions; `AutoShieldBlockPolicy` is retained primarily for tests/helpers.
- Shield Block log contract: `ShieldBlockResolvedEvent` is forwarded to combat log via `StrikeLogForwarder`; changes to event payload/message format should keep actor-prefix rules (`CombatEventBus.Publish(actor, message)`).
- Future PF2e modifier-model guardrail: numeric bonuses/penalties should evolve toward `Modifier { value, type, source }` (for stacking + provenance), while feat-granted actions/reactions/capabilities stay a separate subsystem (not modifier buckets).
- Future PF2e stat-resolution guardrail: avoid introducing a parallel runtime stat source (`CharacterRulesRuntime` companion) while `EntityData` is still mutated directly in gameplay/tests; if modifier caching becomes necessary, prefer `EntityData`-owned derived cache first.
- AI target selection contract is deterministic: nearest target first, then lower HP, then lower handle id as final tie-break.
- AI policy seam contract: `IAIDecisionPolicy` must remain decision-only (target/range/stride choice); sticky lock and turn-loop orchestration remain controller responsibilities.
- AI target lock contract: once selected, enemy keeps target for the turn unless target becomes invalid (dead/non-player/different elevation/missing).
- `CombatEventBus.Publish(actor, message)` messages must not include actor name prefix.
- `EntityHandle.None` means invalid handle (`Id == 0`).
- Grid/entity raycasts rely on layer consistency (`EntityView` objects share grid layer).

## Known Issues / TODOs
- AI is intentionally minimal: nearest-target melee only (prefers same elevation, then falls back to any elevation), no tactical scoring.
- AI no-progress bailout uses threshold 2 repeated identical loop snapshots; tune only with matching regression tests.
- `SampleScene` remains authored-reference first; `EncounterFlowPrefabScene` is the current preset-driven fallback example scene.
- Restart is scene-reload based (`SceneManager.LoadScene`) and intentionally simple for MVP.
- `PlayerActionExecutor` strike flow is intentionally synchronous in the current Shield Block MVP because the only supported reaction is self-only on the defender; non-self/ally reactions will require player strike flow coroutine conversion.
- `EntityData.AddCondition/RemoveCondition` are now `internal` guardrails; avoid introducing new callers outside core condition infrastructure.
- Legacy `ConditionTick` struct remains only for compatibility in `EntityData.EndTurn`; typed event flow is now `ConditionDelta`-based and EditMode turn-condition tests use `ConditionService` ticks directly.
- Condition model now supports simultaneous `Value + RemainingRounds`; stacking/implied helpers exist for current attack/AC penalties, but broader PF2e condition interactions are still pending.
- Derived-stat cache is currently an architecture/correctness foundation; for the present simple formulas it is not guaranteed to be a net performance win yet.
- Input System package exists, but most gameplay input is polled directly from keyboard/mouse.
- CI requires repository-level `UNITY_LICENSE` secret; workflow fails fast when missing.
- PlayMode regression now covers multi-round movement/AI/condition-tick flow, blocked-turn recovery, sticky-target lock behavior, ranged concealment/cover logic, Shield Block reaction prompts, and Delay planned/manual + full-round expiry + pointer-level UI click flows (including pointer-driven Skip-loop expiry); full matrix coverage for spells/visibility states is still pending.
- Ranged strike MVP is implemented (bow path, range increment penalties, grid LoS + simple cover AC, concealment flat-check misses via `Concealed`, `Volley` penalty, weapon-aware strike targeting, and crit math support for `Deadly`/`Fatal`), but advanced ranged rules remain deferred: ammo, reload, hidden/undetected and richer visibility states, volley-info preview UX, striking-rune scaling for crit traits, and crit specialization effects.
- Delay UX is currently hybrid by design: Owlcat-style planned insertion is primary, while manual inter-turn `Return`/`Skip` controls are retained as fallback for non-planned delayed actors.
- `DelayUiOrchestrator` is implemented but currently optional in scene wiring; fallback subscriptions remain active when orchestrator is absent/disabled.
- `ModalReactionPolicy` is the runtime default for both controllers; `AutoShieldBlockPolicy` remains in code for tests and simple synchronous policy scenarios.
- Combat round regression deadlock assertions now combine lock duration with turn-progress signals to reduce CI timing flakes while still detecting real stuck locks.
- PF2e feat/rule-engine architecture is intentionally deferred: no generic `FeatDefinition`/rule-engine/modifier-stack runtime is built yet. Current guardrails are documented (modifier `{value,type,source}`, capability grants separate, `EntityData` state + resolver context split) and should be implemented only when concrete gameplay features require them (second typed-bonus conflict, first feat-granted capability, etc.).
- `TargetingHintControllerTests` avoid compile-time `TMPro` dependency via reflection because the EditMode test asmdef does not reference `Unity.TextMeshPro`; keep that pattern unless the asmdef dependency is intentionally updated.
- Duplicate-looking armor asset naming (`GoblinArmor_.asset`) should be normalized later.
- Legacy forwarder stubs (`TurnManagerLogForwarder`, `TurnManagerTypedForwarder`) were removed from scenes and code; turn/combat typed flow is direct `TurnManager -> CombatEventBus`.

## Next 3 Recommended Tasks (Small, High Value)
1. Add dedicated scene/autofix regression coverage to assert `DelayUiOrchestrator` auto-create path and field wiring details (not only component presence).
2. Add explicit UI path for non-planned Delay (manual-delay click flow) so expiry tests can be fully pointer-driven end-to-end without API setup.
3. Decide whether combat log should remain hard-capped (`maxLines=80`) or gain optional extended history (virtualized scrollback/export) before content scale increases.

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
