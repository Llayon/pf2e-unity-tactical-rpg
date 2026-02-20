using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PF2e.Core;
using PF2e.Data;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Productized encounter flow controls.
    /// Primary path: Start/End Encounter buttons.
    /// Authoring-first: buttons should be wired in scene.
    /// Optional runtime UI creation remains as a development fallback.
    /// </summary>
    public class EncounterFlowController : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private Canvas rootCanvas;

        [Header("Preset")]
        [SerializeField] private bool useFlowPreset = false;
        [SerializeField] private EncounterFlowUIPreset flowPreset;

        [Header("UI")]
        [SerializeField] private Button startEncounterButton;
        [SerializeField] private Button endEncounterButton;
        [SerializeField] private RectTransform encounterFlowPanelPrefab;
        [SerializeField] private bool autoCreateRuntimeButtons = false;

        private RectTransform runtimePanel;

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyFlowPresetIfEnabled();

            if (turnManager == null) Debug.LogError("[EncounterFlow] Missing TurnManager", this);
            if (entityManager == null) Debug.LogError("[EncounterFlow] Missing EntityManager", this);
            if (rootCanvas == null) Debug.LogError("[EncounterFlow] Missing root Canvas", this);
            if (useFlowPreset && flowPreset == null)
                Debug.LogWarning("[EncounterFlow] useFlowPreset is enabled but flowPreset is not assigned.", this);

            if (!autoCreateRuntimeButtons)
            {
                if (startEncounterButton == null)
                    Debug.LogWarning("[EncounterFlow] startEncounterButton is not assigned (authoring mode).", this);
                if (endEncounterButton == null)
                    Debug.LogWarning("[EncounterFlow] endEncounterButton is not assigned (authoring mode).", this);
            }
            else if (encounterFlowPanelPrefab == null)
            {
                Debug.LogWarning("[EncounterFlow] autoCreateRuntimeButtons is enabled without encounterFlowPanelPrefab. Falling back to generated runtime panel.", this);
            }
        }
