using UnityEngine;
using UnityEngine.UI;

// Fondo detrás del celular: la banqueta y la calle vistas desde arriba, recorriéndose en loop,
// como si fueras caminando por la calle mientras juegas en el celular.
// Si no se asigna una textura, se genera una pixel-art por código (tileable en vertical).
public class StreetBackground : MonoBehaviour
{
    [Tooltip("Textura propia (opcional). Debe repetirse en vertical. Si está vacía se genera una")]
    public Texture streetTexture;
    [Tooltip("Velocidad del recorrido (pantallas por segundo aprox.)")]
    public float scrollSpeed = 0.18f;
    [Tooltip("Balanceo lateral de los pasos")]
    public float swayAmount = 0.004f;
    public float stepsPerSecond = 1.8f;
    [Tooltip("Oculta lo que la Main Camera renderiza (los minijuegos), para que a los lados del celular solo se vea la calle")]
    public bool hideWorldOnMainCamera = true;

    private RawImage image;
    private Canvas canvas;
    private float offset;
    private Texture texture;

    void Start()
    {
        if (hideWorldOnMainCamera && Camera.main != null)
        {
            Camera.main.cullingMask = 0;
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Color.black;
        }

        GameObject canvasObj = new GameObject("Street Background (runtime)");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10; // Debajo del canvas del celular

        GameObject imgObj = new GameObject("Street", typeof(RectTransform));
        imgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform rt = imgObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        image = imgObj.AddComponent<RawImage>();
        image.raycastTarget = false;
        texture = streetTexture != null ? streetTexture : GenerateStreetTexture();
        texture.wrapMode = TextureWrapMode.Repeat;
        image.texture = texture;
    }

    public void SetVisible(bool visible)
    {
        if (canvas != null) canvas.enabled = visible;
    }

    void Update()
    {
        if (image == null) return;

        // Alto del recorte para que los pixeles queden cuadrados según la pantalla
        float screenAspect = (float)Screen.height / Mathf.Max(1, Screen.width);
        float texAspect = (float)texture.width / Mathf.Max(1, texture.height);
        float uvHeight = screenAspect * texAspect;

        offset = Mathf.Repeat(offset + scrollSpeed * uvHeight * Time.deltaTime, 1f);
        float sway = Mathf.Sin(Time.time * stepsPerSecond * Mathf.PI) * swayAmount;

        image.uvRect = new Rect(sway, offset, 1f, uvHeight);
    }

    // ---------------- Textura procedural ----------------
    // Columnas (de izquierda a derecha): calle con línea punteada | guarnición | banqueta | pared de ladrillo

    private const int W = 128;
    private const int H = 128;

    private static Texture2D GenerateStreetTexture()
    {
        System.Random rng = new System.Random(1234);
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        Color[] px = new Color[W * H];

        Color asphalt = new Color(0.20f, 0.20f, 0.23f);
        Color lane = new Color(0.92f, 0.85f, 0.45f);
        Color curb = new Color(0.62f, 0.62f, 0.60f);
        Color curbEdge = new Color(0.40f, 0.40f, 0.40f);
        Color concrete = new Color(0.66f, 0.64f, 0.60f);
        Color seam = new Color(0.48f, 0.46f, 0.43f);
        Color brick = new Color(0.52f, 0.24f, 0.18f);
        Color mortar = new Color(0.35f, 0.30f, 0.28f);

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                Color c;
                float noise = (float)rng.NextDouble() * 0.06f - 0.03f;

                if (x < 40) // Calle
                {
                    c = asphalt;
                    if (rng.NextDouble() < 0.04) noise -= 0.05f;
                    // Línea punteada (periodo de 64 para que repita bien)
                    if ((x == 14 || x == 15) && (y % 64) < 36) c = lane;
                }
                else if (x < 44) // Guarnición
                {
                    c = x == 40 ? curbEdge : curb;
                }
                else if (x < 112) // Banqueta
                {
                    c = concrete;
                    bool horizontalSeam = y % 32 == 0;
                    bool verticalSeam = x == 44 || x == 78;
                    if (horizontalSeam || verticalSeam) c = seam;
                }
                else // Pared de ladrillo
                {
                    int row = y / 4;
                    int brickX = (x - 112 + (row % 2 == 0 ? 0 : 4)) % 8;
                    c = (y % 4 == 0 || brickX == 0) ? mortar : brick;
                    if (x == 112) c = mortar * 0.7f;
                }

                c.r += noise; c.g += noise; c.b += noise; c.a = 1f;
                px[y * W + x] = c;
            }
        }

        // Grietas en la banqueta
        for (int i = 0; i < 5; i++)
        {
            int x = rng.Next(48, 108), y = rng.Next(0, H);
            for (int s = 0; s < 14; s++)
            {
                Set(px, x, y, seam * 0.85f);
                x += rng.Next(-1, 2);
                y += 1;
                x = Mathf.Clamp(x, 46, 110);
            }
        }

        // Chicles pegados
        for (int i = 0; i < 9; i++)
        {
            int x = rng.Next(48, 108), y = rng.Next(0, H);
            Color gum = new Color(0.35f, 0.34f, 0.33f);
            Set(px, x, y, gum); Set(px, x + 1, y, gum); Set(px, x, y + 1, gum); Set(px, x + 1, y + 1, gum * 0.9f);
        }

        // Hojas
        Color[] leaves = { new Color(0.85f, 0.45f, 0.15f), new Color(0.75f, 0.60f, 0.15f), new Color(0.35f, 0.55f, 0.20f) };
        for (int i = 0; i < 14; i++)
        {
            int x = rng.Next(44, 110), y = rng.Next(0, H);
            Color leaf = leaves[rng.Next(leaves.Length)];
            Set(px, x, y, leaf); Set(px, x + 1, y, leaf); Set(px, x, y + 1, leaf * 0.85f);
        }

        // Coladera junto a la guarnición
        for (int y = 70; y < 80; y++)
            for (int x = 33; x < 40; x++)
                Set(px, x, y, (y % 2 == 0) ? new Color(0.08f, 0.08f, 0.09f) : new Color(0.30f, 0.30f, 0.32f));

        // Alcantarilla en la calle
        DrawCircle(px, 26, 20, 6, new Color(0.28f, 0.27f, 0.27f), new Color(0.15f, 0.15f, 0.16f));

        // Árbol visto desde arriba (copa que tapa parte de la banqueta)
        DrawCircle(px, 52, 100, 12, new Color(0.18f, 0.42f, 0.18f), new Color(0.10f, 0.28f, 0.12f));
        DrawCircle(px, 49, 103, 6, new Color(0.26f, 0.52f, 0.22f), new Color(0.26f, 0.52f, 0.22f));

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static void DrawCircle(Color[] px, int cx, int cy, int r, Color fill, Color border)
    {
        for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                int d2 = x * x + y * y;
                if (d2 > r * r) continue;
                Set(px, cx + x, cy + y, d2 > (r - 1) * (r - 1) ? border : fill);
            }
    }

    // Escribe un pixel; en vertical da la vuelta para que la textura siga siendo tileable
    private static void Set(Color[] px, int x, int y, Color c)
    {
        if (x < 0 || x >= W) return;
        y = ((y % H) + H) % H;
        c.a = 1f;
        px[y * W + x] = c;
    }
}
