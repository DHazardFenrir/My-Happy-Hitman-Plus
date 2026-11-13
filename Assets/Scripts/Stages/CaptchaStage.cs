using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using static StageBuilder;

// Ninth minigame: data labeling. The drone can't recognize the target on its own, so you label a
// camera shot for it, like the "Select all squares with..." test: the street is cut into a 3x3 grid,
// a cursor visits every square once (snake order) and you press on each square the target is in.
// After the last square it verifies by itself: every square with a good part of the target marked
// and none without it = VERIFIED, and the drone uses your labels to shoot (the picture was real).
// Marking someone else's square gets THAT person shot; missing a square is "Please try again".
// Only squares holding a big part of the target (requiredShare, the whole time the cursor is there) are
// required; where it barely shows (the head, a foot) is optional: marking them is fine. The target is
// always drawn in front of everyone, and a fail frames the squares that were MISSED or had NO TARGET.
// From liveFromScore points on, the picture is live video: people walk while the cursor moves.
// The street picture lives in the prefab; the grid, the widget and the people are made here.
public class CaptchaStage : WarioStage
{
    [Header("Captcha")]
    [Tooltip("Seconds the cursor stays on each square at 0 points and at the target score (it speeds up)")]
    [SerializeField] private Vector2 dwellSeconds = new Vector2(0.85f, 0.55f);
    [Tooltip("From this score on, the picture is live video and people walk")]
    [SerializeField] private int liveFromScore = 600;
    [Tooltip("People in the picture besides the target, at 0 points and at the target score")]
    [SerializeField] private Vector2Int decoys = new Vector2Int(3, 5);
    [Tooltip("A square is required when it holds at least this part of the target's body")]
    [SerializeField, Range(0.05f, 0.5f)] private float requiredShare = 0.35f;
    [Tooltip("How tall people look, in squares")]
    [SerializeField] private float personHeightInSquares = 1.45f;
    [Tooltip("Top of the sidewalk in the street picture: people's feet always go below it (move it if you repaint the picture)")]
    [SerializeField] private Transform groundLine;

    private const int Size = 3;
    private const float StartDelay = 0.8f; // the "I AM A ROBOT" box ticks itself first

    private static readonly Color Blue = new Color(0.27f, 0.53f, 0.95f);
    private static readonly Color Ink = new Color(0.25f, 0.27f, 0.3f);
    private static readonly Color CursorColor = new Color(1f, 0.8f, 0.1f);

    private struct Cell
    {
        public Rect rect;          // local
        public bool marked;
        public float minShare, maxShare; // of the target's body inside it, while the cursor was on it
        public GameObject markVisual;
    }

    private readonly Cell[] cells = new Cell[Size * Size];
    private readonly int[] order = new int[Size * Size]; // snake: top row left to right, then back...
    private Rect visible, grid;
    private float dwell, clock;
    private int step = -1;
    private bool live, verifying;
    private NPCWalker target;
    private Transform cursor;
    private SpriteRenderer verifyButton, liveDot;
    private GameObject robotBox, robotCheck;
    private TextMeshPro robotLabel;

    // Autopilot: the square it gets wrong on purpose (-1 = none)
    private int autopilotMistake = -2;

    public override WalkTheme Theme => WalkTheme.Street;
    public override string ActionVerb => "LABEL!";
    public override IrlDeathKind IrlDeath => IrlDeathKind.ShotByDrone;
    public override Sprite IrlThreatSprite => GameSprites.Drone(0);

    public override StageTarget PickTarget()
    {
        StageTarget t = base.PickTarget();
        t.header = "LABEL";
        t.hint = "PRESS ON EVERY SQUARE WITH THE TARGET";
        return t;
    }

    public override void SetupStage(NPCAppearance.ClothingPart targetClothing)
    {
        float progress = GameManager.Instance != null ? Mathf.Clamp01((float)GameManager.Instance.Score / GameManager.Instance.targetScore) : 0f;
        live = GameManager.Instance != null && GameManager.Instance.Score >= liveFromScore;
        dwell = Mathf.Lerp(dwellSeconds.x, dwellSeconds.y, progress);
        extraNPCsCount = Mathf.RoundToInt(Mathf.Lerp(decoys.x, decoys.y, progress));
        timeLimit = StartDelay + dwell * cells.Length + 5f; // the cursor ends the round, not the clock

        base.SetupStage(targetClothing); // focuses the camera and spawns the people (rearranged below)

        Vector2 origin = transform.position;
        visible = RenderCameraFollow.Instance != null ? RenderCameraFollow.Instance.GetPhoneVisibleWorldRect(transform.position.z) : view;
        visible.position -= origin;
        Rect fullView = new Rect(view.position - origin, view.size);

        BuildWidget(fullView);
        PlacePeople();
    }

