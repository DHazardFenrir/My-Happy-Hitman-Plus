using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// How the player dies "in real life" after failing too many times
public enum IrlDeathKind { HitByCar, CrushedFromAbove, Fall, ShotByDrone, MauledByDogs }

// What the target card shows and what the stage receives. Most stages target a monito by its
// clothes; others (the RC car) describe something else and use `code` for their own data.
public struct StageTarget
{
    public NPCAppearance.ClothingPart clothing;
    public int code;
    public string header;    // e.g. "TARGET"
    public string line1;     // e.g. "RED SHIRT"
    public string connector; // e.g. "&" (empty to hide it)
    public string line2;     // e.g. "BLACK PANTS"
    public string hint;      // controls reminder at the bottom of the card
}

public abstract class WarioStage : MonoBehaviour
{
    [Header("Base Config")]
    public float timeLimit = 15f;
    public Transform spawnPoint;
    public GameObject npcPrefab;

    [Tooltip("How many decoy monitos spawn besides the target")]
    public int extraNPCsCount = 3;

    [Tooltip("Monito walking speed in this minigame (0 = the prefab value)")]
    public float npcMoveSpeed = 1.4f;

    [Tooltip("Walking speed at 0 points, as a fraction of npcMoveSpeed (it reaches the full speed at the target score)")]
    [Range(0.3f, 1f)] public float easySpeedFactor = 0.75f;

    [Tooltip("Margin from the edge of the phone screen where monitos pop in/out")]
    public float screenMargin = 0.35f;

    [Tooltip("Seconds the trap has to hit someone after it is triggered. If it hits nobody, the minigame is failed.")]
    public float trapResolveTime = 2.5f;

    [Header("Camera Config")]
    public Transform cameraFocusPoint;
    public float stageOrthographicSize = 10f;

    public NPCAppearance.ClothingPart targetClothing { get; private set; }
    public bool isActive { get; protected set; } = false;
    public bool isFinished { get; protected set; } = false;
    public bool succeeded { get; protected set; } = false;
    public float TimeRemaining { get; private set; }

    protected readonly List<NPCWalker> spawnedNPCs = new List<NPCWalker>();

    // Autopilot: decided once per round (hit the target or press at the wrong time on purpose)
    protected bool autopilotMisses { get; private set; }
    protected float autopilotReactTime { get; private set; }

    // World area visible on the phone screen
    protected Rect view;

    // 0 at the start of the run, 1 at the target score: minigames start chill and get tougher
    protected float Difficulty => GameManager.Instance != null && GameManager.Instance.targetScore > 0
        ? Mathf.Clamp01((float)GameManager.Instance.Score / GameManager.Instance.targetScore) : 0f;

    private static readonly NPCAppearance.ClothingPart[] availableCombinations =
    {
        NPCAppearance.ClothingPart.RedShirt   | NPCAppearance.ClothingPart.BlackPants,
        NPCAppearance.ClothingPart.RedShirt   | NPCAppearance.ClothingPart.BluePants,
        NPCAppearance.ClothingPart.WhiteShirt | NPCAppearance.ClothingPart.BlackPants,
        NPCAppearance.ClothingPart.WhiteShirt | NPCAppearance.ClothingPart.BluePants,
        NPCAppearance.ClothingPart.BlackShirt | NPCAppearance.ClothingPart.BlackPants,
        NPCAppearance.ClothingPart.BlackShirt | NPCAppearance.ClothingPart.BluePants,
        NPCAppearance.ClothingPart.GreenShirt | NPCAppearance.ClothingPart.BlackPants,
        NPCAppearance.ClothingPart.GreenShirt | NPCAppearance.ClothingPart.BluePants,
        NPCAppearance.ClothingPart.BlueShirt  | NPCAppearance.ClothingPart.BlackPants,
        NPCAppearance.ClothingPart.BlueShirt  | NPCAppearance.ClothingPart.BluePants
    };

    // The target is picked BEFORE the stage is instantiated, so the card can be shown first.
    public static NPCAppearance.ClothingPart PickRandomTarget()
    {
        return availableCombinations[Random.Range(0, availableCombinations.Length)];
    }

    // ---- Target card ----

    // Called on the PREFAB before it's instantiated (so it must not change anything): picks the target
    // and says what the card shows. By default a random shirt + pants combination.
    public virtual StageTarget PickTarget()
    {
        NPCAppearance.ClothingPart clothing = PickRandomTarget();
        return new StageTarget
        {
            clothing = clothing,
            header = "TARGET",
            line1 = GameManager.ShirtName(clothing),
            connector = "&",
            line2 = GameManager.PantsName(clothing),
            hint = "SPACE / CLICK AT THE RIGHT TIME",
        };
    }

