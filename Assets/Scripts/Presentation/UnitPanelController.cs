using System.Text;
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
    /// Bottom-left unit panel.
    /// Displays active actor by default and hovered target while targeting is active.
    /// </summary>
    public class UnitPanelController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private TargetingController targetingController;

        [Header("UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private Image hpFillImage;
        [SerializeField] private TMP_Text acText;
        [SerializeField] private TMP_Text conditionsText;

        [Header("Behavior")]
        [SerializeField] private bool hideWhenNotInCombat = true;
        [SerializeField] private bool hoverOverridesOnlyWhileTargeting = true;
        [SerializeField] private string noConditionsText = "Conditions: None";
        [SerializeField] private int maxConditionsShown = 4;

        private bool inCombat;
        private EntityHandle activeActor;
        private EntityHandle? hoveredEntity;
        private EntityHandle displayedEntity;
        private bool dependenciesResolved;

        private void Awake()
        {
            EnsureDependencies();
            EnsureUiFallback();
            HidePanel();
        }

        private void OnEnable()
        {
            EnsureDependencies();
            EnsureUiFallback();

            if (!dependenciesResolved || eventBus == null || entityManager == null)
            {
                Debug.LogError("[UnitPanel] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            if (gridManager != null)
                hoveredEntity = gridManager.HoveredEntity;

            eventBus.OnCombatStartedTyped += HandleCombatStarted;
            eventBus.OnCombatEndedTyped += HandleCombatEnded;
            eventBus.OnTurnStartedTyped += HandleTurnStarted;
            eventBus.OnTurnEndedTyped += HandleTurnEnded;
            eventBus.OnConditionChangedTyped += HandleConditionChanged;
            eventBus.OnStrikeResolved += HandleStrikeResolved;
            eventBus.OnDamageAppliedTyped += HandleDamageApplied;

            if (gridManager != null)
            {
                gridManager.OnEntityHovered += HandleEntityHovered;
                gridManager.OnEntityUnhovered += HandleEntityUnhovered;
            }

            if (targetingController != null)
                targetingController.OnModeChanged += HandleTargetingModeChanged;

            Refresh(force: true);
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                eventBus.OnCombatStartedTyped -= HandleCombatStarted;
                eventBus.OnCombatEndedTyped -= HandleCombatEnded;
                eventBus.OnTurnStartedTyped -= HandleTurnStarted;
                eventBus.OnTurnEndedTyped -= HandleTurnEnded;
                eventBus.OnConditionChangedTyped -= HandleConditionChanged;
                eventBus.OnStrikeResolved -= HandleStrikeResolved;
                eventBus.OnDamageAppliedTyped -= HandleDamageApplied;
            }

            if (gridManager != null)
            {
                gridManager.OnEntityHovered -= HandleEntityHovered;
                gridManager.OnEntityUnhovered -= HandleEntityUnhovered;
            }

            if (targetingController != null)
                targetingController.OnModeChanged -= HandleTargetingModeChanged;
        }

        private void EnsureDependencies()
        {
            if (eventBus == null)
                eventBus = FindFirstObjectByType<CombatEventBus>();
            if (entityManager == null)
                entityManager = FindFirstObjectByType<EntityManager>();
            if (turnManager == null)
                turnManager = FindFirstObjectByType<TurnManager>();
            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();
            if (targetingController == null)
                targetingController = FindFirstObjectByType<TargetingController>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            dependenciesResolved = eventBus != null && entityManager != null;
        }

        private void EnsureUiFallback()
        {
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (nameText == null)
                nameText = CreateTextChild("NameText", 20, FontStyles.Bold);
            if (levelText == null)
                levelText = CreateTextChild("LevelText", 14, FontStyles.Normal);
            if (hpText == null)
                hpText = CreateTextChild("HPText", 14, FontStyles.Normal);
            if (acText == null)
                acText = CreateTextChild("ACText", 14, FontStyles.Normal);
            if (conditionsText == null)
                conditionsText = CreateTextChild("ConditionsText", 13, FontStyles.Normal);

            if (hpFillImage == null)
            {
                var hpBarRoot = new GameObject("HPBar", typeof(RectTransform), typeof(Image));
                hpBarRoot.transform.SetParent(transform, false);
                var hpBarRect = hpBarRoot.GetComponent<RectTransform>();
                hpBarRect.sizeDelta = new Vector2(160f, 12f);
                var hpBarBg = hpBarRoot.GetComponent<Image>();
                hpBarBg.color = new Color(0f, 0f, 0f, 0.45f);

                var hpFillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                hpFillGo.transform.SetParent(hpBarRoot.transform, false);
                var hpFillRect = hpFillGo.GetComponent<RectTransform>();
                hpFillRect.anchorMin = new Vector2(0f, 0f);
                hpFillRect.anchorMax = new Vector2(1f, 1f);
                hpFillRect.pivot = new Vector2(0f, 0.5f);
                hpFillRect.anchoredPosition = Vector2.zero;
                hpFillRect.sizeDelta = Vector2.zero;

                hpFillImage = hpFillGo.GetComponent<Image>();
                hpFillImage.color = new Color(0.35f, 0.85f, 0.35f, 1f);
                hpFillImage.type = Image.Type.Filled;
                hpFillImage.fillMethod = Image.FillMethod.Horizontal;
                hpFillImage.fillOrigin = 0;
                hpFillImage.fillAmount = 1f;
            }

            if (!TryGetComponent<VerticalLayoutGroup>(out _))
            {
                var v = gameObject.AddComponent<VerticalLayoutGroup>();
                v.padding = new RectOffset(12, 12, 10, 10);
                v.spacing = 4f;
                v.childControlWidth = true;
                v.childControlHeight = false;
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
            }

            if (!TryGetComponent<ContentSizeFitter>(out _))
            {
                var f = gameObject.AddComponent<ContentSizeFitter>();
                f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        private TMP_Text CreateTextChild(string objectName, float fontSize, FontStyles style)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = new Color(0.92f, 0.92f, 0.95f, 1f);
            text.raycastTarget = false;
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        private void HandleCombatStarted(in CombatStartedEvent e)
        {
            inCombat = true;
            Refresh(force: true);
        }

        private void HandleCombatEnded(in CombatEndedEvent e)
        {
            inCombat = false;
            activeActor = default;
            hoveredEntity = null;
            displayedEntity = default;
            Refresh(force: true);
        }

        private void HandleTurnStarted(in TurnStartedEvent e)
        {
            activeActor = e.actor;
            Refresh(force: false);
        }

        private void HandleTurnEnded(in TurnEndedEvent e)
        {
            if (activeActor == e.actor)
                Refresh(force: false);
        }

        private void HandleConditionChanged(in ConditionChangedEvent e)
        {
            if (e.entity == displayedEntity || e.entity == activeActor)
                Refresh(force: true);
        }

        private void HandleStrikeResolved(in StrikeResolvedEvent e)
        {
            if (e.target == displayedEntity || e.target == activeActor)
                Refresh(force: true);
        }

        private void HandleDamageApplied(in DamageAppliedEvent e)
        {
            if (e.target == displayedEntity || e.target == activeActor)
                Refresh(force: true);
        }

        private void HandleEntityHovered(EntityHandle handle)
        {
            hoveredEntity = handle;
            Refresh(force: false);
        }

        private void HandleEntityUnhovered()
        {
            hoveredEntity = null;
            Refresh(force: false);
        }

        private void HandleTargetingModeChanged(TargetingMode mode)
        {
            Refresh(force: false);
        }

        private void Refresh(bool force)
        {
            ApplyVisibility();

            if (!inCombat)
            {
                ClearTexts();
                return;
            }

            var next = ResolveDisplayedEntity();
            if (!force && next == displayedEntity)
                return;

            displayedEntity = next;
            PopulateFromEntity(next);
        }

        private void ApplyVisibility()
        {
            if (!hideWhenNotInCombat)
            {
                ShowPanel();
                return;
            }

            if (inCombat)
                ShowPanel();
            else
                HidePanel();
        }

        private EntityHandle ResolveDisplayedEntity()
        {
            if (entityManager == null || entityManager.Registry == null)
                return default;

            bool canUseHover = hoveredEntity.HasValue && hoveredEntity.Value.IsValid;
            if (canUseHover && hoverOverridesOnlyWhileTargeting)
                canUseHover = targetingController != null && targetingController.ActiveMode != TargetingMode.None;

            if (canUseHover && entityManager.Registry.Exists(hoveredEntity.Value))
                return hoveredEntity.Value;

            if (activeActor.IsValid && entityManager.Registry.Exists(activeActor))
                return activeActor;

            if (turnManager != null)
            {
                var current = turnManager.CurrentEntity;
                if (current.IsValid && entityManager.Registry.Exists(current))
                    return current;
            }

            return default;
        }

        private void PopulateFromEntity(EntityHandle handle)
        {
            if (!handle.IsValid || entityManager == null || entityManager.Registry == null)
            {
                ClearTexts();
                return;
            }

            var data = entityManager.Registry.Get(handle);
            if (data == null)
            {
                ClearTexts();
                return;
            }

            if (nameText != null)
                nameText.text = data.Name;

            if (levelText != null)
                levelText.text = $"Level {data.Level}";

            if (hpText != null)
                hpText.text = $"HP {Mathf.Max(0, data.CurrentHP)} / {Mathf.Max(0, data.MaxHP)}";

            if (hpFillImage != null)
            {
                float fill = data.MaxHP > 0 ? Mathf.Clamp01((float)data.CurrentHP / data.MaxHP) : 0f;
                hpFillImage.fillAmount = fill;
                hpFillImage.color = fill <= 0.25f
                    ? new Color(0.95f, 0.25f, 0.25f, 1f)
                    : new Color(0.35f, 0.85f, 0.35f, 1f);
            }

            if (acText != null)
                acText.text = $"AC {data.EffectiveAC}";

            if (conditionsText != null)
                conditionsText.text = FormatConditions(data.Conditions);
        }

        private string FormatConditions(System.Collections.Generic.IReadOnlyList<ActiveCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return noConditionsText;

            int shown = 0;
            var sb = new StringBuilder("Conditions: ");
            for (int i = 0; i < conditions.Count; i++)
            {
                var c = conditions[i];
                if (c == null)
                    continue;

                if (shown > 0)
                    sb.Append(", ");

                sb.Append(c.Type);
                if (c.Value > 0)
                {
                    sb.Append(' ');
                    sb.Append(c.Value);
                }

                shown++;
                if (shown >= Mathf.Max(1, maxConditionsShown))
                    break;
            }

            if (shown == 0)
                return noConditionsText;

            if (conditions.Count > shown)
                sb.Append(", ...");

            return sb.ToString();
        }

        private void ShowPanel()
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void HidePanel()
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void ClearTexts()
        {
            if (nameText != null) nameText.text = "-";
            if (levelText != null) levelText.text = "Level -";
            if (hpText != null) hpText.text = "HP - / -";
            if (acText != null) acText.text = "AC -";
            if (conditionsText != null) conditionsText.text = noConditionsText;
            if (hpFillImage != null) hpFillImage.fillAmount = 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null) Debug.LogWarning("[UnitPanel] CombatEventBus not assigned.", this);
            if (entityManager == null) Debug.LogWarning("[UnitPanel] EntityManager not assigned.", this);
            if (turnManager == null) Debug.LogWarning("[UnitPanel] TurnManager not assigned.", this);
            if (gridManager == null) Debug.LogWarning("[UnitPanel] GridManager not assigned.", this);
            if (targetingController == null) Debug.LogWarning("[UnitPanel] TargetingController not assigned.", this);
            if (canvasGroup == null) Debug.LogWarning("[UnitPanel] CanvasGroup not assigned.", this);
        }
#endif
    }
}
