using System.Collections.Generic;
using UnityEngine;

// The original pixel-art of the game, painted in code (drones, bullets, ad products, hotel, drone
// stages...). EDITOR ONLY: Tools > Agent TR8N1 > Bake missing sprites saves each one as a PNG in
// Assets/Resources/Sprites, and from then on the PNG is the real art (edit it in Aseprite).
// Delete a PNG and bake again to get the original back.
public static class SpritePainter
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    private static readonly Color Clear = new Color(0, 0, 0, 0);
    private static readonly Color Outline = new Color(0.10f, 0.10f, 0.13f);
    private static readonly Color Body = new Color(0.45f, 0.48f, 0.55f);
    private static readonly Color BodyLight = new Color(0.65f, 0.68f, 0.75f);
    private static readonly Color Rotor = new Color(0.85f, 0.85f, 0.90f, 0.85f);
    private static readonly Color Led = new Color(1f, 0.15f, 0.15f);

    // 24x12 px drone. frame 0/1 swaps the propellers to animate them.
    public static Sprite Drone(int frame)
    {
        string key = "drone" + frame;
        if (cache.TryGetValue(key, out Sprite cached)) return cached;

        Texture2D tex = NewTexture(24, 12);

        // Propellers
        if (frame == 0)
        {
            Fill(tex, 0, 11, 8, 11, Rotor);
            Fill(tex, 15, 11, 23, 11, Rotor);
        }
        else
        {
            Fill(tex, 2, 11, 6, 11, Rotor);
            Fill(tex, 17, 11, 21, 11, Rotor);
        }

        // Propeller masts
        Fill(tex, 4, 9, 4, 10, Outline);
        Fill(tex, 19, 9, 19, 10, Outline);

        // Arms
        Fill(tex, 2, 8, 21, 8, Outline);

        // Body
        Fill(tex, 7, 3, 16, 7, Outline);
        Fill(tex, 8, 4, 15, 6, Body);
        Fill(tex, 9, 6, 14, 6, BodyLight);

        // Red light
        Fill(tex, 14, 5, 14, 5, Led);

        // Camera / gun
        Fill(tex, 10, 1, 13, 2, Outline);
        Fill(tex, 11, 0, 12, 0, Outline);

        return Save(key, tex, 30f);
    }

    public static Sprite Bullet()
    {
        if (cache.TryGetValue("bullet", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(3, 3);
        Fill(tex, 0, 0, 2, 2, new Color(1f, 0.85f, 0.2f));
        Fill(tex, 1, 1, 1, 1, Color.white);
        return Save("bullet", tex, 30f);
    }

    // 1x1 px white sprite (ropes, banners, stains...). Tinted with SpriteRenderer.color.
    public static Sprite Pixel()
    {
        if (cache.TryGetValue("pixel", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Save("pixel", tex, 1f);
    }

    // Sprite that can be drawn in Tiled mode (SpriteRenderer.drawMode = Tiled)
    private static Sprite SaveTiled(string key, Texture2D tex, float pixelsPerUnit)
    {
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
            pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = key;
        cache[key] = sprite;
        return sprite;
    }

    // ---------------- Retro ad products ----------------

    // Retro TV-ad glasses: thick frame, gradient lenses and a shine
    public static Sprite Glasses()
    {
        if (cache.TryGetValue("glasses", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(48, 20);
        Color frame = new Color(0.08f, 0.06f, 0.10f);
        Color lensTop = new Color(0.35f, 0.95f, 1f, 0.9f);
        Color lensBottom = new Color(1f, 0.25f, 0.75f, 0.9f);

        for (int side = 0; side < 2; side++)
        {
            int x0 = side == 0 ? 3 : 27, x1 = x0 + 17;
            // Frame with clipped corners
            Fill(tex, x0 + 1, 2, x1 - 1, 17, frame);
            Fill(tex, x0, 3, x1, 16, frame);
            // Lens with a vertical gradient
            for (int y = 4; y <= 15; y++)
            {
                Color c = Color.Lerp(lensBottom, lensTop, (y - 4) / 11f);
                Fill(tex, x0 + 2, y, x1 - 2, y, c);
            }
            // Diagonal shine
            for (int i = 0; i < 6; i++)
            {
                tex.SetPixel(x0 + 4 + i, 14 - i, Color.white);
                tex.SetPixel(x0 + 5 + i, 14 - i, new Color(1f, 1f, 1f, 0.6f));
            }
        }
        // Bridge and temples
        Fill(tex, 21, 12, 26, 14, frame);
        Fill(tex, 0, 13, 2, 15, frame);
        Fill(tex, 45, 13, 47, 15, frame);
        return Save("glasses", tex, 16f);
    }

    // Eye supplement bottle: orange bottle, white cap, label with an eye
    public static Sprite PillBottle()
    {
        if (cache.TryGetValue("pills", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(24, 32);
        Color outline = new Color(0.25f, 0.12f, 0.02f);
        Color bottle = new Color(0.95f, 0.55f, 0.10f), shine = new Color(1f, 0.78f, 0.45f);
        Color cap = new Color(0.95f, 0.95f, 0.95f), label = new Color(0.98f, 0.95f, 0.85f);
        Fill(tex, 3, 0, 20, 24, outline);
        Fill(tex, 4, 1, 19, 23, bottle);
        Fill(tex, 6, 3, 7, 21, shine);
        Fill(tex, 5, 24, 18, 31, outline);
        Fill(tex, 6, 25, 17, 30, cap);
        for (int x = 7; x < 17; x += 2) Fill(tex, x, 25, x, 30, new Color(0.8f, 0.8f, 0.8f));
        Fill(tex, 4, 7, 19, 17, label);
        // Eye on the label
        Fill(tex, 8, 11, 15, 13, new Color(0.2f, 0.5f, 0.95f));
        Fill(tex, 10, 10, 13, 14, new Color(0.2f, 0.5f, 0.95f));
        Fill(tex, 11, 11, 12, 13, Color.black);
        return Save("pills", tex, 16f);
    }

    // Puzzle magazine: cover with a title band and a crossword grid
    public static Sprite PuzzleMagazine()
    {
        if (cache.TryGetValue("magazine", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(26, 32);
        Color outline = new Color(0.10f, 0.10f, 0.15f);
        Fill(tex, 0, 0, 25, 31, outline);
        Fill(tex, 1, 1, 24, 30, new Color(0.25f, 0.55f, 0.95f));
        Fill(tex, 1, 24, 24, 30, new Color(0.95f, 0.20f, 0.30f));
        for (int x = 3; x < 23; x += 4) Fill(tex, x, 26, x + 2, 28, Color.white); // "letters" of the title
        // Crossword grid
        Fill(tex, 4, 4, 21, 21, outline);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                bool black = (x + y * 3) % 5 == 0;
                Fill(tex, 5 + x * 4, 5 + y * 4, 7 + x * 4, 7 + y * 4, black ? outline : Color.white);
            }
        // Big yellow "!" sticker
        Fill(tex, 19, 18, 24, 23, new Color(1f, 0.88f, 0.15f));
        Fill(tex, 21, 20, 22, 22, outline);
        tex.SetPixel(21, 19, outline); tex.SetPixel(22, 19, outline);
        return Save("magazine", tex, 16f);
    }

    // A big shiny carrot
    public static Sprite Carrot()
    {
        if (cache.TryGetValue("carrot", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(20, 32);
        Color outline = new Color(0.40f, 0.15f, 0.02f);
        Color carrot = new Color(1f, 0.50f, 0.08f), shine = new Color(1f, 0.72f, 0.35f);
        Color leaf = new Color(0.20f, 0.65f, 0.20f), leafDark = new Color(0.10f, 0.40f, 0.12f);
        for (int y = 0; y < 24; y++)
        {
            int half = Mathf.Max(1, y * 6 / 23);
            Fill(tex, 10 - half - 1, y, 10 + half, y, outline);
            if (half > 1) Fill(tex, 10 - half, y, 10 + half - 1, y, carrot);
            if (half > 2) tex.SetPixel(10 - half + 1, y, shine);
            if (y % 5 == 2 && half > 2) Fill(tex, 10, y, 10 + half - 2, y, outline);
        }
        Fill(tex, 5, 24, 7, 31, leafDark);
        Fill(tex, 9, 24, 11, 31, leaf);
        Fill(tex, 13, 24, 15, 30, leafDark);
        Fill(tex, 7, 26, 13, 27, leaf);
        return Save("carrot", tex, 16f);
    }

    // Sleep mask / blindfold (the "stop watching that stuff at night" product)
    public static Sprite Blindfold()
    {
        if (cache.TryGetValue("blindfold", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(44, 20);
        Color strap = new Color(0.15f, 0.12f, 0.20f);
        Color mask = new Color(0.30f, 0.12f, 0.45f), maskLight = new Color(0.45f, 0.22f, 0.62f);
        Color pink = new Color(1f, 0.45f, 0.70f);
        Fill(tex, 0, 9, 43, 11, strap);
        Fill(tex, 6, 3, 37, 16, strap);
        Fill(tex, 7, 4, 36, 15, mask);
        Fill(tex, 8, 13, 35, 14, maskLight);
        Fill(tex, 20, 3, 23, 6, strap); // nose notch
        // Closed eyes (Zz)
        Fill(tex, 11, 9, 17, 9, pink); tex.SetPixel(10, 10, pink); tex.SetPixel(18, 10, pink);
        Fill(tex, 26, 9, 32, 9, pink); tex.SetPixel(25, 10, pink); tex.SetPixel(33, 10, pink);
        return Save("blindfold", tex, 16f);
    }

    // Yellow deal starburst with an orange edge
    public static Sprite Starburst()
    {
        if (cache.TryGetValue("starburst", out Sprite cached)) return cached;
        const int size = 64;
        Texture2D tex = NewTexture(size, size);
        Color fill = new Color(1f, 0.89f, 0.18f);
        Color edge = new Color(1f, 0.45f, 0.05f);
        const int points = 14;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f);
                float angle = Mathf.Atan2(p.y, p.x);
                float k = Mathf.Abs(Mathf.Repeat(angle / (2f * Mathf.PI) * points, 1f) - 0.5f) * 2f; // 0 at a point, 1 in a valley
                float radius = Mathf.Lerp(31f, 22f, k);
                float d = p.magnitude;
                if (d <= radius - 2f) tex.SetPixel(x, y, fill);
                else if (d <= radius) tex.SetPixel(x, y, edge);
            }
        return Save("starburst", tex, 32f);
    }

    // Soft-edged white circle (shadows). Tinted with the Image/SpriteRenderer color.
    public static Sprite SoftCircle()
    {
        if (cache.TryGetValue("softcircle", out Sprite cached)) return cached;
        const int size = 64;
        Texture2D tex = NewTexture(size, size);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f).magnitude / (size / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 3f)));
            }
        return Save("softcircle", tex, 32f);
    }

    // Diagonal stripes for the ad background (tiling)
    public static Sprite AdStripes()
    {
        if (cache.TryGetValue("adstripes", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(16, 16);
        Color a = new Color(1f, 0.25f, 0.64f);
        Color b = new Color(1f, 0.82f, 0.25f);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                tex.SetPixel(x, y, ((x + y) / 4) % 2 == 0 ? a : b);
        return SaveTiled("adstripes", tex, 16f);
    }

    // ---------------- Hotel (elevator minigame) ----------------

    public static Sprite HotelWallpaper()
    {
        if (cache.TryGetValue("wallpaper", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(16, 16);
        Color baseC = new Color(0.45f, 0.10f, 0.14f);
        Color stripe = new Color(0.36f, 0.07f, 0.11f);
        Color gold = new Color(0.80f, 0.62f, 0.25f);
        Fill(tex, 0, 0, 15, 15, baseC);
        Fill(tex, 6, 0, 9, 15, stripe);
        Fill(tex, 0, 0, 0, 15, gold);
        // Gold diamond
        tex.SetPixel(12, 9, gold); tex.SetPixel(11, 8, gold); tex.SetPixel(13, 8, gold); tex.SetPixel(12, 7, gold);
        return SaveTiled("wallpaper", tex, 16f);
    }

    public static Sprite HotelCarpet()
    {
        if (cache.TryGetValue("carpet", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(16, 8);
        Color red = new Color(0.55f, 0.08f, 0.10f);
        Color gold = new Color(0.85f, 0.65f, 0.22f);
        Fill(tex, 0, 0, 15, 7, red);
        for (int i = 0; i < 4; i++)
        {
            tex.SetPixel(4 + i, 4 + i % 2, gold);
            tex.SetPixel(12 - i, 4 - i % 2, gold);
        }
        Fill(tex, 0, 7, 15, 7, gold);
        return SaveTiled("carpet", tex, 16f);
    }

    public static Sprite WoodPanel()
    {
        if (cache.TryGetValue("wood", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(8, 8);
        Color wood = new Color(0.36f, 0.22f, 0.12f);
        Color dark = new Color(0.25f, 0.15f, 0.08f);
        Fill(tex, 0, 0, 7, 7, wood);
        Fill(tex, 0, 0, 0, 7, dark);
        Fill(tex, 4, 2, 4, 5, dark);
        return SaveTiled("wood", tex, 16f);
    }

    // ---------------- Car seen from the front (real-life death) ----------------

    // 48x32 px car coming at you: windshield, headlights, grille, bumper and tires
    public static Sprite CarFront()
    {
        if (cache.TryGetValue("carfront", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(48, 32);
        Color body = new Color(0.17f, 0.22f, 0.42f), bodyDark = new Color(0.10f, 0.13f, 0.27f);
        Color glass = new Color(0.55f, 0.75f, 0.90f), shine = new Color(0.85f, 0.93f, 1f);
        Color tire = new Color(0.08f, 0.08f, 0.09f), chrome = new Color(0.75f, 0.77f, 0.80f);
        Color lamp = new Color(1f, 0.97f, 0.75f), grille = new Color(0.06f, 0.06f, 0.08f);

        Fill(tex, 4, 0, 11, 4, tire);
        Fill(tex, 36, 0, 43, 4, tire);
        Fill(tex, 1, 6, 46, 15, body);
        Fill(tex, 1, 6, 46, 6, bodyDark);
        Fill(tex, 2, 3, 45, 5, chrome);                       // bumper
        Fill(tex, 20, 3, 27, 5, Color.white);                 // plate
        Fill(tex, 18, 8, 29, 12, grille);
        for (int x = 19; x <= 28; x += 2) Fill(tex, x, 9, x, 11, bodyDark);
        Fill(tex, 4, 9, 12, 13, lamp);                        // headlights
        Fill(tex, 35, 9, 43, 13, lamp);
        Fill(tex, 3, 8, 13, 8, chrome);
        Fill(tex, 34, 8, 44, 8, chrome);
        Fill(tex, 3, 16, 44, 18, body);                       // hood
        Fill(tex, 2, 17, 5, 19, body);                        // mirrors
        Fill(tex, 42, 17, 45, 19, body);
        for (int y = 19; y <= 28; y++)                        // windshield frame + glass
        {
            int inset = (y - 19) / 2;
            Fill(tex, 6 + inset, y, 41 - inset, y, body);
            if (y >= 20 && y <= 27) Fill(tex, 8 + inset, y, 39 - inset, y, glass);
        }
        for (int i = 0; i < 5; i++) tex.SetPixel(14 + i, 21 + i, shine);
        for (int i = 0; i < 3; i++) tex.SetPixel(18 + i, 21 + i, shine);
        Fill(tex, 11, 29, 36, 30, body);                      // roof
        return Save("carfront", tex, 32f);
    }

    // 16x16 px gold star (certified drone)
    public static Sprite Star()
    {
        if (cache.TryGetValue("star", out Sprite cached)) return cached;
        const int size = 16;
        Texture2D tex = NewTexture(size, size);
        Color gold = new Color(1f, 0.82f, 0.15f), edge = new Color(0.65f, 0.40f, 0.05f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f);
                float angle = Mathf.Atan2(p.y, p.x) - Mathf.PI / 2f;
                float k = Mathf.Abs(Mathf.Repeat(angle / (2f * Mathf.PI) * 5f, 1f) - 0.5f) * 2f; // 1 at a point
                float radius = Mathf.Lerp(3.2f, 7.8f, k);
                float d = p.magnitude;
                if (d <= radius - 1.2f) tex.SetPixel(x, y, gold);
                else if (d <= radius) tex.SetPixel(x, y, edge);
            }
        return Save("star", tex, 16f);
    }

    // ---------------- Drone camera ----------------

    // 32x32 px crosshair: broken ring, ticks and a center dot. White, tinted with SpriteRenderer.color.
    public static Sprite Crosshair()
    {
        if (cache.TryGetValue("crosshair", out Sprite cached)) return cached;
        const int size = 32;
        Texture2D tex = NewTexture(size, size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f);
                float d = p.magnitude;
                bool gap = Mathf.Abs(p.x) < 3f || Mathf.Abs(p.y) < 3f;
                if (d > 11f && d < 13.5f && !gap) tex.SetPixel(x, y, Color.white);
            }
        Fill(tex, 15, 0, 16, 9, Color.white);
        Fill(tex, 15, 22, 16, 31, Color.white);
        Fill(tex, 0, 15, 9, 16, Color.white);
        Fill(tex, 22, 15, 31, 16, Color.white);
        Fill(tex, 15, 15, 16, 16, Color.white);
        return Save("crosshair", tex, 32f);
    }

    // Night-vision scope edge: a thick ring with tick marks (the inside stays transparent)
    public static Sprite ScopeRing()
    {
        if (cache.TryGetValue("scopering", out Sprite cached)) return cached;
        const int size = 64;
        Texture2D tex = NewTexture(size, size);
        Color ring = new Color(0.35f, 1f, 0.45f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f);
                float d = p.magnitude;
                if (d > 29f && d < 31.5f) tex.SetPixel(x, y, ring);
                float angle = Mathf.Repeat(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, 30f);
                if (d > 26f && d <= 29f && (angle < 2.5f || angle > 27.5f)) tex.SetPixel(x, y, ring);
            }
        return Save("scopering", tex, 32f);
    }

    // Night-vision darkness: opaque white everywhere except a soft round hole in the middle
    // (radius = GameSprites.VignetteHoleRadius of the side).
    public static Sprite ScopeVignette()
    {
        if (cache.TryGetValue("scopevignette", out Sprite cached)) return cached;
        const int size = 1024;
        Texture2D tex = NewTexture(size, size);
        tex.filterMode = FilterMode.Bilinear;
        float hole = size * GameSprites.VignetteHoleRadius;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f).magnitude;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((d - hole * 0.85f) / (hole * 0.15f)));
            }
        tex.SetPixels(px);
        return Save("scopevignette", tex, size);
    }

    // Camera scanlines (tiles): one dark line every 4 px
    public static Sprite Scanlines()
    {
        if (cache.TryGetValue("scanlines", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(1, 4);
        tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.35f));
        tex.SetPixel(0, 1, new Color(0f, 0f, 0f, 0.12f));
        return SaveTiled("scanlines", tex, 160f);
    }

    // Camera vignette: transparent in the middle, darker toward the edges and corners (1 unit, stretched)
    public static Sprite CameraVignette()
    {
        if (cache.TryGetValue("cameravignette", out Sprite cached)) return cached;
        const int size = 128;
        Texture2D tex = NewTexture(size, size);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x / (size - 1f) * 2f - 1f, y / (size - 1f) * 2f - 1f);
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(p.x), 4f) + Mathf.Pow(Mathf.Abs(p.y), 4f), 0.25f); // rounded square
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 0.75f, (d - 0.6f) / 0.4f)));
            }
        return Save("cameravignette", tex, size);
    }

    // Building facade with windows (tiles). At night some windows are lit.
    public static Sprite BuildingWindows(bool night)
    {
        string key = night ? "windows_night" : "windows_day";
        if (cache.TryGetValue(key, out Sprite cached)) return cached;
        Texture2D tex = NewTexture(16, 16);
        Color wall = night ? new Color(0.10f, 0.11f, 0.17f) : new Color(0.62f, 0.52f, 0.45f);
        Color frame = night ? new Color(0.06f, 0.06f, 0.10f) : new Color(0.40f, 0.33f, 0.30f);
        Fill(tex, 0, 0, 15, 15, wall);
        System.Random rng = new System.Random(night ? 7 : 3);
        for (int wx = 0; wx < 2; wx++)
            for (int wy = 0; wy < 2; wy++)
            {
                int x0 = 2 + wx * 8, y0 = 2 + wy * 8;
                Color glass = night
                    ? (rng.NextDouble() < 0.45 ? new Color(1f, 0.85f, 0.45f) : new Color(0.05f, 0.06f, 0.10f))
                    : new Color(0.55f, 0.75f, 0.90f);
                Fill(tex, x0 - 1, y0 - 1, x0 + 4, y0 + 4, frame);
                Fill(tex, x0, y0, x0 + 3, y0 + 3, glass);
                if (!night) Fill(tex, x0, y0 + 3, x0 + 1, y0 + 3, Color.white);
            }
        return SaveTiled(key, tex, 16f);
    }

    // Plaza paving stones (tiles)
    public static Sprite Pavement(bool night)
    {
        string key = night ? "pavement_night" : "pavement_day";
        if (cache.TryGetValue(key, out Sprite cached)) return cached;
        Texture2D tex = NewTexture(16, 16);
        Color stone = night ? new Color(0.16f, 0.17f, 0.20f) : new Color(0.72f, 0.70f, 0.66f);
        Color joint = night ? new Color(0.09f, 0.09f, 0.12f) : new Color(0.55f, 0.53f, 0.50f);
        Fill(tex, 0, 0, 15, 15, stone);
        Fill(tex, 0, 0, 15, 0, joint);
        Fill(tex, 0, 8, 15, 8, joint);
        Fill(tex, 0, 1, 0, 7, joint);
        Fill(tex, 8, 9, 8, 15, joint);
        return SaveTiled(key, tex, 16f);
    }

    // ---------------- RC car minigame (the traffic uses the bk_cars1.a sheet) ----------------

    // Roadside grass (tiling) with darker blades and a few flowers
    public static Sprite RcGrass()
    {
        if (cache.TryGetValue("rc_grass", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(16, 16);
        Color grass = new Color(0.28f, 0.58f, 0.24f), blade = new Color(0.20f, 0.45f, 0.18f);
        Fill(tex, 0, 0, 15, 15, grass);
        int[] bx = { 2, 6, 11, 14, 4, 9, 13, 1 }, by = { 3, 12, 6, 14, 8, 1, 10, 13 };
        for (int i = 0; i < bx.Length; i++) { tex.SetPixel(bx[i], by[i], blade); tex.SetPixel(bx[i], (by[i] + 1) % 16, blade); }
        tex.SetPixel(8, 5, new Color(1f, 0.9f, 0.3f)); tex.SetPixel(3, 13, Color.white); tex.SetPixel(12, 2, new Color(1f, 0.5f, 0.7f));
        return SaveTiled("rc_grass", tex, 16f);
    }

    // Red and white curb (tiling vertically)
    public static Sprite RcCurb()
    {
        if (cache.TryGetValue("rc_curb", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(4, 16);
        Fill(tex, 0, 0, 3, 7, new Color(0.88f, 0.12f, 0.12f));
        Fill(tex, 0, 8, 3, 15, new Color(0.95f, 0.95f, 0.95f));
        return SaveTiled("rc_curb", tex, 16f);
    }

    // ---------------- Guard dogs minigame ----------------

    // 22x14 px angry guard dog seen from the side, facing right. frame 0/1 = running stride.
    // Rottweiler colors: black fur, tan muzzle and legs, red spiked collar, red eye, bared teeth.
    public static Sprite Dog(int frame)
    {
        string key = "dog" + frame;
        if (cache.TryGetValue(key, out Sprite cached)) return cached;
        Texture2D tex = NewTexture(22, 14);
        Color fur = new Color(0.16f, 0.12f, 0.10f), furLight = new Color(0.28f, 0.22f, 0.18f);
        Color tan = new Color(0.68f, 0.43f, 0.20f), collar = new Color(0.85f, 0.1f, 0.1f), spike = new Color(0.8f, 0.82f, 0.85f);
        Color eye = new Color(1f, 0.15f, 0.1f), teeth = Color.white, nose = new Color(0.05f, 0.05f, 0.05f);

        // Legs (tan paws): planted, or stretched in the stride
        if (frame == 0)
        {
            Fill(tex, 4, 0, 5, 4, fur); Fill(tex, 7, 0, 8, 4, fur);
            Fill(tex, 14, 0, 15, 4, fur); Fill(tex, 17, 0, 18, 4, fur);
            Fill(tex, 4, 0, 5, 0, tan); Fill(tex, 7, 0, 8, 0, tan); Fill(tex, 14, 0, 15, 0, tan); Fill(tex, 17, 0, 18, 0, tan);
        }
        else
        {
            Fill(tex, 2, 1, 3, 4, fur); Fill(tex, 1, 0, 2, 1, tan);      // back legs kicked back
            Fill(tex, 9, 1, 10, 4, fur); Fill(tex, 9, 0, 10, 0, tan);
            Fill(tex, 17, 1, 18, 4, fur); Fill(tex, 19, 0, 20, 1, tan);  // front legs reaching forward
            Fill(tex, 13, 1, 14, 4, fur); Fill(tex, 13, 0, 14, 0, tan);
        }

        // Body, chest and tail
        Fill(tex, 3, 4, 17, 8, fur);
        Fill(tex, 5, 8, 15, 8, furLight);                // back highlight
        Fill(tex, 7, 4, 13, 4, tan);                     // belly
        Fill(tex, 15, 4, 18, 9, fur);                    // chest
        Fill(tex, 1, 8, 2, 10, fur);                     // stubby tail up
        if (frame == 1) tex.SetPixel(0, 11, fur);

        // Head with tan muzzle, black nose, red eye and teeth
        Fill(tex, 16, 7, 20, 12, fur);
        Fill(tex, 19, 7, 21, 9, tan);
        tex.SetPixel(21, 9, nose); tex.SetPixel(21, 10, nose);
        tex.SetPixel(18, 10, eye);
        Fill(tex, 19, 7, 20, 7, teeth);                  // snarl
        Fill(tex, 16, 12, 17, 13, fur);                  // pointy ear
        tex.SetPixel(17, 13, furLight);

        // Spiked collar
        Fill(tex, 15, 6, 16, 9, collar);
        tex.SetPixel(15, 10, spike); tex.SetPixel(16, 5, spike);
        return Save(key, tex, 24f);
    }

    // Steel slats of the smart fence (tiling): 6 px slat with a highlight, 2 px gap to see through
    public static Sprite FenceSlats()
    {
        if (cache.TryGetValue("fence_slats", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(8, 16);
        Color steel = new Color(0.30f, 0.33f, 0.38f), light = new Color(0.50f, 0.55f, 0.60f), dark = new Color(0.18f, 0.20f, 0.24f);
        Fill(tex, 0, 0, 5, 15, steel);
        Fill(tex, 1, 0, 1, 15, light);
        Fill(tex, 5, 0, 5, 15, dark);
        Fill(tex, 0, 14, 5, 15, dark);                   // cap
        return SaveTiled("fence_slats", tex, 12f);
    }

    // ---------------- Blood splatters (the "real life" death) ----------------
    // Four 256x256 splatters baked once; the game picks one at random and shows it big over the screen
    // (and on the car's windshield). Repaint them in Aseprite to make them look more natural.

    public static Sprite BloodSplash0() => Save("bloodsplash0", PaintBloodSplash(101), 100f);
    public static Sprite BloodSplash1() => Save("bloodsplash1", PaintBloodSplash(202), 100f);
    public static Sprite BloodSplash2() => Save("bloodsplash2", PaintBloodSplash(303), 100f);
    public static Sprite BloodSplash3() => Save("bloodsplash3", PaintBloodSplash(404), 100f);

    // Big blood splatter (transparent): an irregular splash with fingers shooting out, outer drops with
    // tails pointing back at it and tiny satellites, and drips running down
    private static Texture2D PaintBloodSplash(int seed)
    {
        const int size = 256;
        Color[] px = new Color[size * size];
        System.Random rng = new System.Random(seed);
        Color dark = new Color(0.40f, 0.01f, 0.02f, 0.95f);
        Color bright = new Color(0.72f, 0.03f, 0.05f, 0.95f);
        float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        // A spot with an irregular edge: a few random harmonics (2..6) with random strength and phase,
        // so no two drops are alike and none is a perfect circle (or a star)
        void Blob(float cx, float cy, float r, float rough = 0f)
        {
            float[] amp = new float[5], phase = new float[5];
            for (int h = 0; h < 5; h++) { amp[h] = rough > 0f ? Rand(0f, rough) / Mathf.Sqrt(h + 2) : 0f; phase[h] = Rand(0f, 6.28f); }
            float reach = r * (1f + rough * 1.6f) + 2f;
            for (int y = (int)(cy - reach); y <= cy + reach; y++)
                for (int x = (int)(cx - reach); x <= cx + reach; x++)
                {
                    if (x < 0 || y < 0 || x >= size || y >= size) continue;
                    float dx = x - cx, dy = y - cy, theta = Mathf.Atan2(dy, dx);
                    float edge = 1f;
                    for (int h = 0; h < 5; h++) edge += amp[h] * Mathf.Sin(theta * (h + 2) + phase[h]);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / (r * edge);
                    if (d > 1f) continue;
                    int i = y * size + x;
                    if (px[i].a == 0f || d < 0.5f) px[i] = Color.Lerp(bright, dark, d);
                }
        }

        // A tapering line of blood from (x0, y0) toward `angle`, optionally ending in a drop
        void Streak(float x0, float y0, float angle, float length, float w0, float w1, float endDrop = 0f)
        {
            int steps = Mathf.Max(1, (int)length);
            for (int k = 0; k < steps; k++)
            {
                float t = k / (float)Mathf.Max(1, steps - 1);
                Blob(x0 + Mathf.Cos(angle) * length * t, y0 + Mathf.Sin(angle) * length * t, Mathf.Max(0.8f, Mathf.Lerp(w0, w1, t)));
            }
            if (endDrop > 0f) Blob(x0 + Mathf.Cos(angle) * length, y0 + Mathf.Sin(angle) * length, endDrop, 0.25f);
        }

        float cx = size / 2f, cy = size / 2f + 8f;
        Blob(cx, cy, 46f, 0.35f);

        // Fingers shooting out of the main splash, each ending in a drop
        int fingers = rng.Next(7, 11);
        for (int f = 0; f < fingers; f++)
        {
            float a = Rand(0f, 6.28f), start = 40f + Rand(-4f, 4f);
            Streak(cx + Mathf.Cos(a) * start, cy + Mathf.Sin(a) * start, a, Rand(14f, 42f), Rand(5f, 8f), Rand(2f, 3f), Rand(3f, 6f));
        }

        // Outer drops: irregular, with a tail pointing back to the splash and a few tiny satellites
        for (int i = 0; i < 18; i++)
        {
            float a = Rand(0f, 6.28f), dist = Rand(75f, 118f), r = Rand(2.5f, 7f);
            float dx = cx + Mathf.Cos(a) * dist, dy = cy + Mathf.Sin(a) * dist;
            Blob(dx, dy, r, 0.45f);
            Streak(dx, dy, a + Mathf.PI, Rand(r * 1.5f, r * 4f), r * 0.55f, 0.8f);
            int satellites = rng.Next(1, 4);
            for (int k = 0; k < satellites; k++)
            {
                float sa = a + Rand(-0.6f, 0.6f), sd = r + Rand(3f, 10f);
                Blob(dx + Mathf.Cos(sa) * sd, dy + Mathf.Sin(sa) * sd, Rand(0.8f, 1.8f));
            }
        }

        // Drips running down, a little wobbly
        int drips = rng.Next(5, 9);
        for (int i = 0; i < drips; i++)
        {
            float x = cx + Rand(-40f, 40f), width = Rand(2.5f, 6f);
            int length = rng.Next(35, 100);
            for (int j = 0; j < length; j++) Blob(x + Mathf.Sin(j * 0.15f) * 0.6f, cy - 30f - j, width * (1f - j / (float)length * 0.55f));
            Blob(x, cy - 30f - length, width * 0.95f, 0.2f);
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ---------------- Signal scan ----------------

    // Thin white ring, 2 units wide (scale = radius), tinted by the SpriteRenderer
    public static Sprite ScanRing()
    {
        if (cache.TryGetValue("scanring", out Sprite cached)) return cached;
        const int size = 96;
        Texture2D tex = NewTexture(size, size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f).magnitude;
                if (d > 45.5f && d < 47.5f) tex.SetPixel(x, y, Color.white);
                else if (d > 43f && d <= 45.5f) tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.25f));
            }
        return Save("scanring", tex, 48f);
    }

    // A phone with wifi arcs above it: each person's device in the signal scan (white, tinted)
    public static Sprite PhoneSignal()
    {
        if (cache.TryGetValue("phonesignal", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(13, 16);
        Color w = Color.white, dim = new Color(1f, 1f, 1f, 0.55f);
        Fill(tex, 4, 0, 8, 7, w);          // phone
        Fill(tex, 5, 2, 7, 6, Clear);       // screen
        Fill(tex, 6, 1, 6, 1, Clear);       // button
        Fill(tex, 5, 9, 7, 9, w);           // small arc
        Fill(tex, 4, 10, 4, 10, w); Fill(tex, 8, 10, 8, 10, w);
        Fill(tex, 3, 12, 9, 12, dim);       // middle arc
        Fill(tex, 2, 11, 2, 11, dim); Fill(tex, 10, 11, 10, 11, dim);
        Fill(tex, 2, 15, 10, 15, dim);      // big arc
        Fill(tex, 1, 14, 1, 14, dim); Fill(tex, 11, 14, 11, 14, dim);
        Fill(tex, 0, 13, 0, 13, dim); Fill(tex, 12, 13, 12, 13, dim);
        return Save("phonesignal", tex, 100f);
    }

    // ---------------- Faces (human check minigame) ----------------
    // 32x32 layers stacked by HumanCheckStage. White parts (skin, hair) are tinted at runtime;
    // the rest keep their colors. People with a facial difference are drawn like everyone else:
    // the difference is just one more layer (the AI is the one that gets them wrong).

    private const int F = 32;
    private static readonly Color FaceInk = new Color(0.13f, 0.10f, 0.12f);

    public static Sprite FaceRound() => FaceLayer("face_round", (x, y) => InHead(x, y, false) ? Color.white : Clear);
    public static Sprite FaceRoundLine() => FaceLayer("face_round_line", (x, y) => HeadEdge(x, y, false) ? FaceInk : Clear);
    public static Sprite FacePointy() => FaceLayer("face_pointy", (x, y) => InHead(x, y, true) ? Color.white : Clear);
    public static Sprite FacePointyLine() => FaceLayer("face_pointy_line", (x, y) => HeadEdge(x, y, true) ? FaceInk : Clear);

    // Eyes at y = 16, x = 12..13 and 19..20
    public static Sprite EyesA() => FaceLayer("eyes_a", (x, y) => EyePair(x, y, Color.white, FaceInk, false));
    public static Sprite EyesB() => FaceLayer("eyes_b", (x, y) =>
        (y == 16 && (x == 12 || x == 20)) ? FaceInk : (y == 18 && (x >= 11 && x <= 13 || x >= 19 && x <= 21)) ? new Color(0.25f, 0.18f, 0.12f) : Clear);
    public static Sprite EyesSlit() => FaceLayer("eyes_slit", (x, y) => EyePair(x, y, new Color(0.95f, 0.85f, 0.2f), FaceInk, true));
    public static Sprite EyesThree() => FaceLayer("eyes_three", (x, y) =>
    {
        Color c = EyePair(x, y, Color.white, FaceInk, false);
        if (c.a > 0f) return c;
        if (y >= 20 && y <= 21 && x >= 15 && x <= 17) return (x == 16) ? FaceInk : Color.white;
        return Clear;
    });
    public static Sprite EyesCyclops() => FaceLayer("eyes_cyclops", (x, y) =>
    {
        float d = new Vector2(x - 16f, (y - 16.5f) * 1.3f).magnitude;
        if (d < 1.3f) return FaceInk;
        if (d < 2.4f) return new Color(0.85f, 0.2f, 0.2f);
        if (d < 3.6f) return Color.white;
        if (d < 4.3f) return FaceInk;
        return Clear;
    });

    // Mouths at y = 10
    public static Sprite MouthA() => FaceLayer("mouth_a", (x, y) => (y == 10 && x >= 14 && x <= 18) || (y == 11 && (x == 13 || x == 19)) ? FaceInk : Clear);
    public static Sprite MouthB() => FaceLayer("mouth_b", (x, y) =>
        (y >= 9 && y <= 10 && x >= 15 && x <= 17) ? (y == 9 ? new Color(0.75f, 0.25f, 0.3f) : FaceInk) : Clear);
    public static Sprite MouthFangs() => FaceLayer("mouth_fangs", (x, y) =>
        (y == 10 && x >= 13 && x <= 19) ? FaceInk : ((y == 9 || y == 8) && (x == 14 || x == 18)) ? Color.white : Clear);

    // Hair (white, tinted): short, long, bun
    public static Sprite HairA() => FaceLayer("hair_a", (x, y) => InHead(x, y, false) && y >= 21 && !(y < 23 && x > 13 && x < 19) ? HairShade(x, y) : Clear);
    public static Sprite HairB() => FaceLayer("hair_b", (x, y) =>
        (InHead(x, y, false) || Mathf.Abs(x - 15.5f) <= 9f && y >= 8 && y <= 24) && (y >= 21 || x <= 9 || x >= 22) && !(y < 23 && x > 13 && x < 19) ? HairShade(x, y) : Clear);
    public static Sprite HairC() => FaceLayer("hair_c", (x, y) =>
        (InHead(x, y, false) && y >= 22) || new Vector2(x - 15.5f, y - 28f).magnitude < 3.2f ? HairShade(x, y) : Clear);

    // Monster extras
    public static Sprite Horns() => FaceLayer("horns", (x, y) =>
    {
        foreach (int side in new[] { -1, 1 })
        {
            float bx = 16f + side * 5f, t = (y - 23f) / 8f; // from the forehead up and out
            if (t < 0f || t > 1f) continue;
            float cx = bx + side * 3f * t, half = 1.6f * (1f - t);
            if (Mathf.Abs(x + 0.5f - cx) <= half + 0.2f) return t > 0.75f ? new Color(0.6f, 0.55f, 0.45f) : new Color(0.92f, 0.88f, 0.75f);
        }
        return Clear;
    });
    public static Sprite Antennae() => FaceLayer("antennae", (x, y) =>
    {
        if (new Vector2(x - 10.5f, y - 30f).magnitude < 1.5f || new Vector2(x - 21.5f, y - 30f).magnitude < 1.5f) return new Color(0.55f, 0.95f, 0.35f);
        if (y >= 24 && y <= 28 && (x == 12 + (28 - y) / 2 || x == 20 - (28 - y) / 2)) return FaceInk;
        return Clear;
    });
    public static Sprite Scales() => FaceLayer("scales", (x, y) =>
        InHead(x, y, false) && y < 22 && (x + y * 2) % 5 == 0 && y % 2 == 0 ? new Color(0.1f, 0.3f, 0.15f, 0.45f) : Clear);

    // Facial differences (human)
    public static Sprite DiffEyepatch() => FaceLayer("diff_eyepatch", (x, y) =>
        (x >= 11 && x <= 14 && y >= 15 && y <= 17) || (y == 18 + (x - 8) / 6 && x >= 8 && x <= 24) ? new Color(0.08f, 0.08f, 0.1f) : Clear);
    public static Sprite DiffScar() => FaceLayer("diff_scar", (x, y) =>
        (x >= 18 && x <= 22 && y == 13 + (x - 18)) || ((x == 19 || x == 21) && y == 14 + (x - 18)) ? new Color(0.8f, 0.45f, 0.45f) : Clear);
    public static Sprite DiffHearingAid() => FaceLayer("diff_hearingaid", (x, y) =>
        (x >= 24 && x <= 25 && y >= 13 && y <= 17) || (x == 23 && y == 15) ? new Color(0.85f, 0.75f, 0.6f) : Clear);
    public static Sprite DiffGlasses() => FaceLayer("diff_glasses", (x, y) =>
    {
        bool rimL = x >= 10 && x <= 15 && y >= 14 && y <= 18, rimR = x >= 17 && x <= 22 && y >= 14 && y <= 18;
        bool inL = x >= 11 && x <= 14 && y >= 15 && y <= 17, inR = x >= 18 && x <= 21 && y >= 15 && y <= 17;
        if (inL || inR) return new Color(0.7f, 0.85f, 1f, 0.35f);
        if (rimL || rimR || (y == 16 && x == 16)) return FaceInk;
        return Clear;
    });
    public static Sprite DiffBirthmark() => FaceLayer("diff_birthmark", (x, y) =>
        InHead(x, y, false) && new Vector2((x - 10f) * 0.9f, y - 12f).magnitude < 3.5f + Mathf.Sin(x * 1.7f + y) * 0.8f ? new Color(0.6f, 0.15f, 0.35f, 0.45f) : Clear);
    public static Sprite DiffVitiligo() => FaceLayer("diff_vitiligo", (x, y) =>
        InHead(x, y, false) && (new Vector2(x - 20f, y - 19f).magnitude < 2.6f || new Vector2(x - 12f, y - 9f).magnitude < 2.2f || new Vector2(x - 15f, y - 3f).magnitude < 1.8f)
            ? new Color(1f, 0.97f, 0.92f, 0.55f) : Clear);

    // Head shape: oval face, ears (round or pointy) and neck
    private static bool InHead(int x, int y, bool pointy)
    {
        float px = x + 0.5f, py = y + 0.5f;
        if (Mathf.Pow((px - 16f) / 7.5f, 2) + Mathf.Pow((py - 16f) / 10.5f, 2) <= 1f) return true;
        if (px >= 13f && px <= 19f && py <= 7f) return true; // neck
        foreach (int side in new[] { -1, 1 })
        {
            float ex = 16f + side * 7.8f;
            if (!pointy && new Vector2(px - ex, py - 15f).magnitude <= 2.6f) return true;
            if (pointy)
            {
                // Triangle from the side of the head up and out to a tip
                float t = (py - 12f) / 10f, reach = 1.5f + 4f * t;
                if (t >= 0f && t <= 1f && side * (px - 16f) >= 6.5f && side * (px - 16f) <= 6.5f + reach * (1f - t) + 1f) return true;
            }
        }
        return false;
    }

    private static bool HeadEdge(int x, int y, bool pointy)
    {
        if (InHead(x, y, pointy)) return false;
        return InHead(x + 1, y, pointy) || InHead(x - 1, y, pointy) || InHead(x, y + 1, pointy) || InHead(x, y - 1, pointy);
    }

    private static Color EyePair(int x, int y, Color white, Color pupil, bool slit)
    {
        bool left = x >= 12 && x <= 13, right = x >= 19 && x <= 20;
        if (!(left || right) || y < 15 || y > 17) return Clear;
        bool center = slit ? (x == 13 || x == 20) : (x == 13 || x == 20) && y == 16;
        if (slit && y == 17 && !center) return Clear;
        return center ? pupil : white;
    }

    private static Color HairShade(int x, int y) => (x + y) % 5 == 0 ? new Color(0.78f, 0.78f, 0.78f) : Color.white;

    private static Sprite FaceLayer(string key, System.Func<int, int, Color> paint)
    {
        if (cache.TryGetValue(key, out Sprite cached)) return cached;
        Texture2D tex = NewTexture(F, F);
        for (int y = 0; y < F; y++)
            for (int x = 0; x < F; x++)
                tex.SetPixel(x, y, paint(x, y));
        return Save(key, tex, 32f);
    }

    // ---------------- Fingerprint (last chance) ----------------

    // White fingerprint ridges (oval loops with a few breaks), filled bottom-up in the UI
    public static Sprite Fingerprint()
    {
        if (cache.TryGetValue("fingerprint", out Sprite cached)) return cached;
        const int w = 48, h = 60;
        Texture2D tex = NewTexture(w, h);
        Vector2 core = new Vector2(w / 2f, h * 0.55f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - core.x, (y + 0.5f - core.y) * 0.8f);
                float r = p.magnitude;
                // The outline of a fingertip: an oval, flat at the bottom
                Vector2 o = new Vector2((x + 0.5f - w / 2f) / (w / 2f - 1f), (y + 0.5f - h / 2f) / (h / 2f - 1f));
                if (o.sqrMagnitude > 1f || y < 3) continue;
                float angle = Mathf.Atan2(p.y, p.x);
                // Ridges every 3.4 px, wobbling a little; breaks at a few angles like a real print
                float ridge = Mathf.Repeat(r + 0.9f * Mathf.Sin(angle * 3f + r * 0.15f), 3.4f);
                bool gap = Mathf.Sin(angle * 5f + Mathf.Floor(r / 3.4f) * 1.7f) > 0.93f;
                if (ridge < 1.3f && !gap && r > 1.5f) tex.SetPixel(x, y, Color.white);
            }
        return Save("fingerprint", tex, 100f);
    }

    private static Texture2D NewTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Clear;
        tex.SetPixels(pixels);
        return tex;
    }

    private static void Fill(Texture2D tex, int x0, int y0, int x1, int y1, Color c)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                tex.SetPixel(x, y, c);
    }

    private static Sprite Save(string key, Texture2D tex, float pixelsPerUnit)
    {
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        sprite.name = key;
        cache[key] = sprite;
        return sprite;
    }
}
