using System;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.TurnSystem
{
    public enum TargetingMode
    {
        None = 0,     // contextual (enemy→Strike, cell→Stride)
        Strike = 1,   // explicit mode: click on enemy (weapon-aware melee/ranged)
        Trip = 2,     // explicit mode: click enemy
        Shove = 3,    // explicit mode: click enemy
        Grapple = 4,  // explicit mode: click enemy
        Escape = 5,   // explicit mode: click grappler (enemy)
        Demoralize = 6, // explicit mode: click enemy
        Reposition = 7, // two-step: target enemy, then destination cell
        SpellSingle = 8, // future: single target
        SpellAoE = 9,    // future: cell + template
        HealSingle = 10  // future: ally
    }

    public enum TargetingResult
    {
        Success,
        InvalidTarget,    // null / data not found
        NotAlive,         // target is dead
        SelfTarget,       // cannot attack self
        WrongTeam,        // wrong target type for current mode
        OutOfRange,       // future: range check
        ModeNotSupported  // mode doesn't support this click type
    }

    public enum RepositionTargetSelectionResult
    {
        Rejected = 0,
        ResolvedAndClear = 1,
        EnterCellSelection = 2
    }

    /// <summary>
    /// Routes entity/cell clicks to the correct action based on ActiveMode.
    /// TurnInputController delegates here after basic guards (IsPlayerTurn, IsBusy).
    /// Inspector-only wiring.
    /// </summary>
    public class TargetingController : MonoBehaviour
    {
        private enum RepositionPhase
        {
            None = 0,
            SelectTarget = 1,
            SelectCell = 2
        }

        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private PlayerActionExecutor actionExecutor;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private CombatEventBus eventBus;

        public TargetingMode ActiveMode { get; private set; } = TargetingMode.None;
        public bool IsRepositionSelectingCell => ActiveMode == TargetingMode.Reposition && _repositionPhase == RepositionPhase.SelectCell;
        public bool IsRepositionSelectingTarget => ActiveMode == TargetingMode.Reposition && _repositionPhase == RepositionPhase.SelectTarget;
        public event Action<TargetingMode> OnModeChanged;

        // Callbacks for explicit modes (BeginTargeting).
        // NOTE: closures acceptable (called once per action, not per-frame).
        // Defer zero-alloc optimization to Phase 17 if needed.
        private Action<EntityHandle> _onEntityConfirmed;
        private Action _onCancelled;
        private Func<EntityHandle, RepositionTargetSelectionResult> _onRepositionTargetConfirmed;
        private Func<Vector3Int, bool> _onRepositionCellConfirmed;
        private Action _onRepositionCellCancelled;
        private RepositionPhase _repositionPhase = RepositionPhase.None;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (actionExecutor == null) Debug.LogError("[TargetingController] Missing PlayerActionExecutor", this);
            if (entityManager  == null) Debug.LogError("[TargetingController] Missing EntityManager", this);
            if (turnManager    == null) Debug.LogError("[TargetingController] Missing TurnManager", this);
            if (eventBus       == null) Debug.LogError("[TargetingController] Missing CombatEventBus", this);
        }
#endif

        private void OnEnable()
        {
            if (turnManager == null || eventBus == null)
            {
                Debug.LogError("[TargetingController] Missing dependencies", this);
                enabled = false;
                return;
            }

            eventBus.OnTurnEndedTyped += OnTurnEnded;
            eventBus.OnCombatEndedTyped += OnCombatEnded;
        }

        private void OnDisable()
        {
            if (eventBus == null) return;
            eventBus.OnTurnEndedTyped -= OnTurnEnded;
            eventBus.OnCombatEndedTyped -= OnCombatEnded;
        }

        private void OnTurnEnded(in TurnEndedEvent e) => ClearTargeting();
        private void OnCombatEnded(in CombatEndedEvent e) => ClearTargeting();

        // — Public API —

        /// <summary>
        /// Enter explicit targeting mode (called from Action Bar UI / hotkey / ability).
        /// NOTE: callbacks may be lambdas — acceptable (1 call per action).
        /// </summary>
        public void BeginTargeting(TargetingMode mode,
                                   Action<EntityHandle> onConfirmed = null,
                                   Action onCancelled = null)
        {
            ActiveMode         = mode;
            _onEntityConfirmed = onConfirmed;
            _onCancelled       = onCancelled;
            _onRepositionTargetConfirmed = null;
            _onRepositionCellConfirmed = null;
            _onRepositionCellCancelled = null;
            _repositionPhase = mode == TargetingMode.Reposition ? RepositionPhase.SelectTarget : RepositionPhase.None;
            OnModeChanged?.Invoke(ActiveMode);
        }

        /// <summary>
        /// Begin two-step Reposition targeting. Target selection happens first; the target callback decides whether
        /// to resolve immediately or enter destination-cell selection.
        /// </summary>
        public void BeginRepositionTargeting(
            Func<EntityHandle, RepositionTargetSelectionResult> onTargetConfirmed,
            Func<Vector3Int, bool> onCellConfirmed,
            Action onCancelled = null,
            Action onCellPhaseCancelled = null)
        {
            ActiveMode = TargetingMode.Reposition;
            _onEntityConfirmed = null;
            _onCancelled = onCancelled;
            _onRepositionTargetConfirmed = onTargetConfirmed;
            _onRepositionCellConfirmed = onCellConfirmed;
            _onRepositionCellCancelled = onCellPhaseCancelled;
            _repositionPhase = RepositionPhase.SelectTarget;
            OnModeChanged?.Invoke(ActiveMode);
        }

        /// <summary>Cancel targeting (Escape / turn end / combat end).</summary>
        public void CancelTargeting()
        {
            if (ActiveMode == TargetingMode.Reposition && _repositionPhase == RepositionPhase.SelectCell)
                _onRepositionCellCancelled?.Invoke();
            else
                _onCancelled?.Invoke();
            ClearTargeting();
        }

        /// <summary>
        /// Called by TurnInputController on entity click.
        /// Returns TargetingResult for optional UI feedback (Phase 15).
        /// </summary>
        public TargetingResult TryConfirmEntity(EntityHandle handle)
        {
            return EvaluateEntity(handle, executeOnSuccess: true);
        }

        /// <summary>
        /// Non-mutating validation for UI feedback. Uses the same rules path as TryConfirmEntity.
        /// Does not invoke callbacks and does not change targeting mode.
        /// </summary>
        public TargetingResult PreviewEntity(EntityHandle handle)
        {
            return PreviewEntityDetailed(handle).result;
        }

        /// <summary>
        /// Non-mutating detailed preview for UI hinting. Uses the same validation core as TryConfirmEntity.
        /// Does not invoke callbacks and does not change targeting mode.
        /// </summary>
        public TargetingEvaluationResult PreviewEntityDetailed(EntityHandle handle)
        {
            return EvaluateEntityDetailed(handle, executeOnSuccess: false);
        }

        /// <summary>
        /// UI helper for Strike wording only (reach vs range). Validation still uses PreviewEntityDetailed.
        /// </summary>
        public bool IsCurrentStrikeWeaponRanged()
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return false;

            var actor = turnManager.CurrentEntity;
            var actorData = actor.IsValid ? entityManager.Registry.Get(actor) : null;
            if (actorData == null) return false;

            return actorData.EquippedWeapon.IsRanged;
        }

        private TargetingResult EvaluateEntity(EntityHandle handle, bool executeOnSuccess)
        {
            return EvaluateEntityDetailed(handle, executeOnSuccess).result;
        }

        private TargetingEvaluationResult EvaluateEntityDetailed(EntityHandle handle, bool executeOnSuccess)
        {
            switch (ActiveMode)
            {
                case TargetingMode.None:
                    if (!executeOnSuccess)
                        return TargetingEvaluationResult.FromFailure(TargetingFailureReason.ModeNotSupported);

                    var contextual = HandleContextualEntity(handle);
                    return contextual == TargetingResult.Success
                        ? TargetingEvaluationResult.Success()
                        : TargetingEvaluationResult.FromFailure(MapBasicResultToFailure(contextual));

                case TargetingMode.Strike:
                    // Used when player explicitly selects Strike from action bar (Phase 15+).
                    // Currently not triggered via UI; contextual mode handles Strike via None.
                case TargetingMode.Trip:
                case TargetingMode.Shove:
                case TargetingMode.Grapple:
                case TargetingMode.Escape:
                case TargetingMode.Demoralize:
                case TargetingMode.Reposition:
                    if (ActiveMode == TargetingMode.Reposition && _repositionPhase == RepositionPhase.SelectCell)
                        return TargetingEvaluationResult.FromFailure(TargetingFailureReason.ModeNotSupported);

                    var evaluation = EvaluateExplicitEntityMode(handle);
                    if (executeOnSuccess && evaluation.result == TargetingResult.Success)
                    {
                        if (ActiveMode == TargetingMode.Reposition)
                        {
                            var result = _onRepositionTargetConfirmed != null
                                ? _onRepositionTargetConfirmed.Invoke(handle)
                                : RepositionTargetSelectionResult.Rejected;

                            switch (result)
                            {
                                case RepositionTargetSelectionResult.EnterCellSelection:
                                    _repositionPhase = RepositionPhase.SelectCell;
                                    // Same external mode, but listeners (hint/tint UX) need a refresh for cell phase.
                                    OnModeChanged?.Invoke(ActiveMode);
                                    break;

                                case RepositionTargetSelectionResult.ResolvedAndClear:
                                    ClearTargeting();
                                    break;

                                case RepositionTargetSelectionResult.Rejected:
                                default:
                                    // Stay in SelectTarget. Validation passed but callback rejected due to runtime state race.
                                    break;
                            }
                        }
                        else
                        {
                            _onEntityConfirmed?.Invoke(handle);
                            ClearTargeting();
                        }
                    }
                    return evaluation;

                // future: RangedStrike, SpellSingle, HealSingle
                default:
                    return TargetingEvaluationResult.FromFailure(TargetingFailureReason.ModeNotSupported);
            }
        }

        private TargetingEvaluationResult EvaluateExplicitEntityMode(EntityHandle handle)
        {
            if (actionExecutor != null)
            {
                var detailed = actionExecutor.PreviewEntityTargetDetailed(ActiveMode, handle);
                if (detailed.failureReason != TargetingFailureReason.InvalidState)
                    return detailed;
            }

            // Fallback for isolated tests that construct TargetingController without PlayerActionExecutor.
            var result = ValidateEnemy(handle);
            return result == TargetingResult.Success
                ? TargetingEvaluationResult.Success()
                : TargetingEvaluationResult.FromFailure(MapBasicResultToFailure(result));
        }

        /// <summary>
        /// Called by TurnInputController on cell click.
        /// Returns TargetingResult for optional UI feedback (Phase 15).
        /// </summary>
        public TargetingResult TryConfirmCell(Vector3Int cell)
        {
            switch (ActiveMode)
            {
                case TargetingMode.None:
                    // Default: Stride. Future actions (Stand, Step, Interact) added as
                    // new TargetingModes or via cell context menu.
                    actionExecutor.TryExecuteStrideToCell(cell);
                    return TargetingResult.Success;

                case TargetingMode.Reposition:
                    if (_repositionPhase != RepositionPhase.SelectCell)
                        return TargetingResult.ModeNotSupported;

                    if (_onRepositionCellConfirmed != null && _onRepositionCellConfirmed.Invoke(cell))
                    {
                        ClearTargeting();
                        return TargetingResult.Success;
                    }

                    return TargetingResult.InvalidTarget;

                // future: SpellAoE (place template at cell)
                default:
                    return TargetingResult.ModeNotSupported;
            }
        }

        // — Private —

        private TargetingResult HandleContextualEntity(EntityHandle handle)
        {
            var actor      = turnManager.CurrentEntity;
            var actorData  = entityManager.Registry?.Get(actor);
            var targetData = entityManager.Registry?.Get(handle);

            if (targetData == null || actorData == null) return TargetingResult.InvalidTarget;
            if (!targetData.IsAlive)                     return TargetingResult.NotAlive;
            if (handle == actor)                         return TargetingResult.SelfTarget;

            if (targetData.Team != actorData.Team)
            {
                actionExecutor.TryExecuteStrike(handle);
                return TargetingResult.Success;
            }

            // Ally: future (inspect / heal)
            return TargetingResult.WrongTeam;
        }

        private TargetingResult ValidateEnemy(EntityHandle handle)
        {
            var actor      = turnManager.CurrentEntity;
            var actorData  = entityManager.Registry?.Get(actor);
            var targetData = entityManager.Registry?.Get(handle);

            if (targetData == null || actorData == null) return TargetingResult.InvalidTarget;
            if (!targetData.IsAlive)                     return TargetingResult.NotAlive;
            if (handle == actor)                         return TargetingResult.SelfTarget;
            if (targetData.Team == actorData.Team)       return TargetingResult.WrongTeam;
            return TargetingResult.Success;
        }

        private static TargetingFailureReason MapBasicResultToFailure(TargetingResult result)
        {
            return result switch
            {
                TargetingResult.Success => TargetingFailureReason.None,
                TargetingResult.InvalidTarget => TargetingFailureReason.InvalidTarget,
                TargetingResult.NotAlive => TargetingFailureReason.NotAlive,
                TargetingResult.SelfTarget => TargetingFailureReason.SelfTarget,
                TargetingResult.WrongTeam => TargetingFailureReason.WrongTeam,
                TargetingResult.OutOfRange => TargetingFailureReason.OutOfRange,
                TargetingResult.ModeNotSupported => TargetingFailureReason.ModeNotSupported,
                _ => TargetingFailureReason.InvalidTarget
            };
        }

        private void ClearTargeting()
        {
            bool modeChanged = ActiveMode != TargetingMode.None;
            ActiveMode         = TargetingMode.None;
            _onEntityConfirmed = null;
            _onCancelled       = null;
            _onRepositionTargetConfirmed = null;
            _onRepositionCellConfirmed = null;
            _onRepositionCellCancelled = null;
            _repositionPhase = RepositionPhase.None;
            if (modeChanged)
                OnModeChanged?.Invoke(TargetingMode.None);
        }
    }
}
