using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PF2e.Core;

namespace PF2e.Presentation
{
    /// <summary>
    /// Event-driven presenter that tracks prepared Aid state per helper and updates a badge UI.
    /// </summary>
    public sealed class AidPreparedIndicatorPresenter
    {
        private readonly Dictionary<EntityHandle, int> preparedCountsByHelper = new();
        private readonly List<AidPreparedRecord> snapshotBuffer = new();

        public void Clear()
        {
            preparedCountsByHelper.Clear();
        }

        public void HandleAidPrepared(in AidPreparedEvent e)
        {
            if (!e.helper.IsValid)
                return;

            preparedCountsByHelper.TryGetValue(e.helper, out var currentCount);
            preparedCountsByHelper[e.helper] = Mathf.Max(0, currentCount) + 1;
        }

        public void HandleAidCleared(in AidClearedEvent e)
        {
            if (!e.helper.IsValid)
                return;

            if (!preparedCountsByHelper.TryGetValue(e.helper, out var currentCount))
                return;

            currentCount = Mathf.Max(0, currentCount - 1);
            if (currentCount <= 0)
                preparedCountsByHelper.Remove(e.helper);
            else
                preparedCountsByHelper[e.helper] = currentCount;
        }

        public void RebuildFromService(AidService aidService)
        {
            preparedCountsByHelper.Clear();
            if (aidService == null)
                return;

            snapshotBuffer.Clear();
            aidService.GetPreparedAidSnapshot(snapshotBuffer);
            for (int i = 0; i < snapshotBuffer.Count; i++)
            {
                var record = snapshotBuffer[i];
                if (!record.helper.IsValid)
                    continue;

                preparedCountsByHelper.TryGetValue(record.helper, out var currentCount);
                preparedCountsByHelper[record.helper] = Mathf.Max(0, currentCount) + 1;
            }

            snapshotBuffer.Clear();
        }

        public void RefreshForActor(
            EntityHandle actor,
            GameObject indicatorRoot,
            TMP_Text indicatorLabel,
            string singleText,
            string countFormat)
        {
            int count = 0;
            if (actor.IsValid)
                preparedCountsByHelper.TryGetValue(actor, out count);

            SetIndicator(indicatorRoot, indicatorLabel, count, singleText, countFormat);
        }

        private static void SetIndicator(
            GameObject indicatorRoot,
            TMP_Text indicatorLabel,
            int count,
            string singleText,
            string countFormat)
        {
            int normalized = Mathf.Max(0, count);
            bool show = normalized > 0;

            if (indicatorRoot != null && indicatorRoot.activeSelf != show)
                indicatorRoot.SetActive(show);

            if (indicatorLabel != null)
                indicatorLabel.text = FormatLabelText(normalized, singleText, countFormat);
        }

        public static string FormatLabelText(int count, string singleText, string countFormat)
        {
            if (count <= 0)
                return string.Empty;

            if (count == 1)
                return singleText ?? string.Empty;

            if (string.IsNullOrEmpty(countFormat))
                return count.ToString();

            try
            {
                return string.Format(countFormat, count);
            }
            catch (System.FormatException)
            {
                return count.ToString();
            }
        }
    }
}
