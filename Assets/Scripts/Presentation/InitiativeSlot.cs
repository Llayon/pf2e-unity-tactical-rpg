using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PF2e.Core;

namespace PF2e.Presentation
{
    public class InitiativeSlot : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image hpBarFill;
        [SerializeField] private Image background;
        [SerializeField] private GameObject activeHighlight;

        [Header("Colors")]
        [SerializeField] private Color playerColor  = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField] private Color enemyColor   = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color neutralColor = new Color(0.85f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color defeatedColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        public EntityHandle Handle { get; private set; }

        private Color baseColor;
        private bool defeated;
        private bool delayed;
        private string baseDisplayName = string.Empty;

        public void SetupStatic(EntityHandle handle, string displayName, Team team)
        {
            Handle = handle;
            baseDisplayName = displayName ?? string.Empty;
            delayed = false;
            ApplyNameVisual();

            baseColor = team == Team.Player ? playerColor :
                        team == Team.Enemy  ? enemyColor  : neutralColor;

            defeated = false;
            ApplyColors();
            SetHighlight(false);
        }

        public void RefreshHP(int currentHP, int maxHP, bool isAlive)
        {
            float fill = (maxHP > 0) ? Mathf.Clamp01((float)currentHP / maxHP) : 0f;
            if (hpBarFill != null) hpBarFill.fillAmount = fill;

            if (!isAlive) SetDefeated(true);
        }

        public void SetHighlight(bool active)
        {
            if (activeHighlight != null)
                activeHighlight.SetActive(active);
        }

        public void SetDefeated(bool value)
        {
            if (defeated == value) return;
            defeated = value;
            ApplyColors();
        }

        public void SetDelayed(bool value)
        {
            if (delayed == value) return;
            delayed = value;
            ApplyNameVisual();
            ApplyAlphaVisual();
        }

        private void ApplyColors()
        {
            if (background != null)
                background.color = defeated ? defeatedColor : baseColor;

            ApplyAlphaVisual();
        }

        private void ApplyNameVisual()
        {
            if (nameText == null) return;
            nameText.SetText(delayed ? $"{baseDisplayName} (Delayed)" : baseDisplayName);
        }

        private void ApplyAlphaVisual()
        {
            float alpha = delayed ? 0.55f : 1f;

            if (background != null)
            {
                var c = background.color;
                c.a = alpha;
                background.color = c;
            }

            if (hpBarFill != null)
            {
                var c = hpBarFill.color;
                c.a = alpha;
                hpBarFill.color = c;
            }

            if (nameText != null)
            {
                var c = nameText.color;
                c.a = alpha;
                nameText.color = c;
            }
        }
    }
}
