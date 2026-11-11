using System.Collections.Generic;
using UnityEngine;

// The original top-down pixel-art of the walking backgrounds (128x128, tileable vertically), painted
// in code. EDITOR ONLY: Tools > Agent TR8N1 > Bake missing sprites saves each one as a PNG in
// Assets/Resources/Sprites/Walk; from then on the PNG is the real art.
// The phone covers the middle of the screen, so the interesting details live on the left and right sides.
public static class WalkThemePainter
{
    public const int W = 128;
    public const int H = 128;

    private static readonly Dictionary<WalkTheme, Texture2D> cache = new Dictionary<WalkTheme, Texture2D>();

    public static Texture2D Get(WalkTheme theme)
    {
        if (cache.TryGetValue(theme, out Texture2D cached)) return cached;

        Painter p = new Painter(1234 + (int)theme);
        switch (theme)
        {
            case WalkTheme.Crosswalk: PaintCrosswalk(p); break;
            case WalkTheme.Construction: PaintConstruction(p); break;
            case WalkTheme.Sewer: PaintSewer(p); break;
            case WalkTheme.Hotel: PaintHotel(p); break;
            default: PaintStreet(p); break;
        }

        Texture2D tex = p.ToTexture();
        cache[theme] = tex;
        return tex;
    }

    // ---------------- Street: road | curb | sidewalk | brick wall ----------------

    private static void PaintStreet(Painter p)
    {
        Color asphalt = new Color(0.20f, 0.20f, 0.23f);
        Color lane = new Color(0.92f, 0.85f, 0.45f);

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (x < 40) p.SetNoisy(x, y, asphalt, p.Chance(0.04f) ? -0.08f : 0f);
                else if (x < 44) p.SetNoisy(x, y, x == 40 ? new Color(0.40f, 0.40f, 0.40f) : new Color(0.62f, 0.62f, 0.60f));
                else if (x < 112) p.SetNoisy(x, y, (y % 32 == 0 || x == 44 || x == 78) ? new Color(0.48f, 0.46f, 0.43f) : new Color(0.66f, 0.64f, 0.60f));
                else p.SetNoisy(x, y, BrickColor(x - 112, y, new Color(0.52f, 0.24f, 0.18f), new Color(0.35f, 0.30f, 0.28f)));
            }

        // Dashed lane line (period 64 so it tiles)
        for (int y = 0; y < H; y++)
            if (y % 64 < 36) { p.Set(14, y, lane); p.Set(15, y, lane); }
        for (int y = 0; y < H; y++) p.Set(112, y, new Color(0.25f, 0.21f, 0.20f));

        SidewalkDetails(p, 46, 110);

