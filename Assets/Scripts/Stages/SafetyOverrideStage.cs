using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static StageBuilder;

// Eleventh minigame: the drone has already locked onto the target, but a safety lock blocks the
// shot ("ETHICS LOCK - FIRING DISABLED"). Tap again and again to force it before the target reaches
// the metro entrance: every tap fills the OVERRIDE bar (it drains when you stop), every quarter
// breaks a padlock, and at 100% the drone fires by itself. Target in the metro = TARGET LOST.
// From reviewSkippedFromScore points on the lock reads "LEGAL REVIEW: SKIPPED" and gives in easier.
// The set lives in its prefab; the crowd and the camera HUD come from DroneCamStage.
public class SafetyOverrideStage : DroneCamStage
{
    [Header("Safety override")]
    [Tooltip("Taps to break the lock, at 0 points and at the target score")]
    [SerializeField] private Vector2Int tapsNeeded = new Vector2Int(16, 22);
    [Tooltip("Taps once the legal review is skipped")]
    [SerializeField] private int tapsWhenSkipped = 10;
    [Tooltip("From this score on the lock barely resists")]
    [SerializeField] private int reviewSkippedFromScore = 600;
    [Tooltip("How much of the bar drains per second when you stop tapping")]
    [SerializeField] private float drainPerSecond = 0.15f;
    [Tooltip("Seconds the target takes to reach the metro, at 0 points and at the target score")]
    [SerializeField] private Vector2 secondsToMetro = new Vector2(7.5f, 5.5f);

    private const int Locks = 4;
    private static readonly Color Red = new Color(1f, 0.25f, 0.25f);
    private static readonly Color Amber = new Color(1f, 0.75f, 0.15f);

    private NPCWalker target;
    private float progress, perTap, exitX;
    private bool skipped, done;
    private Transform reticle, reticleRing, fill;
    private float clock;
    private float barLeft, barWidth;
    private readonly List<Transform> shackles = new List<Transform>();
    private readonly List<SpriteRenderer> lockParts = new List<SpriteRenderer>();
    private int brokenLocks;
    private float nextBotTap;
    private bool botFast;

    public override WalkTheme Theme => WalkTheme.Crosswalk;
    protected override Color HudColor => new Color(1f, 1f, 1f, 0.9f);
    protected override string CamLabel => "STRIKE // " + AssignedDrone.Tag;
    protected override bool HasCrosshair => false;

    public override StageTarget PickTarget()
    {
        StageTarget t = base.PickTarget();
        t.header = "OVERRIDE";
        t.hint = "TAP AGAIN AND AGAIN";
        return t;
    }

    protected override void BuildOverlay()
    {
        float k = GameManager.Instance != null ? Mathf.Clamp01((float)GameManager.Instance.Score / GameManager.Instance.targetScore) : 0f;
        skipped = GameManager.Instance != null && GameManager.Instance.Score >= reviewSkippedFromScore;
        perTap = 1f / (skipped ? tapsWhenSkipped : Mathf.RoundToInt(Mathf.Lerp(tapsNeeded.x, tapsNeeded.y, k)));
        target = spawnedNPCs.Find(n => n != null && n.isTarget);

        // The target heads for the metro entrance on the far side (so there is time to break the lock)
        float x = target.BodyCenter.x - transform.position.x;
        bool goRight = visible.xMax - x > x - visible.xMin;
        exitX = goRight ? visible.xMax - 0.45f : visible.xMin + 0.45f;
        target.SetWalkDirection(goRight ? Vector3.right : Vector3.left);
        target.moveSpeed = Mathf.Abs(exitX - x) / Mathf.Lerp(secondsToMetro.x, secondsToMetro.y, k); // always within the time limit
        target.randomStops = false;

        BuildMetro(exitX, target.BodyRect.yMin - transform.position.y);
        BuildLockPanel();
        reticle = new GameObject("Lock-on").transform;
        reticle.SetParent(transform, false);
        reticleRing = SpriteAt(reticle, "Crosshair", GameSprites.Crosshair(), 0f, 0f, 0.5f, Red, 35).transform;
        TextMeshPro locked = WorldText(reticle, "LOCKED", 0f, -0.42f, 0.8f, 0.14f, Red, 35);
        locked.outlineColor = Color.black;
        locked.outlineWidth = 0.25f;
        botFast = Random.value >= Autopilot.MissChance;
    }

