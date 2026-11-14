using UnityEditor;
using UnityEngine;

// Builds the game from the command line (used to record the trailer and for the event launcher):
//   Unity -batchmode -quit -projectPath . -executeMethod TrailerBuild.BuildMac -buildPath Builds/AgentTR8N1.app
//   Unity -batchmode -quit -projectPath . -executeMethod TrailerBuild.BuildWindows -buildPath Builds/Windows/AgentTR8N1.exe
//   Unity -batchmode -quit -projectPath . -executeMethod TrailerBuild.BuildWebGL -buildPath Builds/WebGL
// (Windows needs the "Windows Build Support (Mono)" module)
public static class TrailerBuild
{
    [MenuItem("Tools/Build macOS (trailer)")]
    public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/AgentTR8N1.app");

    [MenuItem("Tools/Build Windows")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/AgentTR8N1.exe");

    [MenuItem("Tools/Build WebGL (itch.io)")]
    public static void BuildWebGL()
    {
        // itch.io doesn't send the Content-Encoding header for Unity's compressed files:
        // with the decompression fallback the build loads anyway
        PlayerSettings.WebGL.decompressionFallback = true;
        Build(BuildTarget.WebGL, "Builds/WebGL");
    }

    private static void Build(BuildTarget target, string path)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-buildPath") path = args[i + 1];

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = path,
            target = target,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"Build {report.summary.result}: {path}");
        if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
