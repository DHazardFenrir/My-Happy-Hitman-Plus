using System.Collections.Generic;
using UnityEngine;

// The places you walk through while playing on your phone. Each minigame has its own.
public enum WalkTheme { Street, Crosswalk, Construction, Sewer, Hotel }

// Top-down pixel-art of each walking background (128x128, tileable vertically), loaded from
// Assets/Resources/Sprites/Walk/<theme>.png. Baked from Editor/Art/WalkThemePainter.
public static class WalkThemeTextures
{
    public const string Folder = GameSprites.Folder + "/Walk";
    public const int W = 128;
    public const int H = 128;

    private static readonly Dictionary<WalkTheme, Texture2D> cache = new Dictionary<WalkTheme, Texture2D>();

    public static Texture2D Get(WalkTheme theme)
    {
        if (cache.TryGetValue(theme, out Texture2D cached)) return cached;
        Texture2D tex = Resources.Load<Texture2D>(Folder + "/" + theme);
        if (tex == null) Debug.LogError($"Missing walking background Assets/Resources/{Folder}/{theme}.png (Tools > Agent TR8N1 > Bake missing sprites)");
        cache[theme] = tex;
        return tex;
    }
}
