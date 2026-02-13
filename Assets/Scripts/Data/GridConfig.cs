using UnityEngine;

namespace PF2e.Data
{
    [CreateAssetMenu(fileName = "GridConfig", menuName = "PF2e/Grid Config")]
    public class GridConfig : ScriptableObject
    {
        [Header("Grid Dimensions")]
        public float cellWorldSize = 1.5f;
        public float heightStepWorldSize = 1.5f;
        public int chunkSize = 16;

        [Header("Grid Colors")]
        public Color gridColor = new Color(1f, 1f, 1f, 0.3f);
        public Color hoverColor = new Color(1f, 1f, 0f, 0.4f);
        public Color selectedColor = new Color(0f, 0.8f, 1f, 0.5f);
        public Color moveZoneColor = new Color(0f, 1f, 0.5f, 0.3f);
        public Color pathColor = new Color(0f, 1f, 0.5f, 0.6f);
    }
}
