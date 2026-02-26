using System;
using PF2e.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PF2e.Presentation
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(LayoutElement))]
    [RequireComponent(typeof(Image))]
    public class InitiativeInsertionMarker : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Fallback Visuals (optional)")]
        [SerializeField] private Image lineVisual;
        [SerializeField] private Image diamondVisual;

        [Header("Colors")]
        [SerializeField] private Color disabledColor = new Color(0.95f, 0.8f, 0.25f, 0.2f);
        [SerializeField] private Color readyColor = new Color(1f, 0.9f, 0.35f, 0.65f);
        [SerializeField] private Color hoverColor = new Color(1f, 0.95f, 0.45f, 1f);
        [SerializeField] private Color hoverHitAreaColor = new Color(1f, 0.9f, 0.35f, 0.08f);

        [Header("Sizing")]
        [SerializeField] private float preferredWidth = 14f;
        [SerializeField] private float lineWidth = 2f;
        [SerializeField] private float hoverLineWidth = 4f;
        [SerializeField] private float diamondSize = 8f;
        [SerializeField] private float hoverDiamondSize = 12f;

        private Image hitAreaImage;
        private Image background;
        private LayoutElement layoutElement;
        private bool interactable;
        private bool hovered;

        public EntityHandle AnchorHandle { get; private set; }
        public float PreferredWidth => preferredWidth;

        public event Action<InitiativeInsertionMarker> OnClicked;
        public event Action<InitiativeInsertionMarker> OnHoverEntered;
        public event Action<InitiativeInsertionMarker> OnHoverExited;

        private void Awake()
        {
            background = GetComponent<Image>();
            hitAreaImage = background;
            layoutElement = GetComponent<LayoutElement>();
            EnsureFallbackVisuals();
            ApplyLayout();
            ApplyVisualState();
        }

        private void OnEnable()
        {
            ApplyLayout();
            ApplyVisualState();
        }

        private void EnsureFallbackVisuals()
        {
            if (hitAreaImage != null)
            {
                hitAreaImage.raycastTarget = true;
                hitAreaImage.color = Color.clear;
            }

            if (lineVisual == null)
                lineVisual = CreateFallbackChild("Line");
            if (diamondVisual == null)
                diamondVisual = CreateFallbackChild("Diamond");

            ConfigureLineRect(lineVisual != null ? lineVisual.rectTransform : null);
            ConfigureDiamondRect(diamondVisual != null ? diamondVisual.rectTransform : null);
        }

        private Image CreateFallbackChild(string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void ConfigureLineRect(RectTransform rect)
        {
            if (rect == null) return;

            rect.anchorMin = new Vector2(0.5f, 0.12f);
            rect.anchorMax = new Vector2(0.5f, 0.88f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(2f, 0f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static void ConfigureDiamondRect(RectTransform rect)
        {
            if (rect == null) return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(8f, 8f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            rect.localScale = Vector3.one;
        }

        private void ApplyLayout()
        {
            if (layoutElement == null) return;

            layoutElement.minWidth = preferredWidth;
            layoutElement.preferredWidth = preferredWidth;
            layoutElement.flexibleWidth = 0f;
        }

        public void Setup(EntityHandle anchorHandle, bool canSelect)
        {
            AnchorHandle = anchorHandle;
            SetInteractable(canSelect);
        }

        public void SetOverlayPlacement(float anchoredX)
        {
            var rect = transform as RectTransform;
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(anchoredX, 0f);
            rect.sizeDelta = new Vector2(preferredWidth, 0f);
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (!interactable)
                hovered = false;

            ApplyVisualState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!interactable) return;
            hovered = true;
            ApplyVisualState();
            OnHoverEntered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!hovered) return;
            hovered = false;
            ApplyVisualState();
            OnHoverExited?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (!interactable) return;

            OnClicked?.Invoke(this);
        }

        private void ApplyVisualState()
        {
            var targetColor = !interactable
                ? disabledColor
                : (hovered ? hoverColor : readyColor);

            if (lineVisual != null)
            {
                lineVisual.color = targetColor;
                var lineRect = lineVisual.rectTransform;
                var size = lineRect.sizeDelta;
                size.x = hovered ? hoverLineWidth : lineWidth;
                lineRect.sizeDelta = size;
            }

            if (diamondVisual != null)
            {
                diamondVisual.color = targetColor;
                var diamondRect = diamondVisual.rectTransform;
                var size = diamondRect.sizeDelta;
                float targetSize = hovered ? hoverDiamondSize : diamondSize;
                size.x = targetSize;
                size.y = targetSize;
                diamondRect.sizeDelta = size;
            }

            if (hitAreaImage != null)
                hitAreaImage.color = (interactable && hovered) ? hoverHitAreaColor : Color.clear;
        }
    }
}
