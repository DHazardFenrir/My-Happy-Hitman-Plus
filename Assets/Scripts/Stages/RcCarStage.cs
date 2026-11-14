using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static StageBuilder;

// Seventh minigame: a remote-control car on a highway, seen from above. The card describes a car
// (color + type, cars from the bk_cars1.a sheet); you drive a small toy version of another car of the
// sheet, picked each round (that model never shows up in the traffic); steer the RC car left/right (A/D, arrows, or hold a side of the screen) and ram
// the target so it spins off the road and crashes. Hitting another car, or letting the target get
// away, is a fail. The road set (asphalt, lanes, grass, curbs) lives in the prefab; the traffic and
// the RC car are made here.
public class RcCarStage : WarioStage
{
    // The traffic comes from the bk_cars1.a sprite sheet (top-down cars, front facing up).
    // slice = the N in bk_cars1.a_N. Specials (taxi, police, ambulance...) only show up as decoys.
    private struct CarModel
    {
        public int slice;
        public string color, type;
        public bool special;
        public CarModel(int slice, string color, string type, bool special = false)
        { this.slice = slice; this.color = color; this.type = type; this.special = special; }
    }

    private static readonly CarModel[] Catalog =
    {
        new CarModel(1, "BLACK", "SEDAN"), new CarModel(10, "PINK", "SEDAN"), new CarModel(11, "ORANGE", "SEDAN"),
        new CarModel(4, "RED", "PICKUP"), new CarModel(5, "BLUE", "PICKUP"), new CarModel(6, "YELLOW", "PICKUP"),
        new CarModel(12, "WHITE", "PICKUP"), new CarModel(13, "BLACK", "PICKUP"), new CarModel(14, "GRAY", "PICKUP"),
        new CarModel(15, "LIGHT BLUE", "VAN"), new CarModel(17, "RED", "VAN"), new CarModel(18, "BLACK", "VAN"),
        new CarModel(20, "GRAY", "VAN"),
        new CarModel(21, "BLACK", "SPORTS CAR"), new CarModel(22, "YELLOW", "SPORTS CAR"), new CarModel(23, "RED", "SPORTS CAR"),
        new CarModel(24, "WHITE", "SPORTS CAR"), new CarModel(25, "SILVER", "SPORTS CAR"),
        new CarModel(26, "WHITE", "RACE CAR"), new CarModel(27, "RED", "RACE CAR"), new CarModel(28, "BLUE", "RACE CAR"),
        new CarModel(29, "BLACK", "RACE CAR"),
        // Specials: only show up as decoys. (Vans 16 and 19 are left out: they look too much like 15 and 18.)
        new CarModel(9, "YELLOW", "TAXI", true), new CarModel(2, "", "POLICE CAR", true), new CarModel(3, "", "AMBULANCE", true),
        new CarModel(7, "", "FIRE TRUCK", true), new CarModel(0, "BLACK", "LIMO", true), new CarModel(8, "WHITE", "LIMO", true),
    };

    [Header("RC car")]
    [SerializeField] private float steerSpeed = 4.2f;
    [Tooltip("Lane centers (local X). The RC car can drive anywhere between the outer lanes")]
    [SerializeField] private float[] lanes = { -1.1f, 0f, 1.1f };
    [SerializeField] private float roadHalfWidth = 1.65f;
    [SerializeField] private float playerY = -5.2f;
    [Tooltip("The player's car is a smaller \"toy\" version of a car from the sheet")]
    [SerializeField] private float playerScale = 0.75f;
    [SerializeField] private float carScale = 1f;
    [Tooltip("The 30 cars of bk_cars1.a, in order (element N = bk_cars1.a_N). Filled in by StageSetBaker")]
    [SerializeField] private Sprite[] carSprites;

    [Header("Traffic")]
    [SerializeField] private int carCount = 4;
    [Tooltip("Seconds between cars")]
    [SerializeField] private float spawnInterval = 1.1f;
    [Tooltip("How fast the traffic comes down the screen (you are overtaking it)")]
    [SerializeField] private Vector2 trafficSpeed = new Vector2(1.9f, 2.7f);
    [Tooltip("Sideways speed a car gets when you ram it")]
    [SerializeField] private float knockSpeed = 4.8f;

