using UnityEngine;
using UnityEngine.UI;

namespace PF2e.Presentation
{
    /// <summary>
    /// Applies a 9-slice panel sprite to UI Image backgrounds.
    /// Attach to any GameObject; assign target Images and sprite in inspector.
    /// </summary>
    public class PanelSkinApplier : MonoBehaviour
    {
        [Header("Sprite")]
        [SerializeField] private Sprite panelSprite;

        [Header("Target Panels")]
        [SerializeField] private Image[] targetPanels;

        [Header("Options")]
        [SerializeField] private float pixelsPerUnitMultiplier = 1f;

        private void OnEnable()
        {
            Apply();
        }

        public void Apply()
        {
            if (panelSprite == null)
            {
                Debug.LogWarning("[PanelSkinApplier] No panel sprite assigned.", this);
                return;
            }

            foreach (var img in targetPanels)
            {
                if (img == null) continue;
                img.sprite = panelSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                img.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (panelSprite == null)
                Debug.LogWarning("[PanelSkinApplier] Assign a 9-slice panel sprite.", this);
        }
#endif
    }
}
