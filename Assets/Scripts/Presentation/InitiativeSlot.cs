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

        public void SetupStatic(EntityHandle handle, string displayName, Team team)
        {
            Handle = handle;

            if (nameText != null)
                nameText.SetText(displayName);

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

        private void ApplyColors()
        {
            if (background != null)
                background.color = defeated ? defeatedColor : baseColor;
        }
    }
}
