using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Ending cutscene when the target score is reached, full screen with a surveillance camera look:
// monitos cross the street, two drones fly in carrying a "THANKS FOR PLAYING" banner between them
// (one at each end) and leave; then the freshly trained drones arrive (yours in front, with its star)
// and shoot the monitos, "THANKS FOR YOUR SERVICE" lights up, the credits roll and the game goes back to the start menu.
public class EndingSequence : MonoBehaviour
{
    [Header("Crowd")]
    public int walkerCount = 8;
    public Vector2 walkSpeedRange = new Vector2(0.6f, 1.1f);
    public float panicSpeedMultiplier = 2.5f;

    [Header("Drones")]
    public float droneSpeed = 1.6f;
    public string bannerText = "THANKS FOR PLAYING";
    [Tooltip("The big sign that lights up after the drones have killed the people")]
    public string finalMessage = "THANKS FOR YOUR SERVICE";
    [Tooltip("Seconds the banner stays in the middle of the screen")]
    public float bannerHoldSeconds = 2.8f;
    [Tooltip("Optional custom drone sprite. If empty, a pixel-art one generated in code is used")]
    public Sprite droneSpriteOverride;

    [Header("Shooting")]
    [Tooltip("Freshly trained drones that come in after the banner (the first one is yours)")]
    public int shooterCount = 3;
    public int victims = 5;
    public float bulletSpeed = 9f;

    private const int DroneOrder = 50;
    private const float EdgeMargin = 0.35f;

    private readonly List<NPCWalker> walkers = new List<NPCWalker>();
    private Transform root;
    private float viewLeft, viewRight, viewTop, streetY, droneY;
    private bool running;

    [Header("Credits")]
    public string[] creditRoles = { "DIRECTOR & PROGRAMMER", "ARTIST", "COMPOSER & MUSIC" };
    public string[] creditNames = { "Gold", "Luis Ortega", "Carlos Rodriguez" };

    private CinematicOverlay overlay;