#endif

        private void OnEnable()
        {
            ApplyFlowPresetIfEnabled();

            if (turnManager == null || entityManager == null || rootCanvas == null)
            {
                Debug.LogError("[EncounterFlow] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }

            EnsureButtons();
            if (startEncounterButton == null || endEncounterButton == null)
            {
                Debug.LogError("[EncounterFlow] Missing encounter flow buttons. Assign in Inspector or enable runtime creation.", this);
                enabled = false;
                return;
            }

            startEncounterButton.onClick.AddListener(OnStartEncounterClicked);
            endEncounterButton.onClick.AddListener(OnEndEncounterClicked);

            turnManager.OnCombatStarted += HandleCombatStarted;
            turnManager.OnCombatEndedWithResult += HandleCombatEnded;

            RefreshButtons();
        }

        private void Update()
        {
            // Team availability can change during scene startup before any combat events fire.
            RefreshButtons();
        }

        private void OnDisable()
        {
            if (startEncounterButton != null)
                startEncounterButton.onClick.RemoveListener(OnStartEncounterClicked);
            if (endEncounterButton != null)
                endEncounterButton.onClick.RemoveListener(OnEndEncounterClicked);

            if (turnManager != null)
            {
                turnManager.OnCombatStarted -= HandleCombatStarted;
                turnManager.OnCombatEndedWithResult -= HandleCombatEnded;
            }
        }

        private void OnStartEncounterClicked()
        {
            if (!CanStartEncounter())
            {
                RefreshButtons();
                return;
            }

            turnManager.StartCombat();
            RefreshButtons();
        }

        private void OnEndEncounterClicked()
        {
            if (!CanEndEncounter())
            {
                RefreshButtons();
                return;
            }

            turnManager.EndCombat(EncounterResult.Aborted);
            RefreshButtons();
        }

        private void HandleCombatStarted()
        {
            RefreshButtons();
        }

        private void HandleCombatEnded(EncounterResult _)
        {
            RefreshButtons();
        }

        private bool CanStartEncounter()
        {
            if (turnManager == null || entityManager == null || entityManager.Registry == null)
                return false;

            if (turnManager.State != TurnState.Inactive)
                return false;

            return HasLivingTeam(Team.Player) && HasLivingTeam(Team.Enemy);
        }

        private bool CanEndEncounter()
        {
            return turnManager != null && turnManager.State != TurnState.Inactive;
        }

        private bool HasLivingTeam(Team team)
        {
            foreach (var data in entityManager.Registry.GetAll())
            {
                if (data == null || !data.IsAlive) continue;
                if (data.Team == team) return true;
            }
            return false;
        }

        private void RefreshButtons()
        {
            if (startEncounterButton != null)
                startEncounterButton.interactable = CanStartEncounter();

            if (endEncounterButton != null)
                endEncounterButton.interactable = CanEndEncounter();
        }

        private void EnsureButtons()
        {
            if (startEncounterButton != null && endEncounterButton != null)
                return;

            if (!autoCreateRuntimeButtons)
                return;

            CreateOrReuseRuntimePanel();

            if (startEncounterButton == null)
                startEncounterButton = FindOrCreateButton("StartEncounterButton", "Start Encounter");

            if (endEncounterButton == null)
                endEncounterButton = FindOrCreateButton("EndEncounterButton", "End Encounter");
        }

        private void CreateOrReuseRuntimePanel()
        {
            if (runtimePanel != null)
                return;

            var existing = rootCanvas.transform.Find("EncounterFlowPanel") as RectTransform;
            if (existing != null)
            {
                runtimePanel = existing;
                TryWireButtonsFromRuntimePanel();
                return;
            }

            if (encounterFlowPanelPrefab != null)
            {
                runtimePanel = Instantiate(encounterFlowPanelPrefab, rootCanvas.transform, false);
                runtimePanel.name = encounterFlowPanelPrefab.name;
                TryWireButtonsFromRuntimePanel();

                if (startEncounterButton != null && endEncounterButton != null)
                    return;

                Debug.LogWarning("[EncounterFlow] Prefab panel is missing StartEncounterButton/EndEncounterButton. Falling back to generated runtime panel.", this);
                Destroy(runtimePanel.gameObject);
                runtimePanel = null;
            }

            var panelGo = new GameObject("EncounterFlowPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            runtimePanel = panelGo.GetComponent<RectTransform>();
            runtimePanel.SetParent(rootCanvas.transform, false);
            runtimePanel.anchorMin = new Vector2(1f, 1f);
            runtimePanel.anchorMax = new Vector2(1f, 1f);
            runtimePanel.pivot = new Vector2(1f, 1f);
            runtimePanel.anchoredPosition = new Vector2(-16f, -16f);
            runtimePanel.sizeDelta = new Vector2(0f, 44f);

            var image = panelGo.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.35f);

            var layout = panelGo.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = panelGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void TryWireButtonsFromRuntimePanel()
        {
            if (runtimePanel == null)
                return;

            if (startEncounterButton == null)
                startEncounterButton = runtimePanel.Find("StartEncounterButton")?.GetComponent<Button>();

            if (endEncounterButton == null)
                endEncounterButton = runtimePanel.Find("EndEncounterButton")?.GetComponent<Button>();
        }

        private void ApplyFlowPresetIfEnabled()
        {
            if (!useFlowPreset || flowPreset == null)
                return;

            autoCreateRuntimeButtons = flowPreset.autoCreateRuntimeButtons;
            encounterFlowPanelPrefab = flowPreset.encounterFlowPanelPrefab;
        }

        private Button FindOrCreateButton(string name, string label)
        {
            var existing = runtimePanel.Find(name);
            if (existing != null)
                return existing.GetComponent<Button>();

            var buttonGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rect = buttonGo.GetComponent<RectTransform>();
            rect.SetParent(runtimePanel, false);
            rect.sizeDelta = new Vector2(180f, 36f);

            var image = buttonGo.GetComponent<Image>();
            image.color = new Color(0.18f, 0.23f, 0.3f, 0.92f);

            var layout = buttonGo.GetComponent<LayoutElement>();
            layout.minWidth = 180f;
            layout.minHeight = 36f;
            layout.preferredWidth = 180f;
            layout.preferredHeight = 36f;

            var labelGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;

            return buttonGo.GetComponent<Button>();
        }
    }
}
