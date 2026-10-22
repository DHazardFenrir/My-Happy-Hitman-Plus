using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Cinemática final al llegar a la puntuación objetivo, a pantalla completa con look de cámara de vigilancia:
// los monitos cruzan la calle, pasan drones (en vez de pájaros) con un banner "THANKS FOR YOUR SERVICE",
// un dron dispara a algunos monitos, salen los créditos y se regresa al menú de inicio.
public class EndingSequence : MonoBehaviour
{
    [Header("Monitos")]
    public int walkerCount = 8;
    public Vector2 walkSpeedRange = new Vector2(0.6f, 1.1f);
    public float panicSpeedMultiplier = 2.5f;

    [Header("Drones")]
    public int droneCount = 3;
    public float droneSpeed = 1.6f;
    public string bannerText = "THANKS FOR YOUR SERVICE";
    [Tooltip("Opcional: sprite propio para el dron. Si está vacío se usa uno pixel-art generado por código")]
    public Sprite droneSpriteOverride;

    [Header("Disparos")]
    public int victims = 4;
    public float bulletSpeed = 9f;

    private const int DroneOrder = 50;
    private const float EdgeMargin = 0.35f;

    private readonly List<NPCWalker> walkers = new List<NPCWalker>();
    private Transform root;
    private float viewLeft, viewRight, viewTop, streetY, droneY;
    private bool running;

    [Header("Créditos")]
    public string[] creditRoles = { "DIRECTOR & PROGRAMMER", "ARTIST" };
    public string[] creditNames = { "Gold", "Luis Ortega" };

    private CinematicOverlay overlay;