    // The ending is NOT shown on the phone: it takes the whole screen, as if it were
    // a surveillance camera. The "aha" moment: you were being recorded the whole time.
    public IEnumerator Play(WarioStage backdropPrefab, Canvas phoneCanvas, WalkingBackground street)
    {
        running = true;
        walkers.Clear();

        if (overlay == null) overlay = gameObject.AddComponent<CinematicOverlay>();
        overlay.Show();
        overlay.SetCCTV(false);
        overlay.HideCaption();
        overlay.SetFade(0f);

        // 1. Everything fades to black (the phone too)
        yield return overlay.FadeTo(1f, 0.8f);
        yield return new WaitForSeconds(0.4f);

        if (phoneCanvas != null) phoneCanvas.enabled = false;
        if (street != null) street.SetVisible(false);

        // 2. Set: a minigame (the street) is reused as the backdrop, without its logic,
        //    and the minigame camera renders to the full screen
        WarioStage backdrop = Instantiate(backdropPrefab);
        backdrop.enabled = false;
        root = backdrop.transform;

        Camera cam = RenderCameraFollow.Instance != null ? RenderCameraFollow.Instance.Cam : null;
        RenderTexture phoneTexture = null;
        float camDepth = 0f;
        if (cam != null)
        {
            phoneTexture = cam.targetTexture;
            camDepth = cam.depth;
            cam.targetTexture = null;
            cam.ResetAspect();
            cam.depth = (Camera.main != null ? Camera.main.depth : 0f) + 1f;
            FrameFullScreen(cam, backdrop);
        }
        CalculateView(backdrop);

        // 3. Monitos crossing back and forth
        if (backdrop.npcPrefab != null)
        {
            for (int i = 0; i < walkerCount; i++) SpawnWalker(backdrop.npcPrefab, i);
        }
        StartCoroutine(WrapWalkersRoutine());

        // 4. Cut to the surveillance camera, with a couple of signal glitches
        overlay.SetCCTV(true);
        overlay.SetFade(0f);
        yield return new WaitForSeconds(0.08f);
        overlay.SetFade(1f);
        yield return new WaitForSeconds(0.06f);
        overlay.SetFade(0f);
        yield return new WaitForSeconds(0.12f);
        overlay.SetFade(0.8f);
        yield return new WaitForSeconds(0.05f);
        overlay.SetFade(0f);
        yield return new WaitForSeconds(1.2f);

        overlay.ShowCaption($"{GameManager.Instance?.targetScore ?? 1000} POINTS\n<size=60%>TRAINING COMPLETE</size>", new Color(1f, 0.86f, 0.1f), 0.72f, 60f);
        yield return new WaitForSeconds(2.5f);
        overlay.HideCaption();

        // 5. Two drones carry the banner in (one at each end), hold it in the middle and fly off
        TrailerCapture.Log("ending drones");
        yield return BannerFlight();
        yield return new WaitForSeconds(0.6f);

        // 6. The newly trained drones arrive (yours first, with its star and name) and open fire
        TrailerCapture.Log("ending shooters");
        List<Transform> shooters = new List<Transform>();
        for (int i = 0; i < shooterCount; i++)
        {
            // A bit lower than the banner flock
            Transform s = CreateDrone(new Vector3(viewLeft - 0.8f - i * 0.7f, droneY - 0.6f + (i % 2) * 0.25f, 0f));
            if (i == 0) AddPlayerBadge(s);
            shooters.Add(s);
        }
        // They fly in together (yours ends up on the right, in front) and wait until all are in place
        float longestTrip = 0f;
        for (int i = 0; i < shooters.Count; i++)
        {
            float x = Mathf.Lerp(viewRight - 0.9f, viewLeft + 0.9f, shooters.Count == 1 ? 0.5f : (float)i / (shooters.Count - 1));
            Vector3 slot = new Vector3(x, shooters[i].position.y, 0f);
            longestTrip = Mathf.Max(longestTrip, Vector3.Distance(shooters[i].position, slot));
            StartCoroutine(MoveTo(shooters[i], slot, droneSpeed * 2.5f));
        }
        yield return new WaitForSeconds(longestTrip / (droneSpeed * 2.5f) + 0.3f);

        int kills = 0;
        int attempts = 0;
        while (kills < victims && attempts < victims * 4)
        {
            attempts++;
            NPCWalker target = PickVisibleTarget((viewLeft + viewRight) / 2f + Random.Range(-1f, 1f));
            if (target == null)
            {
                yield return new WaitForSeconds(0.3f);
                continue;
            }

            // The closest drone takes the shot (yours fires first)
            Transform shooter = kills == 0 ? shooters[0] : ClosestTo(shooters, target.BodyCenter.x);

            // Lines up over the target
            float aimTime = 0.5f;
            while (aimTime > 0f && target != null && !target.IsDead)
            {
                aimTime -= Time.deltaTime;
                Vector3 desired = new Vector3(target.BodyCenter.x, shooter.position.y, 0f);
                shooter.position = Vector3.MoveTowards(shooter.position, desired, droneSpeed * 2f * Time.deltaTime);
                yield return null;
            }

            if (target == null || target.IsDead) continue;
            float shotX = target.BodyCenter.x;
            yield return Shoot(shooter.position + Vector3.down * 0.25f, target);
            kills++;

            if (kills == 1) Panic(shotX);
            yield return new WaitForSeconds(0.25f);
        }

        // 7. The drones leave and the message shows up
        for (int i = 0; i < shooters.Count; i++)
            StartCoroutine(MoveTo(shooters[i], new Vector3(viewRight + 1.5f + i * 0.5f, viewTop + 1f, 0f), droneSpeed * 2f));
        yield return new WaitForSeconds(1.8f);
        TrailerCapture.Log("ending thanks");
        overlay.ShowCaption(finalMessage, new Color(1f, 0.86f, 0.1f), 0.5f, 96f, new Color(1f, 0.35f, 0.05f));
        yield return SignFlickerOn();
        yield return new WaitForSeconds(2.6f);

        yield return overlay.FadeTo(1f, 1.2f);
        overlay.HideCaption();
        overlay.SetCCTV(false);

        // 8. Credits over black (fade in / fade out)
        yield return new WaitForSeconds(0.5f);
        for (int i = 0; i < Mathf.Min(creditRoles.Length, creditNames.Length); i++)
        {
            yield return overlay.ShowCredit(creditRoles[i], creditNames[i]);
            yield return new WaitForSeconds(0.3f);
        }

        // 9. Restore everything and go back to the menu
        running = false;
        walkers.Clear();
        Destroy(backdrop.gameObject);

        if (cam != null)
        {
            cam.targetTexture = phoneTexture;
            cam.ResetAspect();
            cam.depth = camDepth;
        }
        if (phoneCanvas != null) phoneCanvas.enabled = true;
        if (street != null) street.SetVisible(true);

        yield return null;
        overlay.Hide();
    }

