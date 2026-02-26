using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PF2e.Presentation
{
    /// <summary>
    /// Owns Delay placement prompt/banner UI state (text, hover state, runtime fallback creation/styling).
    /// Keeps InitiativeBarController focused on initiative row rendering and marker interactions.
    /// </summary>
    internal sealed class DelayPlacementPromptPresenter
    {
        private readonly float minWidth;
        private readonly float maxWidth;
        private readonly float textPaddingX;
        private readonly float offsetY;

        private GameObject panelRoot;
        private TextMeshProUGUI roundLabel;

        private GameObject promptRoot;
        private TextMeshProUGUI promptLabel;
        private Image promptBackground;

        private bool hoverActive;

        public DelayPlacementPromptPresenter(
            float minWidth,
            float maxWidth,
            float textPaddingX,
            float offsetY)
        {
            this.minWidth = minWidth;
            this.maxWidth = maxWidth;
            this.textPaddingX = textPaddingX;
            this.offsetY = offsetY;
        }

        public GameObject PromptRoot => promptRoot;
        public TextMeshProUGUI PromptLabel => promptLabel;
        public Image PromptBackground => promptBackground;
        public bool IsHoverActive => hoverActive;

        public void Bind(
            GameObject panelRoot,
            TextMeshProUGUI roundLabel,
            GameObject promptRoot,
            TextMeshProUGUI promptLabel,
            Image promptBackground)
        {
            this.panelRoot = panelRoot;
            this.roundLabel = roundLabel;
            this.promptRoot = promptRoot;
            this.promptLabel = promptLabel;
            this.promptBackground = promptBackground;

            EnsurePromptRootFromLabel();
            if (this.promptBackground == null && this.promptRoot != null)
                this.promptBackground = this.promptRoot.GetComponent<Image>();
        }

        public void EnsureView()
        {
            if (promptLabel != null)
            {
                EnsurePromptRootFromLabel();
                StylePromptBanner();
                return;
            }

            if (panelRoot == null || roundLabel == null)
                return;

            var rootGo = new GameObject(
                "DelayPlacementPromptBanner",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.SetParent(panelRoot.transform, false);
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, offsetY);
            rootRect.sizeDelta = new Vector2(maxWidth, 24f);
            rootRect.SetAsLastSibling();

            promptRoot = rootGo;
            promptBackground = rootGo.GetComponent<Image>();

            var clone = Object.Instantiate(roundLabel, rootRect);
            clone.gameObject.name = "DelayPlacementPromptLabel";
            clone.gameObject.SetActive(true);
            clone.raycastTarget = false;
            clone.enableWordWrapping = false;
            clone.overflowMode = TextOverflowModes.Ellipsis;
            clone.alignment = TextAlignmentOptions.Center;
            clone.fontStyle = FontStyles.Bold;

            var promptRect = clone.rectTransform;
            promptRect.anchorMin = Vector2.zero;
            promptRect.anchorMax = Vector2.one;
            promptRect.offsetMin = new Vector2(10f, 2f);
            promptRect.offsetMax = new Vector2(-10f, -2f);

            promptLabel = clone;
            StylePromptBanner();
            Hide();
        }

        public void Hide()
        {
            hoverActive = false;
            SetVisible(false);
        }

        public void ClearHoverState()
        {
            hoverActive = false;
        }

        public void ShowDefaultPrompt()
        {
            hoverActive = false;
            SetPromptText("Choose delay position (between portraits)");
        }

        public void ShowAnchorPrompt(string anchorName)
        {
            hoverActive = true;
            SetPromptText($"Delay after {anchorName}");
        }

        private void SetPromptText(string text)
        {
            EnsureView();
            if (promptLabel == null)
                return;

            promptLabel.SetText(text);
            UpdatePromptBannerSize();
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (promptRoot != null)
            {
                if (promptRoot.activeSelf != visible)
                    promptRoot.SetActive(visible);
                return;
            }

            if (promptLabel == null)
                return;

            if (promptLabel.gameObject.activeSelf != visible)
                promptLabel.gameObject.SetActive(visible);
        }

        private void EnsurePromptRootFromLabel()
        {
            if (promptLabel == null || promptRoot != null)
                return;

            var candidate = promptLabel.transform.parent != null
                ? promptLabel.transform.parent.gameObject
                : null;

            if (candidate != null && candidate.name.Contains("DelayPlacementPrompt"))
                promptRoot = candidate;
        }

        private void StylePromptBanner()
        {
            if (promptBackground != null)
            {
                promptBackground.raycastTarget = false;
                promptBackground.color = new Color(0.07f, 0.07f, 0.1f, 0.92f);

                if (promptBackground.GetComponent<Outline>() == null)
                {
                    var outline = promptBackground.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(1f, 0.9f, 0.45f, 0.45f);
                    outline.effectDistance = new Vector2(1f, -1f);
                }
            }

            if (promptLabel != null)
            {
                promptLabel.raycastTarget = false;
                promptLabel.enableWordWrapping = false;
                promptLabel.overflowMode = TextOverflowModes.Ellipsis;
                promptLabel.alignment = TextAlignmentOptions.Center;
                promptLabel.color = new Color(0.98f, 0.95f, 0.82f, 1f);

                if (promptLabel.GetComponent<Shadow>() == null)
                {
                    var shadow = promptLabel.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
                    shadow.effectDistance = new Vector2(1f, -1f);
                }
            }

            if (promptRoot != null)
            {
                var rootRect = promptRoot.transform as RectTransform;
                if (rootRect != null)
                {
                    rootRect.anchorMin = new Vector2(0.5f, 1f);
                    rootRect.anchorMax = new Vector2(0.5f, 1f);
                    rootRect.pivot = new Vector2(0.5f, 0f);
                    rootRect.anchoredPosition = new Vector2(0f, offsetY);
                }
            }

            UpdatePromptBannerSize();
        }

        private void UpdatePromptBannerSize()
        {
            if (promptRoot == null || promptLabel == null)
                return;

            var rootRect = promptRoot.transform as RectTransform;
            if (rootRect == null)
                return;

            string text = promptLabel.text;
            if (string.IsNullOrEmpty(text))
                return;

            float preferredWidth = promptLabel.GetPreferredValues(text).x;
            float targetWidth = Mathf.Clamp(preferredWidth + textPaddingX, minWidth, maxWidth);

            var size = rootRect.sizeDelta;
            if (Mathf.Abs(size.x - targetWidth) > 0.5f)
            {
                size.x = targetWidth;
                rootRect.sizeDelta = size;
            }
        }
    }
}
