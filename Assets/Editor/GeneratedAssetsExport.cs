using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Copies the game's art (the PNGs in Assets/Resources/Sprites) and the sound effects generated in code
// to a folder for the artist. Each image is saved at its real size and at 8x (pixel-perfect).
//   Unity -batchmode -quit -projectPath . -executeMethod GeneratedAssetsExport.Export -exportPath Docs/GeneratedAssets
public static class GeneratedAssetsExport
{
    private const int Zoom = 8;

    [MenuItem("Tools/Export generated assets")]
    public static void Export()
    {
        string folder = "Docs/GeneratedAssets";
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-exportPath") folder = args[i + 1];

        List<string> lines = new List<string> { "file\tsize\tsource" };

        // The baked PNGs (the game's real art): a copy at real size plus an 8x enlargement
        foreach ((string from, string to) in new[] { (ArtBaker.SpriteDir, "sprites"), (ArtBaker.WalkDir, "walk-backgrounds") })
        {
            foreach (string png in Directory.GetFiles(from, "*.png"))
            {
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(png));
                string name = Path.GetFileNameWithoutExtension(png);
                Save(tex, Path.Combine(folder, to), name);
                lines.Add($"{to}/{name}.png\t{tex.width}x{tex.height}\t{png.Replace('\\', '/')}");
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        RuntimeSounds.ExportAll(Path.Combine(folder, "sfx"));
        foreach (Sfx id in Enum.GetValues(typeof(Sfx)))
            lines.Add($"sfx/{id}.wav\t-\tRuntimeSounds.Samples(Sfx.{id})");

        File.WriteAllLines(Path.Combine(folder, "index.tsv"), lines);
        Debug.Log($"Exported {lines.Count - 1} generated assets to {folder}");
    }

    private static void Save(Texture2D tex, string folder, string name)
    {
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder, "x8"));
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());

        // Pixel-perfect enlargement (each pixel becomes an 8x8 block), easier to paint over
        int w = tex.width, h = tex.height;
        if (w * Zoom > 4096 || h * Zoom > 4096) return;
        Color32[] src = tex.GetPixels32();
        Color32[] dst = new Color32[w * Zoom * h * Zoom];
        for (int y = 0; y < h * Zoom; y++)
            for (int x = 0; x < w * Zoom; x++)
                dst[y * w * Zoom + x] = src[(y / Zoom) * w + x / Zoom];
        Texture2D big = new Texture2D(w * Zoom, h * Zoom, TextureFormat.RGBA32, false);
        big.SetPixels32(dst);
        big.Apply();
        File.WriteAllBytes(Path.Combine(folder, "x8", name + ".png"), big.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(big);
    }
}
