using UnityEditor;
using UnityEngine;

// Builds the game for macOS from the command line (used to record the trailer):
//   Unity -batchmode -quit -projectPath . -executeMethod TrailerBuild.BuildMac -buildPath Builds/AgentTR8N1.app
public static class TrailerBuild
{
    [MenuItem("Tools/Build macOS (trailer)")]
    public static void BuildMac()
    {
        string path = "Builds/AgentTR8N1.app";
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-buildPath") path = args[i + 1];

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = path,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"Build {report.summary.result}: {path}");
        if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
