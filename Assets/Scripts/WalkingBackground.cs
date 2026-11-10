using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The world behind the phone, seen from above while you walk and play: it scrolls in a loop.
// Each minigame has its own place (street, crosswalk, construction site, sewer, hotel). When the
// minigame changes you gradually walk into the new place: it comes in from the top of the screen.
public class WalkingBackground : MonoBehaviour
{
    [Tooltip("Optional custom texture for the plain street (must tile vertically). Leave empty to use the generated one")]
    public Texture streetTextureOverride;
    [Tooltip("Scroll speed (roughly screens per second)")]
    public float scrollSpeed = 0.18f;
    [Tooltip("How fast the new place comes in, relative to the scroll speed")]
    public float transitionSpeedMultiplier = 2f;
    [Tooltip("Side-to-side sway of your steps")]
    public float swayAmount = 0.004f;
    public float stepsPerSecond = 1.8f;
    [Tooltip("Stops the Main Camera from drawing the minigames, so only the background shows around the phone")]
    public bool hideWorldOnMainCamera = true;

    // Stops walking (the background freezes), e.g. when a car is about to hit you
    public bool Paused { get; set; }

    // Moves/shakes the whole background (used by the "real life" death)
    public RectTransform ShakeRoot { get; private set; }

    public WalkTheme CurrentTheme { get; private set; } = WalkTheme.Street;

    private Canvas canvas;
    private RawImage baseImage;
    private RectTransform incomingMask;
    private RawImage incomingImage;
    private Image threshold;
    private float offset;
    private Coroutine transition;

    void Start()
    {
        if (hideWorldOnMainCamera && Camera.main != null)
        {
            Camera.main.cullingMask = 0;
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Color.black;
        }

        GameObject canvasObj = new GameObject("Walking Background (runtime)");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10; // below the phone canvas

        // A bit bigger than the screen so it can shake without showing the edges
        GameObject root = new GameObject("Shake Root", typeof(RectTransform));
        root.transform.SetParent(canvasObj.transform, false);
        ShakeRoot = root.GetComponent<RectTransform>();
        ShakeRoot.anchorMin = Vector2.zero;
        ShakeRoot.anchorMax = Vector2.one;
        ShakeRoot.offsetMin = new Vector2(-40f, -40f);
        ShakeRoot.offsetMax = new Vector2(40f, 40f);

        baseImage = NewRawImage(ShakeRoot, "Current Place");
        Stretch(baseImage.rectTransform);
        baseImage.texture = TextureFor(CurrentTheme);

        // The next place lives inside a mask that grows from the top of the screen
        GameObject mask = new GameObject("Incoming Mask", typeof(RectTransform), typeof(RectMask2D));
        mask.transform.SetParent(ShakeRoot, false);
        incomingMask = mask.GetComponent<RectTransform>();
        incomingMask.anchorMin = new Vector2(0f, 1f);
        incomingMask.anchorMax = new Vector2(1f, 1f);
        incomingMask.pivot = new Vector2(0.5f, 1f);
        incomingMask.sizeDelta = Vector2.zero;

        incomingImage = NewRawImage(incomingMask, "Incoming Place");
        RectTransform irt = incomingImage.rectTransform;
        irt.anchorMin = new Vector2(0f, 1f);
        irt.anchorMax = new Vector2(1f, 1f);
        irt.pivot = new Vector2(0.5f, 1f);

        // Darker line where one place ends and the next begins (a doorway / edge)
        threshold = new GameObject("Threshold", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        threshold.transform.SetParent(ShakeRoot, false);
        threshold.color = new Color(0f, 0f, 0f, 0.45f);
        threshold.raycastTarget = false;
        threshold.rectTransform.anchorMin = new Vector2(0f, 1f);
        threshold.rectTransform.anchorMax = new Vector2(1f, 1f);
        threshold.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        mask.SetActive(false);
        threshold.gameObject.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (canvas != null) canvas.enabled = visible;
    }

    // Gradually walk into another place
    public void TransitionTo(WalkTheme theme)
    {
        if (baseImage == null || theme == CurrentTheme) return;
        if (transition != null) FinishTransition();
        transition = StartCoroutine(TransitionRoutine(theme));
    }

    // Switch instantly (no walking in)
    public void SnapTo(WalkTheme theme)
    {
        if (baseImage == null) return;
        if (transition != null) FinishTransition();
        CurrentTheme = theme;
        baseImage.texture = TextureFor(theme);
    }

    private WalkTheme pendingTheme;

    private IEnumerator TransitionRoutine(WalkTheme theme)
    {
        pendingTheme = theme;
        incomingImage.texture = TextureFor(theme);
        incomingMask.gameObject.SetActive(true);
        threshold.gameObject.SetActive(true);

        float height = ShakeRoot.rect.height;
        incomingImage.rectTransform.sizeDelta = new Vector2(0f, height);
        float revealed = 0f;
        while (revealed < 1f)
        {
            if (!Paused) revealed += scrollSpeed * transitionSpeedMultiplier * Time.deltaTime;
            float h = Mathf.Clamp01(revealed) * height;
            incomingMask.sizeDelta = new Vector2(0f, h);
            threshold.rectTransform.sizeDelta = new Vector2(0f, Mathf.Max(4f, height * 0.006f));
            threshold.rectTransform.anchoredPosition = new Vector2(0f, -h);
            yield return null;
        }
        FinishTransition();
    }

    private void FinishTransition()
    {
        if (transition != null) StopCoroutine(transition);
        transition = null;
        CurrentTheme = pendingTheme;
        baseImage.texture = TextureFor(pendingTheme);
        incomingMask.gameObject.SetActive(false);
        threshold.gameObject.SetActive(false);
    }

    void Update()
    {
        if (baseImage == null) return;

        // Height of the visible slice so the pixels stay square on any screen
        float screenAspect = (float)Screen.height / Mathf.Max(1, Screen.width);
        float uvHeight = screenAspect * WalkThemeTextures.W / (float)WalkThemeTextures.H;

        if (!Paused) offset = Mathf.Repeat(offset + scrollSpeed * uvHeight * Time.deltaTime, 1f);
        float sway = Paused ? 0f : Mathf.Sin(Time.time * stepsPerSecond * Mathf.PI) * swayAmount;

        Rect uv = new Rect(sway, offset, 1f, uvHeight);
        baseImage.uvRect = uv;

        if (incomingMask.gameObject.activeSelf)
        {
            // The incoming image is screen-sized and pinned to the top, so it must sample the same slice
            incomingImage.uvRect = uv;
        }
    }

    private Texture TextureFor(WalkTheme theme)
    {
        Texture tex = theme == WalkTheme.Street && streetTextureOverride != null
            ? streetTextureOverride
            : WalkThemeTextures.Get(theme);
        tex.wrapMode = TextureWrapMode.Repeat;
        return tex;
    }

    private static RawImage NewRawImage(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RawImage img = go.GetComponent<RawImage>();
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
