using System;
using System.Collections.Generic;
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
        HealSingle = 10, // future: ally
        Aid = 11,        // explicit mode: choose ally in reach
        ReadyStrike = 12, // explicit mode: choose enemy for readied strike trigger
        Jump = 13,       // explicit mode: choose landing cell
        ForceBarrage = 14,
        ElectricArc = 15,
        Snowball = 16,
        Fear = 17,
        HarmSingle = 18,
        Step = 19
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
        public bool IsSpellTargetingActive => ActiveMode == TargetingMode.ForceBarrage || ActiveMode == TargetingMode.ElectricArc || ActiveMode == TargetingMode.Snowball || ActiveMode == TargetingMode.Fear || ActiveMode == TargetingMode.HealSingle || ActiveMode == TargetingMode.HarmSingle || ActiveMode == TargetingMode.SpellAoE;
        public SpellId? ActiveSpellId => ActiveMode switch
        {
            TargetingMode.ForceBarrage => SpellId.ForceBarrage,
            TargetingMode.ElectricArc => SpellId.ElectricArc,
            TargetingMode.Snowball => SpellId.Snowball,
            TargetingMode.Fear => SpellId.Fear,
            TargetingMode.HealSingle => SpellId.Heal,
            TargetingMode.HarmSingle => SpellId.Harm,
            TargetingMode.SpellAoE => _spellAoESpellId,
            _ => null
        };
        public bool CanConfirmSpellTargeting => ActiveMode switch
        {
            TargetingMode.ForceBarrage => _onForceBarrageConfirmed != null && _forceBarrageTargets.Count > 0 && _forceBarrageTargets.Count <= _forceBarrageShardCount,
            TargetingMode.ElectricArc => _onElectricArcConfirmed != null && _electricArcTargets.Count > 0,
            TargetingMode.Snowball => _onSnowballConfirmed != null && _snowballTargets.Count > 0,
            TargetingMode.Fear => _onFearConfirmed != null && _fearTargets.Count > 0,
            TargetingMode.HealSingle => _onHealConfirmed != null && _healTargets.Count > 0,
            TargetingMode.HarmSingle => _onHarmConfirmed != null && _harmTargets.Count > 0,
            TargetingMode.SpellAoE => _onSpellAoEConfirmed != null && _spellAoESelectedCell.HasValue,
            _ => false
        };
        public bool CanUndoSpellSelection => ActiveMode switch
        {
            TargetingMode.ForceBarrage => _forceBarrageTargets.Count > 0,
            TargetingMode.ElectricArc => _electricArcTargets.Count > 0,
            TargetingMode.Snowball => _snowballTargets.Count > 0,
            TargetingMode.Fear => _fearTargets.Count > 0,
            TargetingMode.HealSingle => _healTargets.Count > 0,
            TargetingMode.HarmSingle => _harmTargets.Count > 0,
            TargetingMode.SpellAoE => _spellAoESelectedCell.HasValue,
            _ => false
        };
        public int ForceBarrageAssignedShardCount => ActiveMode == TargetingMode.ForceBarrage ? _forceBarrageTargets.Count : 0;
        public int ForceBarrageShardCapacity => ActiveMode == TargetingMode.ForceBarrage ? _forceBarrageShardCount : 0;
        public int ForceBarrageRemainingShardCount => ActiveMode == TargetingMode.ForceBarrage
            ? Mathf.Max(0, _forceBarrageShardCount - _forceBarrageTargets.Count)
            : 0;
        public int ElectricArcSelectedTargetCount => ActiveMode == TargetingMode.ElectricArc ? _electricArcTargets.Count : 0;
        public int SnowballSelectedTargetCount => ActiveMode == TargetingMode.Snowball ? _snowballTargets.Count : 0;
        public int FearSelectedTargetCount => ActiveMode == TargetingMode.Fear ? _fearTargets.Count : 0;
        public int HealSelectedTargetCount => ActiveMode == TargetingMode.HealSingle ? _healTargets.Count : 0;
        public int HealActionCount => ActiveMode == TargetingMode.HealSingle ? _healActionCount : 0;
        public int HarmSelectedTargetCount => ActiveMode == TargetingMode.HarmSingle ? _harmTargets.Count : 0;
        public int HarmActionCount => ActiveMode == TargetingMode.HarmSingle ? _harmActionCount : 0;
        public int SpellAoEActionCount => ActiveMode == TargetingMode.SpellAoE ? _spellAoEActionCount : 0;
        public bool HasSelectedSpellAreaCell => ActiveMode == TargetingMode.SpellAoE && _spellAoESelectedCell.HasValue;
        public Vector3Int? SelectedSpellAreaCell => ActiveMode == TargetingMode.SpellAoE ? _spellAoESelectedCell : null;
        public IReadOnlyList<EntityHandle> ForceBarrageAssignedTargets => _forceBarrageTargets;
        public IReadOnlyList<EntityHandle> ElectricArcSelectedTargets => _electricArcTargets;
        public IReadOnlyList<EntityHandle> SnowballSelectedTargets => _snowballTargets;
        public IReadOnlyList<EntityHandle> FearSelectedTargets => _fearTargets;
        public IReadOnlyList<EntityHandle> HealSelectedTargets => _healTargets;
        public IReadOnlyList<EntityHandle> HarmSelectedTargets => _harmTargets;
        public event Action<TargetingMode> OnModeChanged;

        // Callbacks for explicit modes (BeginTargeting).
        // NOTE: closures acceptable (called once per action, not per-frame).
        // Defer zero-alloc optimization to Phase 17 if needed.
        private Action<EntityHandle> _onEntityConfirmed;
        private Func<Vector3Int, bool> _onCellConfirmed;
        private Action _onCancelled;
        private Func<EntityHandle, RepositionTargetSelectionResult> _onRepositionTargetConfirmed;
        private Func<Vector3Int, bool> _onRepositionCellConfirmed;
        private Action _onRepositionCellCancelled;
        private Func<IReadOnlyList<EntityHandle>, bool> _onForceBarrageConfirmed;
        private Action _onForceBarrageCancelled;
        private readonly List<EntityHandle> _forceBarrageTargets = new(3);
        private int _forceBarrageShardCount;
        private Func<IReadOnlyList<EntityHandle>, bool> _onElectricArcConfirmed;
        private Action _onElectricArcCancelled;
        private readonly List<EntityHandle> _electricArcTargets = new(2);
        private Func<IReadOnlyList<EntityHandle>, bool> _onSnowballConfirmed;
        private Action _onSnowballCancelled;
        private readonly List<EntityHandle> _snowballTargets = new(1);
        private Func<IReadOnlyList<EntityHandle>, bool> _onFearConfirmed;
        private Action _onFearCancelled;
        private readonly List<EntityHandle> _fearTargets = new(1);
        private Func<IReadOnlyList<EntityHandle>, bool> _onHealConfirmed;
        private Action _onHealCancelled;
        private readonly List<EntityHandle> _healTargets = new(1);
        private int _healActionCount;
        private Func<IReadOnlyList<EntityHandle>, bool> _onHarmConfirmed;
        private Action _onHarmCancelled;
        private readonly List<EntityHandle> _harmTargets = new(1);
        private int _harmActionCount;
        private Func<Vector3Int, bool> _onSpellAoEConfirmed;
        private Action _onSpellAoECancelled;
        private SpellId _spellAoESpellId = SpellId.BurningHands;
        private int _spellAoEActionCount = 2;
        private Vector3Int? _spellAoESelectedCell;
        private RepositionPhase _repositionPhase = RepositionPhase.None;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (EditorValidationGuard.ShouldSkipMissingReferenceWarnings())
                return;

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
            _onCellConfirmed = null;
            _onCancelled       = onCancelled;
            _onRepositionTargetConfirmed = null;
            _onRepositionCellConfirmed = null;
            _onRepositionCellCancelled = null;
            _onForceBarrageConfirmed = null;
            _onForceBarrageCancelled = null;
            _forceBarrageTargets.Clear();
            _forceBarrageShardCount = 0;
            _onElectricArcConfirmed = null;
            _onElectricArcCancelled = null;
            _electricArcTargets.Clear();
            _onSnowballConfirmed = null;
            _onSnowballCancelled = null;
            _snowballTargets.Clear();
            _onFearConfirmed = null;
            _onFearCancelled = null;
            _fearTargets.Clear();
            _onHealConfirmed = null;
            _onHealCancelled = null;
            _healTargets.Clear();
            _healActionCount = 0;
            _onHarmConfirmed = null;
            _onHarmCancelled = null;
            _harmTargets.Clear();
            _harmActionCount = 0;
            _onSpellAoEConfirmed = null;
            _onSpellAoECancelled = null;
            _spellAoESpellId = SpellId.BurningHands;
            _spellAoEActionCount = 2;
            _spellAoESelectedCell = null;
            _repositionPhase = mode == TargetingMode.Reposition ? RepositionPhase.SelectTarget : RepositionPhase.None;
            OnModeChanged?.Invoke(ActiveMode);
        }

        public void BeginCellTargeting(
            TargetingMode mode,
            Func<Vector3Int, bool> onCellConfirmed,
            Action onCancelled = null)
        {
            ActiveMode = mode;
            _onEntityConfirmed = null;
            _onCellConfirmed = onCellConfirmed;
            _onCancelled = onCancelled;
            _onRepositionTargetConfirmed = null;
            _onRepositionCellConfirmed = null;
            _onRepositionCellCancelled = null;
            _onForceBarrageConfirmed = null;
            _onForceBarrageCancelled = null;
            _forceBarrageTargets.Clear();
            _forceBarrageShardCount = 0;
            _onElectricArcConfirmed = null;
            _onElectricArcCancelled = null;
            _electricArcTargets.Clear();
            _onSnowballConfirmed = null;
            _onSnowballCancelled = null;
            _snowballTargets.Clear();
            _onFearConfirmed = null;
            _onFearCancelled = null;
            _fearTargets.Clear();
            _onHealConfirmed = null;
            _onHealCancelled = null;
            _healTargets.Clear();
            _healActionCount = 0;
            _onHarmConfirmed = null;
            _onHarmCancelled = null;
            _harmTargets.Clear();
            _harmActionCount = 0;
            _onSpellAoEConfirmed = null;
            _onSpellAoECancelled = null;
            _spellAoESpellId = SpellId.BurningHands;
            _spellAoESelectedCell = null;
            _repositionPhase = RepositionPhase.None;
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
            _onForceBarrageConfirmed = null;
            _onForceBarrageCancelled = null;
            _forceBarrageTargets.Clear();
            _forceBarrageShardCount = 0;
            _onElectricArcConfirmed = null;
            _onElectricArcCancelled = null;
            _electricArcTargets.Clear();
            _onSnowballConfirmed = null;
            _onSnowballCancelled = null;
            _snowballTargets.Clear();
            _onFearConfirmed = null;
            _onFearCancelled = null;
            _fearTargets.Clear();
            _onHealConfirmed = null;
            _onHealCancelled = null;
            _healTargets.Clear();
            _healActionCount = 0;
            _onHarmConfirmed = null;
            _onHarmCancelled = null;
            _harmTargets.Clear();
            _harmActionCount = 0;
            _onSpellAoEConfirmed = null;
            _onSpellAoECancelled = null;
            _spellAoESpellId = SpellId.BurningHands;
            _spellAoESelectedCell = null;
            _repositionPhase = RepositionPhase.SelectTarget;
            OnModeChanged?.Invoke(ActiveMode);
        }

        public void BeginForceBarrageTargeting(
            int shardCount,
            Func<IReadOnlyList<EntityHandle>, bool> onConfirmed,
            Action onCancelled = null)
        {
            ActiveMode = TargetingMode.ForceBarrage;
            _onEntityConfirmed = null;
            _onCellConfirmed = null;
            _onCancelled = null;
            _onRepositionTargetConfirmed = null;
            _onRepositionCellConfirmed = null;
            _onRepositionCellCancelled = null;
            _onForceBarrageConfirmed = onConfirmed;
            _onForceBarrageCancelled = onCancelled;
            _forceBarrageTargets.Clear();
            _forceBarrageShardCount = Mathf.Clamp(shardCount, 1, 3);
            _onElectricArcConfirmed = null;
            _onElectricArcCancelled = null;
            _electricArcTargets.Clear();
            _onSnowballConfirmed = null;
            _onSnowballCancelled = null;
            _snowballTargets.Clear();
            _onFearConfirmed = null;
            _onFearCancelled = null;
            _fearTargets.Clear();
            _onHealConfirmed = null;
            _onHealCancelled = null;
            _healTargets.Clear();
            _healActionCount = 0;
            _onHarmConfirmed = null;
            _onHarmCancelled = null;
            _harmTargets.Clear();
            _harmActionCount = 0;
            _onSpellAoEConfirmed = null;
            _onSpellAoECancelled = null;
            _spellAoESpellId = SpellId.BurningHands;
            _spellAoESelectedCell = null;
            _repositionPhase = RepositionPhase.None;
            OnModeChanged?.Invoke(ActiveMode);
        }

        public void BeginElectricArcTargeting(
            Func<IReadOnlyList<EntityHandle>, bool> onConfirmed,
            Action onCancelled = null)
        {
            ActiveMode = TargetingMode.ElectricArc;
            _onEntityConfirmed = null;
            _onCellConfirmed = null;
            _onCancelled = null;
            _onRepositionTargetConfirmed = null;
            _onRepositionCellConfirmed = null;
            _onRepositionCellCancelled = null;
            _onForceBarrageConfirmed = null;
            _onForceBarrageCancelled = null;
            _forceBarrageTargets.Clear();
            _forceBarrageShardCount = 0;
            _onElectricArcConfirmed = onConfirmed;
            _onElectricArcCancelled = onCancelled;
            _electricArcTargets.Clear();
            _onSnowballConfirmed = null;
            _onSnowballCancelled = null;
            _snowballTargets.Clear();
            _onFearConfirmed = null;
            _onFearCancelled = null;
            _fearTargets.Clear();
            _onHealConfirmed = null;
            _onHealCancelled = null;
            _healTargets.Clear();
            _healActionCount = 0;
            _onHarmConfirmed = null;
            _onHarmCancelled = null;
            _harmTargets.Clear();
            _harmActionCount = 0;
            _onSpellAoEConfirmed = null;
            _onSpellAoECancelled = null;
            _spellAoESpellId = SpellId.BurningHands;
            _spellAoESelectedCell = null;
            _repositionPhase = RepositionPhase.None;
            OnModeChanged?.Invoke(ActiveMode);
        }

        public void BeginSnowballTargeting(
            Func<IReadOnlyList<EntityHandle>, bool> onConfirmed,
            Action onCancelled = null)
        {
            ActiveMode = TargetingMode.Snowball;
            _onEntityConfirmed = null;
            _onCellConfirmed = null;
            _onCancelled = null;
            _onRepositionTargetConfirmed = null;
            _onRepositionCellConfirmed = null;
            _onRepositionCellCancelled = null;
            _onForceBarrageConfirmed = null;
            _onForceBarrageCancelled = null;
            _forceBarrageTargets.Clear();
            _forceBarrageShardCount = 0;
            _onElectricArcConfirmed = null;
            _onElectricArcCancelled = null;
            _electricArcTargets.Clear();
            _onSnowballConfirmed = onConfirmed;
            _onSnowballCancelled = onCancelled;
            _snowballTargets.Clear();
            _onFearConfirmed = null;
            _onFearCancelled = null;
            _fearTargets.Clear();
            _onHealConfirmed = null;
            _onHealCancelled = null;
            _healTargets.Clear();
            _healActionCount = 0;
            _onHarmConfirmed = null;
            _onHarmCancelled = null;
            _harmTargets.Clear();
            _harmActionCount = 0;
            _onSpellAoEConfirmed = null;
            _onSpellAoECancelled = null;
            _spellAoESpellId = SpellId.BurningHands;
            _spellAoESelectedCell = null;
            _repositionPhase = RepositionPhase.None;
            OnModeChanged?.Invoke(ActiveMode);
        }

        public void BeginFearTargeting(
            Func<IReadOnlyList<EntityHandle>, bool> onConfirmed,
            Action onCancelled = null)
        {
            ActiveMode = TargetingMode.Fear;
            _onEntityConfirmed = null;
            _onCellConfirmed = null;
            _onCancelled = null;
            _onRepositionTargetConfirmed = null;
            _onRepositionCellConfirmed = null;
            _onRepositionCellCancelled = null;
            _onForceBarrageConfirmed = null;
            _onForceBarrageCancelled = null;
            _forceBarrageTargets.Clear();
            _forceBarrageShardCount = 0;
            _onElectricArcConfirmed = null;
            _onElectricArcCancelled = null;
            _electricArcTargets.Clear();
            _onSnowballConfirmed = null;
            _onSnowballCancelled = null;
            _snowballTargets.Clear();
            _onFearConfirmed = onConfirmed;
            _onFearCancelled = onCancelled;
            _fearTargets.Clear();
            _onHealConfirmed = null;
            _onHealCancelled = null;
            _healTargets.Clear();
            _healActionCount = 0;
            _onHarmConfirmed = null;
            _onHarmCancelled = null;
            _harmTargets.Clear();
            _harmActionCount = 0;
            _onSpellAoEConfirmed = null;
            _onSpellAoECancelled = null;
            _spellAoESpellId = SpellId.BurningHands;
            _spellAoESelectedCell = null;
            _repositionPhase = RepositionPhase.None;
            OnModeChanged?.Invoke(ActiveMode);
        }

        public void BeginHealTargeting(
            int actionCount,
            Func<IReadOnlyList<EntityHandle>, bool> onConfirmed,
            Action onCancelled = null)
        {
            ActiveMode = TargetingMode.HealSingle;
            _onEntityConfirmed = null;
            _onCellConfirmed = null;
            _onCancelled = null;
            _onRepositionTargetConfirmed = null;
            _onRepositionCellConfirmed = null;
            _onRepositionCellCancelled = null;
            _onForceBarrageConfirmed = null;
            _onForceBarrageCancelled = null;
            _forceBarrageTargets.Clear();
            _forceBarrageShardCount = 0;
            _onElectricArcConfirmed = null;
            _onElectricArcCancelled = null;
            _electricArcTargets.Clear();
            _onSnowballConfirmed = null;
            _onSnowballCancelled = null;
            _snowballTargets.Clear();
            _onFearConfirmed = null;
            _onFearCancelled = null;
            _fearTargets.Clear();
            _onHealConfirmed = onConfirmed;
            _onHealCancelled = onCancelled;
            _healTargets.Clear();
            _healActionCount = Mathf.Clamp(actionCount, 1, 2);
            _onHarmConfirmed = null;
            _onHarmCancelled = null;
            _harmTargets.Clear();
            _harmActionCount = 0;
            _onSpellAoEConfirmed = null;
            _onSpellAoECancelled = null;
            _spellAoESpellId = SpellId.BurningHands;
            _spellAoESelectedCell = null;
            _repositionPhase = RepositionPhase.None;
            OnModeChanged?.Invoke(ActiveMode);
        }

        public void BeginHarmTargeting(
            int actionCount,
            Func<IReadOnlyList<EntityHandle>, bool> onConfirmed,
            Action onCancelled = null)
        {
            ActiveMode = TargetingMode.HarmSingle;
            _onEntityConfirmed = null;
            _onCellConfirmed = null;
            _onCancelled = null;
            _onRepositionTargetConfirmed = null;
            _onRepositionCellConfirmed = null;
            _onRepositionCellCancelled = null;
            _onForceBarrageConfirmed = null;
            _onForceBarrageCancelled = null;
            _forceBarrageTargets.Clear();
            _forceBarrageShardCount = 0;
            _onElectricArcConfirmed = null;
            _onElectricArcCancelled = null;
            _electricArcTargets.Clear();
            _onSnowballConfirmed = null;
            _onSnowballCancelled = null;
            _snowballTargets.Clear();
            _onFearConfirmed = null;
            _onFearCancelled = null;
            _fearTargets.Clear();
            _onHealConfirmed = null;
            _onHealCancelled = null;
            _healTargets.Clear();
            _healActionCount = 0;
            _onHarmConfirmed = onConfirmed;
            _onHarmCancelled = onCancelled;
            _harmTargets.Clear();
            _harmActionCount = Mathf.Clamp(actionCount, 1, 2);
            _onSpellAoEConfirmed = null;
            _onSpellAoECancelled = null;
            _spellAoESpellId = SpellId.BurningHands;
            _spellAoESelectedCell = null;
            _repositionPhase = RepositionPhase.None;
            OnModeChanged?.Invoke(ActiveMode);
        }

        public void BeginSpellAoETargeting(
            SpellId spellId,
            Func<Vector3Int, bool> onConfirmed,
            Action onCancelled = null,
            int actionCount = 0,
            Vector3Int? initialSelectedCell = null)
        {
            ActiveMode = TargetingMode.SpellAoE;
            _onEntityConfirmed = null;
            _onCellConfirmed = null;
            _onCancelled = null;
            _onRepositionTargetConfirmed = null;
            _onRepositionCellConfirmed = null;
            _onRepositionCellCancelled = null;
            _onForceBarrageConfirmed = null;
            _onForceBarrageCancelled = null;
            _forceBarrageTargets.Clear();
            _forceBarrageShardCount = 0;
            _onElectricArcConfirmed = null;
            _onElectricArcCancelled = null;
            _electricArcTargets.Clear();
            _onSnowballConfirmed = null;
            _onSnowballCancelled = null;
            _snowballTargets.Clear();
            _onFearConfirmed = null;
            _onFearCancelled = null;
            _fearTargets.Clear();
            _onHealConfirmed = null;
            _onHealCancelled = null;
            _healTargets.Clear();
            _healActionCount = 0;
            _onHarmConfirmed = null;
            _onHarmCancelled = null;
            _harmTargets.Clear();
            _harmActionCount = 0;
            _onSpellAoEConfirmed = onConfirmed;
            _onSpellAoECancelled = onCancelled;
            _spellAoESpellId = spellId;
            _spellAoEActionCount = actionCount > 0
                ? actionCount
                : SpellCatalog.Get(spellId).minActionCost;
            _spellAoESelectedCell = initialSelectedCell;
            _repositionPhase = RepositionPhase.None;
            OnModeChanged?.Invoke(ActiveMode);
        }

        public bool TryConfirmSpellTargeting()
        {
            switch (ActiveMode)
            {
                case TargetingMode.ForceBarrage:
                    if (!CanConfirmSpellTargeting)
                        return false;

                    if (_onForceBarrageConfirmed.Invoke(_forceBarrageTargets))
                    {
                        ClearTargeting();
                        return true;
                    }

                    return false;

                case TargetingMode.ElectricArc:
                    if (!CanConfirmSpellTargeting)
                        return false;

                    if (_onElectricArcConfirmed.Invoke(_electricArcTargets))
                    {
                        ClearTargeting();
                        return true;
                    }

                    return false;

                case TargetingMode.Snowball:
                    if (!CanConfirmSpellTargeting)
                        return false;

                    if (_onSnowballConfirmed.Invoke(_snowballTargets))
                    {
                        ClearTargeting();
                        return true;
                    }

                    return false;

                case TargetingMode.Fear:
                    if (!CanConfirmSpellTargeting)
                        return false;

                    if (_onFearConfirmed.Invoke(_fearTargets))
                    {
                        ClearTargeting();
                        return true;
                    }

                    return false;

                case TargetingMode.HealSingle:
                    if (!CanConfirmSpellTargeting)
                        return false;

                    if (_onHealConfirmed.Invoke(_healTargets))
                    {
                        ClearTargeting();
                        return true;
                    }

                    return false;

                case TargetingMode.HarmSingle:
                    if (!CanConfirmSpellTargeting)
                        return false;

                    if (_onHarmConfirmed.Invoke(_harmTargets))
                    {
                        ClearTargeting();
                        return true;
                    }

                    return false;

                case TargetingMode.SpellAoE:
                    if (!CanConfirmSpellTargeting || !_spellAoESelectedCell.HasValue)
                        return false;

                    if (_onSpellAoEConfirmed.Invoke(_spellAoESelectedCell.Value))
                    {
                        ClearTargeting();
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }

        public bool TryUndoLastSpellSelection()
        {
            switch (ActiveMode)
            {
                case TargetingMode.ForceBarrage:
                    if (_forceBarrageTargets.Count <= 0)
                        return false;

                    _forceBarrageTargets.RemoveAt(_forceBarrageTargets.Count - 1);
                    OnModeChanged?.Invoke(ActiveMode);
                    return true;

                case TargetingMode.ElectricArc:
                    if (_electricArcTargets.Count <= 0)
                        return false;

                    _electricArcTargets.RemoveAt(_electricArcTargets.Count - 1);
                    OnModeChanged?.Invoke(ActiveMode);
                    return true;

                case TargetingMode.Snowball:
                    if (_snowballTargets.Count <= 0)
                        return false;

                    _snowballTargets.Clear();
                    OnModeChanged?.Invoke(ActiveMode);
                    return true;

                case TargetingMode.Fear:
                    if (_fearTargets.Count <= 0)
                        return false;

                    _fearTargets.Clear();
                    OnModeChanged?.Invoke(ActiveMode);
                    return true;

                case TargetingMode.HealSingle:
                    if (_healTargets.Count <= 0)
                        return false;

                    _healTargets.Clear();
                    OnModeChanged?.Invoke(ActiveMode);
                    return true;

                case TargetingMode.HarmSingle:
                    if (_harmTargets.Count <= 0)
                        return false;

                    _harmTargets.Clear();
                    OnModeChanged?.Invoke(ActiveMode);
                    return true;

                case TargetingMode.SpellAoE:
                    if (!_spellAoESelectedCell.HasValue)
                        return false;

                    _spellAoESelectedCell = null;
                    OnModeChanged?.Invoke(ActiveMode);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Cancel targeting (Escape / turn end / combat end).</summary>
        public void CancelTargeting()
        {
            if (ActiveMode == TargetingMode.Reposition && _repositionPhase == RepositionPhase.SelectCell)
                _onRepositionCellCancelled?.Invoke();
            else if (ActiveMode == TargetingMode.ForceBarrage)
                _onForceBarrageCancelled?.Invoke();
            else if (ActiveMode == TargetingMode.ElectricArc)
                _onElectricArcCancelled?.Invoke();
            else if (ActiveMode == TargetingMode.Snowball)
                _onSnowballCancelled?.Invoke();
            else if (ActiveMode == TargetingMode.Fear)
                _onFearCancelled?.Invoke();
            else if (ActiveMode == TargetingMode.HealSingle)
                _onHealCancelled?.Invoke();
            else if (ActiveMode == TargetingMode.HarmSingle)
                _onHarmCancelled?.Invoke();
            else if (ActiveMode == TargetingMode.SpellAoE)
                _onSpellAoECancelled?.Invoke();
            else
                _onCancelled?.Invoke();
            ClearTargeting();
        }

        public bool TryPreviewSpellAreaCell(Vector3Int cell, out SpellAreaPreview preview)
        {
            if (ActiveMode != TargetingMode.SpellAoE || actionExecutor == null)
            {
                preview = SpellAreaPreview.Invalid(_spellAoESpellId, cell, TargetingFailureReason.ModeNotSupported);
                return false;
            }

            return actionExecutor.TryPreviewSpellAreaCell(_spellAoESpellId, cell, out preview);
        }

        public bool TryGetSelectedSpellAreaPreview(out SpellAreaPreview preview)
        {
            if (ActiveMode != TargetingMode.SpellAoE || !_spellAoESelectedCell.HasValue)
            {
                preview = SpellAreaPreview.Invalid(_spellAoESpellId, Vector3Int.zero, TargetingFailureReason.InvalidTarget);
                return false;
            }

            return TryPreviewSpellAreaCell(_spellAoESelectedCell.Value, out preview);
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
                case TargetingMode.ReadyStrike:
                    // Used when player explicitly selects Strike from action bar (Phase 15+).
                    // Currently not triggered via UI; contextual mode handles Strike via None.
                case TargetingMode.Trip:
                case TargetingMode.Shove:
                case TargetingMode.Grapple:
                case TargetingMode.Escape:
                case TargetingMode.Demoralize:
                case TargetingMode.Reposition:
                case TargetingMode.Aid:
                case TargetingMode.Jump:
                case TargetingMode.ForceBarrage:
                case TargetingMode.ElectricArc:
                case TargetingMode.Snowball:
                case TargetingMode.Fear:
                case TargetingMode.HealSingle:
                case TargetingMode.HarmSingle:
                case TargetingMode.SpellAoE:
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
                        else if (ActiveMode == TargetingMode.ForceBarrage)
                        {
                            if (_forceBarrageTargets.Count < _forceBarrageShardCount)
                                _forceBarrageTargets.Add(handle);

                            OnModeChanged?.Invoke(ActiveMode);
                        }
                        else if (ActiveMode == TargetingMode.ElectricArc)
                        {
                            int index = _electricArcTargets.IndexOf(handle);
                            if (index >= 0)
                                _electricArcTargets.RemoveAt(index);
                            else if (_electricArcTargets.Count < 2)
                                _electricArcTargets.Add(handle);

                            OnModeChanged?.Invoke(ActiveMode);
                        }
                        else if (ActiveMode == TargetingMode.Snowball)
                        {
                            if (_snowballTargets.Count == 1 && _snowballTargets[0] == handle)
                                _snowballTargets.Clear();
                            else
                            {
                                _snowballTargets.Clear();
                                _snowballTargets.Add(handle);
                            }

                            OnModeChanged?.Invoke(ActiveMode);
                        }
                        else if (ActiveMode == TargetingMode.Fear)
                        {
                            if (_fearTargets.Count == 1 && _fearTargets[0] == handle)
                                _fearTargets.Clear();
                            else
                            {
                                _fearTargets.Clear();
                                _fearTargets.Add(handle);
                            }

                            OnModeChanged?.Invoke(ActiveMode);
                        }
                        else if (ActiveMode == TargetingMode.HealSingle)
                        {
                            if (_healTargets.Count == 1 && _healTargets[0] == handle)
                                _healTargets.Clear();
                            else
                            {
                                _healTargets.Clear();
                                _healTargets.Add(handle);
                            }

                            OnModeChanged?.Invoke(ActiveMode);
                        }
                        else if (ActiveMode == TargetingMode.HarmSingle)
                        {
                            if (_harmTargets.Count == 1 && _harmTargets[0] == handle)
                                _harmTargets.Clear();
                            else
                            {
                                _harmTargets.Clear();
                                _harmTargets.Add(handle);
                            }

                            OnModeChanged?.Invoke(ActiveMode);
                        }
                        else
                        {
                            _onEntityConfirmed?.Invoke(handle);
                            ClearTargeting();
                        }
                    }
                    return AttachPreviewWarnings(evaluation, handle);

                // future: RangedStrike, SpellSingle, HealSingle
                default:
                    return TargetingEvaluationResult.FromFailure(TargetingFailureReason.ModeNotSupported);
            }
        }

        private TargetingEvaluationResult EvaluateExplicitEntityMode(EntityHandle handle)
        {
            if (ActiveMode == TargetingMode.HealSingle && actionExecutor != null)
            {
                var detailedHeal = actionExecutor.PreviewHealTargetDetailed(handle, _healActionCount);
                if (detailedHeal.failureReason != TargetingFailureReason.InvalidState)
                    return detailedHeal;
            }

            if (ActiveMode == TargetingMode.HarmSingle && actionExecutor != null)
            {
                var detailedHarm = actionExecutor.PreviewHarmTargetDetailed(handle, _harmActionCount);
                if (detailedHarm.failureReason != TargetingFailureReason.InvalidState)
                    return detailedHarm;
            }

            if (actionExecutor != null)
            {
                var detailed = actionExecutor.PreviewEntityTargetDetailed(ActiveMode, handle);
                if (detailed.failureReason != TargetingFailureReason.InvalidState)
                    return detailed;
            }

            // Fallback for isolated tests that construct TargetingController without PlayerActionExecutor.
            var result = ActiveMode switch
            {
                TargetingMode.Aid => ValidateAlly(handle),
                TargetingMode.ForceBarrage => ValidateCreature(handle),
                TargetingMode.ElectricArc => ValidateCreature(handle),
                TargetingMode.Snowball => ValidateCreature(handle),
                TargetingMode.Fear => ValidateCreature(handle),
                TargetingMode.HealSingle => ValidateHealTarget(handle),
                TargetingMode.HarmSingle => ValidateHarmTarget(handle),
                _ => ValidateEnemy(handle)
            };
            return result == TargetingResult.Success
                ? TargetingEvaluationResult.Success()
                : TargetingEvaluationResult.FromFailure(MapBasicResultToFailure(result));
        }

        private TargetingEvaluationResult AttachPreviewWarnings(TargetingEvaluationResult evaluation, EntityHandle target)
        {
            if (!evaluation.IsSuccess)
                return evaluation;

            if (ActiveMode != TargetingMode.Strike)
                return evaluation;

            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return evaluation;

            var actor = turnManager.CurrentEntity;
            var actorData = actor.IsValid ? entityManager.Registry.Get(actor) : null;
            var targetData = target.IsValid ? entityManager.Registry.Get(target) : null;
            if (actorData == null || targetData == null)
                return evaluation;

            if (!actorData.EquippedWeapon.IsRanged)
                return evaluation;

            TargetingWarningReason warnings = TargetingWarningReason.None;

            if (actorData.GridPosition.y == targetData.GridPosition.y && entityManager.GridData != null)
            {
                var line = StrikeLineResolver.ResolveSameElevation(
                    entityManager.GridData,
                    entityManager.Occupancy,
                    actorData.GridPosition,
                    targetData.GridPosition,
                    actor,
                    target);

                if (line.hasLineOfSight && line.coverAcBonus > 0)
                    warnings |= TargetingWarningReason.CoverAcBonus;
            }

            if (!targetData.HasCondition(ConditionType.Concealed))
                return warnings == TargetingWarningReason.None
                    ? evaluation
                    : evaluation.WithWarning(warnings);

            warnings |= TargetingWarningReason.ConcealmentFlatCheck;
            return evaluation.WithWarning(warnings);
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

                case TargetingMode.Jump:
                case TargetingMode.Step:
                    if (_onCellConfirmed != null && _onCellConfirmed.Invoke(cell))
                    {
                        ClearTargeting();
                        return TargetingResult.Success;
                    }
                    return TargetingResult.InvalidTarget;

                case TargetingMode.SpellAoE:
                    if (!TryPreviewSpellAreaCell(cell, out var spellAreaPreview))
                        return MapFailureReasonToResult(spellAreaPreview.failureReason);

                    if (_spellAoESelectedCell.HasValue && _spellAoESelectedCell.Value == cell)
                        _spellAoESelectedCell = null;
                    else
                        _spellAoESelectedCell = cell;

                    OnModeChanged?.Invoke(ActiveMode);
                    return TargetingResult.Success;

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

        private TargetingResult ValidateAlly(EntityHandle handle)
        {
            var actor      = turnManager.CurrentEntity;
            var actorData  = entityManager.Registry?.Get(actor);
            var targetData = entityManager.Registry?.Get(handle);

            if (targetData == null || actorData == null) return TargetingResult.InvalidTarget;
            if (!targetData.IsAlive)                     return TargetingResult.NotAlive;
            if (handle == actor)                         return TargetingResult.SelfTarget;
            if (targetData.Team != actorData.Team)       return TargetingResult.WrongTeam;
            return TargetingResult.Success;
        }

        private TargetingResult ValidateCreature(EntityHandle handle)
        {
            var actor = turnManager.CurrentEntity;
            var actorData = entityManager.Registry?.Get(actor);
            var targetData = entityManager.Registry?.Get(handle);

            if (targetData == null || actorData == null) return TargetingResult.InvalidTarget;
            if (!targetData.IsAlive) return TargetingResult.NotAlive;
            if (handle == actor) return TargetingResult.SelfTarget;
            return TargetingResult.Success;
        }

        private TargetingResult ValidateHealTarget(EntityHandle handle)
        {
            var actor = turnManager.CurrentEntity;
            var actorData = entityManager.Registry?.Get(actor);
            var targetData = entityManager.Registry?.Get(handle);

            if (targetData == null || actorData == null) return TargetingResult.InvalidTarget;
            if (!targetData.IsAlive) return TargetingResult.NotAlive;
            if (handle == actor) return TargetingResult.Success;
            if (targetData.VitalityAffinity == VitalityAffinity.Undead) return TargetingResult.Success;
            if (targetData.Team != actorData.Team) return TargetingResult.WrongTeam;
            return TargetingResult.Success;
        }

        private TargetingResult ValidateHarmTarget(EntityHandle handle)
        {
            var actor = turnManager.CurrentEntity;
            var actorData = entityManager.Registry?.Get(actor);
            var targetData = entityManager.Registry?.Get(handle);

            if (targetData == null || actorData == null) return TargetingResult.InvalidTarget;
            if (!targetData.IsAlive) return TargetingResult.NotAlive;
            if (targetData.VitalityAffinity == VitalityAffinity.Undead) return TargetingResult.Success;
            if (handle == actor) return TargetingResult.WrongTeam;
            if (targetData.Team == actorData.Team) return TargetingResult.WrongTeam;
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

        private static TargetingResult MapFailureReasonToResult(TargetingFailureReason reason)
        {
            return reason switch
            {
                TargetingFailureReason.None => TargetingResult.Success,
                TargetingFailureReason.InvalidTarget => TargetingResult.InvalidTarget,
                TargetingFailureReason.NotAlive => TargetingResult.NotAlive,
                TargetingFailureReason.SelfTarget => TargetingResult.SelfTarget,
                TargetingFailureReason.WrongTeam => TargetingResult.WrongTeam,
                TargetingFailureReason.OutOfRange => TargetingResult.OutOfRange,
                TargetingFailureReason.ModeNotSupported => TargetingResult.ModeNotSupported,
                _ => TargetingResult.InvalidTarget
            };
        }

        private void ClearTargeting()
        {
            bool modeChanged = ActiveMode != TargetingMode.None;
            ActiveMode         = TargetingMode.None;
            _onEntityConfirmed = null;
            _onCellConfirmed = null;
            _onCancelled       = null;
            _onRepositionTargetConfirmed = null;
            _onRepositionCellConfirmed = null;
            _onRepositionCellCancelled = null;
            _onForceBarrageConfirmed = null;
            _onForceBarrageCancelled = null;
            _forceBarrageTargets.Clear();
            _forceBarrageShardCount = 0;
            _onElectricArcConfirmed = null;
            _onElectricArcCancelled = null;
            _electricArcTargets.Clear();
            _onSnowballConfirmed = null;
            _onSnowballCancelled = null;
            _snowballTargets.Clear();
            _onFearConfirmed = null;
            _onFearCancelled = null;
            _fearTargets.Clear();
            _onHealConfirmed = null;
            _onHealCancelled = null;
            _healTargets.Clear();
            _healActionCount = 0;
            _onHarmConfirmed = null;
            _onHarmCancelled = null;
            _harmTargets.Clear();
            _harmActionCount = 0;
            _onSpellAoEConfirmed = null;
            _onSpellAoECancelled = null;
            _spellAoESpellId = SpellId.BurningHands;
            _spellAoESelectedCell = null;
            _repositionPhase = RepositionPhase.None;
            if (modeChanged)
                OnModeChanged?.Invoke(TargetingMode.None);
        }
    }
}
