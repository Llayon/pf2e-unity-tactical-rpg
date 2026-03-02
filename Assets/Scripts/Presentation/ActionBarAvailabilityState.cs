namespace PF2e.Presentation
{
    public readonly struct ActionBarAvailabilityState
    {
        public readonly bool strikeInteractable;
        public readonly bool tripInteractable;
        public readonly bool shoveInteractable;
        public readonly bool grappleInteractable;
        public readonly bool repositionInteractable;
        public readonly bool demoralizeInteractable;
        public readonly bool escapeInteractable;
        public readonly bool aidInteractable;
        public readonly bool raiseShieldInteractable;
        public readonly bool standInteractable;

        public ActionBarAvailabilityState(
            bool strikeInteractable,
            bool tripInteractable,
            bool shoveInteractable,
            bool grappleInteractable,
            bool repositionInteractable,
            bool demoralizeInteractable,
            bool escapeInteractable,
            bool aidInteractable,
            bool raiseShieldInteractable,
            bool standInteractable)
        {
            this.strikeInteractable = strikeInteractable;
            this.tripInteractable = tripInteractable;
            this.shoveInteractable = shoveInteractable;
            this.grappleInteractable = grappleInteractable;
            this.repositionInteractable = repositionInteractable;
            this.demoralizeInteractable = demoralizeInteractable;
            this.escapeInteractable = escapeInteractable;
            this.aidInteractable = aidInteractable;
            this.raiseShieldInteractable = raiseShieldInteractable;
            this.standInteractable = standInteractable;
        }
    }
}
