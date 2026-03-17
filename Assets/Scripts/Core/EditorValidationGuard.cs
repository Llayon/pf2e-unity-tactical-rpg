#if UNITY_EDITOR
using System.Diagnostics;
using UnityEditor;

namespace PF2e.Core
{
    internal static class EditorValidationGuard
    {
        public static bool ShouldSkipMissingReferenceWarnings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return true;

            var stackTrace = new StackTrace();
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                var method = stackTrace.GetFrame(i)?.GetMethod();
                var type = method?.DeclaringType;
                string fullName = type?.FullName ?? string.Empty;

                if (fullName.Contains("UnityEditor.TestRunner")
                    || fullName.Contains("UnityEditor.TestTools")
                    || fullName.Contains("UnityEngine.TestTools")
                    || fullName.Contains("NUnit.Framework")
                    || fullName.Contains("PF2e.Tests"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
