using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PF2e.Core;

namespace PF2e.Presentation
{
    /// <summary>
    /// Shows end-of-encounter result (Victory/Defeat/Aborted) and provides restart/close actions.
    /// Uses CanvasGroup visibility to keep subscriptions alive.
    /// </summary>
    public class EncounterEndPanelController : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private CombatEventBus eventBus;

        [Header("UI")]
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button closeButton;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (eventBus == null) Debug.LogError("[EncounterEndPanel] Missing CombatEventBus", this);
            if (panelCanvasGroup == null) Debug.LogError("[EncounterEndPanel] Missing CanvasGroup", this);
            if (titleText == null) Debug.LogWarning("[EncounterEndPanel] titleText not assigned", this);
            if (subtitleText == null) Debug.LogWarning("[EncounterEndPanel] subtitleText not assigned", this);
            if (restartButton == null) Debug.LogWarning("[EncounterEndPanel] restartButton not assigned", this);
            if (closeButton == null) Debug.LogWarning("[EncounterEndPanel] closeButton not assigned", this);
        }
#endif

        private void OnEnable()
        {
            if (panelCanvasGroup == null)
                panelCanvasGroup = GetComponent<CanvasGroup>();

            if (eventBus == null || panelCanvasGroup == null)
            {
                Debug.LogError("[EncounterEndPanel] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            eventBus.OnCombatStartedTyped += HandleCombatStarted;
            eventBus.OnCombatEndedTyped += HandleCombatEnded;

            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartClicked);
            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseClicked);

            HidePanel();
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                eventBus.OnCombatStartedTyped -= HandleCombatStarted;
                eventBus.OnCombatEndedTyped -= HandleCombatEnded;
            }

            if (restartButton != null)
                restartButton.onClick.RemoveListener(OnRestartClicked);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void HandleCombatStarted(in CombatStartedEvent e)
        {
            HidePanel();
        }

        private void HandleCombatEnded(in CombatEndedEvent e)
        {
            switch (e.result)
            {
                case EncounterResult.Victory:
                    if (titleText != null) titleText.text = "Victory";
                    if (subtitleText != null) subtitleText.text = "All enemies defeated.";
                    break;
                case EncounterResult.Defeat:
                    if (titleText != null) titleText.text = "Defeat";
                    if (subtitleText != null) subtitleText.text = "All players defeated.";
                    break;
                default:
                    if (titleText != null) titleText.text = "Encounter Ended";
                    if (subtitleText != null) subtitleText.text = "Combat was ended manually.";
                    break;
            }

            ShowPanel();
        }

        private void OnRestartClicked()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(activeScene.name);
            }
        }

        private void OnCloseClicked()
        {
            HidePanel();
        }

        private void ShowPanel()
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.blocksRaycasts = true;
            panelCanvasGroup.interactable = true;
        }

        private void HidePanel()
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.interactable = false;
        }
    }
}
