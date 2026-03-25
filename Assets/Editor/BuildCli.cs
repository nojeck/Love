#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildCli
{
    [MenuItem("LoveSim/Build/Windows64")]
    public static void BuildWindows64Menu()
    {
        BuildWindows64();
    }

    // Entry point for batch mode:
    // Unity.exe -batchmode -quit -projectPath <path> -executeMethod BuildCli.BuildWindows64
    public static void BuildWindows64()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new Exception("No enabled scenes in Build Settings.");
        }

        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var outDir = Path.Combine(projectRoot, "Builds", "Windows");
        Directory.CreateDirectory(outDir);

        var exePath = Path.Combine(outDir, "LoveSimulation_sample.exe");
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        Debug.Log("Starting Windows build...");
        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception("Build failed: " + report.summary.result + ", errors=" + report.summary.totalErrors);
        }

        Debug.Log("Build succeeded: " + exePath);
    }
}
#endif
