using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Text hint panel for targeting modes. Shows mode prompt with no hover and valid/invalid reason text on hover.
    /// Uses TargetingController.PreviewEntityDetailed to avoid preview/confirm drift.
    /// </summary>
    public class TargetingHintController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private TargetingController targetingController;

        [Header("UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Image backgroundImage;

        [Header("Style")]
        [SerializeField] private Color infoColor = new Color(0.9f, 0.9f, 0.95f, 1f);
        [SerializeField] private Color validColor = new Color(0.45f, 1f, 0.55f, 1f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.55f, 0.5f, 1f);
        [SerializeField] private bool hideWhenModeNone = true;

        private EntityHandle? hoveredEntity;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null) Debug.LogWarning("[TargetingHint] Missing CombatEventBus", this);
            if (turnManager == null) Debug.LogWarning("[TargetingHint] Missing TurnManager", this);
            if (gridManager == null) Debug.LogWarning("[TargetingHint] Missing GridManager", this);
            if (targetingController == null) Debug.LogWarning("[TargetingHint] Missing TargetingController", this);
            if (canvasGroup == null) Debug.LogWarning("[TargetingHint] Missing CanvasGroup", this);
            if (hintText == null) Debug.LogWarning("[TargetingHint] Missing hintText", this);
        }
#endif

        private void Awake()
        {
            HidePanel();
        }

        private void OnEnable()
        {
            if (eventBus == null || turnManager == null || gridManager == null || targetingController == null || canvasGroup == null || hintText == null)
            {
                Debug.LogError("[TargetingHint] Missing dependencies", this);
                enabled = false;
                return;
            }

            hoveredEntity = gridManager.HoveredEntity;

            targetingController.OnModeChanged += HandleModeChanged;
            gridManager.OnEntityHovered += HandleEntityHovered;
            gridManager.OnEntityUnhovered += HandleEntityUnhovered;

            eventBus.OnCombatStartedTyped += HandleCombatStarted;
            eventBus.OnCombatEndedTyped += HandleCombatEnded;
            eventBus.OnTurnStartedTyped += HandleTurnStarted;
            eventBus.OnTurnEndedTyped += HandleTurnEnded;
            eventBus.OnActionsChangedTyped += HandleActionsChanged;
            eventBus.OnConditionChangedTyped += HandleConditionChanged;
            eventBus.OnEntityMovedTyped += HandleEntityMoved;

            RefreshHint();
        }

        private void OnDisable()
        {
            if (targetingController != null)
                targetingController.OnModeChanged -= HandleModeChanged;

            if (gridManager != null)
            {
                gridManager.OnEntityHovered -= HandleEntityHovered;
                gridManager.OnEntityUnhovered -= HandleEntityUnhovered;
            }

            if (eventBus != null)
            {
                eventBus.OnCombatStartedTyped -= HandleCombatStarted;
                eventBus.OnCombatEndedTyped -= HandleCombatEnded;
                eventBus.OnTurnStartedTyped -= HandleTurnStarted;
                eventBus.OnTurnEndedTyped -= HandleTurnEnded;
                eventBus.OnActionsChangedTyped -= HandleActionsChanged;
                eventBus.OnConditionChangedTyped -= HandleConditionChanged;
                eventBus.OnEntityMovedTyped -= HandleEntityMoved;
            }

            HidePanel();
        }

        private void HandleModeChanged(TargetingMode mode)
        {
            RefreshHint();
        }

        private void HandleEntityHovered(EntityHandle handle)
        {
            hoveredEntity = handle;
            RefreshHint();
        }

        private void HandleEntityUnhovered()
        {
            hoveredEntity = null;
            RefreshHint();
        }

        private void HandleCombatStarted(in CombatStartedEvent e)
        {
            RefreshHint();
        }

        private void HandleCombatEnded(in CombatEndedEvent e)
        {
            hoveredEntity = null;
            HidePanel();
        }

        private void HandleTurnStarted(in TurnStartedEvent e)
        {
            RefreshHint();
        }

        private void HandleTurnEnded(in TurnEndedEvent e)
        {
            hoveredEntity = null;
            HidePanel();
        }

        private void HandleActionsChanged(in ActionsChangedEvent e)
        {
            if (IsTargetingActive())
                RefreshHint();
        }

        private void HandleConditionChanged(in ConditionChangedEvent e)
        {
            if (IsTargetingActive())
                RefreshHint();
        }

        private void HandleEntityMoved(in EntityMovedEvent e)
        {
            if (IsTargetingActive())
                RefreshHint();
        }

        private bool IsTargetingActive()
        {
            return targetingController != null && targetingController.ActiveMode != TargetingMode.None;
        }

        private void RefreshHint()
        {
            if (targetingController == null)
            {
                HidePanel();
                return;
            }

            var mode = targetingController.ActiveMode;
            if (mode == TargetingMode.None && hideWhenModeNone)
            {
                HidePanel();
                return;
            }

            TargetingHintMessage message;
            bool strikeIsRanged = mode == TargetingMode.Strike && targetingController.IsCurrentStrikeWeaponRanged();
            if (mode != TargetingMode.None && hoveredEntity.HasValue && hoveredEntity.Value.IsValid)
            {
                var evaluation = targetingController.PreviewEntityDetailed(hoveredEntity.Value);
                message = TargetingReasonFormatter.ForPreview(mode, evaluation, strikeIsRanged);
            }
            else
            {
                message = TargetingReasonFormatter.ForModeNoHover(mode, strikeIsRanged);
            }

            ApplyMessage(message);
        }

        private void ApplyMessage(TargetingHintMessage message)
        {
            if (message.IsHidden)
            {
                HidePanel();
                return;
            }

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            hintText.text = message.Text;
            hintText.color = message.Tone switch
            {
                TargetingHintTone.Valid => validColor,
                TargetingHintTone.Invalid => invalidColor,
                _ => infoColor
            };

            if (backgroundImage != null && !backgroundImage.enabled)
                backgroundImage.enabled = true;
        }

        private void HidePanel()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (hintText != null)
                hintText.text = string.Empty;
        }
    }
}
