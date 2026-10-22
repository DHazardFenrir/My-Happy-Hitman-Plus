using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Construye por código el menú de inicio, la card del objetivo y el letrero de GOOD JOB / FAILED.
// No hace falta armar nada en la escena: el GameManager lo crea al iniciar.
public class GameUI : MonoBehaviour
{
    private TMP_FontAsset font;
    private RectTransform screenRect;   // El área de la pantalla del teléfono (GameScreen)

    private GameObject menuRoot;
    private GameObject howToPanel;
    private Action onPlay;

    private RectTransform resultCard;      // Fondo oscuro que tapa toda la pantalla del celular
    private CanvasGroup neonSign;          // Letrero (texto + marco) que parpadea
    private TextMeshProUGUI resultTitle;
    private TextMeshProUGUI resultSubtitle;
    private Image[] neonBorder;

    private RectTransform targetCard;
    private TextMeshProUGUI targetShirt;
    private TextMeshProUGUI targetPants;


    public void Build(Canvas mainCanvas, RectTransform gameScreenRect, TMP_FontAsset uiFont, Action playCallback)
    {
        font = uiFont;
        screenRect = gameScreenRect;
        onPlay = playCallback;

        EnsureEventSystem();

        // Canvas propio para el menú: siempre encima de todo
        GameObject menuCanvasObj = new GameObject("Menu Canvas (runtime)");
        Canvas menuCanvas = menuCanvasObj.AddComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = 100;
        CanvasScaler scaler = menuCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);
        scaler.matchWidthOrHeight = 0.5f;
        menuCanvasObj.AddComponent<GraphicRaycaster>();

        BuildMenu(menuCanvasObj.transform);

        // Las cards van encima de la pantalla del teléfono.
        // Si no se encontró GameScreen, cubren toda la pantalla.
        Transform overlayParent = screenRect != null ? screenRect.parent : menuCanvasObj.transform;
        if (overlayParent == null && mainCanvas != null) overlayParent = mainCanvas.transform;

        BuildTargetCard(overlayParent);
        BuildResultCard(overlayParent);