    public override void StartStage()
    {
        base.StartStage();
        // A still picture until the score makes it live video
        foreach (NPCWalker npc in spawnedNPCs) if (npc != null) SetFrozen(npc, !live);
        StartCoroutine(TickRobotBox());
    }

    // ---------------- Layout ----------------

    private void BuildWidget(Rect fullView)
    {
        const float margin = 0.18f, pad = 0.12f, headerH = 1.45f, footerH = 0.85f;
        float left = visible.xMin + margin, right = visible.xMax - margin;
        float top = visible.yMax - 1.3f; // room for the score at the top of the phone
        float side = right - left - 2f * pad;
        float cellSize = side / Size;
        grid = new Rect(left + pad, top - headerH - pad - side, side, side);
        float bottom = grid.yMin - pad - footerH - pad;
        Rect panel = Rect.MinMaxRect(left, bottom, right, top);

        Transform ui = new GameObject("Captcha Widget").transform;
        ui.SetParent(transform, false);

        // The page (gray) and the panel (white) around the hole where the picture shows
        Color page = new Color(0.86f, 0.87f, 0.89f), white = new Color(0.98f, 0.98f, 0.98f);
        Frame(ui, "Page", Rect.MinMaxRect(fullView.xMin - 1f, fullView.yMin - 1f, fullView.xMax + 1f, fullView.yMax + 1f), grid, page, 20);
        Frame(ui, "Panel", panel, grid, white, 21);
        Outline(ui, "Panel Border", panel, 0.03f, new Color(0.75f, 0.76f, 0.78f), 21);

        // Header: what to select and who learns from it
        Rect header = Rect.MinMaxRect(grid.xMin, top - headerH, grid.xMax, top - pad);
        Box(ui, "Header", header.center.x, header.center.y, header.width, header.height, Blue, 22);
        Text(ui, "Select all squares with", header.center.x, header.yMax - 0.27f, header.width - 0.4f, 0.22f, Color.white, TextAlignmentOptions.Left, FontStyles.Normal);
        Text(ui, $"{GameManager.ShirtName(targetClothing)} + {GameManager.PantsName(targetClothing)}", header.center.x, header.yMax - 0.66f, header.width - 0.4f, 0.4f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        Text(ui, $"Your labels train {AssignedDrone.Tag}", header.center.x, header.yMin + 0.2f, header.width - 0.4f, 0.17f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Left, FontStyles.Normal);
        if (live)
        {
            liveDot = Box(ui, "Live Dot", header.xMax - 0.78f, header.yMax - 0.27f, 0.12f, 0.12f, new Color(1f, 0.2f, 0.2f), 23);
            Text(ui, "LIVE", header.xMax - 0.42f, header.yMax - 0.27f, 0.5f, 0.2f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        }

        // Grid lines and the squares (snake order)
        for (int i = 1; i < Size; i++)
        {
            Box(ui, "Grid V", grid.xMin + i * cellSize, grid.center.y, 0.06f, grid.height, white, 22);
            Box(ui, "Grid H", grid.center.x, grid.yMin + i * cellSize, grid.width, 0.06f, white, 22);
        }
        for (int row = 0; row < Size; row++)
        {
            for (int col = 0; col < Size; col++)
            {
                int index = row * Size + col;
                Rect r = new Rect(grid.xMin + col * cellSize, grid.yMax - (row + 1) * cellSize, cellSize, cellSize);
                cells[index] = new Cell { rect = r, markVisual = MarkVisual(ui, r) };
                order[row * Size + (row % 2 == 0 ? col : Size - 1 - col)] = index;
            }
        }
        cursor = new GameObject("Cursor").transform;
        cursor.SetParent(ui, false);
        Outline(cursor, "Cursor Frame", new Rect(-cellSize / 2f, -cellSize / 2f, cellSize, cellSize), 0.08f, CursorColor, 24);
        cursor.gameObject.SetActive(false);

        // Footer: the robot checkbox and the VERIFY button
        float fy = grid.yMin - pad - footerH / 2f;
        float boxX = grid.xMin + 0.25f;
        robotBox = new GameObject("Robot Box");
        robotBox.transform.SetParent(ui, false);
        Box(robotBox.transform, "Border", boxX, fy, 0.36f, 0.36f, new Color(0.62f, 0.63f, 0.66f), 22);
        Box(robotBox.transform, "Inside", boxX, fy, 0.3f, 0.3f, Color.white, 23);
        robotCheck = CheckMark(robotBox.transform, boxX, fy, 0.42f, new Color(0.1f, 0.65f, 0.25f), 24).transform.parent.gameObject;
        robotCheck.SetActive(false);
        robotLabel = Text(ui, "I AM A ROBOT", grid.xMin + 1.45f, fy, 1.8f, 0.22f, Ink, TextAlignmentOptions.Left, FontStyles.Normal);
        verifyButton = Box(ui, "Verify Button", grid.xMax - 0.75f, fy, 1.5f, 0.52f, Blue, 22);
        Text(ui, "VERIFY", grid.xMax - 0.75f, fy, 1.3f, 0.26f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
    }

    // Spread the people over the picture (one per column slot so nobody hides the target),
    // close-up size, standing on the sidewalk (below groundLine), the lower ones in front
    private void PlacePeople()
    {
        List<NPCWalker> people = spawnedNPCs.FindAll(n => n != null);
        target = people.Find(n => n.isTarget);
        for (int i = people.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (people[i], people[j]) = (people[j], people[i]);
        }

        float cellSize = grid.width / Size;
        float slot = grid.width / people.Count;
        foreach (NPCWalker npc in people)
        {
            npc.ScaleBy(personHeightInSquares * cellSize / npc.BodyRect.height);
            npc.personalSpace *= 3f;
            npc.SetWalkDirection(Random.value < 0.5f ? Vector3.left : Vector3.right);
            npc.moveSpeed = npc.isTarget ? Random.Range(0.4f, 0.6f) : Random.Range(0.5f, 0.9f);
        }

        for (int i = 0; i < people.Count; i++)
        {
            NPCWalker npc = people[i];
            Rect body = Local(npc.BodyRect);
            float x = grid.xMin + (i + 0.5f) * slot + Random.Range(-0.15f, 0.15f) * slot;
            x = Mathf.Clamp(x, grid.xMin + body.width / 2f + 0.05f, grid.xMax - body.width / 2f - 0.05f);
            // Feet on the sidewalk (never over the buildings), and the whole body inside the picture
            float ground = groundLine != null ? groundLine.localPosition.y : grid.yMin + grid.height / 3f;
            float highest = Mathf.Max(grid.yMin + 0.15f, Mathf.Min(ground, grid.yMax - body.height - 0.1f));
            float feet = Random.Range(grid.yMin + 0.15f, highest);
            npc.transform.position += new Vector3(x - body.center.x, feet - body.yMin, 0f);
        }

        people.Sort((a, b) => b.BodyRect.yMin.CompareTo(a.BodyRect.yMin)); // highest feet first (behind)
        people.Remove(target);
        people.Add(target); // ...except the target, always in front: nobody walking by can hide it
        for (int i = 0; i < people.Count; i++)
            people[i].gameObject.AddComponent<SortingGroup>().sortingOrder = 3 + i;
    }

    // The top of the sidewalk, so it can be moved by hand in the prefab
    void OnDrawGizmos()
    {
        if (groundLine == null) return;
        Gizmos.color = Color.green;
        Vector3 p = groundLine.position;
        Gizmos.DrawLine(p + Vector3.left * 3f, p + Vector3.right * 3f);
    }

    // ---------------- Playing ----------------

    protected override void OnStageUpdate()
    {
        if (liveDot != null) liveDot.enabled = Mathf.Repeat(Time.time, 1f) < 0.6f;
        if (verifying) return;

        clock += Time.deltaTime;
        if (clock < StartDelay) return;

        int now = Mathf.FloorToInt((clock - StartDelay) / dwell);
        if (now != step)
        {
            if (now >= cells.Length) { StartCoroutine(Verify()); return; }
            EnterCell(now);
        }

        int index = order[step];
        float share = TargetShare(cells[index].rect);
        cells[index].minShare = Mathf.Min(cells[index].minShare, share);
        cells[index].maxShare = Mathf.Max(cells[index].maxShare, share);
        cursor.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(Time.time * 18f));

        if (Autopilot.Enabled) AutopilotTick(index, share);
        if (SingleInput.ActionPressed)
        {
            cells[index].marked = !cells[index].marked;
            cells[index].markVisual.SetActive(cells[index].marked);
            SoundFX.Play(Sfx.MenuSelect, 0.6f);
        }
    }

    private void EnterCell(int newStep)
    {
        step = newStep;
        int index = order[step];
        cursor.gameObject.SetActive(true);
        cursor.localPosition = cells[index].rect.center;
        float share = TargetShare(cells[index].rect);
        cells[index].minShare = cells[index].maxShare = share;
    }

    // Part of the target's body inside a square (0..1)
    private float TargetShare(Rect cell)
    {
        if (target == null || target.IsDead) return 0f;
        Rect body = Local(target.BodyRect);
        return OverlapArea(body, cell) / (body.width * body.height);
    }

    // In the live video everybody stays inside the picture, turning around at its edges
    protected override void HandleScreenEdges()
    {
        if (!live || verifying) return;
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc == null || npc.IsDead) continue;
            Rect body = Local(npc.BodyRect);
            if (body.xMin < grid.xMin + 0.05f && npc.WalkDirection.x < 0f) npc.SetWalkDirection(Vector3.right);
            else if (body.xMax > grid.xMax - 0.05f && npc.WalkDirection.x > 0f) npc.SetWalkDirection(Vector3.left);
        }
    }

