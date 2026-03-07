using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PF2e.Presentation
{
    public class CombatLogHoverController : MonoBehaviour
    {
        [SerializeField] private CombatLogController logController;
        [SerializeField] private CombatLogTooltipPanel tooltipPanel;
        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private ScrollRect scrollRect;

        private readonly Vector3[] viewportCorners = new Vector3[4];
        private readonly Vector3[] lineCorners = new Vector3[4];

        private Canvas rootCanvas;
        private UnityEngine.Camera uiCamera;
        private Vector2 lastMousePosition = new Vector2(float.NaN, float.NaN);
        private TextMeshProUGUI currentLine;
        private int currentLinkIndex = -1;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (logController == null) Debug.LogWarning("[CombatLogHoverController] logController is not assigned.", this);
            if (tooltipPanel == null) Debug.LogWarning("[CombatLogHoverController] tooltipPanel is not assigned.", this);
            if (viewportRect == null) Debug.LogWarning("[CombatLogHoverController] viewportRect is not assigned.", this);
            if (scrollRect == null) Debug.LogWarning("[CombatLogHoverController] scrollRect is not assigned.", this);
        }
#endif

        private void OnEnable()
        {
            if (logController == null || tooltipPanel == null || viewportRect == null || scrollRect == null)
            {
                Debug.LogError("[CombatLogHoverController] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            CacheCanvasAndCamera();
            scrollRect.onValueChanged.AddListener(HandleScrollChanged);
            ClearHover();
        }

        private void OnDisable()
        {
            if (scrollRect != null)
            {
                scrollRect.onValueChanged.RemoveListener(HandleScrollChanged);
            }

            ClearHover();
        }

        private void Update()
        {
            if (!TryGetMousePosition(out var mousePosition))
            {
                return;
            }

            if (mousePosition == lastMousePosition)
            {
                return;
            }

            ProcessPointer(mousePosition);
        }

        private void ProcessPointer(Vector2 mousePosition)
        {
            lastMousePosition = mousePosition;

            if (!IsPointerInsideViewport(mousePosition))
            {
                ClearHover();
                return;
            }

            if (!TryFindHoveredLink(mousePosition, out var hoveredLine, out int hoveredLinkIndex))
            {
                ClearHover();
                return;
            }

            if (hoveredLine == currentLine && hoveredLinkIndex == currentLinkIndex)
            {
                if (tooltipPanel.IsVisible)
                {
                    tooltipPanel.UpdatePosition(mousePosition);
                }

                return;
            }

            currentLine = hoveredLine;
            currentLinkIndex = hoveredLinkIndex;

            string token = hoveredLine.textInfo.linkInfo[hoveredLinkIndex].GetLinkID();
            if (logController.TryGetTooltip(hoveredLine, token, out string title, out string body))
            {
                tooltipPanel.Show(title, body, mousePosition);
            }
            else
            {
                ClearHover();
            }
        }

        private void HandleScrollChanged(Vector2 _)
        {
            ClearHover();
        }

        private void CacheCanvasAndCamera()
        {
            rootCanvas = viewportRect.GetComponentInParent<Canvas>();
            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>();
            }

            if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = null;
                return;
            }

            uiCamera = rootCanvas.worldCamera;
        }

        private bool IsPointerInsideViewport(Vector3 mousePosition)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(viewportRect, mousePosition, uiCamera);
        }

        private static bool TryGetMousePosition(out Vector2 mousePosition)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                mousePosition = mouse.position.ReadValue();
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            mousePosition = Input.mousePosition;
            return true;
#else
            mousePosition = default;
            return false;
#endif
        }

        private bool TryFindHoveredLink(Vector3 mousePosition, out TextMeshProUGUI hoveredLine, out int hoveredLinkIndex)
        {
            hoveredLine = null;
            hoveredLinkIndex = -1;

            var activeLines = logController.GetActiveLines();
            foreach (var line in activeLines)
            {
                if (line == null || !line.isActiveAndEnabled || !line.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!IsLineVisibleInViewport(line))
                {
                    continue;
                }

                int linkIndex = TMP_TextUtilities.FindIntersectingLink(line, mousePosition, uiCamera);
                if (linkIndex < 0)
                {
                    continue;
                }

                hoveredLine = line;
                hoveredLinkIndex = linkIndex;
                return true;
            }

            return false;
        }

        private bool IsLineVisibleInViewport(TextMeshProUGUI line)
        {
            line.rectTransform.GetWorldCorners(lineCorners);
            viewportRect.GetWorldCorners(viewportCorners);

            float lineTop = lineCorners[1].y > lineCorners[2].y ? lineCorners[1].y : lineCorners[2].y;
            float lineBottom = lineCorners[0].y < lineCorners[3].y ? lineCorners[0].y : lineCorners[3].y;
            float viewportTop = viewportCorners[1].y > viewportCorners[2].y ? viewportCorners[1].y : viewportCorners[2].y;
            float viewportBottom = viewportCorners[0].y < viewportCorners[3].y ? viewportCorners[0].y : viewportCorners[3].y;

            if (lineTop < viewportBottom)
            {
                return false;
            }

            if (lineBottom > viewportTop)
            {
                return false;
            }

            return true;
        }

        private void ClearHover()
        {
            currentLine = null;
            currentLinkIndex = -1;

            if (tooltipPanel != null)
            {
                tooltipPanel.Hide();
            }
        }
    }
}
