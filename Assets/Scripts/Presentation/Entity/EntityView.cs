using UnityEngine;
using PF2e.Core;

namespace PF2e.Presentation.Entity
{
    public class EntityView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private EntityHandle handle;
        private MeshRenderer mr;

        private MaterialPropertyBlock mpb;
        private Color baseColor;
        private bool selected;

        public EntityHandle Handle => handle;

        public void Initialize(EntityHandle h, Color color)
        {
            handle = h;
            mr = GetComponent<MeshRenderer>();
            baseColor = color;
            ApplyColor(baseColor);
        }

        private void ApplyColor(Color c)
        {
            if (mr == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            mr.SetPropertyBlock(mpb);
        }

        public void SetSelected(bool isSelected)
        {
            selected = isSelected;
            var c = selected ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor;
            ApplyColor(c);
        }
    }
}
