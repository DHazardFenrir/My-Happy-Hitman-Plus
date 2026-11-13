using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// When the player fails too many times, the current minigame's trap happens to THEM, "in real life":
// they're already walking through the minigame's place; the car hits them / the piano falls on them /
// they fall down the hole, the phone flies off, the screen cracks with blood and "FAILED JOB - RETRY?" shows up.
public class IrlDeathSequence : MonoBehaviour
{
    public string failedTitle = "FAILED JOB";
    public string failedSubtitle = "ALERT RESOLVED - THREAT ELIMINATED\n<size=75%><color=#ffffff>YOU HAVE FAILED.</color></size>";
    public string alertTitle = "THREAT ALERT";
    [Tooltip("Seconds the THREAT ALERT stays on screen before the trap comes for you")]
    public float alertSeconds = 3.2f;

    private Canvas canvas;
    private RectTransform canvasRect;
    private Image darkness, flash, shadow, threat;
    private RawImage crack, blood;
    private RawImage carCrack, carBlood;  // on the car's windshield
    private Image[] headlightGlows;
    private Image lockOn;                 // red crosshair of the drone that shoots you
    private TextMeshProUGUI title, subtitle;
    private GameObject subtitleBox;
    private RectTransform titleRoot; // title + its shadow, animated together
    private Button retryButton;
    private bool retryPressed;

    // THREAT ALERT: shown before the death, when the system flags you as dangerous
    private RectTransform alertRoot;
    private Image alertTint;
    private RawImage[] alertBars;
    private TextMeshProUGUI alertText, alertLines;

