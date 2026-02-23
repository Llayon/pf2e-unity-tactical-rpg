using UnityEngine;
using UnityEngine.Rendering;

namespace PF2e.Presentation.Entity
{
    /// <summary>
    /// Presentation-only targeting tint overlay for one entity view.
    /// Separate from EntityView selection visuals to avoid MPB conflicts.
    /// </summary>
    public class TargetingTintController : MonoBehaviour
    {
        [Header("Tint Colors")]
        [SerializeField] private Color eligibleColor = new Color(0.2f, 0.85f, 1f, 0.28f);
        [SerializeField] private Color hoverValidColor = new Color(0.25f, 1f, 0.35f, 0.45f);
        [SerializeField] private Color hoverInvalidColor = new Color(1f, 0.25f, 0.25f, 0.4f);
        [SerializeField] private float overlayScaleMultiplier = 1.08f;

        private GameObject overlayObject;
        private MeshRenderer overlayRenderer;
        private Material overlayMaterial;
        private TargetingTintState currentState = TargetingTintState.None;

        public TargetingTintState CurrentState => currentState;

        public void SetState(TargetingTintState state)
        {
            if (currentState == state)
                return;

            currentState = state;

            if (state == TargetingTintState.None)
            {
                if (overlayObject != null)
                    overlayObject.SetActive(false);
                return;
            }

            if (!EnsureOverlay())
                return;

            overlayObject.SetActive(true);
            ApplyColor(GetColorForState(state));
        }

        public void Clear()
        {
            SetState(TargetingTintState.None);
        }

        private bool EnsureOverlay()
        {
            if (overlayRenderer != null && overlayObject != null)
                return true;

            var sourceFilter = GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
                return false;

            overlayObject = new GameObject("TargetingTintOverlay");
            overlayObject.transform.SetParent(transform, false);
            overlayObject.transform.localScale = Vector3.one * overlayScaleMultiplier;
            overlayObject.layer = gameObject.layer;

            var filter = overlayObject.AddComponent<MeshFilter>();
            filter.sharedMesh = sourceFilter.sharedMesh;

            overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
            overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
                return false;

            overlayMaterial = new Material(shader)
            {
                name = "TargetingTintOverlay_Mat"
            };

            overlayRenderer.sharedMaterial = overlayMaterial;
            overlayObject.SetActive(false);
            return true;
        }

        private void ApplyColor(Color color)
        {
            if (overlayMaterial == null)
                return;

            if (overlayMaterial.HasProperty("_Color"))
                overlayMaterial.SetColor("_Color", color);
            if (overlayMaterial.HasProperty("_BaseColor"))
                overlayMaterial.SetColor("_BaseColor", color);
        }

        private Color GetColorForState(TargetingTintState state)
        {
            switch (state)
            {
                case TargetingTintState.Eligible:
                    return eligibleColor;
                case TargetingTintState.HoverValid:
                    return hoverValidColor;
                case TargetingTintState.HoverInvalid:
                    return hoverInvalidColor;
                default:
                    return Color.clear;
            }
        }

        private void OnDisable()
        {
            if (overlayObject != null)
                overlayObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (overlayMaterial != null)
                DestroyImmediate(overlayMaterial);
        }
    }
}
