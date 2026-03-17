using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System;
using System.Collections.Generic;
using System.Text;
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
        [SerializeField] private Button jumpButton;
        [SerializeField] private Button tripButton;
        [SerializeField] private Button shoveButton;
        [SerializeField] private Button grappleButton;
        [SerializeField] private Button repositionButton;
        [SerializeField] private Button demoralizeButton;
        [SerializeField] private Button escapeButton;
        [SerializeField] private Button aidButton;
        [SerializeField] private Button castSpellButton;
        [SerializeField] private TMP_Text castSpellButtonLabel;
        [SerializeField] private RectTransform castSpellModeSelectorRoot;
        [SerializeField] private Button castSpellModeStandardButton;
        [SerializeField] private Button castSpellModeGlassButton;
        [SerializeField] private Button castSpellModeSnowballButton;
        [SerializeField] private Button castSpellModeBurningHandsButton;
        [SerializeField] private Button castSpellModeFearButton;
        [SerializeField] private Button castSpellModeHealButton;
        [SerializeField] private Button castSpellModeHarmButton;
        [SerializeField] private GameObject targetingHintPanelRoot;
        [SerializeField] private Button raiseShieldButton;
        [SerializeField] private Button standButton;

        [Header("Launcher Layout (Step 5, optional)")]
        [SerializeField] private bool useLauncherLayout;
        [SerializeField] private Button tacticsLauncherButton;
        [SerializeField] private RectTransform strikePopupRoot;
        [SerializeField] private RectTransform tacticsPopupRoot;
        [SerializeField] private Button strikePopupStrikeButton;
        [SerializeField] private float popupClampPadding = 10f;
        [SerializeField] private float strikePopupTileWidth = 132f;
        [SerializeField] private float tacticsPopupTileWidth = 126f;
        [SerializeField] private float castPopupTileWidth = 132f;

        [Header("Highlights (optional overlays)")]
        [SerializeField] private Image strikeHighlight;
        [SerializeField] private Image jumpHighlight;
        [SerializeField] private Image tripHighlight;
        [SerializeField] private Image shoveHighlight;
        [SerializeField] private Image grappleHighlight;
        [SerializeField] private Image repositionHighlight;
        [SerializeField] private Image demoralizeHighlight;
        [SerializeField] private Image escapeHighlight;
        [SerializeField] private Image aidHighlight;
        [SerializeField] private Image castSpellHighlight;
        [SerializeField] private Image raiseShieldHighlight;
        [SerializeField] private Image standHighlight;

        [Header("Aid Prepared Indicator (optional)")]
        [SerializeField] private GameObject aidPreparedIndicatorRoot;
        [SerializeField] private TMP_Text aidPreparedIndicatorLabel;
        [SerializeField] private Color aidPreparedIndicatorFillColor = new Color(0.98f, 0.82f, 0.22f, 0.95f);
        [SerializeField] private Color aidPreparedIndicatorLabelColor = Color.black;
        [SerializeField] private string aidPreparedSingleText = string.Empty;
        [SerializeField] private string aidPreparedCountFormat = "{0}";
        [SerializeField] private Color castSpellModeSelectedColor = new Color(0.95f, 0.78f, 0.18f, 0.95f);
        [SerializeField] private Color castSpellModeUnselectedColor = new Color(0.18f, 0.23f, 0.30f, 0.92f);
        [SerializeField] private Color castSpellModeTextColor = new Color(0.92f, 0.92f, 0.95f, 1f);

        private bool buttonListenersBound;
        private bool spellPanelPinnedByActiveTargeting;
        private RectTransform spellCastPanelContentRoot;
        private RectTransform spellCastDetailRoot;
        private TMP_Text spellCastTitleLabel;
        private TMP_Text spellCastSummaryLabel;
        private RectTransform spellCastActionCountRow;
        private Button spellCastOneActionButton;
        private Button spellCastTwoActionButton;
        private Button spellCastThreeActionButton;
        private RectTransform spellCastFooterRow;
        private Button spellCastConfirmButton;
        private Button spellCastCancelButton;
        private readonly ActionBarAvailabilityPolicy actionBarAvailabilityPolicy = new();
        private readonly ActionBarLauncherPresenter actionBarLauncherPresenter = new();
        private readonly AidPreparedIndicatorPresenter aidPreparedIndicatorPresenter = new();
        private readonly ActionBarCommandCoordinator actionBarCommandCoordinator = new();
        private bool aidUiWiringWarned;
        private bool castSpellUiWiringWarned;
        private bool launcherLayoutWiringWarned;
        private bool strikePopupHeaderWiringWarned;
        private const float LauncherPopupOffsetY = 18f;
        private const float CastPopupSelectionOffsetY = 42f;
        private const float CastPopupDetailOffsetY = 84f;
        private const float StrikePopupMenuWidth = 156f;
        private const float TacticsPopupMenuWidth = 148f;
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (EditorValidationGuard.ShouldSkipMissingReferenceWarnings())
                return;

            if (eventBus == null) Debug.LogError("[ActionBar] Missing CombatEventBus", this);
            if (entityManager == null) Debug.LogError("[ActionBar] Missing EntityManager", this);
            if (turnManager == null) Debug.LogError("[ActionBar] Missing TurnManager", this);
            if (actionExecutor == null) Debug.LogError("[ActionBar] Missing PlayerActionExecutor", this);
            if (targetingController == null) Debug.LogError("[ActionBar] Missing TargetingController", this);

            if (canvasGroup == null) Debug.LogWarning("[ActionBar] Missing CanvasGroup", this);

            if (strikeButton == null) Debug.LogWarning("[ActionBar] strikeButton not assigned", this);
            if (jumpButton == null) Debug.LogWarning("[ActionBar] jumpButton not assigned", this);
            if (tripButton == null) Debug.LogWarning("[ActionBar] tripButton not assigned", this);
            if (shoveButton == null) Debug.LogWarning("[ActionBar] shoveButton not assigned", this);
            if (grappleButton == null) Debug.LogWarning("[ActionBar] grappleButton not assigned", this);
            if (repositionButton == null) Debug.LogWarning("[ActionBar] repositionButton not assigned", this);
            if (demoralizeButton == null) Debug.LogWarning("[ActionBar] demoralizeButton not assigned", this);
            if (escapeButton == null) Debug.LogWarning("[ActionBar] escapeButton not assigned", this);
            if (aidButton == null) Debug.LogWarning("[ActionBar] aidButton not assigned", this);
            if (aidHighlight == null) Debug.LogWarning("[ActionBar] aidHighlight not assigned", this);
            if (aidPreparedIndicatorRoot == null) Debug.LogWarning("[ActionBar] aidPreparedIndicatorRoot not assigned", this);
            if (aidPreparedIndicatorLabel == null) Debug.LogWarning("[ActionBar] aidPreparedIndicatorLabel not assigned", this);
            if (castSpellButton == null) Debug.LogWarning("[ActionBar] castSpellButton not assigned", this);
            if (castSpellButtonLabel == null) Debug.LogWarning("[ActionBar] castSpellButtonLabel not assigned", this);
            if (castSpellModeSelectorRoot == null) Debug.LogWarning("[ActionBar] castSpellModeSelectorRoot not assigned", this);
            if (castSpellModeStandardButton == null) Debug.LogWarning("[ActionBar] castSpellModeStandardButton not assigned", this);
            if (castSpellModeGlassButton == null) Debug.LogWarning("[ActionBar] castSpellModeGlassButton not assigned", this);
            if (raiseShieldButton == null) Debug.LogWarning("[ActionBar] raiseShieldButton not assigned", this);
            if (standButton == null) Debug.LogWarning("[ActionBar] standButton not assigned", this);
            if (useLauncherLayout && tacticsLauncherButton == null) Debug.LogWarning("[ActionBar] useLauncherLayout=true but tacticsLauncherButton is not assigned (run scene validator autofix or assign in scene).", this);
            if (useLauncherLayout && strikePopupRoot == null) Debug.LogWarning("[ActionBar] useLauncherLayout=true but strikePopupRoot is not assigned (run scene validator autofix or assign in scene).", this);
            if (useLauncherLayout && tacticsPopupRoot == null) Debug.LogWarning("[ActionBar] useLauncherLayout=true but tacticsPopupRoot is not assigned (run scene validator autofix or assign in scene).", this);
            if (useLauncherLayout && strikePopupStrikeButton == null) Debug.LogWarning("[ActionBar] useLauncherLayout=true but strikePopupStrikeButton is not assigned (run scene validator autofix or assign in scene).", this);
        }
