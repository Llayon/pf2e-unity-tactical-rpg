namespace PF2e.Core
{
    public readonly struct TooltipEntry
    {
        public readonly string token;
        public readonly string title;
        public readonly string body;

        public TooltipEntry(string token, string title, string body)
        {
            this.token = token;
            this.title = title;
            this.body = body;
        }
    }

    public readonly struct CombatLogTooltipPayload
    {
        public readonly TooltipEntry[] entries;

        public CombatLogTooltipPayload(TooltipEntry[] entries)
        {
            this.entries = entries;
        }

        public bool HasEntries => entries != null && entries.Length > 0;
    }
}
