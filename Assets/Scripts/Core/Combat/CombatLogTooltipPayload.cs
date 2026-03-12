namespace PF2e.Core
{
    public enum TooltipLayoutProfile : byte
    {
        Compact = 0,
        Standard = 1,
        Extended = 2
    }

    public readonly struct TooltipEntry
    {
        public readonly string token;
        public readonly string title;
        public readonly string body;
        public readonly TooltipLayoutProfile layoutProfile;

        public TooltipEntry(string token, string title, string body)
            : this(token, title, body, TooltipLayoutProfile.Standard)
        {
        }

        public TooltipEntry(string token, string title, string body, TooltipLayoutProfile layoutProfile)
        {
            this.token = token;
            this.title = title;
            this.body = body;
            this.layoutProfile = layoutProfile;
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
