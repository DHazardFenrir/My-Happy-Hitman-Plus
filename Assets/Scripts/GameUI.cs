using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Builds in code the start menu, the target card, the GOOD JOB / FAILED sign and the retro ad.
// Nothing needs to be set up in the scene: the GameManager creates it on start.
public class GameUI : MonoBehaviour
{
    private TMP_FontAsset font;
    private RectTransform screenRect;   // The phone screen area (GameScreen)

    private GameObject menuRoot;
    private GameObject howToPanel;
    private GameObject[] howToPages;
    private Image[] howToDots;
    private TextMeshProUGUI howToFooter, howToDrone;
    private int howToPage;
    private Action onPlay;

    private RectTransform resultCard;      // Dark background covering the whole phone screen
    private CanvasGroup neonSign;          // Flickering sign (text + frame)
    private TextMeshProUGUI resultTitle;
    private TextMeshProUGUI resultSubtitle;
    private Image[] neonBorder;

    private RectTransform targetCard;
    private TextMeshProUGUI targetShirt;
    private TextMeshProUGUI targetHeader, targetConnector, targetHint;
    private TextMeshProUGUI targetPants;


    // Container with the whole phone (frame, screen, score, cards). Can be moved/rotated to throw it.
    public RectTransform PhoneRig { get; private set; }

    public void Build(Canvas mainCanvas, RectTransform gameScreenRect, TMP_FontAsset uiFont, Action playCallback)
    {
        font = uiFont;
        screenRect = gameScreenRect;
        onPlay = playCallback;

        EnsureEventSystem();
        if (mainCanvas != null)
        {
            if (mainCanvas.GetComponent<GraphicRaycaster>() == null) mainCanvas.gameObject.AddComponent<GraphicRaycaster>();
            BuildPhoneRig(mainCanvas);
        }

        // Everything goes inside the phone screen (full screen if GameScreen wasn't found)
        Transform overlayParent = screenRect != null ? screenRect.parent : null;
        if (overlayParent == null) overlayParent = BuildFallbackCanvas().transform;

        BuildTargetCard(overlayParent);
        BuildActionCard(overlayParent);
        BuildDroneCard(overlayParent);
        BuildLoadingCard(overlayParent);
        BuildResultCard(overlayParent);
        BuildFingerprintCard(overlayParent);
        BuildAd(overlayParent);
        BuildMenu(overlayParent);
    }

