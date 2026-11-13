using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static StageBuilder;

// Tenth minigame: the drone finds the target by its phone's signal. Through the drone camera in
// "signal mode" every person shows a device icon. HOLD the button and a scan ring grows from the
// drone's scan origin (which hovers near the target and drifts); RELEASE when the ring holds ONLY
// the target's device. Just the target = DEVICE MATCH and the drone fires. Anyone else inside =
// the system fires at every device in the ring (a fail). Releasing before reaching the target is
// NO SIGNAL; holding until the ring maxes out is SCAN OVERLOAD. One scan per round.
// The set lives in its prefab; the crowd and the camera HUD come from DroneCamStage.
public class SignalScanStage : DroneCamStage
{
    [Header("Signal scan")]
    [Tooltip("How fast the ring grows (units per second) at 0 points and at the target score")]
    [SerializeField] private Vector2 ringSpeed = new Vector2(1.3f, 2.1f);
    [Tooltip("Biggest the ring gets before SCAN OVERLOAD")]
    [SerializeField] private float maxRadius = 2.6f;
    [Tooltip("How far from the target's device the scan origin hovers (min, max)")]
    [SerializeField] private Vector2 originOffset = new Vector2(0.25f, 0.5f);

    private static readonly Color Cyan = new Color(0.35f, 0.95f, 1f);
    private static readonly Color Red = new Color(1f, 0.25f, 0.25f);

    private readonly Dictionary<NPCWalker, SpriteRenderer> devices = new Dictionary<NPCWalker, SpriteRenderer>();
    private Transform origin, ringT;
    private SpriteRenderer ring;
    private Vector2 offsetDir;
    private float offsetDistance, radius, speed, nextPing;
    private bool holding, released;
    private NPCWalker target;

    // Autopilot: it holds the button, and lets go once the ring reaches this radius
    private bool autopilotHolding;
    private float autopilotRelease;