    // La secuencia final ya NO se ve en el celular: ocupa toda la pantalla como si fuera
    // una cámara de vigilancia — el "aha": todo este tiempo te estaban grabando.
    public IEnumerator Play(WarioStage backdropPrefab, Canvas phoneCanvas, StreetBackground street)
    {
        running = true;
        walkers.Clear();

        if (overlay == null) overlay = gameObject.AddComponent<CinematicOverlay>();
        overlay.Show();
        overlay.SetCCTV(false);
        overlay.HideCaption();
        overlay.SetFade(0f);

        // 1. Se apaga todo a negro (el celular incluido)
        yield return overlay.FadeTo(1f, 0.8f);
        yield return new WaitForSeconds(0.4f);

        if (phoneCanvas != null) phoneCanvas.enabled = false;
        if (street != null) street.SetVisible(false);

        // 2. Escenario: se reutiliza un minijuego (la calle) como fondo, sin su lógica,
        //    y la cámara del minijuego pasa a renderizar a pantalla completa
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

        // 3. Monitos cruzando de un lado a otro
        if (backdrop.npcPrefab != null)
        {
            for (int i = 0; i < walkerCount; i++) SpawnWalker(backdrop.npcPrefab, i);
        }
        StartCoroutine(WrapWalkersRoutine());

        // 4. Corte a la cámara de vigilancia, con un par de saltos de señal
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

        overlay.ShowCaption($"{GameManager.Instance?.targetScore ?? 1000} POINTS\n<size=60%>MISSION ACCOMPLISHED</size>", new Color(1f, 0.85f, 0.2f), 0.72f, 56f);
        yield return new WaitForSeconds(2.5f);
        overlay.HideCaption();

        // 5. Parvada de drones con el banner
        List<Transform> drones = new List<Transform>();
        for (int i = 0; i < droneCount; i++)
        {
            Vector3 pos = new Vector3(viewRight + 1f + i * 0.9f, droneY + (i % 2) * 0.35f, 0f);
            drones.Add(CreateDrone(pos, i == 0));
        }

        float flockEnd = viewLeft - 3.5f;
        while (drones[drones.Count - 1].position.x > flockEnd)
        {
            foreach (Transform d in drones) d.position += Vector3.left * droneSpeed * Time.deltaTime;
            yield return null;
        }

        // 6. Un dron regresa y dispara a algunos monitos
        for (int i = 1; i < drones.Count; i++) Destroy(drones[i].gameObject);
        Transform shooter = drones[0];
        shooter.position = new Vector3(viewLeft - 0.6f, droneY, 0f);
        yield return MoveTo(shooter, new Vector3((viewLeft + viewRight) / 2f, droneY, 0f), droneSpeed * 1.5f);

        int kills = 0;
        int attempts = 0;
        while (kills < victims && attempts < victims * 4)
        {
            attempts++;
            NPCWalker target = PickVisibleTarget(shooter.position.x);
            if (target == null)
            {
                yield return new WaitForSeconds(0.3f);
                continue;
            }

            // Se alinea sobre el objetivo
            float aimTime = 0.5f;
            while (aimTime > 0f && target != null && !target.IsDead)
            {
                aimTime -= Time.deltaTime;
                Vector3 desired = new Vector3(target.BodyCenter.x, droneY, 0f);
                shooter.position = Vector3.MoveTowards(shooter.position, desired, droneSpeed * 2f * Time.deltaTime);
                yield return null;
            }

            if (target == null || target.IsDead) continue;
            yield return Shoot(shooter.position + Vector3.down * 0.25f, target);
            kills++;

            if (kills == 1) Panic();
            yield return new WaitForSeconds(0.35f);
        }

        // 7. El dron se va y aparece el mensaje
        yield return MoveTo(shooter, new Vector3(viewRight + 1.5f, viewTop + 1f, 0f), droneSpeed * 2f);
        overlay.ShowCaption(bannerText, Color.white, 0.5f, 72f);
        yield return new WaitForSeconds(2.5f);

        yield return overlay.FadeTo(1f, 1.2f);
        overlay.HideCaption();
        overlay.SetCCTV(false);

        // 8. Créditos sobre negro (fade in / fade out)
        yield return new WaitForSeconds(0.5f);
        for (int i = 0; i < Mathf.Min(creditRoles.Length, creditNames.Length); i++)
        {
            yield return overlay.ShowCredit(creditRoles[i], creditNames[i]);
            yield return new WaitForSeconds(0.3f);
        }

        // 9. Se restaura todo para volver al menú
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

    // Encuadra la calle a pantalla completa: el sprite más grande del escenario (el paso peatonal)
    // llena la pantalla sin dejar bordes vacíos.
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

    // Los monitos nunca cruzan el borde del celular: al llegar al borde desaparecen
    // y vuelven a aparecer del otro lado, dentro de la pantalla.
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

    private void Panic()
    {
        foreach (NPCWalker w in walkers)
        {
            if (w != null && !w.IsDead) w.moveSpeed *= panicSpeedMultiplier;
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
        GameObject bullet = CreateSprite("Bullet", RuntimeSprites.Bullet(), from, DroneOrder + 2);

        while (target != null && !target.IsDead)
        {
            Vector3 to = target.BodyCenter;
            bullet.transform.position = Vector3.MoveTowards(bullet.transform.position, to, bulletSpeed * Time.deltaTime);
            if (Vector3.Distance(bullet.transform.position, to) < 0.05f) break;
            yield return null;
        }

        Destroy(bullet);
        if (target != null && !target.IsDead) target.Kill(); // Kill() suelta la sangre
    }

    private Transform CreateDrone(Vector3 position, bool withBanner)
    {
        GameObject drone = new GameObject("Drone");
        drone.transform.SetParent(root, true);
        drone.transform.position = position;

        Sprite frameA = droneSpriteOverride != null ? droneSpriteOverride : RuntimeSprites.Drone(0);
        Sprite frameB = droneSpriteOverride != null ? droneSpriteOverride : RuntimeSprites.Drone(1);

        GameObject body = CreateSprite("Body", frameA, position, DroneOrder);
        body.transform.SetParent(drone.transform, true);

        DroneVisual visual = drone.AddComponent<DroneVisual>();
        visual.body = body.GetComponent<SpriteRenderer>();
        visual.frames = new[] { frameA, frameB };

        if (withBanner) CreateBanner(drone.transform);
        return drone.transform;
    }

    private void CreateBanner(Transform drone)
    {
        const float bannerWidth = 2.6f;
        const float bannerHeight = 0.36f;
        const float gap = 0.35f;

        Vector3 dronePos = drone.position;
        float bannerCenterX = 0.4f + gap + bannerWidth / 2f;

        // Cuerda
        GameObject rope = CreateSprite("Rope", RuntimeSprites.Pixel(), dronePos + new Vector3(0.4f + gap / 2f, -0.1f, 0f), DroneOrder - 2);
        rope.GetComponent<SpriteRenderer>().color = new Color(0.15f, 0.15f, 0.15f);
        rope.transform.localScale = new Vector3(gap, 0.02f, 1f);
        rope.transform.SetParent(drone, true);

        // Tela del banner
        GameObject cloth = CreateSprite("Banner", RuntimeSprites.Pixel(), dronePos + new Vector3(bannerCenterX, -0.1f, 0f), DroneOrder - 1);
        cloth.GetComponent<SpriteRenderer>().color = new Color(1f, 0.96f, 0.85f);
        cloth.transform.localScale = new Vector3(bannerWidth, bannerHeight, 1f);
        cloth.transform.SetParent(drone, true);

        // Texto
        GameObject textObj = new GameObject("Banner Text");
        textObj.transform.SetParent(drone, false);
        textObj.transform.position = dronePos + new Vector3(bannerCenterX, -0.1f, -0.01f);
        TextMeshPro text = textObj.AddComponent<TextMeshPro>();
        text.text = bannerText;
        text.color = new Color(0.7f, 0.05f, 0.05f);
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.05f;
        text.fontSizeMax = 20f;
        text.rectTransform.sizeDelta = new Vector2(bannerWidth * 0.92f, bannerHeight * 0.8f);
        text.sortingOrder = DroneOrder + 1;
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
