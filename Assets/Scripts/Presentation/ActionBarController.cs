using UnityEngine;
using UnityEngine.UI;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Bottom-center combat action bar (MVP fixed slots).
    /// Event-driven via CombatEventBus + TargetingController.OnModeChanged.
    /// Reads current state from TurnManager / EntityManager / PlayerActionExecutor on refresh.
    /// </summary>
    public class ActionBarController : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private PlayerActionExecutor actionExecutor;
        [SerializeField] private TargetingController targetingController;

        [Header("UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button strikeButton;
        [SerializeField] private Button tripButton;
        [SerializeField] private Button shoveButton;
        [SerializeField] private Button grappleButton;
        [SerializeField] private Button demoralizeButton;
        [SerializeField] private Button escapeButton;
        [SerializeField] private Button raiseShieldButton;
        [SerializeField] private Button standButton;

        [Header("Highlights (optional overlays)")]
        [SerializeField] private Image strikeHighlight;
        [SerializeField] private Image tripHighlight;
        [SerializeField] private Image shoveHighlight;
        [SerializeField] private Image grappleHighlight;
        [SerializeField] private Image demoralizeHighlight;
        [SerializeField] private Image escapeHighlight;
        [SerializeField] private Image raiseShieldHighlight;
        [SerializeField] private Image standHighlight;

        private bool buttonListenersBound;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null) Debug.LogError("[ActionBar] Missing CombatEventBus", this);
            if (entityManager == null) Debug.LogError("[ActionBar] Missing EntityManager", this);
            if (turnManager == null) Debug.LogError("[ActionBar] Missing TurnManager", this);
            if (actionExecutor == null) Debug.LogError("[ActionBar] Missing PlayerActionExecutor", this);
            if (targetingController == null) Debug.LogError("[ActionBar] Missing TargetingController", this);

            if (canvasGroup == null) Debug.LogWarning("[ActionBar] Missing CanvasGroup", this);

            if (strikeButton == null) Debug.LogWarning("[ActionBar] strikeButton not assigned", this);
            if (tripButton == null) Debug.LogWarning("[ActionBar] tripButton not assigned", this);
            if (shoveButton == null) Debug.LogWarning("[ActionBar] shoveButton not assigned", this);
            if (grappleButton == null) Debug.LogWarning("[ActionBar] grappleButton not assigned", this);
            if (demoralizeButton == null) Debug.LogWarning("[ActionBar] demoralizeButton not assigned", this);
            if (escapeButton == null) Debug.LogWarning("[ActionBar] escapeButton not assigned", this);
            if (raiseShieldButton == null) Debug.LogWarning("[ActionBar] raiseShieldButton not assigned", this);
            if (standButton == null) Debug.LogWarning("[ActionBar] standButton not assigned", this);
        }