    [Header("Set")]
    [Tooltip("Lane dashes, grass and curbs: moved down and wrapped to make the road scroll")]
    [SerializeField] private Transform scrollRoot;
    [SerializeField] private float scrollPeriod = 2f;
    [SerializeField] private float roadSpeed = 6f;
    [Tooltip("Where the traffic appears (local Y, just above the visible part of the phone)")]
    [SerializeField] private float spawnY = 3.8f;
    [Tooltip("Below this (local Y) a car has left the visible part of the phone")]
    [SerializeField] private float escapeY = -7f;

    private class Car
    {
        public Transform t;
        public int code;
        public float speed;          // down the screen (how fast it wants to go)
        public float currentSpeed;   // after keeping its distance to the car in front
        public Vector2 halfSize;     // for collisions
        public bool knocked, crashed;
        public Vector2 knockVelocity;
        public float spin;
    }

    private readonly List<Car> traffic = new List<Car>();
    private readonly List<int> pendingCodes = new List<int>();
    private int targetCode, playerCar;
    private Vector2 playerHalfSize = new Vector2(0.3f, 0.38f);
    private TMPro.TextMeshPro youLabel;
    private float youLabelY;
    private Transform player;
    private float playerX, bounceVelocity;
    private float spawnTimer;
    private bool hit;

    public override IrlDeathKind IrlDeath => IrlDeathKind.HitByCar;
    public override WalkTheme Theme => WalkTheme.Street;
    public override string ActionVerb => "RAM!";


    // ---------------- Card ----------------

    // code = target + player * 1000 (both are indexes in the catalog). The player's car is picked here too,
    // so it can be shown on the loading card and kept out of the traffic.
    private const int PlayerCodeStep = 1000;

    public override StageTarget PickTarget()
    {
        // The player drives a sports or race car; the target is any other regular car
        List<int> playable = new List<int>();
        for (int c = 0; c < Catalog.Length; c++)
            if (Catalog[c].type == "SPORTS CAR" || Catalog[c].type == "RACE CAR") playable.Add(c);
        int playerCar = playable[Random.Range(0, playable.Count)];
        int code;
        do code = Random.Range(0, Catalog.Length); while (Catalog[code].special || code == playerCar);
        return new StageTarget
        {
            code = code + playerCar * PlayerCodeStep,
            header = "TARGET VEHICLE",
            line1 = Catalog[code].color,
            connector = "",
            line2 = Catalog[code].type,
            hint = "STEER WITH A / D OR THE ARROWS",
        };
    }

    public override IEnumerator BeforeStage(GameUI ui, StageTarget target)
    {
        CarModel mine = Catalog[target.code / PlayerCodeStep];
        yield return ui.ShowLoadingCard("PREPARING RC CAR...", $"YOUR CAR: {mine.color} {mine.type}", "RAM THE TARGET OFF THE ROAD", SpriteOf(mine), 1.9f);
    }

    private Sprite SpriteOf(CarModel model) =>
        carSprites != null && model.slice < carSprites.Length ? carSprites[model.slice] : null;

    // ---------------- Setup ----------------

    public override void SetupStage(NPCAppearance.ClothingPart target) => SetupStage(PickTarget());

