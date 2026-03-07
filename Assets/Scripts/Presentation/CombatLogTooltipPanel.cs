using TMPro;
using UnityEngine;

namespace PF2e.Presentation
{
    public class CombatLogTooltipPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Canvas rootCanvas;

        private RectTransform parentRect;
        private bool isVisible;

        public bool IsVisible => isVisible;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (panelRect == null) Debug.LogWarning("[CombatLogTooltipPanel] panelRect is not assigned.", this);
            if (titleText == null) Debug.LogWarning("[CombatLogTooltipPanel] titleText is not assigned.", this);
            if (bodyText == null) Debug.LogWarning("[CombatLogTooltipPanel] bodyText is not assigned.", this);
            if (canvasGroup == null) Debug.LogWarning("[CombatLogTooltipPanel] canvasGroup is not assigned.", this);
        }
#endif

        private void OnEnable()
        {
            EnsureReferences();
            Hide();
        }

        private void OnDisable()
        {
            isVisible = false;
        }

        public void Show(string title, string body, Vector2 screenPosition)
        {
            if (!EnsureReferences())
            {
                return;
            }

            titleText.text = title ?? string.Empty;
            bodyText.text = body ?? string.Empty;

            if (!panelRect.gameObject.activeSelf)
            {
                panelRect.gameObject.SetActive(true);
            }

            isVisible = true;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            UpdatePosition(screenPosition);
        }

        public void Hide()
        {
            EnsureReferences();

            isVisible = false;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (panelRect != null && panelRect.gameObject.activeSelf)
            {
                panelRect.gameObject.SetActive(false);
            }
        }

        public void UpdatePosition(Vector2 screenPosition)
        {
            if (!isVisible || panelRect == null || parentRect == null)
            {
                return;
            }

            float halfScreenWidth = Screen.width * 0.5f;
            float halfScreenHeight = Screen.height * 0.5f;
            float pivotX = screenPosition.x < halfScreenWidth ? 0f : 1f;
            float pivotY = screenPosition.y < halfScreenHeight ? 0f : 1f;
            panelRect.pivot = new Vector2(pivotX, pivotY);

            Camera uiCamera = ResolveCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, uiCamera, out var localPoint))
            {
                return;
            }

            panelRect.anchoredPosition = localPoint;
            ClampToParentBounds();
        }

        private bool EnsureReferences()
        {
            if (panelRect == null)
            {
                panelRect = transform as RectTransform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>();
            }

            if (panelRect != null && parentRect == null)
            {
                parentRect = panelRect.parent as RectTransform;
            }

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
            }

            return panelRect != null && titleText != null && bodyText != null && canvasGroup != null && parentRect != null;
        }

        private Camera ResolveCamera()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>();
            }

            if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return rootCanvas.worldCamera;
        }

        private void ClampToParentBounds()
        {
            Vector2 size = panelRect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            Vector2 pivot = panelRect.pivot;
            Rect parent = parentRect.rect;
            Vector2 pos = panelRect.anchoredPosition;

            float minX = parent.xMin + (size.x * pivot.x);
            float maxX = parent.xMax - (size.x * (1f - pivot.x));
            float minY = parent.yMin + (size.y * pivot.y);
            float maxY = parent.yMax - (size.y * (1f - pivot.y));

            if (minX > maxX)
            {
                float centerX = (minX + maxX) * 0.5f;
                minX = centerX;
                maxX = centerX;
            }

            if (minY > maxY)
            {
                float centerY = (minY + maxY) * 0.5f;
                minY = centerY;
                maxY = centerY;
            }

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            panelRect.anchoredPosition = pos;
        }
    }
}
