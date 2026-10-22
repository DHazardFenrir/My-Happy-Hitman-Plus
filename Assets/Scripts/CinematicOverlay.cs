using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Capa a pantalla completa para la secuencia final: look de cámara de vigilancia (CCTV),
// textos, fundidos a negro y créditos. Se construye por código la primera vez que se usa.
public class CinematicOverlay : MonoBehaviour
{
    public string cameraLabel = "CAM 07  -  5TH AVE & MAIN ST";
    public string networkLabel = "CITY SURVEILLANCE NETWORK";

    private Canvas canvas;
    private GameObject cctvRoot;
    private RawImage scanlines;
    private TextMeshProUGUI recText;
    private TextMeshProUGUI timeText;
    private Image fade;
    private TextMeshProUGUI caption;
    private CanvasGroup creditsGroup;
    private TextMeshProUGUI creditRole;
    private TextMeshProUGUI creditName;

    private float scanOffset;
    private System.DateTime fakeClock;

    public void Show()
    {
        if (canvas == null) Build();
        canvas.gameObject.SetActive(true);
        fakeClock = System.DateTime.Now;
    }

    public void Hide()
    {
        if (canvas != null) canvas.gameObject.SetActive(false);
    }

    public void SetCCTV(bool visible) => cctvRoot.SetActive(visible);

    public void SetFade(float alpha) => fade.color = new Color(0f, 0f, 0f, alpha);

