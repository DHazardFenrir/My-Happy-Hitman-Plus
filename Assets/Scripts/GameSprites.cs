using System.Collections.Generic;
using UnityEngine;

// The game's pixel-art sprites, loaded from Assets/Resources/Sprites (one PNG per sprite).
// The PNGs were baked from the original code painting (Editor/Art/SpritePainter) and are now the
// real art: edit them in Aseprite and Unity reimports them. Prefabs reference the PNGs directly;
// code that builds things at runtime (UI, cinematics, HUD) asks for them here.
public static class GameSprites
{
    public const string Folder = "Sprites";

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // Where the windshield and headlights are inside CarFront(), normalized 0..1 (for overlays)
    public static readonly Rect CarWindshield = new Rect(8f / 48f, 20f / 32f, 32f / 48f, 8f / 32f);
    public static readonly Vector2[] CarHeadlights = { new Vector2(8.5f / 48f, 11.5f / 32f), new Vector2(39.5f / 48f, 11.5f / 32f) };

    // Radius of the hole in ScopeVignette(), as a fraction of its side
    public const float VignetteHoleRadius = 1f / 24f;

    public static Sprite Drone(int frame) => Get("drone" + frame); // frame 0/1 swaps the propellers
    public static Sprite Bullet() => Get("bullet");
    public static Sprite Pixel() => Get("pixel"); // 1x1 white, tinted with SpriteRenderer.color

    // Retro ad products
    public static Sprite Glasses() => Get("glasses");
    public static Sprite PillBottle() => Get("pills");
    public static Sprite PuzzleMagazine() => Get("magazine");
    public static Sprite Carrot() => Get("carrot");
    public static Sprite Blindfold() => Get("blindfold");
    public static Sprite Starburst() => Get("starburst");
    public static Sprite SoftCircle() => Get("softcircle");
    public static Texture2D AdStripes() => Get("adstripes")?.texture; // tiling

    // Hotel (elevator minigame), tiling
    public static Sprite HotelWallpaper() => Get("wallpaper");
    public static Sprite HotelCarpet() => Get("carpet");
    public static Sprite WoodPanel() => Get("wood");

    // Real-life death and ending
    public static Sprite CarFront() => Get("carfront");
    public static Sprite Star() => Get("star");

    // Drone stages
    public static Sprite Crosshair() => Get("crosshair");
    public static Sprite ScopeRing() => Get("scopering");
    public static Sprite ScopeVignette() => Get("scopevignette");
    public static Sprite Scanlines() => Get("scanlines");
    public static Sprite CameraVignette() => Get("cameravignette");
    public static Sprite BuildingWindows(bool night) => Get(night ? "windows_night" : "windows_day");
    public static Sprite Pavement(bool night) => Get(night ? "pavement_night" : "pavement_day");

    // RC car minigame (the traffic cars come from the bk_cars1.a sheet, see RcCarStage)
    public static Sprite RcGrass() => Get("rc_grass"); // tiling
    public static Sprite RcCurb() => Get("rc_curb");   // tiling

    // Guard dogs minigame
    public static Sprite Dog(int frame) => Get("dog" + frame); // frame 0/1 = running stride, faces right
    public static Sprite FenceSlats() => Get("fence_slats");   // tiling

    // Signal scan minigame
    public static Sprite ScanRing() => Get("scanring");       // 2 units wide: scale = radius
    public static Sprite PhoneSignal() => Get("phonesignal"); // a device above each person

    // Face layers of the human check minigame (face_round, eyes_a, horns, diff_glasses... see HumanCheckStage)
    public static Sprite FacePart(string key) => Get(key);

    // Fingerprint of the last-chance re-authentication (filled bottom-up in the UI)
    public static Sprite Fingerprint() => Get("fingerprint");

    // Blood splatter over the screen when you die "in real life" (4 variants, pick one at random)
    public const int BloodSplashCount = 4;
    public static Sprite BloodSplash(int variant) => Get("bloodsplash" + variant);

    public static Sprite Get(string key)
    {
        if (cache.TryGetValue(key, out Sprite cached)) return cached;
        Sprite sprite = Resources.Load<Sprite>(Folder + "/" + key);
        if (sprite == null) Debug.LogError($"Missing sprite Assets/Resources/{Folder}/{key}.png (Tools > Agent TR8N1 > Bake missing sprites)");
        cache[key] = sprite;
        return sprite;
    }
}