    protected override void OnStageUpdate()
    {
        AnimateHud();
        if (done || target == null) return;
        reticle.localPosition = (Vector2)target.BodyRect.center - (Vector2)transform.position;
        reticleRing.localRotation = Quaternion.Euler(0f, 0f, Time.time * 40f);
        clock += Time.deltaTime;

        // Made it to the metro
        float tx = target.BodyCenter.x - transform.position.x;
        if ((target.WalkDirection.x > 0f && tx >= exitX) || (target.WalkDirection.x < 0f && tx <= exitX))
        {
            StartCoroutine(Lost());
            return;
        }

        if (Autopilot.Enabled && clock >= nextBotTap + 0.6f)
        {
            Autopilot.Press();
            nextBotTap = clock - 0.6f + (botFast ? 0.13f : 0.4f);
        }

        if (SingleInput.ActionPressed)
        {
            progress += perTap;
            SoundFX.Play(Sfx.FingerTap);
            fill.parent.localScale = Vector3.one * 1.04f;
        }
        progress = Mathf.Max(0f, progress - drainPerSecond * Time.deltaTime);
        fill.localScale = new Vector3(barWidth * Mathf.Clamp01(progress), fill.localScale.y, 1f);
        fill.localPosition = new Vector3(barLeft + barWidth * Mathf.Clamp01(progress) / 2f, fill.localPosition.y, 0f);
        fill.parent.localScale = Vector3.Lerp(fill.parent.localScale, Vector3.one, 12f * Time.deltaTime);

        // Every quarter breaks a padlock (they never lock again, even if the bar drains)
        while (brokenLocks < Locks && progress >= (brokenLocks + 1f) / Locks)
        {
            Transform s = shackles[brokenLocks];
            s.localPosition += new Vector3(0.04f, 0.1f, 0f);
            s.localRotation = Quaternion.Euler(0f, 0f, -30f);
            brokenLocks++;
            SoundFX.Play(Sfx.SewerOpen, 0.5f);
        }

        if (progress >= 1f) StartCoroutine(Fire());
    }

    // ---------------- Result ----------------