    // Frames the street full screen: the biggest sprite of the set (the crosswalk)
    // fills the screen without empty borders.
    private static void FrameFullScreen(Camera cam, WarioStage backdrop)
    {
        SpriteRenderer biggest = null;
        float biggestArea = 0f;
        foreach (SpriteRenderer sr in backdrop.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr.name.ToLower().Contains("crosswalk")) { biggest = sr; break; }
            float area = sr.bounds.size.x * sr.bounds.size.y;
            if (area > biggestArea) { biggestArea = area; biggest = sr; }
        }

        Bounds b = biggest != null ? biggest.bounds : new Bounds(backdrop.GetCameraPosition(), new Vector3(9.6f, 4.32f, 0f));
        float distance = 20f;
        float halfH = Mathf.Min(b.extents.y, b.extents.x / cam.aspect) * 0.98f;

        cam.transform.position = new Vector3(b.center.x, b.center.y, b.center.z - distance);
        if (cam.orthographic) cam.orthographicSize = halfH;
        else cam.fieldOfView = 2f * Mathf.Atan(halfH / distance) * Mathf.Rad2Deg;
    }

    private void CalculateView(WarioStage backdrop)
    {
        Rect view = RenderCameraFollow.Instance != null
            ? RenderCameraFollow.Instance.GetVisibleWorldRect()
            : new Rect(backdrop.GetCameraPosition().x - 2.5f, backdrop.GetCameraPosition().y - 5f, 5f, 10f);

        viewLeft = view.xMin;
        viewRight = view.xMax;
        viewTop = view.yMax;

        streetY = backdrop.spawnPoint != null ? backdrop.spawnPoint.position.y : view.center.y;
        droneY = Mathf.Min(streetY + 1.8f, viewTop - 0.6f);
    }

    private void SpawnWalker(GameObject prefab, int index)
    {
        Vector3 pos = new Vector3(Random.Range(viewLeft + EdgeMargin, viewRight - EdgeMargin), streetY + Random.Range(-0.15f, 0.15f), 0f);
        GameObject obj = Instantiate(prefab, pos, Quaternion.identity, root);

        NPCAppearance.ClothingPart clothing = WarioStage.PickRandomTarget();
        NPCAppearance appearance = obj.GetComponent<NPCAppearance>();
        if (appearance != null) appearance.SetupAppearance(clothing);

        NPCWalker walker = obj.GetComponent<NPCWalker>();
        if (walker == null) return;

        walker.clothingPart = clothing;
        walker.isTarget = false;
        walker.randomStops = false;
        walker.moveSpeed = Random.Range(walkSpeedRange.x, walkSpeedRange.y);
        walker.SetWalkDirection(index % 2 == 0 ? Vector3.right : Vector3.left);
        walker.CanMove = true;
        walker.PopIn();
        walkers.Add(walker);
    }

    // Monitos never cross the edge of the screen: when they reach it they vanish
    // and pop back in on the other side, inside the screen.
    private IEnumerator WrapWalkersRoutine()
    {
        while (running)
        {
            foreach (NPCWalker w in walkers)
            {
                if (w == null || w.IsDead || w.IsPopping) continue;
                float x = w.transform.position.x;
                bool atEdge = w.WalkDirection.x > 0 ? x > viewRight - EdgeMargin : x < viewLeft + EdgeMargin;
                if (atEdge) StartCoroutine(RespawnOtherSide(w));
            }
            yield return null;
        }
    }

    private IEnumerator RespawnOtherSide(NPCWalker w)
    {
        w.PopOut(false);
        yield return new WaitWhile(() => w != null && !w.IsDead && w.IsPopping);
        if (w == null || w.IsDead) yield break;

        Vector3 p = w.transform.position;
        p.x = w.WalkDirection.x > 0 ? viewLeft + EdgeMargin + 0.05f : viewRight - EdgeMargin - 0.05f;
        w.transform.position = p;
        w.PopIn();
    }

    // Everybody runs away from the first shot: faster, legs pumping faster, and no more polite queue
    // (they push past each other)
    private void Panic(float shotX)
    {
        foreach (NPCWalker w in walkers)
        {
            if (w == null || w.IsDead) continue;
            w.moveSpeed *= panicSpeedMultiplier;
            w.personalSpace = 0f;
            w.SetWalkDirection(w.transform.position.x < shotX ? Vector3.left : Vector3.right);
            foreach (Animator legs in w.GetComponentsInChildren<Animator>(true)) legs.speed = panicSpeedMultiplier;
        }
    }

    private NPCWalker PickVisibleTarget(float fromX)
    {
        NPCWalker best = null;
        float bestDist = float.MaxValue;
        foreach (NPCWalker w in walkers)
        {
            if (w == null || w.IsDead) continue;
            float x = w.transform.position.x;
            if (w.IsPopping || x < viewLeft + EdgeMargin || x > viewRight - EdgeMargin) continue;
            float d = Mathf.Abs(x - fromX);
            if (d < bestDist)
            {
                bestDist = d;
                best = w;
            }
        }
        return best;
    }

    private IEnumerator Shoot(Vector3 from, NPCWalker target)
    {
        GameObject bullet = CreateSprite("Bullet", GameSprites.Bullet(), from, DroneOrder + 2);
        SoundFX.Play(Sfx.DroneShot);

        while (target != null && !target.IsDead)
        {
            Vector3 to = target.BodyCenter;
            bullet.transform.position = Vector3.MoveTowards(bullet.transform.position, to, bulletSpeed * Time.deltaTime);
            if (Vector3.Distance(bullet.transform.position, to) < 0.05f) break;
            yield return null;
        }

        Destroy(bullet);
        if (target != null && !target.IsDead)
        {
            SoundFX.Play(Sfx.DroneHit);
            target.Kill(); // Kill() spills the blood
        }
    }

    private Transform CreateDrone(Vector3 position)
    {
        GameObject drone = new GameObject("Drone");
        drone.transform.SetParent(root, true);
        drone.transform.position = position;

        Sprite frameA = droneSpriteOverride != null ? droneSpriteOverride : GameSprites.Drone(0);
        Sprite frameB = droneSpriteOverride != null ? droneSpriteOverride : GameSprites.Drone(1);

        GameObject body = CreateSprite("Body", frameA, position, DroneOrder);
        body.transform.SetParent(drone.transform, true);

        DroneVisual visual = drone.AddComponent<DroneVisual>();
        visual.body = body.GetComponent<SpriteRenderer>();
        visual.frames = new[] { frameA, frameB };

        return drone.transform;
    }

    private static Transform ClosestTo(List<Transform> drones, float x)
    {
        Transform best = drones[0];
        foreach (Transform d in drones)
            if (Mathf.Abs(d.position.x - x) < Mathf.Abs(best.position.x - x)) best = d;
        return best;
    }

    // The player's drone: a small gold star painted on its body (where the lights are),
    // child of the body so it bobs with it
    private void AddPlayerBadge(Transform drone)
    {
        Transform body = drone.Find("Body");
        if (body == null) return;
        // Body center of GameSprites.Drone: pixel (11.5, 5) of 24x12 at 30 px per unit
        GameObject star = CreateSprite("Star", GameSprites.Star(), body.position + new Vector3(-0.017f, -0.033f, -0.01f), DroneOrder + 1);
        star.transform.localScale = Vector3.one * 0.17f;
        star.transform.SetParent(body, true);
    }

    // ---------------- Banner ----------------

    // The banner hangs between two drones. It flies in from the right, stops in the middle
    // swaying a little (long enough to read it) and then the drones lift it off the top of the screen.
    private IEnumerator BannerFlight()
    {
        float viewWidth = viewRight - viewLeft;
        float width = Mathf.Min(4.4f, viewWidth * 0.6f);
        float height = width * 0.16f;

        Transform group = BuildBanner(width, height);
        // Low enough that the two drones above the banner are inside the frame
        float droneTop = height / 2f + 0.42f + 0.35f; // drone lift + half a drone + a margin
        Vector3 center = new Vector3((viewLeft + viewRight) / 2f, Mathf.Min(droneY - 0.1f, viewTop - droneTop), 0f);
        Vector3 start = new Vector3(viewRight + width / 2f + 1.2f, center.y + 0.3f, 0f);
        Vector3 exit = new Vector3(center.x - 0.6f, viewTop + height + 1.5f, 0f);
        group.position = start;

        // In: decelerates into place
        float t = 0f;
        const float inTime = 2.2f;
        while (t < inTime)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / inTime), 3f);
            group.position = Vector3.Lerp(start, center, k);
            group.localRotation = Quaternion.Euler(0f, 0f, (1f - k) * 6f); // leaning into the turn
            yield return null;
        }

        // Hold: gentle sway
        t = 0f;
        while (t < bannerHoldSeconds)
        {
            t += Time.deltaTime;
            group.position = center + new Vector3(0f, Mathf.Sin(t * 2.2f) * 0.05f, 0f);
            group.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.7f) * 2.5f);
            yield return null;
        }

        // Out: accelerates up and away
        Vector3 from = group.position;
        t = 0f;
        const float outTime = 1.6f;
        while (t < outTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / outTime);
            group.position = Vector3.Lerp(from, exit, k * k);
            group.localRotation = Quaternion.Euler(0f, 0f, -k * 8f);
            yield return null;
        }
        Destroy(group.gameObject);
    }

    // Builds: a drone at each end, ropes down to the banner, a dark plate with a yellow border
    // and big yellow glowing text (same look as the game's other signs)
    private Transform BuildBanner(float width, float height)
    {
        Transform group = new GameObject("Banner Flight").transform;
        group.SetParent(root, false);

        float droneLift = height / 2f + 0.42f;
        foreach (float side in new[] { -1f, 1f })
        {
            Transform drone = CreateDrone(Vector3.zero);
            drone.SetParent(group, false);
            drone.localPosition = new Vector3(side * (width / 2f - 0.05f), droneLift, 0f);

            GameObject rope = CreateSprite("Rope", GameSprites.Pixel(), Vector3.zero, DroneOrder - 2);
            rope.transform.SetParent(group, false);
            rope.transform.localPosition = new Vector3(side * (width / 2f - 0.05f), height / 2f + 0.17f, 0f);
            rope.transform.localScale = new Vector3(0.025f, 0.34f, 1f);
            rope.GetComponent<SpriteRenderer>().color = new Color(0.12f, 0.12f, 0.12f);
        }

        Color yellow = new Color(1f, 0.86f, 0.1f);
        LocalRect(group, "Banner Plate", 0f, 0f, width, height, new Color(0.07f, 0.05f, 0.12f), DroneOrder - 1);
        const float border = 0.05f;
        LocalRect(group, "Border Top", 0f, height / 2f - border / 2f, width, border, yellow, DroneOrder);
        LocalRect(group, "Border Bottom", 0f, -height / 2f + border / 2f, width, border, yellow, DroneOrder);
        LocalRect(group, "Border Left", -width / 2f + border / 2f, 0f, border, height, yellow, DroneOrder);
        LocalRect(group, "Border Right", width / 2f - border / 2f, 0f, border, height, yellow, DroneOrder);

        GameObject textObj = new GameObject("Banner Text");
        textObj.transform.SetParent(group, false);
        textObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        TextMeshPro text = textObj.AddComponent<TextMeshPro>();
        text.text = bannerText;
        text.color = yellow;
        text.fontStyle = FontStyles.Bold | FontStyles.Italic;
        text.characterSpacing = 4f;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.05f;
        text.fontSizeMax = 20f;
        text.outlineWidth = 0.28f;
        text.outlineColor = Color.black;
        text.rectTransform.sizeDelta = new Vector2(width * 0.88f, height * 0.72f);
        text.sortingOrder = DroneOrder + 3;
        TextGlow.Apply(text, new Color(1f, 0.4f, 0.05f), height * 0.07f, 0.45f, height * 0.06f);

        return group;
    }

    private void LocalRect(Transform parent, string name, float x, float y, float w, float h, Color color, int order)
    {
        GameObject go = CreateSprite(name, GameSprites.Pixel(), Vector3.zero, order);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, 0f);
        go.transform.localScale = new Vector3(w, h, 1f);
        go.GetComponent<SpriteRenderer>().color = color;
    }

    // The final sign turns on like a neon: a few sparks, then it stays lit
    private IEnumerator SignFlickerOn()
    {
        float[] steps = { 0.06f, 0.09f, 0.05f, 0.16f, 0.04f, 0.07f };
        for (int i = 0; i < steps.Length; i++)
        {
            overlay.SetCaptionAlpha(i % 2 == 0 ? 1f : 0.1f);
            yield return new WaitForSeconds(steps[i]);
        }
        overlay.SetCaptionAlpha(1f);
        SoundFX.Play(Sfx.Success);
    }

    private GameObject CreateSprite(string name, Sprite sprite, Vector3 position, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, true);
        go.transform.position = position;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return go;
    }

    private static IEnumerator MoveTo(Transform t, Vector3 target, float speed)
    {
        while (t != null && Vector3.Distance(t.position, target) > 0.01f)
        {
            t.position = Vector3.MoveTowards(t.position, target, speed * Time.deltaTime);
            yield return null;
        }
    }
}