    // Extra card(s) between the target card and the minigame (also called on the prefab). None by default.
    public virtual IEnumerator BeforeStage(GameUI ui, StageTarget target) { yield break; }

    public virtual void SetupStage(StageTarget target) => SetupStage(target.clothing);

    // ---- "Real life" death (see IrlDeathSequence) ----
    public virtual IrlDeathKind IrlDeath => IrlDeathKind.Fall;

    // What kills you (the car, the piano...). Null if it doesn't apply.
    public virtual Sprite IrlThreatSprite => null;

    // The place you walk through (behind the phone) while this minigame is on
    public virtual WalkTheme Theme => WalkTheme.Street;

    public Vector3 GetCameraPosition()
    {
        Vector3 pos = cameraFocusPoint != null ? cameraFocusPoint.position : transform.position;
        pos.z = -10f;
        return pos;
    }

    public virtual void SetupStage(NPCAppearance.ClothingPart target)
    {
        targetClothing = target;
        PrepareStage();

        List<NPCAppearance.ClothingPart> extraNPCPool = new List<NPCAppearance.ClothingPart>(availableCombinations);
        extraNPCPool.Remove(target);

        int extras = Mathf.Min(extraNPCsCount, extraNPCPool.Count);

        // Monitos spawn INSIDE the phone screen (they never walk in from outside the frame):
        // a row starting at the spawnPoint or at the visible left edge, whichever is further in.
        // Early on the target takes the spot farthest from the trap (it arrives last, so there's time
        // to spot it); as the score goes up it's more and more likely to be anywhere in the row.
        float startX = Mathf.Max(spawnPoint.position.x, view.xMin + screenMargin);
        List<Vector3> slots = new List<Vector3>();
        float currentXOffset = 0f;
        for (int i = 0; i <= extras; i++)
        {
            slots.Add(new Vector3(startX + currentXOffset, spawnPoint.position.y, spawnPoint.position.z));
            currentXOffset += Random.Range(0.6f, 0.8f);
        }
        int targetSlot = Random.value < Difficulty * Difficulty ? Random.Range(0, slots.Count) : FarthestFromTrap(slots); // 1% at 100 points, 25% at 500, 100% at the target score
        Vector3 targetPosition = slots[targetSlot];
        slots.RemoveAt(targetSlot);
        Shuffle(slots);
        slots.Insert(0, targetPosition);

        SpawnNPC(slots[0], target, true);

        for (int i = 0; i < extras; i++)
        {
            int randDistractor = Random.Range(0, extraNPCPool.Count);
            SpawnNPC(slots[i + 1], extraNPCPool[randDistractor], false);
            extraNPCPool.RemoveAt(randDistractor);
        }
    }

    private int FarthestFromTrap(List<Vector3> slots)
    {
        if (!TryGetTrapZone(out float trapX, out _)) return Random.Range(0, slots.Count);
        int best = 0;
        for (int i = 1; i < slots.Count; i++)
            if (Mathf.Abs(slots[i].x - trapX) > Mathf.Abs(slots[best].x - trapX)) best = i;
        return best;
    }

    // Starts the timer and focuses the camera first, to know which part of the world is visible on the phone.
    // Stages without monitos call only this from their SetupStage.
    protected void PrepareStage()
    {
        TimeRemaining = timeLimit;
        Vector3 anchor = spawnPoint != null ? spawnPoint.position : transform.position;
        if (RenderCameraFollow.Instance != null)
        {
            RenderCameraFollow.Instance.FocusOnStage(this);
            view = RenderCameraFollow.Instance.GetVisibleWorldRect(anchor.z);
        }
        else
        {
            view = new Rect(anchor.x - 1f, anchor.y - 5f, 1000f, 10f);
        }
    }

