using System;
using System.Collections.Generic;
using PF2e.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PF2e.Presentation
{
    /// <summary>
    /// Owns Delay placement insertion marker pooling, event wiring, and overlay repositioning.
    /// InitiativeBarController decides which anchors are legal; this presenter only renders/dispatches marker interactions.
    /// </summary>
    internal sealed class DelayPlacementMarkerOverlayPresenter
    {
        private readonly List<InitiativeInsertionMarker> activeMarkers = new List<InitiativeInsertionMarker>(32);
        private readonly Stack<InitiativeInsertionMarker> markerPool = new Stack<InitiativeInsertionMarker>(32);
        private bool markersDirty;

        public event Action<EntityHandle> OnMarkerClicked;
        public event Action<EntityHandle> OnMarkerHoverEntered;
        public event Action<EntityHandle> OnMarkerHoverExited;

        public bool HasActiveMarkers => activeMarkers.Count > 0;
        public bool IsDirty => markersDirty;

        public void ClearToPool()
        {
            for (int i = 0; i < activeMarkers.Count; i++)
            {
                var marker = activeMarkers[i];
                if (marker == null) continue;

                marker.OnClicked -= HandleMarkerClicked;
                marker.OnHoverEntered -= HandleMarkerHoverEntered;
                marker.OnHoverExited -= HandleMarkerHoverExited;
                marker.gameObject.SetActive(false);
                markerPool.Push(marker);
            }

            activeMarkers.Clear();
            markersDirty = false;
        }

        public void AddMarker(
            EntityHandle anchorHandle,
            bool canSelect,
            Transform overlayParent,
            InitiativeInsertionMarker markerPrefab)
        {
            if (!anchorHandle.IsValid || overlayParent == null)
                return;

            var marker = GetMarker(overlayParent, markerPrefab);
            marker.transform.SetParent(overlayParent, false);
            marker.gameObject.SetActive(true);
            marker.OnClicked -= HandleMarkerClicked;
            marker.OnClicked += HandleMarkerClicked;
            marker.OnHoverEntered -= HandleMarkerHoverEntered;
            marker.OnHoverEntered += HandleMarkerHoverEntered;
            marker.OnHoverExited -= HandleMarkerHoverExited;
            marker.OnHoverExited += HandleMarkerHoverExited;
            marker.Setup(anchorHandle, canSelect);
            activeMarkers.Add(marker);
        }

        public void RepositionMarkers(
            RectTransform overlayRect,
            Transform slotsContainer,
            Dictionary<EntityHandle, InitiativeSlot> slotByHandle)
        {
            if (activeMarkers.Count == 0)
            {
                markersDirty = false;
                return;
            }
            if (overlayRect == null || slotsContainer == null || slotByHandle == null)
                return;

            float spacing = 0f;
            if (slotsContainer.TryGetComponent<HorizontalLayoutGroup>(out var h))
                spacing = h.spacing;

            for (int i = 0; i < activeMarkers.Count; i++)
            {
                var marker = activeMarkers[i];
                if (marker == null) continue;
                if (!slotByHandle.TryGetValue(marker.AnchorHandle, out var anchorSlot) || anchorSlot == null)
                    continue;

                var anchorRect = anchorSlot.transform as RectTransform;
                if (anchorRect == null)
                    continue;

                var rightCenterWorld = anchorRect.TransformPoint(
                    new Vector3(anchorRect.rect.xMax + (spacing * 0.5f), anchorRect.rect.center.y, 0f));
                var localPoint = overlayRect.InverseTransformPoint(rightCenterWorld);
                marker.SetOverlayPlacement(localPoint.x);
            }

            markersDirty = false;
        }

        public void MarkDirtyIfAny()
        {
            markersDirty = activeMarkers.Count > 0;
        }

        public void ClearDirty()
        {
            markersDirty = false;
        }

        private InitiativeInsertionMarker GetMarker(Transform overlayParent, InitiativeInsertionMarker markerPrefab)
        {
            if (markerPool.Count > 0)
                return markerPool.Pop();

            InitiativeInsertionMarker inst;
            if (markerPrefab != null)
            {
                inst = UnityEngine.Object.Instantiate(markerPrefab, overlayParent);
            }
            else
            {
                var go = new GameObject(
                    "InitiativeInsertionMarker",
                    typeof(RectTransform),
                    typeof(LayoutElement),
                    typeof(Image),
                    typeof(InitiativeInsertionMarker));
                go.transform.SetParent(overlayParent, false);
                inst = go.GetComponent<InitiativeInsertionMarker>();
            }

            inst.gameObject.SetActive(false);
            return inst;
        }

        private void HandleMarkerClicked(InitiativeInsertionMarker marker)
        {
            if (marker == null) return;
            OnMarkerClicked?.Invoke(marker.AnchorHandle);
        }

        private void HandleMarkerHoverEntered(InitiativeInsertionMarker marker)
        {
            if (marker == null) return;
            OnMarkerHoverEntered?.Invoke(marker.AnchorHandle);
        }

        private void HandleMarkerHoverExited(InitiativeInsertionMarker marker)
        {
            if (marker == null) return;
            OnMarkerHoverExited?.Invoke(marker.AnchorHandle);
        }
    }
}
