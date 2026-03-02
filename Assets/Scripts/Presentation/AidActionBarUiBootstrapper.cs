using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PF2e.Presentation
{
    /// <summary>
    /// Resolves/creates optional Aid UI references for Action Bar compatibility with older scene wiring.
    /// </summary>
    public sealed class AidActionBarUiBootstrapper
    {
        private const string AidButtonName = "AidButton";
        private const string AidPreparedBadgeName = "AidPreparedBadge";
        private const string ActiveHighlightName = "ActiveHighlight";

        public void ResolveOptionalReferences(
            MonoBehaviour owner,
            Button escapeButton,
            Button demoralizeButton,
            Button strikeButton,
            ref Button aidButton,
            ref Image aidHighlight,
            ref GameObject aidPreparedIndicatorRoot,
            ref TMP_Text aidPreparedIndicatorLabel,
            Color aidPreparedIndicatorFillColor,
            Color aidPreparedIndicatorLabelColor)
        {
            if (owner == null)
                return;

            if (aidButton == null)
                aidButton = FindAidButtonInHierarchy(owner.transform);

            if (aidButton == null)
                aidButton = TryCreateAidButtonFromTemplate(escapeButton, demoralizeButton, strikeButton);

            if (aidHighlight == null && aidButton != null)
                aidHighlight = FindAidHighlight(aidButton);

            ResolveAidPreparedIndicatorReferences(
                aidButton,
                ref aidPreparedIndicatorRoot,
                ref aidPreparedIndicatorLabel,
                aidPreparedIndicatorFillColor,
                aidPreparedIndicatorLabelColor);
        }

        public void ResolveAidPreparedIndicatorReferences(
            Button aidButton,
            ref GameObject aidPreparedIndicatorRoot,
            ref TMP_Text aidPreparedIndicatorLabel,
            Color aidPreparedIndicatorFillColor,
            Color aidPreparedIndicatorLabelColor)
        {
            if (aidButton == null)
                return;

            if (aidPreparedIndicatorRoot == null)
            {
                var existing = aidButton.transform.Find(AidPreparedBadgeName);
                if (existing != null)
                    aidPreparedIndicatorRoot = existing.gameObject;
            }

            if (aidPreparedIndicatorRoot == null)
                aidPreparedIndicatorRoot = TryCreateAidPreparedIndicator(aidButton);

            if (aidPreparedIndicatorLabel == null && aidPreparedIndicatorRoot != null)
                aidPreparedIndicatorLabel = aidPreparedIndicatorRoot.GetComponentInChildren<TMP_Text>(true);

            ApplyAidPreparedIndicatorStyle(
                aidPreparedIndicatorRoot,
                aidPreparedIndicatorLabel,
                aidPreparedIndicatorFillColor,
                aidPreparedIndicatorLabelColor);
        }

        private static Button FindAidButtonInHierarchy(Transform root)
        {
            if (root == null)
                return null;

            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null)
                    continue;

                if (string.Equals(button.gameObject.name, AidButtonName, StringComparison.Ordinal))
                    return button;
            }

            return null;
        }

        private static Button TryCreateAidButtonFromTemplate(Button escapeButton, Button demoralizeButton, Button strikeButton)
        {
            var template = escapeButton != null ? escapeButton : (demoralizeButton != null ? demoralizeButton : strikeButton);
            if (template == null || template.transform.parent == null)
                return null;

            var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent, false);
            clone.name = AidButtonName;
            clone.SetActive(true);
            clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

            var button = clone.GetComponent<Button>();
            if (button != null)
                button.onClick.RemoveAllListeners();

            var labels = clone.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null)
                    continue;

                if (string.Equals(label.text, "ESC", StringComparison.OrdinalIgnoreCase))
                    label.text = "A";
                else if (string.Equals(label.text, "Escape", StringComparison.OrdinalIgnoreCase))
                    label.text = "Aid";
            }

            return button;
        }

        private static Image FindAidHighlight(Button button)
        {
            if (button == null)
                return null;

            var direct = button.transform.Find(ActiveHighlightName);
            if (direct != null)
                return direct.GetComponent<Image>();

            var images = button.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var image = images[i];
                if (image == null)
                    continue;

                if (string.Equals(image.gameObject.name, ActiveHighlightName, StringComparison.Ordinal))
                    return image;
            }

            return null;
        }

        private static GameObject TryCreateAidPreparedIndicator(Button aidButton)
        {
            if (aidButton == null)
                return null;

            var badgeGo = new GameObject(AidPreparedBadgeName, typeof(RectTransform), typeof(Image));
            badgeGo.transform.SetParent(aidButton.transform, false);

            var rect = badgeGo.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-4f, -4f);
                rect.sizeDelta = new Vector2(10f, 10f);
            }

            var image = badgeGo.GetComponent<Image>();
            if (image != null)
                image.raycastTarget = false;

            badgeGo.SetActive(false);
            return badgeGo;
        }

        private static void ApplyAidPreparedIndicatorStyle(
            GameObject aidPreparedIndicatorRoot,
            TMP_Text aidPreparedIndicatorLabel,
            Color aidPreparedIndicatorFillColor,
            Color aidPreparedIndicatorLabelColor)
        {
            if (aidPreparedIndicatorRoot == null)
                return;

            var image = aidPreparedIndicatorRoot.GetComponent<Image>();
            if (image != null)
                image.color = aidPreparedIndicatorFillColor;

            if (aidPreparedIndicatorLabel != null)
                aidPreparedIndicatorLabel.color = aidPreparedIndicatorLabelColor;
        }
    }
}