    public override void SetupStage(StageTarget target)
    {
        targetCode = target.code % PlayerCodeStep;
        playerCar = target.code / PlayerCodeStep;
        PrepareStage();

        // The RC car: a small version of a car from the sheet, with "YOU" bobbing above it
        player = new GameObject("RC Car").transform;
        player.SetParent(transform, false);
        playerX = 0f;
        player.localPosition = new Vector3(playerX, playerY, 0f);
        Sprite mine = SpriteOf(Catalog[playerCar]);
        Vector2 size = mine != null ? (Vector2)mine.bounds.size * playerScale : new Vector2(0.6f, 0.9f);
        playerHalfSize = size * 0.42f;
        Box(player, "Shadow", 0.06f, -0.06f, size.x * 0.85f, size.y * 0.95f, new Color(0f, 0f, 0f, 0.35f), 7);
        SpriteAt(player, "Body", mine, 0f, 0f, playerScale, Color.white, 9);
        youLabelY = size.y / 2f + 0.22f;
        youLabel = WorldText(player, "YOU", 0f, youLabelY, 0.5f, 0.2f, new Color(1f, 0.86f, 0.1f), 10);
        youLabel.outlineWidth = 0.3f;
        youLabel.outlineColor = Color.black;

        // The traffic: the target plus decoys that look alike (same color or same type), in random order,
        // and sometimes one special vehicle (taxi, police car, ambulance...) for flavor
        pendingCodes.Clear();
        pendingCodes.Add(targetCode);
        CarModel wanted = Catalog[targetCode];
        List<int> decoys = new List<int>(), specials = new List<int>();
        for (int c = 0; c < Catalog.Length; c++)
        {
            if (c == targetCode || c == playerCar) continue; // never a copy of your own car on the road
            if (Catalog[c].special) { specials.Add(c); continue; }
            if (Catalog[c].color == wanted.color || Catalog[c].type == wanted.type) decoys.Add(c);
        }
        if (Random.value < 0.6f && specials.Count > 0) pendingCodes.Add(specials[Random.Range(0, specials.Count)]);
        while (pendingCodes.Count < carCount && decoys.Count > 0)
        {
            int i = Random.Range(0, decoys.Count);
            pendingCodes.Add(decoys[i]);
            decoys.RemoveAt(i);
        }
        // The target is never the very first car (you get to see a decoy pass first)
        for (int i = pendingCodes.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pendingCodes[i], pendingCodes[j]) = (pendingCodes[j], pendingCodes[i]);
        }
        if (pendingCodes[0] == targetCode && pendingCodes.Count > 1) (pendingCodes[0], pendingCodes[1]) = (pendingCodes[1], pendingCodes[0]);