#endif

        private void Awake()
        {
            EnsureButtonListenersBound();

            SetCombatVisible(false);
            SetAllInteractable(false);
            ClearAllHighlights();
        }

        private void OnEnable()
        {
            EnsureButtonListenersBound();

            if (eventBus == null || entityManager == null || turnManager == null || actionExecutor == null || targetingController == null)
            {
                Debug.LogError("[ActionBar] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnCombatStartedTyped += HandleCombatStarted;
            eventBus.OnCombatEndedTyped += HandleCombatEnded;
            eventBus.OnTurnStartedTyped += HandleTurnStarted;
            eventBus.OnTurnEndedTyped += HandleTurnEnded;
            eventBus.OnActionsChangedTyped += HandleActionsChanged;
            eventBus.OnConditionChangedTyped += HandleConditionChanged;
            eventBus.OnShieldRaisedTyped += HandleShieldRaised;
            targetingController.OnModeChanged += HandleModeChanged;

            HandleModeChanged(targetingController.ActiveMode);
            RefreshAvailability();
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                eventBus.OnCombatStartedTyped -= HandleCombatStarted;
                eventBus.OnCombatEndedTyped -= HandleCombatEnded;
                eventBus.OnTurnStartedTyped -= HandleTurnStarted;
                eventBus.OnTurnEndedTyped -= HandleTurnEnded;
                eventBus.OnActionsChangedTyped -= HandleActionsChanged;
                eventBus.OnConditionChangedTyped -= HandleConditionChanged;
                eventBus.OnShieldRaisedTyped -= HandleShieldRaised;
            }

            if (targetingController != null)
                targetingController.OnModeChanged -= HandleModeChanged;
        }

        private void EnsureButtonListenersBound()
        {
            if (buttonListenersBound) return;

            int boundCount = 0;
            boundCount += BindButton(strikeButton, OnStrikeClicked);
            boundCount += BindButton(tripButton, OnTripClicked);
            boundCount += BindButton(shoveButton, OnShoveClicked);
            boundCount += BindButton(grappleButton, OnGrappleClicked);
            boundCount += BindButton(demoralizeButton, OnDemoralizeClicked);
            boundCount += BindButton(escapeButton, OnEscapeClicked);
            boundCount += BindButton(raiseShieldButton, OnRaiseShieldClicked);
            boundCount += BindButton(standButton, OnStandClicked);

            if (boundCount > 0)
                buttonListenersBound = true;
        }

        private int BindButton(Button button, UnityEngine.Events.UnityAction handler)
        {
            if (button == null || handler == null) return 0;
            button.onClick.AddListener(handler);
            return 1;
        }

        private void HandleCombatStarted(in CombatStartedEvent e)
        {
            SetCombatVisible(true);
            RefreshAvailability();
            HandleModeChanged(targetingController != null ? targetingController.ActiveMode : TargetingMode.None);
        }

        private void HandleCombatEnded(in CombatEndedEvent e)
        {
            if (targetingController != null && targetingController.ActiveMode != TargetingMode.None)
                targetingController.CancelTargeting();

            SetCombatVisible(false);
            SetAllInteractable(false);
            ClearAllHighlights();
        }

        private void HandleTurnStarted(in TurnStartedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleTurnEnded(in TurnEndedEvent e)
        {
            if (targetingController != null && targetingController.ActiveMode != TargetingMode.None)
                targetingController.CancelTargeting();

            SetAllInteractable(false);
            ClearAllHighlights();
        }

        private void HandleActionsChanged(in ActionsChangedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleConditionChanged(in ConditionChangedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleShieldRaised(in ShieldRaisedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleModeChanged(TargetingMode mode)
        {
            SetHighlight(strikeHighlight, mode == TargetingMode.MeleeStrike);
            SetHighlight(tripHighlight, mode == TargetingMode.Trip);
            SetHighlight(shoveHighlight, mode == TargetingMode.Shove);
            SetHighlight(grappleHighlight, mode == TargetingMode.Grapple);
            SetHighlight(demoralizeHighlight, mode == TargetingMode.Demoralize);
            SetHighlight(escapeHighlight, mode == TargetingMode.Escape);
            SetHighlight(raiseShieldHighlight, false);
            SetHighlight(standHighlight, false);

            RefreshAvailability();
        }

        private void RefreshAvailability()
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null || actionExecutor == null)
            {
                SetAllInteractable(false);
                return;
            }

            bool canAct = turnManager.IsPlayerTurn
                       && !actionExecutor.IsBusy
                       && turnManager.ActionsRemaining > 0;

            if (!canAct)
            {
                SetAllInteractable(false);
                return;
            }

            var actor = turnManager.CurrentEntity;
            var data = actor.IsValid ? entityManager.Registry.Get(actor) : null;
            if (data == null || !data.IsAlive)
            {
                SetAllInteractable(false);
                return;
            }

            SetInteractable(strikeButton, true);
            SetInteractable(tripButton, HasWeaponTrait(data, WeaponTraitFlags.Trip));
            SetInteractable(shoveButton, HasWeaponTrait(data, WeaponTraitFlags.Shove));
            SetInteractable(grappleButton, HasWeaponTrait(data, WeaponTraitFlags.Grapple));
            SetInteractable(demoralizeButton, true);
            SetInteractable(escapeButton, IsGrabbedOrRestrained(data));
            SetInteractable(raiseShieldButton, CanRaiseShield(data));
            SetInteractable(standButton, HasCondition(data, ConditionType.Prone));
        }

        private static bool HasWeaponTrait(EntityData data, WeaponTraitFlags trait)
        {
            if (data == null) return false;
            return (data.EquippedWeapon.Traits & trait) != 0;
        }

        private static bool IsGrabbedOrRestrained(EntityData data)
        {
            if (data == null) return false;
            return data.HasCondition(ConditionType.Grabbed) || data.HasCondition(ConditionType.Restrained);
        }

        private static bool CanRaiseShield(EntityData data)
        {
            if (data == null) return false;
            var shield = data.EquippedShield;
            if (!shield.IsEquipped) return false;
            if (shield.IsBroken) return false;
            if (shield.isRaised) return false;
            return true;
        }

        private static bool HasCondition(EntityData data, ConditionType type)
        {
            return data != null && data.HasCondition(type);
        }

        private void SetCombatVisible(bool visible)
        {
            if (canvasGroup == null) return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        private void SetAllInteractable(bool enabled)
        {
            SetInteractable(strikeButton, enabled);
            SetInteractable(tripButton, enabled);
            SetInteractable(shoveButton, enabled);
            SetInteractable(grappleButton, enabled);
            SetInteractable(demoralizeButton, enabled);
            SetInteractable(escapeButton, enabled);
            SetInteractable(raiseShieldButton, enabled);
            SetInteractable(standButton, enabled);
        }

        private static void SetInteractable(Button button, bool enabled)
        {
            if (button != null) button.interactable = enabled;
        }

        private void ClearAllHighlights()
        {
            SetHighlight(strikeHighlight, false);
            SetHighlight(tripHighlight, false);
            SetHighlight(shoveHighlight, false);
            SetHighlight(grappleHighlight, false);
            SetHighlight(demoralizeHighlight, false);
            SetHighlight(escapeHighlight, false);
            SetHighlight(raiseShieldHighlight, false);
            SetHighlight(standHighlight, false);
        }

        private static void SetHighlight(Image image, bool active)
        {
            if (image == null) return;
            if (image.gameObject.activeSelf != active)
                image.gameObject.SetActive(active);
        }

        private void OnStrikeClicked()
        {
            if (targetingController == null || actionExecutor == null) return;
            targetingController.BeginTargeting(TargetingMode.MeleeStrike, h => actionExecutor.TryExecuteStrike(h));
        }

        private void OnTripClicked()
        {
            if (targetingController == null || actionExecutor == null) return;
            targetingController.BeginTargeting(TargetingMode.Trip, h => actionExecutor.TryExecuteTrip(h));
        }

        private void OnShoveClicked()
        {
            if (targetingController == null || actionExecutor == null) return;
            targetingController.BeginTargeting(TargetingMode.Shove, h => actionExecutor.TryExecuteShove(h));
        }

        private void OnGrappleClicked()
        {
            if (targetingController == null || actionExecutor == null) return;
            targetingController.BeginTargeting(TargetingMode.Grapple, h => actionExecutor.TryExecuteGrapple(h));
        }

        private void OnDemoralizeClicked()
        {
            if (targetingController == null || actionExecutor == null) return;
            targetingController.BeginTargeting(TargetingMode.Demoralize, h => actionExecutor.TryExecuteDemoralize(h));
        }

        private void OnEscapeClicked()
        {
            if (targetingController == null || actionExecutor == null) return;
            targetingController.BeginTargeting(TargetingMode.Escape, h => actionExecutor.TryExecuteEscape(h));
        }

        private void OnRaiseShieldClicked()
        {
            if (actionExecutor == null) return;
            actionExecutor.TryExecuteRaiseShield();
        }

        private void OnStandClicked()
        {
            if (actionExecutor == null) return;
            actionExecutor.TryExecuteStand();
        }
    }
}
