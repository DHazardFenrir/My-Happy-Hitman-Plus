using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using static StageBuilder;

// "Shoot this!" minigames: you look through a drone's camera at a crowd staring at their phones.
// Aim with the mouse (or arrows / WASD, or tap where you want to shoot) and press to fire. One shot only:
// hit the target and it's a win; hit someone else or nobody and it's a fail. The drone wobbles in the wind.
// The set (buildings, billboard, pavement...) lives in each stage's prefab; the crowd, the
// crosshair and the camera HUD are made here because they adapt to the part of the phone you see.
public abstract class DroneCamStage : WarioStage
{
    [Header("Drone camera")]
    [Tooltip("How much the crosshair wobbles with the wind (world units)")]
    [SerializeField] protected float swayAmount = 0.12f;
    [SerializeField] private float keyAimSpeed = 2.2f;
    [Tooltip("How far from the crosshair a shot still hits someone (world units). Bigger = more forgiving")]
    [SerializeField] private float hitRadius = 0.22f;
    [SerializeField] private int crowdSize = 10; // max 10: one per shirt/pants combination
    [Tooltip("How fast the target strolls while looking at the phone (it never leaves the frame)")]
    [SerializeField] private Vector2 strollSpeed = new Vector2(0.25f, 0.6f);
    [Tooltip("Everybody else walks faster and keeps leaving and coming back in through the edges")]
    [SerializeField] private Vector2 passerbySpeed = new Vector2(0.7f, 1.2f);

    [Header("Set")]
    [Tooltip("Where the feet of the crowd's front row go (the other two rows stand behind, higher up)")]
    [SerializeField] private Transform crowdFrontRow;
    [Tooltip("Part of the phone visible on a 16:9 monitor (local units). Only drawn as a gizmo, to lay out the set")]
    [SerializeField] private Rect designFrame = Rect.MinMaxRect(-2.91f, -6.832f, 2.91f, 2.806f);

    // Vertical distance between the crowd's rows (they overlap about half a monito)
    public const float RowStep = 0.3f;

    // Part of the phone screen that is visible on the monitor, in the stage's local space.
    // At 16:9 the bottom of the phone is off-screen, so the whole minigame lives in here.
    protected Rect visible;
    // Everything the camera sees (also local): backgrounds cover this so there are no gaps
    protected Rect fullView;

    private Transform crosshair;
    private SpriteRenderer crosshairSprite;
    private SpriteRenderer recDot;
    private Transform interference;
    private Vector2 aim;
    private Vector3 lastMouse;
    private bool fired;
    private NPCWalker autopilotPick;
    private readonly Dictionary<NPCWalker, int> rowOf = new Dictionary<NPCWalker, int>();

    public override IrlDeathKind IrlDeath => IrlDeathKind.ShotByDrone; // another drone shoots you
    public override Sprite IrlThreatSprite => GameSprites.Drone(0);

    protected abstract Color HudColor { get; }
    // Color wash of the drone camera (transparent = none)
    protected virtual Color FilterTint => new Color(0.55f, 0.95f, 1f, 0.07f);
    protected abstract string CamLabel { get; }
    protected virtual void BuildOverlay() { }
    protected virtual void OnAimMoved(Vector2 localAim) { }

    public override void SetupStage(NPCAppearance.ClothingPart target)
    {
        extraNPCsCount = crowdSize - 1;
        base.SetupStage(target); // focuses the camera and spawns the monitos (in a row, rearranged below)

        Vector2 origin = transform.position;
        fullView = new Rect(view.position - origin, view.size);
        visible = VisibleWorldRect();
        visible.position -= origin;

        // Packed like a crowd, above the HUD text at the bottom
        ArrangeCrowd(crowdFrontRow.localPosition.y, RowStep);
        BuildHud(target);
        BuildOverlay();

        crosshairSprite = SpriteAt(transform, "Crosshair", GameSprites.Crosshair(), 0f, 0f, 0.45f, HudColor, 35);
        crosshair = crosshairSprite.transform;
        aim = visible.center;
        lastMouse = Input.mousePosition;
    }