#endif

        private void Awake()
        {
            ValidateAndApplyUiWiring();
            EnsureButtonListenersBound();
            ApplyStaticButtonLabels();

            SetCombatVisible(false);
            SetCastSpellUiVisible(false);
            SetAllInteractable(false);
            SetCastSpellModeButtonsInteractable(false);
            RefreshCastSpellModeButtonsVisual();
            RefreshCastSpellButtonLabel();
            RefreshMobilityButtonLabel(null);
            ClearAllHighlights();
            SetStrikePopupVisible(false);
            SetTacticsPopupVisible(false);
            SetCastPopupVisible(false);
        }

        private void ValidateAndApplyUiWiring()
        {
            ResolveJumpUiReferences();
            ResolveTargetingHintPanelReference();
            ValidateAidUiReferences();
            ApplyAidPreparedIndicatorStyle();
            ResolveCastSpellUiReferences();
            EnsureLauncherLayoutFallback();
            EnsureSpellCastPanelUi();
            ConfigureLauncherPresenter();
            ApplyTypographyStyle();

            aidPreparedIndicatorPresenter.Clear();
            RefreshAidPreparedIndicator();
        }

        private void ValidateAidUiReferences()
        {
            if (aidButton != null && aidHighlight != null && aidPreparedIndicatorRoot != null && aidPreparedIndicatorLabel != null)
                return;

            if (aidUiWiringWarned)
                return;

            aidUiWiringWarned = true;
            Debug.LogWarning(
                "[ActionBar] Aid UI is not fully wired (aidButton/aidHighlight/aidPreparedIndicatorRoot/aidPreparedIndicatorLabel). " +
                "Assign references in scene or run scene validator autofix.",
                this);
        }

        private void ResolveJumpUiReferences()
        {
            if (jumpButton == null)
                jumpButton = FindButtonByName("JumpButton");

            if (jumpButton != null && jumpHighlight == null)
            {
                var highlightTf = jumpButton.transform.Find("ActiveHighlight")
                    ?? jumpButton.transform.Find("Highlight");
                if (highlightTf != null)
                    jumpHighlight = highlightTf.GetComponent<Image>();
            }
        }

        private void ResolveTargetingHintPanelReference()
        {
            if (targetingHintPanelRoot != null)
                return;

            var hintTransform = transform.Find("TargetingHintPanel");
            if (hintTransform != null)
                targetingHintPanelRoot = hintTransform.gameObject;
        }

        private Button FindButtonByName(string buttonName)
        {
            if (string.IsNullOrWhiteSpace(buttonName))
                return null;

            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && string.Equals(buttons[i].name, buttonName, StringComparison.OrdinalIgnoreCase))
                    return buttons[i];
            }

            return null;
        }

        private void ApplyAidPreparedIndicatorStyle()
        {
            aidPreparedIndicatorFillColor = CombatUiPalette.HudButtonSelectedColor;
            aidPreparedIndicatorLabelColor = CombatUiPalette.HudButtonSelectedTextColor;

            if (aidPreparedIndicatorRoot != null)
            {
                var indicatorImage = aidPreparedIndicatorRoot.GetComponent<Image>();
                if (indicatorImage != null)
                    indicatorImage.color = aidPreparedIndicatorFillColor;
            }

            CombatUiTypography.ApplyButton(aidPreparedIndicatorLabel, 12f, 0.12f, aidPreparedIndicatorLabelColor, FontStyles.Bold);
        }

        private void ResolveCastSpellUiReferences()
        {
            if (castSpellButton == null)
            {
                WarnMissingCastSpellWiring();
                return;
            }

            if (castSpellButton != null && castSpellButtonLabel == null)
                castSpellButtonLabel = castSpellButton.GetComponentInChildren<TMP_Text>(true);

            if (castSpellModeSelectorRoot == null && castSpellButton != null)
                castSpellModeSelectorRoot = castSpellButton.transform.Find("CastSpellModeSelector") as RectTransform;

            if (castSpellModeSelectorRoot == null)
            {
                WarnMissingCastSpellWiring();
                return;
            }

            if (castSpellModeStandardButton == null || castSpellModeGlassButton == null)
                ResolveCastSpellModeButtonsFromRoot(castSpellModeSelectorRoot);

            if (castSpellButtonLabel == null || castSpellModeStandardButton == null || castSpellModeGlassButton == null)
                WarnMissingCastSpellWiring();
        }

        private void ResolveCastSpellModeButtonsFromRoot(RectTransform root)
        {
            if (root == null)
                return;

            var standardByName = root.Find("CastSpellModeStandardButton");
            if (castSpellModeStandardButton == null && standardByName != null)
                castSpellModeStandardButton = standardByName.GetComponent<Button>();

            var glassByName = root.Find("CastSpellModeGlassButton");
            if (castSpellModeGlassButton == null && glassByName != null)
                castSpellModeGlassButton = glassByName.GetComponent<Button>();

            if (castSpellModeStandardButton != null && castSpellModeGlassButton != null)
                return;

            var buttons = root.GetComponentsInChildren<Button>(true);
            if (buttons == null || buttons.Length == 0)
                return;

            if (castSpellModeStandardButton == null)
                castSpellModeStandardButton = buttons[0];

            if (castSpellModeGlassButton == null && buttons.Length > 1)
                castSpellModeGlassButton = buttons[1];
        }

        private void WarnMissingCastSpellWiring()
        {
            if (castSpellUiWiringWarned)
                return;

            castSpellUiWiringWarned = true;
            Debug.LogWarning(
                "[ActionBar] Cast Spell UI is not fully wired (castSpellButton/label/mode selector/mode buttons). " +
                "Assign references in scene or run scene validator autofix.",
                this);
        }

        private void EnsureSpellCastPanelUi()
        {
            if (castSpellModeSelectorRoot == null)
                return;

            spellCastPanelContentRoot = ConfigureSpellCastPanelRoot(castSpellModeSelectorRoot, spellCastPanelContentRoot);

            float selectionWidth = Mathf.Max(castPopupTileWidth, 248f);
            ConfigurePopupTileLayout(castSpellModeStandardButton, selectionWidth);
            ConfigurePopupTileLayout(castSpellModeGlassButton, selectionWidth);
            EnsureSnowballSpellButton(selectionWidth);
            ConfigurePopupTileLayout(castSpellModeSnowballButton, selectionWidth);
            EnsureBurningHandsSpellButton(selectionWidth);
            ConfigurePopupTileLayout(castSpellModeBurningHandsButton, selectionWidth);
            EnsureFearSpellButton(selectionWidth);
            ConfigurePopupTileLayout(castSpellModeFearButton, selectionWidth);
            EnsureHealSpellButton(selectionWidth);
            ConfigurePopupTileLayout(castSpellModeHealButton, selectionWidth);
            EnsureHarmSpellButton(selectionWidth);
            ConfigurePopupTileLayout(castSpellModeHarmButton, selectionWidth);

            if (castSpellModeStandardButton != null && castSpellModeStandardButton.transform.parent != spellCastPanelContentRoot)
                castSpellModeStandardButton.transform.SetParent(spellCastPanelContentRoot, false);

            if (castSpellModeGlassButton != null && castSpellModeGlassButton.transform.parent != spellCastPanelContentRoot)
                castSpellModeGlassButton.transform.SetParent(spellCastPanelContentRoot, false);

            if (castSpellModeSnowballButton != null && castSpellModeSnowballButton.transform.parent != spellCastPanelContentRoot)
                castSpellModeSnowballButton.transform.SetParent(spellCastPanelContentRoot, false);

            if (castSpellModeBurningHandsButton != null && castSpellModeBurningHandsButton.transform.parent != spellCastPanelContentRoot)
                castSpellModeBurningHandsButton.transform.SetParent(spellCastPanelContentRoot, false);

            if (castSpellModeFearButton != null && castSpellModeFearButton.transform.parent != spellCastPanelContentRoot)
                castSpellModeFearButton.transform.SetParent(spellCastPanelContentRoot, false);

            if (castSpellModeHealButton != null && castSpellModeHealButton.transform.parent != spellCastPanelContentRoot)
                castSpellModeHealButton.transform.SetParent(spellCastPanelContentRoot, false);

            if (castSpellModeHarmButton != null && castSpellModeHarmButton.transform.parent != spellCastPanelContentRoot)
                castSpellModeHarmButton.transform.SetParent(spellCastPanelContentRoot, false);

            if (spellCastDetailRoot != null)
                return;

            var detailGo = new GameObject(
                "SpellCastDetailRoot",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(LayoutElement));
            detailGo.transform.SetParent(spellCastPanelContentRoot, false);
            spellCastDetailRoot = detailGo.GetComponent<RectTransform>();
            var detailLayout = detailGo.GetComponent<VerticalLayoutGroup>();
            detailLayout.padding = new RectOffset(0, 0, 0, 0);
            detailLayout.spacing = 8f;
            detailLayout.childAlignment = TextAnchor.UpperLeft;
            detailLayout.childControlWidth = true;
            detailLayout.childControlHeight = true;
            detailLayout.childForceExpandWidth = true;
            detailLayout.childForceExpandHeight = false;
            var detailElement = detailGo.GetComponent<LayoutElement>();
            detailElement.preferredWidth = selectionWidth;
            detailElement.minWidth = selectionWidth;
            var detailFitter = detailGo.GetComponent<ContentSizeFitter>();
            if (detailFitter == null)
                detailFitter = detailGo.AddComponent<ContentSizeFitter>();
            detailFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            detailFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            spellCastTitleLabel = CreateSpellPanelLabel("Title", spellCastDetailRoot, TextAlignmentOptions.MidlineLeft, 16f);
            spellCastSummaryLabel = CreateSpellPanelLabel("Summary", spellCastDetailRoot, TextAlignmentOptions.TopLeft, 12.5f);
            spellCastSummaryLabel.enableWordWrapping = true;
            if (spellCastSummaryLabel.TryGetComponent<LayoutElement>(out var summaryLayout))
            {
                summaryLayout.preferredHeight = 82f;
                summaryLayout.minHeight = 72f;
            }

            spellCastActionCountRow = CreateSpellPanelRow("ActionCountRow", spellCastDetailRoot, spacing: 4f);
            spellCastOneActionButton = CreateSpellPanelButton("OneActionButton", spellCastActionCountRow, "◆", preferredWidth: 48f);
            spellCastTwoActionButton = CreateSpellPanelButton("TwoActionButton", spellCastActionCountRow, "◆◆", preferredWidth: 58f);
            spellCastThreeActionButton = CreateSpellPanelButton("ThreeActionButton", spellCastActionCountRow, "◆◆◆", preferredWidth: 68f);

            spellCastFooterRow = CreateSpellPanelRow("FooterRow", spellCastDetailRoot, spacing: 6f);
            spellCastConfirmButton = CreateSpellPanelButton("ConfirmButton", spellCastFooterRow, "Confirm", preferredWidth: 92f);
            spellCastCancelButton = CreateSpellPanelButton("CancelButton", spellCastFooterRow, "Cancel", preferredWidth: 92f);

            SetSpellCastDetailVisible(false);
        }

        private void EnsureSnowballSpellButton(float preferredWidth)
        {
            if (spellCastPanelContentRoot == null || castSpellModeSnowballButton != null)
                return;

            castSpellModeSnowballButton = CreateSpellPanelButton(
                "CastSpellModeSnowballButton",
                spellCastPanelContentRoot,
                "Snowball [2]",
                preferredWidth);
        }

        private void EnsureBurningHandsSpellButton(float preferredWidth)
        {
            if (spellCastPanelContentRoot == null || castSpellModeBurningHandsButton != null)
                return;

            castSpellModeBurningHandsButton = CreateSpellPanelButton(
                "CastSpellModeBurningHandsButton",
                spellCastPanelContentRoot,
                "Burning Hands [2]",
                preferredWidth);
        }

        private void EnsureFearSpellButton(float preferredWidth)
        {
            if (spellCastPanelContentRoot == null || castSpellModeFearButton != null)
                return;

            castSpellModeFearButton = CreateSpellPanelButton(
                "CastSpellModeFearButton",
                spellCastPanelContentRoot,
                "Fear [2]",
                preferredWidth);
        }

        private void EnsureHealSpellButton(float preferredWidth)
        {
            if (spellCastPanelContentRoot == null || castSpellModeHealButton != null)
                return;

            castSpellModeHealButton = CreateSpellPanelButton(
                "CastSpellModeHealButton",
                spellCastPanelContentRoot,
                "Heal [1-3]",
                preferredWidth);
        }

        private void EnsureHarmSpellButton(float preferredWidth)
        {
            if (spellCastPanelContentRoot == null || castSpellModeHarmButton != null)
                return;

            castSpellModeHarmButton = CreateSpellPanelButton(
                "CastSpellModeHarmButton",
                spellCastPanelContentRoot,
                "Harm [1-3]",
                preferredWidth);
        }

        private static RectTransform ConfigureSpellCastPanelRoot(RectTransform root, RectTransform existingContentRoot)
        {
            if (root == null)
                return existingContentRoot;

            if (root.TryGetComponent<HorizontalLayoutGroup>(out var rootLayout))
            {
                rootLayout.enabled = true;
                rootLayout.padding = new RectOffset(0, 0, 0, 0);
                rootLayout.spacing = 0f;
                rootLayout.childAlignment = TextAnchor.MiddleCenter;
                rootLayout.childControlWidth = false;
                rootLayout.childControlHeight = false;
                rootLayout.childForceExpandWidth = false;
                rootLayout.childForceExpandHeight = false;
            }

            var contentRoot = existingContentRoot;
            if (contentRoot == null)
            {
                var existing = root.Find("SpellCastPanelContent") as RectTransform;
                if (existing != null)
                    contentRoot = existing;
            }

            if (contentRoot == null)
            {
                var contentGo = new GameObject(
                    "SpellCastPanelContent",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(LayoutElement));
                contentGo.transform.SetParent(root, false);
                contentRoot = contentGo.GetComponent<RectTransform>();
            }

            var verticalLayout = contentRoot.GetComponent<VerticalLayoutGroup>();
            if (verticalLayout == null)
                verticalLayout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();

            var contentLayout = contentRoot.GetComponent<LayoutElement>();
            if (contentLayout == null)
                contentLayout = contentRoot.gameObject.AddComponent<LayoutElement>();

            var contentFitter = contentRoot.GetComponent<ContentSizeFitter>();
            if (contentFitter == null)
                contentFitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();

            verticalLayout.padding = new RectOffset(8, 8, 8, 8);
            verticalLayout.spacing = 8f;
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;

            contentLayout.preferredWidth = 248f;
            contentLayout.minWidth = 248f;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return contentRoot;
        }

        private static RectTransform CreateSpellPanelRow(string name, Transform parent, float spacing)
        {
            var rowGo = new GameObject(
                name,
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            rowGo.transform.SetParent(parent, false);

            var rowRect = rowGo.GetComponent<RectTransform>();
            var rowLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(0, 0, 0, 0);
            rowLayout.spacing = spacing;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var rowElement = rowGo.GetComponent<LayoutElement>();
            rowElement.preferredHeight = 28f;
            rowElement.minHeight = 28f;
            return rowRect;
        }

        private static TMP_Text CreateSpellPanelLabel(
            string name,
            Transform parent,
            TextAlignmentOptions alignment,
            float preferredHeight,
            bool fillParent = false)
        {
            var labelGo = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelGo.transform.SetParent(parent, false);

            var labelRect = labelGo.GetComponent<RectTransform>();
            if (fillParent)
            {
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
            }
            else
            {
                labelRect.anchorMin = new Vector2(0f, 0.5f);
                labelRect.anchorMax = new Vector2(1f, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.sizeDelta = new Vector2(0f, preferredHeight);
            }

            var layout = labelGo.GetComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            layout.minHeight = preferredHeight;

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = string.Empty;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private Button CreateSpellPanelButton(string name, Transform parent, string labelText, float preferredWidth)
        {
            var buttonGo = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonGo.transform.SetParent(parent, false);

            var layout = buttonGo.GetComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
            layout.minWidth = preferredWidth;
            layout.preferredHeight = 28f;
            layout.minHeight = 28f;

            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 0.5f);
            buttonRect.anchorMax = new Vector2(0f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(preferredWidth, 28f);

            var image = buttonGo.GetComponent<Image>();
            image.color = castSpellModeUnselectedColor;
            image.raycastTarget = true;

            var button = buttonGo.GetComponent<Button>();
            button.targetGraphic = image;

            var label = CreateSpellPanelLabel(
                "Label",
                buttonGo.transform,
                TextAlignmentOptions.Center,
                preferredHeight: 28f,
                fillParent: true);
            label.text = labelText;

            return button;
        }

        private void EnsureLauncherLayoutFallback()
        {
            if (!useLauncherLayout)
                return;
            if (!Application.isPlaying)
                return;

            if (strikeButton == null || castSpellButton == null || demoralizeButton == null)
            {
                if (!launcherLayoutWiringWarned)
                {
                    launcherLayoutWiringWarned = true;
                    Debug.LogWarning(
                        "[ActionBar] Launcher layout requires strike/cast/demoralize buttons. Falling back to legacy visibility.",
                        this);
                }
                useLauncherLayout = false;
                return;
            }

            if (tacticsLauncherButton == null || strikePopupRoot == null || tacticsPopupRoot == null || strikePopupStrikeButton == null)
            {
                if (!launcherLayoutWiringWarned)
                {
                    launcherLayoutWiringWarned = true;
                    Debug.LogWarning(
                        "[ActionBar] Launcher layout requires scene wiring: TacticsLauncherButton, StrikePopupRoot, TacticsPopupRoot, StrikePopupStrikeButton.",
                        this);
                }

                useLauncherLayout = false;
                return;
            }

            if (strikePopupRoot != null)
            {
                strikePopupRoot.SetParent(strikeButton.transform, false);
                strikePopupRoot.anchorMin = new Vector2(0.5f, 1f);
                strikePopupRoot.anchorMax = new Vector2(0.5f, 1f);
                strikePopupRoot.pivot = new Vector2(0.5f, 0f);
                strikePopupRoot.anchoredPosition = new Vector2(0f, LauncherPopupOffsetY);
                ConfigurePopupRootVisual(strikePopupRoot);
            }

            if (tacticsPopupRoot != null && tacticsLauncherButton != null)
            {
                tacticsPopupRoot.SetParent(tacticsLauncherButton.transform, false);
                tacticsPopupRoot.anchorMin = new Vector2(0.5f, 1f);
                tacticsPopupRoot.anchorMax = new Vector2(0.5f, 1f);
                tacticsPopupRoot.pivot = new Vector2(0.5f, 0f);
                tacticsPopupRoot.anchoredPosition = new Vector2(0f, LauncherPopupOffsetY);
                ConfigurePopupRootVisual(tacticsPopupRoot);
            }

            MoveButtonToPopup(tripButton, strikePopupRoot);
            MoveButtonToPopup(shoveButton, strikePopupRoot);
            MoveButtonToPopup(grappleButton, strikePopupRoot);
            MoveButtonToPopup(repositionButton, strikePopupRoot);

            MoveButtonToPopup(demoralizeButton, tacticsPopupRoot);
            MoveButtonToPopup(escapeButton, tacticsPopupRoot);
            MoveButtonToPopup(aidButton, tacticsPopupRoot);

            if (castSpellModeSelectorRoot != null)
            {
                castSpellModeSelectorRoot.SetParent(castSpellButton.transform, false);
                castSpellModeSelectorRoot.anchorMin = new Vector2(0.5f, 1f);
                castSpellModeSelectorRoot.anchorMax = new Vector2(0.5f, 1f);
                castSpellModeSelectorRoot.pivot = new Vector2(0.5f, 0f);
                castSpellModeSelectorRoot.anchoredPosition = new Vector2(0f, CastPopupSelectionOffsetY);
                ConfigurePopupRootVisual(castSpellModeSelectorRoot);
            }

            ConfigurePopupTileLayout(strikePopupStrikeButton, strikePopupTileWidth);
            ConfigurePopupTileLayout(tripButton, strikePopupTileWidth);
            ConfigurePopupTileLayout(shoveButton, strikePopupTileWidth);
            ConfigurePopupTileLayout(grappleButton, strikePopupTileWidth);
            ConfigurePopupTileLayout(repositionButton, strikePopupTileWidth);

            ConfigurePopupTileLayout(demoralizeButton, tacticsPopupTileWidth);
            ConfigurePopupTileLayout(escapeButton, tacticsPopupTileWidth);
            ConfigurePopupTileLayout(aidButton, tacticsPopupTileWidth);

            ConfigurePopupTileLayout(castSpellModeStandardButton, castPopupTileWidth);
            ConfigurePopupTileLayout(castSpellModeGlassButton, castPopupTileWidth);
            ConfigurePopupTileLayout(castSpellModeSnowballButton, castPopupTileWidth);
            ConfigurePopupTileLayout(castSpellModeBurningHandsButton, castPopupTileWidth);
            ConfigurePopupTileLayout(castSpellModeFearButton, castPopupTileWidth);
            ConfigurePopupTileLayout(castSpellModeHealButton, castPopupTileWidth);
            ConfigurePopupTileLayout(castSpellModeHarmButton, castPopupTileWidth);

            ConfigureStrikePopupCompactMenu();
            ConfigureTacticsPopupCompactMenu();
        }

        private void ConfigureLauncherPresenter()
        {
            actionBarLauncherPresenter.Configure(
                strikeButton != null ? strikeButton.transform as RectTransform : null,
                tacticsLauncherButton != null ? tacticsLauncherButton.transform as RectTransform : null,
                castSpellButton != null ? castSpellButton.transform as RectTransform : null,
                strikePopupRoot,
                tacticsPopupRoot,
                castSpellModeSelectorRoot);
            actionBarLauncherPresenter.SetStrikePopupVerticalOffset(LauncherPopupOffsetY);
            actionBarLauncherPresenter.SetTacticsPopupVerticalOffset(LauncherPopupOffsetY);
            actionBarLauncherPresenter.SetCastPopupVerticalOffset(CastPopupSelectionOffsetY);
        }

        private static void ConfigurePopupRootVisual(RectTransform root)
        {
            if (root == null)
                return;

            if (!root.TryGetComponent<Image>(out var image))
                image = root.gameObject.AddComponent<Image>();
            image.color = CombatUiPalette.HudPanelBackgroundColor;
            image.raycastTarget = true;

            if (!root.TryGetComponent<HorizontalLayoutGroup>(out var layout))
                layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 4, 4);
            layout.spacing = 4f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            if (!root.TryGetComponent<ContentSizeFitter>(out var fitter))
                fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void ApplyTypographyStyle()
        {
            castSpellModeSelectedColor = CombatUiPalette.HudButtonSelectedColor;
            castSpellModeUnselectedColor = CombatUiPalette.HudButtonBackgroundColor;
            castSpellModeTextColor = CombatUiPalette.HudButtonTextColor;

            ApplyButtonTypography(strikeButton);
            ApplyButtonTypography(jumpButton);
            ApplyButtonTypography(tripButton);
            ApplyButtonTypography(shoveButton);
            ApplyButtonTypography(grappleButton);
            ApplyButtonTypography(repositionButton);
            ApplyButtonTypography(demoralizeButton);
            ApplyButtonTypography(escapeButton);
            ApplyButtonTypography(aidButton);
            ApplyButtonTypography(castSpellButton);
            ApplyButtonTypography(castSpellModeStandardButton);
            ApplyButtonTypography(castSpellModeGlassButton);
            ApplyButtonTypography(castSpellModeSnowballButton);
            ApplyButtonTypography(castSpellModeBurningHandsButton);
            ApplyButtonTypography(castSpellModeFearButton);
            ApplyButtonTypography(castSpellModeHealButton);
            ApplyButtonTypography(castSpellModeHarmButton);
            ApplyButtonTypography(raiseShieldButton);
            ApplyButtonTypography(standButton);
            ApplyButtonTypography(tacticsLauncherButton);
            ApplyButtonTypography(strikePopupStrikeButton);
            ApplyButtonTypography(spellCastOneActionButton);
            ApplyButtonTypography(spellCastTwoActionButton);
            ApplyButtonTypography(spellCastThreeActionButton);
            ApplyButtonTypography(spellCastConfirmButton);
            ApplyButtonTypography(spellCastCancelButton);

            CombatUiTypography.ApplyTitle(spellCastTitleLabel, 16f, 0.1f, CombatUiPalette.HudButtonTextColor, FontStyles.Bold);
            CombatUiTypography.ApplyBody(spellCastSummaryLabel, 12.5f, 0.08f, CombatUiPalette.HudButtonTextColor, lineSpacing: 4f);

            CombatUiTypography.ApplyButton(castSpellButtonLabel, 15.5f, 0.12f, CombatUiPalette.HudButtonTextColor);
        }

        private static void ApplyButtonTypography(Button button)
        {
            if (button == null)
                return;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            CombatUiTypography.ApplyButton(label, 15.5f, 0.12f, CombatUiPalette.HudButtonTextColor);
        }

        private static void ConfigurePopupTileLayout(Button button, float preferredWidth)
        {
            if (button == null)
                return;

            var layoutElement = EnsureLayoutElement(button.gameObject);
            layoutElement.preferredWidth = preferredWidth;
            layoutElement.minWidth = Mathf.Min(preferredWidth, 110f);
            layoutElement.preferredHeight = 24f;
            layoutElement.minHeight = 24f;
        }

        private static LayoutElement EnsureLayoutElement(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<LayoutElement>(out var existing))
                return existing;
            return gameObject.AddComponent<LayoutElement>();
        }

        private static void MoveButtonToPopup(Button button, RectTransform popupRoot)
        {
            if (button == null || popupRoot == null)
                return;

            button.transform.SetParent(popupRoot, false);
            button.transform.SetAsLastSibling();
            if (button.transform is RectTransform rect)
            {
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }
            if (button.TryGetComponent<LayoutElement>(out var layoutElement))
            {
                layoutElement.preferredHeight = Mathf.Max(24f, layoutElement.preferredHeight);
            }
        }

        private void ConfigureStrikePopupCompactMenu()
        {
            var contentRoot = EnsurePopupMenuContentRoot(strikePopupRoot, "StrikePopupMenuContent");
            SetPopupElementVisible(strikePopupRoot != null ? strikePopupRoot.Find("AttacksHeader") as RectTransform : null, false);
            SetPopupElementVisible(strikePopupRoot != null ? strikePopupRoot.Find("ManeuversHeader") as RectTransform : null, false);
            SetPopupElementVisible(strikePopupRoot != null ? strikePopupRoot.Find("ReadyModeMoveButton") as RectTransform : null, false);
            SetPopupElementVisible(strikePopupRoot != null ? strikePopupRoot.Find("ReadyModeAttackButton") as RectTransform : null, false);
            SetPopupElementVisible(strikePopupRoot != null ? strikePopupRoot.Find("ReadyModeAnyButton") as RectTransform : null, false);

            MovePopupElementToContainer(strikePopupStrikeButton != null ? strikePopupStrikeButton.transform as RectTransform : null, contentRoot);
            MovePopupElementToContainer(tripButton != null ? tripButton.transform as RectTransform : null, contentRoot);
            MovePopupElementToContainer(shoveButton != null ? shoveButton.transform as RectTransform : null, contentRoot);
            MovePopupElementToContainer(grappleButton != null ? grappleButton.transform as RectTransform : null, contentRoot);
            MovePopupElementToContainer(repositionButton != null ? repositionButton.transform as RectTransform : null, contentRoot);

            ConfigurePopupTileLayout(strikePopupStrikeButton, StrikePopupMenuWidth);
            ConfigurePopupTileLayout(tripButton, StrikePopupMenuWidth);
            ConfigurePopupTileLayout(shoveButton, StrikePopupMenuWidth);
            ConfigurePopupTileLayout(grappleButton, StrikePopupMenuWidth);
            ConfigurePopupTileLayout(repositionButton, StrikePopupMenuWidth);

            int siblingIndex = 0;
            SetPopupSibling(strikePopupStrikeButton, siblingIndex++);
            SetPopupSibling(tripButton, siblingIndex++);
            SetPopupSibling(shoveButton, siblingIndex++);
            SetPopupSibling(grappleButton, siblingIndex++);
            SetPopupSibling(repositionButton, siblingIndex);
        }

        private void ConfigureTacticsPopupCompactMenu()
        {
            var contentRoot = EnsurePopupMenuContentRoot(tacticsPopupRoot, "TacticsPopupMenuContent");

            MovePopupElementToContainer(demoralizeButton != null ? demoralizeButton.transform as RectTransform : null, contentRoot);
            MovePopupElementToContainer(escapeButton != null ? escapeButton.transform as RectTransform : null, contentRoot);
            MovePopupElementToContainer(aidButton != null ? aidButton.transform as RectTransform : null, contentRoot);

            ConfigurePopupTileLayout(demoralizeButton, TacticsPopupMenuWidth);
            ConfigurePopupTileLayout(escapeButton, TacticsPopupMenuWidth);
            ConfigurePopupTileLayout(aidButton, TacticsPopupMenuWidth);

            int siblingIndex = 0;
            SetPopupSibling(demoralizeButton, siblingIndex++);
            SetPopupSibling(escapeButton, siblingIndex++);
            SetPopupSibling(aidButton, siblingIndex);
        }

        private static RectTransform EnsurePopupMenuContentRoot(RectTransform popupRoot, string contentName)
        {
            if (popupRoot == null)
                return null;

            var contentRoot = popupRoot.Find(contentName) as RectTransform;
            if (contentRoot == null)
            {
                var contentGo = new GameObject(
                    contentName,
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter),
                    typeof(LayoutElement));
                contentGo.transform.SetParent(popupRoot, false);
                contentRoot = contentGo.GetComponent<RectTransform>();
            }

            if (popupRoot.TryGetComponent<HorizontalLayoutGroup>(out var rootLayout))
            {
                rootLayout.enabled = true;
                rootLayout.padding = new RectOffset(0, 0, 0, 0);
                rootLayout.spacing = 0f;
                rootLayout.childAlignment = TextAnchor.MiddleCenter;
                rootLayout.childControlWidth = false;
                rootLayout.childControlHeight = false;
                rootLayout.childForceExpandWidth = false;
                rootLayout.childForceExpandHeight = false;
            }

            var verticalLayout = contentRoot.GetComponent<VerticalLayoutGroup>();
            if (verticalLayout == null)
                verticalLayout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();

            var contentFitter = contentRoot.GetComponent<ContentSizeFitter>();
            if (contentFitter == null)
                contentFitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();

            var layoutElement = contentRoot.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = contentRoot.gameObject.AddComponent<LayoutElement>();

            verticalLayout.enabled = true;
            verticalLayout.padding = new RectOffset(6, 6, 6, 6);
            verticalLayout.spacing = 4f;
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;

            contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            layoutElement.minWidth = 0f;
            layoutElement.minHeight = 0f;
            return contentRoot;
        }

        private static void MovePopupElementToContainer(RectTransform element, RectTransform container)
        {
            if (element == null || container == null || element.parent == container)
                return;

            element.SetParent(container, false);
            element.localScale = Vector3.one;
            element.localRotation = Quaternion.identity;
        }

        private static void SetPopupElementVisible(RectTransform rect, bool visible)
        {
            if (rect == null || rect.gameObject.activeSelf == visible)
                return;

            rect.gameObject.SetActive(visible);
        }

        private static void SetPopupSibling(Button button, int siblingIndex)
        {
            if (button == null)
                return;

            button.transform.SetSiblingIndex(siblingIndex);
        }

        private void EnsureStrikePopupGroupHeaders()
        {
            if (strikePopupRoot == null)
                return;

            var attacksHeader = strikePopupRoot.Find("AttacksHeader") as RectTransform;
            var maneuversHeader = strikePopupRoot.Find("ManeuversHeader") as RectTransform;
            if (attacksHeader == null || maneuversHeader == null)
            {
                if (!strikePopupHeaderWiringWarned)
                {
                    strikePopupHeaderWiringWarned = true;
                    Debug.LogWarning(
                        "[ActionBar] Strike popup group headers are missing (AttacksHeader/ManeuversHeader). " +
                        "Use scene wiring or validator autofix.",
                        this);
                }
                return;
            }

            ConfigurePopupHeaderLayout(attacksHeader, preferredWidth: 70f);
            ConfigurePopupHeaderLayout(maneuversHeader, preferredWidth: 92f);

            if (attacksHeader != null)
                attacksHeader.SetSiblingIndex(0);

            if (strikePopupStrikeButton != null && attacksHeader != null)
                strikePopupStrikeButton.transform.SetSiblingIndex(attacksHeader.GetSiblingIndex() + 1);

            if (maneuversHeader != null)
            {
                int maneuversHeaderIndex = 1;
                if (strikePopupStrikeButton != null)
                    maneuversHeaderIndex = strikePopupStrikeButton.transform.GetSiblingIndex() + 1;
                maneuversHeader.SetSiblingIndex(maneuversHeaderIndex);
            }

            if (tripButton != null && maneuversHeader != null)
                tripButton.transform.SetSiblingIndex(maneuversHeader.GetSiblingIndex() + 1);
        }

        private static void ConfigurePopupHeaderLayout(RectTransform headerRect, float preferredWidth)
        {
            if (headerRect == null)
                return;

            var layoutElement = EnsureLayoutElement(headerRect.gameObject);
            layoutElement.preferredWidth = preferredWidth;
            layoutElement.minWidth = preferredWidth;
            layoutElement.preferredHeight = 22f;
            layoutElement.minHeight = 22f;
        }

        private void ApplyStaticButtonLabels()
        {
            if (!useLauncherLayout)
                return;

            if (strikeButton != null)
                SetButtonLabelText(strikeButton, "Strike v");
            if (jumpButton != null)
                SetButtonLabelText(jumpButton, "Jump [1/2]");

            if (tacticsLauncherButton != null)
                SetButtonLabelText(tacticsLauncherButton, "Tactics v");

            if (tripButton != null)
                SetButtonLabelText(tripButton, "Trip [1][ATK]");
            if (shoveButton != null)
                SetButtonLabelText(shoveButton, "Shove [1][ATK]");
            if (grappleButton != null)
                SetButtonLabelText(grappleButton, "Grapple [1][ATK]");
            if (repositionButton != null)
                SetButtonLabelText(repositionButton, "Reposition [1][ATK]");
            if (demoralizeButton != null)
                SetButtonLabelText(demoralizeButton, "Demoralize [1]");
            if (escapeButton != null)
                SetButtonLabelText(escapeButton, "Escape [1]");
            if (aidButton != null)
                SetButtonLabelText(aidButton, "Aid [1]");
            if (raiseShieldButton != null)
                SetButtonLabelText(raiseShieldButton, "Guard [1]");

            if (strikePopupStrikeButton != null)
                SetButtonLabelText(strikePopupStrikeButton, "Strike [1][ATK]");

            RefreshCastSpellModeButtonLabels();
            RefreshCastSpellButtonLabel();
        }

        private static void SetButtonLabelText(Button button, string text)
        {
            if (button == null)
                return;

            TMP_Text label = null;
            var directText = button.transform.Find("Text");
            if (directText != null)
                label = directText.GetComponent<TMP_Text>();

            if (label == null)
            {
                var directLabel = button.transform.Find("Label");
                if (directLabel != null)
                    label = directLabel.GetComponent<TMP_Text>();
            }

            if (label == null)
            {
                var labels = button.GetComponentsInChildren<TMP_Text>(true);
                if (labels != null && labels.Length > 0)
                    label = labels[0];
            }

            if (label != null)
                label.text = text;
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

            actionBarCommandCoordinator.Bind(targetingController, actionExecutor, RefreshAvailability);
            SubscribeCoreEvents();

            targetingController.OnModeChanged += HandleModeChanged;

            HandleModeChanged(targetingController.ActiveMode);
            RebuildAidPreparedCountsFromService();
            RefreshAvailability();
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                UnsubscribeCoreEvents();
            }

            if (targetingController != null)
                targetingController.OnModeChanged -= HandleModeChanged;

            CloseAllPopups();
        }

        private void Update()
        {
            if (!useLauncherLayout)
                return;
            if (!actionBarLauncherPresenter.AnyPopupOpen)
                return;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                CloseAllPopups();
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            if (targetingController != null && targetingController.IsSpellTargetingActive)
                return;

            Vector2 screen = mouse.position.ReadValue();
            if (actionBarLauncherPresenter.IsPointInsideLauncherOrPopup(screen))
                return;

            CloseAllPopups();
        }

        private void LateUpdate()
        {
            if (!useLauncherLayout || !actionBarLauncherPresenter.AnyPopupOpen)
                return;

            var rootCanvas = canvasGroup != null
                ? canvasGroup.GetComponentInParent<Canvas>()
                : GetComponentInParent<Canvas>();
            if (rootCanvas == null)
                return;

            var canvasRect = rootCanvas.transform as RectTransform;
            actionBarLauncherPresenter.ClampOpenPopupsToCanvas(canvasRect, popupClampPadding);
        }

        private void EnsureButtonListenersBound()
        {
            if (buttonListenersBound) return;

            int boundCount = 0;
            boundCount += BindButton(strikeButton, useLauncherLayout ? ToggleStrikePopup : actionBarCommandCoordinator.OnStrikeClicked);
            boundCount += BindButton(jumpButton, actionBarCommandCoordinator.OnJumpClicked);
            boundCount += BindButton(tripButton, useLauncherLayout ? HandleTripPopupClicked : actionBarCommandCoordinator.OnTripClicked);
            boundCount += BindButton(shoveButton, useLauncherLayout ? HandleShovePopupClicked : actionBarCommandCoordinator.OnShoveClicked);
            boundCount += BindButton(grappleButton, useLauncherLayout ? HandleGrapplePopupClicked : actionBarCommandCoordinator.OnGrappleClicked);
            boundCount += BindButton(repositionButton, useLauncherLayout ? HandleRepositionPopupClicked : actionBarCommandCoordinator.OnRepositionClicked);
            boundCount += BindButton(demoralizeButton, useLauncherLayout ? HandleDemoralizePopupClicked : actionBarCommandCoordinator.OnDemoralizeClicked);
            boundCount += BindButton(escapeButton, useLauncherLayout ? HandleEscapePopupClicked : actionBarCommandCoordinator.OnEscapeClicked);
            boundCount += BindButton(aidButton, useLauncherLayout ? HandleAidPopupClicked : actionBarCommandCoordinator.OnAidClicked);
            boundCount += BindButton(castSpellButton, useLauncherLayout ? ToggleCastPopup : actionBarCommandCoordinator.OnCastSpellClicked);
            boundCount += BindButton(castSpellModeStandardButton, useLauncherLayout ? HandleCastStandardPopupClicked : actionBarCommandCoordinator.OnCastSpellModeStandardClicked);
            boundCount += BindButton(castSpellModeGlassButton, useLauncherLayout ? HandleCastGlassPopupClicked : actionBarCommandCoordinator.OnCastSpellModeGlassClicked);
            boundCount += BindButton(castSpellModeSnowballButton, useLauncherLayout ? HandleCastSnowballPopupClicked : actionBarCommandCoordinator.OnCastSpellModeSnowballClicked);
            boundCount += BindButton(castSpellModeBurningHandsButton, useLauncherLayout ? HandleCastBurningHandsPopupClicked : actionBarCommandCoordinator.OnCastSpellModeBurningHandsClicked);
            boundCount += BindButton(castSpellModeFearButton, useLauncherLayout ? HandleCastFearPopupClicked : actionBarCommandCoordinator.OnCastSpellModeFearClicked);
            boundCount += BindButton(castSpellModeHealButton, useLauncherLayout ? HandleCastHealPopupClicked : actionBarCommandCoordinator.OnCastSpellModeHealClicked);
            boundCount += BindButton(castSpellModeHarmButton, useLauncherLayout ? HandleCastHarmPopupClicked : actionBarCommandCoordinator.OnCastSpellModeHarmClicked);
            boundCount += BindButton(raiseShieldButton, actionBarCommandCoordinator.OnRaiseShieldClicked);
            boundCount += BindButton(standButton, HandleMobilityClicked);
            boundCount += BindButton(tacticsLauncherButton, ToggleTacticsPopup);
            boundCount += BindButton(strikePopupStrikeButton, HandleStrikePopupStrikeClicked);
            boundCount += BindButton(spellCastOneActionButton, () => HandleForceBarrageActionCountClicked(1));
            boundCount += BindButton(spellCastTwoActionButton, () => HandleForceBarrageActionCountClicked(2));
            boundCount += BindButton(spellCastThreeActionButton, () => HandleForceBarrageActionCountClicked(3));
            boundCount += BindButton(spellCastConfirmButton, HandleSpellConfirmClicked);
            boundCount += BindButton(spellCastCancelButton, HandleSpellCancelClicked);

            if (boundCount > 0)
                buttonListenersBound = true;
        }

        private int BindButton(Button button, UnityEngine.Events.UnityAction handler)
        {
            if (button == null || handler == null) return 0;
            button.onClick.AddListener(handler);
            return 1;
        }

        private void ToggleStrikePopup()
        {
            if (!useLauncherLayout)
            {
                actionBarCommandCoordinator.OnStrikeClicked();
                return;
            }

            actionBarLauncherPresenter.ToggleStrikePopup();
        }

        private void ToggleTacticsPopup()
        {
            if (!useLauncherLayout)
                return;

            actionBarLauncherPresenter.ToggleTacticsPopup();
        }

        private void ToggleCastPopup()
        {
            if (!useLauncherLayout)
            {
                actionBarCommandCoordinator.OnCastSpellClicked();
                return;
            }

            if (targetingController != null && targetingController.IsSpellTargetingActive)
            {
                SetCastPopupVisible(true);
                RefreshSpellCastPanelContent();
                return;
            }

            actionBarLauncherPresenter.ToggleCastPopup();
            RefreshSpellCastPanelContent();
        }

        private void HandleStrikePopupStrikeClicked()
        {
            actionBarCommandCoordinator.OnStrikeClicked();
            CloseAllPopups();
        }

        private void HandleTripPopupClicked() => ExecutePopupAction(actionBarCommandCoordinator.OnTripClicked);
        private void HandleShovePopupClicked() => ExecutePopupAction(actionBarCommandCoordinator.OnShoveClicked);
        private void HandleGrapplePopupClicked() => ExecutePopupAction(actionBarCommandCoordinator.OnGrappleClicked);
        private void HandleRepositionPopupClicked() => ExecutePopupAction(actionBarCommandCoordinator.OnRepositionClicked);
        private void HandleDemoralizePopupClicked() => ExecutePopupAction(actionBarCommandCoordinator.OnDemoralizeClicked);
        private void HandleEscapePopupClicked() => ExecutePopupAction(actionBarCommandCoordinator.OnEscapeClicked);
        private void HandleAidPopupClicked() => ExecutePopupAction(actionBarCommandCoordinator.OnAidClicked);

        private void HandleCastStandardPopupClicked()
        {
            if (!useLauncherLayout)
            {
                actionBarCommandCoordinator.OnCastSpellModeStandardClicked();
                return;
            }

            actionBarCommandCoordinator.SelectSpell(SpellId.ForceBarrage);
            int shardCapacity = Mathf.Clamp(turnManager != null ? turnManager.ActionsRemaining : 1, 1, 3);
            if (actionBarCommandCoordinator.TryBeginForceBarrage(shardCapacity))
                SetCastPopupVisible(true);
        }

        private void HandleCastGlassPopupClicked()
        {
            if (!useLauncherLayout)
            {
                actionBarCommandCoordinator.OnCastSpellModeGlassClicked();
                return;
            }

            actionBarCommandCoordinator.SelectSpell(SpellId.ElectricArc);
            if (actionBarCommandCoordinator.TryBeginElectricArc())
                SetCastPopupVisible(true);
        }

        private void HandleCastSnowballPopupClicked()
        {
            if (!useLauncherLayout)
            {
                actionBarCommandCoordinator.OnCastSpellModeSnowballClicked();
                return;
            }

            actionBarCommandCoordinator.SelectSpell(SpellId.Snowball);
            if (actionBarCommandCoordinator.TryBeginSnowball())
                SetCastPopupVisible(true);
        }

        private void HandleCastBurningHandsPopupClicked()
        {
            if (!useLauncherLayout)
            {
                actionBarCommandCoordinator.OnCastSpellModeBurningHandsClicked();
                return;
            }

            actionBarCommandCoordinator.SelectSpell(SpellId.BurningHands);
            if (actionBarCommandCoordinator.TryBeginBurningHands())
                SetCastPopupVisible(true);
        }

        private void HandleCastFearPopupClicked()
        {
            if (!useLauncherLayout)
            {
                actionBarCommandCoordinator.OnCastSpellModeFearClicked();
                return;
            }

            actionBarCommandCoordinator.SelectSpell(SpellId.Fear);
            if (actionBarCommandCoordinator.TryBeginFear())
                SetCastPopupVisible(true);
        }

        private void HandleCastHealPopupClicked()
        {
            if (!useLauncherLayout)
            {
                actionBarCommandCoordinator.OnCastSpellModeHealClicked();
                return;
            }

            actionBarCommandCoordinator.SelectSpell(SpellId.Heal);
            if (actionBarCommandCoordinator.TryBeginHeal(actionBarCommandCoordinator.CurrentHealActionCount))
                SetCastPopupVisible(true);
        }

        private void HandleCastHarmPopupClicked()
        {
            if (!useLauncherLayout)
            {
                actionBarCommandCoordinator.OnCastSpellModeHarmClicked();
                return;
            }

            actionBarCommandCoordinator.SelectSpell(SpellId.Harm);
            if (actionBarCommandCoordinator.TryBeginHarm(actionBarCommandCoordinator.CurrentHarmActionCount))
                SetCastPopupVisible(true);
        }

        private void HandleForceBarrageActionCountClicked(int actionCount)
        {
            if (!useLauncherLayout)
                return;

            SpellId spellId = targetingController != null
                ? targetingController.ActiveSpellId ?? actionBarCommandCoordinator.CurrentSelectedSpell
                : actionBarCommandCoordinator.CurrentSelectedSpell;

            if (spellId == SpellId.Heal)
            {
                if (actionBarCommandCoordinator.TryBeginHeal(actionCount))
                    SetCastPopupVisible(true);
                return;
            }

            if (spellId == SpellId.Harm)
            {
                if (actionBarCommandCoordinator.TryBeginHarm(actionCount))
                    SetCastPopupVisible(true);
            }
        }

        private void HandleSpellConfirmClicked()
        {
            if (targetingController != null && targetingController.IsSpellTargetingActive)
            {
                if (actionBarCommandCoordinator.TryConfirmSpellTargeting())
                    CloseAllPopups();
            }
        }

        private void HandleSpellCancelClicked()
        {
            if (targetingController != null && targetingController.IsSpellTargetingActive)
                actionBarCommandCoordinator.CancelSpellTargeting();

            CloseAllPopups();
        }

        private void ExecutePopupAction(System.Action action)
        {
            action?.Invoke();
            CloseAllPopups();
        }

        private void SetStrikePopupVisible(bool visible)
        {
            if (!useLauncherLayout)
                return;
            actionBarLauncherPresenter.SetStrikePopupVisible(visible);
        }

        private void SetTacticsPopupVisible(bool visible)
        {
            if (!useLauncherLayout)
                return;
            actionBarLauncherPresenter.SetTacticsPopupVisible(visible);
        }

        private void SetCastPopupVisible(bool visible)
        {
            if (!useLauncherLayout)
                return;
            actionBarLauncherPresenter.SetCastPopupVisible(visible);
        }

        private void CloseAllPopups()
        {
            if (!useLauncherLayout)
                return;
            actionBarLauncherPresenter.CloseAllPopups();
        }

        private void SubscribeCoreEvents()
        {
            eventBus.OnCombatStartedTyped += HandleCombatStarted;
            eventBus.OnCombatEndedTyped += HandleCombatEnded;
            eventBus.OnTurnStartedTyped += HandleTurnStarted;
            eventBus.OnTurnEndedTyped += HandleTurnEnded;
            eventBus.OnActionsChangedTyped += HandleActionsChanged;
            eventBus.OnConditionChangedTyped += HandleConditionChanged;
            eventBus.OnShieldRaisedTyped += HandleShieldRaised;
            eventBus.OnAidPreparedTyped += HandleAidPrepared;
            eventBus.OnAidClearedTyped += HandleAidCleared;
        }

        private void UnsubscribeCoreEvents()
        {
            eventBus.OnCombatStartedTyped -= HandleCombatStarted;
            eventBus.OnCombatEndedTyped -= HandleCombatEnded;
            eventBus.OnTurnStartedTyped -= HandleTurnStarted;
            eventBus.OnTurnEndedTyped -= HandleTurnEnded;
            eventBus.OnActionsChangedTyped -= HandleActionsChanged;
            eventBus.OnConditionChangedTyped -= HandleConditionChanged;
            eventBus.OnShieldRaisedTyped -= HandleShieldRaised;
            eventBus.OnAidPreparedTyped -= HandleAidPrepared;
            eventBus.OnAidClearedTyped -= HandleAidCleared;
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

            spellPanelPinnedByActiveTargeting = false;
            SetCombatVisible(false);
            SetCastSpellUiVisible(false);
            SetAllInteractable(false);
            SetCastSpellModeButtonsInteractable(false);
            RefreshCastSpellModeButtonsVisual();
            RefreshCastSpellButtonLabel();
            RefreshSpellCastPanelContent();
            ClearAllHighlights();
            aidPreparedIndicatorPresenter.Clear();
            RefreshAidPreparedIndicator();
        }

        private void HandleTurnStarted(in TurnStartedEvent e)
        {
            RefreshAvailability();
        }

        private void HandleTurnEnded(in TurnEndedEvent e)
        {
            if (targetingController != null && targetingController.ActiveMode != TargetingMode.None)
                targetingController.CancelTargeting();

            spellPanelPinnedByActiveTargeting = false;
            SetAllInteractable(false);
            SetCastSpellModeButtonsInteractable(false);
            RefreshCastSpellModeButtonsVisual();
            RefreshCastSpellButtonLabel();
            RefreshSpellCastPanelContent();
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

        private void HandleAidPrepared(in AidPreparedEvent e)
        {
            aidPreparedIndicatorPresenter.HandleAidPrepared(in e);
            RefreshAvailability();
        }

        private void HandleAidCleared(in AidClearedEvent e)
        {
            aidPreparedIndicatorPresenter.HandleAidCleared(in e);
            RefreshAvailability();
        }

        private void HandleModeChanged(TargetingMode mode)
        {
            bool spellTargetingActive = targetingController != null && targetingController.IsSpellTargetingActive;
            if (useLauncherLayout)
            {
                if (spellTargetingActive)
                {
                    spellPanelPinnedByActiveTargeting = true;
                    SetCastPopupVisible(true);
                }
                else if (spellPanelPinnedByActiveTargeting)
                {
                    spellPanelPinnedByActiveTargeting = false;
                    SetCastPopupVisible(false);
                }
            }

            SetHighlight(strikeHighlight, mode == TargetingMode.Strike);
            SetHighlight(jumpHighlight, mode == TargetingMode.Jump);
            SetHighlight(tripHighlight, mode == TargetingMode.Trip);
            SetHighlight(shoveHighlight, mode == TargetingMode.Shove);
            SetHighlight(grappleHighlight, mode == TargetingMode.Grapple);
            SetHighlight(repositionHighlight, mode == TargetingMode.Reposition);
            SetHighlight(demoralizeHighlight, mode == TargetingMode.Demoralize);
            SetHighlight(escapeHighlight, mode == TargetingMode.Escape);
            SetHighlight(aidHighlight, mode == TargetingMode.Aid);
            SetHighlight(castSpellHighlight, targetingController != null && targetingController.IsSpellTargetingActive);
            SetHighlight(raiseShieldHighlight, false);
            SetHighlight(standHighlight, mode == TargetingMode.Step);

            RefreshAvailability();
        }

        private void HandleMobilityClicked()
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return;

            var actor = turnManager.CurrentEntity;
            var actorData = actor.IsValid ? entityManager.Registry.Get(actor) : null;
            if (actorData != null && actorData.HasCondition(ConditionType.Prone))
                actionBarCommandCoordinator.OnStandClicked();
            else
                actionBarCommandCoordinator.OnStepClicked();
        }

        private void RefreshMobilityButtonLabel(EntityData actorData)
        {
            if (standButton == null)
                return;

            bool showStand = actorData != null && actorData.HasCondition(ConditionType.Prone);
            SetButtonLabelText(standButton, showStand ? "Stand [1]" : "Step [1]");
        }

        private void RefreshAvailability()
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null || actionExecutor == null)
            {
                CloseAllPopups();
                SetCastSpellUiVisible(false);
                SetAllInteractable(false);
                aidPreparedIndicatorPresenter.Clear();
                SetCastSpellModeButtonsInteractable(false);
                RefreshCastSpellModeButtonsVisual();
                RefreshCastSpellButtonLabel();
                RefreshSpellCastPanelContent();
                RefreshAidPreparedIndicator();
                RefreshMobilityButtonLabel(null);
                return;
            }

            var actor = turnManager.CurrentEntity;
            var actorData = entityManager.Registry.Get(actor);
            actionBarCommandCoordinator.SyncSpellSelection(actorData, turnManager.ActionsRemaining);
            SetCastSpellUiVisible(ShouldShowCastSpellUi(actorData));

            if (turnManager.IsDelayReturnWindowOpen || turnManager.IsDelayPlacementSelectionOpen)
            {
                CloseAllPopups();
                SetAllInteractable(false);
                SetCastSpellModeButtonsInteractable(false);
                RefreshCastSpellModeButtonsVisual();
                RefreshCastSpellButtonLabel();
                RefreshSpellCastPanelContent();
                RefreshAidPreparedIndicator();
                RefreshMobilityButtonLabel(actorData);
                return;
            }

            if (!actionBarAvailabilityPolicy.TryEvaluate(
                turnManager,
                actionExecutor,
                entityManager.Registry,
                out var availability))
            {
                CloseAllPopups();
                SetAllInteractable(false);
                SetCastSpellModeButtonsInteractable(false);
                RefreshCastSpellModeButtonsVisual();
                RefreshCastSpellButtonLabel();
                RefreshSpellCastPanelContent();
                RefreshAidPreparedIndicator();
                RefreshMobilityButtonLabel(actorData);
                return;
            }

            ApplyActionAvailability(in availability);

            bool canAdjustCastSpellMode =
                actorData != null &&
                !actionExecutor.IsBusy &&
                turnManager.IsPlayerTurn &&
                (targetingController == null || !targetingController.IsSpellTargetingActive) &&
                !turnManager.IsDelayPlacementSelectionOpen &&
                !turnManager.IsDelayReturnWindowOpen;
            bool canSelectForceBarrage = canAdjustCastSpellMode
                && actionBarCommandCoordinator.CanSelectForceBarrage(actorData, turnManager.ActionsRemaining);
            bool canSelectElectricArc = canAdjustCastSpellMode
                && actionBarCommandCoordinator.CanSelectElectricArc(actorData, turnManager.ActionsRemaining);
            bool canSelectSnowball = canAdjustCastSpellMode
                && actionBarCommandCoordinator.CanSelectSnowball(actorData, turnManager.ActionsRemaining);
            bool canSelectBurningHands = canAdjustCastSpellMode
                && actionBarCommandCoordinator.CanSelectBurningHands(actorData, turnManager.ActionsRemaining);
            bool canSelectFear = canAdjustCastSpellMode
                && actionBarCommandCoordinator.CanSelectFear(actorData, turnManager.ActionsRemaining);
            bool canSelectHeal = canAdjustCastSpellMode
                && actionBarCommandCoordinator.CanSelectHeal(actorData, turnManager.ActionsRemaining);
            bool canSelectHarm = canAdjustCastSpellMode
                && actionBarCommandCoordinator.CanSelectHarm(actorData, turnManager.ActionsRemaining);
            SetCastSpellModeButtonsInteractable(canSelectForceBarrage || canSelectElectricArc || canSelectSnowball || canSelectBurningHands || canSelectFear || canSelectHeal || canSelectHarm);
            SetInteractable(castSpellModeStandardButton, canSelectForceBarrage);
            SetInteractable(castSpellModeGlassButton, canSelectElectricArc);
            SetInteractable(castSpellModeSnowballButton, canSelectSnowball);
            SetInteractable(castSpellModeBurningHandsButton, canSelectBurningHands);
            SetInteractable(castSpellModeFearButton, canSelectFear);
            SetInteractable(castSpellModeHealButton, canSelectHeal);
            SetInteractable(castSpellModeHarmButton, canSelectHarm);
            RefreshCastSpellModeButtonsVisual();

            RefreshCastSpellButtonLabel();
            RefreshSpellCastPanelContent();
            RefreshAidPreparedIndicator();
            ApplyStaticButtonLabels();
            RefreshMobilityButtonLabel(actorData);
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
            if (!enabled)
            {
                // Guard/Stand are contextual controls; when action bar is globally inactive
                // (enemy turn, delay windows, no actionable actor), hide them to avoid stale carry-over.
                SetButtonVisible(raiseShieldButton, false);
                SetButtonVisible(standButton, false);
            }

            if (useLauncherLayout)
            {
                SetInteractable(strikeButton, enabled);
                SetInteractable(jumpButton, enabled);
                SetInteractable(tacticsLauncherButton, enabled);
                SetInteractable(castSpellButton, enabled);
                SetInteractable(raiseShieldButton, enabled);
                SetInteractable(castSpellModeStandardButton, enabled);
                SetInteractable(castSpellModeGlassButton, enabled);
                SetInteractable(castSpellModeSnowballButton, enabled);
                SetInteractable(castSpellModeBurningHandsButton, enabled);
                SetInteractable(castSpellModeFearButton, enabled);
                SetInteractable(castSpellModeHealButton, enabled);
                SetInteractable(castSpellModeHarmButton, enabled);
                SetInteractable(tripButton, enabled);
                SetInteractable(shoveButton, enabled);
                SetInteractable(grappleButton, enabled);
                SetInteractable(repositionButton, enabled);
                SetInteractable(demoralizeButton, enabled);
                SetInteractable(escapeButton, enabled);
                SetInteractable(aidButton, enabled);
                SetInteractable(strikePopupStrikeButton, enabled);
                SetInteractable(standButton, enabled);
                SetInteractable(spellCastOneActionButton, enabled);
                SetInteractable(spellCastTwoActionButton, enabled);
                SetInteractable(spellCastThreeActionButton, enabled);
                SetInteractable(spellCastConfirmButton, enabled);
                SetInteractable(spellCastCancelButton, enabled);
            }
            else
            {
                SetInteractable(strikeButton, enabled);
                SetInteractable(jumpButton, enabled);
                SetInteractable(tripButton, enabled);
                SetInteractable(shoveButton, enabled);
                SetInteractable(grappleButton, enabled);
                SetInteractable(repositionButton, enabled);
                SetInteractable(demoralizeButton, enabled);
                SetInteractable(escapeButton, enabled);
                SetInteractable(aidButton, enabled);
                SetInteractable(castSpellButton, enabled);
                SetInteractable(castSpellModeStandardButton, enabled);
                SetInteractable(castSpellModeGlassButton, enabled);
                SetInteractable(castSpellModeSnowballButton, enabled);
                SetInteractable(castSpellModeBurningHandsButton, enabled);
                SetInteractable(castSpellModeFearButton, enabled);
                SetInteractable(castSpellModeHealButton, enabled);
                SetInteractable(castSpellModeHarmButton, enabled);
                SetInteractable(raiseShieldButton, enabled);
                SetInteractable(standButton, enabled);
                SetInteractable(spellCastOneActionButton, enabled);
                SetInteractable(spellCastTwoActionButton, enabled);
                SetInteractable(spellCastThreeActionButton, enabled);
                SetInteractable(spellCastConfirmButton, enabled);
                SetInteractable(spellCastCancelButton, enabled);
            }
        }

        private void ApplyActionAvailability(in ActionBarAvailabilityState availability)
        {
            if (useLauncherLayout)
            {
                bool anyStrikeOptions = availability.strikeInteractable
                                     || availability.tripInteractable
                                     || availability.shoveInteractable
                                     || availability.grappleInteractable
                                     || availability.repositionInteractable;
                bool anyTacticsOptions = availability.demoralizeInteractable
                                      || availability.escapeInteractable
                                      || availability.aidInteractable;

                SetInteractable(strikeButton, anyStrikeOptions);
                SetInteractable(jumpButton, availability.jumpInteractable);
                SetInteractable(strikePopupStrikeButton, availability.strikeInteractable);
                SetInteractable(tripButton, availability.tripInteractable);
                SetInteractable(shoveButton, availability.shoveInteractable);
                SetInteractable(grappleButton, availability.grappleInteractable);
                SetInteractable(repositionButton, availability.repositionInteractable);

                SetInteractable(tacticsLauncherButton, anyTacticsOptions);
                SetInteractable(demoralizeButton, availability.demoralizeInteractable);
                SetInteractable(escapeButton, availability.escapeInteractable);
                SetInteractable(aidButton, availability.aidInteractable);

                SetInteractable(castSpellButton, availability.castSpellInteractable);

                SetInteractable(raiseShieldButton, availability.raiseShieldInteractable);
                SetButtonVisible(raiseShieldButton, availability.guardVisible);

                SetButtonVisible(standButton, availability.stepVisible || availability.standVisible);
                SetInteractable(standButton, availability.stepInteractable || availability.standInteractable);
            }
            else
            {
                SetInteractable(strikeButton, availability.strikeInteractable);
                SetInteractable(jumpButton, availability.jumpInteractable);
                SetInteractable(tripButton, availability.tripInteractable);
                SetInteractable(shoveButton, availability.shoveInteractable);
                SetInteractable(grappleButton, availability.grappleInteractable);
                SetInteractable(repositionButton, availability.repositionInteractable);
                SetInteractable(demoralizeButton, availability.demoralizeInteractable);
                SetInteractable(escapeButton, availability.escapeInteractable);
                SetInteractable(aidButton, availability.aidInteractable);
                SetInteractable(castSpellButton, availability.castSpellInteractable);
                SetInteractable(raiseShieldButton, availability.raiseShieldInteractable);
                SetButtonVisible(raiseShieldButton, availability.guardVisible);
                SetInteractable(standButton, availability.stepInteractable || availability.standInteractable);
                SetButtonVisible(standButton, availability.stepVisible || availability.standVisible);
            }
        }

        private static void SetInteractable(Button button, bool enabled)
        {
            if (button != null) button.interactable = enabled;
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button == null)
                return;

            var go = button.gameObject;
            if (go == null || go.activeSelf == visible)
                return;

            try
            {
                go.SetActive(visible);
            }
            catch (System.IndexOutOfRangeException)
            {
                // Unity UI Selectable can throw transient IndexOutOfRange during scene activation/
                // teardown races (especially in EditMode scene-validation tests). Visibility is
                // best-effort here; skip this frame and allow next refresh to reconcile.
            }
        }

        private void ClearAllHighlights()
        {
            SetHighlight(strikeHighlight, false);
            SetHighlight(jumpHighlight, false);
            SetHighlight(tripHighlight, false);
            SetHighlight(shoveHighlight, false);
            SetHighlight(grappleHighlight, false);
            SetHighlight(repositionHighlight, false);
            SetHighlight(demoralizeHighlight, false);
            SetHighlight(escapeHighlight, false);
            SetHighlight(aidHighlight, false);
            SetHighlight(castSpellHighlight, false);
            SetHighlight(raiseShieldHighlight, false);
            SetHighlight(standHighlight, false);
        }

        private static void SetHighlight(Image image, bool active)
        {
            if (image == null) return;
            if (image.gameObject.activeSelf != active)
                image.gameObject.SetActive(active);
        }

        private void RebuildAidPreparedCountsFromService()
        {
            aidPreparedIndicatorPresenter.RebuildFromService(turnManager != null ? turnManager.AidService : null);
        }

        private void RefreshAidPreparedIndicator()
        {
            var actor = turnManager != null ? turnManager.CurrentEntity : default;
            aidPreparedIndicatorPresenter.RefreshForActor(
                actor,
                aidPreparedIndicatorRoot,
                aidPreparedIndicatorLabel,
                aidPreparedSingleText,
                aidPreparedCountFormat);
        }

        private void SetCastSpellModeButtonsInteractable(bool enabled)
        {
            if (castSpellModeSelectorRoot != null)
            {
                bool shouldBeVisible = castSpellButton != null && castSpellButton.gameObject.activeInHierarchy;
                if (useLauncherLayout)
                    shouldBeVisible = shouldBeVisible
                        && (actionBarLauncherPresenter.CastPopupOpen
                            || spellPanelPinnedByActiveTargeting
                            || (targetingController != null && targetingController.IsSpellTargetingActive));
                castSpellModeSelectorRoot.gameObject.SetActive(shouldBeVisible);
            }

            bool showSelectionButtons = !useLauncherLayout || targetingController == null || !targetingController.IsSpellTargetingActive;
            SetButtonVisible(castSpellModeStandardButton, showSelectionButtons);
            SetButtonVisible(castSpellModeGlassButton, showSelectionButtons);
            SetButtonVisible(castSpellModeSnowballButton, showSelectionButtons);
            SetButtonVisible(castSpellModeBurningHandsButton, showSelectionButtons);
            SetButtonVisible(castSpellModeFearButton, showSelectionButtons);
            SetButtonVisible(castSpellModeHealButton, showSelectionButtons);
            SetButtonVisible(castSpellModeHarmButton, showSelectionButtons);
            SetInteractable(castSpellModeStandardButton, enabled && showSelectionButtons);
            SetInteractable(castSpellModeGlassButton, enabled && showSelectionButtons);
            SetInteractable(castSpellModeSnowballButton, enabled && showSelectionButtons);
            SetInteractable(castSpellModeBurningHandsButton, enabled && showSelectionButtons);
            SetInteractable(castSpellModeFearButton, enabled && showSelectionButtons);
            SetInteractable(castSpellModeHealButton, enabled && showSelectionButtons);
            SetInteractable(castSpellModeHarmButton, enabled && showSelectionButtons);

            if (useLauncherLayout)
                SetSpellCastDetailVisible(!showSelectionButtons && castSpellModeSelectorRoot != null && castSpellModeSelectorRoot.gameObject.activeSelf);
        }

        private void SetCastSpellUiVisible(bool visible)
        {
            if (castSpellButton != null && castSpellButton.gameObject.activeSelf != visible)
                castSpellButton.gameObject.SetActive(visible);

            if (castSpellModeSelectorRoot != null)
            {
                bool modeRootVisible = visible
                    && (!useLauncherLayout
                        || actionBarLauncherPresenter.CastPopupOpen
                        || spellPanelPinnedByActiveTargeting
                        || (targetingController != null && targetingController.IsSpellTargetingActive));
                if (castSpellModeSelectorRoot.gameObject.activeSelf != modeRootVisible)
                    castSpellModeSelectorRoot.gameObject.SetActive(modeRootVisible);
            }
        }

        private void SetSpellCastDetailVisible(bool visible)
        {
            if (spellCastDetailRoot == null)
                return;

            if (spellCastDetailRoot.gameObject.activeSelf != visible)
                spellCastDetailRoot.gameObject.SetActive(visible);
        }

        private void SetTargetingHintPanelVisible(bool visible)
        {
            if (targetingHintPanelRoot == null || targetingHintPanelRoot.activeSelf == visible)
                return;

            targetingHintPanelRoot.SetActive(visible);
        }

        private void UpdateSpellCastPanelPlacement(bool showDetailPanel)
        {
            if (castSpellModeSelectorRoot == null || castSpellButton == null)
                return;

            float verticalOffset = showDetailPanel ? CastPopupDetailOffsetY : CastPopupSelectionOffsetY;
            castSpellModeSelectorRoot.SetParent(castSpellButton.transform, false);
            castSpellModeSelectorRoot.anchorMin = new Vector2(0.5f, 1f);
            castSpellModeSelectorRoot.anchorMax = new Vector2(0.5f, 1f);
            castSpellModeSelectorRoot.pivot = new Vector2(0.5f, 0f);
            castSpellModeSelectorRoot.anchoredPosition = new Vector2(0f, verticalOffset);
            actionBarLauncherPresenter.SetCastPopupVerticalOffset(verticalOffset);
        }

        private void RefreshSpellCastPanelContent()
        {
            if (!useLauncherLayout)
                return;

            EnsureSpellCastPanelUi();

            if (castSpellModeSelectorRoot != null
                && targetingController != null
                && targetingController.IsSpellTargetingActive
                && !castSpellModeSelectorRoot.gameObject.activeSelf)
            {
                castSpellModeSelectorRoot.gameObject.SetActive(true);
            }

            bool rootVisible = castSpellModeSelectorRoot != null && castSpellModeSelectorRoot.gameObject.activeSelf;
            bool spellTargetingActive = targetingController != null && targetingController.IsSpellTargetingActive;
            bool showDetailPanel = spellTargetingActive;

            SetButtonVisible(castSpellModeStandardButton, rootVisible && !showDetailPanel);
            SetButtonVisible(castSpellModeGlassButton, rootVisible && !showDetailPanel);
            SetButtonVisible(castSpellModeSnowballButton, rootVisible && !showDetailPanel);
            SetButtonVisible(castSpellModeBurningHandsButton, rootVisible && !showDetailPanel);
            SetButtonVisible(castSpellModeFearButton, rootVisible && !showDetailPanel);
            SetButtonVisible(castSpellModeHealButton, rootVisible && !showDetailPanel);
            SetButtonVisible(castSpellModeHarmButton, rootVisible && !showDetailPanel);
            SetSpellCastDetailVisible(rootVisible && showDetailPanel);
            SetTargetingHintPanelVisible(!showDetailPanel);
            UpdateSpellCastPanelPlacement(showDetailPanel);

            if (spellCastPanelContentRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(spellCastPanelContentRoot);
            if (castSpellModeSelectorRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(castSpellModeSelectorRoot);

            if (!rootVisible || !showDetailPanel || spellCastTitleLabel == null || spellCastSummaryLabel == null)
                return;

            SpellId spellId = targetingController.ActiveSpellId ?? SpellId.ForceBarrage;

            switch (spellId)
            {
                case SpellId.ForceBarrage:
                    RefreshForceBarrageSpellPanel();
                    break;

                case SpellId.ElectricArc:
                    RefreshElectricArcSpellPanel();
                    break;

                case SpellId.Snowball:
                    RefreshSnowballSpellPanel();
                    break;

                case SpellId.BurningHands:
                    RefreshBurningHandsSpellPanel();
                    break;

                case SpellId.Fear:
                    RefreshFearSpellPanel();
                    break;

                case SpellId.Heal:
                    RefreshHealSpellPanel();
                    break;

                case SpellId.Harm:
                    RefreshHarmSpellPanel();
                    break;
            }
        }

        private void RefreshForceBarrageSpellPanel()
        {
            if (spellCastTitleLabel == null || spellCastSummaryLabel == null || targetingController == null)
                return;

            int shardCapacity = Mathf.Max(1, targetingController.ForceBarrageShardCapacity);
            int assignedShardCount = targetingController.ForceBarrageAssignedShardCount;

            spellCastTitleLabel.text = "Force Barrage";
            spellCastSummaryLabel.text = BuildForceBarrageSummary(shardCapacity);

            if (spellCastActionCountRow != null && spellCastActionCountRow.gameObject.activeSelf)
                spellCastActionCountRow.gameObject.SetActive(false);

            SetButtonLabelText(
                spellCastConfirmButton,
                $"Confirm [{SpellCatalog.GetShortToken(SpellId.ForceBarrage, Mathf.Max(1, assignedShardCount))}]");
            SetInteractable(spellCastConfirmButton, targetingController.CanConfirmSpellTargeting);
            SetInteractable(spellCastCancelButton, true);
        }

        private void RefreshElectricArcSpellPanel()
        {
            if (spellCastTitleLabel == null || spellCastSummaryLabel == null || targetingController == null)
                return;

            spellCastTitleLabel.text = "Electric Arc";
            spellCastSummaryLabel.text = BuildElectricArcSummary();

            if (spellCastActionCountRow != null && spellCastActionCountRow.gameObject.activeSelf)
                spellCastActionCountRow.gameObject.SetActive(false);

            SetButtonLabelText(spellCastConfirmButton, $"Confirm [{SpellCatalog.GetShortToken(SpellId.ElectricArc)}]");
            SetInteractable(spellCastConfirmButton, targetingController.CanConfirmSpellTargeting);
            SetInteractable(spellCastCancelButton, true);
        }

        private void RefreshSnowballSpellPanel()
        {
            if (spellCastTitleLabel == null || spellCastSummaryLabel == null || targetingController == null)
                return;

            spellCastTitleLabel.text = "Snowball";
            spellCastSummaryLabel.text = BuildSnowballSummary();

            if (spellCastActionCountRow != null && spellCastActionCountRow.gameObject.activeSelf)
                spellCastActionCountRow.gameObject.SetActive(false);

            SetButtonLabelText(spellCastConfirmButton, $"Confirm [{SpellCatalog.GetShortToken(SpellId.Snowball)}]");
            SetInteractable(spellCastConfirmButton, targetingController.CanConfirmSpellTargeting);
            SetInteractable(spellCastCancelButton, true);
        }

        private void RefreshBurningHandsSpellPanel()
        {
            if (spellCastTitleLabel == null || spellCastSummaryLabel == null || targetingController == null)
                return;

            spellCastTitleLabel.text = "Burning Hands";
            spellCastSummaryLabel.text = BuildBurningHandsSummary();

            if (spellCastActionCountRow != null && spellCastActionCountRow.gameObject.activeSelf)
                spellCastActionCountRow.gameObject.SetActive(false);

            SetButtonLabelText(spellCastConfirmButton, $"Confirm [{SpellCatalog.GetShortToken(SpellId.BurningHands)}]");
            SetInteractable(spellCastConfirmButton, targetingController.CanConfirmSpellTargeting);
            SetInteractable(spellCastCancelButton, true);
        }

        private void RefreshFearSpellPanel()
        {
            if (spellCastTitleLabel == null || spellCastSummaryLabel == null || targetingController == null)
                return;

            spellCastTitleLabel.text = "Fear";
            spellCastSummaryLabel.text = BuildFearSummary();

            if (spellCastActionCountRow != null && spellCastActionCountRow.gameObject.activeSelf)
                spellCastActionCountRow.gameObject.SetActive(false);

            SetButtonLabelText(spellCastConfirmButton, $"Confirm [{SpellCatalog.GetShortToken(SpellId.Fear)}]");
            SetInteractable(spellCastConfirmButton, targetingController.CanConfirmSpellTargeting);
            SetInteractable(spellCastCancelButton, true);
        }

        private void RefreshHealSpellPanel()
        {
            if (spellCastTitleLabel == null || spellCastSummaryLabel == null || targetingController == null)
                return;

            int actionCount = GetActiveHealActionCount();
            spellCastTitleLabel.text = "Heal";
            spellCastSummaryLabel.text = BuildHealSummary();

            if (spellCastActionCountRow != null && !spellCastActionCountRow.gameObject.activeSelf)
                spellCastActionCountRow.gameObject.SetActive(true);

            SetButtonVisible(spellCastOneActionButton, true);
            SetButtonVisible(spellCastTwoActionButton, true);
            SetButtonVisible(spellCastThreeActionButton, true);

            int actionsRemaining = turnManager != null ? turnManager.ActionsRemaining : 0;
            ApplySpellPanelSelectorState(spellCastOneActionButton, actionCount == 1, actionsRemaining >= 1);
            ApplySpellPanelSelectorState(spellCastTwoActionButton, actionCount == 2, actionsRemaining >= 2);
            ApplySpellPanelSelectorState(spellCastThreeActionButton, actionCount == 3, actionsRemaining >= 3);

            SetButtonLabelText(spellCastConfirmButton, $"Confirm [{SpellCatalog.GetShortToken(SpellId.Heal, actionCount)}]");
            SetInteractable(spellCastConfirmButton, targetingController.CanConfirmSpellTargeting);
            SetInteractable(spellCastCancelButton, true);
        }

        private void RefreshHarmSpellPanel()
        {
            if (spellCastTitleLabel == null || spellCastSummaryLabel == null || targetingController == null)
                return;

            int actionCount = GetActiveHarmActionCount();
            spellCastTitleLabel.text = "Harm";
            spellCastSummaryLabel.text = BuildHarmSummary();

            if (spellCastActionCountRow != null && !spellCastActionCountRow.gameObject.activeSelf)
                spellCastActionCountRow.gameObject.SetActive(true);

            SetButtonVisible(spellCastOneActionButton, true);
            SetButtonVisible(spellCastTwoActionButton, true);
            SetButtonVisible(spellCastThreeActionButton, true);

            int actionsRemaining = turnManager != null ? turnManager.ActionsRemaining : 0;
            ApplySpellPanelSelectorState(spellCastOneActionButton, actionCount == 1, actionsRemaining >= 1);
            ApplySpellPanelSelectorState(spellCastTwoActionButton, actionCount == 2, actionsRemaining >= 2);
            ApplySpellPanelSelectorState(spellCastThreeActionButton, actionCount == 3, actionsRemaining >= 3);

            SetButtonLabelText(spellCastConfirmButton, $"Confirm [{SpellCatalog.GetShortToken(SpellId.Harm, actionCount)}]");
            SetInteractable(spellCastConfirmButton, targetingController.CanConfirmSpellTargeting);
            SetInteractable(spellCastCancelButton, true);
        }

        private void ApplySpellPanelSelectorState(Button button, bool selected, bool interactable)
        {
            ApplyCastSpellModeButtonVisual(button, selected);
            SetInteractable(button, interactable);
        }

        private string BuildForceBarrageSummary(int shardCapacity)
        {
            int assignedShardCount = targetingController != null
                ? targetingController.ForceBarrageAssignedShardCount
                : 0;
            var summary = new StringBuilder();
            summary.Append("Range 120 ft. 1 shard per click.");
            summary.Append('\n');
            summary.Append(assignedShardCount);
            summary.Append('/');
            summary.Append(shardCapacity);
            summary.Append(" shards assigned.");

            string allocationSummary = BuildGroupedTargetSummary(
                targetingController != null ? targetingController.ForceBarrageAssignedTargets : null,
                "x");
            if (!string.IsNullOrEmpty(allocationSummary))
            {
                summary.Append('\n');
                summary.Append(allocationSummary);
            }

            if (assignedShardCount <= 0)
                summary.Append("\nClick a creature to add the first shard.");
            else if (assignedShardCount < shardCapacity)
                summary.Append("\nKeep clicking to add shards, or Confirm now to spend the assigned actions.");
            else
                summary.Append("\nAll shards assigned. Confirm to cast or Esc to cancel.");

            return summary.ToString();
        }

        private string BuildElectricArcSummary()
        {
            int selectedTargetCount = targetingController != null ? targetingController.ElectricArcSelectedTargetCount : 0;
            var summary = new StringBuilder();
            summary.Append("Range 30 ft. 2d4 electricity, basic Reflex.");
            summary.Append('\n');
            summary.Append(selectedTargetCount);
            summary.Append("/2 targets selected.");

            string targetSummary = BuildSelectedTargetList(targetingController != null ? targetingController.ElectricArcSelectedTargets : null);
            if (!string.IsNullOrEmpty(targetSummary))
            {
                summary.Append('\n');
                summary.Append(targetSummary);
            }
            else
            {
                summary.Append("\nChoose one or two visible creatures.");
            }

            if (selectedTargetCount > 0)
                summary.Append("\nConfirm to cast or Esc to cancel.");

            return summary.ToString();
        }

        private string BuildSnowballSummary()
        {
            int selectedTargetCount = targetingController != null ? targetingController.SnowballSelectedTargetCount : 0;
            var summary = new StringBuilder();
            summary.Append("Range 30 ft. 2d4 cold, spell attack.");
            summary.Append('\n');
            summary.Append(selectedTargetCount);
            summary.Append("/1 target selected.");

            string targetSummary = BuildSelectedTargetList(targetingController != null ? targetingController.SnowballSelectedTargets : null);
            if (!string.IsNullOrEmpty(targetSummary))
            {
                summary.Append('\n');
                summary.Append(targetSummary);
                summary.Append("\nConfirm to cast or Esc to cancel.");
            }
            else
            {
                summary.Append("\nChoose a visible creature.");
            }

            return summary.ToString();
        }

        private string BuildBurningHandsSummary()
        {
            var summary = new StringBuilder();
            summary.Append("15 ft cone. 2d6 fire, basic Reflex.");

            if (targetingController != null && targetingController.TryGetSelectedSpellAreaPreview(out var preview))
            {
                summary.Append('\n');
                summary.Append(preview.TargetCount);
                summary.Append(" target(s) in area");
                if (preview.enemyCount > 0 || preview.allyCount > 0)
                {
                    summary.Append(" (");
                    summary.Append(preview.enemyCount);
                    summary.Append(" enemy, ");
                    summary.Append(preview.allyCount);
                    summary.Append(" ally)");
                }
                summary.Append('.');

                string targetSummary = BuildSelectedTargetList(preview.targets);
                if (!string.IsNullOrEmpty(targetSummary))
                {
                    summary.Append('\n');
                    summary.Append(targetSummary);
                }

                if (preview.allyCount > 0)
                    summary.Append("\nWarning: allies are inside the cone.");

                summary.Append("\nConfirm to cast or Esc to cancel.");
            }
            else
            {
                summary.Append("\nChoose a cell or creature to set the cone direction.");
            }

            return summary.ToString();
        }

        private string BuildFearSummary()
        {
            int selectedTargetCount = targetingController != null ? targetingController.FearSelectedTargetCount : 0;
            var summary = new StringBuilder();
            summary.Append("Range 30 ft. Will save; crit failure also applies fleeing for 1 round.");
            summary.Append('\n');
            summary.Append(selectedTargetCount);
            summary.Append("/1 target selected.");

            string targetSummary = BuildSelectedTargetList(targetingController != null ? targetingController.FearSelectedTargets : null);
            if (!string.IsNullOrEmpty(targetSummary))
            {
                summary.Append('\n');
                summary.Append(targetSummary);
                summary.Append("\nSuccess = frightened 1, failure = frightened 2, crit failure = frightened 3 + fleeing 1 round.");
                summary.Append("\nConfirm to cast or Esc to cancel.");
            }
            else
            {
                summary.Append("\nChoose a visible creature.");
            }

            return summary.ToString();
        }

        private string BuildHealSummary()
        {
            int actionCount = GetActiveHealActionCount();
            var summary = new StringBuilder();
            if (actionCount >= 3)
            {
                summary.Append("30 ft emanation. Living creatures heal 1d8. Undead take 1d8 vitality, basic Fort.");

                if (targetingController != null && targetingController.TryGetSelectedSpellAreaPreview(out var preview))
                {
                    int livingCount = 0;
                    int undeadCount = 0;
                    for (int i = 0; i < preview.targets.Length; i++)
                    {
                        var data = entityManager != null && entityManager.Registry != null
                            ? entityManager.Registry.Get(preview.targets[i])
                            : null;
                        if (data == null)
                            continue;

                        if (data.VitalityAffinity == VitalityAffinity.Undead)
                            undeadCount++;
                        else
                            livingCount++;
                    }

                    summary.Append('\n');
                    summary.Append(preview.TargetCount);
                    summary.Append(" creature(s) in area (");
                    summary.Append(livingCount);
                    summary.Append(" living, ");
                    summary.Append(undeadCount);
                    summary.Append(" undead).");

                    string areaTargetSummary = BuildSelectedTargetList(preview.targets);
                    if (!string.IsNullOrEmpty(areaTargetSummary))
                    {
                        summary.Append('\n');
                        summary.Append(areaTargetSummary);
                    }

                    summary.Append("\nConfirm to cast or Esc to cancel.");
                }
                else
                {
                    summary.Append("\nThe emanation is centered on the caster.");
                }

                return summary.ToString();
            }

            int selectedTargetCount = targetingController != null ? targetingController.HealSelectedTargetCount : 0;
            if (actionCount >= 2)
                summary.Append("Range 30 ft. Living: 1d8+8 healing. Undead: 1d8 vitality, basic Fort.");
            else
                summary.Append("Touch. Living: 1d8 healing. Undead: 1d8 vitality, basic Fort.");

            summary.Append('\n');
            summary.Append(selectedTargetCount);
            summary.Append("/1 target selected.");

            string targetSummary = BuildSelectedTargetList(targetingController != null ? targetingController.HealSelectedTargets : null);
            if (!string.IsNullOrEmpty(targetSummary))
            {
                summary.Append('\n');
                summary.Append(targetSummary);
                summary.Append("\nConfirm to cast or Esc to cancel.");
            }
            else
            {
                summary.Append("\nChoose self, an ally, or an undead creature.");
            }

            return summary.ToString();
        }

        private string BuildHarmSummary()
        {
            int actionCount = GetActiveHarmActionCount();
            var summary = new StringBuilder();
            if (actionCount >= 3)
            {
                summary.Append("30 ft emanation. Living creatures take 1d8 void, basic Fort. Undead heal 1d8.");

                if (targetingController != null && targetingController.TryGetSelectedSpellAreaPreview(out var preview))
                {
                    int livingCount = 0;
                    int undeadCount = 0;
                    for (int i = 0; i < preview.targets.Length; i++)
                    {
                        var data = entityManager != null && entityManager.Registry != null
                            ? entityManager.Registry.Get(preview.targets[i])
                            : null;
                        if (data == null)
                            continue;

                        if (data.VitalityAffinity == VitalityAffinity.Undead)
                            undeadCount++;
                        else
                            livingCount++;
                    }

                    summary.Append('\n');
                    summary.Append(preview.TargetCount);
                    summary.Append(" creature(s) in area (");
                    summary.Append(livingCount);
                    summary.Append(" living, ");
                    summary.Append(undeadCount);
                    summary.Append(" undead).");

                    string areaTargetSummary = BuildSelectedTargetList(preview.targets);
                    if (!string.IsNullOrEmpty(areaTargetSummary))
                    {
                        summary.Append('\n');
                        summary.Append(areaTargetSummary);
                    }

                    if (preview.allyCount > 0)
                        summary.Append("\nWarning: living allies are inside the emanation.");

                    summary.Append("\nConfirm to cast or Esc to cancel.");
                }
                else
                {
                    summary.Append("\nThe emanation is centered on the caster.");
                }

                return summary.ToString();
            }

            int selectedTargetCount = targetingController != null ? targetingController.HarmSelectedTargetCount : 0;
            if (actionCount >= 2)
                summary.Append("Range 30 ft. Living enemy: 1d8+8 void, basic Fort. Undead: 1d8+8 healing.");
            else
                summary.Append("Touch. Living enemy: 1d8 void, basic Fort. Undead: 1d8 healing.");

            summary.Append('\n');
            summary.Append(selectedTargetCount);
            summary.Append("/1 target selected.");

            string targetSummary = BuildSelectedTargetList(targetingController != null ? targetingController.HarmSelectedTargets : null);
            if (!string.IsNullOrEmpty(targetSummary))
            {
                summary.Append('\n');
                summary.Append(targetSummary);
                summary.Append("\nConfirm to cast or Esc to cancel.");
            }
            else
            {
                summary.Append("\nChoose a living enemy or an undead creature.");
            }

            return summary.ToString();
        }

        private string BuildGroupedTargetSummary(IReadOnlyList<EntityHandle> targets, string countSeparator)
        {
            if (targets == null || targets.Count == 0)
                return string.Empty;

            var orderedNames = new List<string>(targets.Count);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < targets.Count; i++)
            {
                string targetName = ResolveEntityName(targets[i]);
                if (counts.TryGetValue(targetName, out int currentCount))
                {
                    counts[targetName] = currentCount + 1;
                    continue;
                }

                counts.Add(targetName, 1);
                orderedNames.Add(targetName);
            }

            var summary = new StringBuilder();
            for (int i = 0; i < orderedNames.Count; i++)
            {
                if (i > 0)
                    summary.Append(", ");

                string targetName = orderedNames[i];
                summary.Append(targetName);
                summary.Append(' ');
                summary.Append(countSeparator);
                summary.Append(counts[targetName]);
            }

            return summary.ToString();
        }

        private string BuildSelectedTargetList(IReadOnlyList<EntityHandle> targets)
        {
            if (targets == null || targets.Count == 0)
                return string.Empty;

            var summary = new StringBuilder();
            for (int i = 0; i < targets.Count; i++)
            {
                if (i > 0)
                    summary.Append(", ");

                summary.Append(ResolveEntityName(targets[i]));
            }

            return summary.ToString();
        }

        private string ResolveEntityName(EntityHandle handle)
        {
            if (entityManager == null || entityManager.Registry == null || !handle.IsValid)
                return "Unknown";

            var data = entityManager.Registry.Get(handle);
            if (data == null || string.IsNullOrWhiteSpace(data.Name))
                return "Unknown";

            return data.Name;
        }

        private static bool ShouldShowCastSpellUi(EntityData actorData)
        {
            return actorData != null
                && actorData.IsAlive
                && actorData.KnowsAnyActionBarSpell;
        }

        private void RefreshCastSpellModeButtonsVisual()
        {
            RefreshCastSpellModeButtonLabels();

            var selectedSpell = actionBarCommandCoordinator.CurrentSelectedSpell;
            ApplyCastSpellModeButtonVisual(castSpellModeStandardButton, selectedSpell == SpellId.ForceBarrage);
            ApplyCastSpellModeButtonVisual(castSpellModeGlassButton, selectedSpell == SpellId.ElectricArc);
            ApplyCastSpellModeButtonVisual(castSpellModeSnowballButton, selectedSpell == SpellId.Snowball);
            ApplyCastSpellModeButtonVisual(castSpellModeBurningHandsButton, selectedSpell == SpellId.BurningHands);
            ApplyCastSpellModeButtonVisual(castSpellModeFearButton, selectedSpell == SpellId.Fear);
            ApplyCastSpellModeButtonVisual(castSpellModeHealButton, selectedSpell == SpellId.Heal);
            ApplyCastSpellModeButtonVisual(castSpellModeHarmButton, selectedSpell == SpellId.Harm);
        }

        private void ApplyCastSpellModeButtonVisual(Button button, bool selected)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? castSpellModeSelectedColor : castSpellModeUnselectedColor;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.color = selected ? CombatUiPalette.HudButtonSelectedTextColor : castSpellModeTextColor;
        }

        private void RefreshCastSpellButtonLabel()
        {
            string token = GetActiveSpellToken();

            if (useLauncherLayout)
            {
                SetButtonLabelText(castSpellButton, "Cast v");
                return;
            }

            if (castSpellButtonLabel == null)
                return;

            string verb = targetingController != null && targetingController.IsSpellTargetingActive
                ? "Confirm"
                : "Cast";
            castSpellButtonLabel.text = $"{verb} [{token}]";
        }

        private void RefreshCastSpellModeButtonLabels()
        {
            if (useLauncherLayout)
            {
                SetButtonLabelText(castSpellModeStandardButton, "Force Barrage [1-3]");
                SetButtonLabelText(castSpellModeGlassButton, "Electric Arc [2]");
                SetButtonLabelText(castSpellModeSnowballButton, "Snowball [2]");
                SetButtonLabelText(castSpellModeBurningHandsButton, "Burning Hands [2]");
                SetButtonLabelText(castSpellModeFearButton, "Fear [2]");
                SetButtonLabelText(castSpellModeHealButton, "Heal [1-3]");
                SetButtonLabelText(castSpellModeHarmButton, "Harm [1-3]");
                return;
            }

            int forceBarrageActionCount = actionBarCommandCoordinator.CurrentForceBarrageActionCount;
            int healActionCount = actionBarCommandCoordinator.CurrentHealActionCount;
            int harmActionCount = actionBarCommandCoordinator.CurrentHarmActionCount;
            SetButtonLabelText(castSpellModeStandardButton, $"Force Barrage [{Mathf.Clamp(forceBarrageActionCount, 1, 3)}]");
            SetButtonLabelText(castSpellModeGlassButton, "Electric Arc [2]");
            SetButtonLabelText(castSpellModeSnowballButton, "Snowball [2]");
            SetButtonLabelText(castSpellModeBurningHandsButton, "Burning Hands [2]");
            SetButtonLabelText(castSpellModeFearButton, "Fear [2]");
            SetButtonLabelText(castSpellModeHealButton, $"Heal [{Mathf.Clamp(healActionCount, 1, 3)}]");
            SetButtonLabelText(castSpellModeHarmButton, $"Harm [{Mathf.Clamp(harmActionCount, 1, 3)}]");
        }

        private string GetActiveSpellToken()
        {
            if (targetingController != null)
            {
                switch (targetingController.ActiveSpellId)
                {
                    case SpellId.ForceBarrage:
                        return SpellCatalog.GetShortToken(
                            SpellId.ForceBarrage,
                            Mathf.Max(1, targetingController.ForceBarrageShardCapacity));

                    case SpellId.ElectricArc:
                        return SpellCatalog.GetShortToken(SpellId.ElectricArc);

                    case SpellId.Snowball:
                        return SpellCatalog.GetShortToken(SpellId.Snowball);

                    case SpellId.BurningHands:
                        return SpellCatalog.GetShortToken(SpellId.BurningHands);

                    case SpellId.Fear:
                        return SpellCatalog.GetShortToken(SpellId.Fear);

                    case SpellId.Heal:
                        return SpellCatalog.GetShortToken(SpellId.Heal, GetActiveHealActionCount());

                    case SpellId.Harm:
                        return SpellCatalog.GetShortToken(SpellId.Harm, GetActiveHarmActionCount());
                }
            }

            return actionBarCommandCoordinator.GetSelectedSpellToken();
        }

        private int GetActiveHealActionCount()
        {
            if (targetingController != null && targetingController.ActiveSpellId == SpellId.Heal)
            {
                if (targetingController.ActiveMode == TargetingMode.HealSingle)
                    return Mathf.Clamp(targetingController.HealActionCount, 1, 3);

                if (targetingController.ActiveMode == TargetingMode.SpellAoE)
                    return Mathf.Clamp(targetingController.SpellAoEActionCount, 1, 3);
            }

            return actionBarCommandCoordinator != null
                ? Mathf.Clamp(actionBarCommandCoordinator.CurrentHealActionCount, 1, 3)
                : 1;
        }

        private int GetActiveHarmActionCount()
        {
            if (targetingController != null && targetingController.ActiveSpellId == SpellId.Harm)
            {
                if (targetingController.ActiveMode == TargetingMode.HarmSingle)
                    return Mathf.Clamp(targetingController.HarmActionCount, 1, 3);

                if (targetingController.ActiveMode == TargetingMode.SpellAoE)
                    return Mathf.Clamp(targetingController.SpellAoEActionCount, 1, 3);
            }

            return actionBarCommandCoordinator != null
                ? Mathf.Clamp(actionBarCommandCoordinator.CurrentHarmActionCount, 1, 3)
                : 1;
        }

    }
}
