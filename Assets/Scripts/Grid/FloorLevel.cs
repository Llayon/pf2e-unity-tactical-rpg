using UnityEngine;

namespace PF2e.Grid
{
    /// <summary>
    /// Attach to colliders to indicate which grid elevation level they represent.
    /// Used by GridInteraction instead of WorldToCell.y math.
    /// </summary>
    public class FloorLevel : MonoBehaviour
    {
        public int elevation;
    }
}
