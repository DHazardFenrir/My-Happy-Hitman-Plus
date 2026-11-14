using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Eighth minigame: a busy downtown street lined with shops. Between the donut shop and the phone
// repair shop there's a small kennel packed with guard dogs behind a "smart gate"; they jump at the
// bars and bark nonstop. A danger zone is painted on the sidewalk in front of it. Press when the
// target is on the danger zone: the gate rolls up and the dogs rush out and maul whoever is standing
// there. Nobody there (or the wrong person) is a fail.
// The set lives in its prefab (---- EIGHTH MINI GAME (guard dogs) ----); after changing the layout
// in StageSetBaker use Tools > Agent TR8N1 > Rebuild guard dogs stage.
public class DogFenceStage : WarioStage
{
    [Header("Gate")]
    [Tooltip("Center of the gate (local X)")]
    [SerializeField] private float gateCenterX = 0f;
    [Tooltip("Width of the danger zone in front of the kennel: whoever is in it gets bitten (also painted on the sidewalk)")]
    [SerializeField] private float biteZoneWidth = 2.4f;
    [SerializeField] private float gateRiseHeight = 1.7f;
    [SerializeField] private float gateRiseTime = 0.15f;

    [Header("Dogs")]
    [SerializeField] private float runSpeed = 9f;
    [Tooltip("Seconds between barks of each dog (random in this range)")]
    [SerializeField] private Vector2 barkInterval = new Vector2(0.5f, 1.3f);

    [Header("Set (parts that move)")]
    [SerializeField] private Transform gate;
    [SerializeField] private SpriteRenderer[] dogs;
    [SerializeField] private SpriteRenderer statusLight;
    [SerializeField] private SpriteRenderer ledStrip;
    [SerializeField] private TextMeshPro screenText;

    // Sidewalk level in the prefab (local units), same as the hotel floor
    public const float FloorY = -3.0f;
    public float BiteZoneWidth => biteZoneWidth;

    private bool released;
    private Vector3[] home;
    private float[] nextBark, hopPhase;

    public override WalkTheme Theme => WalkTheme.Street;
    public override string ActionVerb => "RELEASE!";
    public override IrlDeathKind IrlDeath => IrlDeathKind.MauledByDogs;
    public override Sprite IrlThreatSprite => GameSprites.Dog(0);

    public override void SetupStage(NPCAppearance.ClothingPart target)
    {
        base.SetupStage(target); // spawns the row before the bite zone (WarioStage.LineUpBeforeTrap)
        home = new Vector3[dogs.Length];
        nextBark = new float[dogs.Length];
        hopPhase = new float[dogs.Length];
        for (int i = 0; i < dogs.Length; i++)
        {
            home[i] = dogs[i].transform.localPosition;
            nextBark[i] = Random.Range(0.1f, barkInterval.y);
            hopPhase[i] = Random.value * 10f;
        }
        SetFenceState(locked: true);
    }

    protected override void OnStageUpdate()
    {
        if (released) return;
        AngryInTheKennel();

        if (SingleInput.ActionPressed)
        {
            released = true;
            StartCoroutine(ReleaseTheDogs());
        }
    }

    // ---------------- In the kennel ----------------

    // They jump against the bars, shuffle around a little, turn, and bark nonstop
    private void AngryInTheKennel()
    {
        for (int i = 0; i < dogs.Length; i++)
        {
            float t = Time.time * 7f + hopPhase[i];
            float hop = Mathf.Max(0f, Mathf.Sin(t)) * 0.18f;
            float shuffle = Mathf.Sin(t * 0.23f) * 0.08f;
            dogs[i].transform.localPosition = home[i] + new Vector3(shuffle, hop, 0f);
            dogs[i].sprite = GameSprites.Dog(hop > 0.05f ? 1 : 0);
            if (Random.value < 0.004f) dogs[i].flipX = !dogs[i].flipX;

            nextBark[i] -= Time.deltaTime;
            if (nextBark[i] <= 0f)
            {
                nextBark[i] = Random.Range(barkInterval.x, barkInterval.y);
                SoundFX.Play(Sfx.DogBark, Random.Range(0.75f, 1f));
                StartCoroutine(Woof(dogs[i].transform));
            }
        }
    }

