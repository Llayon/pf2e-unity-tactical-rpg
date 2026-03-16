using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PF2e.Core;

namespace PF2e.Presentation
{
    [RequireComponent(typeof(LayoutElement))]
    public class InitiativeSlot : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image hpBarFill;
        [SerializeField] private Image background;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private RectTransform portraitMaskRect;
        [SerializeField] private Image portraitImage;
        [SerializeField] private AspectRatioFitter portraitAspectFitter;
        [SerializeField] private Image damageOverlay;
        [SerializeField] private Image frameImage;
        [SerializeField] private GameObject activeHighlight;
        [SerializeField] private GameObject duplicateBadgeRoot;
        [SerializeField] private Image duplicateBadgeBackground;
        [SerializeField] private TMP_Text duplicateBadgeText;
        [SerializeField] private GameObject delayedBadgeRoot;
        [SerializeField] private Image delayedBadgeBackground;
        [SerializeField] private TMP_Text delayedBadgeText;

        [Header("Colors")]
        [SerializeField] private Color playerColor  = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField] private Color enemyColor   = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color neutralColor = new Color(0.85f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color defeatedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color activeFrameColor = new Color(1f, 0.96f, 0.78f, 1f);
        [SerializeField] private float activeScaleFactor = 1.3f;
        [SerializeField] private Color actedFrameTint = new Color(0.56f, 0.6f, 0.66f, 1f);
        [SerializeField] private Color actedPortraitTint = new Color(0.76f, 0.78f, 0.8f, 1f);
        [SerializeField] private float actedAlphaMultiplier = 0.68f;
        [SerializeField] private Color damageOverlayColor = new Color(0.7f, 0.05f, 0.05f, 0.18f);
        [SerializeField] private Color defeatedPortraitTint = new Color(0.42f, 0.42f, 0.42f, 1f);
        [SerializeField] private Color defeatedFrameTint = new Color(0.48f, 0.48f, 0.48f, 1f);
        [SerializeField] private Color delayedBadgeBackgroundColor = new Color(0.97f, 0.82f, 0.28f, 0.98f);
        [SerializeField] private Color delayedBadgeTextColor = new Color(0.14f, 0.09f, 0.03f, 1f);
        [SerializeField] private Color duplicateBadgeBackgroundColor = new Color(0.2f, 0.26f, 0.33f, 0.96f);
        [SerializeField] private Color duplicateBadgeTextColor = new Color(0.95f, 0.97f, 0.99f, 1f);
        [SerializeField] private Color hpStripHighColor = new Color(0.38f, 0.86f, 0.43f, 1f);
        [SerializeField] private Color hpStripMidColor = new Color(0.95f, 0.8f, 0.28f, 1f);
        [SerializeField] private Color hpStripLowColor = new Color(0.93f, 0.31f, 0.26f, 1f);

        [Header("Portrait Insets")]
        [SerializeField] private Vector2 playerPortraitMaskOffsetMin = new Vector2(4f, 5f);
        [SerializeField] private Vector2 playerPortraitMaskOffsetMax = new Vector2(-4f, -5f);
        [SerializeField] private Vector2 enemyPortraitMaskOffsetMin = new Vector2(4f, 5f);
        [SerializeField] private Vector2 enemyPortraitMaskOffsetMax = new Vector2(-4f, -5f);
        [SerializeField] private Vector2 neutralPortraitMaskOffsetMin = new Vector2(4f, 5f);
        [SerializeField] private Vector2 neutralPortraitMaskOffsetMax = new Vector2(-4f, -5f);

        [Header("Layout Stability")]
        [SerializeField] private bool enforceFixedLayoutSize = true;
        [SerializeField] private float fixedPreferredWidth = 64f;
        [SerializeField] private float fixedPreferredHeight = 86f;
        [SerializeField] private Vector2 visualRootBaseAnchoredPosition = new Vector2(0f, -1f);

        [Header("Delayed State")]
        [SerializeField] private bool appendDelayedNameSuffixFallback;

        public EntityHandle Handle { get; private set; }

        private Color baseColor;
        private bool defeated;
        private bool delayed;
        private bool highlighted;
        private bool actedThisRound;
        private string baseDisplayName = string.Empty;
        private string duplicateBadgeValue = string.Empty;
        private float currentHpFill = 1f;
        private float currentDamageFraction;
        private LayoutElement layoutElement;
        private Team currentTeam;
        private Canvas slotCanvas;
        private RectTransform rootRectTransform;

        public event Action<InitiativeSlot> OnClicked;

        internal float ActiveScaleFactor => activeScaleFactor;

        private void Awake()
        {
            rootRectTransform = transform as RectTransform;
            layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = gameObject.AddComponent<LayoutElement>();

            slotCanvas = GetComponent<Canvas>();
            if (slotCanvas == null)
                slotCanvas = gameObject.AddComponent<Canvas>();

            slotCanvas.overrideSorting = false;
            slotCanvas.sortingOrder = 0;
            slotCanvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;

            if (rootRectTransform != null)
                rootRectTransform.pivot = new Vector2(0.5f, 1f);

            EnsureVisualRoot();

            if (enforceFixedLayoutSize)
            {
                layoutElement.minWidth = fixedPreferredWidth;
                layoutElement.preferredWidth = fixedPreferredWidth;
                layoutElement.flexibleWidth = 0f;

                layoutElement.minHeight = fixedPreferredHeight;
                layoutElement.preferredHeight = fixedPreferredHeight;
                layoutElement.flexibleHeight = 0f;
            }

            if (nameText != null)
            {
                nameText.enableWordWrapping = false;
                nameText.overflowMode = TextOverflowModes.Truncate;
            }

            EnsurePortraitHierarchy();
            InitHpStrip();
            InitDamageOverlay();
            EnsureDuplicateBadgeFallback();
            EnsureDelayedBadgeFallback();
            ApplyTypography();
            ApplyDuplicateBadgeVisual();
            ApplyDelayedBadgeVisual();
        }

        public void SetupStatic(EntityHandle handle, string displayName, Team team)
        {
            SetupStatic(handle, displayName, team, null, null, null);
        }

        public void SetupStatic(EntityHandle handle, string displayName, Team team, Sprite portrait)
        {
            SetupStatic(handle, displayName, team, portrait, null, null);
        }

        public void SetupStatic(EntityHandle handle, string displayName, Team team, Sprite portrait, Sprite frame)
        {
            SetupStatic(handle, displayName, team, portrait, frame, null);
        }

        public void SetupStatic(EntityHandle handle, string displayName, Team team, Sprite portrait, Sprite frame, string duplicateBadgeLabel)
        {
            Handle = handle;
            baseDisplayName = displayName ?? string.Empty;
            delayed = false;
            highlighted = false;
            actedThisRound = false;
            currentTeam = team;
            duplicateBadgeValue = duplicateBadgeLabel ?? string.Empty;
            transform.localScale = Vector3.one;
            ResetVisualScale();
            SetVisualOffsetX(0f);
            ApplyTypography();
            ApplyNameVisual();
            ApplyDuplicateBadgeVisual();
            ApplyDelayedBadgeVisual();
            ApplyPortraitMaskInsets(team);
            ApplyPortrait(portrait);
            ApplyFrame(frame);

            baseColor = team == Team.Player ? playerColor :
                        team == Team.Enemy  ? enemyColor  : neutralColor;

            defeated = false;
            ApplyColors();
            SetHighlight(false);
            UpdateVisualStacking();
        }

        public void RefreshHP(int currentHP, int maxHP, bool isAlive)
        {
            float fill = (maxHP > 0) ? Mathf.Clamp01((float)currentHP / maxHP) : 0f;
            currentHpFill = fill;
            currentDamageFraction = 1f - fill;

            if (hpBarFill != null)
            {
                UpdateHpStripGeometry(fill);
                hpBarFill.color = EvaluateHpStripColor(fill);
            }

            if (damageOverlay != null)
            {
                UpdateDamageOverlayGeometry(currentDamageFraction);
                var overlayColor = damageOverlayColor;
                overlayColor.a = currentDamageFraction > 0f
                    ? damageOverlayColor.a * currentDamageFraction
                    : 0f;
                damageOverlay.color = overlayColor;
                damageOverlay.gameObject.SetActive(currentDamageFraction > 0f);
            }

            if (!isAlive) SetDefeated(true);
        }

        public void SetHighlight(bool active)
        {
            highlighted = active;

            SetVisualScale(highlighted && !defeated ? activeScaleFactor : 1f);
            ApplyColors();
            UpdateVisualStacking();
        }

        public void SetActedThisRound(bool value)
        {
            if (actedThisRound == value)
                return;

            actedThisRound = value;
            ApplyColors();
        }

        public void SetDefeated(bool value)
        {
            if (defeated == value) return;
            defeated = value;
            if (defeated && hasPortrait)
                ResetVisualScale();
            ApplyColors();
            UpdateVisualStacking();
        }

        public void SetDelayed(bool value)
        {
            if (delayed == value) return;
            delayed = value;
            ApplyNameVisual();
            ApplyAlphaVisual();
            ApplyDelayedBadgeVisual();
        }

        private void EnsurePortraitHierarchy()
        {
            EnsureVisualRoot();
            if (portraitMaskRect != null
                && portraitImage != null
                && portraitAspectFitter != null
                && damageOverlay != null
                && frameImage != null
                && duplicateBadgeRoot != null)
                return;

            // Prepared portrait art already matches the frame window. Keep the frame on top.
            if (frameImage == null)
            {
                var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var frameRect = frameGo.GetComponent<RectTransform>();
                frameRect.SetParent(visualRoot, false);
                frameRect.anchorMin = Vector2.zero;
                frameRect.anchorMax = Vector2.one;
                frameRect.offsetMin = Vector2.zero;
                frameRect.offsetMax = Vector2.zero;
                frameRect.SetAsFirstSibling();

                frameImage = frameGo.GetComponent<Image>();
                frameImage.type = Image.Type.Simple;
                frameImage.pixelsPerUnitMultiplier = 1f;
                frameImage.preserveAspect = false;
                frameImage.raycastTarget = false;
                frameImage.color = Color.white;
                frameGo.SetActive(false);
            }

            // Mask container clips portrait + overlay to the slot bounds.
            if (portraitMaskRect == null)
            {
                var maskGo = new GameObject("PortraitMask", typeof(RectTransform), typeof(RectMask2D));
                portraitMaskRect = maskGo.GetComponent<RectTransform>();
                portraitMaskRect.SetParent(visualRoot, false);
                portraitMaskRect.anchorMin = Vector2.zero;
                portraitMaskRect.anchorMax = Vector2.one;
                portraitMaskRect.offsetMin = Vector2.zero;
                portraitMaskRect.offsetMax = Vector2.zero;
            }
            else if (visualRoot != null && portraitMaskRect.parent != visualRoot)
            {
                portraitMaskRect.SetParent(visualRoot, false);
            }

            // Portrait — prepared art fills the slot directly.
            if (portraitImage == null)
            {
                var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AspectRatioFitter));
                var portraitRect = portraitGo.GetComponent<RectTransform>();
                portraitRect.SetParent(portraitMaskRect, false);
                portraitRect.anchorMin = Vector2.zero;
                portraitRect.anchorMax = Vector2.one;
                portraitRect.offsetMin = Vector2.zero;
                portraitRect.offsetMax = Vector2.zero;
                portraitRect.pivot = new Vector2(0.5f, 0.5f);

                portraitImage = portraitGo.GetComponent<Image>();
                portraitImage.preserveAspect = false;
                portraitImage.raycastTarget = false;
                portraitImage.color = Color.white;
                portraitAspectFitter = portraitGo.GetComponent<AspectRatioFitter>();
                portraitAspectFitter.aspectMode = AspectRatioFitter.AspectMode.None;
                portraitAspectFitter.aspectRatio = 1f;
                portraitGo.SetActive(false);
            }
            else if (portraitAspectFitter == null)
            {
                portraitAspectFitter = portraitImage.GetComponent<AspectRatioFitter>();
                if (portraitAspectFitter == null)
                    portraitAspectFitter = portraitImage.gameObject.AddComponent<AspectRatioFitter>();

                portraitAspectFitter.aspectMode = AspectRatioFitter.AspectMode.None;
                portraitAspectFitter.aspectRatio = 1f;
                portraitImage.preserveAspect = false;
            }

            // Damage overlay — child of mask, fills mask rect
            if (damageOverlay == null)
            {
                var overlayGo = new GameObject("DamageOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var overlayRect = overlayGo.GetComponent<RectTransform>();
                overlayRect.SetParent(portraitMaskRect, false);
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
                overlayRect.SetSiblingIndex(portraitImage.transform.GetSiblingIndex() + 1);

                damageOverlay = overlayGo.GetComponent<Image>();
                damageOverlay.raycastTarget = false;
                overlayGo.SetActive(false);
            }

            if (portraitMaskRect != null)
                portraitMaskRect.SetAsFirstSibling();

            if (frameImage != null)
                frameImage.transform.SetAsLastSibling();
        }

        private void InitHpStrip()
        {
            if (hpBarFill == null)
                return;

            hpBarFill.type = Image.Type.Simple;
            hpBarFill.raycastTarget = false;
            UpdateHpStripGeometry(currentHpFill);
            hpBarFill.color = EvaluateHpStripColor(currentHpFill);
        }

        private void InitDamageOverlay()
        {
            if (damageOverlay == null) return;

            damageOverlay.type = Image.Type.Simple;
            damageOverlay.color = damageOverlayColor;
            damageOverlay.raycastTarget = false;
            UpdateDamageOverlayGeometry(0f);
            damageOverlay.gameObject.SetActive(false);
        }

        private bool hasPortrait;
        private bool hasFrame;

        private void ApplyPortrait(Sprite portrait)
        {
            hasPortrait = portrait != null;

            if (portraitImage != null)
            {
                if (hasPortrait)
                {
                    portraitImage.sprite = portrait;
                    portraitImage.color = Color.white;
                    portraitImage.preserveAspect = false;
                    portraitImage.gameObject.SetActive(true);
                    if (portraitAspectFitter != null)
                    {
                        portraitAspectFitter.aspectMode = AspectRatioFitter.AspectMode.None;
                        portraitAspectFitter.aspectRatio = 1f;
                    }
                }
                else
                {
                    portraitImage.gameObject.SetActive(false);
                }
            }

            if (portraitMaskRect != null)
                portraitMaskRect.gameObject.SetActive(hasPortrait);

            // When portrait is present: hide name, HP bar, and background (portrait + frame only)
            if (nameText != null)
                nameText.gameObject.SetActive(!hasPortrait);

            HideHpBar(false);
            ApplyDuplicateBadgeVisual();
        }

        private void HideHpBar(bool hide)
        {
            if (hpBarFill == null) return;

            // Hide the fill and its parent (HPBarBackground)
            hpBarFill.gameObject.SetActive(!hide);
            var hpBarParent = hpBarFill.transform.parent;
            if (hpBarParent != null && hpBarParent != transform)
                hpBarParent.gameObject.SetActive(!hide);
        }

        private void ApplyFrame(Sprite frame)
        {
            hasFrame = frame != null;
            ApplyPortraitMaskInsets(currentTeam);
            // When frame present: transparent fill (frame + portrait cover everything). When no frame: normal color.
            if (background != null && hasFrame)
                background.color = Color.clear;

            if (frameImage != null)
            {
                if (hasFrame)
                {
                    frameImage.sprite = frame;
                    frameImage.color = Color.white;
                    frameImage.gameObject.SetActive(true);
                }
                else
                {
                    frameImage.gameObject.SetActive(false);
                }
            }
        }

        private void ApplyPortraitMaskInsets(Team team)
        {
            if (portraitMaskRect == null)
                return;

            switch (team)
            {
                case Team.Player:
                    portraitMaskRect.offsetMin = playerPortraitMaskOffsetMin;
                    portraitMaskRect.offsetMax = playerPortraitMaskOffsetMax;
                    break;

                case Team.Enemy:
                    portraitMaskRect.offsetMin = enemyPortraitMaskOffsetMin;
                    portraitMaskRect.offsetMax = enemyPortraitMaskOffsetMax;
                    break;

                default:
                    portraitMaskRect.offsetMin = neutralPortraitMaskOffsetMin;
                    portraitMaskRect.offsetMax = neutralPortraitMaskOffsetMax;
                    break;
            }
        }

        private void ApplyColors()
        {
            if (background != null)
            {
                if (hasFrame)
                    background.color = Color.clear;
                else
                {
                    if (defeated)
                        background.color = defeatedColor;
                    else if (highlighted && hasPortrait)
                        background.color = activeFrameColor;
                    else
                        background.color = baseColor;
                }
            }

            if (frameImage != null && frameImage.gameObject.activeSelf)
            {
                frameImage.color = defeated
                    ? defeatedFrameTint
                    : highlighted ? activeFrameColor
                    : actedThisRound ? actedFrameTint
                    : Color.white;
            }

            if (defeated && portraitImage != null && portraitImage.gameObject.activeSelf)
            {
                portraitImage.color = defeatedPortraitTint;
            }
            else if (portraitImage != null && portraitImage.gameObject.activeSelf)
            {
                portraitImage.color = actedThisRound && !highlighted
                    ? actedPortraitTint
                    : Color.white;
            }

            if (activeHighlight != null)
            {
                bool showActiveHighlight = highlighted && !defeated;
                if (activeHighlight.activeSelf != showActiveHighlight)
                    activeHighlight.SetActive(showActiveHighlight);

                if (activeHighlight.TryGetComponent<Image>(out var activeHighlightImage))
                {
                    var highlightColor = activeFrameColor;
                    highlightColor.a = showActiveHighlight ? 0.96f : 0f;
                    activeHighlightImage.color = highlightColor;
                    activeHighlightImage.raycastTarget = false;
                }
            }

            ApplyAlphaVisual();
        }

        private void ApplyNameVisual()
        {
            if (nameText == null) return;

            // Hide name when portrait is displayed (BG3-style)
            if (hasPortrait)
            {
                nameText.gameObject.SetActive(false);
                return;
            }

            nameText.gameObject.SetActive(true);
            if (delayed && appendDelayedNameSuffixFallback && !HasDelayedBadgeVisual())
                nameText.SetText($"{baseDisplayName} (Delayed)");
            else
                nameText.SetText(baseDisplayName);
        }

        private bool HasDelayedBadgeVisual()
        {
            return delayedBadgeRoot != null;
        }

        private void ApplyDelayedBadgeVisual()
        {
            if (delayedBadgeRoot != null && delayedBadgeRoot.activeSelf != delayed)
                delayedBadgeRoot.SetActive(delayed);

            if (delayedBadgeBackground != null)
                delayedBadgeBackground.color = delayedBadgeBackgroundColor;

            if (delayedBadgeText != null)
            {
                ApplyTypography();
                delayedBadgeText.color = delayedBadgeTextColor;
                delayedBadgeText.SetText("DLY");
            }
        }

        private void ApplyDuplicateBadgeVisual()
        {
            bool showDuplicateBadge = hasPortrait && !string.IsNullOrEmpty(duplicateBadgeValue);
            if (duplicateBadgeRoot != null && duplicateBadgeRoot.activeSelf != showDuplicateBadge)
                duplicateBadgeRoot.SetActive(showDuplicateBadge);

            if (duplicateBadgeBackground != null)
                duplicateBadgeBackground.color = duplicateBadgeBackgroundColor;

            if (duplicateBadgeText != null)
            {
                ApplyTypography();
                duplicateBadgeText.SetText(duplicateBadgeValue);
            }
        }

        private void EnsureDuplicateBadgeFallback()
        {
            if (duplicateBadgeRoot != null)
            {
                if (visualRoot != null && duplicateBadgeRoot.transform.parent != visualRoot)
                    duplicateBadgeRoot.transform.SetParent(visualRoot, false);
                CacheDuplicateBadgeChildrenFromRootIfNeeded();
                return;
            }

            var existingBadge = transform.Find("DuplicateBadge");
            if (existingBadge != null && existingBadge is RectTransform)
            {
                duplicateBadgeRoot = existingBadge.gameObject;
                if (visualRoot != null && duplicateBadgeRoot.transform.parent != visualRoot)
                    duplicateBadgeRoot.transform.SetParent(visualRoot, false);
                CacheDuplicateBadgeChildrenFromRootIfNeeded();
                if (duplicateBadgeRoot != null)
                    duplicateBadgeRoot.SetActive(false);
                return;
            }

            if (nameText == null)
                return;

            var badgeRootGo = new GameObject("DuplicateBadge", typeof(RectTransform));
            var badgeRect = badgeRootGo.GetComponent<RectTransform>();
            badgeRect.SetParent(visualRoot != null ? visualRoot : transform, false);
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.pivot = new Vector2(0f, 1f);
            badgeRect.anchoredPosition = new Vector2(3f, -3f);
            badgeRect.sizeDelta = new Vector2(14f, 14f);

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.SetParent(badgeRect, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            duplicateBadgeBackground = bgGo.GetComponent<Image>();
            duplicateBadgeBackground.raycastTarget = false;

            var textClone = Instantiate(nameText, badgeRect);
            textClone.gameObject.name = "Label";
            var textRect = textClone.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textClone.raycastTarget = false;
            textClone.enableWordWrapping = false;
            textClone.overflowMode = TextOverflowModes.Overflow;
            textClone.alignment = TextAlignmentOptions.Center;
            textClone.fontSize = Mathf.Min(textClone.fontSize, 10.5f);
            textClone.fontStyle = FontStyles.Bold;
            duplicateBadgeText = textClone;

            duplicateBadgeRoot = badgeRootGo;
            duplicateBadgeRoot.SetActive(false);
        }

        private void EnsureDelayedBadgeFallback()
        {
            if (delayedBadgeRoot != null)
            {
                if (visualRoot != null && delayedBadgeRoot.transform.parent != visualRoot)
                    delayedBadgeRoot.transform.SetParent(visualRoot, false);
                CacheDelayedBadgeChildrenFromRootIfNeeded();
                return;
            }

            var existingBadge = transform.Find("DelayedBadge");
            if (existingBadge != null)
            {
                if (existingBadge is RectTransform)
                {
                    delayedBadgeRoot = existingBadge.gameObject;
                    if (visualRoot != null && delayedBadgeRoot.transform.parent != visualRoot)
                        delayedBadgeRoot.transform.SetParent(visualRoot, false);
                    CacheDelayedBadgeChildrenFromRootIfNeeded();
                    if (delayedBadgeRoot != null)
                        delayedBadgeRoot.SetActive(false);
                    return;
                }
            }

            if (nameText == null)
                return;

            var badgeRootGo = new GameObject("DelayedBadge", typeof(RectTransform));
            var badgeRect = badgeRootGo.GetComponent<RectTransform>();
            badgeRect.SetParent(visualRoot != null ? visualRoot : transform, false);
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition = new Vector2(-3f, -3f);
            badgeRect.sizeDelta = new Vector2(26f, 14f);

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.SetParent(badgeRect, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            delayedBadgeBackground = bgGo.GetComponent<Image>();
            delayedBadgeBackground.raycastTarget = false;

            // Reuse TMP settings/font from the slot name for a stable runtime fallback badge.
            var textClone = Instantiate(nameText, badgeRect);
            textClone.gameObject.name = "Label";
            var textRect = textClone.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textClone.raycastTarget = false;
            textClone.enableWordWrapping = false;
            textClone.overflowMode = TextOverflowModes.Overflow;
            textClone.alignment = TextAlignmentOptions.Center;
            textClone.fontSize = Mathf.Min(textClone.fontSize, 10.5f);
            textClone.fontStyle = FontStyles.Bold;
            delayedBadgeText = textClone;

            delayedBadgeRoot = badgeRootGo;
            delayedBadgeRoot.SetActive(false);
        }

        private void CacheDelayedBadgeChildrenFromRootIfNeeded()
        {
            if (delayedBadgeRoot == null)
                return;

            if (delayedBadgeBackground == null)
                delayedBadgeBackground = delayedBadgeRoot.GetComponentInChildren<Image>(includeInactive: true);

            if (delayedBadgeText == null)
                delayedBadgeText = delayedBadgeRoot.GetComponentInChildren<TMP_Text>(includeInactive: true);
        }

        private void CacheDuplicateBadgeChildrenFromRootIfNeeded()
        {
            if (duplicateBadgeRoot == null)
                return;

            if (duplicateBadgeBackground == null)
                duplicateBadgeBackground = duplicateBadgeRoot.GetComponentInChildren<Image>(includeInactive: true);

            if (duplicateBadgeText == null)
                duplicateBadgeText = duplicateBadgeRoot.GetComponentInChildren<TMP_Text>(includeInactive: true);
        }

        private void ApplyAlphaVisual()
        {
            float alpha = delayed ? 0.55f : 1f;
            if (actedThisRound && !highlighted && !defeated)
                alpha *= actedAlphaMultiplier;

            if (background != null)
            {
                var c = background.color;
                c.a = hasFrame ? 0f : alpha;
                background.color = c;
            }

            if (hpBarFill != null)
            {
                var c = hpBarFill.color;
                c.a = alpha;
                hpBarFill.color = c;

                if (hpBarFill.transform.parent != null
                    && hpBarFill.transform.parent.TryGetComponent<Image>(out var hpBarBackground))
                {
                    var bgColor = hpBarBackground.color;
                    bgColor.a = alpha;
                    hpBarBackground.color = bgColor;
                }
            }

            if (portraitImage != null && portraitImage.gameObject.activeSelf)
            {
                var c = portraitImage.color;
                c.a = alpha;
                portraitImage.color = c;
            }

            if (frameImage != null && frameImage.gameObject.activeSelf)
            {
                var c = frameImage.color;
                c.a = alpha;
                frameImage.color = c;
            }

            if (damageOverlay != null)
            {
                var overlayColor = damageOverlayColor;
                overlayColor.a = currentDamageFraction > 0f
                    ? damageOverlayColor.a * currentDamageFraction
                    : 0f;
                overlayColor.a *= alpha;
                damageOverlay.color = overlayColor;
            }

            if (!hasPortrait)
            {
                if (nameText != null)
                {
                    var c = nameText.color;
                    c.a = alpha;
                    nameText.color = c;
                }
            }

            if (duplicateBadgeBackground != null && duplicateBadgeRoot != null && duplicateBadgeRoot.activeSelf)
            {
                var c = duplicateBadgeBackground.color;
                c.a = alpha;
                duplicateBadgeBackground.color = c;
            }

            if (duplicateBadgeText != null && duplicateBadgeRoot != null && duplicateBadgeRoot.activeSelf)
            {
                var c = duplicateBadgeText.color;
                c.a = alpha;
                duplicateBadgeText.color = c;
            }

            if (delayedBadgeBackground != null && delayedBadgeRoot != null && delayedBadgeRoot.activeSelf)
            {
                var c = delayedBadgeBackground.color;
                c.a = alpha;
                delayedBadgeBackground.color = c;
            }

            if (delayedBadgeText != null && delayedBadgeRoot != null && delayedBadgeRoot.activeSelf)
            {
                var c = delayedBadgeText.color;
                c.a = alpha;
                delayedBadgeText.color = c;
            }
        }

        private void UpdateVisualStacking()
        {
            if (slotCanvas == null)
                return;

            bool bringToFront = highlighted && !defeated;
            slotCanvas.overrideSorting = bringToFront;
            slotCanvas.sortingOrder = bringToFront ? 10 : 0;
        }

        private void EnsureVisualRoot()
        {
            if (visualRoot == null)
            {
                var existing = transform.Find("VisualRoot") as RectTransform;
                if (existing != null)
                    visualRoot = existing;
            }

            if (visualRoot == null)
            {
                var go = new GameObject("VisualRoot", typeof(RectTransform));
                visualRoot = go.GetComponent<RectTransform>();
                visualRoot.SetParent(transform, false);
            }

            if (visualRoot == null)
                return;

            visualRoot.anchorMin = new Vector2(0.5f, 1f);
            visualRoot.anchorMax = new Vector2(0.5f, 1f);
            visualRoot.pivot = new Vector2(0.5f, 1f);
            visualRoot.anchoredPosition = visualRootBaseAnchoredPosition;
            visualRoot.sizeDelta = new Vector2(
                Mathf.Max(0f, fixedPreferredWidth),
                Mathf.Max(0f, fixedPreferredHeight));
            visualRoot.localScale = Vector3.one;
        }

        private void SetVisualScale(float scale)
        {
            if (visualRoot != null)
                visualRoot.localScale = Vector3.one * scale;
        }

        public void SetVisualOffsetX(float x)
        {
            if (visualRoot == null)
                return;

            visualRoot.anchoredPosition = new Vector2(visualRootBaseAnchoredPosition.x + x, visualRootBaseAnchoredPosition.y);
        }

        private void ResetVisualScale()
        {
            SetVisualScale(1f);
        }

        private Color EvaluateHpStripColor(float fill)
        {
            if (fill <= 0.25f)
                return hpStripLowColor;

            if (fill <= 0.55f)
            {
                float t = Mathf.InverseLerp(0.25f, 0.55f, fill);
                return Color.Lerp(hpStripLowColor, hpStripMidColor, t);
            }

            float highT = Mathf.InverseLerp(0.55f, 1f, fill);
            return Color.Lerp(hpStripMidColor, hpStripHighColor, highT);
        }

        private void UpdateHpStripGeometry(float fill)
        {
            if (hpBarFill == null)
                return;

            RectTransform rect = hpBarFill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(fill, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 0.5f);
        }

        private void UpdateDamageOverlayGeometry(float damageFraction)
        {
            if (damageOverlay == null)
                return;

            RectTransform rect = damageOverlay.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, damageFraction);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f);
        }

        private void ApplyTypography()
        {
            CombatUiTypography.ApplyTitle(nameText, 13.5f, 0.08f, CombatUiPalette.HudTextPrimaryColor);
            CombatUiTypography.ApplyButton(
                duplicateBadgeText,
                10.5f,
                0.04f,
                duplicateBadgeTextColor,
                FontStyles.Bold);
            CombatUiTypography.ApplyButton(
                delayedBadgeText,
                10.5f,
                0.08f,
                delayedBadgeTextColor,
                FontStyles.Bold);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            OnClicked?.Invoke(this);
        }
    }
}