    public IEnumerator Play(WarioStage stage, GameUI ui, WalkingBackground street, int failCount = 0)
    {
        if (canvas == null) Build();
        ResetVisuals();
        canvas.gameObject.SetActive(true);

        RectTransform phone = ui.PhoneRig;
        float screenH = canvasRect.rect.height;

        // 1. You stop: you're already walking through the minigame's place
        if (street != null) street.Paused = true;
        yield return new WaitForSeconds(0.4f);

        // 1b. The system flags you: THREAT ALERT
        yield return ThreatAlert(stage.IrlDeath, failCount);
        yield return new WaitForSeconds(0.3f);

        // 2. The trap comes for you
        switch (stage.IrlDeath)
        {
            case IrlDeathKind.HitByCar:
                yield return CarApproaches(screenH, phone);
                break;
            case IrlDeathKind.CrushedFromAbove:
                SoundFX.Play(Sfx.PianoWhistle);
                yield return SomethingFalls(stage.IrlThreatSprite, screenH);
                break;
            case IrlDeathKind.ShotByDrone:
                yield return DroneShootsYou(screenH, phone);
                break;
            case IrlDeathKind.MauledByDogs:
                yield return DogCharges(screenH, phone);
                break;
            default:
                SoundFX.Play(Sfx.ElevatorFall);
                yield return FallDown(phone, street);
                break;
        }

        // 3. Impact: flash, shake, the phone flies off, the screen cracks and blood splatters
        TrailerCapture.Log("irl impact");
        bool car = stage.IrlDeath == IrlDeathKind.HitByCar;
        bool drone = stage.IrlDeath == IrlDeathKind.ShotByDrone;
        SoundFX.Play(stage.IrlDeath == IrlDeathKind.CrushedFromAbove ? Sfx.PianoCrush : drone ? Sfx.DroneHit
            : stage.IrlDeath == IrlDeathKind.MauledByDogs ? Sfx.DogBite : Sfx.CarHit);
        threat.gameObject.SetActive(car || drone); // the car / the drone stays in front of you
        lockOn.gameObject.SetActive(false);
        shadow.gameObject.SetActive(false);
        if (car)
        {
            SoundFX.Play(Sfx.GlassBreak);
            carCrack.texture = ProceduralTextures.ScreenCrack(new Vector2(Random.Range(0.35f, 0.65f), Random.Range(0.3f, 0.6f)), Random.Range(0, 9999));
            carCrack.gameObject.SetActive(true);
            carBlood.texture = GameSprites.BloodSplash(Random.Range(0, GameSprites.BloodSplashCount))?.texture;
            carBlood.gameObject.SetActive(true);
            foreach (Image glow in headlightGlows) glow.color = new Color(1f, 0.95f, 0.7f, 0.35f);
        }
        flash.color = Color.white;
        crack.texture = ProceduralTextures.ScreenCrack(new Vector2(Random.Range(0.35f, 0.65f), Random.Range(0.35f, 0.65f)), Random.Range(0, 9999));
        crack.gameObject.SetActive(true);
        SoundFX.Play(Sfx.PhoneCrack); // the phone's screen cracks in every death
        blood.texture = GameSprites.BloodSplash(Random.Range(0, GameSprites.BloodSplashCount))?.texture;
        blood.rectTransform.anchoredPosition = new Vector2(Random.Range(-200f, 200f), Random.Range(-80f, 120f));
        blood.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f));
        blood.gameObject.SetActive(true);

        StartCoroutine(drone ? PhoneDrops(phone) : PhoneFlies(phone)); // shot: you just let go of it
        Vector2 stillStart = street != null ? street.ShakeRoot.anchoredPosition : Vector2.zero;
        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - t / 0.15f));
            blood.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.6f, Mathf.Clamp01(t / 0.12f));
            Vector2 shake = Random.insideUnitCircle * 30f * (1f - t / 0.5f);
            if (street != null) street.ShakeRoot.anchoredPosition = stillStart + shake;
            crack.rectTransform.anchoredPosition = shake;
            yield return null;
        }
        flash.color = Color.clear;
        crack.rectTransform.anchoredPosition = Vector2.zero;

        yield return new WaitForSeconds(0.9f);

        // 4. FAILED JOB - RETRY?
        yield return FadeImage(darkness, darkness.color.a, 0.6f, 0.5f);
        titleRoot.gameObject.SetActive(true);
        t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            titleRoot.localScale = Vector3.one * Mathf.Lerp(2.5f, 1f, t / 0.25f);
            yield return null;
        }
        titleRoot.localScale = Vector3.one;
        yield return ShakeTitle();
        subtitleBox.SetActive(true);
        yield return new WaitForSeconds(0.4f);

        retryPressed = false;
        retryButton.gameObject.SetActive(true);
        float waited = 0f;
        while (!retryPressed)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) retryPressed = true;
            if (Autopilot.Enabled && (waited += Time.deltaTime) > 2f) retryPressed = true;
            retryButton.transform.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.08f);
            titleRoot.anchoredPosition = Random.value < 0.03f ? Random.insideUnitCircle * 6f : Vector2.zero;
            yield return null;
        }

        // 5. Back to the game
        yield return FadeImage(darkness, darkness.color.a, 1f, 0.4f);
        if (street != null)
        {
            street.ShakeRoot.anchoredPosition = Vector2.zero;
            street.Paused = false;
        }
        ui.ResetPhoneRig();
        crack.gameObject.SetActive(false);
        blood.gameObject.SetActive(false);
        titleRoot.gameObject.SetActive(false);
        subtitleBox.SetActive(false);
        retryButton.gameObject.SetActive(false);
        yield return FadeImage(darkness, 1f, 0f, 0.4f);
        canvas.gameObject.SetActive(false);
    }

    // A car seen from the front: first only its headlights in the distance and the horn,
    // then it rushes at you until it fills the screen
    private IEnumerator CarApproaches(float screenH, RectTransform phone)
    {
        ShowThreat(GameSprites.CarFront(), Color.white);
        threat.rectTransform.sizeDelta = new Vector2(480f, 320f);
        foreach (Image glow in headlightGlows) glow.gameObject.SetActive(true);
        SoundFX.Play(Sfx.CarHorn);

        float t = 0f;
        const float duration = 1.5f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;
            float eased = k * k * k;
            threat.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(screenH * 0.08f, -screenH * 0.05f, eased));
            threat.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.08f, 2.6f, eased);
            threat.color = Color.Lerp(new Color(0.25f, 0.25f, 0.3f), Color.white, Mathf.Clamp01(k * 1.5f)); // dark while far away

            // Blinding headlights that flicker as it gets closer
            float glare = Mathf.Lerp(0.6f, 1f, k) * (0.85f + Random.value * 0.15f);
            foreach (Image glow in headlightGlows)
            {
                glow.color = new Color(1f, 0.95f, 0.7f, glare);
                glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(3f, 1.3f, k);
            }

            if (phone != null) phone.anchoredPosition = Random.insideUnitCircle * 10f * k;
            yield return null;
        }
    }

    // A drone comes down in front of you, locks on (red crosshair + beeps) and fires
    private IEnumerator DroneShootsYou(float screenH, RectTransform phone)
    {
        ShowThreat(GameSprites.Drone(0), Color.white);
        threat.rectTransform.sizeDelta = new Vector2(480f, 240f);
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Pow(1f - t / 0.8f, 3f);
            threat.sprite = GameSprites.Drone((int)(t / 0.05f) % 2);
            threat.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(screenH * 0.7f, screenH * 0.22f, k));
            threat.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.4f, k);
            yield return null;
        }

        lockOn.gameObject.SetActive(true);
        for (int beep = 0; beep < 3; beep++)
        {
            SoundFX.Play(Sfx.MenuSelect);
            float b = 0f;
            while (b < 0.22f)
            {
                b += Time.deltaTime;
                t += Time.deltaTime;
                threat.sprite = GameSprites.Drone((int)(t / 0.05f) % 2);
                threat.rectTransform.anchoredPosition = new Vector2(0f, screenH * 0.22f + Mathf.Sin(t * 6f) * 8f);
                lockOn.color = new Color(1f, 0.1f, 0.1f, b < 0.12f ? 1f : 0.3f);
                lockOn.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f - beep * 0.25f, 1.3f - beep * 0.25f, b / 0.22f);
                yield return null;
            }
        }

        SoundFX.Play(Sfx.DroneShot);
        flash.color = new Color(1f, 0.9f, 0.6f, 0.8f);
        if (phone != null) phone.anchoredPosition = Random.insideUnitCircle * 12f;
        yield return null;
    }

    // You get shot and let go of the phone: it slips down off the screen, turning a little
    private IEnumerator PhoneDrops(RectTransform phone)
    {
        if (phone == null) yield break;
        float parentH = ((RectTransform)phone.parent).rect.height;
        Vector2 pos = phone.anchoredPosition;
        float velocity = 0f;
        float spin = Random.Range(-40f, 40f);
        float angle = 0f;
        float t = 0f;
        yield return new WaitForSeconds(0.15f);
        while (t < 1.6f)
        {
            t += Time.deltaTime;
            velocity += parentH * 2.5f * Time.deltaTime;
            pos.y -= velocity * Time.deltaTime;
            angle += spin * Time.deltaTime;
            phone.anchoredPosition = pos;
            phone.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }
    }

    // The piano falls from above: first its shadow grows, then it lands on you
    private IEnumerator SomethingFalls(Sprite thing, float screenH)
    {
        shadow.gameObject.SetActive(true);
        float t = 0f;
        while (t < 0.9f)
        {
            t += Time.deltaTime;
            float k = t / 0.9f;
            shadow.rectTransform.localScale = new Vector3(Mathf.Lerp(0.2f, 3.5f, k), Mathf.Lerp(0.1f, 1.6f, k), 1f);
            shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.1f, 0.6f, k));
            yield return null;
        }

        ShowThreat(thing, new Color(0.1f, 0.1f, 0.12f));
        t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            float k = t / 0.45f;
            threat.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(screenH * 1.1f, 0f, k * k));
            threat.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.5f, 4.5f, k * k);
            yield return null;
        }
    }

    // A guard dog charges at you from down the street, barking, and leaps at your face
    private IEnumerator DogCharges(float screenH, RectTransform phone)
    {
        ShowThreat(GameSprites.Dog(0), Color.white);
        threat.rectTransform.sizeDelta = new Vector2(440f, 280f);
        SoundFX.Play(Sfx.DogBark);
        float t = 0f;
        const float run = 1.2f;
        while (t < run)
        {
            t += Time.deltaTime;
            float k = t / run;
            threat.sprite = GameSprites.Dog((int)(t / 0.07f) % 2);
            float hop = Mathf.Abs(Mathf.Sin(t * 18f)) * 30f * (0.4f + k);
            threat.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-screenH * 0.3f, 0f, k), Mathf.Lerp(-screenH * 0.1f, -screenH * 0.05f, k) + hop);
            threat.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.15f, 1.2f, k * k);
            if (t > run * 0.55f && t - Time.deltaTime <= run * 0.55f) SoundFX.Play(Sfx.DogBark);
            if (phone != null) phone.anchoredPosition = Random.insideUnitCircle * 6f * k;
            yield return null;
        }

        // The leap: up and at you, filling the screen
        SoundFX.Play(Sfx.DogBite);
        t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float k = t / 0.3f;
            threat.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(-screenH * 0.05f, screenH * 0.08f, k));
            threat.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.2f, 4.5f, k * k);
            threat.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 20f, k));
            yield return null;
        }
    }

    // Falling down the sewer / elevator shaft: the world rushes up and everything goes dark
    private IEnumerator FallDown(RectTransform phone, WalkingBackground street)
    {
        float t = 0f;
        const float duration = 1.2f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;
            float fallen = k * k;
            if (street != null) street.ShakeRoot.anchoredPosition = new Vector2(Random.Range(-4f, 4f), fallen * 2500f);
            if (phone != null) phone.anchoredPosition = new Vector2(0f, fallen * 120f);
            darkness.color = new Color(0f, 0f, 0f, Mathf.Clamp01(k * 1.2f));
            yield return null;
        }
        darkness.color = new Color(0f, 0f, 0f, 0.85f);
    }

    // The phone flies off, spinning, and falls off screen
    private IEnumerator PhoneFlies(RectTransform phone)
    {
        if (phone == null) yield break;
        float parentH = ((RectTransform)phone.parent).rect.height;
        Vector2 velocity = new Vector2(Random.Range(-250f, 250f), parentH * 0.9f);
        float spin = Random.Range(-400f, 400f);
        Vector2 pos = phone.anchoredPosition;
        float angle = 0f;
        float t = 0f;
        while (t < 2f)
        {
            t += Time.deltaTime;
            velocity.y -= parentH * 3.5f * Time.deltaTime;
            pos += velocity * Time.deltaTime;
            angle += spin * Time.deltaTime;
            phone.anchoredPosition = pos;
            phone.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }
    }

    private void ShowThreat(Sprite sprite, Color fallbackColor)
    {
        threat.sprite = sprite;
        threat.color = sprite != null ? Color.white : fallbackColor;
        threat.preserveAspect = true;
        threat.rectTransform.localRotation = Quaternion.identity;
        threat.gameObject.SetActive(true);
    }

    private static IEnumerator FadeImage(Image img, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            img.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        img.color = new Color(0f, 0f, 0f, to);
    }

    private void ResetVisuals()
    {
        darkness.color = Color.clear;
        flash.color = Color.clear;
        threat.gameObject.SetActive(false);
        threat.rectTransform.sizeDelta = new Vector2(300f, 300f);
        foreach (Image glow in headlightGlows) glow.gameObject.SetActive(false);
        carCrack.gameObject.SetActive(false);
        carBlood.gameObject.SetActive(false);
        lockOn.gameObject.SetActive(false);
        shadow.gameObject.SetActive(false);
        crack.gameObject.SetActive(false);
        blood.gameObject.SetActive(false);
        titleRoot.gameObject.SetActive(false);
        subtitleBox.SetActive(false);
        retryButton.gameObject.SetActive(false);
        alertRoot.gameObject.SetActive(false);
    }

    // ---------------- THREAT ALERT ----------------

    // Red alarm over everything: hazard stripes, a flashing "THREAT ALERT" and a terminal-style
    // report saying you are dangerous and what is being sent to take care of you
    private IEnumerator ThreatAlert(IrlDeathKind kind, int failCount)
    {
        string countermeasure = kind switch
        {
            IrlDeathKind.HitByCar => "TRAFFIC UNIT",
            IrlDeathKind.CrushedFromAbove => "FALLING OBJECT",
            IrlDeathKind.ShotByDrone => "AERIAL UNIT",
            IrlDeathKind.MauledByDogs => "K-9 UNIT",
            _ => "FLOOR RECONFIGURATION",
        };
        string report =
            "SUBJECT FLAGGED AS DANGEROUS\n" +
            $"<color=#ffffff>FAILED JOBS: {failCount}   LOYALTY: LOW</color>\n" +
            $"DISPATCHING COUNTERMEASURE: {countermeasure}";

        alertRoot.gameObject.SetActive(true);
        alertLines.text = report;
        alertLines.maxVisibleCharacters = 0;

        int total = report.Length;
        float t = 0f;
        float nextBeep = 0f;
        while (t < alertSeconds)
        {
            t += Time.deltaTime;
            if (t >= nextBeep) { SoundFX.Play(Sfx.TargetAlert); nextBeep += 0.8f; }

            // Red pulse, flashing title, scrolling hazard stripes, report typing in
            float pulse = Mathf.Abs(Mathf.Sin(t * 6f));
            alertTint.color = new Color(0.9f, 0f, 0.05f, Mathf.Lerp(0.15f, 0.45f, pulse));
            alertText.alpha = Mathf.Repeat(t, 0.5f) < 0.36f ? 1f : 0.25f;
            alertText.rectTransform.localScale = Vector3.one * (1f + pulse * 0.05f);
            foreach (RawImage bar in alertBars) bar.uvRect = new Rect(t * 1.5f, 0f, bar.rectTransform.rect.width / Mathf.Max(1f, bar.rectTransform.rect.height), 1f);
            alertLines.maxVisibleCharacters = Mathf.Min(total, (int)(Mathf.Max(0f, t - 0.4f) * 45f));
            yield return null;
        }
        alertRoot.gameObject.SetActive(false);
    }

    private void BuildThreatAlert(Transform parent)
    {
        alertRoot = new GameObject("Threat Alert", typeof(RectTransform)).GetComponent<RectTransform>();
        alertRoot.SetParent(parent, false);
        Stretch(alertRoot);

        alertTint = NewImage(alertRoot, "Red Tint", Color.clear);
        Stretch(alertTint.rectTransform);

        // Yellow and black hazard stripes at the top and bottom
        Texture2D hazard = HazardTexture();
        alertBars = new RawImage[2];
        for (int i = 0; i < 2; i++)
        {
            RawImage bar = NewRawImage(alertRoot, "Hazard Bar");
            bar.texture = hazard;
            bar.rectTransform.anchorMin = new Vector2(0f, i == 0 ? 0.88f : 0f);
            bar.rectTransform.anchorMax = new Vector2(1f, i == 0 ? 1f : 0.12f);
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
            alertBars[i] = bar;
        }

        Image band = NewImage(alertRoot, "Band", new Color(0.12f, 0f, 0.01f, 0.85f));
        AnchorBox(band.rectTransform, new Vector2(0f, 0.3f), new Vector2(1f, 0.78f));

        alertText = NewText(alertRoot, "Title", alertTitle, 140, Color.white);
        alertText.fontStyle = FontStyles.Bold | FontStyles.Italic;
        alertText.characterSpacing = 10f;
        alertText.outlineColor = new Color(0.5f, 0f, 0.05f);
        alertText.outlineWidth = 0.3f;
        alertText.enableVertexGradient = true;
        alertText.colorGradient = new VertexGradient(Color.white, Color.white, new Color(1f, 0.2f, 0.25f), new Color(1f, 0.2f, 0.25f));
        AnchorBox(alertText.rectTransform, new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.76f));
        TextGlow.Apply(alertText, new Color(1f, 0.05f, 0.1f), 16f, 0.5f, 9f);

        alertLines = NewText(alertRoot, "Report", "", 34, new Color(1f, 0.86f, 0.1f));
        alertLines.fontStyle = FontStyles.Bold;
        alertLines.characterSpacing = 4f;
        alertLines.outlineColor = Color.black;
        alertLines.outlineWidth = 0.3f;
        alertLines.enableAutoSizing = true;
        alertLines.fontSizeMin = 10f;
        AnchorBox(alertLines.rectTransform, new Vector2(0.08f, 0.31f), new Vector2(0.92f, 0.51f));

        alertRoot.gameObject.SetActive(false);
    }

    private static Texture2D HazardTexture()
    {
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        Color yellow = new Color(1f, 0.82f, 0.05f), black = new Color(0.06f, 0.06f, 0.06f);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                tex.SetPixel(x, y, ((x + y) / 8) % 2 == 0 ? yellow : black);
        tex.Apply();
        return tex;
    }

    // ---------------- Building ----------------

    private void Build()
    {
        GameObject canvasObj = new GameObject("IRL Death Canvas (runtime)");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95; // above the phone
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();
        canvasRect = canvasObj.GetComponent<RectTransform>();

        shadow = NewImage(canvasObj.transform, "Shadow", new Color(0f, 0f, 0f, 0.4f));
        shadow.sprite = GameSprites.SoftCircle();
        shadow.rectTransform.sizeDelta = new Vector2(260f, 260f);

        threat = NewImage(canvasObj.transform, "Threat", Color.white);
        threat.rectTransform.sizeDelta = new Vector2(300f, 300f);

        // Car extras, placed over the headlights and the windshield of CarFront()
        headlightGlows = new Image[GameSprites.CarHeadlights.Length];
        for (int i = 0; i < headlightGlows.Length; i++)
        {
            headlightGlows[i] = NewImage(threat.transform, "Headlight Glow", Color.clear);
            headlightGlows[i].sprite = GameSprites.SoftCircle();
            RectTransform g = headlightGlows[i].rectTransform;
            g.anchorMin = g.anchorMax = GameSprites.CarHeadlights[i];
            g.sizeDelta = new Vector2(120f, 120f);
        }
        lockOn = NewImage(canvasObj.transform, "Lock On", Color.clear);
        lockOn.sprite = GameSprites.Crosshair();
        lockOn.rectTransform.sizeDelta = new Vector2(220f, 220f);

        carCrack = NewRawImage(threat.transform, "Windshield Crack");
        carBlood = NewRawImage(threat.transform, "Windshield Blood");
        foreach (RawImage overlay in new[] { carCrack, carBlood })
        {
            Rect w = GameSprites.CarWindshield;
            overlay.rectTransform.anchorMin = w.min;
            overlay.rectTransform.anchorMax = w.max;
            overlay.rectTransform.offsetMin = overlay.rectTransform.offsetMax = Vector2.zero;
        }

        crack = NewRawImage(canvasObj.transform, "Crack");
        Stretch(crack.rectTransform);

        blood = NewRawImage(canvasObj.transform, "Blood");
        blood.rectTransform.sizeDelta = new Vector2(520f, 520f);

        darkness = NewImage(canvasObj.transform, "Darkness", Color.clear);
        Stretch(darkness.rectTransform);

        flash = NewImage(canvasObj.transform, "Flash", Color.clear);
        Stretch(flash.rectTransform);

        BuildThreatAlert(canvasObj.transform);

        // Big and slanted: white-to-blood-red gradient, thick black outline and a hard shadow behind.
        // (Only vertex colors and the outline: the font's glow/underlay break its outline.)
        titleRoot = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
        titleRoot.SetParent(canvasObj.transform, false);
        AnchorBox(titleRoot, new Vector2(0f, 0.52f), new Vector2(1f, 0.8f));
        // Dark red band behind the title, with bright edges, so it reads over anything
        Image plate = NewImage(titleRoot, "Plate", new Color(0.22f, 0f, 0.02f, 0.88f));
        Stretch(plate.rectTransform);
        foreach (float y in new[] { 0f, 1f })
        {
            Image edge = NewImage(titleRoot, "Plate Edge", new Color(1f, 0.15f, 0.2f));
            edge.rectTransform.anchorMin = new Vector2(0f, y);
            edge.rectTransform.anchorMax = new Vector2(1f, y);
            edge.rectTransform.sizeDelta = new Vector2(0f, 6f);
        }
        title = TitleText(titleRoot, "Text", Color.white);
        AnchorBox(title.rectTransform, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f));
        TextGlow.Apply(title, new Color(1f, 0.1f, 0.15f), 14f, 0.45f, 9f);
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(new Color(1f, 0.95f, 0.9f), new Color(1f, 0.95f, 0.9f),
                                                 new Color(0.85f, 0.02f, 0.08f), new Color(0.85f, 0.02f, 0.08f));

        // Subtitle: crisp text (no halo, it blurs at this size) on a solid dark box
        Image subtitlePlate = NewImage(canvasObj.transform, "Subtitle Box", new Color(0.05f, 0f, 0.01f, 0.9f));
        AnchorBox(subtitlePlate.rectTransform, new Vector2(0.12f, 0.36f), new Vector2(0.88f, 0.515f));
        subtitleBox = subtitlePlate.gameObject;
        subtitle = NewText(subtitlePlate.transform, "Subtitle", failedSubtitle, 34, new Color(1f, 0.86f, 0.1f));
        subtitle.fontStyle = FontStyles.Bold;
        subtitle.characterSpacing = 6f;
        subtitle.outlineColor = Color.black;
        subtitle.outlineWidth = 0.2f;
        subtitle.enableWordWrapping = false;
        AnchorBox(subtitle.rectTransform, new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.92f));

        GameObject buttonObj = new GameObject("Retry Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(canvasObj.transform, false);
        buttonObj.GetComponent<Image>().color = new Color(1f, 0.85f, 0.1f);
        Outline frame = buttonObj.AddComponent<Outline>();
        frame.effectColor = Color.black;
        frame.effectDistance = new Vector2(5f, -5f);
        retryButton = buttonObj.GetComponent<Button>();
        retryButton.onClick.AddListener(() => retryPressed = true);
        RectTransform brt = buttonObj.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.24f);
        brt.sizeDelta = new Vector2(300f, 80f);
        TextMeshProUGUI label = NewText(buttonObj.transform, "Label", "RETRY?", 44, Color.black);
        label.fontStyle = FontStyles.Bold | FontStyles.Italic;
        label.characterSpacing = 6f;
        Stretch(label.rectTransform);

        canvasObj.SetActive(false);
    }

    // The title slams in and shakes for a moment
    private IEnumerator ShakeTitle()
    {
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            titleRoot.anchoredPosition = Random.insideUnitCircle * 18f * (1f - t / 0.35f);
            yield return null;
        }
        titleRoot.anchoredPosition = Vector2.zero;
    }

    private TextMeshProUGUI TitleText(RectTransform parent, string name, Color color)
    {
        TextMeshProUGUI t = NewText(parent, name, failedTitle, 130, color);
        t.fontStyle = FontStyles.Bold | FontStyles.Italic;
        t.characterSpacing = 8f;
        t.outlineColor = Color.black;
        t.outlineWidth = 0.3f;
        Stretch(t.rectTransform);
        return t;
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

    private static RawImage NewRawImage(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RawImage img = go.GetComponent<RawImage>();
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI NewText(Transform parent, string name, string text, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 10f;
        tmp.fontSizeMax = size;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void AnchorBox(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
