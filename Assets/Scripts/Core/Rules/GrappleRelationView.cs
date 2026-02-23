namespace PF2e.Core
{
    /// <summary>
    /// Read-only snapshot of an active grapple relation for queries/tests.
    /// </summary>
    public readonly struct GrappleRelationView
    {
        public readonly EntityHandle grappler;
        public readonly EntityHandle target;
        public readonly GrappleHoldState holdState;
        public readonly int turnEndsUntilExpire;

        public GrappleRelationView(EntityHandle grappler, EntityHandle target, GrappleHoldState holdState, int turnEndsUntilExpire)
        {
            this.grappler = grappler;
            this.target = target;
            this.holdState = holdState;
            this.turnEndsUntilExpire = turnEndsUntilExpire;
        }
    }
}