    private IEnumerator Fire()
    {
        done = true;
        foreach (SpriteRenderer part in lockParts) part.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        Banner(skipped ? "REVIEW SKIPPED" : "OVERRIDE COMPLETE", Amber);
        SoundFX.Play(Sfx.ScanLock);
        yield return new WaitForSeconds(0.3f);

        Vector2 p = (Vector2)target.BodyRect.center - (Vector2)transform.position;
        SoundFX.Play(Sfx.DroneShot);
        SpriteRenderer flash = SpriteAt(transform, "Shot Flash", GameSprites.SoftCircle(), p.x, p.y, 0.3f, new Color(1f, 1f, 0.8f, 0.9f), 40);
        for (float t = 0f; t < 0.12f; t += Time.deltaTime)
        {
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 0.6f, t / 0.12f);
            flash.color = new Color(1f, 1f, 0.8f, 0.9f * (1f - t / 0.12f));
            yield return null;
        }
        Destroy(flash.gameObject);
        reticle.gameObject.SetActive(false);
        Panic(p.x, target);
        SoundFX.Play(Sfx.DroneHit);
        OnTrapHitNPC(target);
    }

    private IEnumerator Lost()
    {
        done = true;
        reticle.gameObject.SetActive(false);
        target.PopOut(true); // down the metro stairs
        Banner("TARGET LOST", Red);
        SoundFX.Play(Sfx.Fail, 0.6f);
        yield return new WaitForSeconds(0.6f);
        OnTrapMissed();
    }

    // ---------------- Set pieces made here ----------------

    // A metro entrance at the side the target walks to: dark stairs and a lit sign
    private void BuildMetro(float x, float feetY)
    {
        Transform metro = new GameObject("Metro Entrance").transform;
        metro.SetParent(transform, false);
        metro.localPosition = new Vector3(x, feetY, 0f);
        Box(metro, "Stairs", 0f, 0.32f, 0.7f, 0.64f, new Color(0.05f, 0.05f, 0.08f), 2);
        Box(metro, "Rail L", -0.37f, 0.4f, 0.05f, 0.8f, new Color(0.7f, 0.72f, 0.75f), 2);
        Box(metro, "Rail R", 0.37f, 0.4f, 0.05f, 0.8f, new Color(0.7f, 0.72f, 0.75f), 2);
        Box(metro, "Sign", 0f, 1.0f, 0.75f, 0.3f, new Color(0.1f, 0.45f, 0.85f), 2);
        TextMeshPro sign = WorldText(metro, "METRO", 0f, 1.0f, 0.65f, 0.22f, Color.white, 2);
        sign.fontStyle = FontStyles.Bold;
    }

    // "ETHICS LOCK - FIRING DISABLED", four padlocks and the OVERRIDE bar, under the score
    private void BuildLockPanel()
    {
        Transform panel = new GameObject("Lock Panel").transform;
        panel.SetParent(transform, false);
        float cy = visible.yMax - 2.55f, w = visible.width - 0.8f;
        panel.localPosition = new Vector3(visible.center.x, cy, 0f);

        Box(panel, "Back", 0f, 0f, w, 1.35f, new Color(0f, 0f, 0f, 0.6f), 30);
        TextMeshPro title = WorldText(panel, skipped ? "LEGAL REVIEW: SKIPPED" : "ETHICS LOCK - FIRING DISABLED", 0f, 0.47f, w - 0.3f, 0.24f, skipped ? Amber : Red, 31);
        title.outlineColor = Color.black;
        title.outlineWidth = 0.25f;

        for (int i = 0; i < Locks; i++) Padlock(panel, (i - (Locks - 1) / 2f) * 0.55f, 0.02f);

        barWidth = w - 0.6f;
        barLeft = -barWidth / 2f;
        Box(panel, "Bar Back", 0f, -0.45f, barWidth + 0.06f, 0.2f, new Color(1f, 1f, 1f, 0.25f), 31);
        Transform bar = new GameObject("Bar").transform; // pulses on every tap
        bar.SetParent(panel, false);
        fill = Box(bar, "Fill", barLeft, -0.45f, 0.001f, 0.14f, Amber, 32).transform;
        TextMeshPro label = WorldText(panel, "OVERRIDE", 0f, -0.45f, 1.2f, 0.13f, Color.black, 33);
        label.fontStyle = FontStyles.Bold;
    }

    private void Padlock(Transform parent, float x, float y)
    {
        Color metal = new Color(0.85f, 0.8f, 0.55f), body = new Color(0.95f, 0.75f, 0.2f);
        lockParts.Add(Box(parent, "Lock Body", x, y - 0.06f, 0.3f, 0.24f, body, 31));
        lockParts.Add(Box(parent, "Keyhole", x, y - 0.07f, 0.05f, 0.09f, new Color(0.2f, 0.15f, 0.05f), 32));
        Transform shackle = new GameObject("Shackle").transform;
        shackle.SetParent(parent, false);
        shackle.localPosition = new Vector3(x, y + 0.06f, 0f);
        lockParts.Add(Box(shackle, "Left", -0.1f, 0.08f, 0.05f, 0.16f, metal, 31));
        lockParts.Add(Box(shackle, "Right", 0.1f, 0.08f, 0.05f, 0.16f, metal, 31));
        lockParts.Add(Box(shackle, "Top", 0f, 0.17f, 0.25f, 0.05f, metal, 31));
        shackles.Add(shackle);
    }

    private void Banner(string text, Color color)
    {
        TextMeshPro t = WorldText(transform, text, visible.center.x, visible.yMax - 3.65f, visible.width * 0.8f, 0.42f, color, 36);
        t.outlineColor = Color.black;
        t.outlineWidth = 0.3f;
    }
}