    // ---------------- Verifying ----------------

    private IEnumerator Verify()
    {
        verifying = true;
        cursor.gameObject.SetActive(false);
        verifyButton.color = Blue * 0.75f;
        SoundFX.Play(Sfx.MenuSelect);
        yield return new WaitForSeconds(0.35f);

        bool anyMarked = false, wrong = false, missed = false;
        foreach (Cell c in cells)
        {
            anyMarked |= c.marked;
            if (c.marked && c.maxShare <= 0.001f) wrong = true;
            if (!c.marked && c.minShare >= requiredShare) missed = true;
        }

        if (anyMarked && !wrong && !missed)
        {
            Stamp("VERIFIED", new Color(0.1f, 0.7f, 0.3f));
            yield return new WaitForSeconds(0.5f);
            yield return Shoot(target);
            yield break;
        }

        // Shows what went wrong, so a fail is never a mystery
        ShowMistakes();
        yield return new WaitForSeconds(0.6f);

        // A wrong label points the drone at whoever is in that square
        NPCWalker wronged = wrong ? MostInsideWrongSquares() : null;
        if (wronged != null)
        {
            Stamp("VERIFIED", new Color(0.1f, 0.7f, 0.3f)); // the system trusts you anyway
            yield return new WaitForSeconds(0.5f);
            yield return Shoot(wronged);
            yield break;
        }

        robotBox.SetActive(false);
        robotLabel.text = "Please try again.";
        robotLabel.color = new Color(0.85f, 0.15f, 0.15f);
        robotLabel.rectTransform.localPosition -= new Vector3(0.4f, 0f, 0f); // where the checkbox was
        yield return new WaitForSeconds(0.7f);
        OnTrapMissed();
    }

