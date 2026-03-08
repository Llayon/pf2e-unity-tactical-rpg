using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PF2e.Presentation
{
    public class CombatLogTooltipPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private float minWidth = 280f;
        [SerializeField] private float maxWidth = 360f;
        [SerializeField] private float edgePadding = 8f;
        [SerializeField] private float dockGap = 14f;
        [SerializeField] private RectTransform dockTarget;
        [SerializeField] private RectTransform dockViewport;
        [SerializeField] private Vector2 cursorOffset = new Vector2(14f, 14f);

        private readonly Vector3[] viewportCorners = new Vector3[4];
        private readonly Vector3[] targetCorners = new Vector3[4];
        private RectTransform parentRect;
        private LayoutElement panelLayoutElement;
        private bool isVisible;
        private bool loggedMissingDockTarget;

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

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
            }
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

            ApplyWidthConstraints();
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

            if (TryUpdateDockedPosition(screenPosition))
            {
                return;
            }

            UpdatePositionLegacy(screenPosition);
        }

        private bool TryUpdateDockedPosition(Vector2 screenPosition)
        {
            if (dockTarget == null)
            {
                if (!loggedMissingDockTarget)
                {
                    Debug.LogWarning("[CombatLogTooltipPanel] dockTarget is not assigned. Falling back to cursor positioning.", this);
                    loggedMissingDockTarget = true;
                }

                return false;
            }

            loggedMissingDockTarget = false;

            RectTransform viewport = dockViewport != null ? dockViewport : parentRect;
            if (viewport == null)
            {
                return false;
            }

            panelRect.pivot = new Vector2(1f, 0.5f);
            dockTarget.GetWorldCorners(targetCorners);
            float targetLeftX = targetCorners[0].x < targetCorners[1].x ? targetCorners[0].x : targetCorners[1].x;
            float targetCenterY = (targetCorners[0].y + targetCorners[1].y) * 0.5f;
            Vector3 targetLeftWorld = new Vector3(targetLeftX, targetCenterY, targetCorners[0].z);
            Vector3 targetLeftLocal = parentRect.InverseTransformPoint(targetLeftWorld);
            float anchoredX = targetLeftLocal.x - dockGap;

            UnityEngine.Camera uiCamera = ResolveCamera();
            float desiredY = targetLeftLocal.y;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, uiCamera, out var localMouse))
            {
                desiredY = localMouse.y;
            }

            viewport.GetWorldCorners(viewportCorners);
            float viewportBottom = parentRect.InverseTransformPoint(viewportCorners[0]).y;
            float viewportTop = parentRect.InverseTransformPoint(viewportCorners[1]).y;
            float panelHeight = Mathf.Max(panelRect.rect.height, LayoutUtility.GetPreferredHeight(panelRect));
            float halfHeight = panelHeight * 0.5f;

            float minY = viewportBottom + edgePadding + halfHeight;
            float maxY = viewportTop - edgePadding - halfHeight;
            float anchoredY;
            if (minY > maxY)
            {
                anchoredY = (viewportBottom + viewportTop) * 0.5f;
            }
            else
            {
                anchoredY = Mathf.Clamp(desiredY, minY, maxY);
            }

            panelRect.anchoredPosition = new Vector2(anchoredX, anchoredY);
            ClampToParentBounds();
            return true;
        }

        private void UpdatePositionLegacy(Vector2 screenPosition)
        {
            float halfScreenWidth = Screen.width * 0.5f;
            float halfScreenHeight = Screen.height * 0.5f;
            float pivotX = screenPosition.x < halfScreenWidth ? 0f : 1f;
            float pivotY = screenPosition.y < halfScreenHeight ? 0f : 1f;
            panelRect.pivot = new Vector2(pivotX, pivotY);

            UnityEngine.Camera uiCamera = ResolveCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, uiCamera, out var localPoint))
            {
                return;
            }

            localPoint.x += pivotX < 0.5f ? cursorOffset.x : -cursorOffset.x;
            localPoint.y += pivotY < 0.5f ? cursorOffset.y : -cursorOffset.y;
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

            if (panelRect != null && panelLayoutElement == null)
            {
                panelLayoutElement = panelRect.GetComponent<LayoutElement>();
                if (panelLayoutElement == null)
                {
                    panelLayoutElement = panelRect.gameObject.AddComponent<LayoutElement>();
                    panelLayoutElement.layoutPriority = 1;
                }
            }

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
            }

            return panelRect != null && titleText != null && bodyText != null && canvasGroup != null && parentRect != null;
        }

        private UnityEngine.Camera ResolveCamera()
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

        private void ApplyWidthConstraints()
        {
            if (panelRect == null || titleText == null || bodyText == null)
            {
                return;
            }

            float clampedMaxWidth = Mathf.Max(minWidth, maxWidth);
            float titleWidth = titleText.GetPreferredValues(titleText.text, clampedMaxWidth, 0f).x;
            float bodyWidth = bodyText.GetPreferredValues(bodyText.text, clampedMaxWidth, 0f).x;
            float contentWidth = Mathf.Max(titleWidth, bodyWidth);
            float horizontalPadding = 0f;

            if (panelRect.TryGetComponent<VerticalLayoutGroup>(out var layout))
            {
                horizontalPadding = layout.padding.left + layout.padding.right;
            }

            float targetWidth = Mathf.Clamp(contentWidth + horizontalPadding, minWidth, clampedMaxWidth);
            if (panelLayoutElement != null)
            {
                panelLayoutElement.minWidth = minWidth;
                panelLayoutElement.preferredWidth = targetWidth;
                panelLayoutElement.flexibleWidth = 0f;
            }

            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        }
    }
}
