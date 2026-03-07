using UnityEngine;

namespace PF2e.Core
{
    public readonly struct JumpResolvedEvent
    {
        public readonly EntityHandle actor;
        public readonly JumpType jumpType;
        public readonly Vector3Int fromCell;
        public readonly Vector3Int takeoffCell;
        public readonly Vector3Int landingCell;
        public readonly int actionCost;
        public readonly int runUpFeet;
        public readonly int jumpDistanceFeet;
        public readonly bool hasCheck;
        public readonly CheckRoll checkRoll;
        public readonly int dc;
        public readonly DegreeOfSuccess degree;
        public readonly bool movedToLanding;
        public readonly bool becameProne;

        public JumpResolvedEvent(
            EntityHandle actor,
            JumpType jumpType,
            Vector3Int fromCell,
            Vector3Int takeoffCell,
            Vector3Int landingCell,
            int actionCost,
            int runUpFeet,
            int jumpDistanceFeet,
            bool hasCheck,
            in CheckRoll checkRoll,
            int dc,
            DegreeOfSuccess degree,
            bool movedToLanding,
            bool becameProne)
        {
            this.actor = actor;
            this.jumpType = jumpType;
            this.fromCell = fromCell;
            this.takeoffCell = takeoffCell;
            this.landingCell = landingCell;
            this.actionCost = actionCost;
            this.runUpFeet = runUpFeet;
            this.jumpDistanceFeet = jumpDistanceFeet;
            this.hasCheck = hasCheck;
            this.checkRoll = checkRoll;
            this.dc = dc;
            this.degree = degree;
            this.movedToLanding = movedToLanding;
            this.becameProne = becameProne;
        }
    }
}