    public IEnumerator FadeTo(float target, float duration)
    {
        float start = fade.color.a;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetFade(Mathf.Lerp(start, target, t / duration));
            yield return null;
        }
        SetFade(target);
    }

    // verticalPosition: 0 = abajo, 1 = arriba
    public void ShowCaption(string text, Color color, float verticalPosition, float size = 48f)
    {
        caption.text = text;
        caption.color = color;
        caption.fontSize = size;
        caption.rectTransform.anchorMin = new Vector2(0.05f, verticalPosition - 0.12f);
        caption.rectTransform.anchorMax = new Vector2(0.95f, verticalPosition + 0.12f);
        caption.rectTransform.offsetMin = caption.rectTransform.offsetMax = Vector2.zero;
        caption.gameObject.SetActive(true);
    }

    public void HideCaption() => caption.gameObject.SetActive(false);

    // Un crédito: aparece (fade in), se queda y desaparece (fade out)
    public IEnumerator ShowCredit(string role, string name, float hold = 2.2f, float fadeTime = 0.8f)
    {
        creditRole.text = role;
        creditName.text = name;
        creditsGroup.gameObject.SetActive(true);

        yield return FadeGroup(creditsGroup, 0f, 1f, fadeTime);
        yield return new WaitForSeconds(hold);
        yield return FadeGroup(creditsGroup, 1f, 0f, fadeTime);

        creditsGroup.gameObject.SetActive(false);
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }

    void Update()
    {
        if (canvas == null || !canvas.gameObject.activeSelf || !cctvRoot.activeSelf) return;

        // REC parpadeando y reloj corriendo
        recText.alpha = Mathf.Repeat(Time.time, 1f) < 0.6f ? 1f : 0f;
        fakeClock = fakeClock.AddSeconds(Time.deltaTime);
        timeText.text = fakeClock.ToString("yyyy-MM-dd   HH:mm:ss");

        // Scanlines bajando y algún parpadeo de señal
        scanOffset = Mathf.Repeat(scanOffset + Time.deltaTime * 0.6f, 1f);
        float lines = Screen.height / 3f;
        scanlines.uvRect = new Rect(0f, scanOffset, 1f, lines);
        scanlines.color = new Color(1f, 1f, 1f, Random.value < 0.03f ? 0.6f : 0.35f);
    }

    // ---------------- Construcción ----------------

    private void Build()
    {
        GameObject canvasObj = new GameObject("Cinematic Canvas (runtime)");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        // --- CCTV ---
        cctvRoot = new GameObject("CCTV", typeof(RectTransform));
        cctvRoot.transform.SetParent(canvasObj.transform, false);
        Stretch(cctvRoot.GetComponent<RectTransform>());

        // Tinte verdoso de cámara de seguridad
        Image tint = NewImage(cctvRoot.transform, "Tint", new Color(0.2f, 0.45f, 0.25f, 0.18f));
        Stretch(tint.rectTransform);

        scanlines = NewRawImage(cctvRoot.transform, "Scanlines", ScanlineTexture());
        Stretch(scanlines.rectTransform);

        RawImage vignette = NewRawImage(cctvRoot.transform, "Vignette", VignetteTexture());
        Stretch(vignette.rectTransform);

        // Esquinas tipo visor
        for (int corner = 0; corner < 4; corner++) CreateCornerBracket(cctvRoot.transform, corner);

        recText = NewText(cctvRoot.transform, "REC", "<color=#ff2a2a>●</color> REC", 34, Color.white, TextAlignmentOptions.TopLeft);
        Anchor(recText.rectTransform, new Vector2(0.06f, 0.86f), new Vector2(0.4f, 0.94f));

        TextMeshProUGUI camText = NewText(cctvRoot.transform, "Cam", cameraLabel, 26, Color.white, TextAlignmentOptions.TopRight);
        Anchor(camText.rectTransform, new Vector2(0.5f, 0.86f), new Vector2(0.94f, 0.94f));

        timeText = NewText(cctvRoot.transform, "Time", "", 26, Color.white, TextAlignmentOptions.BottomLeft);
        Anchor(timeText.rectTransform, new Vector2(0.06f, 0.06f), new Vector2(0.5f, 0.13f));

        TextMeshProUGUI netText = NewText(cctvRoot.transform, "Network", networkLabel, 22, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.BottomRight);
        Anchor(netText.rectTransform, new Vector2(0.5f, 0.06f), new Vector2(0.94f, 0.13f));

        // --- Textos grandes ---
        caption = NewText(canvasObj.transform, "Caption", "", 48, Color.white, TextAlignmentOptions.Center);
        caption.fontStyle = FontStyles.Bold;
        caption.outlineWidth = 0.25f;
        caption.outlineColor = Color.black;
        caption.gameObject.SetActive(false);

        // --- Fundido a negro (encima del CCTV, debajo de los créditos) ---
        fade = NewImage(canvasObj.transform, "Fade", new Color(0f, 0f, 0f, 0f));
        Stretch(fade.rectTransform);

        // --- Créditos ---
        GameObject credits = new GameObject("Credits", typeof(RectTransform), typeof(CanvasGroup));
        credits.transform.SetParent(canvasObj.transform, false);
        Stretch(credits.GetComponent<RectTransform>());
        creditsGroup = credits.GetComponent<CanvasGroup>();
        creditsGroup.alpha = 0f;

        creditRole = NewText(credits.transform, "Role", "", 30, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Center);
        creditRole.characterSpacing = 10f;
        Anchor(creditRole.rectTransform, new Vector2(0.1f, 0.52f), new Vector2(0.9f, 0.62f));

        creditName = NewText(credits.transform, "Name", "", 72, Color.white, TextAlignmentOptions.Center);
        creditName.fontStyle = FontStyles.Bold;
        Anchor(creditName.rectTransform, new Vector2(0.1f, 0.36f), new Vector2(0.9f, 0.52f));
        credits.SetActive(false);

        canvasObj.SetActive(false);
    }

    private void CreateCornerBracket(Transform parent, int corner)
    {
        Vector2 anchor = new Vector2(corner % 2 == 0 ? 0.04f : 0.96f, corner < 2 ? 0.95f : 0.05f);
        Vector2 dir = new Vector2(corner % 2 == 0 ? 1f : -1f, corner < 2 ? -1f : 1f);
        const float length = 60f, thickness = 4f;

        Image h = NewImage(parent, "Bracket H", Color.white);
        h.rectTransform.anchorMin = h.rectTransform.anchorMax = anchor;
        h.rectTransform.pivot = new Vector2(dir.x > 0 ? 0f : 1f, dir.y > 0 ? 0f : 1f);
        h.rectTransform.sizeDelta = new Vector2(length, thickness);
        h.rectTransform.anchoredPosition = Vector2.zero;

        Image v = NewImage(parent, "Bracket V", Color.white);
        v.rectTransform.anchorMin = v.rectTransform.anchorMax = anchor;
        v.rectTransform.pivot = h.rectTransform.pivot;
        v.rectTransform.sizeDelta = new Vector2(thickness, length);
        v.rectTransform.anchoredPosition = Vector2.zero;
    }

    private static Texture2D ScanlineTexture()
    {
        Texture2D tex = new Texture2D(1, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
        tex.SetPixel(0, 1, new Color(0f, 0f, 0f, 0f));
        tex.SetPixel(0, 2, new Color(0f, 0f, 0f, 0f));
        tex.Apply();
        return tex;
    }

    private static Texture2D VignetteTexture()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x / (size - 1f) * 2f - 1f, y / (size - 1f) * 2f - 1f);
                float a = Mathf.SmoothStep(0f, 0.85f, Mathf.InverseLerp(0.6f, 1.45f, p.magnitude));
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        tex.Apply();
        return tex;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Image NewImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static RawImage NewRawImage(Transform parent, string name, Texture texture)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RawImage img = go.GetComponent<RawImage>();
        img.texture = texture;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI NewText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
        return tmp;
    }
}
