using System.Collections.Generic;
using UnityEngine;

// Sprites pixel-art generados por código para la secuencia final (drones, balas, manchas).
// Así no hace falta importar arte nuevo; si después tienes sprites propios, se pueden asignar en EndingSequence.
public static class RuntimeSprites
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    private static readonly Color Clear = new Color(0, 0, 0, 0);
    private static readonly Color Outline = new Color(0.10f, 0.10f, 0.13f);
    private static readonly Color Body = new Color(0.45f, 0.48f, 0.55f);
    private static readonly Color BodyLight = new Color(0.65f, 0.68f, 0.75f);
    private static readonly Color Rotor = new Color(0.85f, 0.85f, 0.90f, 0.85f);
    private static readonly Color Led = new Color(1f, 0.15f, 0.15f);

    // Dron de 24x12 px. frame 0/1 cambia las hélices para animarlas.
    public static Sprite Drone(int frame)
    {
        string key = "drone" + frame;
        if (cache.TryGetValue(key, out Sprite cached)) return cached;

        Texture2D tex = NewTexture(24, 12);

        // Hélices
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

        // Mástiles de las hélices
        Fill(tex, 4, 9, 4, 10, Outline);
        Fill(tex, 19, 9, 19, 10, Outline);

        // Brazos
        Fill(tex, 2, 8, 21, 8, Outline);

        // Cuerpo
        Fill(tex, 7, 3, 16, 7, Outline);
        Fill(tex, 8, 4, 15, 6, Body);
        Fill(tex, 9, 6, 14, 6, BodyLight);

        // Luz roja
        Fill(tex, 14, 5, 14, 5, Led);

        // Cámara / cañón
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

    // Sprite blanco de 1x1 px (para cuerdas, banners, manchas…). Se colorea con SpriteRenderer.color.
    public static Sprite Pixel()
    {
        if (cache.TryGetValue("pixel", out Sprite cached)) return cached;
        Texture2D tex = NewTexture(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Save("pixel", tex, 1f);
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
