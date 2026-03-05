using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
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
        [SerializeField] private Button repositionButton;
        [SerializeField] private Button demoralizeButton;
        [SerializeField] private Button escapeButton;
        [SerializeField] private Button aidButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonLabel;
        [SerializeField] private RectTransform readyModeSelectorRoot;
        [SerializeField] private Button readyModeMoveButton;
        [SerializeField] private Button readyModeAttackButton;
        [SerializeField] private Button readyModeAnyButton;
        [SerializeField] private Button castSpellButton;
        [SerializeField] private TMP_Text castSpellButtonLabel;
        [SerializeField] private RectTransform castSpellModeSelectorRoot;
        [SerializeField] private Button castSpellModeStandardButton;
        [SerializeField] private Button castSpellModeGlassButton;
        [SerializeField] private Button raiseShieldButton;
        [SerializeField] private Button standButton;
        [SerializeField] private Button delayButton;
        [SerializeField] private Button returnNowButton;
        [SerializeField] private Button skipDelayWindowButton;

        [Header("Launcher Layout (Step 5, optional)")]
        [SerializeField] private bool useLauncherLayout;
        [SerializeField] private Button tacticsLauncherButton;
        [SerializeField] private RectTransform strikePopupRoot;
        [SerializeField] private RectTransform tacticsPopupRoot;
        [SerializeField] private Button strikePopupStrikeButton;

        [Header("Highlights (optional overlays)")]
        [SerializeField] private Image strikeHighlight;
        [SerializeField] private Image tripHighlight;
        [SerializeField] private Image shoveHighlight;
        [SerializeField] private Image grappleHighlight;
        [SerializeField] private Image repositionHighlight;
        [SerializeField] private Image demoralizeHighlight;
        [SerializeField] private Image escapeHighlight;
        [SerializeField] private Image aidHighlight;
        [SerializeField] private Image readyHighlight;
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
        [SerializeField] private Color readyModeSelectedColor = new Color(0.95f, 0.78f, 0.18f, 0.95f);
        [SerializeField] private Color readyModeUnselectedColor = new Color(0.18f, 0.23f, 0.30f, 0.92f);
        [SerializeField] private Color readyModeTextColor = new Color(0.92f, 0.92f, 0.95f, 1f);
        [SerializeField] private Color castSpellModeSelectedColor = new Color(0.95f, 0.78f, 0.18f, 0.95f);
        [SerializeField] private Color castSpellModeUnselectedColor = new Color(0.18f, 0.23f, 0.30f, 0.92f);
        [SerializeField] private Color castSpellModeTextColor = new Color(0.92f, 0.92f, 0.95f, 1f);

        private bool buttonListenersBound;
        private bool delayEventsSubscribedInternally;
        private bool turnManagementButtonsExternallyHidden;
        private bool strikePopupOpen;
        private bool tacticsPopupOpen;
        private bool castPopupOpen;
        private readonly ActionBarAvailabilityPolicy actionBarAvailabilityPolicy = new();
        private readonly AidPreparedIndicatorPresenter aidPreparedIndicatorPresenter = new();
        private readonly DelayActionBarStatePresenter delayActionBarStatePresenter = new();
        private readonly ActionBarCommandCoordinator actionBarCommandCoordinator = new();
        private bool aidUiWiringWarned;
        private bool readyUiWiringWarned;
        private bool readyModeWiringWarned;
        private bool launcherLayoutWiringWarned;
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
            if (repositionButton == null) Debug.LogWarning("[ActionBar] repositionButton not assigned", this);
            if (demoralizeButton == null) Debug.LogWarning("[ActionBar] demoralizeButton not assigned", this);
            if (escapeButton == null) Debug.LogWarning("[ActionBar] escapeButton not assigned", this);
            if (aidButton == null) Debug.LogWarning("[ActionBar] aidButton not assigned", this);
            if (aidHighlight == null) Debug.LogWarning("[ActionBar] aidHighlight not assigned", this);
            if (aidPreparedIndicatorRoot == null) Debug.LogWarning("[ActionBar] aidPreparedIndicatorRoot not assigned", this);
            if (aidPreparedIndicatorLabel == null) Debug.LogWarning("[ActionBar] aidPreparedIndicatorLabel not assigned", this);
            if (readyButton == null) Debug.LogWarning("[ActionBar] readyButton not assigned", this);
            if (readyButtonLabel == null) Debug.LogWarning("[ActionBar] readyButtonLabel not assigned", this);
            if (readyHighlight == null) Debug.LogWarning("[ActionBar] readyHighlight not assigned", this);
            if (readyModeSelectorRoot == null) Debug.LogWarning("[ActionBar] readyModeSelectorRoot not assigned", this);
            if (readyModeMoveButton == null) Debug.LogWarning("[ActionBar] readyModeMoveButton not assigned", this);
            if (readyModeAttackButton == null) Debug.LogWarning("[ActionBar] readyModeAttackButton not assigned", this);
            if (readyModeAnyButton == null) Debug.LogWarning("[ActionBar] readyModeAnyButton not assigned", this);
            if (castSpellButton == null) Debug.LogWarning("[ActionBar] castSpellButton not assigned", this);
            if (castSpellButtonLabel == null) Debug.LogWarning("[ActionBar] castSpellButtonLabel not assigned", this);
            if (raiseShieldButton == null) Debug.LogWarning("[ActionBar] raiseShieldButton not assigned", this);
            if (standButton == null) Debug.LogWarning("[ActionBar] standButton not assigned", this);
            // delay/return/skip buttons are optional in older scenes; no warning spam.
            if (useLauncherLayout && tacticsLauncherButton == null) Debug.LogWarning("[ActionBar] useLauncherLayout=true but tacticsLauncherButton is not assigned (runtime fallback will be used).", this);
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
            ApplyDelayControls(delayActionBarStatePresenter.BuildInactiveState());
            SetReadyModeButtonsInteractable(false);
            SetCastSpellModeButtonsInteractable(false);
            RefreshReadyModeButtonsVisual();
            RefreshCastSpellModeButtonsVisual();
            RefreshReadyButtonLabel();
            RefreshCastSpellButtonLabel();
            ClearAllHighlights();
            SetStrikePopupVisible(false);
            SetTacticsPopupVisible(false);
            SetCastPopupVisible(false);
        }

        private void ValidateAndApplyUiWiring()
        {
            ValidateAidUiReferences();
            ApplyAidPreparedIndicatorStyle();
            ValidateReadyUiReferences();
            ResolveReadyModeSelectorReferences();
            ResolveCastSpellUiReferences();
            EnsureCastSpellUiFallback();
            EnsureLauncherLayoutFallback();

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

        private void ApplyAidPreparedIndicatorStyle()
        {
            if (aidPreparedIndicatorRoot != null)
            {
                var indicatorImage = aidPreparedIndicatorRoot.GetComponent<Image>();
                if (indicatorImage != null)
                    indicatorImage.color = aidPreparedIndicatorFillColor;
            }

            if (aidPreparedIndicatorLabel != null)
                aidPreparedIndicatorLabel.color = aidPreparedIndicatorLabelColor;
        }

        private void ValidateReadyUiReferences()
        {
            if (readyButton != null && readyButtonLabel != null && readyHighlight != null)
                return;

            if (readyUiWiringWarned)
                return;

            readyUiWiringWarned = true;
            Debug.LogWarning(
                "[ActionBar] Ready UI is not fully wired (readyButton/readyButtonLabel/readyHighlight). " +
                "Assign references in scene or run scene validator autofix.",
                this);
        }

        private void ResolveReadyModeSelectorReferences()
        {
            if (readyButton == null)
            {
                WarnMissingReadyModeWiring();
                return;
            }

            if (readyModeSelectorRoot == null
                || readyModeMoveButton == null
                || readyModeAttackButton == null
                || readyModeAnyButton == null)
                WarnMissingReadyModeWiring();
        }

        private void WarnMissingReadyModeWiring()
        {
            if (readyModeWiringWarned)
                return;

            readyModeWiringWarned = true;
            Debug.LogWarning(
                "[ActionBar] Ready mode selector is not fully wired (root/mode buttons missing). " +
                "Run scene validator autofix or assign references in scene.",
                this);
        }

        private void ResolveCastSpellUiReferences()
        {
            if (castSpellButton != null && castSpellButtonLabel == null)
                castSpellButtonLabel = castSpellButton.GetComponentInChildren<TMP_Text>(true);
        }

        private void EnsureCastSpellUiFallback()
        {
            if (castSpellButton == null && raiseShieldButton != null)
            {
                var clonedObject = Instantiate(raiseShieldButton.gameObject, raiseShieldButton.transform.parent);
                clonedObject.name = "CastSpellButton_Auto";

                var clonedRect = clonedObject.transform as RectTransform;
                var raiseShieldRect = raiseShieldButton.transform as RectTransform;
                if (clonedRect != null && raiseShieldRect != null)
                {
                    clonedRect.SetSiblingIndex(Mathf.Max(0, raiseShieldRect.GetSiblingIndex()));
                }

                castSpellButton = clonedObject.GetComponent<Button>();
                castSpellButtonLabel = castSpellButton != null
                    ? castSpellButton.GetComponentInChildren<TMP_Text>(true)
                    : null;
                if (castSpellButtonLabel != null)
                    castSpellButtonLabel.text = "Cast [STD]";

                if (castSpellHighlight == null)
                    castSpellHighlight = ResolveFirstChildImage(clonedObject.transform);
            }

            if (castSpellModeSelectorRoot == null && castSpellButton != null)
            {
                castSpellModeSelectorRoot = BuildCastSpellModeSelector(castSpellButton.transform);
            }

            if ((castSpellModeStandardButton == null || castSpellModeGlassButton == null) && castSpellModeSelectorRoot != null)
            {
                ResolveCastSpellModeButtonsFromRoot(castSpellModeSelectorRoot);
            }
        }

        private RectTransform BuildCastSpellModeSelector(Transform parent)
        {
            var rootObject = new GameObject(
                "CastSpellModeSelector_Auto",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup));
            rootObject.transform.SetParent(parent, false);

            var rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 3f);
            rootRect.sizeDelta = new Vector2(64f, 16f);

            var layout = rootObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            castSpellModeStandardButton = BuildCastSpellModeButton("Standard_Auto", "S", rootObject.transform);
            castSpellModeGlassButton = BuildCastSpellModeButton("Glass_Auto", "G", rootObject.transform);
            return rootRect;
        }

        private static Button BuildCastSpellModeButton(string name, string text, Transform parent)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(30f, 16f);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 30f;
            layout.preferredHeight = 16f;
            layout.minWidth = 28f;
            layout.minHeight = 16f;

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return buttonObject.GetComponent<Button>();
        }

        private void ResolveCastSpellModeButtonsFromRoot(RectTransform root)
        {
            if (root == null)
                return;

            var buttons = root.GetComponentsInChildren<Button>(true);
            if (buttons == null || buttons.Length == 0)
                return;

            if (castSpellModeStandardButton == null)
                castSpellModeStandardButton = buttons[0];

            if (castSpellModeGlassButton == null && buttons.Length > 1)
                castSpellModeGlassButton = buttons[1];
        }

        private static Image ResolveFirstChildImage(Transform root)
        {
            if (root == null)
                return null;

            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].transform != root)
                    return images[i];
            }

            return null;
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

            var rowParent = strikeButton.transform.parent;
            if (rowParent == null)
            {
                useLauncherLayout = false;
                return;
            }

            if (tacticsLauncherButton == null)
                tacticsLauncherButton = BuildLauncherButtonFromTemplate("TacticsLauncherButton_Auto", demoralizeButton, rowParent, "Tactics ▾");

            if (strikePopupRoot == null)
                strikePopupRoot = BuildPopupRoot("StrikePopup_Auto", rowParent);
            if (tacticsPopupRoot == null)
                tacticsPopupRoot = BuildPopupRoot("TacticsPopup_Auto", rowParent);

            if (strikePopupRoot != null)
            {
                strikePopupRoot.SetParent(strikeButton.transform, false);
                strikePopupRoot.anchorMin = new Vector2(0.5f, 1f);
                strikePopupRoot.anchorMax = new Vector2(0.5f, 1f);
                strikePopupRoot.pivot = new Vector2(0.5f, 0f);
                strikePopupRoot.anchoredPosition = new Vector2(0f, 10f);
            }

            if (tacticsPopupRoot != null && tacticsLauncherButton != null)
            {
                tacticsPopupRoot.SetParent(tacticsLauncherButton.transform, false);
                tacticsPopupRoot.anchorMin = new Vector2(0.5f, 1f);
                tacticsPopupRoot.anchorMax = new Vector2(0.5f, 1f);
                tacticsPopupRoot.pivot = new Vector2(0.5f, 0f);
                tacticsPopupRoot.anchoredPosition = new Vector2(0f, 10f);
            }

            if (strikePopupStrikeButton == null)
            {
                strikePopupStrikeButton = BuildPopupActionButton("StrikeOptionButton_Auto", strikePopupRoot, "Strike [1]");
                CopyButtonVisualStyle(strikePopupStrikeButton, strikeButton);
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
                castSpellModeSelectorRoot.anchoredPosition = new Vector2(0f, 10f);
            }

            if (standButton != null)
                standButton.gameObject.SetActive(false);
        }

        private static Button BuildLauncherButtonFromTemplate(string name, Button template, Transform parent, string labelText)
        {
            if (template == null || parent == null)
                return null;

            var clonedObject = Instantiate(template.gameObject, parent);
            clonedObject.name = name;

            var rect = clonedObject.transform as RectTransform;
            if (rect != null && template.transform is RectTransform templateRect)
            {
                rect.anchorMin = templateRect.anchorMin;
                rect.anchorMax = templateRect.anchorMax;
                rect.pivot = templateRect.pivot;
                rect.sizeDelta = templateRect.sizeDelta;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }

            var label = clonedObject.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = labelText;

            return clonedObject.GetComponent<Button>();
        }

        private static RectTransform BuildPopupRoot(string name, Transform parent)
        {
            var popupObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter));
            popupObject.transform.SetParent(parent, false);

            var rect = popupObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 14f);
            rect.sizeDelta = new Vector2(520f, 30f);

            var image = popupObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);
            image.raycastTarget = true;

            var layout = popupObject.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 4, 4);
            layout.spacing = 4f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            var fitter = popupObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            popupObject.SetActive(false);
            return rect;
        }

        private static Button BuildPopupActionButton(string name, RectTransform parent, string labelText)
        {
            if (parent == null)
                return null;

            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120f, 24f);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 120f;
            layout.preferredHeight = 24f;
            layout.minWidth = 90f;
            layout.minHeight = 24f;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.23f, 0.30f, 0.92f);

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 12f;
            label.color = new Color(0.92f, 0.92f, 0.95f, 1f);
            label.raycastTarget = false;
            label.enableWordWrapping = false;

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return buttonObject.GetComponent<Button>();
        }

        private static void MoveButtonToPopup(Button button, RectTransform popupRoot)
        {
            if (button == null || popupRoot == null)
                return;

            button.transform.SetParent(popupRoot, false);
            if (button.TryGetComponent<LayoutElement>(out var layoutElement))
            {
                layoutElement.preferredHeight = Mathf.Max(24f, layoutElement.preferredHeight);
            }
        }

        private static void CopyButtonVisualStyle(Button destination, Button source)
        {
            if (destination == null || source == null)
                return;

            if (destination.TryGetComponent<Image>(out var destinationImage)
                && source.TryGetComponent<Image>(out var sourceImage))
            {
                destinationImage.color = sourceImage.color;
                destinationImage.sprite = sourceImage.sprite;
                destinationImage.type = sourceImage.type;
            }
        }

        private void ApplyStaticButtonLabels()
        {
            if (!useLauncherLayout)
                return;

            if (strikeButton != null)
                SetButtonLabelText(strikeButton, "Strike ▾");

            if (tacticsLauncherButton != null)
                SetButtonLabelText(tacticsLauncherButton, "Tactics ▾");

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
            if (standButton != null)
                SetButtonLabelText(standButton, "Stand [1]");

            if (castSpellButton != null)
                SetButtonLabelText(castSpellButton, "Cast ▾");
            if (castSpellModeStandardButton != null)
                SetButtonLabelText(castSpellModeStandardButton, "Shield [1]");
            if (castSpellModeGlassButton != null)
                SetButtonLabelText(castSpellModeGlassButton, "Glass Shield [1]");
            if (strikePopupStrikeButton != null)
                SetButtonLabelText(strikePopupStrikeButton, "Strike [1]");
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

            actionBarCommandCoordinator.Bind(turnManager, targetingController, actionExecutor, RefreshAvailability);
            SubscribeCoreEvents();
            SubscribeDelayEventsIfNeeded();
            SetTurnManagementButtonsVisible(!IsExternalTurnOptionsPresenterPresent());

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
                UnsubscribeDelayEvents();
            }

            if (targetingController != null)
                targetingController.OnModeChanged -= HandleModeChanged;

            CloseAllPopups();
        }

        private void Update()
        {
            if (!useLauncherLayout)
                return;
            if (!strikePopupOpen && !tacticsPopupOpen && !castPopupOpen)
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

            Vector2 screen = mouse.position.ReadValue();
            if (IsPointInsideAnyLauncherUi(screen))
                return;

            CloseAllPopups();
        }

        private bool IsPointInsideAnyLauncherUi(Vector2 screenPoint)
        {
            if (IsPointInsideRect(screenPoint, strikeButton != null ? strikeButton.transform as RectTransform : null)) return true;
            if (IsPointInsideRect(screenPoint, tacticsLauncherButton != null ? tacticsLauncherButton.transform as RectTransform : null)) return true;
            if (IsPointInsideRect(screenPoint, castSpellButton != null ? castSpellButton.transform as RectTransform : null)) return true;
            if (IsPointInsideRect(screenPoint, strikePopupRoot)) return true;
            if (IsPointInsideRect(screenPoint, tacticsPopupRoot)) return true;
            if (IsPointInsideRect(screenPoint, castSpellModeSelectorRoot)) return true;
            return false;
        }

        private static bool IsPointInsideRect(Vector2 screenPoint, RectTransform rect)
        {
            return rect != null && rect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null);
        }

        private void EnsureButtonListenersBound()
        {
            if (buttonListenersBound) return;

            int boundCount = 0;
            boundCount += BindButton(strikeButton, useLauncherLayout ? ToggleStrikePopup : actionBarCommandCoordinator.OnStrikeClicked);
            boundCount += BindButton(tripButton, useLauncherLayout ? HandleTripPopupClicked : actionBarCommandCoordinator.OnTripClicked);
            boundCount += BindButton(shoveButton, useLauncherLayout ? HandleShovePopupClicked : actionBarCommandCoordinator.OnShoveClicked);
            boundCount += BindButton(grappleButton, useLauncherLayout ? HandleGrapplePopupClicked : actionBarCommandCoordinator.OnGrappleClicked);
            boundCount += BindButton(repositionButton, useLauncherLayout ? HandleRepositionPopupClicked : actionBarCommandCoordinator.OnRepositionClicked);
            boundCount += BindButton(demoralizeButton, useLauncherLayout ? HandleDemoralizePopupClicked : actionBarCommandCoordinator.OnDemoralizeClicked);
            boundCount += BindButton(escapeButton, useLauncherLayout ? HandleEscapePopupClicked : actionBarCommandCoordinator.OnEscapeClicked);
            boundCount += BindButton(aidButton, useLauncherLayout ? HandleAidPopupClicked : actionBarCommandCoordinator.OnAidClicked);
            boundCount += BindButton(readyButton, actionBarCommandCoordinator.OnReadyClicked);
            boundCount += BindButton(readyModeMoveButton, actionBarCommandCoordinator.OnReadyModeMoveClicked);
            boundCount += BindButton(readyModeAttackButton, actionBarCommandCoordinator.OnReadyModeAttackClicked);
            boundCount += BindButton(readyModeAnyButton, actionBarCommandCoordinator.OnReadyModeAnyClicked);
            boundCount += BindButton(castSpellButton, useLauncherLayout ? ToggleCastPopup : actionBarCommandCoordinator.OnCastSpellClicked);
            boundCount += BindButton(castSpellModeStandardButton, useLauncherLayout ? HandleCastStandardPopupClicked : actionBarCommandCoordinator.OnCastSpellModeStandardClicked);
            boundCount += BindButton(castSpellModeGlassButton, useLauncherLayout ? HandleCastGlassPopupClicked : actionBarCommandCoordinator.OnCastSpellModeGlassClicked);
            boundCount += BindButton(raiseShieldButton, actionBarCommandCoordinator.OnRaiseShieldClicked);
            boundCount += BindButton(standButton, actionBarCommandCoordinator.OnStandClicked);
            boundCount += BindButton(delayButton, actionBarCommandCoordinator.OnDelayClicked);
            boundCount += BindButton(returnNowButton, actionBarCommandCoordinator.OnReturnNowClicked);
            boundCount += BindButton(skipDelayWindowButton, actionBarCommandCoordinator.OnSkipDelayWindowClicked);
            boundCount += BindButton(tacticsLauncherButton, ToggleTacticsPopup);
            boundCount += BindButton(strikePopupStrikeButton, HandleStrikePopupStrikeClicked);

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

            bool next = !strikePopupOpen;
            CloseAllPopups();
            SetStrikePopupVisible(next);
        }

        private void ToggleTacticsPopup()
        {
            if (!useLauncherLayout)
                return;

            bool next = !tacticsPopupOpen;
            CloseAllPopups();
            SetTacticsPopupVisible(next);
        }

        private void ToggleCastPopup()
        {
            if (!useLauncherLayout)
            {
                actionBarCommandCoordinator.OnCastSpellClicked();
                return;
            }

            bool next = !castPopupOpen;
            CloseAllPopups();
            SetCastPopupVisible(next);
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
            actionBarCommandCoordinator.OnCastSpellModeStandardClicked();
            actionBarCommandCoordinator.OnCastSpellClicked();
            CloseAllPopups();
        }

        private void HandleCastGlassPopupClicked()
        {
            actionBarCommandCoordinator.OnCastSpellModeGlassClicked();
            actionBarCommandCoordinator.OnCastSpellClicked();
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

            strikePopupOpen = visible;
            if (strikePopupRoot != null && strikePopupRoot.gameObject.activeSelf != visible)
                strikePopupRoot.gameObject.SetActive(visible);
        }

        private void SetTacticsPopupVisible(bool visible)
        {
            if (!useLauncherLayout)
                return;

            tacticsPopupOpen = visible;
            if (tacticsPopupRoot != null && tacticsPopupRoot.gameObject.activeSelf != visible)
                tacticsPopupRoot.gameObject.SetActive(visible);
        }

        private void SetCastPopupVisible(bool visible)
        {
            if (!useLauncherLayout)
                return;

            castPopupOpen = visible;
            if (castSpellModeSelectorRoot != null && castSpellModeSelectorRoot.gameObject.activeSelf != visible)
                castSpellModeSelectorRoot.gameObject.SetActive(visible);
        }

        private void CloseAllPopups()
        {
            if (!useLauncherLayout)
                return;

            SetStrikePopupVisible(false);
            SetTacticsPopupVisible(false);
            SetCastPopupVisible(false);
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
            eventBus.OnReadyTriggerModeChangedTyped += HandleReadyTriggerModeChanged;
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
            eventBus.OnReadyTriggerModeChangedTyped -= HandleReadyTriggerModeChanged;
        }

        private void SubscribeDelayEventsIfNeeded()
        {
            if (IsExternalDelayOrchestratorPresent())
            {
                delayEventsSubscribedInternally = false;
                return;
            }

            eventBus.OnDelayTurnBeginTriggerChangedTyped += HandleDelayTurnBeginTriggerChanged;
            eventBus.OnDelayPlacementSelectionChangedTyped += HandleDelayPlacementSelectionChanged;
            eventBus.OnDelayReturnWindowOpenedTyped += HandleDelayReturnWindowOpened;
            eventBus.OnDelayReturnWindowClosedTyped += HandleDelayReturnWindowClosed;
            eventBus.OnDelayedTurnEnteredTyped += HandleDelayedTurnEntered;
            eventBus.OnDelayedTurnResumedTyped += HandleDelayedTurnResumed;
            eventBus.OnDelayedTurnExpiredTyped += HandleDelayedTurnExpired;
            delayEventsSubscribedInternally = true;
        }

        private void UnsubscribeDelayEvents()
        {
            if (!delayEventsSubscribedInternally)
                return;

            eventBus.OnDelayTurnBeginTriggerChangedTyped -= HandleDelayTurnBeginTriggerChanged;
            eventBus.OnDelayPlacementSelectionChangedTyped -= HandleDelayPlacementSelectionChanged;
            eventBus.OnDelayReturnWindowOpenedTyped -= HandleDelayReturnWindowOpened;
            eventBus.OnDelayReturnWindowClosedTyped -= HandleDelayReturnWindowClosed;
            eventBus.OnDelayedTurnEnteredTyped -= HandleDelayedTurnEntered;
            eventBus.OnDelayedTurnResumedTyped -= HandleDelayedTurnResumed;
            eventBus.OnDelayedTurnExpiredTyped -= HandleDelayedTurnExpired;
            delayEventsSubscribedInternally = false;
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
            SetCastSpellUiVisible(false);
            SetAllInteractable(false);
            ApplyDelayControls(delayActionBarStatePresenter.BuildInactiveState());
            SetReadyModeButtonsInteractable(false);
            SetCastSpellModeButtonsInteractable(false);
            RefreshReadyModeButtonsVisual();
            RefreshCastSpellModeButtonsVisual();
            RefreshReadyButtonLabel();
            RefreshCastSpellButtonLabel();
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

            SetAllInteractable(false);
            ApplyDelayControls(delayActionBarStatePresenter.BuildInactiveState());
            SetReadyModeButtonsInteractable(false);
            SetCastSpellModeButtonsInteractable(false);
            RefreshReadyModeButtonsVisual();
            RefreshCastSpellModeButtonsVisual();
            RefreshCastSpellButtonLabel();
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

        private void HandleReadyTriggerModeChanged(in ReadyTriggerModeChangedEvent e)
        {
            _ = e;
            RefreshAvailability();
        }

        private void HandleDelayTurnBeginTriggerChanged(in DelayTurnBeginTriggerChangedEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        private void HandleDelayPlacementSelectionChanged(in DelayPlacementSelectionChangedEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        private void HandleDelayReturnWindowOpened(in DelayReturnWindowOpenedEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        private void HandleDelayReturnWindowClosed(in DelayReturnWindowClosedEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        private void HandleDelayedTurnEntered(in DelayedTurnEnteredEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        private void HandleDelayedTurnResumed(in DelayedTurnResumedEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        private void HandleDelayedTurnExpired(in DelayedTurnExpiredEvent e)
        {
            RefreshDelayUiFromOrchestrator();
        }

        public void RefreshDelayUiFromOrchestrator()
        {
            RefreshAvailability();
        }

        private void HandleModeChanged(TargetingMode mode)
        {
            SetHighlight(strikeHighlight, mode == TargetingMode.Strike);
            SetHighlight(tripHighlight, mode == TargetingMode.Trip);
            SetHighlight(shoveHighlight, mode == TargetingMode.Shove);
            SetHighlight(grappleHighlight, mode == TargetingMode.Grapple);
            SetHighlight(repositionHighlight, mode == TargetingMode.Reposition);
            SetHighlight(demoralizeHighlight, mode == TargetingMode.Demoralize);
            SetHighlight(escapeHighlight, mode == TargetingMode.Escape);
            SetHighlight(aidHighlight, mode == TargetingMode.Aid);
            SetHighlight(readyHighlight, !turnManagementButtonsExternallyHidden && mode == TargetingMode.ReadyStrike);
            SetHighlight(castSpellHighlight, false);
            SetHighlight(raiseShieldHighlight, false);
            SetHighlight(standHighlight, false);

            RefreshAvailability();
        }

        private void RefreshAvailability()
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null || actionExecutor == null)
            {
                CloseAllPopups();
                SetCastSpellUiVisible(false);
                SetAllInteractable(false);
                ApplyDelayControls(delayActionBarStatePresenter.BuildInactiveState());
                aidPreparedIndicatorPresenter.Clear();
                SetReadyModeButtonsInteractable(false);
                SetCastSpellModeButtonsInteractable(false);
                RefreshReadyModeButtonsVisual();
                RefreshCastSpellModeButtonsVisual();
                RefreshCastSpellButtonLabel();
                RefreshAidPreparedIndicator();
                return;
            }

            var actor = turnManager.CurrentEntity;
            var actorData = entityManager.Registry.Get(actor);
            SetCastSpellUiVisible(ShouldShowCastSpellUi(actorData));

            if (turnManager.IsDelayReturnWindowOpen)
            {
                CloseAllPopups();
                SetAllInteractable(false);

                bool canReturnNow = turnManager.TryGetFirstDelayedPlayerActor(out _);
                ApplyDelayControls(delayActionBarStatePresenter.BuildReturnWindowState(canReturnNow));
                SetReadyModeButtonsInteractable(false);
                SetCastSpellModeButtonsInteractable(false);
                RefreshReadyModeButtonsVisual();
                RefreshCastSpellModeButtonsVisual();
                RefreshReadyButtonLabel();
                RefreshCastSpellButtonLabel();
                RefreshAidPreparedIndicator();
                return;
            }

            if (turnManager.IsDelayPlacementSelectionOpen)
            {
                CloseAllPopups();
                SetAllInteractable(false);
                ApplyDelayControls(delayActionBarStatePresenter.BuildPlacementSelectionState());
                SetReadyModeButtonsInteractable(false);
                SetCastSpellModeButtonsInteractable(false);
                RefreshReadyModeButtonsVisual();
                RefreshCastSpellModeButtonsVisual();
                RefreshReadyButtonLabel();
                RefreshCastSpellButtonLabel();
                RefreshAidPreparedIndicator();
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
                ApplyDelayControls(delayActionBarStatePresenter.BuildInactiveState());
                SetReadyModeButtonsInteractable(false);
                SetCastSpellModeButtonsInteractable(false);
                RefreshReadyModeButtonsVisual();
                RefreshCastSpellModeButtonsVisual();
                RefreshReadyButtonLabel();
                RefreshCastSpellButtonLabel();
                RefreshAidPreparedIndicator();
                return;
            }

            ApplyActionAvailability(in availability);
            ApplyDelayControls(delayActionBarStatePresenter.BuildNormalState(turnManager.CanDelayCurrentTurn()));

            bool canAdjustReadyMode =
                actor.IsValid &&
                turnManager.IsPlayerTurn &&
                !actionExecutor.IsBusy &&
                !turnManager.IsDelayPlacementSelectionOpen &&
                !turnManager.IsDelayReturnWindowOpen &&
                !turnManager.HasReadiedStrike(actor);
            SetReadyModeButtonsInteractable(canAdjustReadyMode);
            RefreshReadyModeButtonsVisual();

            bool canAdjustCastSpellMode =
                actorData != null &&
                actorData.CanCastStandardShield &&
                actorData.CanCastGlassShield &&
                !actionExecutor.IsBusy &&
                turnManager.IsPlayerTurn &&
                !turnManager.IsDelayPlacementSelectionOpen &&
                !turnManager.IsDelayReturnWindowOpen;
            SetCastSpellModeButtonsInteractable(canAdjustCastSpellMode);
            RefreshCastSpellModeButtonsVisual();

            RefreshReadyButtonLabel();
            RefreshCastSpellButtonLabel();
            RefreshAidPreparedIndicator();
            ApplyStaticButtonLabels();
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
            if (useLauncherLayout)
            {
                SetInteractable(strikeButton, enabled);
                SetInteractable(tacticsLauncherButton, enabled);
                SetInteractable(castSpellButton, enabled);
                SetInteractable(raiseShieldButton, enabled);
                SetInteractable(castSpellModeStandardButton, enabled);
                SetInteractable(castSpellModeGlassButton, enabled);
                SetInteractable(tripButton, enabled);
                SetInteractable(shoveButton, enabled);
                SetInteractable(grappleButton, enabled);
                SetInteractable(repositionButton, enabled);
                SetInteractable(demoralizeButton, enabled);
                SetInteractable(escapeButton, enabled);
                SetInteractable(aidButton, enabled);
                SetInteractable(strikePopupStrikeButton, enabled);
                SetInteractable(standButton, enabled);
            }
            else
            {
                SetInteractable(strikeButton, enabled);
                SetInteractable(tripButton, enabled);
                SetInteractable(shoveButton, enabled);
                SetInteractable(grappleButton, enabled);
                SetInteractable(repositionButton, enabled);
                SetInteractable(demoralizeButton, enabled);
                SetInteractable(escapeButton, enabled);
                SetInteractable(aidButton, enabled);
                SetInteractable(readyButton, !turnManagementButtonsExternallyHidden && enabled);
                SetInteractable(readyModeMoveButton, enabled);
                SetInteractable(readyModeAttackButton, enabled);
                SetInteractable(readyModeAnyButton, enabled);
                SetInteractable(castSpellButton, enabled);
                SetInteractable(castSpellModeStandardButton, enabled);
                SetInteractable(castSpellModeGlassButton, enabled);
                SetInteractable(raiseShieldButton, enabled);
                SetInteractable(standButton, enabled);
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
                SetInteractable(castSpellModeStandardButton, availability.castSpellInteractable);
                SetInteractable(castSpellModeGlassButton, availability.castSpellInteractable);

                bool guardInteractable = availability.raiseShieldInteractable || availability.castSpellInteractable;
                SetInteractable(raiseShieldButton, guardInteractable);
                SetButtonVisible(raiseShieldButton, guardInteractable);

                SetButtonVisible(standButton, availability.standInteractable);
                SetInteractable(standButton, availability.standInteractable);
            }
            else
            {
                SetInteractable(strikeButton, availability.strikeInteractable);
                SetInteractable(tripButton, availability.tripInteractable);
                SetInteractable(shoveButton, availability.shoveInteractable);
                SetInteractable(grappleButton, availability.grappleInteractable);
                SetInteractable(repositionButton, availability.repositionInteractable);
                SetInteractable(demoralizeButton, availability.demoralizeInteractable);
                SetInteractable(escapeButton, availability.escapeInteractable);
                SetInteractable(aidButton, availability.aidInteractable);
                SetInteractable(readyButton, !turnManagementButtonsExternallyHidden && availability.readyInteractable);
                SetInteractable(castSpellButton, availability.castSpellInteractable);
                SetInteractable(raiseShieldButton, availability.raiseShieldInteractable);
                SetInteractable(standButton, availability.standInteractable);
            }
        }

        private void ApplyDelayControls(in DelayActionBarState state)
        {
            if (turnManagementButtonsExternallyHidden)
            {
                SetButtonVisible(delayButton, false);
                SetButtonVisible(returnNowButton, false);
                SetButtonVisible(skipDelayWindowButton, false);
                return;
            }

            delayActionBarStatePresenter.Apply(in state, delayButton, returnNowButton, skipDelayWindowButton);
        }

        public void SetTurnManagementButtonsVisible(bool visible)
        {
            turnManagementButtonsExternallyHidden = !visible;
            SetButtonVisible(readyButton, visible);
            SetButtonVisible(delayButton, visible);
            if (!visible)
            {
                SetButtonVisible(returnNowButton, false);
                SetButtonVisible(skipDelayWindowButton, false);
                SetReadyModeButtonsInteractable(false);
                SetHighlight(readyHighlight, false);
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

            if (button.gameObject.activeSelf != visible)
                button.gameObject.SetActive(visible);
        }

        private void ClearAllHighlights()
        {
            SetHighlight(strikeHighlight, false);
            SetHighlight(tripHighlight, false);
            SetHighlight(shoveHighlight, false);
            SetHighlight(grappleHighlight, false);
            SetHighlight(repositionHighlight, false);
            SetHighlight(demoralizeHighlight, false);
            SetHighlight(escapeHighlight, false);
            SetHighlight(aidHighlight, false);
            SetHighlight(readyHighlight, false);
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

        private void RefreshReadyButtonLabel()
        {
            if (readyButtonLabel == null)
                return;

            var mode = turnManager != null ? turnManager.CurrentReadyTriggerMode : ReadyTriggerMode.Any;
            readyButtonLabel.text = $"Ready [{mode.ToShortToken()}]";
        }

        private void SetReadyModeButtonsInteractable(bool enabled)
        {
            if (readyModeSelectorRoot != null)
            {
                bool visible = !useLauncherLayout
                    && readyButton != null
                    && readyButton.gameObject.activeInHierarchy;
                readyModeSelectorRoot.gameObject.SetActive(visible);
            }

            SetInteractable(readyModeMoveButton, enabled);
            SetInteractable(readyModeAttackButton, enabled);
            SetInteractable(readyModeAnyButton, enabled);
        }

        private void SetCastSpellModeButtonsInteractable(bool enabled)
        {
            if (castSpellModeSelectorRoot != null)
            {
                bool shouldBeVisible = castSpellButton != null && castSpellButton.gameObject.activeInHierarchy;
                if (useLauncherLayout)
                    shouldBeVisible = shouldBeVisible && castPopupOpen;
                castSpellModeSelectorRoot.gameObject.SetActive(shouldBeVisible);
            }

            SetInteractable(castSpellModeStandardButton, enabled);
            SetInteractable(castSpellModeGlassButton, enabled);
        }

        private void SetCastSpellUiVisible(bool visible)
        {
            if (castSpellButton != null && castSpellButton.gameObject.activeSelf != visible)
                castSpellButton.gameObject.SetActive(visible);

            if (castSpellModeSelectorRoot != null)
            {
                bool modeRootVisible = visible && (!useLauncherLayout || castPopupOpen);
                if (castSpellModeSelectorRoot.gameObject.activeSelf != modeRootVisible)
                    castSpellModeSelectorRoot.gameObject.SetActive(modeRootVisible);
            }
        }

        private static bool ShouldShowCastSpellUi(EntityData actorData)
        {
            return actorData != null
                && actorData.IsAlive
                && (actorData.KnowsStandardShieldCantrip || actorData.KnowsGlassShieldCantrip);
        }

        private void RefreshReadyModeButtonsVisual()
        {
            var mode = turnManager != null ? turnManager.CurrentReadyTriggerMode : ReadyTriggerMode.Any;
            ApplyReadyModeButtonVisual(readyModeMoveButton, mode == ReadyTriggerMode.Movement);
            ApplyReadyModeButtonVisual(readyModeAttackButton, mode == ReadyTriggerMode.Attack);
            ApplyReadyModeButtonVisual(readyModeAnyButton, mode == ReadyTriggerMode.Any);
        }

        private void RefreshCastSpellModeButtonsVisual()
        {
            var mode = actionBarCommandCoordinator.CurrentCastShieldSpellMode;
            ApplyCastSpellModeButtonVisual(castSpellModeStandardButton, mode == RaiseShieldSpellMode.Standard);
            ApplyCastSpellModeButtonVisual(castSpellModeGlassButton, mode == RaiseShieldSpellMode.Glass);
        }

        private void ApplyReadyModeButtonVisual(Button button, bool selected)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? readyModeSelectedColor : readyModeUnselectedColor;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.color = selected ? Color.black : readyModeTextColor;
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
                label.color = selected ? Color.black : castSpellModeTextColor;
        }

        private void RefreshCastSpellButtonLabel()
        {
            if (useLauncherLayout)
            {
                SetButtonLabelText(castSpellButton, "Cast ▾");
                return;
            }

            if (castSpellButtonLabel == null)
                return;

            string token = actionBarCommandCoordinator.CurrentCastShieldSpellMode.ToShortToken();
            castSpellButtonLabel.text = $"Cast [{token}]";
        }

        private static bool IsExternalDelayOrchestratorPresent()
        {
            var orchestrator = UnityEngine.Object.FindFirstObjectByType<DelayUiOrchestrator>();
            return orchestrator != null && orchestrator.isActiveAndEnabled;
        }

        private static bool IsExternalTurnOptionsPresenterPresent()
        {
            var presenter = UnityEngine.Object.FindFirstObjectByType<TurnOptionsPresenter>();
            return presenter != null && presenter.isActiveAndEnabled;
        }
    }
}