    protected override void OnStageUpdate()
    {
        if (fired) return;

        KeepWanderersInside();
        if (recDot != null) recDot.enabled = Mathf.Repeat(Time.time, 1f) < 0.6f;
        if (interference != null)
        {
            float y = Mathf.Lerp(fullView.yMin - 0.3f, fullView.yMax + 0.3f, Mathf.Repeat(Time.time * 0.35f, 1f));
            interference.localPosition = new Vector3(fullView.center.x, y, 0f);
        }

        if (Autopilot.Enabled) AutopilotAim();
        else PlayerAim();

        aim.x = Mathf.Clamp(aim.x, visible.xMin + 0.1f, visible.xMax - 0.1f);
        aim.y = Mathf.Clamp(aim.y, visible.yMin + 0.1f, visible.yMax - 0.1f);

        // The drone wobbles: the crosshair drifts around the point you aim at
        Vector2 sway = new Vector2(Mathf.PerlinNoise(Time.time * 0.9f, 3.1f) - 0.5f,
                                   Mathf.PerlinNoise(7.7f, Time.time * 0.9f) - 0.5f) * 2f * swayAmount;
        Vector2 point = aim + sway;
        crosshair.localPosition = new Vector3(point.x, point.y, -0.05f);
        crosshairSprite.color = PersonUnder(point) != null ? new Color(1f, 0.85f, 0.1f) : HudColor;
        OnAimMoved(point);

        if (SingleInput.ActionPressed) StartCoroutine(Fire(point));
    }

    // ---------------- Aiming ----------------

    private void PlayerAim()
    {
        // Tapping moves the crosshair there (and the tap itself fires)
        if (Input.touchCount > 0)
        {
            if (ScreenToLocal(Input.GetTouch(0).position, out Vector2 touched)) aim = touched;
        }
        else if ((Input.mousePosition - lastMouse).sqrMagnitude > 0.25f)
        {
            lastMouse = Input.mousePosition;
            if (ScreenToLocal(lastMouse, out Vector2 pointed)) aim = pointed;
        }

        Vector2 keys = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        aim += keys * keyAimSpeed * Time.deltaTime;
    }

    // The bot slides the crosshair onto the target (or a decoy when it decided to miss) and shoots
    private void AutopilotAim()
    {
        if (autopilotPick == null || autopilotPick.IsDead || autopilotPick.IsGone)
        {
            List<NPCWalker> options = spawnedNPCs.FindAll(n => n != null && !n.IsDead && !n.IsGone && n.isTarget != autopilotMisses);
            if (options.Count == 0) return;
            autopilotPick = options[Random.Range(0, options.Count)];
        }

        Vector2 goal = (Vector2)autopilotPick.BodyRect.center - (Vector2)transform.position;
        aim = Vector2.MoveTowards(aim, goal, 2.5f * Time.deltaTime);
        bool ready = timeLimit - TimeRemaining > autopilotReactTime + 0.6f;
        if (ready && Vector2.Distance(crosshair.localPosition, goal) < 0.06f) Autopilot.Press();
    }

    // Screen position (mouse / touch) -> point in the stage, going through the phone screen
    // (the RawImage that shows the minigame camera's render texture)
    private bool ScreenToLocal(Vector2 screenPos, out Vector2 local)
    {
        local = aim;
        RectTransform screen = GameManager.Instance != null ? GameManager.Instance.gameScreen : null;
        Camera cam = RenderCameraFollow.Instance != null ? RenderCameraFollow.Instance.Cam : null;
        if (screen == null || cam == null) return false;

        Canvas canvas = screen.GetComponentInParent<Canvas>().rootCanvas;
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(screen, screenPos, uiCam, out Vector2 inScreen)) return false;

