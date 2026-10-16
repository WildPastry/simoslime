using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SimoSlime.Editor
{
    /// <summary>
    /// Batchmode entry point for <c>tools/unity.ps1 -Task build</c>. Builds the enabled
    /// EditorBuildSettings scenes to <c>&lt;-buildOutput&gt;/SimoSlime.exe</c>, as a Development
    /// build when <c>-development</c> is passed. Any failure exits the editor with code 1.
    /// </summary>
    public static class BuildScript
    {
        public static void BuildWindows64()
        {
            try
            {
                ProjectConfig.Apply();

                string[] args = Environment.GetCommandLineArgs();
                string output = ArgValue(args, "-buildOutput");
                if (string.IsNullOrEmpty(output))
                {
                    throw new ArgumentException("-buildOutput <dir> is required");
                }

                bool development = args.Contains("-development");
                string[] scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray();
                if (scenes.Length == 0)
                {
                    throw new InvalidOperationException("EditorBuildSettings has no enabled scenes");
                }

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Path.Combine(output, "SimoSlime.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError($"Build {report.summary.result}: {report.SummarizeErrors()}");
                    EditorApplication.Exit(1);
                    return;
                }

                string kind = development ? "Development" : "Release";
                Debug.Log($"Built {kind} player at {options.locationPathName}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static string ArgValue(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
