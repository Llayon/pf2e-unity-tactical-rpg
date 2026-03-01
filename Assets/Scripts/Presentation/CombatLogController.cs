using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PF2e.Core;
using PF2e.Managers;
using System.Collections.Generic;

namespace PF2e.Presentation
{
    /// <summary>
    /// Phase 10.2: Bus-only Combat Log (scrollable), presentation-only.
    /// Subscribes ONLY to CombatEventBus (not TurnManager directly).
    /// Uses pooled TMP_Text lines.
    /// Auto-scroll is deferred via Canvas.willRenderCanvases (no coroutine, no ForceUpdateCanvases, no alloc).
    /// </summary>
    public class CombatLogController : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private CombatEventBus eventBus;

        [Header("UI (Inspector-only)")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private TextMeshProUGUI lineTemplate;

        [Header("Behavior")]
        [SerializeField] private bool hideWhenNotInCombat = true;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool autoScrollToBottom = true;
        [SerializeField] private int maxLines = 300;
        [SerializeField] private bool showRetentionNotice = true;
        [SerializeField] private TextMeshProUGUI retentionNoticeLabel;

        [Header("Formatting")]
        [SerializeField] private bool showLineNumbers = true;
        [SerializeField] private bool showTimestamps = true;

        private readonly Queue<TextMeshProUGUI> activeLines = new Queue<TextMeshProUGUI>(128);
        private readonly Stack<TextMeshProUGUI> pooledLines = new Stack<TextMeshProUGUI>(128);
        private readonly StringBuilder sb = new StringBuilder(256);

        private int lineCounter = 0;
        private float combatStartTime = -1f;
        private bool scrollPending;
        private bool inCombat;
        private float cachedMinLineHeight = 0f;
        private const int LegacyDefaultMaxLines = 80;
        private const int CurrentDefaultMaxLines = 300;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entityManager == null) Debug.LogError("[CombatLog] Missing reference: EntityManager", this);
            if (eventBus == null) Debug.LogError("[CombatLog] Missing reference: CombatEventBus", this);
            if (scrollRect == null) Debug.LogWarning("[CombatLog] scrollRect not assigned", this);
            if (content == null) Debug.LogWarning("[CombatLog] content not assigned", this);
            if (lineTemplate == null) Debug.LogWarning("[CombatLog] lineTemplate not assigned", this);
        }
