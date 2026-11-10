using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Opens art in Aseprite from inside Unity: right-click a PNG (or .ase/.aseprite) in the Project window
// > Open in Aseprite, or Tools > Agent TR8N1 > Open blood splatters in Aseprite. Save in Aseprite and
// come back to Unity: it reimports the file by itself. Aseprite is found in the usual places (App
// Store / direct download / Steam / the Desktop); if it's somewhere else, you pick it once and it's remembered.
public static class AsepriteOpener
{
    private const string PrefKey = "AgentTR8N1.AsepritePath";

    private static readonly string[] Candidates =
    {
        "/Applications/Aseprite.app",
        "~/Applications/Aseprite.app",
        "~/Desktop/Aseprite.app",
        "~/Library/Application Support/Steam/steamapps/common/Aseprite/Aseprite.app",
        "/Applications/LibreSprite.app",
    };

    [MenuItem("Assets/Open in Aseprite", false, 19)]
    private static void OpenSelection()
    {
        List<string> files = new List<string>();
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (IsArt(path)) files.Add(path);
        }
        Open(files);
    }

    [MenuItem("Assets/Open in Aseprite", true)]
    private static bool CanOpenSelection()
    {
        foreach (Object obj in Selection.objects)
            if (IsArt(AssetDatabase.GetAssetPath(obj))) return true;
        return false;
    }

    [MenuItem("Tools/Agent TR8N1/Open blood splatters in Aseprite")]
    private static void OpenBloodSplatters()
    {
        List<string> files = new List<string>();
        for (int i = 0; i < GameSprites.BloodSplashCount; i++)
        {
            string path = $"Assets/Resources/{GameSprites.Folder}/bloodsplash{i}.png";
            if (File.Exists(path)) files.Add(path);
        }
        if (files.Count == 0)
        {
            EditorUtility.DisplayDialog("No blood splatters yet", "Run Tools > Agent TR8N1 > Bake missing sprites first.", "OK");
            return;
        }
        Open(files);
    }

    [MenuItem("Tools/Agent TR8N1/Choose Aseprite app...")]
    private static void ChooseApp()
    {
        string picked = EditorUtility.OpenFilePanel("Where is Aseprite?", "/Applications", "app");
        if (!string.IsNullOrEmpty(picked)) EditorPrefs.SetString(PrefKey, picked);
    }

    private static void Open(List<string> assetPaths)
    {
        string app = FindApp();
        if (app == null) return;
        foreach (string assetPath in assetPaths)
        {
            string full = Path.GetFullPath(assetPath);
            System.Diagnostics.Process.Start("open", $"-a \"{app}\" \"{full}\"");
        }
    }

    private static string FindApp()
    {
        string saved = EditorPrefs.GetString(PrefKey, "");
        if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved)) return saved;

        string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        foreach (string candidate in Candidates)
        {
            string path = candidate.StartsWith("~") ? home + candidate.Substring(1) : candidate;
            if (Directory.Exists(path)) return path;
        }

        if (EditorUtility.DisplayDialog("Aseprite not found",
                "Aseprite isn't in /Applications or Steam. Pick where it is (it's remembered).", "Choose...", "Cancel"))
        {
            ChooseApp();
            saved = EditorPrefs.GetString(PrefKey, "");
            if (Directory.Exists(saved)) return saved;
        }
        return null;
    }

    private static bool IsArt(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".png" || ext == ".ase" || ext == ".aseprite";
    }
}