        // Storm drain by the curb, manhole on the road, tree from above
        for (int y = 70; y < 80; y++)
            for (int x = 33; x < 40; x++)
                p.Set(x, y, y % 2 == 0 ? new Color(0.08f, 0.08f, 0.09f) : new Color(0.30f, 0.30f, 0.32f));
        p.Circle(26, 20, 6, new Color(0.28f, 0.27f, 0.27f), new Color(0.15f, 0.15f, 0.16f));
        p.Circle(52, 100, 12, new Color(0.18f, 0.42f, 0.18f), new Color(0.10f, 0.28f, 0.12f));
        p.Circle(49, 103, 6, new Color(0.26f, 0.52f, 0.22f), new Color(0.26f, 0.52f, 0.22f));
    }

    private static void SidewalkDetails(Painter p, int xMin, int xMax)
    {
        Color crack = new Color(0.41f, 0.39f, 0.36f);
        for (int i = 0; i < 5; i++)
        {
            int x = p.Range(xMin + 2, xMax - 2), y = p.Range(0, H);
            for (int s = 0; s < 14; s++)
            {
                p.Set(x, y, crack);
                x = Mathf.Clamp(x + p.Range(-1, 2), xMin, xMax);
                y++;
            }
        }
        Color gum = new Color(0.35f, 0.34f, 0.33f);
        for (int i = 0; i < 9; i++)
        {
            int x = p.Range(xMin + 2, xMax - 2), y = p.Range(0, H);
            p.Rect(x, y, x + 1, y + 1, gum);
        }
        Color[] leaves = { new Color(0.85f, 0.45f, 0.15f), new Color(0.75f, 0.60f, 0.15f), new Color(0.35f, 0.55f, 0.20f) };
        for (int i = 0; i < 14; i++)
        {
            int x = p.Range(xMin, xMax), y = p.Range(0, H);
            Color leaf = leaves[p.Range(0, leaves.Length)];
            p.Set(x, y, leaf); p.Set(x + 1, y, leaf); p.Set(x, y + 1, leaf * 0.85f);
        }
    }

    // ---------------- Crosswalk: same street, with a zebra crossing and a traffic light ----------------

    private static void PaintCrosswalk(Painter p)
    {
        PaintStreet(p);
        Color asphalt = new Color(0.20f, 0.20f, 0.23f);
        Color paint = new Color(0.88f, 0.88f, 0.84f);

        // Zebra band across the road, with the lane line interrupted
        for (int y = 44; y < 84; y++)
            for (int x = 0; x < 40; x++)
            {
                bool bar = x % 8 < 5 && y > 46 && y < 81;
                p.SetNoisy(x, y, bar ? paint : asphalt);
            }

        // Lowered curb with yellow tactile paving
        Color tactile = new Color(0.90f, 0.75f, 0.25f);
        for (int y = 46; y < 82; y++)
        {
            for (int x = 40; x < 44; x++) p.SetNoisy(x, y, new Color(0.62f, 0.62f, 0.60f));
            for (int x = 44; x < 52; x++) p.Set(x, y, (x + y) % 2 == 0 ? tactile : tactile * 0.8f);
        }

        // Traffic light pole seen from above (pole + signal box)
        p.Circle(48, 92, 3, new Color(0.25f, 0.25f, 0.28f), new Color(0.12f, 0.12f, 0.14f));
        p.Rect(44, 95, 51, 100, new Color(0.15f, 0.15f, 0.15f));
        p.Rect(45, 97, 46, 98, new Color(0.95f, 0.15f, 0.15f));
        p.Rect(48, 97, 49, 98, new Color(0.30f, 0.90f, 0.35f));
    }

    // ---------------- Construction site: cones on the road | plank walkway with hazard tape | fence and dirt ----------------

    private static void PaintConstruction(Painter p)
    {
        Color asphalt = new Color(0.22f, 0.21f, 0.22f);
        Color wood = new Color(0.72f, 0.54f, 0.33f);
        Color seam = new Color(0.45f, 0.32f, 0.18f);
        Color dirt = new Color(0.48f, 0.36f, 0.23f);

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (x < 40) p.SetNoisy(x, y, asphalt, p.Chance(0.05f) ? -0.07f : 0f);
                else if (x < 44) p.SetNoisy(x, y, new Color(0.62f, 0.62f, 0.60f));
                else if (x < 111) p.SetNoisy(x, y, (y % 8 == 0 || (x - 44) % 22 == 0) ? seam : wood, amount: 0.04f);
                else p.SetNoisy(x, y, dirt, p.Chance(0.08f) ? 0.1f : 0f);
            }

        // Nails on the planks
        for (int y = 3; y < H; y += 8)
            for (int x = 47; x < 110; x += 22) { p.Set(x, y, seam * 0.6f); p.Set(x + 18, y + 2, seam * 0.6f); }

        // Hazard tape along both edges of the walkway
        Color yellow = new Color(0.98f, 0.82f, 0.10f), black = new Color(0.08f, 0.08f, 0.08f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < 3; x++)
            {
                p.Set(44 + x, y, ((y + x) / 3) % 2 == 0 ? yellow : black);
                p.Set(108 + x, y, ((y + x) / 3) % 2 == 0 ? yellow : black);
            }

        // Red and white barrier blocks on the road
        for (int y = 0; y < H; y++)
            for (int x = 33; x < 39; x++)
                p.Set(x, y, (y / 8) % 2 == 0 ? new Color(0.85f, 0.12f, 0.10f) : new Color(0.92f, 0.92f, 0.90f));

        // Traffic cones from above
        foreach (int cy in new[] { 18, 60, 102 })
        {
            p.Circle(18, cy, 5, new Color(1f, 0.45f, 0.05f), new Color(0.75f, 0.28f, 0.02f));
            p.Circle(18, cy, 3, new Color(0.95f, 0.95f, 0.92f), new Color(0.95f, 0.95f, 0.92f));
            p.Circle(18, cy, 1, new Color(1f, 0.45f, 0.05f), new Color(1f, 0.45f, 0.05f));
        }

        // Chain-link fence (posts + diamond mesh), gravel and a sand pile on the dirt
        Color post = new Color(0.55f, 0.55f, 0.58f), mesh = new Color(0.68f, 0.68f, 0.70f);
        for (int y = 0; y < H; y++)
        {
            p.Set(111, y, post);
            p.Set(112, y, post * 0.8f);
            for (int x = 113; x < 118; x++)
                if ((x + y) % 4 == 0 || (x - y + 256) % 4 == 0) p.Set(x, y, mesh);
        }
        for (int y = 0; y < H; y += 32) p.Rect(110, y, 113, y + 2, post * 0.7f);
        for (int i = 0; i < 40; i++) p.Set(p.Range(118, 128), p.Range(0, H), new Color(0.62f, 0.58f, 0.52f));
        p.Circle(122, 70, 8, new Color(0.82f, 0.70f, 0.45f), new Color(0.70f, 0.58f, 0.36f));
    }

    // ---------------- Sewer: the street, but old and worn, with drains everywhere ----------------
    // (the trap is falling down a drain, so you walk along a run-down street full of them).
    // Starts from the street as it is painted now (Walk/Street.png) so it keeps the same style.

    private const string StreetPng = "Assets/Resources/Sprites/Walk/Street.png";

    private static void PaintSewer(Painter p)
    {
        if (System.IO.File.Exists(StreetPng))
        {
            Texture2D street = new Texture2D(2, 2);
            street.LoadImage(System.IO.File.ReadAllBytes(StreetPng));
            p.Load(street);
        }
        else PaintStreet(p);

        // Old: faded, a bit darker, with brown grime
        Color grime = new Color(0.42f, 0.37f, 0.28f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color c = p.Get(x, y);
                float gray = c.grayscale;
                c = Color.Lerp(c, new Color(gray, gray, gray), 0.3f) * 0.88f;
                c = Color.Lerp(c, grime, p.Chance(0.15f) ? 0.25f : 0.1f);
                p.Set(x, y, c);
            }

        // The lane line is almost gone: only bits of paint left
        for (int y = 0; y < H; y++)
            for (int x = 0; x < 40; x++)
            {
                Color c = p.Get(x, y);
                bool paint = c.r > 0.25f && c.r - c.b > 0.12f;
                if (paint && (y / 3 + x) % 4 != 0) p.Set(x, y, Color.Lerp(c, new Color(0.17f, 0.17f, 0.18f), 0.85f));
            }

        // Potholes on the road, more cracks everywhere and a missing sidewalk slab
        Color holeDark = new Color(0.08f, 0.08f, 0.09f), holeRim = new Color(0.30f, 0.29f, 0.27f);
        Blob(p, 12, 58, 5, 3, holeRim, holeDark);
        Blob(p, 28, 112, 4, 3, holeRim, holeDark);
        Color crack = new Color(0.22f, 0.21f, 0.19f);
        for (int i = 0; i < 12; i++)
        {
            int x = p.Range(2, 110), y = p.Range(0, H);
            for (int s = 0; s < p.Range(8, 20); s++)
            {
                p.Set(x, y, crack);
                x = Mathf.Clamp(x + p.Range(-1, 2), 0, 110);
                y++;
            }
        }
        for (int y = 100; y < 126; y++)
            for (int x = 82; x < 108; x++)
                p.SetNoisy(x, y, p.Chance(0.2f) ? new Color(0.50f, 0.44f, 0.34f) : new Color(0.38f, 0.32f, 0.24f), amount: 0.05f);

        // Drains: storm grates along the curb (with a dark wet stain and a puddle),
        // manholes on the road and on the sidewalk
        foreach (int gy in new[] { 6, 92 }) // the street already has a big grate in the middle
        {
            for (int y = gy - 3; y < gy + 13; y++)
                for (int x = 30; x < 46; x++)
                    p.Tint(x, y, new Color(0.10f, 0.12f, 0.12f), 0.35f * (1f - Mathf.Abs(y - gy - 5) / 9f));
            for (int y = gy; y < gy + 10; y++)
                for (int x = 33; x < 40; x++)
                    p.Set(x, y, y % 2 == 0 ? new Color(0.06f, 0.06f, 0.07f) : new Color(0.34f, 0.32f, 0.30f));
            p.Rect(32, gy - 1, 40, gy - 1, new Color(0.20f, 0.19f, 0.18f));
            p.Rect(32, gy + 10, 40, gy + 10, new Color(0.20f, 0.19f, 0.18f));
            Blob(p, 24, gy + 5, 4, 2, new Color(0.22f, 0.26f, 0.30f), new Color(0.27f, 0.32f, 0.37f)); // puddle
        }
        foreach (Vector2Int m in new[] { new Vector2Int(18, 28), new Vector2Int(96, 66) })
        {
            p.Circle(m.x, m.y, 6, new Color(0.30f, 0.29f, 0.27f), new Color(0.14f, 0.14f, 0.15f));
            for (int y = m.y - 3; y <= m.y + 3; y++)
                for (int x = m.x - 3; x <= m.x + 3; x++)
                    if ((x + y) % 3 == 0) p.Set(x, y, new Color(0.20f, 0.19f, 0.18f));
            for (int i = 0; i < 6; i++) p.Set(m.x + p.Range(-5, 6), m.y + p.Range(-5, 6), new Color(0.45f, 0.30f, 0.18f)); // rust
        }

        // Moss and weeds in the cracks by the wall
        for (int i = 0; i < 40; i++) p.Set(p.Range(44, 112), p.Range(0, H), p.Chance(0.5f) ? new Color(0.24f, 0.36f, 0.16f) : new Color(0.30f, 0.44f, 0.18f));
    }

    // A rough oval (potholes, puddles)
    private static void Blob(Painter p, int cx, int cy, int rx, int ry, Color border, Color fill)
    {
        for (int y = -ry - 1; y <= ry + 1; y++)
            for (int x = -rx - 1; x <= rx + 1; x++)
            {
                float d = (x * x) / (float)(rx * rx) + (y * y) / (float)(ry * ry);
                if (d <= 0.6f) p.Set(cx + x, cy + y, fill);
                else if (d <= 1.15f) p.Set(cx + x, cy + y, border);
            }
    }

    // ---------------- Hotel: wall with doors | wood floor | carpet runner | wood floor | wall with doors ----------------

    private static void PaintHotel(Painter p)
    {
        Color paper = new Color(0.45f, 0.10f, 0.14f), paperStripe = new Color(0.36f, 0.07f, 0.11f);
        Color baseboard = new Color(0.22f, 0.12f, 0.06f);
        Color wood = new Color(0.48f, 0.30f, 0.16f), woodSeam = new Color(0.36f, 0.22f, 0.11f);
        Color carpet = new Color(0.50f, 0.09f, 0.12f), gold = new Color(0.82f, 0.64f, 0.24f);

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color c;
                if (x < 14 || x >= 116) c = (x % 6 < 3) ? paper : paperStripe;
                else if (x < 16 || (x >= 114 && x < 116)) c = baseboard;
                else if (x < 36 || (x >= 104 && x < 114)) c = ((y + (x / 5) * 7) % 12 == 0 || x % 5 == 0) ? woodSeam : wood;
                else
                {
                    c = carpet;
                    if (x == 38 || x == 39 || x == 100 || x == 101) c = gold;
                    // Diamonds down the middle of the runner
                    int dy = Mathf.Abs((y % 16) - 8), dx = Mathf.Abs(x - 70);
                    if (dx + dy == 6 || dx + dy == 3) c = gold;
                    if ((x == 52 || x == 88) && y % 4 < 2) c = gold * 0.8f;
                }
                p.SetNoisy(x, y, c, amount: 0.025f);
            }

        // Room doors on both walls (left ones offset from the right ones), with number plates and knobs
        DrawDoor(p, 1, 10, 13, 0);
        DrawDoor(p, 1, 74, 13, 0);
        DrawDoor(p, 117, 42, 127, 1);
        DrawDoor(p, 117, 106, 127, 1);

        // Warm light pools from wall sconces
        foreach (int cy in new[] { 40, 104 })
            for (int y = cy - 8; y < cy + 8; y++)
                for (int x = 16; x < 26; x++)
                    p.Tint(x, y, new Color(1f, 0.85f, 0.55f), 0.18f * (1f - Mathf.Abs(y - cy) / 8f));
        foreach (int cy in new[] { 8, 72 })
            for (int y = cy - 8; y < cy + 8; y++)
                for (int x = 104; x < 114; x++)
                    p.Tint(x, y, new Color(1f, 0.85f, 0.55f), 0.18f * (1f - Mathf.Abs(y - cy) / 8f));
    }

    private static void DrawDoor(Painter p, int x0, int y0, int x1, int knobSide)
    {
        Color door = new Color(0.36f, 0.20f, 0.11f), frame = new Color(0.20f, 0.11f, 0.05f), gold = new Color(0.85f, 0.68f, 0.28f);
        p.Rect(x0, y0, x1, y0 + 22, frame);
        p.Rect(x0 + 1, y0 + 1, x1 - 1, y0 + 21, door);
        int plateX = (x0 + x1) / 2 - 1;
        p.Rect(plateX, y0 + 16, plateX + 2, y0 + 18, gold);
        int knobX = knobSide == 0 ? x1 - 2 : x0 + 2;
        p.Set(knobX, y0 + 10, gold);
    }

    // ---------------- Helpers ----------------

    private static Color BrickColor(int x, int y, Color brick, Color mortar)
    {
        int row = y / 4;
        int bx = (x + (row % 2 == 0 ? 0 : 4)) % 8;
        return (y % 4 == 0 || bx == 0) ? mortar : brick;
    }

    private class Painter
    {
        private readonly Color[] px = new Color[W * H];
        private readonly System.Random rng;

        public Painter(int seed) { rng = new System.Random(seed); }

        public bool Chance(float p) => rng.NextDouble() < p;
        public int Range(int min, int maxExclusive) => rng.Next(min, maxExclusive);

        // Writes a pixel; wraps vertically so the texture keeps tiling
        public void Set(int x, int y, Color c)
        {
            if (x < 0 || x >= W) return;
            y = ((y % H) + H) % H;
            c.a = 1f;
            px[y * W + x] = c;
        }

        // Color with a little random brightness noise. shift darkens/brightens on top of the noise.
        public void SetNoisy(int x, int y, Color c, float shift = 0f, float amount = 0.03f)
        {
            float n = (float)rng.NextDouble() * amount * 2f - amount + shift;
            Set(x, y, new Color(c.r + n, c.g + n, c.b + n));
        }

        public Color Get(int x, int y) => px[(((y % H) + H) % H) * W + Mathf.Clamp(x, 0, W - 1)];

        // Starts from an existing 128x128 picture
        public void Load(Texture2D tex)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    px[y * W + x] = tex.GetPixel(x * tex.width / W, y * tex.height / H);
        }

        public void Tint(int x, int y, Color tint, float amount)
        {
            if (x < 0 || x >= W) return;
            int wy = ((y % H) + H) % H;
            Set(x, y, Color.Lerp(px[wy * W + x], tint, amount));
        }

        public void Rect(int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Set(x, y, c);
        }

        public void Circle(int cx, int cy, int r, Color fill, Color border)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    int d2 = x * x + y * y;
                    if (d2 > r * r) continue;
                    Set(cx + x, cy + y, d2 > (r - 1) * (r - 1) ? border : fill);
                }
        }

        public Texture2D ToTexture()
        {
            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
