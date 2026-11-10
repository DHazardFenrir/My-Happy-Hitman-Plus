using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Turns the art painted in code (SpritePainter, WalkThemePainter) into PNG files the game loads
// from Assets/Resources/Sprites. Existing PNGs are never overwritten (they may have been repainted
// by hand): delete one and bake again to get the original back.
//   Unity -batchmode -quit -projectPath . -executeMethod ArtBaker.MigrateAll
public static class ArtBaker
{
    public const string SpriteDir = "Assets/Resources/" + GameSprites.Folder;
    public const string WalkDir = "Assets/Resources/" + WalkThemeTextures.Folder;

    [MenuItem("Tools/Agent TR8N1/Bake missing sprites")]
    public static void BakeMissingSprites()
    {
        int baked = BakeSprites();
        Debug.Log($"Baked {baked} sprites into {SpriteDir} (existing PNGs were kept)");
    }

    // Sprites + the three code stages as prefabs + registering them in the scene
    public static void MigrateAll()
    {
        BakeMissingSprites();
        StageSetBaker.BakeStages();
        StageSetBaker.RegisterInScene("Assets/Scenes/SampleScene.unity");
    }

    public static int BakeSprites()
    {
        Directory.CreateDirectory(SpriteDir);
        Directory.CreateDirectory(WalkDir);
        List<(string path, Texture2D tex, float ppu, bool sprite)> baked = new List<(string, Texture2D, float, bool)>();

        // Every public static method of SpritePainter returns a Sprite; the sprite name is its key
        foreach (MethodInfo method in typeof(SpritePainter).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.ReturnType != typeof(Sprite)) continue;
            foreach (object[] args in SampleArguments(method))
            {
                Sprite sprite = (Sprite)method.Invoke(null, args);
                string path = $"{SpriteDir}/{sprite.name}.png";
                if (File.Exists(path)) continue;
                File.WriteAllBytes(path, sprite.texture.EncodeToPNG());
                baked.Add((path, sprite.texture, sprite.pixelsPerUnit, true));
            }
        }

        foreach (WalkTheme theme in Enum.GetValues(typeof(WalkTheme)))
        {
            string path = $"{WalkDir}/{theme}.png";
            if (File.Exists(path)) continue;
            Texture2D tex = WalkThemePainter.Get(theme);
            tex.wrapMode = TextureWrapMode.Repeat;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            baked.Add((path, tex, 100f, false));
        }

        AssetDatabase.Refresh();
        foreach (var (path, tex, ppu, sprite) in baked) Configure(path, tex, ppu, sprite);
        return baked.Count;
    }

    // Pixel-art import: no filtering (except the soft gradients), no compression, no mipmaps.
    // Sprites keep the size they had in code (pixels per unit) and a full-rect mesh so they tile.
    private static void Configure(string path, Texture2D source, float ppu, bool sprite)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        importer.filterMode = source.filterMode;
        importer.wrapMode = source.wrapMode;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        if (sprite)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
        }
        importer.SaveAndReimport();
    }

    // Values for the painters that take parameters (drone frames, day/night)
    private static IEnumerable<object[]> SampleArguments(MethodInfo method)
    {
        ParameterInfo[] p = method.GetParameters();
        if (p.Length == 0) yield return new object[0];
        else if (p.Length == 1 && p[0].ParameterType == typeof(int)) { yield return new object[] { 0 }; yield return new object[] { 1 }; }
        else if (p.Length == 1 && p[0].ParameterType == typeof(bool)) { yield return new object[] { false }; yield return new object[] { true }; }
        else Debug.LogWarning($"ArtBaker doesn't know which arguments to pass to SpritePainter.{method.Name}");
    }
}