        menuRoot.transform.SetAsLastSibling();
    }

    // ---------------- MENÚ DE INICIO ----------------

    private void BuildMenu(Transform parent)
    {
        menuRoot = CreatePanel(parent, "Start Menu", new Color(0.07f, 0.06f, 0.10f, 0.82f));
        Stretch(menuRoot.GetComponent<RectTransform>());

        TextMeshProUGUI title = CreateText(menuRoot.transform, "Title", "MY HAPPY\nHITMAN", 64, new Color(1f, 0.25f, 0.25f));
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(700, 180));

        TextMeshProUGUI subtitle = CreateText(menuRoot.transform, "Subtitle", "Very, very unfortunate accidents", 22, new Color(0.9f, 0.9f, 0.9f));
        Place(subtitle.rectTransform, new Vector2(0.5f, 0.53f), new Vector2(700, 40));

        Button play = CreateButton(menuRoot.transform, "Play Button", "PLAY", new Color(0.85f, 0.15f, 0.15f));
        Place(play.GetComponent<RectTransform>(), new Vector2(0.5f, 0.38f), new Vector2(260, 60));
        play.onClick.AddListener(PressPlay);

        Button howTo = CreateButton(menuRoot.transform, "HowTo Button", "HOW TO PLAY", new Color(0.25f, 0.25f, 0.32f));
        Place(howTo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.26f), new Vector2(260, 50));
        howTo.onClick.AddListener(() => howToPanel.SetActive(!howToPanel.activeSelf));

        Button quit = CreateButton(menuRoot.transform, "Quit Button", "QUIT", new Color(0.25f, 0.25f, 0.32f));
        Place(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.15f), new Vector2(260, 50));
        quit.onClick.AddListener(Quit);

        TextMeshProUGUI hint = CreateText(menuRoot.transform, "Hint", "SPACE / CLICK to play", 16, new Color(1f, 1f, 1f, 0.5f));
        Place(hint.rectTransform, new Vector2(0.5f, 0.05f), new Vector2(600, 30));

        // Panel de instrucciones
        howToPanel = CreatePanel(menuRoot.transform, "HowTo Panel", new Color(0f, 0f, 0f, 0.92f));
        Place(howToPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(560, 380));
        TextMeshProUGUI howToText = CreateText(howToPanel.transform, "Text",
            "<b>HOW TO PLAY</b>\n\n" +
            "Before each minigame a card shows your <color=#ff5555>TARGET</color>: " +
            "the color of their shirt and pants.\n\n" +
            "Press <b>SPACE</b> or <b>CLICK</b> at the right moment to spring the trap.\n\n" +
            "Hit the target: <color=#55ff55>+100</color>.\nMiss: on to the next minigame.\n\n" +
            "Reach <b>1000</b> points to finish the job.\n\n<size=70%>(click to close)</size>",
            20, Color.white);
        Stretch(howToText.rectTransform, 24);
        Button closeHowTo = howToPanel.AddComponent<Button>();
        closeHowTo.onClick.AddListener(() => howToPanel.SetActive(false));
        howToPanel.SetActive(false);

        menuRoot.SetActive(false);
    }

    public bool IsMenuVisible => menuRoot != null && menuRoot.activeSelf;

    public void ShowMenu()
    {
        menuRoot.SetActive(true);
        howToPanel.SetActive(false);
    }

    public void HideMenu() => menuRoot.SetActive(false);

    private void Update()
    {
        // Atajo de teclado en el menú (el click ya lo manejan los botones)
        if (IsMenuVisible && !howToPanel.activeSelf &&
            (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            PressPlay();
        }
    }

    private void PressPlay()
    {
        if (!IsMenuVisible) return;
        HideMenu();
        onPlay?.Invoke();
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------- CARD DEL OBJETIVO ----------------
    // Un solo color de alto contraste (amarillo con contorno negro sobre fondo oscuro),
    // cada línea se auto-ajusta al ancho de la pantalla del celular.

    private static readonly Color TargetColor = new Color(1f, 0.86f, 0.1f);

    private void BuildTargetCard(Transform parent)
    {
        GameObject card = CreatePanel(parent, "Target Card (runtime)", new Color(0.04f, 0.03f, 0.06f, 1f));
        targetCard = card.GetComponent<RectTransform>();
        MatchScreen(targetCard, 1f, 1f);

        // Marco amarillo: 4 líneas delgadas (solo el borde, el centro queda vacío para el texto)
        GameObject frame = new GameObject("Frame", typeof(RectTransform));
        frame.transform.SetParent(card.transform, false);
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.05f, 0.2f);
        frameRect.anchorMax = new Vector2(0.95f, 0.85f);
        frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
        CreateBorderLines(frame.transform, TargetColor, 3f);

        TextMeshProUGUI header = CreateTargetLine(card.transform, "Header", "TARGET", 0.72f, 0.82f, 30f);
        header.color = Color.white;
        header.characterSpacing = 12f;

        targetShirt = CreateTargetLine(card.transform, "Shirt", "RED SHIRT", 0.52f, 0.66f, 44f);
        TextMeshProUGUI and = CreateTargetLine(card.transform, "And", "&", 0.46f, 0.52f, 24f);
        and.color = Color.white;
        targetPants = CreateTargetLine(card.transform, "Pants", "BLACK PANTS", 0.32f, 0.46f, 44f);

        TextMeshProUGUI hint = CreateTargetLine(card.transform, "Hint", "SPACE / CLICK AT THE RIGHT TIME", 0.08f, 0.14f, 14f);
        hint.color = new Color(1f, 1f, 1f, 0.6f);
        hint.outlineWidth = 0f;

        card.SetActive(false);
    }

    private static void CreateBorderLines(Transform parent, Color color, float thickness)
    {
        for (int i = 0; i < 4; i++)
        {
            RectTransform rt = CreatePanel(parent, "Border " + i, color).GetComponent<RectTransform>();
            rt.GetComponent<Image>().raycastTarget = false;
            switch (i)
            {
                case 0: rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.sizeDelta = new Vector2(0, thickness); break;
                case 1: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.sizeDelta = new Vector2(0, thickness); break;
                case 2: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.sizeDelta = new Vector2(thickness, 0); break;
                default: rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1); rt.sizeDelta = new Vector2(thickness, 0); break;
            }
            rt.anchoredPosition = Vector2.zero;
        }
    }

    private TextMeshProUGUI CreateTargetLine(Transform parent, string name, string text, float yMin, float yMax, float maxSize)
    {
        TextMeshProUGUI line = CreateText(parent, name, text, maxSize, TargetColor);
        line.fontStyle = FontStyles.Bold;
        line.enableWordWrapping = false;
        line.enableAutoSizing = true;
        line.fontSizeMin = 8f;
        line.fontSizeMax = maxSize;
        line.outlineColor = Color.black;
        line.outlineWidth = 0.3f;
        line.rectTransform.anchorMin = new Vector2(0.1f, yMin);
        line.rectTransform.anchorMax = new Vector2(0.9f, yMax);
        line.rectTransform.offsetMin = line.rectTransform.offsetMax = Vector2.zero;
        return line;
    }

    public IEnumerator ShowTargetCard(string shirt, string pants, float duration)
    {
        targetShirt.text = shirt;
        targetPants.text = pants;
        targetCard.SetAsLastSibling();
        targetCard.gameObject.SetActive(true);

        // Entra con un "golpe" y luego late para llamar la atención
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float pop = t < 0.15f ? Mathf.Lerp(1.25f, 1f, t / 0.15f) : 1f + Mathf.Sin(t * 9f) * 0.025f;
            targetShirt.rectTransform.localScale = targetPants.rectTransform.localScale = Vector3.one * pop;
            yield return null;
        }

        targetCard.gameObject.SetActive(false);
    }

    // ---------------- LETRERO DE NEÓN GOOD JOB / FAILED ----------------

    private static readonly Color NeonGood = new Color(0.15f, 1f, 0.45f);
    private static readonly Color NeonFail = new Color(1f, 0.12f, 0.35f);

    private void BuildResultCard(Transform parent)
    {
        // Fondo opaco: el minijuego NO se ve detrás del letrero
        GameObject card = CreatePanel(parent, "Result Neon (runtime)", new Color(0.03f, 0.02f, 0.05f, 1f));
        resultCard = card.GetComponent<RectTransform>();
        MatchScreen(resultCard, 1f, 1f);

        // Letrero centrado
        GameObject sign = new GameObject("Neon Sign", typeof(RectTransform), typeof(CanvasGroup));
        sign.transform.SetParent(card.transform, false);
        RectTransform signRect = sign.GetComponent<RectTransform>();
        signRect.anchorMin = new Vector2(0.06f, 0.36f);
        signRect.anchorMax = new Vector2(0.94f, 0.64f);
        signRect.offsetMin = signRect.offsetMax = Vector2.zero;
        neonSign = sign.GetComponent<CanvasGroup>();

        // Marco del letrero (4 tubos de neón)
        neonBorder = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject tube = CreatePanel(sign.transform, "Tube " + i, Color.white);
            RectTransform rt = tube.GetComponent<RectTransform>();
            const float thickness = 3f;
            switch (i)
            {
                case 0: rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.sizeDelta = new Vector2(0, thickness); break; // arriba
                case 1: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.sizeDelta = new Vector2(0, thickness); break; // abajo
                case 2: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.sizeDelta = new Vector2(thickness, 0); break; // izquierda
                default: rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1); rt.sizeDelta = new Vector2(thickness, 0); break; // derecha
            }
            rt.anchoredPosition = Vector2.zero;

            // Halo alrededor del tubo
            Outline glow = tube.AddComponent<Outline>();
            glow.effectDistance = new Vector2(3f, 3f);
            Shadow glow2 = tube.AddComponent<Shadow>();
            glow2.effectDistance = new Vector2(-3f, -3f);
            neonBorder[i] = tube.GetComponent<Image>();
        }

        resultTitle = CreateText(sign.transform, "Title", "GOOD JOB!", 40, Color.white);
        resultTitle.fontStyle = FontStyles.Bold | FontStyles.Italic;
        resultTitle.enableWordWrapping = false;
        resultTitle.enableAutoSizing = true;
        resultTitle.fontSizeMin = 10;
        resultTitle.fontSizeMax = 44;
        resultTitle.rectTransform.anchorMin = new Vector2(0.04f, 0.1f);
        resultTitle.rectTransform.anchorMax = new Vector2(0.96f, 0.9f);
        resultTitle.rectTransform.offsetMin = resultTitle.rectTransform.offsetMax = Vector2.zero;

        resultSubtitle = CreateText(card.transform, "Subtitle", "+100", 18, Color.white);
        resultSubtitle.rectTransform.anchorMin = new Vector2(0f, 0.22f);
        resultSubtitle.rectTransform.anchorMax = new Vector2(1f, 0.32f);
        resultSubtitle.rectTransform.offsetMin = resultSubtitle.rectTransform.offsetMax = Vector2.zero;

        card.SetActive(false);
    }

    private void ApplyNeonColor(Color neon)
    {
        // Núcleo casi blanco con borde y brillo del color del neón
        resultTitle.color = Color.Lerp(neon, Color.white, 0.55f);
        resultTitle.outlineColor = neon;
        resultTitle.outlineWidth = 0.18f;

        Material m = resultTitle.fontMaterial; // instancia propia, no toca el material compartido
        if (m.HasProperty("_GlowColor"))
        {
            m.EnableKeyword("GLOW_ON");
            m.SetColor("_GlowColor", new Color(neon.r, neon.g, neon.b, 0.9f));
            m.SetFloat("_GlowOffset", 0.1f);
            m.SetFloat("_GlowInner", 0.1f);
            m.SetFloat("_GlowOuter", 0.75f);
            m.SetFloat("_GlowPower", 0.9f);
        }
        if (m.HasProperty("_UnderlayColor"))
        {
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(neon.r, neon.g, neon.b, 0.6f));
            m.SetFloat("_UnderlaySoftness", 1f);
            m.SetFloat("_UnderlayDilate", 0.7f);
        }
        resultTitle.UpdateMeshPadding();

        foreach (Image tube in neonBorder)
        {
            tube.color = Color.Lerp(neon, Color.white, 0.4f);
            foreach (Shadow glow in tube.GetComponents<Shadow>())
            {
                glow.effectColor = new Color(neon.r, neon.g, neon.b, 0.45f);
            }
        }

        resultSubtitle.color = new Color(neon.r, neon.g, neon.b, 0.85f);
    }

    public IEnumerator ShowResultCard(bool success, string subtitle, float duration)
    {
        ApplyNeonColor(success ? NeonGood : NeonFail);
        resultTitle.text = success ? "GOOD JOB!" : "FAILED";
        resultSubtitle.text = subtitle;
        resultSubtitle.alpha = 0f;

        resultCard.SetAsLastSibling();
        resultCard.gameObject.SetActive(true);

        // Encendido del neón: chispazos antes de quedarse prendido
        float[] startup = { 0.06f, 0.08f, 0.05f, 0.18f, 0.04f, 0.06f };
        for (int i = 0; i < startup.Length; i++)
        {
            neonSign.alpha = i % 2 == 0 ? 1f : 0.08f;
            yield return new WaitForSeconds(startup[i]);
        }
        neonSign.alpha = 1f;
        resultSubtitle.alpha = 1f;

        // Prendido, con zumbido y parpadeos al azar (FAILED parpadea más, como letrero descompuesto)
        float flickerChance = success ? 0.6f : 2.5f; // parpadeos por segundo
        float t = 0f;
        while (t < duration)
        {
            float dt = Time.deltaTime;
            t += dt;

            if (UnityEngine.Random.value < flickerChance * dt)
            {
                neonSign.alpha = UnityEngine.Random.Range(0.05f, 0.35f);
                float off = UnityEngine.Random.Range(0.03f, 0.09f);
                yield return new WaitForSeconds(off);
                t += off;
            }

            neonSign.alpha = 0.88f + Mathf.Sin(t * 40f) * 0.06f + UnityEngine.Random.Range(-0.04f, 0.04f);
            yield return null;
        }

        // Se apaga
        neonSign.alpha = 0.1f;
        yield return new WaitForSeconds(0.05f);
        resultCard.gameObject.SetActive(false);
    }

    // ---------------- HELPERS ----------------

    // Coloca el rect sobre la pantalla del teléfono, con un tamaño relativo a ella
    private void MatchScreen(RectTransform rt, float widthFactor, float heightFactor, float verticalPosition = 0.5f)
    {
        if (screenRect == null)
        {
            rt.anchorMin = new Vector2(0.5f - widthFactor / 2f, verticalPosition - heightFactor / 2f);
            rt.anchorMax = new Vector2(0.5f + widthFactor / 2f, verticalPosition + heightFactor / 2f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return;
        }

        rt.anchorMin = screenRect.anchorMin;
        rt.anchorMax = screenRect.anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 size = screenRect.rect.size;
        Vector2 screenCenter = screenRect.anchoredPosition + Vector2.Scale(new Vector2(0.5f, 0.5f) - screenRect.pivot, size);
        rt.sizeDelta = new Vector2(size.x * widthFactor, size.y * heightFactor);
        rt.anchoredPosition = screenCenter + new Vector2(0f, (verticalPosition - 0.5f) * size.y);
    }

    private static void Stretch(RectTransform rt, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button CreateButton(Transform parent, string name, string label, Color color)
    {
        GameObject go = CreatePanel(parent, name, color);
        Button button = go.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        button.colors = colors;

        TextMeshProUGUI text = CreateText(go.transform, "Label", label, 26, Color.white);
        text.fontStyle = FontStyles.Bold;
        Stretch(text.rectTransform);
        return button;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }
}
