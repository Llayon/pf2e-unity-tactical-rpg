using UnityEngine;

namespace PF2e.Presentation
{
    /// <summary>
    /// Owns launcher popup open/close state and click-outside hit testing.
    /// </summary>
    public sealed class ActionBarLauncherPresenter
    {
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