    public override WalkTheme Theme => WalkTheme.Crosswalk;
    public override string ActionVerb => "SCAN!";
    protected override Color HudColor => new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f);
    protected override string CamLabel => "SIGNAL SCAN // " + AssignedDrone.Tag;
    protected override Color FilterTint => new Color(0.05f, 0.25f, 0.6f, 0.28f);
    protected override bool HasCrosshair => false;

    public override StageTarget PickTarget()
    {
        StageTarget t = base.PickTarget();
        t.header = "SCAN";
        t.hint = "HOLD TO SCAN, RELEASE ON THE TARGET";
        return t;
    }

    protected override void BuildOverlay()
    {
        float progress = GameManager.Instance != null ? Mathf.Clamp01((float)GameManager.Instance.Score / GameManager.Instance.targetScore) : 0f;
        speed = Mathf.Lerp(ringSpeed.x, ringSpeed.y, progress);
        target = spawnedNPCs.Find(n => n != null && n.isTarget);

        // A device above every head (they follow their owner every frame)
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc == null) continue;
            devices[npc] = SpriteAt(transform, "Device", GameSprites.PhoneSignal(), 0f, 0f, 2.2f, Cyan, 32);
        }

        // The scan origin: where the drone hovers, near the target but never right on it
        offsetDir = Random.insideUnitCircle.normalized;
        if (Mathf.Abs(offsetDir.y) > 0.6f) offsetDir = new Vector2(Mathf.Sign(offsetDir.x + 0.01f), 0.4f * Mathf.Sign(offsetDir.y)).normalized;
        offsetDistance = Random.Range(originOffset.x, originOffset.y) * Mathf.Lerp(0.6f, 1f, progress); // closer early: a small ring is enough
        origin = new GameObject("Scan Origin").transform;
        origin.SetParent(transform, false);
        SpriteAt(origin, "Drone", GameSprites.Drone(0), 0f, 0.05f, 0.6f, Color.white, 33);
        Box(origin, "Dot", 0f, 0f, 0.07f, 0.07f, Cyan, 34);
        ring = SpriteAt(transform, "Scan Ring", GameSprites.ScanRing(), 0f, 0f, 0.001f, Cyan, 33);
        ringT = ring.transform;
        ring.enabled = false;
        MoveDevicesAndOrigin(instant: true);
    }

    protected override void OnStageUpdate()
    {
        AnimateHud();
        if (released) return;
        KeepWanderersInside();
        MoveDevicesAndOrigin(instant: false);

        if (Autopilot.Enabled) AutopilotTick();

        if (!holding)
        {
            if (!SingleInput.ActionPressed) return;
            holding = true;
            ring.enabled = true;
            nextPing = 0f;
        }

        if (!SingleInput.ActionHeld)
        {
            StartCoroutine(Resolve());
            return;
        }

        // Holding: the ring grows and pings, faster as it gets bigger
        radius += speed * Time.deltaTime;
        ringT.localScale = Vector3.one * radius;
        ringT.localPosition = origin.localPosition;
        HighlightInside();
        nextPing -= Time.deltaTime;
        if (nextPing <= 0f)
        {
            SoundFX.Play(Sfx.ScanPing, 0.4f + 0.6f * radius / maxRadius);
            nextPing = Mathf.Lerp(0.28f, 0.1f, radius / maxRadius);
        }
        if (radius >= maxRadius) StartCoroutine(Resolve());
    }

    // ---------------- Devices and the scan origin ----------------

    private Vector2 DevicePoint(NPCWalker npc)
    {
        Rect body = npc.BodyRect;
        return new Vector2(body.center.x, body.yMax + 0.17f) - (Vector2)transform.position;
    }

    private void MoveDevicesAndOrigin(bool instant)
    {
        foreach (var pair in devices)
        {
            if (pair.Key == null) continue;
            pair.Value.enabled = !pair.Key.IsDead && !pair.Key.IsGone;
            pair.Value.transform.localPosition = DevicePoint(pair.Key);
        }

        // The drone drifts around the target with some lag (and the wind)
        if (target == null) return;
        Vector2 wobble = new Vector2(Mathf.PerlinNoise(Time.time * 0.6f, 1.3f) - 0.5f, Mathf.PerlinNoise(4.1f, Time.time * 0.6f) - 0.5f) * swayAmount * 2f;
        Vector2 goal = DevicePoint(target) + offsetDir * offsetDistance + wobble;
        goal.x = Mathf.Clamp(goal.x, visible.xMin + 0.3f, visible.xMax - 0.3f);
        goal.y = Mathf.Clamp(goal.y, visible.yMin + 0.9f, visible.yMax - 0.8f);
        origin.localPosition = instant ? (Vector3)goal : Vector3.Lerp(origin.localPosition, goal, 2.5f * Time.deltaTime);
    }

    private List<NPCWalker> Inside()
    {
        List<NPCWalker> inside = new List<NPCWalker>();
        foreach (NPCWalker npc in devices.Keys)
        {
            if (npc == null || npc.IsDead || npc.IsGone) continue;
            if (Vector2.Distance(DevicePoint(npc), origin.localPosition) <= radius) inside.Add(npc);
        }
        return inside;
    }

    // Devices inside the ring light up (white), the rest stay cyan
    private void HighlightInside()
    {
        List<NPCWalker> inside = Inside();
        foreach (var pair in devices) if (pair.Key != null) pair.Value.color = inside.Contains(pair.Key) ? Color.white : Cyan;
    }

    // ---------------- Result ----------------

    private IEnumerator Resolve()
    {
        released = true;
        Autopilot.Hold(false);
        List<NPCWalker> inside = Inside();
        bool overload = radius >= maxRadius;
        bool match = !overload && inside.Count == 1 && inside[0] == target;

        foreach (var pair in devices) if (pair.Key != null) pair.Value.color = inside.Contains(pair.Key) ? Red : new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
        ring.color = match ? Color.white : Red;

        string label = overload ? "SCAN OVERLOAD" : inside.Count == 0 ? "NO SIGNAL" : match ? "DEVICE MATCH" : inside.Count > 1 ? "MULTIPLE DEVICES" : "WRONG DEVICE";
        Banner(label, match ? Cyan : Red);
        SoundFX.Play(match ? Sfx.ScanLock : Sfx.Fail, match ? 1f : 0.6f);
        yield return new WaitForSeconds(0.55f);

        if (overload || inside.Count == 0)
        {
            OnTrapMissed();
            yield break;
        }

        // The drone fires at every device in the ring (the system doesn't ask twice)
        NPCWalker blame = match ? target : inside.Find(n => !n.isTarget); // a wrong scan is on whoever else was in it
        Panic(origin.localPosition.x, null);
        foreach (NPCWalker npc in inside) npc.CanMove = false;
        SoundFX.Play(Sfx.DroneShot);
        foreach (NPCWalker npc in inside)
        {
            Vector2 p = (Vector2)npc.BodyRect.center - (Vector2)transform.position;
            StartCoroutine(Flash(p));
        }
        yield return new WaitForSeconds(0.12f);
        SoundFX.Play(Sfx.DroneHit);
        foreach (NPCWalker npc in inside) if (npc != blame) npc.Kill();
        OnTrapHitNPC(blame);
    }

    private IEnumerator Flash(Vector2 p)
    {
        SpriteRenderer flash = SpriteAt(transform, "Shot Flash", GameSprites.SoftCircle(), p.x, p.y, 0.3f, new Color(1f, 1f, 0.8f, 0.9f), 40);
        for (float t = 0f; t < 0.12f; t += Time.deltaTime)
        {
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 0.6f, t / 0.12f);
            flash.color = new Color(1f, 1f, 0.8f, 0.9f * (1f - t / 0.12f));
            yield return null;
        }
        Destroy(flash.gameObject);
    }

    private void Banner(string text, Color color)
    {
        TextMeshPro t = WorldText(transform, text, visible.center.x, visible.yMax - 2.4f, visible.width * 0.8f, 0.42f, color, 36); // below the score
        t.outlineColor = Color.black;
        t.outlineWidth = 0.3f;
    }

    // After the scan nobody walks off and comes back in any more
    protected override void HandleScreenEdges()
    {
        if (!released) base.HandleScreenEdges();
    }

    void OnDestroy()
    {
        if (autopilotHolding) Autopilot.Hold(false);
    }

    // ---------------- Autopilot ----------------

    // Starts holding when the target's device is the closest one to the scan origin and releases
    // once the ring has just passed it. When it decided to miss, it lets go too early or too late.
    private void AutopilotTick()
    {
        if (timeLimit - TimeRemaining < autopilotReactTime + 0.5f || target == null) return;

        float dTarget = Vector2.Distance(DevicePoint(target), origin.localPosition);
        float dOther = float.MaxValue;
        foreach (NPCWalker npc in devices.Keys)
            if (npc != null && npc != target && !npc.IsDead && !npc.IsGone)
                dOther = Mathf.Min(dOther, Vector2.Distance(DevicePoint(npc), origin.localPosition));

        if (!holding)
        {
            // Wait for a clear window (the time it takes the ring to get there, others won't come much closer)
            if (dOther - dTarget > 0.35f || autopilotMisses)
            {
                autopilotHolding = true;
                autopilotRelease = !autopilotMisses ? -1f : Random.value < 0.5f ? dTarget * 0.5f : Mathf.Min(dOther + 0.1f, maxRadius);
                Autopilot.Hold(true);
            }
            return;
        }

        bool letGo = autopilotRelease > 0f ? radius >= autopilotRelease : radius >= dTarget + 0.08f;
        if (letGo) Autopilot.Hold(false);
    }
}
