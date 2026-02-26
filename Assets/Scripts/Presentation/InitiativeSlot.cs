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
        [SerializeField] private GameObject activeHighlight;
        [SerializeField] private GameObject delayedBadgeRoot;
        [SerializeField] private Image delayedBadgeBackground;
        [SerializeField] private TMP_Text delayedBadgeText;

        [Header("Colors")]
        [SerializeField] private Color playerColor  = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField] private Color enemyColor   = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color neutralColor = new Color(0.85f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color defeatedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color delayedBadgeBackgroundColor = new Color(0.95f, 0.85f, 0.25f, 0.95f);
        [SerializeField] private Color delayedBadgeTextColor = new Color(0.1f, 0.08f, 0.03f, 1f);

        [Header("Layout Stability")]
        [SerializeField] private bool enforceFixedLayoutSize = true;
        [SerializeField] private float fixedPreferredWidth = 70f;
        [SerializeField] private float fixedPreferredHeight = 80f;

        [Header("Delayed State")]
        [SerializeField] private bool appendDelayedNameSuffixFallback;

        public EntityHandle Handle { get; private set; }

        private Color baseColor;
        private bool defeated;
        private bool delayed;
        private string baseDisplayName = string.Empty;
        private LayoutElement layoutElement;

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
                nameText.overflowMode = TextOverflowModes.Ellipsis;
            }

            EnsureDelayedBadgeFallback();
            ApplyDelayedBadgeVisual();
        }

        public void SetupStatic(EntityHandle handle, string displayName, Team team)
        {
            Handle = handle;
            baseDisplayName = displayName ?? string.Empty;
            delayed = false;
            ApplyNameVisual();
            ApplyDelayedBadgeVisual();

            baseColor = team == Team.Player ? playerColor :
                        team == Team.Enemy  ? enemyColor  : neutralColor;

            defeated = false;
            ApplyColors();
            SetHighlight(false);
        }

        public void RefreshHP(int currentHP, int maxHP, bool isAlive)
        {
            float fill = (maxHP > 0) ? Mathf.Clamp01((float)currentHP / maxHP) : 0f;
            if (hpBarFill != null) hpBarFill.fillAmount = fill;

            if (!isAlive) SetDefeated(true);
        }

        public void SetHighlight(bool active)
        {
            if (activeHighlight != null)
                activeHighlight.SetActive(active);
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

        private void ApplyColors()
        {
            if (background != null)
                background.color = defeated ? defeatedColor : baseColor;

            ApplyAlphaVisual();
        }

        private void ApplyNameVisual()
        {
            if (nameText == null) return;

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

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            OnClicked?.Invoke(this);
        }
    }
}
