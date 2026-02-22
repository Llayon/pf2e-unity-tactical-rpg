using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PF2e.Core;

namespace PF2e.Presentation
{
    /// <summary>
    /// Modal UI for Shield Block reaction prompt.
    /// Panel-as-child pattern: controller stays on parent, rootPanel is a child that gets SetActive toggled.
    /// Single pending callback, no queue (MVP).
    /// </summary>
    public class ReactionPromptController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        [Header("Settings")]
        [SerializeField] private float timeoutSeconds = 10f;

        private Action<bool> pendingCallback;
        private float promptStartTime;
        private Button boundYesButton;
        private Button boundNoButton;

        public bool IsPromptActive => pendingCallback != null;

        public float TimeoutSeconds => timeoutSeconds;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (rootPanel == null) Debug.LogWarning("[ReactionPrompt] Missing rootPanel", this);
            if (yesButton == null) Debug.LogWarning("[ReactionPrompt] Missing yesButton", this);
            if (noButton == null) Debug.LogWarning("[ReactionPrompt] Missing noButton", this);
        }
#endif

        private void Awake()
        {
            if (rootPanel != null)
                rootPanel.SetActive(false);
        }

        private void OnEnable()
        {
            EnsureButtonListenersBound();
        }

        private void OnDisable()
        {
            UnbindButtonListeners();

            // Safety: if prompt is active when disabled, force decline to prevent lock hang.
            if (pendingCallback != null)
                ForceCloseAsDecline();
        }

        private void Update()
        {
            if (pendingCallback == null) return;

            if (Time.time - promptStartTime >= timeoutSeconds)
            {
                Debug.LogWarning("[ReactionPrompt] Timeout reached. Auto-declining Shield Block.", this);
                CloseWithResult(false);
            }
        }

        /// <summary>
        /// Show the Shield Block prompt. If already active, force-closes previous as decline first.
        /// </summary>
        public void RequestShieldBlockPrompt(
            EntityHandle reactor,
            int incomingDamage,
            int shieldHp,
            int shieldMaxHp,
            Action<bool> onDecided)
        {
            if (onDecided == null)
            {
                Debug.LogError("[ReactionPrompt] onDecided callback is null. Ignoring request.", this);
                return;
            }

            // Guard: close previous prompt if somehow still active.
            if (pendingCallback != null)
            {
                Debug.LogWarning("[ReactionPrompt] Previous prompt still active. Force-closing as decline.", this);
                ForceCloseAsDecline();
            }

            pendingCallback = onDecided;
            promptStartTime = Time.time;

            // Robust against runtime/test setups that assign button refs after OnEnable.
            EnsureButtonListenersBound();

            if (titleText != null)
                titleText.text = "Shield Block?";

            if (bodyText != null)
                bodyText.text = $"Incoming damage: {incomingDamage}\nShield HP: {shieldHp} / {shieldMaxHp}";

            if (rootPanel != null)
                rootPanel.SetActive(true);
        }

        /// <summary>
        /// Force-close the prompt treating it as a decline. Safe to call even when no prompt is active.
        /// </summary>
        public void ForceCloseAsDecline()
        {
            if (pendingCallback == null) return;
            CloseWithResult(false);
        }

        private void OnYesClicked() => CloseWithResult(true);
        private void OnNoClicked() => CloseWithResult(false);

        private void CloseWithResult(bool accepted)
        {
            if (rootPanel != null)
                rootPanel.SetActive(false);

            var cb = pendingCallback;
            pendingCallback = null;
            cb?.Invoke(accepted);
        }

        private void EnsureButtonListenersBound()
        {
            if (ReferenceEquals(boundYesButton, yesButton) && ReferenceEquals(boundNoButton, noButton))
                return;

            UnbindButtonListeners();

            if (yesButton != null)
                yesButton.onClick.AddListener(OnYesClicked);
            if (noButton != null)
                noButton.onClick.AddListener(OnNoClicked);
            boundYesButton = yesButton;
            boundNoButton = noButton;
        }

        private void UnbindButtonListeners()
        {
            if (boundYesButton != null)
                boundYesButton.onClick.RemoveListener(OnYesClicked);
            if (boundNoButton != null)
                boundNoButton.onClick.RemoveListener(OnNoClicked);

            boundYesButton = null;
            boundNoButton = null;
        }
    }
}
