using UnityEngine;
using PF2e.Core;

namespace PF2e.Presentation.Entity
{
    public class EntityView : MonoBehaviour
    {
        private EntityHandle handle;
        private MeshRenderer mr;

        private readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
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
            mpb.SetColor("_BaseColor", c);
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
