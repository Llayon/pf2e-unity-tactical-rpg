using UnityEngine;

namespace PF2e.Presentation
{
    /// <summary>
    /// Owns launcher popup open/close state and click-outside hit testing.
    /// </summary>
    public sealed class ActionBarLauncherPresenter
    {
        private readonly Vector3[] popupCornersBuffer = new Vector3[4];
        private readonly Vector3[] canvasCornersBuffer = new Vector3[4];

        private RectTransform strikeLauncherRect;
        private RectTransform tacticsLauncherRect;
        private RectTransform castLauncherRect;
        private RectTransform strikePopupRect;
        private RectTransform tacticsPopupRect;
        private RectTransform castPopupRect;

        public bool StrikePopupOpen { get; private set; }
        public bool TacticsPopupOpen { get; private set; }
        public bool CastPopupOpen { get; private set; }

        public bool AnyPopupOpen => StrikePopupOpen || TacticsPopupOpen || CastPopupOpen;

        public void Configure(
            RectTransform strikeLauncherRect,
            RectTransform tacticsLauncherRect,
            RectTransform castLauncherRect,
            RectTransform strikePopupRect,
            RectTransform tacticsPopupRect,
            RectTransform castPopupRect)
        {
            this.strikeLauncherRect = strikeLauncherRect;
            this.tacticsLauncherRect = tacticsLauncherRect;
            this.castLauncherRect = castLauncherRect;
            this.strikePopupRect = strikePopupRect;
            this.tacticsPopupRect = tacticsPopupRect;
            this.castPopupRect = castPopupRect;
        }

        public void ToggleStrikePopup()
        {
            bool next = !StrikePopupOpen;
            CloseAllPopups();
            SetStrikePopupVisible(next);
        }

        public void ToggleTacticsPopup()
        {
            bool next = !TacticsPopupOpen;
            CloseAllPopups();
            SetTacticsPopupVisible(next);
        }

        public void ToggleCastPopup()
        {
            bool next = !CastPopupOpen;
            CloseAllPopups();
            SetCastPopupVisible(next);
        }

        public void SetStrikePopupVisible(bool visible)
        {
            StrikePopupOpen = visible;
            SetRectActive(strikePopupRect, visible);
        }

        public void SetTacticsPopupVisible(bool visible)
        {
            TacticsPopupOpen = visible;
            SetRectActive(tacticsPopupRect, visible);
        }

        public void SetCastPopupVisible(bool visible)
        {
            CastPopupOpen = visible;
            SetRectActive(castPopupRect, visible);
        }

        public void CloseAllPopups()
        {
            SetStrikePopupVisible(false);
            SetTacticsPopupVisible(false);
            SetCastPopupVisible(false);
        }

        public bool IsPointInsideLauncherOrPopup(Vector2 screenPoint)
        {
            return IsPointInsideRect(screenPoint, strikeLauncherRect)
                || IsPointInsideRect(screenPoint, tacticsLauncherRect)
                || IsPointInsideRect(screenPoint, castLauncherRect)
                || IsPointInsideRect(screenPoint, strikePopupRect)
                || IsPointInsideRect(screenPoint, tacticsPopupRect)
                || IsPointInsideRect(screenPoint, castPopupRect);
        }

        public void ClampOpenPopupsToCanvas(RectTransform canvasRect, float padding)
        {
            if (!AnyPopupOpen || canvasRect == null)
                return;

            canvasRect.GetWorldCorners(canvasCornersBuffer);
            float minX = canvasCornersBuffer[0].x + padding;
            float maxX = canvasCornersBuffer[2].x - padding;
            float minY = canvasCornersBuffer[0].y + padding;
            float maxY = canvasCornersBuffer[1].y - padding;

            ClampPopupRect(strikePopupRect, minX, maxX, minY, maxY);
            ClampPopupRect(tacticsPopupRect, minX, maxX, minY, maxY);
            ClampPopupRect(castPopupRect, minX, maxX, minY, maxY);
        }

        private void ClampPopupRect(RectTransform popupRect, float minX, float maxX, float minY, float maxY)
        {
            if (popupRect == null || !popupRect.gameObject.activeInHierarchy)
                return;

            popupRect.GetWorldCorners(popupCornersBuffer);
            float left = popupCornersBuffer[0].x;
            float right = popupCornersBuffer[3].x;
            float bottom = popupCornersBuffer[0].y;
            float top = popupCornersBuffer[1].y;

            float deltaX = 0f;
            if (left < minX)
                deltaX = minX - left;
            else if (right > maxX)
                deltaX = maxX - right;

            float deltaY = 0f;
            if (bottom < minY)
                deltaY = minY - bottom;
            else if (top > maxY)
                deltaY = maxY - top;

            if (Mathf.Abs(deltaX) < 0.01f && Mathf.Abs(deltaY) < 0.01f)
                return;

            popupRect.position += new Vector3(deltaX, deltaY, 0f);
        }

        private static bool IsPointInsideRect(Vector2 screenPoint, RectTransform rect)
        {
            return rect != null
                && rect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null);
        }

        private static void SetRectActive(RectTransform rect, bool visible)
        {
            if (rect == null)
                return;

            if (rect.gameObject.activeSelf != visible)
                rect.gameObject.SetActive(visible);
        }
    }
}
