namespace PF2e.Presentation
{
    public readonly struct TargetingHintMessage
    {
        public readonly TargetingHintTone Tone;
        public readonly string Text;

        public bool IsHidden => Tone == TargetingHintTone.Hidden;

        public TargetingHintMessage(TargetingHintTone tone, string text)
        {
            Tone = tone;
            Text = text ?? string.Empty;
        }

        public static TargetingHintMessage Hidden()
        {
            return new TargetingHintMessage(TargetingHintTone.Hidden, string.Empty);
        }
    }
}
