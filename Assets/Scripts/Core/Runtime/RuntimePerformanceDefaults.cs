using UnityEngine;

namespace PF2e.Core.Runtime
{
    /// <summary>
    /// Applies conservative runtime performance defaults to avoid uncapped FPS
    /// in lightweight scenes (common source of laptop fan spikes in PlayMode).
    /// </summary>
    public static class RuntimePerformanceDefaults
    {
        private const int DefaultTargetFps = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            // Use explicit frame cap instead of display refresh (120/144/240 Hz laptops).
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = DefaultTargetFps;
        }
    }
}