#endif

        private void OnEnable()
        {
            if (entityManager == null || eventBus == null || scrollRect == null || content == null || lineTemplate == null)
            {
                Debug.LogError("[CombatLog] Missing core dependencies. Disabling CombatLogController.", this);
                enabled = false;
                return;
            }

            if (hideWhenNotInCombat && canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    Debug.LogWarning("[CombatLog] hideWhenNotInCombat enabled but CanvasGroup not assigned. Log will remain visible.", this);
            }

            if (lineTemplate.gameObject.activeSelf)
                lineTemplate.gameObject.SetActive(false);

            MigrateLegacyMaxLinesDefault();
            CacheTemplateLineHeight();
            EnsureRetentionNoticeLabel();
            UpdateRetentionNoticeLabelText();
            RefreshRetentionNoticeVisibility();

            eventBus.OnLogEntry += HandleBusLogEntry;
            Canvas.willRenderCanvases += HandleWillRenderCanvases;

            // Hide initially
            SetInCombat(false);
        }

        private void OnDisable()
        {
            if (eventBus != null)
                eventBus.OnLogEntry -= HandleBusLogEntry;

            Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        }

        private void OnDestroy()
        {
            // Protection against static event leak (Play Mode exit without OnDisable)
            Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        }

        private void HandleBusLogEntry(CombatLogEntry entry)
        {
            // Handle combat start/end by category (not by string matching)
            if (entry.Category == CombatLogCategory.CombatStart)
            {
                ClearLog();
                combatStartTime = Time.time;
                lineCounter = 0;
                SetInCombat(true);
            }
            else if (entry.Category == CombatLogCategory.CombatEnd)
            {
                SetInCombat(false);
            }

            // Add line to log
            if (entry.Actor.IsValid)
            {
                var name = ResolveName(entry.Actor);
                AddLineRaw($"{name}: {entry.Message}");
            }
            else
            {
                AddLineRaw(entry.Message);
            }
        }

        private void AddLineRaw(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            while (activeLines.Count >= Mathf.Max(1, maxLines))
                RecycleOldestLine();

            var line = GetLineInstance();
            EnsureLineIsLastChild(line);
            line.text = FormatLine(text);
            RefreshLinePreferredHeight(line);
            line.gameObject.SetActive(true);

            activeLines.Enqueue(line);

            if (autoScrollToBottom)
                RequestScrollToBottom();
        }

        private string FormatLine(string text)
        {
            lineCounter++;
            sb.Clear();

            if (showLineNumbers)
            {
                sb.Append('#');
                sb.Append(lineCounter.ToString("0000"));
                sb.Append(' ');
            }

            if (showTimestamps)
            {
                float t = (combatStartTime > 0f) ? (Time.time - combatStartTime) : 0f;
                sb.Append("[+");
                sb.Append(t.ToString("0.0"));
                sb.Append("s] ");
            }

            sb.Append(text);
            return sb.ToString();
        }

        private TextMeshProUGUI GetLineInstance()
        {
            if (pooledLines.Count > 0)
                return pooledLines.Pop();

            var inst = Instantiate(lineTemplate, content);
            inst.gameObject.name = "CombatLogLine";
            inst.gameObject.SetActive(false);
            return inst;
        }

        private void EnsureLineIsLastChild(TextMeshProUGUI line)
        {
            if (line == null || content == null)
                return;

            var lineTransform = line.transform;
            if (lineTransform.parent != content)
                lineTransform.SetParent(content, false);

            lineTransform.SetAsLastSibling();
        }

        private void CacheTemplateLineHeight()
        {
            cachedMinLineHeight = 0f;
            if (lineTemplate == null)
                return;

            if (lineTemplate.TryGetComponent<LayoutElement>(out var templateLayout))
            {
                if (templateLayout.minHeight > 0f)
                    cachedMinLineHeight = Mathf.Max(cachedMinLineHeight, templateLayout.minHeight);
                if (templateLayout.preferredHeight > 0f)
                    cachedMinLineHeight = Mathf.Max(cachedMinLineHeight, templateLayout.preferredHeight);
            }

            if (cachedMinLineHeight <= 0f)
                cachedMinLineHeight = 24f;
        }

        private void MigrateLegacyMaxLinesDefault()
        {
            // Scene instances serialized with the previous default (80) are upgraded
            // to the current baseline (300) unless authoring explicitly changed the value.
            if (maxLines == LegacyDefaultMaxLines)
            {
                maxLines = CurrentDefaultMaxLines;
            }
        }

        private void RefreshLinePreferredHeight(TextMeshProUGUI line)
        {
            if (line == null)
                return;
            if (!line.TryGetComponent<LayoutElement>(out var lineLayout))
                return;

            float availableWidth = GetLineAvailableWidth();
            if (availableWidth <= 1f)
            {
                lineLayout.preferredHeight = cachedMinLineHeight;
                return;
            }

            // Keep wrapping behavior, but let line height grow to the TMP preferred height.
            Vector2 preferred = line.GetPreferredValues(line.text, availableWidth, 0f);
            float targetHeight = Mathf.Max(cachedMinLineHeight, Mathf.Ceil(preferred.y));
            if (Mathf.Abs(lineLayout.preferredHeight - targetHeight) > 0.5f)
                lineLayout.preferredHeight = targetHeight;
        }

        private float GetLineAvailableWidth()
        {
            if (content == null)
                return 0f;

            float width = content.rect.width;
            if (width <= 0f)
                return 0f;

            if (content.TryGetComponent<VerticalLayoutGroup>(out var vlg))
                width -= (vlg.padding.left + vlg.padding.right);

            return Mathf.Max(0f, width);
        }

        private void RecycleOldestLine()
        {
            if (activeLines.Count == 0) return;

            var old = activeLines.Dequeue();
            if (old == null) return;

            old.text = string.Empty;
            old.gameObject.SetActive(false);
            pooledLines.Push(old);
        }

        private void ClearLog()
        {
            while (activeLines.Count > 0)
                RecycleOldestLine();
        }

        private void RequestScrollToBottom()
        {
            scrollPending = true;
        }

        private void HandleWillRenderCanvases()
        {
            if (!scrollPending) return;
            scrollPending = false;

            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }

        private void EnsureRetentionNoticeLabel()
        {
            if (!showRetentionNotice || retentionNoticeLabel != null || lineTemplate == null)
                return;

            var inst = Instantiate(lineTemplate, transform);
            inst.gameObject.name = "CombatLogRetentionNotice";
            inst.gameObject.SetActive(true);
            inst.raycastTarget = false;
            inst.enableWordWrapping = false;
            inst.overflowMode = TextOverflowModes.Ellipsis;
            inst.alignment = TextAlignmentOptions.TopRight;
            inst.fontStyle = FontStyles.Italic;
            inst.fontSize = Mathf.Max(12f, lineTemplate.fontSize * 0.75f);
            inst.color = new Color(0.95f, 0.93f, 0.86f, 0.72f);

            if (inst.TryGetComponent<LayoutElement>(out var le))
                le.enabled = false;

            var rect = inst.rectTransform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-10f, -10f);
            rect.sizeDelta = new Vector2(260f, 20f);

            retentionNoticeLabel = inst;
        }

        private void UpdateRetentionNoticeLabelText()
        {
            if (retentionNoticeLabel == null)
                return;

            retentionNoticeLabel.SetText("Showing last {0} lines", Mathf.Max(1, maxLines));
        }

        private void RefreshRetentionNoticeVisibility()
        {
            if (retentionNoticeLabel == null)
                return;

            retentionNoticeLabel.gameObject.SetActive(showRetentionNotice && inCombat && Mathf.Max(1, maxLines) > 0);
        }

        private string ResolveName(EntityHandle handle)
        {
            if (entityManager == null || entityManager.Registry == null)
                return handle.ToString();

            var data = entityManager.Registry.Get(handle);
            return data?.Name ?? handle.ToString();
        }

        private void SetInCombat(bool value)
        {
            inCombat = value;
            RefreshVisibility();
            RefreshRetentionNoticeVisibility();
        }

        private void RefreshVisibility()
        {
            if (!hideWhenNotInCombat || canvasGroup == null) return;

            canvasGroup.alpha = inCombat ? 1f : 0f;
            canvasGroup.blocksRaycasts = inCombat;
            canvasGroup.interactable = inCombat;
        }
    }
}
