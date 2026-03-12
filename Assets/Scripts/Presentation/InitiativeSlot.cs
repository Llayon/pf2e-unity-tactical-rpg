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
        [SerializeField] private Image portraitImage;
        [SerializeField] private Image damageOverlay;
        [SerializeField] private Image frameImage;
        [SerializeField] private GameObject activeHighlight;
        [SerializeField] private GameObject delayedBadgeRoot;
        [SerializeField] private Image delayedBadgeBackground;
        [SerializeField] private TMP_Text delayedBadgeText;

        [Header("Colors")]
        [SerializeField] private Color playerColor  = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField] private Color enemyColor   = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color neutralColor = new Color(0.85f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color defeatedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color activeFrameColor = new Color(1f, 0.92f, 0.7f, 1f);
        [SerializeField] private float activeScaleFactor = 1.15f;
        [SerializeField] private Color damageOverlayColor = new Color(0.7f, 0.05f, 0.05f, 0.75f);
        [SerializeField] private Color delayedBadgeBackgroundColor = new Color(0.95f, 0.85f, 0.25f, 0.95f);
        [SerializeField] private Color delayedBadgeTextColor = new Color(0.1f, 0.08f, 0.03f, 1f);

        [Header("Layout Stability")]
        [SerializeField] private bool enforceFixedLayoutSize = true;
        [SerializeField] private float fixedPreferredWidth = 60f;
        [SerializeField] private float fixedPreferredHeight = 90f;

        [Header("Delayed State")]
        [SerializeField] private bool appendDelayedNameSuffixFallback;

        public EntityHandle Handle { get; private set; }

        private Color baseColor;
        private bool defeated;
        private bool delayed;
        private bool highlighted;
        private string baseDisplayName = string.Empty;
        private LayoutElement layoutElement;
        private RectTransform portraitMaskRect;

        public event Action<InitiativeSlot> OnClicked;

        private void Awake()
        {
            layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = gameObject.AddComponent<LayoutElement>();

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
            InitDamageOverlay();
            EnsureDelayedBadgeFallback();
            ApplyDelayedBadgeVisual();
        }

        public void SetupStatic(EntityHandle handle, string displayName, Team team)
        {
            SetupStatic(handle, displayName, team, null, null);
        }

        public void SetupStatic(EntityHandle handle, string displayName, Team team, Sprite portrait)
        {
            SetupStatic(handle, displayName, team, portrait, null);
        }

        public void SetupStatic(EntityHandle handle, string displayName, Team team, Sprite portrait, Sprite frame)
        {
            Handle = handle;
            baseDisplayName = displayName ?? string.Empty;
            delayed = false;
            highlighted = false;
            transform.localScale = Vector3.one;
            ApplyNameVisual();
            ApplyDelayedBadgeVisual();
            ApplyPortrait(portrait);
            ApplyFrame(frame);

            baseColor = team == Team.Player ? playerColor :
                        team == Team.Enemy  ? enemyColor  : neutralColor;

            defeated = false;
            ApplyColors();
            SetHighlight(false);
        }

        public void RefreshHP(int currentHP, int maxHP, bool isAlive)
        {
            float fill = (maxHP > 0) ? Mathf.Clamp01((float)currentHP / maxHP) : 0f;

            // Legacy green HP bar (only when no portrait)
            if (!hasPortrait && hpBarFill != null)
                hpBarFill.fillAmount = fill;

            // BG3-style: damage overlay fills from bottom as HP drops
            if (damageOverlay != null)
            {
                float damageFraction = 1f - fill;
                damageOverlay.fillAmount = damageFraction;
                damageOverlay.gameObject.SetActive(damageFraction > 0f);
            }

            if (!isAlive) SetDefeated(true);
        }

        public void SetHighlight(bool active)
        {
            highlighted = active;

            if (hasPortrait)
            {
                // BG3-style: scale up + bright frame instead of yellow overlay
                transform.localScale = active ? Vector3.one * activeScaleFactor : Vector3.one;

                if (!hasFrame && background != null && !defeated)
                    background.color = active ? activeFrameColor : baseColor;

                // Hide legacy yellow overlay in portrait mode
                if (activeHighlight != null)
                    activeHighlight.SetActive(false);
            }
            else
            {
                // Legacy mode (no portrait): use yellow overlay
                transform.localScale = Vector3.one;
                if (activeHighlight != null)
                    activeHighlight.SetActive(active);
            }
        }

        public void SetDefeated(bool value)
        {
            if (defeated == value) return;
            defeated = value;
            ApplyColors();
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
            if (portraitImage != null && damageOverlay != null && frameImage != null)
                return;

            // Mask container — clips portrait + overlay to inset rect
            if (portraitMaskRect == null)
            {
                var maskGo = new GameObject("PortraitMask", typeof(RectTransform), typeof(RectMask2D));
                portraitMaskRect = maskGo.GetComponent<RectTransform>();
                portraitMaskRect.SetParent(transform, false);
                portraitMaskRect.anchorMin = Vector2.zero;
                portraitMaskRect.anchorMax = Vector2.one;
                portraitMaskRect.offsetMin = new Vector2(6f, 6f);
                portraitMaskRect.offsetMax = new Vector2(-6f, -6f);
                portraitMaskRect.SetSiblingIndex(0);
            }

            // Portrait — child of mask, fills mask rect
            if (portraitImage == null)
            {
                var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var portraitRect = portraitGo.GetComponent<RectTransform>();
                portraitRect.SetParent(portraitMaskRect, false);
                portraitRect.anchorMin = Vector2.zero;
                portraitRect.anchorMax = Vector2.one;
                portraitRect.offsetMin = Vector2.zero;
                portraitRect.offsetMax = Vector2.zero;

                portraitImage = portraitGo.GetComponent<Image>();
                portraitImage.preserveAspect = false;
                portraitImage.raycastTarget = false;
                portraitImage.color = Color.white;
                portraitGo.SetActive(false);
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

            // Frame overlay — direct child of slot (NOT inside mask)
            // Extends beyond slot bounds so the frame's visual border overlaps portrait edges
            if (frameImage == null)
            {
                var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var frameRect = frameGo.GetComponent<RectTransform>();
                frameRect.SetParent(transform, false);
                frameRect.anchorMin = Vector2.zero;
                frameRect.anchorMax = Vector2.one;
                frameRect.offsetMin = new Vector2(-4f, -4f);
                frameRect.offsetMax = new Vector2(4f, 4f);
                // Place frame after mask in sibling order so it renders on top
                frameRect.SetSiblingIndex(portraitMaskRect.GetSiblingIndex() + 1);

                frameImage = frameGo.GetComponent<Image>();
                frameImage.type = Image.Type.Sliced;
                frameImage.pixelsPerUnitMultiplier = 15f;
                frameImage.preserveAspect = false;
                frameImage.raycastTarget = false;
                frameImage.color = Color.white;
                frameGo.SetActive(false);
            }
        }

        private void InitDamageOverlay()
        {
            if (damageOverlay == null) return;

            damageOverlay.type = Image.Type.Filled;
            damageOverlay.fillMethod = Image.FillMethod.Vertical;
            damageOverlay.fillOrigin = (int)Image.OriginVertical.Bottom;
            damageOverlay.fillAmount = 0f;
            damageOverlay.color = damageOverlayColor;
            damageOverlay.raycastTarget = false;
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
                    portraitImage.gameObject.SetActive(true);
                }
                else
                {
                    portraitImage.gameObject.SetActive(false);
                }
            }

            // When portrait is present: hide name, HP bar, and background (portrait + frame only)
            if (nameText != null)
                nameText.gameObject.SetActive(!hasPortrait);

            HideHpBar(hasPortrait);
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
                frameImage.color = defeated ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;

            if (defeated && portraitImage != null && portraitImage.gameObject.activeSelf)
                portraitImage.color = new Color(0.4f, 0.4f, 0.4f, 1f);

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
                delayedBadgeText.color = delayedBadgeTextColor;
                delayedBadgeText.SetText("DLY");
            }
        }

        private void EnsureDelayedBadgeFallback()
        {
            if (delayedBadgeRoot != null)
            {
                CacheDelayedBadgeChildrenFromRootIfNeeded();
                return;
            }

            var existingBadge = transform.Find("DelayedBadge");
            if (existingBadge != null)
            {
                if (existingBadge is RectTransform)
                {
                    delayedBadgeRoot = existingBadge.gameObject;
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
            badgeRect.SetParent(transform, false);
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition = new Vector2(-3f, -3f);
            badgeRect.sizeDelta = new Vector2(22f, 12f);

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
            textClone.fontSize = Mathf.Min(textClone.fontSize, 8f);
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

        private void ApplyAlphaVisual()
        {
            float alpha = delayed ? 0.55f : 1f;

            if (background != null)
            {
                var c = background.color;
                c.a = alpha;
                background.color = c;
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

            if (!hasPortrait)
            {
                if (hpBarFill != null)
                {
                    var c = hpBarFill.color;
                    c.a = alpha;
                    hpBarFill.color = c;
                }

                if (nameText != null)
                {
                    var c = nameText.color;
                    c.a = alpha;
                    nameText.color = c;
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            OnClicked?.Invoke(this);
        }
    }
}
