using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PF2e.Editor
{
    public static class BuildPlayerCommand
    {
        private const string DefaultWindowsBuildName = "PF2e Tactical RPG";
        private const string WindowsOutputArg = "-buildOutput";

        [MenuItem("Tools/PF2e/Build/Windows x64 Player")]
        public static void BuildWindows64Menu()
        {
            string outputPath = GetDefaultWindowsExecutablePath();
            BuildWindows64Internal(outputPath);
        }

        // Unity batchmode:
        // Unity.exe -batchmode -projectPath "<path>" -executeMethod PF2e.Editor.BuildPlayerCommand.BuildWindows64Batch -buildOutput "<exe path>" -quit
        public static void BuildWindows64Batch()
        {
            string outputPath = GetCommandLineValue(WindowsOutputArg);
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = GetDefaultWindowsExecutablePath();

            BuildWindows64Internal(outputPath);
        }

        private static void BuildWindows64Internal(string outputPath)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes found in EditorBuildSettings.");

            string normalizedOutputPath = Path.GetFullPath(outputPath);
            string outputDirectory = Path.GetDirectoryName(normalizedOutputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new InvalidOperationException($"Invalid build output path: {outputPath}");

            Directory.CreateDirectory(outputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = normalizedOutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log($"[BuildPlayerCommand] Building Windows x64 player to '{normalizedOutputPath}'");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"Windows x64 build failed. Result={summary.result}, Errors={summary.totalErrors}, Warnings={summary.totalWarnings}");

            Debug.Log(
                $"[BuildPlayerCommand] Build succeeded. Output='{summary.outputPath}', Size={summary.totalSize} bytes, Duration={summary.totalTime.TotalSeconds:F1}s");
        }

        private static string GetDefaultWindowsExecutablePath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Builds", "Windows", $"{DefaultWindowsBuildName}.exe");
        }

        private static string GetCommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    continue;

                return args[i + 1];
            }

            return null;
        }
    }
}