        spawnTimer = 0.2f;
    }

    public override void StartStage()
    {
        base.StartStage();
        SoundFX.Play(Sfx.CarStart);
    }

    // No monitos here
    protected override void HandleScreenEdges() { }

    // ---------------- Update ----------------

    protected override void OnStageUpdate()
    {
        ScrollRoad();
        SpawnTraffic();
        if (!hit) Steer();
        else
        {
            // Bounces back a little after the hit
            playerX += bounceVelocity * Time.deltaTime;
            bounceVelocity = Mathf.MoveTowards(bounceVelocity, 0f, 12f * Time.deltaTime);
            player.localRotation = Quaternion.Lerp(player.localRotation, Quaternion.identity, 8f * Time.deltaTime);
        }
        playerX = Mathf.Clamp(playerX, -roadHalfWidth + 0.35f, roadHalfWidth - 0.35f);
        player.localPosition = new Vector3(playerX, playerY + Mathf.Sin(Time.time * 30f) * 0.01f, 0f); // engine rattle
        if (youLabel != null)
        {
            youLabel.transform.localPosition = new Vector3(0f, youLabelY + Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 0.08f, 0f);
            youLabel.transform.rotation = Quaternion.identity; // stays upright while the car tilts
        }

        MoveTraffic();
        if (!hit) CheckRam();
    }

    private void ScrollRoad()
    {
        if (scrollRoot == null) return;
        Vector3 p = scrollRoot.localPosition;
        p.y -= roadSpeed * Time.deltaTime;
        if (p.y <= -scrollPeriod) p.y += scrollPeriod;
        scrollRoot.localPosition = p;
    }

    private void SpawnTraffic()
    {
        if (pendingCodes.Count == 0) return;
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;
        spawnTimer = spawnInterval * Random.Range(0.85f, 1.2f);

        // Only into a lane whose last car has already moved down (no car appears on top of another)
        List<float> free = new List<float>();
        foreach (float lane in lanes)
            if (!traffic.Exists(c => c.t != null && !c.knocked && Mathf.Abs(c.t.localPosition.x - lane) < 0.4f && c.t.localPosition.y > spawnY - 2.2f))
                free.Add(lane);
        if (free.Count == 0)
        {
            spawnTimer = 0.2f; // try again in a moment
            return;
        }

        int code = pendingCodes[0];
        pendingCodes.RemoveAt(0);
        traffic.Add(CreateCar(code, free[Random.Range(0, free.Count)]));
    }

    private Car CreateCar(int code, float laneX)
    {
        CarModel model = Catalog[code];
        Transform root = new GameObject($"Car {model.color} {model.type}").transform;
        root.SetParent(transform, false);
        root.localPosition = new Vector3(laneX, spawnY, 0f);

        Sprite sprite = carSprites != null && model.slice < carSprites.Length ? carSprites[model.slice] : null;
        Vector2 size = sprite != null ? (Vector2)sprite.bounds.size * carScale : new Vector2(0.8f, 1.45f);
        Box(root, "Shadow", 0.07f, -0.08f, size.x * 0.85f, size.y * 0.95f, new Color(0f, 0f, 0f, 0.3f), 3);
        SpriteAt(root, "Car", sprite, 0f, 0f, carScale, Color.white, 5);

        return new Car
        {
            t = root,
            code = code,
            speed = Random.Range(trafficSpeed.x, trafficSpeed.y),
            currentSpeed = trafficSpeed.x,
            halfSize = new Vector2(size.x * 0.4f, size.y * 0.45f),
        };
    }

    // A/D, arrows, or hold the left/right half of the screen (touch / mouse)
    private void Steer()
    {
        float input = Input.GetAxisRaw("Horizontal");
        if (Mathf.Approximately(input, 0f) && (Input.GetMouseButton(0) || Input.touchCount > 0))
        {
            Vector2 pointer = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            input = pointer.x < Screen.width / 2f ? -1f : 1f;
        }
        if (Autopilot.Enabled) input = AutopilotSteer();

        playerX += input * steerSpeed * Time.deltaTime;
        player.localRotation = Quaternion.Lerp(player.localRotation, Quaternion.Euler(0f, 0f, -input * 12f), 10f * Time.deltaTime);
    }

    private void MoveTraffic()
    {
        // Lowest cars first, so each car can follow the one in front of it in its lane
        traffic.Sort((a, b) => (a.t == null ? 0f : a.t.localPosition.y).CompareTo(b.t == null ? 0f : b.t.localPosition.y));
        foreach (Car car in traffic)
        {
            if (car.t == null) continue;
            Vector3 p = car.t.localPosition;

            if (car.crashed)
            {
                p.y -= roadSpeed * Time.deltaTime; // stopped on the grass: slides away with the scenery
            }
            else if (car.knocked)
            {
                p += (Vector3)car.knockVelocity * Time.deltaTime;
                p.y -= car.speed * Time.deltaTime;
                car.knockVelocity *= Mathf.Exp(-1.4f * Time.deltaTime);
                car.t.Rotate(0f, 0f, car.spin * Time.deltaTime);
                car.spin *= Mathf.Exp(-1.2f * Time.deltaTime);
                if (Mathf.Abs(p.x) > roadHalfWidth + 0.45f) StartCoroutine(Crash(car));
            }
            else
            {
                car.currentSpeed = FollowingSpeed(car, p);
                p.y -= car.currentSpeed * Time.deltaTime;
            }
            car.t.localPosition = p;

            // The target got away
            if (!hit && car.code == targetCode && p.y < escapeY && isActive) EndMinigame(false);
        }

        // Every car went by and nobody was rammed
        if (!hit && pendingCodes.Count == 0 && traffic.TrueForAll(c => c.t == null || c.t.localPosition.y < escapeY)) EndMinigame(false);
    }

    // Keeps a safe distance: a car can't come down faster than the car in front of it in the same lane
    // once it's close (cars never drive through each other)
    private float FollowingSpeed(Car car, Vector3 p)
    {
        const float safeGap = 0.35f;
        Car front = null;
        float gap = float.MaxValue;
        foreach (Car other in traffic)
        {
            if (other == car || other.t == null || other.knocked || other.crashed) continue;
            Vector3 o = other.t.localPosition;
            if (o.y >= p.y || Mathf.Abs(o.x - p.x) > 0.45f) continue;
            float g = (p.y - car.halfSize.y) - (o.y + other.halfSize.y);
            if (g < gap) { gap = g; front = other; }
        }
        // After the ram the RC car stops ramming, so the traffic behind it stops instead of driving over it
        if (hit && p.y > playerY && Mathf.Abs(playerX - p.x) < playerHalfSize.x + car.halfSize.x)
        {
            float g = (p.y - car.halfSize.y) - (playerY + playerHalfSize.y);
            if (g > -0.05f && g < safeGap && g < gap) return 0f;
        }
        if (front == null || gap > safeGap) return car.speed;
        // Too close: match the speed of the car in front, a bit slower if it's really close
        return Mathf.Min(car.speed, front.currentSpeed * (gap < safeGap * 0.5f ? 0.85f : 1f));
    }

    // The RC car touches a car (boxes overlapping): that car is knocked off the road
    private void CheckRam()
    {
        Vector2 me = new Vector2(playerX, playerY);
        Vector2 myHalf = playerHalfSize;
        foreach (Car car in traffic)
        {
            if (car.t == null || car.knocked) continue;
            Vector2 other = car.t.localPosition;
            if (Mathf.Abs(other.x - me.x) > myHalf.x + car.halfSize.x) continue;
            if (Mathf.Abs(other.y - me.y) > myHalf.y + car.halfSize.y) continue;

            hit = true;
            float dir = Mathf.Abs(other.x - me.x) < 0.12f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(other.x - me.x);
            // Pushed away from you (straight on: a random side)
            car.knocked = true;
            car.knockVelocity = new Vector2(dir * knockSpeed, 0.6f);
            car.spin = -dir * 520f;
            bounceVelocity = -dir * 2.5f;
            SoundFX.Play(Sfx.CarHit);
            Spark((me + other) / 2f);
            StartCoroutine(GiveUpIfStuck(car));
            break;
        }
    }

    // A knocked car that somehow stays on the road still counts once it stops spinning
    private IEnumerator GiveUpIfStuck(Car car)
    {
        yield return new WaitForSeconds(1.6f);
        if (isActive && !car.crashed) yield return Crash(car);
    }

    private IEnumerator Crash(Car car)
    {
        if (car.crashed) yield break;
        car.crashed = true;
        car.knockVelocity = Vector2.zero;
        SoundFX.Play(Sfx.CarCrash);
        StartCoroutine(Smoke(car.t));
        yield return new WaitForSeconds(0.6f);
        if (isActive) EndMinigame(car.code == targetCode);
    }

    // ---------------- Effects ----------------

    private void Spark(Vector2 localPos)
    {
        SpriteRenderer s = SpriteAt(transform, "Spark", GameSprites.SoftCircle(), localPos.x, localPos.y, 0.9f, new Color(1f, 0.95f, 0.6f, 0.9f), 12);
        StartCoroutine(FadeAndGrow(s, 0.25f, 1.6f));
    }

    private IEnumerator Smoke(Transform car)
    {
        SpriteRenderer flash = SpriteAt(car, "Crash Flash", GameSprites.SoftCircle(), 0f, 0f, 1.6f, new Color(1f, 0.55f, 0.1f, 0.9f), 12);
        StartCoroutine(FadeAndGrow(flash, 0.35f, 2.2f));
        for (int i = 0; i < 6 && car != null; i++)
        {
            Vector2 offset = Random.insideUnitCircle * 0.25f;
            SpriteRenderer puff = SpriteAt(car, "Smoke", GameSprites.SoftCircle(), offset.x, offset.y + 0.2f, 0.6f, new Color(0.25f, 0.25f, 0.27f, 0.8f), 11);
            puff.transform.SetParent(transform, true); // stays behind as the car slides away
            StartCoroutine(FadeAndGrow(puff, 0.9f, 2.5f));
            yield return new WaitForSeconds(0.08f);
        }
    }

    private static IEnumerator FadeAndGrow(SpriteRenderer sr, float seconds, float growTo)
    {
        Color c = sr.color;
        Vector3 start = sr.transform.localScale;
        float t = 0f;
        while (t < seconds && sr != null)
        {
            t += Time.deltaTime;
            float k = t / seconds;
            sr.transform.localScale = start * Mathf.Lerp(1f, growTo, k);
            sr.color = new Color(c.r, c.g, c.b, c.a * (1f - k));
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    // ---------------- Autopilot ----------------

    // Steers toward the target (or, when it decided to miss, toward a decoy) once it's coming close
    private float AutopilotSteer()
    {
        Car aim = null;
        foreach (Car car in traffic)
        {
            if (car.t == null || car.knocked || car.t.localPosition.y < playerY - 0.5f) continue;
            bool wanted = autopilotMisses ? car.code != targetCode : car.code == targetCode;
            if (wanted && (aim == null || car.t.localPosition.y < aim.t.localPosition.y)) aim = car;
        }
        if (aim == null || aim.t.localPosition.y > playerY + 4f) return 0f;
        float dx = aim.t.localPosition.x - playerX;
        return Mathf.Abs(dx) < 0.05f ? 0f : Mathf.Sign(dx);
    }
}
