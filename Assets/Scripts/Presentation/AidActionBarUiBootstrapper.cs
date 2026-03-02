using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PF2e.Presentation
{
    /// <summary>
    /// Resolves optional Aid UI references for Action Bar.
    /// Creation/wiring of missing Aid UI hierarchy is owned by scene authoring/validator autofix.
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