    // Moves every child of the phone canvas into a container that fills it
    // (the layout doesn't change), so the whole phone can be animated.
    private void BuildPhoneRig(Canvas mainCanvas)
    {
        GameObject rigObj = new GameObject("Phone Rig (runtime)", typeof(RectTransform));
        PhoneRig = rigObj.GetComponent<RectTransform>();
        PhoneRig.SetParent(mainCanvas.transform, false);
        Stretch(PhoneRig);

        // Copied to a list first to keep the draw order
        var children = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in mainCanvas.transform)
            if (child != PhoneRig) children.Add(child);
        foreach (Transform child in children) child.SetParent(PhoneRig, false);
    }

    public void ResetPhoneRig()
    {
        if (PhoneRig == null) return;
        PhoneRig.anchoredPosition = Vector2.zero;
        PhoneRig.localRotation = Quaternion.identity;
        PhoneRig.localScale = Vector3.one;
    }

    private GameObject BuildFallbackCanvas()
    {
        GameObject canvasObj = new GameObject("UI Canvas (runtime)");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);
        canvasObj.AddComponent<GraphicRaycaster>();
        return canvasObj;
    }

    // ---------------- START MENU ----------------
    // Lives inside the phone screen; everything uses relative anchors and auto-sizing,
    // so nothing spills out of the frame at any resolution.

    private void BuildMenu(Transform parent)
    {
        menuRoot = CreatePanel(parent, "Start Menu", new Color(0.07f, 0.06f, 0.10f, 1f));
        MatchVisibleScreen(menuRoot.GetComponent<RectTransform>());

        TextMeshProUGUI title = CreateText(menuRoot.transform, "Title", "AGENT\n<color=#ffd23f>TR8N1</color>", 40, new Color(1f, 0.25f, 0.25f));
        title.fontStyle = FontStyles.Bold;
        FitInside(title, new Vector2(0.06f, 0.64f), new Vector2(0.94f, 0.92f), 10f, 40f);
        title.enableWordWrapping = false;

        TextMeshProUGUI subtitle = CreateText(menuRoot.transform, "Subtitle", "Drone Operator Training Program", 14, new Color(0.9f, 0.9f, 0.9f));
        FitInside(subtitle, new Vector2(0.06f, 0.56f), new Vector2(0.94f, 0.62f), 6f, 14f);

        Button play = CreateButton(menuRoot.transform, "Play Button", "PLAY", new Color(0.85f, 0.15f, 0.15f));
        AnchorBox(play.GetComponent<RectTransform>(), new Vector2(0.15f, 0.40f), new Vector2(0.85f, 0.50f));
        play.onClick.AddListener(PressPlay);

        Button howTo = CreateButton(menuRoot.transform, "HowTo Button", "HOW TO PLAY", new Color(0.25f, 0.25f, 0.32f));
        AnchorBox(howTo.GetComponent<RectTransform>(), new Vector2(0.15f, 0.28f), new Vector2(0.85f, 0.36f));
        howTo.onClick.AddListener(OpenHowTo);

        Button quit = CreateButton(menuRoot.transform, "Quit Button", "QUIT", new Color(0.25f, 0.25f, 0.32f));
        AnchorBox(quit.GetComponent<RectTransform>(), new Vector2(0.15f, 0.16f), new Vector2(0.85f, 0.24f));
        quit.onClick.AddListener(Quit);

        TextMeshProUGUI hint = CreateText(menuRoot.transform, "Hint", "SPACE / CLICK to play", 11, new Color(1f, 1f, 1f, 0.5f));
        FitInside(hint, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.1f), 6f, 11f);

        BuildHowTo(menuRoot.transform);
        menuRoot.SetActive(false);
    }

    // ---------------- HOW TO PLAY ----------------
    // A short training manual, one page at a time: the job, the one button, the score.
    // SPACE / CLICK turns the page; after the last one it goes back to the menu.

    private static readonly Color ManualYellow = new Color(1f, 0.82f, 0.25f);

    private void BuildHowTo(Transform parent)
    {
        howToPanel = CreatePanel(parent, "HowTo Panel", new Color(0.05f, 0.05f, 0.08f, 0.98f));
        Stretch(howToPanel.GetComponent<RectTransform>());
        Button next = howToPanel.AddComponent<Button>();
        next.transition = Selectable.Transition.None;
        next.navigation = new Navigation { mode = Navigation.Mode.None };
        next.onClick.AddListener(NextHowToPage);

        TextMeshProUGUI manual = CreateText(howToPanel.transform, "Manual", "DRONE OPERATOR TRAINING MANUAL", 12f, new Color(1f, 1f, 1f, 0.5f));
        FitInside(manual, new Vector2(0.05f, 0.93f), new Vector2(0.95f, 0.97f), 5f, 12f);
        manual.enableWordWrapping = false;

        howToPages = new GameObject[3];
        howToPages[0] = HowToPage(0, "THE JOB",
            "You train combat drones.\n\n" +
            "Before every round a card shows the <color=#ff5555>TARGET</color>: the color of their shirt and pants " +
            "(sometimes a car, or a phone).\n\n<b>Find them. Only them.</b>");
        howToDrone = CreateText(howToPages[0].transform, "Drone", "", 14f, ManualYellow);
        FitInside(howToDrone, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.24f), 6f, 14f);
        HowToIcon(howToPages[0], GameSprites.Drone(0), Color.white, 0.5f, 0.24f, "");

        howToPages[1] = HowToPage(1, "ONE BUTTON",
            "<b>SPACE</b>, <b>CLICK</b> or <b>TAP</b> does everything.\n\n" +
            "<color=#ffd23f>TAP</color> at the right moment: drop the piano, open the sewer, fire.\n" +
            "<color=#ffd23f>HOLD & RELEASE</color>: the scan grows while you hold.\n" +
            "<color=#ffd23f>MASH</color>: break the locks, fill the fingerprint.\n\n" +
            "<color=#ffd23f>RC CAR</color>: steer with <b>A / D</b> or the arrows.");
        HowToIcon(howToPages[1], GameSprites.Crosshair(), new Color(1f, 0.35f, 0.35f), 0.2f, 0.16f, "TAP");
        HowToIcon(howToPages[1], GameSprites.ScanRing(), new Color(0.35f, 0.95f, 1f), 0.5f, 0.16f, "HOLD");
        HowToIcon(howToPages[1], GameSprites.Fingerprint(), new Color(0.45f, 0.65f, 1f), 0.8f, 0.16f, "MASH");

        howToPages[2] = HowToPage(2, "THE SCORE",
            "Hit the target: <color=#55ff55>+100</color>.\nMiss: on to the next target.\n" +
            "Reach <b>1000</b> to certify your drone.\n\n" +
            "<color=#45d0ff>The AI makes mistakes. Check its work.</color>\n\n" +
            "Fail too often and you'll need\nto prove it's really you.\n<size=85%>Don't fail too often.</size>");
        HowToIcon(howToPages[2], GameSprites.Fingerprint(), new Color(1f, 1f, 1f, 0.85f), 0.5f, 0.2f, "");

        // Page dots and what the button does next
        howToDots = new Image[howToPages.Length];
        for (int i = 0; i < howToDots.Length; i++)
        {
            howToDots[i] = CreatePanel(howToPanel.transform, "Dot " + (i + 1), Color.white).GetComponent<Image>();
            float x = 0.5f + (i - (howToDots.Length - 1) / 2f) * 0.06f;
            AnchorBox(howToDots[i].rectTransform, new Vector2(x - 0.012f, 0.1f), new Vector2(x + 0.012f, 0.115f));
            howToDots[i].raycastTarget = false;
        }
        howToFooter = CreateText(howToPanel.transform, "Footer", "", 11f, new Color(1f, 1f, 1f, 0.55f));
        FitInside(howToFooter, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.08f), 5f, 11f);
        howToFooter.enableWordWrapping = false;

        howToPanel.SetActive(false);
    }

    // A page: big title under the manual line, a picture area and the text
    private GameObject HowToPage(int index, string title, string body)
    {
        GameObject page = new GameObject("Page " + (index + 1) + " " + title, typeof(RectTransform));
        page.transform.SetParent(howToPanel.transform, false);
        Stretch(page.GetComponent<RectTransform>());

        TextMeshProUGUI head = CreateText(page.transform, "Title", title, 30f, new Color(1f, 0.25f, 0.25f));
        head.fontStyle = FontStyles.Bold;
        head.enableWordWrapping = false;
        FitInside(head, new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.92f), 8f, 30f);

        TextMeshProUGUI text = CreateText(page.transform, "Body", body, 15f, Color.white);
        FitInside(text, new Vector2(0.07f, 0.26f), new Vector2(0.93f, 0.62f), 6f, 15f);
        return page;
    }

    // A picture in the band under the title, with an optional label under it
    private void HowToIcon(GameObject page, Sprite sprite, Color color, float x, float halfWidth, string label)
    {
        bool labeled = label.Length > 0;
        Image img = CardImage(page.transform, "Icon", sprite, new Vector2(x - halfWidth, labeled ? 0.7f : 0.65f), new Vector2(x + halfWidth, 0.82f));
        img.color = color;
        if (!labeled) return;
        TextMeshProUGUI text = CreateText(page.transform, "Icon Label", label, 13f, ManualYellow);
        text.fontStyle = FontStyles.Bold;
        text.enableWordWrapping = false;
        FitInside(text, new Vector2(x - halfWidth, 0.64f), new Vector2(x + halfWidth, 0.69f), 5f, 13f);
    }

    private void OpenHowTo()
    {
        if (howToPanel.activeSelf) return;
        howToDrone.text = $"YOUR DRONE: {AssignedDrone.Name}";
        howToPanel.transform.SetAsLastSibling();
        SetMenuItemsVisible(false);
        howToPanel.SetActive(true);
        ShowHowToPage(0);
        SoundFX.Play(Sfx.MenuSelect);
    }

    private void NextHowToPage()
    {
        if (!howToPanel.activeSelf) return;
        if (howToPage + 1 < howToPages.Length) ShowHowToPage(howToPage + 1);
        else CloseHowTo();
        SoundFX.Play(Sfx.MenuSelect, 0.6f);
    }

    private void CloseHowTo()
    {
        howToPanel.SetActive(false);
        SetMenuItemsVisible(true);
    }

    // The title and buttons showed through the manual's panel: they hide while it's open
    private void SetMenuItemsVisible(bool visible)
    {
        foreach (Transform child in menuRoot.transform)
            if (child.gameObject != howToPanel) child.gameObject.SetActive(visible);
    }

    private void ShowHowToPage(int index)
    {
        howToPage = index;
        for (int i = 0; i < howToPages.Length; i++)
        {
            howToPages[i].SetActive(i == index);
            howToDots[i].color = i == index ? ManualYellow : new Color(1f, 1f, 1f, 0.25f);
        }
        howToFooter.text = index + 1 < howToPages.Length ? "SPACE / CLICK  >  NEXT" : "SPACE / CLICK  >  BACK TO MENU";
        // Nothing stays selected, so SPACE never "clicks" a button on top of turning the page
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private static void AnchorBox(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void FitInside(TextMeshProUGUI text, Vector2 min, Vector2 max, float minSize, float maxSize)
    {
        AnchorBox(text.rectTransform, min, max);
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
    }

    public bool IsMenuVisible => menuRoot != null && menuRoot.activeSelf;

    public void ShowMenu()
    {
        menuRoot.transform.SetAsLastSibling();
        menuRoot.SetActive(true);
        CloseHowTo();
    }

    public void HideMenu() => menuRoot.SetActive(false);

    public void PressPlayFromCode() => PressPlay();

    private void Update()
    {
        // The visible part of the phone changes with the window size
        if (Screen.width != layoutScreenSize.x || Screen.height != layoutScreenSize.y)
        {
            layoutScreenSize = new Vector2Int(Screen.width, Screen.height);
            foreach (RectTransform rt in visibleLayouts) LayoutVisible(rt);
        }

        // Keyboard shortcut on the menu (clicks are handled by the buttons): play, or turn the manual's page
        if (IsMenuVisible && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            if (howToPanel.activeSelf) NextHowToPage();
            else PressPlay();
        }
    }

    private void PressPlay()
    {
        if (!IsMenuVisible) return;
        HideMenu();
        SoundFX.Play(Sfx.MenuSelect);
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

    // ---------------- TARGET CARD ----------------
    // A single high-contrast color (yellow with a black outline on a dark background);
    // each line auto-sizes to the width of the phone screen.

    private static readonly Color TargetColor = new Color(1f, 0.86f, 0.1f);

    private void BuildTargetCard(Transform parent)
    {
        GameObject card = CreatePanel(parent, "Target Card (runtime)", new Color(0.04f, 0.03f, 0.06f, 1f));
        targetCard = card.GetComponent<RectTransform>();
        MatchVisibleScreen(targetCard);

        // Yellow frame: 4 thin lines (just the border, the middle stays empty for the text)
        GameObject frame = new GameObject("Frame", typeof(RectTransform));
        frame.transform.SetParent(card.transform, false);
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.05f, 0.2f);
        frameRect.anchorMax = new Vector2(0.95f, 0.85f);
        frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
        CreateBorderLines(frame.transform, TargetColor, 3f);

        targetHeader = CreateTargetLine(card.transform, "Header", "TARGET", 0.72f, 0.82f, 30f);
        TextMeshProUGUI header = targetHeader;
        header.color = Color.white;
        header.characterSpacing = 12f;

        targetShirt = CreateTargetLine(card.transform, "Shirt", "RED SHIRT", 0.52f, 0.66f, 44f);
        targetConnector = CreateTargetLine(card.transform, "And", "&", 0.46f, 0.52f, 24f);
        TextMeshProUGUI and = targetConnector;
        and.color = Color.white;
        targetPants = CreateTargetLine(card.transform, "Pants", "BLACK PANTS", 0.32f, 0.46f, 44f);

        targetHint = CreateTargetLine(card.transform, "Hint", "SPACE / CLICK AT THE RIGHT TIME", 0.08f, 0.14f, 14f);
        TextMeshProUGUI hint = targetHint;
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

    public IEnumerator ShowTargetCard(StageTarget target, float duration)
    {
        targetHeader.text = target.header ?? "TARGET";
        targetShirt.text = target.line1;
        targetConnector.text = target.connector ?? "";
        targetPants.text = target.line2;
        targetHint.text = target.hint ?? "";
        SoundFX.Play(Sfx.TargetAlert);
        targetCard.SetAsLastSibling();
        targetCard.gameObject.SetActive(true);

        // Comes in with a "punch" and then pulses to grab attention
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

    // ---------------- ACTION CARD ----------------
    // WarioWare-style: one big verb right before the minigame (SHOOT!, DROP!, MASH!...). It slams in,
    // shakes a little and is gone: what to do, in one word, while your eyes are still free.

    private RectTransform actionCard;
    private TextMeshProUGUI actionVerb;

    private void BuildActionCard(Transform parent)
    {
        GameObject card = CreatePanel(parent, "Action Card (runtime)", new Color(0.85f, 0.12f, 0.15f, 1f));
        actionCard = card.GetComponent<RectTransform>();
        MatchVisibleScreen(actionCard);

        GameObject frame = new GameObject("Frame", typeof(RectTransform));
        frame.transform.SetParent(card.transform, false);
        AnchorBox(frame.GetComponent<RectTransform>(), new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.7f));
        CreateBorderLines(frame.transform, Color.white, 4f);

        actionVerb = CreateTargetLine(card.transform, "Verb", "SHOOT!", 0.36f, 0.64f, 90f);
        actionVerb.outlineWidth = 0.35f;
        card.SetActive(false);
    }

    public IEnumerator ShowActionCard(string verb, float duration)
    {
        if (string.IsNullOrEmpty(verb) || duration <= 0f) yield break;
        actionVerb.text = verb;
        SoundFX.Play(Sfx.MenuSelect, 1.2f);
        actionCard.SetAsLastSibling();
        actionCard.gameObject.SetActive(true);

        RectTransform rt = actionVerb.rectTransform;
        float tilt = UnityEngine.Random.value < 0.5f ? -6f : 6f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            // Slams in big and tilted, settles, then trembles
            float k = Mathf.Clamp01(t / 0.12f);
            float shake = t > 0.12f ? Mathf.Sin(t * 60f) * 1.5f : 0f;
            rt.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, k * k);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(tilt * 2f, tilt * 0.4f, k) + shake);
            yield return null;
        }
        actionCard.gameObject.SetActive(false);
    }

    // ---------------- LOADING CARD ----------------
    // "Preparing..." card with an icon and a progress bar that fills up (the RC car minigame uses it
    // to get you ready and remind the controls before the road shows up)

    private RectTransform loadingCard;
    private TextMeshProUGUI loadingTitle, loadingSubtitle, loadingHint;
    private Image loadingIcon;
    private RectTransform loadingFill;

    private void BuildLoadingCard(Transform parent)
    {
        GameObject card = CreatePanel(parent, "Loading Card (runtime)", new Color(0.04f, 0.03f, 0.06f, 1f));
        loadingCard = card.GetComponent<RectTransform>();
        MatchVisibleScreen(loadingCard);

        loadingTitle = CreateTargetLine(card.transform, "Title", "", 0.74f, 0.88f, 34f);
        TextGlow.Apply(loadingTitle, new Color(1f, 0.45f, 0.05f), 5f, 0.35f, 3f);
        loadingIcon = CardImage(card.transform, "Icon", null, new Vector2(0.3f, 0.47f), new Vector2(0.7f, 0.72f));
        AddSilhouetteOutline(loadingIcon, Color.white, 2f);

        // Progress bar: yellow frame, dark track, yellow fill growing from the left
        GameObject frame = CreatePanel(card.transform, "Bar Frame", new Color(1f, 0.86f, 0.1f));
        AnchorBox(frame.GetComponent<RectTransform>(), new Vector2(0.12f, 0.38f), new Vector2(0.88f, 0.43f));
        GameObject track = CreatePanel(frame.transform, "Track", new Color(0.08f, 0.06f, 0.1f));
        AnchorBox(track.GetComponent<RectTransform>(), new Vector2(0.015f, 0.15f), new Vector2(0.985f, 0.85f));
        GameObject fill = CreatePanel(track.transform, "Fill", new Color(1f, 0.86f, 0.1f));
        loadingFill = fill.GetComponent<RectTransform>();
        AnchorBox(loadingFill, Vector2.zero, new Vector2(0f, 1f));

        loadingSubtitle = CreateTargetLine(card.transform, "Subtitle", "", 0.27f, 0.35f, 22f);
        loadingSubtitle.color = Color.white;
        loadingHint = CreateTargetLine(card.transform, "Hint", "", 0.17f, 0.24f, 16f);
        loadingHint.color = new Color(1f, 0.86f, 0.1f);

        card.SetActive(false);
    }

    public IEnumerator ShowLoadingCard(string title, string subtitle, string hint, Sprite icon, float duration)
    {
        if (loadingCard == null) yield break;
        loadingTitle.text = title;
        loadingSubtitle.text = subtitle;
        loadingHint.text = hint;
        loadingIcon.sprite = icon;
        loadingIcon.enabled = icon != null;
        loadingCard.SetAsLastSibling();
        loadingCard.gameObject.SetActive(true);
        SoundFX.Play(Sfx.MenuSelect);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            // Fills in uneven steps, like a real loading bar, and the icon idles
            float k = Mathf.Clamp01(t / (duration * 0.85f));
            float stepped = Mathf.Floor(k * 12f) / 12f;
            loadingFill.anchorMax = new Vector2(Mathf.Max(stepped, k * k), 1f);
            loadingIcon.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t * 20f) * 1.5f, Mathf.Abs(Mathf.Sin(t * 8f)) * 4f);
            loadingTitle.alpha = k < 1f && Mathf.Repeat(t, 0.8f) > 0.6f ? 0.6f : 1f;
            yield return null;
        }

        loadingCard.gameObject.SetActive(false);
    }

    // ---------------- FINGERPRINT (LAST CHANCE) ----------------
    // Before the "real life" death, a banking-app style re-authentication: tap again and again to
    // fill the fingerprint before time runs out. Once per drone (the GameManager decides when).

    private RectTransform fingerCard;
    private Image fingerFill, fingerTimer;
    private TextMeshProUGUI fingerTitle, fingerStatus, fingerFine;

    public bool FingerprintPassed { get; private set; }

    private void BuildFingerprintCard(Transform parent)
    {
        GameObject card = CreatePanel(parent, "Fingerprint Card (runtime)", new Color(0.96f, 0.97f, 0.98f, 1f));
        fingerCard = card.GetComponent<RectTransform>();
        MatchVisibleScreen(fingerCard);
        Color ink = new Color(0.15f, 0.17f, 0.22f);

        fingerTitle = CreateText(card.transform, "Title", "OPERATOR RE-AUTHENTICATION", 24f, ink);
        fingerTitle.fontStyle = FontStyles.Bold;
        fingerTitle.enableWordWrapping = false;
        fingerTitle.enableAutoSizing = true;
        fingerTitle.fontSizeMin = 8f;
        fingerTitle.fontSizeMax = 24f;
        AnchorBox(fingerTitle.rectTransform, new Vector2(0.08f, 0.8f), new Vector2(0.92f, 0.87f));
        TextMeshProUGUI sub = CreateText(card.transform, "Subtitle", "Too many failed jobs.\nConfirm your identity to continue.", 15f, new Color(0.4f, 0.42f, 0.47f));
        AnchorBox(sub.rectTransform, new Vector2(0.08f, 0.7f), new Vector2(0.92f, 0.79f));

        // The print: gray ridges, and the same ridges in blue filling up from the bottom
        Image back = CardImage(card.transform, "Fingerprint", GameSprites.Fingerprint(), new Vector2(0.3f, 0.36f), new Vector2(0.7f, 0.66f));
        back.color = new Color(0.78f, 0.8f, 0.84f);
        fingerFill = CardImage(card.transform, "Fingerprint Fill", GameSprites.Fingerprint(), new Vector2(0.3f, 0.36f), new Vector2(0.7f, 0.66f));
        fingerFill.type = Image.Type.Filled;
        fingerFill.fillMethod = Image.FillMethod.Vertical;
        fingerFill.fillOrigin = (int)Image.OriginVertical.Bottom;

        fingerStatus = CreateText(card.transform, "Status", "TAP THE SENSOR", 20f, ink);
        fingerStatus.fontStyle = FontStyles.Bold;
        fingerStatus.enableWordWrapping = false;
        fingerStatus.enableAutoSizing = true;
        fingerStatus.fontSizeMin = 8f;
        fingerStatus.fontSizeMax = 20f;
        AnchorBox(fingerStatus.rectTransform, new Vector2(0.08f, 0.27f), new Vector2(0.92f, 0.33f));

        // Time left: a thin bar that shrinks
        GameObject track = CreatePanel(card.transform, "Timer Track", new Color(0.86f, 0.87f, 0.9f));
        AnchorBox(track.GetComponent<RectTransform>(), new Vector2(0.15f, 0.22f), new Vector2(0.85f, 0.235f));
        fingerTimer = CreatePanel(track.transform, "Timer", new Color(0.9f, 0.25f, 0.25f)).GetComponent<Image>();
        AnchorBox(fingerTimer.rectTransform, Vector2.zero, Vector2.one);

        fingerFine = CreateText(card.transform, "Fine Print", "", 11f, new Color(0.5f, 0.52f, 0.56f));
        AnchorBox(fingerFine.rectTransform, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.18f));

        card.SetActive(false);
    }

    // Tap again and again (each tap fills a bit, it drains when you stop). Sets FingerprintPassed.
    public IEnumerator ShowFingerprintCard(int tapsNeeded, float duration)
    {
        FingerprintPassed = false;
        if (fingerCard == null) yield break;
        Color blue = new Color(0.2f, 0.5f, 0.95f);
        fingerFill.color = blue;
        fingerFill.fillAmount = 0f;
        fingerStatus.text = "TAP THE SENSOR";
        fingerStatus.color = new Color(0.15f, 0.17f, 0.22f);
        fingerFine.text = "";
        fingerCard.SetAsLastSibling();
        fingerCard.gameObject.SetActive(true);
        SoundFX.Play(Sfx.TargetAlert);

        float progress = 0f, t = 0f, nextBotTap = 0.6f;
        bool botFast = UnityEngine.Random.value >= Autopilot.MissChance; // the bot sometimes taps too slowly
        while (t < duration && progress < 1f)
        {
            t += Time.deltaTime;
            if (Autopilot.Enabled && t >= nextBotTap)
            {
                Autopilot.Press();
                nextBotTap = t + (botFast ? 0.13f : 0.4f);
            }
            if (SingleInput.ActionPressed)
            {
                progress += 1f / tapsNeeded;
                SoundFX.Play(Sfx.FingerTap);
                fingerFill.rectTransform.localScale = Vector3.one * 1.04f;
            }
            // Full = done right away (draining that same frame used to leave it at 99.9%: it looked
            // complete but failed when the time ran out)
            if (progress >= 1f - 0.001f) { progress = 1f; break; }
            progress = Mathf.Max(0f, progress - 0.12f * Time.deltaTime); // stop tapping and it drains
            fingerFill.fillAmount = progress;
            fingerFill.rectTransform.localScale = Vector3.Lerp(fingerFill.rectTransform.localScale, Vector3.one, 12f * Time.deltaTime);
            fingerTimer.rectTransform.anchorMax = new Vector2(1f - t / duration, 1f);
            yield return null;
        }

        FingerprintPassed = progress >= 1f;
        fingerFill.fillAmount = FingerprintPassed ? 1f : progress; // a fail shows how far you got
        fingerFill.color = FingerprintPassed ? new Color(0.15f, 0.7f, 0.35f) : new Color(0.9f, 0.2f, 0.2f);
        fingerStatus.text = FingerprintPassed ? "IDENTITY CONFIRMED" : "AUTHENTICATION FAILED";
        fingerStatus.color = fingerFill.color;
        if (FingerprintPassed) fingerFine.text = "Biometric data saved for training.";
        SoundFX.Play(FingerprintPassed ? Sfx.FingerOk : Sfx.Fail);
        yield return new WaitForSeconds(1.3f);
        fingerCard.gameObject.SetActive(false);
    }

    // ---------------- ASSIGNED DRONE CARD ----------------
    // The drone you are training (see AssignedDrone): introduced at the start, blown up after a
    // failed job and decorated with a star when you reach the target score.

    public enum DroneCard { Assigned, Destroyed, Certified }

    private RectTransform droneCard;
    private TextMeshProUGUI droneHeader, droneName, droneFooter, droneStamp;
    private Image droneImage, droneStar, droneFlash;
    private Image[] droneDebris;

    private void BuildDroneCard(Transform parent)
    {
        GameObject card = CreatePanel(parent, "Drone Card (runtime)", new Color(0.04f, 0.03f, 0.06f, 1f));
        droneCard = card.GetComponent<RectTransform>();
        MatchVisibleScreen(droneCard);

        droneHeader = CreateTargetLine(card.transform, "Header", "DRONE ASSIGNED", 0.82f, 0.9f, 28f);
        droneHeader.color = Color.white;
        droneHeader.characterSpacing = 8f;
        droneStar = CardImage(card.transform, "Star", GameSprites.Star(), new Vector2(0.4f, 0.69f), new Vector2(0.6f, 0.8f));
        droneImage = CardImage(card.transform, "Drone", GameSprites.Drone(0), new Vector2(0.15f, 0.48f), new Vector2(0.85f, 0.7f));
        AddSilhouetteOutline(droneImage, Color.white, 3f); // the drone is dark: a white outline separates it from the dark card
        droneName = CreateTargetLine(card.transform, "Name", "", 0.34f, 0.44f, 40f);
        droneFooter = CreateTargetLine(card.transform, "Footer", "", 0.24f, 0.31f, 16f);
        droneFooter.color = Color.white;

        droneDebris = new Image[12];
        for (int i = 0; i < droneDebris.Length; i++)
        {
            droneDebris[i] = CardImage(card.transform, "Debris", GameSprites.Pixel(), new Vector2(0.5f, 0.59f), new Vector2(0.5f, 0.59f));
            droneDebris[i].rectTransform.sizeDelta = Vector2.one * UnityEngine.Random.Range(6f, 14f);
        }

        droneStamp = CreateTargetLine(card.transform, "Stamp", "DECOMMISSIONED", 0.5f, 0.66f, 40f);
        droneStamp.color = new Color(1f, 0.15f, 0.2f);
        droneStamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
        droneFlash = CardImage(card.transform, "Flash", GameSprites.Pixel(), Vector2.zero, Vector2.one);
        droneFlash.preserveAspect = false;
        droneFlash.color = Color.clear;

        card.SetActive(false);
    }

    // Solid outline around a sprite's shape: UI Outline draws 4 diagonal copies, so three of them
    // (diagonal, horizontal, vertical) cover all 8 directions
    private static void AddSilhouetteOutline(Image image, Color color, float width)
    {
        foreach (Vector2 d in new[] { new Vector2(width, width), new Vector2(width, 0f), new Vector2(0f, width) })
        {
            Outline o = image.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = d;
        }
    }

    private static Image CardImage(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        AnchorBox(img.rectTransform, min, max);
        return img;
    }

    public IEnumerator ShowDroneCard(DroneCard mode, float duration)
    {
        droneName.text = AssignedDrone.Name;
        droneHeader.text = mode == DroneCard.Assigned ? "DRONE ASSIGNED" : mode == DroneCard.Destroyed ? "TRAINING FAILED" : "TRAINING COMPLETE";
        droneFooter.text = mode == DroneCard.Assigned ? "IT LEARNS FROM EVERYTHING YOU DO."
            : mode == DroneCard.Destroyed ? "IT LEARNED FROM YOU." : "CERTIFIED FOR FIELD DUTY";
        droneStar.gameObject.SetActive(mode == DroneCard.Certified);
        droneStamp.gameObject.SetActive(false);
        droneImage.gameObject.SetActive(true);
        droneImage.color = Color.white;
        droneFlash.color = Color.clear;
        foreach (Image d in droneDebris) d.gameObject.SetActive(false);

        droneCard.SetAsLastSibling();
        droneCard.gameObject.SetActive(true);
        TrailerCapture.Log("dronecard " + mode);
        if (mode == DroneCard.Assigned) SoundFX.Play(Sfx.MenuSelect);
        if (mode == DroneCard.Certified) SoundFX.Play(Sfx.Success);

        Vector2 size = droneCard.rect.size;
        Vector2[] velocity = new Vector2[droneDebris.Length];
        const float blowUpAt = 0.9f;
        bool exploded = false;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            droneImage.sprite = GameSprites.Drone((int)(t / 0.05f) % 2); // propellers
            float pop = t < 0.15f ? Mathf.Lerp(1.3f, 1f, t / 0.15f) : 1f;
            droneName.rectTransform.localScale = Vector3.one * pop;

            if (mode != DroneCard.Destroyed)
            {
                droneImage.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Sin(t * 6f) * 6f);
                droneStar.rectTransform.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(t * 4f)) * 0.2f);
                droneStar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2f) * 10f);
            }
            else if (t < blowUpAt)
            {
                // Shakes harder and harder and sparks red
                droneImage.rectTransform.anchoredPosition = UnityEngine.Random.insideUnitCircle * 16f * t;
                droneImage.color = UnityEngine.Random.value < 0.3f ? new Color(1f, 0.35f, 0.25f) : Color.white;
            }
            else if (!exploded)
            {
                exploded = true;
                SoundFX.Play(Sfx.Explosion);
                droneImage.gameObject.SetActive(false);
                droneStamp.gameObject.SetActive(true);
                for (int i = 0; i < droneDebris.Length; i++)
                {
                    droneDebris[i].gameObject.SetActive(true);
                    droneDebris[i].rectTransform.anchoredPosition = Vector2.zero;
                    droneDebris[i].color = i % 3 == 0 ? new Color(1f, 0.55f, 0.1f) : new Color(0.3f, 0.32f, 0.38f);
                    velocity[i] = new Vector2(UnityEngine.Random.Range(-0.7f, 0.7f) * size.x, UnityEngine.Random.Range(0.3f, 0.9f) * size.y);
                }
            }
            else
            {
                float e = t - blowUpAt;
                droneFlash.color = new Color(1f, 0.85f, 0.5f, Mathf.Clamp01(0.9f - e / 0.3f));
                droneStamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, Mathf.Clamp01(e / 0.15f));
                for (int i = 0; i < droneDebris.Length; i++)
                {
                    velocity[i].y -= size.y * 2.2f * Time.deltaTime;
                    droneDebris[i].rectTransform.anchoredPosition += velocity[i] * Time.deltaTime;
                    droneDebris[i].rectTransform.Rotate(0f, 0f, 400f * Time.deltaTime);
                }
            }
            yield return null;
        }

        droneCard.gameObject.SetActive(false);
    }

    // ---------------- RETRO TV AD ----------------
    // Shows up when the player fails a lot, like the ads in mobile games. A different product each time
    // (glasses, eye pills, puzzle magazines, carrots, a blindfold...). Each ad lasts longer and the deal
    // gets better (20%, 40%, 50%, free). It can be skipped with SKIP AD.

    private RectTransform adRoot;
    private RawImage adStripes;
    private TextMeshProUGUI adTimer, adBrand, adTagline, adDiscount, adOldPrice, adNewPrice, adCallNow, adFinePrint;
    private RectTransform adFineSticker;
    private Image adCallStrip; // dark strip behind CALL NOW so it doesn't get lost in the yellow
    private RectTransform adProduct, adStar;
    private Image adProductImage;
    private bool adSkipped;
    private RectTransform adSkipButton;
    private static readonly System.Collections.Generic.List<int> adBag = new System.Collections.Generic.List<int>();
    private static int lastAdProduct = -1;

    private struct AdProduct
    {
        public string brand, tagline, phone, finePrint;
        public System.Func<Sprite> sprite;
    }

    // The products; each ad picks one at random (see NextAdProduct)
    private static readonly AdProduct[] adProducts =
    {
        new AdProduct { brand = "SUPER SPECS", tagline = "AS SEEN ON TV!", phone = "1-800-555-SPEX",
            finePrint = "*Offer valid while supplies last. Not responsible for traffic accidents.", sprite = GameSprites.Glasses },
        new AdProduct { brand = "VISION-MAX", tagline = "EAGLE EYES IN 7 DAYS!", phone = "1-800-555-EYES",
            finePrint = "*These statements have not been evaluated by anyone. Ever.", sprite = GameSprites.PillBottle },
        new AdProduct { brand = "BRAIN BLAST", tagline = "PUZZLES FOR A SHARPER MIND!", phone = "1-800-555-IQIQ",
            finePrint = "*Side effects may include noticing the road.", sprite = GameSprites.PuzzleMagazine },
        new AdProduct { brand = "CARROT POWER", tagline = "GRANDMA WAS RIGHT!", phone = "1-800-555-CRRT",
            finePrint = "*Rabbits sold separately.", sprite = GameSprites.Carrot },
        new AdProduct { brand = "NO MORE PEEKING", tagline = "QUIT THE LATE-NIGHT VIDEOS!", phone = "1-800-555-QUIT",
            finePrint = "*Discreet packaging. Your browser history is safe with us.", sprite = GameSprites.Blindfold },
    };

    private void BuildAd(Transform parent)
    {
        GameObject root = CreatePanel(parent, "Retro Ad (runtime)", Color.black);
        adRoot = root.GetComponent<RectTransform>();
        MatchVisibleScreen(adRoot);
        root.AddComponent<RectMask2D>(); // moving parts stay inside the screen

        GameObject stripes = new GameObject("Stripes", typeof(RectTransform), typeof(RawImage));
        stripes.transform.SetParent(root.transform, false);
        adStripes = stripes.GetComponent<RawImage>();
        adStripes.texture = GameSprites.AdStripes();
        adStripes.raycastTarget = false;
        Stretch(adStripes.rectTransform);

        // Brand
        GameObject banner = CreatePanel(root.transform, "Brand Banner", new Color(0.30f, 0.05f, 0.45f, 0.92f));
        AnchorBox(banner.GetComponent<RectTransform>(), new Vector2(0f, 0.76f), new Vector2(1f, 0.92f));
        adBrand = CreateText(banner.transform, "Brand", "SUPER SPECS", 34, new Color(1f, 0.89f, 0.18f));
        adBrand.fontStyle = FontStyles.Bold | FontStyles.Italic;
        adBrand.outlineColor = new Color(0.9f, 0.1f, 0.3f);
        adBrand.outlineWidth = 0.25f;
        FitInside(adBrand, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f), 8f, 34f);
        adBrand.enableWordWrapping = false;

        GameObject tv = CreatePanel(root.transform, "As Seen On TV", new Color(0.9f, 0.1f, 0.15f));
        AnchorBox(tv.GetComponent<RectTransform>(), new Vector2(0.08f, 0.71f), new Vector2(0.92f, 0.765f));
        adTagline = CreateText(tv.transform, "Text", "AS SEEN ON TV!", 14, Color.white);
        adTagline.fontStyle = FontStyles.Bold;
        FitInside(adTagline, new Vector2(0.03f, 0f), new Vector2(0.97f, 1f), 5f, 14f);
        adTagline.enableWordWrapping = false;

        // Product
        GameObject product = new GameObject("Product", typeof(RectTransform), typeof(Image));
        product.transform.SetParent(root.transform, false);
        adProductImage = product.GetComponent<Image>();
        adProductImage.preserveAspect = true;
        adProductImage.raycastTarget = false;
        adProduct = adProductImage.rectTransform;
        AnchorBox(adProduct, new Vector2(0.08f, 0.47f), new Vector2(0.92f, 0.69f));

        // Price
        adOldPrice = CreateText(root.transform, "Old Price", "<s>$99.99</s>", 16, Color.white);
        adOldPrice.fontStyle = FontStyles.Bold;
        adOldPrice.outlineColor = new Color(0.25f, 0.05f, 0.35f);
        adOldPrice.outlineWidth = 0.3f;
        FitInside(adOldPrice, new Vector2(0.03f, 0.36f), new Vector2(0.5f, 0.43f), 6f, 16f);
        adNewPrice = CreateText(root.transform, "New Price", "$79.99", 30, Color.white);
        adNewPrice.fontStyle = FontStyles.Bold;
        adNewPrice.outlineColor = new Color(0.25f, 0.05f, 0.35f);
        adNewPrice.outlineWidth = 0.3f;
        FitInside(adNewPrice, new Vector2(0.03f, 0.25f), new Vector2(0.5f, 0.37f), 8f, 30f);

        // Deal starburst
        GameObject star = new GameObject("Starburst", typeof(RectTransform), typeof(Image));
        star.transform.SetParent(root.transform, false);
        Image starImg = star.GetComponent<Image>();
        starImg.sprite = GameSprites.Starburst();
        starImg.preserveAspect = true;
        starImg.raycastTarget = false;
        adStar = starImg.rectTransform;
        AnchorBox(adStar, new Vector2(0.5f, 0.22f), new Vector2(0.98f, 0.47f));
        adDiscount = CreateText(root.transform, "Discount", "20%\nOFF!", 24, new Color(0.85f, 0.05f, 0.1f));
        adDiscount.fontStyle = FontStyles.Bold;
        FitInside(adDiscount, new Vector2(0.58f, 0.27f), new Vector2(0.9f, 0.42f), 8f, 26f);

        // Call to action and fine print
        adCallStrip = CreatePanel(root.transform, "Call Now Strip", new Color(0.55f, 0f, 0.12f)).GetComponent<Image>();
        adCallStrip.raycastTarget = false;
        AnchorBox(adCallStrip.rectTransform, new Vector2(0f, 0.15f), new Vector2(1f, 0.225f));
        adCallNow = CreateText(root.transform, "Call Now", "CALL NOW! 1-800-555-SPEX", 16, Color.white);
        adCallNow.fontStyle = FontStyles.Bold;
        adCallNow.outlineColor = Color.black;
        adCallNow.outlineWidth = 0.3f;
        FitInside(adCallNow, new Vector2(0.03f, 0.155f), new Vector2(0.97f, 0.22f), 6f, 16f);

        // The joke lives in the fine print, so it's a black sticker with big yellow text (it wiggles a bit)
        GameObject sticker = CreatePanel(root.transform, "Fine Print Sticker", new Color(0.04f, 0.02f, 0.06f, 0.92f));
        adFineSticker = sticker.GetComponent<RectTransform>();
        AnchorBox(adFineSticker, new Vector2(0.02f, 0.078f), new Vector2(0.98f, 0.145f));
        Outline stickerEdge = sticker.AddComponent<Outline>();
        stickerEdge.effectColor = new Color(1f, 0.86f, 0.1f);
        stickerEdge.effectDistance = new Vector2(2f, -2f);
        adFinePrint = CreateText(sticker.transform, "Fine Print", "", 11, new Color(1f, 0.92f, 0.3f));
        adFinePrint.fontStyle = FontStyles.Bold | FontStyles.Italic;
        adFinePrint.outlineColor = Color.black;
        adFinePrint.outlineWidth = 0.2f;
        FitInside(adFinePrint, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.96f), 5f, 11f);
        adFinePrint.enableWordWrapping = true;

        // Countdown and skip button
        GameObject timerBox = CreatePanel(root.transform, "Timer", new Color(0f, 0f, 0f, 0.65f));
        AnchorBox(timerBox.GetComponent<RectTransform>(), new Vector2(0.03f, 0.935f), new Vector2(0.3f, 0.985f));
        adTimer = CreateText(timerBox.transform, "Text", "Ad 3", 12, Color.white);
        FitInside(adTimer, new Vector2(0.05f, 0f), new Vector2(0.95f, 1f), 6f, 12f);

        // Skip button: bright yellow, thick black border and a hard shadow, like the rest of the ad
        Button skip = CreateButton(root.transform, "Skip Button", "SKIP AD >>", new Color(1f, 0.86f, 0.1f));
        adSkipButton = skip.GetComponent<RectTransform>();
        AnchorBox(adSkipButton, new Vector2(0.5f, 0.012f), new Vector2(0.97f, 0.068f));
        Shadow skipShadow = skip.gameObject.AddComponent<Shadow>();
        skipShadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        skipShadow.effectDistance = new Vector2(4f, -4f);
        Outline skipBorder = skip.gameObject.AddComponent<Outline>();
        skipBorder.effectColor = Color.black;
        skipBorder.effectDistance = new Vector2(2f, -2f);
        ColorBlock skipColors = skip.colors;
        skipColors.highlightedColor = new Color(1f, 1f, 1f);
        skipColors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        skip.colors = skipColors;
        TextMeshProUGUI skipLabel = skip.GetComponentInChildren<TextMeshProUGUI>();
        skipLabel.color = Color.black;
        skipLabel.fontStyle = FontStyles.Bold | FontStyles.Italic;
        skipLabel.characterSpacing = 4f;
        skip.onClick.AddListener(() => adSkipped = true);

        // Dark veil over the stripes so the content stands out
        GameObject scan = new GameObject("Stripes Shade", typeof(RectTransform), typeof(Image));
        scan.transform.SetParent(root.transform, false);
        Image scanImg = scan.GetComponent<Image>();
        scanImg.color = new Color(0f, 0f, 0f, 0.15f);
        scanImg.raycastTarget = false;
        Stretch(scanImg.rectTransform);
        scan.transform.SetSiblingIndex(1); // above the stripes, below the content

        root.SetActive(false);
    }

    // Random product, without repeats until every product has been shown (shuffle bag),
    // and never the same one twice in a row across bags
    private static int NextAdProduct()
    {
        if (adBag.Count == 0)
        {
            for (int i = 0; i < adProducts.Length; i++) adBag.Add(i);
            for (int i = adBag.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (adBag[i], adBag[j]) = (adBag[j], adBag[i]);
            }
            if (adBag.Count > 1 && adBag[0] == lastAdProduct) (adBag[0], adBag[1]) = (adBag[1], adBag[0]);
        }
        lastAdProduct = adBag[0];
        adBag.RemoveAt(0);
        return lastAdProduct;
    }

    // level 1 = 5 fails, 2 = 10 fails, ...
    public IEnumerator ShowAd(int level, float duration)
    {
        if (adRoot == null) yield break;

        AdProduct item = adProducts[NextAdProduct()];
        adBrand.text = item.brand;
        adTagline.text = item.tagline;
        adProductImage.sprite = item.sprite();
        adCallNow.text = "CALL NOW! " + item.phone;
        adFinePrint.text = item.finePrint;

        int[] discounts = { 20, 40, 50, 100 };
        int discount = discounts[Mathf.Clamp(level - 1, 0, discounts.Length - 1)];
        const float basePrice = 99.99f;
        bool free = discount >= 100;
        adDiscount.text = free ? "FREE!!" : $"{discount}%\nOFF!";
        adOldPrice.text = "<s>$99.99</s>";
        adNewPrice.text = free ? "$0.00" : $"${basePrice * (1f - discount / 100f):0.00}";

        adSkipped = false;
        adRoot.SetAsLastSibling();
        adRoot.gameObject.SetActive(true);

        float t = 0f;
        while (t < duration && !adSkipped)
        {
            t += Time.deltaTime;
            adTimer.text = $"Ad {Mathf.CeilToInt(duration - t)}";

            // Everything wiggles like a late-night infomercial
            adStripes.uvRect = new Rect(t * 0.6f, t * 0.3f, 4f, 6f);
            adProduct.localScale = Vector3.one * (1f + Mathf.Sin(t * 6f) * 0.08f);
            adProduct.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3f) * 8f);
            adStar.localRotation = Quaternion.Euler(0f, 0f, -t * 40f);
            float pulse = 1f + Mathf.Abs(Mathf.Sin(t * 5f)) * 0.15f;
            adDiscount.rectTransform.localScale = Vector3.one * pulse;
            adBrand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 4f) * 3f);
            // The strip flashes (the text stays readable)
            adSkipButton.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(t * 3f)) * 0.06f);
            adFineSticker.localRotation = Quaternion.Euler(0f, 0f, -1.5f + Mathf.Sin(t * 5f) * 1.2f);
            bool lit = Mathf.Repeat(t, 0.6f) < 0.4f;
            adCallStrip.color = lit ? new Color(0.85f, 0.05f, 0.15f) : new Color(0.35f, 0f, 0.08f);
            yield return null;
        }

        adRoot.gameObject.SetActive(false);
    }

    // ---------------- GOOD JOB / FAILED NEON SIGN ----------------

    private static readonly Color NeonGood = new Color(0.15f, 1f, 0.45f);
    private static readonly Color NeonFail = new Color(1f, 0.12f, 0.35f);

    private void BuildResultCard(Transform parent)
    {
        // Opaque background: the minigame is NOT visible behind the sign
        GameObject card = CreatePanel(parent, "Result Neon (runtime)", new Color(0.03f, 0.02f, 0.05f, 1f));
        resultCard = card.GetComponent<RectTransform>();
        MatchVisibleScreen(resultCard);

        // Centered sign
        GameObject sign = new GameObject("Neon Sign", typeof(RectTransform), typeof(CanvasGroup));
        sign.transform.SetParent(card.transform, false);
        RectTransform signRect = sign.GetComponent<RectTransform>();
        signRect.anchorMin = new Vector2(0.06f, 0.36f);
        signRect.anchorMax = new Vector2(0.94f, 0.64f);
        signRect.offsetMin = signRect.offsetMax = Vector2.zero;
        neonSign = sign.GetComponent<CanvasGroup>();

        // Sign frame (4 neon tubes)
        neonBorder = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject tube = CreatePanel(sign.transform, "Tube " + i, Color.white);
            RectTransform rt = tube.GetComponent<RectTransform>();
            const float thickness = 3f;
            switch (i)
            {
                case 0: rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.sizeDelta = new Vector2(0, thickness); break; // top
                case 1: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.sizeDelta = new Vector2(0, thickness); break; // bottom
                case 2: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.sizeDelta = new Vector2(thickness, 0); break; // left
                default: rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1); rt.sizeDelta = new Vector2(thickness, 0); break; // right
            }
            rt.anchoredPosition = Vector2.zero;

            // Glow around the tube
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
        // Almost-white core with an outline and glow in the neon color
        resultTitle.color = Color.Lerp(neon, Color.white, 0.55f);
        resultTitle.outlineColor = neon;
        resultTitle.outlineWidth = 0.18f;

        Material m = resultTitle.fontMaterial; // own instance, doesn't touch the shared material
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

        // Neon turning on: a few sparks before it stays lit
        float[] startup = { 0.06f, 0.08f, 0.05f, 0.18f, 0.04f, 0.06f };
        for (int i = 0; i < startup.Length; i++)
        {
            neonSign.alpha = i % 2 == 0 ? 1f : 0.08f;
            yield return new WaitForSeconds(startup[i]);
        }
        neonSign.alpha = 1f;
        resultSubtitle.alpha = 1f;

        // Lit, humming, with random flickers (FAILED flickers more, like a broken sign)
        float flickerChance = success ? 0.6f : 2.5f; // flickers per second
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

        // Turns off
        neonSign.alpha = 0.1f;
        yield return new WaitForSeconds(0.05f);
        resultCard.gameObject.SetActive(false);
    }

    // ---------------- HELPERS ----------------

    // Places the rect over the phone screen, sized relative to it
    // At 16:9 the bottom of the phone goes off the bottom of the monitor, so the cards
    // (menu, target, result, ad) only cover the part of the phone screen that is visible.
    private readonly System.Collections.Generic.List<RectTransform> visibleLayouts = new System.Collections.Generic.List<RectTransform>();
    private Vector2Int layoutScreenSize;

    private void MatchVisibleScreen(RectTransform rt)
    {
        visibleLayouts.Add(rt);
        LayoutVisible(rt);
    }

    private void LayoutVisible(RectTransform rt)
    {
        RectTransform parent = rt.parent as RectTransform;
        Canvas canvas = screenRect != null ? screenRect.GetComponentInParent<Canvas>() : null;
        if (parent == null || canvas == null)
        {
            MatchScreen(rt, 1f, 1f);
            return;
        }

        Canvas.ForceUpdateCanvases();
        Vector3[] screen = new Vector3[4], monitor = new Vector3[4];
        screenRect.GetWorldCorners(screen);
        ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(monitor);
        Vector2 min = parent.InverseTransformPoint(Vector3.Max(screen[0], monitor[0]));
        Vector2 max = parent.InverseTransformPoint(Vector3.Min(screen[2], monitor[2]));

        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = max - min;
        rt.anchoredPosition = (min + max) / 2f - parent.rect.center;
    }

    // Score in the same style as the target card: bold yellow, black outline and a drop shadow
    public void StyleScore(TextMeshProUGUI score)
    {
        if (score == null) return;
        if (font != null) score.font = font;
        score.fontStyle = FontStyles.Bold;
        score.color = TargetColor;
        score.characterSpacing = 6f;
        score.outlineColor = Color.black;
        score.outlineWidth = 0.3f;

        Material m = score.fontMaterial;
        if (m.HasProperty("_UnderlayColor"))
        {
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.85f));
            m.SetFloat("_UnderlayOffsetX", 0.6f);
            m.SetFloat("_UnderlayOffsetY", -0.6f);
            m.SetFloat("_UnderlayDilate", 0.4f);
            m.SetFloat("_UnderlaySoftness", 0.1f);
        }
        score.UpdateMeshPadding();
    }

    // The score jumps when it goes up
    public void PunchScore(TextMeshProUGUI score)
    {
        if (score != null) StartCoroutine(PunchRoutine(score.rectTransform));
    }

    private IEnumerator PunchRoutine(RectTransform rt)
    {
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

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

        TextMeshProUGUI text = CreateText(go.transform, "Label", label, 22, Color.white);
        text.fontStyle = FontStyles.Bold;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = 6f;
        text.fontSizeMax = 22f;
        Stretch(text.rectTransform, 4);
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