    private void SpawnNPC(Vector3 position, NPCAppearance.ClothingPart clothing, bool isTarget)
    {
        // Monitos are children of the stage: they're destroyed with it
        GameObject npcObj = Instantiate(npcPrefab, position, Quaternion.identity, transform);

        NPCAppearance appearance = npcObj.GetComponent<NPCAppearance>();
        if (appearance != null) appearance.SetupAppearance(clothing);

        NPCWalker walker = npcObj.GetComponent<NPCWalker>();
        if (walker != null)
        {
            walker.clothingPart = clothing;
            walker.isTarget = isTarget;
            walker.CanMove = false; // They don't walk until the minigame starts
            if (npcMoveSpeed > 0f) walker.moveSpeed = npcMoveSpeed * Mathf.Lerp(easySpeedFactor, 1f, Difficulty);
            walker.SetWalkDirection(Vector3.right);
            walker.PopIn();
            spawnedNPCs.Add(walker);
        }
    }

    public virtual void StartStage()
    {
        isActive = true;
        autopilotMisses = Random.value < Autopilot.MissChance;
        autopilotReactTime = Random.Range(0.5f, 1.2f);
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc != null) npc.CanMove = true;
        }
    }

    // The timer lives in the base class; stages implement OnStageUpdate.
    protected virtual void Update()
    {
        if (!isActive) return;

        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining <= 0f)
        {
            EndMinigame(false);
            return;
        }

        HandleScreenEdges();
        if (!isActive) return;

        if (Autopilot.Enabled) AutopilotTick();
        OnStageUpdate();
    }

    // Monitos pop out before reaching the edge of the phone screen.
    // If no monito is left on screen, the minigame is failed.
    // (The drone minigames override it: there people leave and come back in.)
    protected virtual void HandleScreenEdges()
    {
        bool anyoneLeft = false;
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc == null || npc.IsDead || npc.IsGone) continue;
            anyoneLeft = true;

            float x = npc.transform.position.x;
            bool atEdge = npc.WalkDirection.x >= 0 ? x > view.xMax - screenMargin : x < view.xMin + screenMargin;
            if (atEdge && !npc.IsPopping) npc.PopOut(true);
        }

        if (!anyoneLeft) EndMinigame(false);
    }

    protected abstract void OnStageUpdate();

    // ---- Autopilot (see Autopilot.cs) ----

    // Where the trap lands (world X) and how many seconds it takes to reach the monitos.
    // Stages that don't override it are never triggered by the bot.
    public virtual bool TryGetTrapAim(out float worldX, out float delay)
    {
        worldX = 0f;
        delay = 0f;
        return false;
    }

    // Presses when the target will be right under the trap when it arrives.
    // When it decided to miss, it presses early, while the target is still walking toward it.
    private void AutopilotTick()
    {
        if (timeLimit - TimeRemaining < autopilotReactTime) return;
        if (!TryGetTrapAim(out float aimX, out float delay)) return;

        NPCWalker target = spawnedNPCs.Find(n => n != null && n.isTarget && !n.IsDead && !n.IsGone);
        if (target == null || !target.IsWalking) return;

        float predicted = target.BodyCenter.x + target.WalkDirection.x * target.moveSpeed * delay;
        float wanted = autopilotMisses ? aimX - 0.75f * target.WalkDirection.x : aimX;
        if (Mathf.Abs(predicted - wanted) < 0.06f) Autopilot.Press();
    }

    // Call when the trap is triggered: if it hasn't hit anyone within trapResolveTime, it's a fail.
    protected void BeginTrapResolve()
    {
        StartCoroutine(TrapResolveRoutine());
    }

    private IEnumerator TrapResolveRoutine()
    {
        yield return new WaitForSeconds(trapResolveTime);
        if (isActive) EndMinigame(false);
    }

    // Height of the monitos' feet and heads (to place the automatic ground and walls)
    protected float NPCFeetY()
    {
        foreach (NPCWalker npc in spawnedNPCs)
            if (npc != null) return npc.BodyRect.yMin;
        return spawnPoint.position.y;
    }

    protected float NPCHeadY()
    {
        foreach (NPCWalker npc in spawnedNPCs)
            if (npc != null) return npc.BodyRect.yMax;
        return spawnPoint.position.y;
    }

    // Picks the monito that is REALLY under the trap: its body must touch the zone vertically
    // and its center must be inside the middle part of the trap. If several are, the most centered one.
    // This way grazing the monito next to it doesn't count.
    public NPCWalker FindVictim(Bounds trapZone, float centerTolerance)
    {
        NPCWalker best = null;
        float bestDx = float.MaxValue;
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc == null || npc.IsDead || npc.IsGone) continue;

            Rect body = npc.BodyRect;
            if (body.yMax < trapZone.min.y || body.yMin > trapZone.max.y) continue;

            float dx = Mathf.Abs(body.center.x - trapZone.center.x);
            if (dx > centerTolerance || dx >= bestDx) continue;

            best = npc;
            bestDx = dx;
        }
        return best;
    }

    // ---- Heads-up (shown over the first moment of the minigame, which is already running) ----

    // Where the trap hits (world X of its center, and half its width). By default the trap's aim with
    // a typical width; stages with an exact hit area override it. Stages without a trap return false.
    public virtual bool TryGetTrapZone(out float centerX, out float halfWidth)
    {
        halfWidth = 0.4f;
        return TryGetTrapAim(out centerX, out _);
    }

    // The one word on the action card right before the minigame (WarioWare style)
    public virtual string ActionVerb => "GO!";

    // Trap stages show the HIT ZONE (a pulsing column between two cones; the cones stay for the whole
    // round). Nothing waits for it: the monitos walk from the first frame.
    public IEnumerator HeadsUp(float seconds, Sprite cone)
    {
        if (seconds <= 0f) yield break;
        Vector2 origin = transform.position;
        List<GameObject> temporary = new List<GameObject>();
        if (!TryGetTrapZone(out float centerX, out float halfWidth)) yield break;
        Rect phone = RenderCameraFollow.Instance != null ? RenderCameraFollow.Instance.GetPhoneVisibleWorldRect(transform.position.z) : view;

        // In front of the set (translucent, so the monitos still show through)
        float x = centerX - origin.x, feet = NPCFeetY() - origin.y, head = NPCHeadY() - origin.y;
        float top = head + 0.3f, height = top - feet;
        SpriteRenderer column = StageBuilder.Box(transform, "Hit Zone", x, (feet + top) / 2f, halfWidth * 2f, height, Color.clear, 40);
        temporary.Add(column.gameObject);
        // Edges: a yellow line with a dark border, readable on any set
        foreach (int side in new[] { -1, 1 })
        {
            temporary.Add(StageBuilder.Box(transform, "Hit Zone Edge", x + side * halfWidth, (feet + top) / 2f, 0.09f, height, new Color(0f, 0f, 0f, 0.8f), 41).gameObject);
            temporary.Add(StageBuilder.Box(transform, "Hit Zone Edge", x + side * halfWidth, (feet + top) / 2f, 0.04f, height, HeadsUpYellow, 42).gameObject);
        }

        // The label on a dark tag, never off the phone (the bezel covers its very edges)
        float labelX = Mathf.Clamp(x, phone.xMin - origin.x + 1.3f, phone.xMax - origin.x - 1.3f), labelY = top + 0.35f;
        temporary.Add(StageBuilder.Box(transform, "Hit Zone Tag", labelX, labelY, 1.55f, 0.4f, new Color(0f, 0f, 0f, 0.8f), 41).gameObject);
        TextMeshPro label = StageBuilder.WorldText(transform, "HIT ZONE", labelX, labelY, 1.45f, 0.3f, HeadsUpYellow, 42);
        label.fontStyle = FontStyles.Bold;
        temporary.Add(label.gameObject);
        if (cone != null)
            foreach (int side in new[] { -1, 1 })
            {
                SpriteRenderer c = StageBuilder.SpriteAt(transform, "Hit Zone Cone", cone, x + side * (halfWidth + 0.12f), feet + 0.18f, 1.2f, Color.white, 5);
                c.transform.localPosition = new Vector3(c.transform.localPosition.x, feet + c.bounds.extents.y - 0.05f, 0f);
            }

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            column.color = new Color(1f, 0.85f, 0.2f, 0.18f + 0.14f * Mathf.Sin(t * 12f));
            yield return null;
        }
        foreach (GameObject go in temporary) if (go != null) Destroy(go);
    }

    private static readonly Color HeadsUpYellow = new Color(1f, 0.85f, 0.2f);

    // The trap hit a monito
    public void OnTrapHitNPC(NPCWalker npc)
    {
        if (!isActive || npc == null || npc.IsDead) return;

        npc.Kill();
        bool isMatch = npc.isTarget || (npc.clothingPart & targetClothing) == targetClothing;
        EndMinigame(isMatch);
    }

    // The trap hit nobody (crashed into the ground/wall, or the sewer was empty)
    public void OnTrapMissed()
    {
        if (!isActive) return;
        EndMinigame(false);
    }

    protected void EndMinigame(bool success)
    {
        if (isFinished) return;

        isActive = false;
        isFinished = true;
        succeeded = success;
        StopAllCoroutines();
        // The GameManager shows the result card and destroys the stage.
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