    // "WOOF!" pops above a dog
    private IEnumerator Woof(Transform dog)
    {
        TextMeshPro woof = StageBuilder.WorldText(transform, Random.value < 0.5f ? "WOOF!" : "GRR!", 0f, 0f, 0.6f, 0.2f, Color.white, 6);
        woof.outlineWidth = 0.3f;
        woof.outlineColor = Color.black;
        Vector3 start = dog.localPosition + new Vector3(Random.Range(-0.15f, 0.15f), 0.5f, 0f);
        float t = 0f;
        while (t < 0.45f && woof != null)
        {
            t += Time.deltaTime;
            woof.transform.localPosition = start + new Vector3(0f, t * 0.5f, 0f);
            woof.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.1f, Mathf.Clamp01(t / 0.1f));
            woof.alpha = 1f - Mathf.Clamp01((t - 0.25f) / 0.2f);
            yield return null;
        }
        if (woof != null) Destroy(woof.gameObject);
    }

    // ---------------- Release ----------------

    private IEnumerator ReleaseTheDogs()
    {
        // Who is on the danger zone right now? They freeze in fear.
        Vector3 zoneCenter = transform.TransformPoint(new Vector3(gateCenterX, FloorY + 0.55f, 0f));
        Bounds zone = new Bounds(zoneCenter, new Vector3(biteZoneWidth, 1.1f, 1f));
        NPCWalker victim = FindVictim(zone, biteZoneWidth / 2f);
        if (victim != null) victim.CanMove = false;

        // The smart gate unlocks and rolls up (fast)
        SetFenceState(locked: false);
        SoundFX.Play(Sfx.GateOpen);
        Vector3 gateStart = gate.localPosition;
        float t = 0f;
        while (t < gateRiseTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / gateRiseTime);
            gate.localPosition = gateStart + new Vector3(0f, gateRiseHeight * k, 0f);
            yield return null;
        }
        SoundFX.Play(Sfx.DogBark);

        // The dogs rush out onto the sidewalk, in front of everyone, surrounding the victim
        foreach (SpriteRenderer dog in dogs) { dog.sortingOrder = 6; dog.color = Color.white; }
        float targetX = victim != null ? transform.InverseTransformPoint(victim.BodyCenter).x : gateCenterX;
        List<Vector3> spots = new List<Vector3>();
        for (int i = 0; i < dogs.Length; i++)
        {
            float side = (i % 2 == 0 ? -1f : 1f) * (0.2f + (i / 2) * 0.22f);
            bool leaping = i >= dogs.Length - 2; // the last ones jump at the chest
            spots.Add(new Vector3(targetX + side, FloorY + 0.3f + (leaping ? 0.45f : 0f), 0f));
        }

        bool arrived = false;
        while (!arrived)
        {
            arrived = true;
            for (int i = 0; i < dogs.Length; i++)
            {
                Transform d = dogs[i].transform;
                float dir = spots[i].x - d.localPosition.x;
                d.localPosition = Vector3.MoveTowards(d.localPosition, spots[i], runSpeed * Time.deltaTime);
                dogs[i].sprite = GameSprites.Dog((int)(Time.time * 14f) % 2);
                if (Mathf.Abs(dir) > 0.01f) dogs[i].flipX = dir < 0f;
                if (Vector3.Distance(d.localPosition, spots[i]) > 0.01f) arrived = false;
            }
            yield return null;
        }

        if (victim == null)
        {
            // Nobody there: they sniff around, confused, and it's a fail
            for (int i = 0; i < 3; i++)
            {
                foreach (SpriteRenderer dog in dogs) dog.flipX = !dog.flipX;
                SoundFX.Play(Sfx.DogBark, 0.8f);
                yield return new WaitForSeconds(0.3f);
            }
            OnTrapMissed();
            yield break;
        }

        // They maul the victim: jumping on it, shaking, growling
        for (int i = 0; i < dogs.Length; i++) dogs[i].flipX = spots[i].x > targetX;
        SoundFX.Play(Sfx.DogBite);
        Vector3 victimStart = victim.transform.position;
        t = 0f;
        while (t < 0.7f)
        {
            t += Time.deltaTime;
            for (int i = 0; i < dogs.Length; i++)
            {
                Transform d = dogs[i].transform;
                float hop = Mathf.Abs(Mathf.Sin((t * 14f) + i * 1.3f)) * 0.22f;
                d.localPosition = spots[i] + new Vector3(0f, hop, 0f);
                d.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 30f + i) * 12f);
            }
            victim.transform.position = victimStart + (Vector3)(Random.insideUnitCircle * 0.06f);
            yield return null;
        }
        foreach (SpriteRenderer dog in dogs) dog.transform.localRotation = Quaternion.identity;
        OnTrapHitNPC(victim);
    }

    // Red light + "LOCKED", or green + "RELEASED"
    private void SetFenceState(bool locked)
    {
        Color c = locked ? new Color(1f, 0.15f, 0.15f) : new Color(0.2f, 1f, 0.4f);
        if (statusLight != null) statusLight.color = c;
        if (ledStrip != null) ledStrip.color = locked ? new Color(0.2f, 0.85f, 1f) : c;
        if (screenText != null)
        {
            screenText.text = locked ? "LOCKED" : "RELEASED";
            screenText.color = c;
        }
    }

    public override bool TryGetTrapAim(out float worldX, out float delay)
    {
        worldX = transform.TransformPoint(new Vector3(gateCenterX, 0f, 0f)).x;
        delay = gateRiseTime;
        return true;
    }

    public override bool TryGetTrapZone(out float centerX, out float halfWidth)
    {
        halfWidth = biteZoneWidth / 2f;
        return TryGetTrapAim(out centerX, out _);
    }
}
