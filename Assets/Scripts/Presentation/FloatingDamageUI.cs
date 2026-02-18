using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PF2e.Core;
using PF2e.Managers;

namespace PF2e.Presentation
{
    /// <summary>
    /// Displays floating damage numbers above targets when a strike is resolved.
    /// Zero per-frame allocations: object pool + TMP.SetText for numeric display.
    /// </summary>
    public class FloatingDamageUI : MonoBehaviour
    {
        [Header("Dependencies (Inspector-only)")]
        [SerializeField] private CombatEventBus eventBus;
        [SerializeField] private EntityManager  entityManager;

        [Header("Optional Prefab")]
        [SerializeField] private TextMeshPro textPrefab;

        [Header("Placement")]
        [SerializeField] private float headOffset   = 1.5f;
        [SerializeField] private float jitterRadius = 0.25f;

        [Header("Animation")]
        [SerializeField] private float duration  = 1.2f;
        [SerializeField] private float liftSpeed = 1.2f;

        [Header("Display Rules")]
        [SerializeField] private bool   showMiss     = true;
        [SerializeField] private string missText     = "MISS";
        [SerializeField] private string critMissText = "CRIT MISS";

        private string missTextCached;
        private string critMissTextCached;

        [Header("Font Sizes")]
        [SerializeField] private float hitFontSize  = 3.0f;
        [SerializeField] private float critFontSize = 4.2f;
        [SerializeField] private float missFontSize = 2.2f;

        [Header("DamageType Colors")]
        [SerializeField] private Color slashingColor    = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color piercingColor    = new Color(1f, 0.55f, 0.25f, 1f);
        [SerializeField] private Color bludgeoningColor = new Color(1f, 1f,    1f,    1f);

        [Header("Overrides")]
        [SerializeField] private Color critTint      = new Color(1f, 0.75f, 0.2f,  1f);
        [SerializeField] private Color missColor     = new Color(0.65f, 0.65f, 0.65f, 1f);
        [SerializeField] private Color critMissColor = new Color(0.4f,  0.4f,  0.4f,  1f);
        [SerializeField] private Color zeroDamageColor = new Color(1f, 0.55f, 0.55f, 1f);

        // ---- Internal label type ----

        private sealed class Label
        {
            public GameObject  go;
            public TextMeshPro tmp;
            public Transform   follow;
            public Vector3     followOffset;
            public Vector3     startPos;
            public float       startTime;
            public Color       baseColor;
            public bool        isNumeric;
            public int         damageValue;
            public string      staticText;
        }

        private readonly Stack<Label> pool   = new Stack<Label>(16);
        private readonly List<Label>  active = new List<Label>(32);
        private Transform poolContainer;
        private UnityEngine.Camera cachedCamera;

        // ---- Unity lifecycle ----

        private void Awake()
        {
            var poolGo = new GameObject("FloatingDamagePool");
            poolGo.transform.SetParent(transform, false);
            poolContainer = poolGo.transform;

            missTextCached     = string.IsNullOrEmpty(missText)     ? "MISS"          : missText;
            critMissTextCached = string.IsNullOrEmpty(critMissText) ? missTextCached  : critMissText;
        }

        private void OnEnable()
        {
            if (eventBus == null || entityManager == null)
            {
                Debug.LogError("[FloatingDamageUI] Missing dependencies. Disabling.", this);
                enabled = false;
                return;
            }
            eventBus.OnStrikeResolved += HandleStrikeResolved;
        }

        private void OnDisable()
        {
            if (eventBus != null)
                eventBus.OnStrikeResolved -= HandleStrikeResolved;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i]?.go != null) Destroy(active[i].go);
            active.Clear();