        Vector2 uv = Rect.PointToNormalized(screen.rect, inScreen);
        float depth = transform.position.z - cam.transform.position.z;
        local = cam.ViewportToWorldPoint(new Vector3(uv.x, uv.y, depth)) - transform.position;
        return true;
    }

    // The visible part of the phone screen, in world units on the stage plane
    private Rect VisibleWorldRect()
    {
        Camera cam = RenderCameraFollow.Instance != null ? RenderCameraFollow.Instance.Cam : null;
        RectTransform screen = GameManager.Instance != null ? GameManager.Instance.gameScreen : null;
        if (cam == null || screen == null) return view;

        Vector3[] s = new Vector3[4], m = new Vector3[4];
        screen.GetWorldCorners(s);
        ((RectTransform)screen.GetComponentInParent<Canvas>().rootCanvas.transform).GetWorldCorners(m);
        float u0 = Mathf.InverseLerp(s[0].x, s[2].x, Mathf.Max(s[0].x, m[0].x));
        float u1 = Mathf.InverseLerp(s[0].x, s[2].x, Mathf.Min(s[2].x, m[2].x));
        float v0 = Mathf.InverseLerp(s[0].y, s[2].y, Mathf.Max(s[0].y, m[0].y));
        float v1 = Mathf.InverseLerp(s[0].y, s[2].y, Mathf.Min(s[2].y, m[2].y));

        float depth = transform.position.z - cam.transform.position.z;
        Vector3 a = cam.ViewportToWorldPoint(new Vector3(u0, v0, depth));
        Vector3 b = cam.ViewportToWorldPoint(new Vector3(u1, v1, depth));
        return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    }

    // ---------------- Shooting ----------------

    private IEnumerator Fire(Vector2 point)
    {
        fired = true;
        crosshairSprite.color = Color.white;
        SoundFX.Play(Sfx.DroneShot);

        SpriteRenderer flash = SpriteAt(transform, "Shot Flash", GameSprites.SoftCircle(), point.x, point.y, 0.3f, new Color(1f, 1f, 0.8f, 0.9f), 40);
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 0.6f, t / 0.12f);
            flash.color = new Color(1f, 1f, 0.8f, 0.9f * (1f - t / 0.12f));
            yield return null;
        }
        Destroy(flash.gameObject);

        NPCWalker victim = PersonUnder(point);
        Panic(point.x, victim);
        if (victim != null)
        {
            SoundFX.Play(Sfx.DroneHit);
            OnTrapHitNPC(victim);
        }
        else
        {
            SpriteAt(transform, "Bullet Hole", GameSprites.SoftCircle(), point.x, point.y, 0.12f, new Color(0.1f, 0.1f, 0.1f, 0.8f), 2);
            OnTrapMissed();
        }
    }

    // Who the shot hits: anyone whose body is within hitRadius of the crosshair (a bullet grazing them
    // still counts). If the crosshair is right on several people, the one in front; otherwise the closest.
    private NPCWalker PersonUnder(Vector2 localPoint)
    {
        Vector2 world = localPoint + (Vector2)transform.position;
        NPCWalker best = null;
        float bestDistance = float.MaxValue;
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc == null || npc.IsDead || npc.IsGone) continue;
            Rect body = npc.BodyRect;
            Vector2 closest = new Vector2(Mathf.Clamp(world.x, body.xMin, body.xMax), Mathf.Clamp(world.y, body.yMin, body.yMax));
            float distance = Vector2.Distance(world, closest); // 0 when the crosshair is on the body
            if (distance > hitRadius) continue;

            bool better = best == null || distance < bestDistance - 0.001f
                || (Mathf.Abs(distance - bestDistance) <= 0.001f && rowOf[npc] > rowOf[best]);
            if (better)
            {
                best = npc;
                bestDistance = distance;
            }
        }
        return best;
    }

    // After the shot everybody runs away from it
    private void Panic(float shotX, NPCWalker victim)
    {
        float worldShotX = shotX + transform.position.x;
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc == null || npc == victim || npc.IsDead || npc.IsGone) continue;
            npc.CanMove = true;
            npc.randomStops = false;
            npc.moveSpeed = Random.Range(3f, 4.5f);
            npc.SetWalkDirection(npc.transform.position.x < worldShotX ? Vector3.left : Vector3.right);
        }
    }

    // ---------------- Crowd ----------------

    // Three rows (the back one higher and drawn behind), spread across the visible width
    private void ArrangeCrowd(float frontFeet, float rowStep)
    {
        List<NPCWalker> crowd = spawnedNPCs.FindAll(n => n != null);
        for (int i = crowd.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (crowd[i], crowd[j]) = (crowd[j], crowd[i]);
        }

        const int rows = 3;
        for (int i = 0; i < crowd.Count; i++)
        {
            NPCWalker npc = crowd[i];
            int row = i % rows; // 0 = back, 2 = front
            int inRow = (crowd.Count - row + rows - 1) / rows;
            int k = i / rows;
            float x = Mathf.Lerp(visible.xMin + 0.4f, visible.xMax - 0.4f, (k + 0.5f) / inRow) + Random.Range(-0.12f, 0.12f) + (row == 1 ? 0.2f : 0f);
            float feet = frontFeet + (rows - 1 - row) * rowStep;

            Vector3 p = transform.position + new Vector3(x, 0f, 0f);
            p.y = npc.transform.position.y;
            npc.transform.position = p;
            p.y += transform.position.y + feet - npc.BodyRect.yMin;
            npc.transform.position = p;

            npc.SetWalkDirection(Random.value < 0.5f ? Vector3.left : Vector3.right);
            SortingGroup group = npc.gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = 3 + row;
            rowOf[npc] = row;

            // Everybody walks (the walk animation always plays, so standing still looks wrong)
            npc.moveSpeed = npc.isTarget ? Random.Range(strollSpeed.x, strollSpeed.y) : Random.Range(passerbySpeed.x, passerbySpeed.y);
        }
    }

    // Passers-by walk off one side and come back in from the other until time runs out
    protected override void HandleScreenEdges()
    {
        const float offscreen = 0.35f;
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc == null || npc.IsDead || npc.IsGone || npc.isTarget || fired) continue;
            Vector3 p = npc.transform.position;
            float x = p.x - transform.position.x;
            if (npc.WalkDirection.x > 0f && x > fullView.xMax + offscreen) p.x = transform.position.x + fullView.xMin - offscreen;
            else if (npc.WalkDirection.x < 0f && x < fullView.xMin - offscreen) p.x = transform.position.x + fullView.xMax + offscreen;
            npc.transform.position = p;
        }
    }

    // The target bounces between the edges, so it's always somewhere on screen
    private void KeepWanderersInside()
    {
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc == null || npc.IsDead || npc.IsGone || !npc.isTarget) continue;
            float x = npc.transform.position.x - transform.position.x;
            if (x < visible.xMin + 0.5f && npc.WalkDirection.x < 0f) npc.SetWalkDirection(Vector3.right);
            else if (x > visible.xMax - 0.5f && npc.WalkDirection.x > 0f) npc.SetWalkDirection(Vector3.left);
        }
    }

    // The part of the phone you see on a 16:9 monitor, so the set can be laid out by hand
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(designFrame.center, designFrame.size);
    }

    // ---------------- Camera HUD ----------------

    private void BuildHud(NPCAppearance.ClothingPart target)
    {
        Transform hud = new GameObject("Drone HUD").transform;
        hud.SetParent(transform, false);
        Color c = HudColor;

        // Camera filter over the whole picture (below the HUD): tint, scanlines, vignette and a
        // faint interference band that keeps rolling up the screen
        Rect fv = fullView;
        if (FilterTint.a > 0f) Box(hud, "Camera Tint", fv.center.x, fv.center.y, fv.width + 1f, fv.height + 1f, FilterTint, 26);
        Tiled(hud, "Scanlines", GameSprites.Scanlines(), fv.center.x, fv.center.y, fv.width + 1f, fv.height + 1f, 27);
        SpriteRenderer vignette = SpriteAt(hud, "Vignette", GameSprites.CameraVignette(), visible.center.x, visible.center.y, 1f, Color.white, 28);
        vignette.transform.localScale = new Vector3(visible.width, visible.height, 1f);
        interference = Box(hud, "Interference", fv.center.x, fv.center.y, fv.width + 1f, 0.22f, new Color(1f, 1f, 1f, 0.06f), 28).transform;
        Rect r = visible;
        const float inset = 0.12f, len = 0.35f, th = 0.035f;

        // Viewfinder corners
        foreach (Vector2 corner in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) })
        {
            float sx = corner.x < r.center.x ? 1f : -1f, sy = corner.y < r.center.y ? 1f : -1f;
            Vector2 p = corner + new Vector2(sx, sy) * inset;
            Box(hud, "Corner H", p.x + sx * len / 2f, p.y, len, th, c, 30);
            Box(hud, "Corner V", p.x, p.y + sy * len / 2f, th, len, c, 30);
        }

        float top = r.yMax - 0.55f;
        recDot = Box(hud, "REC Dot", r.xMin + 0.3f, top, 0.09f, 0.09f, new Color(1f, 0.15f, 0.15f), 31);
        Label(hud, "REC", r.xMin + 0.55f, top, 0.4f, 0.16f, c, TextAlignmentOptions.Left);
        Label(hud, CamLabel, r.xMax - 0.9f, top, 1.4f, 0.16f, c, TextAlignmentOptions.Right);
        Label(hud, "ALT 38M   WIND 12KM/H", r.xMin + 1.05f, r.yMin + 0.3f, 1.6f, 0.13f, c, TextAlignmentOptions.Left);
        Label(hud, $"TARGET: {GameManager.ShirtName(target)} + {GameManager.PantsName(target)}",
            r.center.x, r.yMin + 0.55f, r.width * 0.8f, 0.17f, new Color(1f, 0.86f, 0.1f), TextAlignmentOptions.Center);
    }

    private static void Label(Transform parent, string text, float x, float y, float w, float h, Color color, TextAlignmentOptions align)
    {
        TextMeshPro t = WorldText(parent, text, x, y, w, h, color, 31);
        t.alignment = align;
        t.outlineColor = Color.black;
        t.outlineWidth = 0.25f;
    }
}
