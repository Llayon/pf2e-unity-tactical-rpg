namespace PF2e.Presentation
{
    /// <summary>
    /// Combat log retention contract:
    /// maxLines > 0  => keep only the last N lines;
    /// maxLines <= 0 => unlimited history.
    /// </summary>
    public static class CombatLogRetentionPolicy
    {
        public static bool IsCapped(int maxLines)
        {
            return maxLines > 0;
        }

        public static string BuildNoticeText(int maxLines)
        {
            if (!IsCapped(maxLines))
            {
                return "Showing all lines";
            }

            return $"Showing last {maxLines} lines";
        }
    }
}