            while (pool.Count > 0)
            {
                var l = pool.Pop();
                if (l?.go != null) Destroy(l.go);
            }
        }

        // ---- Color helper ----

        private Color GetDamageColor(DamageType type)
        {
            switch (type)
            {
                case DamageType.Slashing:    return slashingColor;
                case DamageType.Piercing:    return piercingColor;
                case DamageType.Bludgeoning: return bludgeoningColor;
                default:                     return bludgeoningColor;
            }
        }

        // ---- Event handler ----

        private void HandleStrikeResolved(in StrikeResolvedEvent e)
        {
            bool isHit  = e.degree == DegreeOfSuccess.Success || e.degree == DegreeOfSuccess.CriticalSuccess;
            bool isCrit = e.degree == DegreeOfSuccess.CriticalSuccess;
            bool isCritF= e.degree == DegreeOfSuccess.CriticalFailure;

            if (!isHit && !showMiss) return;

            Transform follow = null;
            Vector3   basePos;

            var view = entityManager.GetView(e.target);
            if (view != null)
            {
                follow  = view.transform;
                basePos = follow.position + Vector3.up * headOffset;
            }
            else
            {
                var data = entityManager.Registry?.Get(e.target);
                if (data == null) return;
                basePos = entityManager.GetEntityWorldPosition(data.GridPosition) + Vector3.up * headOffset;
            }

            Vector2 jitter   = Random.insideUnitCircle * jitterRadius;
            Vector3 spawnPos = basePos + new Vector3(jitter.x, 0f, jitter.y);

            Color color;
            float fontSize;

            if (isHit)
            {
                if (e.damage == 0)
                    color = zeroDamageColor;
                else if (isCrit)
                    color = critTint;
                else
                    color = GetDamageColor(e.damageType);

                fontSize = isCrit ? critFontSize : hitFontSize;
                Spawn(spawnPos, follow, true, e.damage, null, color, fontSize);
            }
            else
            {
                color    = isCritF ? critMissColor : missColor;
                fontSize = missFontSize;
                Spawn(spawnPos, follow, false, 0, isCritF ? critMissTextCached : missTextCached, color, fontSize);
            }
        }

        // ---- Pool + spawn ----

        private void Spawn(Vector3 pos, Transform follow, bool isNumeric, int damageValue, string staticText, Color color, float fontSize)
        {
            var label          = pool.Count > 0 ? pool.Pop() : CreateLabel();
            label.follow       = follow;
            label.followOffset = follow != null ? (pos - follow.position) : Vector3.zero;
            label.startPos     = pos;
            label.startTime    = Time.time;
            label.baseColor    = color;
            label.isNumeric    = isNumeric;
            label.damageValue  = damageValue;
            label.staticText   = staticText;

            label.go.transform.position = pos;
            label.tmp.fontSize = fontSize;
            label.tmp.color    = color;

            if (isNumeric)
                label.tmp.SetText("{0}", damageValue);
            else
                label.tmp.SetText(staticText);

            label.go.SetActive(true);
            active.Add(label);
        }

        private Label CreateLabel()
        {
            if (textPrefab != null)
            {
                var inst = Instantiate(textPrefab, poolContainer);
                inst.gameObject.name = "FloatingDamageLabel";
                inst.gameObject.SetActive(false);
                return new Label { go = inst.gameObject, tmp = inst };
            }

            var go  = new GameObject("FloatingDamageLabel");
            go.transform.SetParent(poolContainer, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.alignment          = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.richText           = false;
            tmp.sortingOrder       = 100;
            go.SetActive(false);
            return new Label { go = go, tmp = tmp };
        }

        // ---- LateUpdate animation (zero alloc per frame, billboard every frame) ----

        private void LateUpdate()
        {
            if (active.Count == 0) return;

            var   cam = MainCamera;
            float now = Time.time;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var l = active[i];
                float t = duration > 0.01f ? Mathf.Clamp01((now - l.startTime) / duration) : 1f;

                if (t >= 1f) { Recycle(i); continue; }

                Vector3 basePos = l.follow != null
                    ? l.follow.position + l.followOffset
                    : l.startPos;
                Vector3 pos = basePos + Vector3.up * (liftSpeed * (now - l.startTime));
                l.go.transform.position = pos;

                var c = l.baseColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                l.tmp.color = c;

                if (cam != null)
                {
                    Vector3 dir = pos - cam.transform.position;
                    if (dir.sqrMagnitude > 0.0001f)
                        l.go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                }
            }
        }

        private UnityEngine.Camera MainCamera
        {
            get
            {
                if (cachedCamera == null) cachedCamera = UnityEngine.Camera.main;
                return cachedCamera;
            }
        }

        private void Recycle(int idx)
        {
            var l = active[idx];
            active.RemoveAt(idx);
            if (l?.go == null) return;
            l.go.SetActive(false);
            l.follow     = null;
            l.staticText = null;
            pool.Push(l);
        }
    }
}