    // Red frame + "MISSED" on squares that needed a mark, red frame + "NO TARGET" on marks with no target
    private void ShowMistakes()
    {
        Color red = new Color(0.95f, 0.15f, 0.15f);
        foreach (Cell c in cells)
        {
            bool wrongMark = c.marked && c.maxShare <= 0.001f, missedMark = !c.marked && c.minShare >= requiredShare;
            if (!wrongMark && !missedMark) continue;
            Rect r = new Rect(c.rect.x + 0.06f, c.rect.y + 0.06f, c.rect.width - 0.12f, c.rect.height - 0.12f);
            Outline(transform, "Mistake", r, 0.1f, red, 30);
            Box(transform, "Mistake Tag", r.center.x, r.yMin + 0.2f, r.width - 0.1f, 0.3f, red, 30);
            TextMeshPro t = WorldText(transform, missedMark ? "MISSED" : "NO TARGET", r.center.x, r.yMin + 0.2f, r.width - 0.2f, 0.2f, Color.white, 31);
            t.fontStyle = FontStyles.Bold;
        }
    }

    // The person (not the target) with the most body inside the squares wrongly marked
    private NPCWalker MostInsideWrongSquares()
    {
        NPCWalker best = null;
        float bestArea = 0f;
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc == null || npc.IsDead || npc.isTarget) continue;
            Rect body = Local(npc.BodyRect);
            float area = 0f;
            foreach (Cell c in cells) if (c.marked && c.maxShare <= 0.001f) area += OverlapArea(body, c.rect);
            if (area > bestArea) { best = npc; bestArea = area; }
        }
        return best;
    }

    // The picture was real: it comes alive, the drone's crosshair drops on the person and fires
    private IEnumerator Shoot(NPCWalker victim)
    {
        foreach (NPCWalker npc in spawnedNPCs) if (npc != null) SetFrozen(npc, false);

        Vector2 from = grid.center, to = Local(victim.BodyRect).center;
        SpriteRenderer crosshair = SpriteAt(transform, "Crosshair", GameSprites.Crosshair(), from.x, from.y, 1.6f, new Color(1f, 0.2f, 0.2f), 35);
        SoundFX.Play(Sfx.TargetAlert, 0.7f);
        for (float t = 0f; t < 0.55f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.55f);
            to = Local(victim.BodyRect).center;
            crosshair.transform.localPosition = Vector2.Lerp(from, to, k);
            crosshair.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 0.6f, k);
            yield return null;
        }

        SoundFX.Play(Sfx.DroneShot);
        SpriteRenderer flash = SpriteAt(transform, "Shot Flash", GameSprites.SoftCircle(), to.x, to.y, 0.6f, new Color(1f, 1f, 0.8f, 0.9f), 34);
        for (float t = 0f; t < 0.12f; t += Time.deltaTime)
        {
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.2f, t / 0.12f);
            flash.color = new Color(1f, 1f, 0.8f, 0.9f * (1f - t / 0.12f));
            yield return null;
        }
        Destroy(flash.gameObject);

        // Everybody else runs away from the shot
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc == null || npc == victim || npc.IsDead) continue;
            npc.CanMove = true;
            npc.randomStops = false;
            npc.moveSpeed = Random.Range(4f, 6f);
            npc.SetWalkDirection(npc.BodyCenter.x < victim.BodyCenter.x ? Vector3.left : Vector3.right);
        }
        SoundFX.Play(Sfx.DroneHit);
        OnTrapHitNPC(victim);
    }

    // ---------------- Autopilot ----------------

    // Marks every square with a fair part of the target (optional ones too: that's allowed); when it decided to miss, it gets one square
    // wrong on purpose (marks a square without the target or skips one with it)
    private void AutopilotTick(int index, float share)
    {
        if (autopilotMistake == -2)
        {
            autopilotMistake = -1;
            if (autopilotMisses)
            {
                List<int> options = new List<int>();
                bool skip = Random.value < 0.5f;
                for (int i = 0; i < cells.Length; i++)
                {
                    float s = TargetShare(cells[i].rect);
                    if (skip ? s >= 0.35f : s <= 0f) options.Add(i);
                }
                if (options.Count > 0) autopilotMistake = options[Random.Range(0, options.Count)];
            }
        }

        float inCell = (clock - StartDelay) - step * dwell;
        if (inCell < dwell * 0.35f || inCell - Time.deltaTime >= dwell * 0.35f) return; // one decision per square
        bool want = share >= requiredShare * 0.5f;
        if (index == autopilotMistake) want = !want;
        if (want != cells[index].marked) Autopilot.Press();
    }

    // ---------------- Helpers ----------------

    private IEnumerator TickRobotBox()
    {
        yield return new WaitForSeconds(0.35f);
        robotCheck.SetActive(true);
        SoundFX.Play(Sfx.MenuSelect, 0.5f);
    }

    private static void SetFrozen(NPCWalker npc, bool frozen)
    {
        npc.CanMove = !frozen;
        foreach (Animator a in npc.GetComponentsInChildren<Animator>(true)) a.speed = frozen ? 0f : 1f;
    }

    private Rect Local(Rect world)
    {
        world.position -= (Vector2)transform.position;
        return world;
    }

    private static float OverlapArea(Rect a, Rect b)
    {
        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return w > 0f && h > 0f ? w * h : 0f;
    }

    private void Stamp(string text, Color color)
    {
        TextMeshPro t = Text(transform, text, grid.center.x, grid.center.y, grid.width * 0.85f, 0.9f, color, TextAlignmentOptions.Center, FontStyles.Bold);
        t.sortingOrder = 30;
        t.outlineColor = Color.white;
        t.outlineWidth = 0.3f;
        t.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
    }

    // A blue tint over a marked square and a check badge in its corner
    private static GameObject MarkVisual(Transform parent, Rect r)
    {
        Transform mark = new GameObject("Mark").transform;
        mark.SetParent(parent, false);
        Box(mark, "Tint", r.center.x, r.center.y, r.width, r.height, new Color(Blue.r, Blue.g, Blue.b, 0.38f), 19);
        Vector2 badge = new Vector2(r.xMin + 0.28f, r.yMax - 0.28f);
        Box(mark, "Badge", badge.x, badge.y, 0.38f, 0.38f, Blue, 23);
        CheckMark(mark, badge.x, badge.y, 0.36f, Color.white, 24);
        mark.gameObject.SetActive(false);
        return mark.gameObject;
    }

    // A check mark made of two bars
    private static SpriteRenderer CheckMark(Transform parent, float x, float y, float size, Color color, int order)
    {
        Transform check = new GameObject("Check").transform;
        check.SetParent(parent, false);
        check.localPosition = new Vector3(x, y, 0f);
        Bar(check, new Vector2(-0.32f, 0.02f) * size, new Vector2(-0.1f, -0.22f) * size, 0.13f * size, color, order);
        return Bar(check, new Vector2(-0.12f, -0.22f) * size, new Vector2(0.34f, 0.26f) * size, 0.13f * size, color, order);
    }

    private static SpriteRenderer Bar(Transform parent, Vector2 a, Vector2 b, float thickness, Color color, int order)
    {
        Vector2 d = b - a;
        SpriteRenderer bar = Box(parent, "Bar", (a.x + b.x) / 2f, (a.y + b.y) / 2f, d.magnitude + thickness * 0.5f, thickness, color, order);
        bar.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        return bar;
    }

    // Four boxes filling `outer` around the `hole`
    private static void Frame(Transform parent, string name, Rect outer, Rect hole, Color color, int order)
    {
        Box(parent, name + " Top", outer.center.x, (hole.yMax + outer.yMax) / 2f, outer.width, outer.yMax - hole.yMax, color, order);
        Box(parent, name + " Bottom", outer.center.x, (outer.yMin + hole.yMin) / 2f, outer.width, hole.yMin - outer.yMin, color, order);
        Box(parent, name + " Left", (outer.xMin + hole.xMin) / 2f, hole.center.y, hole.xMin - outer.xMin, hole.height, color, order);
        Box(parent, name + " Right", (hole.xMax + outer.xMax) / 2f, hole.center.y, outer.xMax - hole.xMax, hole.height, color, order);
    }

    private static void Outline(Transform parent, string name, Rect r, float thickness, Color color, int order)
    {
        Box(parent, name, r.center.x, r.yMax, r.width + thickness, thickness, color, order);
        Box(parent, name, r.center.x, r.yMin, r.width + thickness, thickness, color, order);
        Box(parent, name, r.xMin, r.center.y, thickness, r.height + thickness, color, order);
        Box(parent, name, r.xMax, r.center.y, thickness, r.height + thickness, color, order);
    }

    private static TextMeshPro Text(Transform parent, string text, float x, float y, float w, float h, Color color, TextAlignmentOptions align, FontStyles style)
    {
        TextMeshPro t = WorldText(parent, text, x, y, w, h, color, 23);
        t.alignment = align;
        t.fontStyle = style;
        return t;
    }
}
