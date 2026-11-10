using UnityEngine;

// Textures that really are procedural: a different cracked screen on every "real life" death
// (random impact point and seed). Everything else, the blood splatters included, is a PNG in Resources/Sprites.
public static class ProceduralTextures
{
    // Cracked screen: cracks spreading from an impact point (large transparent texture)
    public static Texture2D ScreenCrack(Vector2 impact01, int seed)
    {
        const int w = 512, h = 288;
        Color[] px = new Color[w * h];
        System.Random rng = new System.Random(seed);
        Vector2 center = new Vector2(impact01.x * w, impact01.y * h);
        Color line = new Color(1f, 1f, 1f, 0.9f);
        Color shadow = new Color(0f, 0f, 0f, 0.5f);

        // Main rays with branches
        int rays = 16;
        for (int r = 0; r < rays; r++)
        {
            float angle = (r + (float)rng.NextDouble() * 0.6f) / rays * Mathf.PI * 2f;
            CrackRay(px, w, h, center, angle, 120 + rng.Next(260), rng, line, shadow, 2);
        }
        // Broken rings around the impact
        for (int ring = 1; ring <= 3; ring++)
        {
            float radius = ring * 26f + (float)rng.NextDouble() * 10f;
            for (float t = 0; t < Mathf.PI * 2f; t += 0.006f)
            {
                if (Mathf.Sin(t * 7f + ring) < -0.3f) continue; // segments
                float rr = radius + Mathf.Sin(t * 23f) * 3f;
                Plot(px, w, h, (int)(center.x + Mathf.Cos(t) * rr), (int)(center.y + Mathf.Sin(t) * rr), line, shadow);
            }
        }
        // Shattered impact point
        for (int y = -10; y <= 10; y++)
            for (int x = -10; x <= 10; x++)
                if (x * x + y * y < 90 && rng.NextDouble() < 0.5)
                    Plot(px, w, h, (int)center.x + x, (int)center.y + y, new Color(1f, 1f, 1f, 0.7f), shadow);

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static void CrackRay(Color[] px, int w, int h, Vector2 from, float angle, int length, System.Random rng, Color line, Color shadow, int depth)
    {
        Vector2 p = from;
        for (int i = 0; i < length; i++)
        {
            angle += ((float)rng.NextDouble() - 0.5f) * 0.25f;
            p += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Plot(px, w, h, (int)p.x, (int)p.y, line, shadow);
            if (depth > 0 && rng.NextDouble() < 0.012)
                CrackRay(px, w, h, p, angle + (rng.NextDouble() < 0.5 ? -0.8f : 0.8f), length / 3, rng, line, shadow, depth - 1);
        }
    }

    private static void Plot(Color[] px, int w, int h, int x, int y, Color c, Color shadow)
    {
        if (x < 1 || y < 1 || x >= w - 1 || y >= h - 1) return;
        if (px[(y - 1) * w + x + 1].a < shadow.a) px[(y - 1) * w + x + 1] = shadow;
        px[y * w + x] = c;
    }
}
