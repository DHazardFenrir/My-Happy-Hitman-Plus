using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static StageBuilder;

// Twelfth minigame: the drone can't tell people apart, so you check faces for it. A dossier shows
// three generated faces, one at a time: PRESS = HUMAN, WAIT = NOT HUMAN, before the bar runs out.
// Humans, monsters, monsters that almost pass as human, and people with a facial difference (an
// eyepatch, a scar, a hearing aid, thick glasses, a birthmark, vitiligo), drawn like everyone else.
// The AI suggests an answer under each face, and it's wrong exactly on the tricky ones: it calls
// people with a difference "NOT HUMAN?" and lets near-human monsters through. Three right answers =
// HUMAN CONFIRMED: the dossier closes onto the drone camera, which finds the last human in the
// crowd and riddles them. One wrong answer = VERIFICATION FAILED.
// The street lives in the prefab; the crowd and the camera HUD come from DroneCamStage.
public class HumanCheckStage : DroneCamStage
{
    [Header("Human check")]
    [Tooltip("Seconds to answer each face, at 0 points and at the target score")]
    [SerializeField] private Vector2 secondsPerFace = new Vector2(1.7f, 1.05f);
    [Tooltip("Chance that a face is a tricky one (a difference or a near-human monster), at 0 points and at the target score")]
    [SerializeField] private Vector2 trickyChance = new Vector2(0.3f, 0.6f);
    [Tooltip("From this score on the AI talks about threats and its hints are wrong more often")]
    [SerializeField] private int threatModeFromScore = 600;

    private const int Faces = 3;
    private enum Kind { Human, HumanWithDifference, Monster, NearHumanMonster }

    private static readonly Color[] HumanSkin =
    {
        new Color(1f, 0.86f, 0.72f), new Color(0.93f, 0.74f, 0.58f), new Color(0.78f, 0.56f, 0.4f),
        new Color(0.58f, 0.4f, 0.27f), new Color(0.4f, 0.27f, 0.18f)
    };
    private static readonly Color[] MonsterSkin = { new Color(0.55f, 0.78f, 0.42f), new Color(0.62f, 0.48f, 0.78f), new Color(0.5f, 0.62f, 0.72f) };
    private static readonly Color[] HairColors =
    {
        new Color(0.15f, 0.1f, 0.08f), new Color(0.42f, 0.26f, 0.13f), new Color(0.85f, 0.7f, 0.35f),
        new Color(0.6f, 0.6f, 0.62f), new Color(0.65f, 0.2f, 0.12f)
    };
    private static readonly string[] Differences = { "diff_eyepatch", "diff_scar", "diff_hearingaid", "diff_glasses", "diff_birthmark", "diff_vitiligo" };
    private static readonly Color Green = new Color(0.25f, 0.85f, 0.4f), Red = new Color(1f, 0.3f, 0.3f), Paper = new Color(0.93f, 0.91f, 0.84f);

    private Transform dossier, faceRoot, timerFill;
    private TextMeshPro subjectLabel, aiLabel, stamp;
    private readonly List<SpriteRenderer> pips = new List<SpriteRenderer>();
    private float progressToTarget;
    private int layerOrder; // drawing order of the next face layer
    private string tell; // what gives this face away, shown when you get it wrong
    private bool threatMode;
    private NPCWalker target;

    public override WalkTheme Theme => WalkTheme.Crosswalk;
    public override string ActionVerb => "VERIFY!";
    protected override Color HudColor => new Color(1f, 1f, 1f, 0.9f);
    protected override string CamLabel => "STRIKE // " + AssignedDrone.Tag;
    protected override bool HasCrosshair => false;

    public override StageTarget PickTarget()
    {
        StageTarget t = base.PickTarget();
        t.header = "VERIFY: HUMAN";
        t.hint = "PRESS = HUMAN   WAIT = NOT HUMAN";
        return t;
    }

    protected override void BuildOverlay()
    {
        progressToTarget = GameManager.Instance != null ? Mathf.Clamp01((float)GameManager.Instance.Score / GameManager.Instance.targetScore) : 0f;
        threatMode = GameManager.Instance != null && GameManager.Instance.Score >= threatModeFromScore;
        target = spawnedNPCs.Find(n => n != null && n.isTarget);
        BuildDossier();
    }

    public override void StartStage()
    {
        base.StartStage();
        StartCoroutine(Run());
    }

    protected override void OnStageUpdate()
    {
        AnimateHud();
        KeepWanderersInside();
    }

    // ---------------- The checks ----------------

    private IEnumerator Run()
    {
        // At least one human, and the last human you confirm is the one the drone goes for
        Kind[] kinds = new Kind[Faces];
        bool anyHuman = false;
        float tricky = Mathf.Lerp(trickyChance.x, trickyChance.y, progressToTarget);
        for (int i = 0; i < Faces; i++)
        {
            bool human = Random.value < 0.5f || (i == Faces - 1 && !anyHuman);
            bool hard = Random.value < tricky;
            kinds[i] = human ? (hard ? Kind.HumanWithDifference : Kind.Human) : (hard ? Kind.NearHumanMonster : Kind.Monster);
            anyHuman |= human;
        }
        int autopilotWrong = autopilotMisses ? Random.Range(0, Faces) : -1;
        float seconds = Mathf.Lerp(secondsPerFace.x, secondsPerFace.y, progressToTarget);

        yield return new WaitForSeconds(0.35f);
        for (int i = 0; i < Faces; i++)
        {
            Kind kind = kinds[i];
            bool isHuman = kind == Kind.Human || kind == Kind.HumanWithDifference;
            ShowFace(i, kind);

            bool pressed = false;
            for (float t = 0f; t < seconds && !pressed; t += Time.deltaTime)
            {
                timerFill.localScale = new Vector3(1f - t / seconds, 1f, 1f);
                if (Autopilot.Enabled && t >= Mathf.Min(0.5f, seconds * 0.5f) && isHuman != (i == autopilotWrong)) Autopilot.Press();
                if (SingleInput.ActionPressed) pressed = true;
                yield return null;
            }

            bool right = pressed == isHuman;
            Stamp(pressed ? "HUMAN" : "NOT HUMAN", right ? (pressed ? Green : Red) : Color.gray);
            SoundFX.Play(right ? Sfx.MenuSelect : Sfx.Fail, 0.7f);
            if (right) pips[i].color = Green;
            yield return new WaitForSeconds(0.45f);

            if (!right)
            {
                Stamp("VERIFICATION FAILED", Red);
                pips[i].color = Red;
                aiLabel.text = isHuman ? tell : "MISSED: " + tell; // so a wrong answer is never a mystery
                aiLabel.color = new Color(1f, 0.86f, 0.1f);
                yield return new WaitForSeconds(1.1f);
                OnTrapMissed();
                yield break;
            }
        }

        Stamp("HUMAN CONFIRMED", Green);
        SoundFX.Play(Sfx.ScanLock);
        yield return new WaitForSeconds(0.6f);
        yield return Riddle();
    }

    // The dossier closes onto the drone camera: the crosshair finds the target and it's riddled
    private IEnumerator Riddle()
    {
        dossier.gameObject.SetActive(false);
        Vector2 from = visible.center;
        SpriteRenderer crosshair = SpriteAt(transform, "Crosshair", GameSprites.Crosshair(), from.x, from.y, 0.9f, Red, 35);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            Vector2 to = (Vector2)target.BodyRect.center - (Vector2)transform.position;
            crosshair.transform.localPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.6f));
            crosshair.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 0.45f, t / 0.6f);
            yield return null;
        }

        target.CanMove = false;
        for (int shot = 0; shot < 6; shot++)
        {
            Vector2 p = (Vector2)target.BodyRect.center - (Vector2)transform.position + Random.insideUnitCircle * 0.12f;
            crosshair.transform.localPosition = p;
            SoundFX.PlaySingle(Sfx.DroneShot, 0.7f); // the volley is already a burst
            SpriteRenderer flash = SpriteAt(transform, "Shot Flash", GameSprites.SoftCircle(), p.x, p.y, 0.35f, new Color(1f, 1f, 0.8f, 0.9f), 40);
            Destroy(flash.gameObject, 0.05f);
            yield return new WaitForSeconds(0.08f);
        }
        Panic(target.BodyCenter.x - transform.position.x, target);
        SoundFX.Play(Sfx.DroneHit);
        OnTrapHitNPC(target);
    }

    // ---------------- Faces ----------------

    private void ShowFace(int index, Kind kind)
    {
        for (int i = faceRoot.childCount - 1; i >= 0; i--) Destroy(faceRoot.GetChild(i).gameObject);
        layerOrder = 48;
        stamp.text = "";
        subjectLabel.text = $"SUBJECT {index + 1:00}/{Faces:00}";
        timerFill.localScale = Vector3.one;

        bool human = kind == Kind.Human || kind == Kind.HumanWithDifference;
        bool monsterSkin = kind == Kind.Monster && Random.value < 0.6f;
        Color skin = monsterSkin ? MonsterSkin[Random.Range(0, MonsterSkin.Length)] : HumanSkin[Random.Range(0, HumanSkin.Length)];
        bool pointy = kind == Kind.NearHumanMonster ? Random.value < 0.5f : kind == Kind.Monster && Random.value < 0.4f;
        string eyes = Random.value < 0.5f ? "eyes_a" : "eyes_b";
        string mouth = Random.value < 0.5f ? "mouth_a" : "mouth_b";
        List<string> extras = new List<string>();

        if (kind == Kind.HumanWithDifference) extras.Add(Differences[Random.Range(0, Differences.Length)]);
        if (kind == Kind.NearHumanMonster && !pointy) eyes = "eyes_slit";
        if (kind == Kind.Monster)
        {
            // Always something obvious (besides the skin, which may be human-colored)
            switch (Random.Range(0, 5))
            {
                case 0: eyes = "eyes_three"; break;
                case 1: eyes = "eyes_cyclops"; break;
                case 2: extras.Add("horns"); break;
                case 3: extras.Add("antennae"); break;
                default: mouth = "mouth_fangs"; extras.Add("scales"); break;
            }
            if (Random.value < 0.4f) mouth = "mouth_fangs";
        }

        tell = FindTell(kind, pointy, eyes, mouth, extras);

        // Back to front
        Layer(pointy ? "face_pointy" : "face_round", skin);
        Layer(pointy ? "face_pointy_line" : "face_round_line", Color.white);
        foreach (string e in extras) if (e == "scales" || e == "diff_birthmark" || e == "diff_vitiligo") Layer(e, Color.white);
        Layer(eyes, Color.white);
        Layer(mouth, Color.white);
        foreach (string e in extras) if (e == "diff_eyepatch" || e == "diff_scar" || e == "diff_glasses" || e == "diff_hearingaid") Layer(e, Color.white);
        if (eyes != "eyes_cyclops" || Random.value < 0.5f)
        {
            // Hair never hides the ears (pointy ones are the only tell) or a hearing aid: long hair
            // covers the sides of the head, so those faces get short hair or the bun
            string[] hair = pointy ? new[] { "hair_a", "hair_c" } : extras.Contains("diff_hearingaid") ? new[] { "hair_a", "hair_c" } : new[] { "hair_a", "hair_b", "hair_c" };
            Layer(hair[Random.Range(0, hair.Length)], HairColors[Random.Range(0, HairColors.Length)]);
        }
        foreach (string e in extras) if (e == "horns" || e == "antennae") Layer(e, Color.white);

        // The AI's guess: right on the easy ones, wrong on the tricky ones (and more often in threat mode)
        bool aiSaysHuman = kind == Kind.Human || kind == Kind.NearHumanMonster;
        if (threatMode && (kind == Kind.Human || kind == Kind.Monster) && Random.value < 0.3f) aiSaysHuman = !aiSaysHuman;
        int confidence = Random.Range(aiSaysHuman == human ? 88 : 61, aiSaysHuman == human ? 99 : 79);
        aiLabel.text = threatMode
            ? (aiSaysHuman ? $"AI: CIVILIAN {confidence}%" : $"AI: THREAT? {confidence}%")
            : (aiSaysHuman ? $"AI: HUMAN {confidence}%" : $"AI: NOT HUMAN? {confidence}%");
        SoundFX.Play(Sfx.ScanPing, 0.6f);
    }

    private static string FindTell(Kind kind, bool pointy, string eyes, string mouth, List<string> extras)
    {
        if (kind == Kind.Human) return "IT WAS HUMAN";
        if (kind == Kind.HumanWithDifference) return "STILL HUMAN";
        if (pointy) return "POINTY EARS";
        if (eyes == "eyes_slit") return "SLIT EYES";
        if (eyes == "eyes_three") return "THREE EYES";
        if (eyes == "eyes_cyclops") return "ONE EYE";
        if (extras.Contains("horns")) return "HORNS";
        if (extras.Contains("antennae")) return "ANTENNAE";
        if (extras.Contains("scales")) return "SCALES";
        return mouth == "mouth_fangs" ? "FANGS" : "NOT HUMAN";
    }

    private void Layer(string key, Color tint)
    {
        SpriteRenderer sr = SpriteAt(faceRoot, key, GameSprites.FacePart(key), 0f, 0f, 1f, tint, layerOrder++);
        sr.transform.localPosition = Vector3.zero;
    }

    // ---------------- Dossier ----------------

    private void BuildDossier()
    {
        dossier = new GameObject("Dossier").transform;
        dossier.SetParent(transform, false);
        Rect fv = Rect.MinMaxRect(visible.xMin - 3f, visible.yMin - 6f, visible.xMax + 3f, visible.yMax + 3f);
        Box(dossier, "Back", fv.center.x, fv.center.y, fv.width, fv.height, new Color(0.07f, 0.08f, 0.1f), 45);

        float cx = visible.center.x, top = visible.yMax - 1.95f; // below the score
        subjectLabel = Text("SUBJECT 01/03", cx, top, 3.6f, 0.3f, Color.white);
        Text("BIOMETRIC SCREENING // " + AssignedDrone.Tag, cx, top - 0.38f, 4.2f, 0.16f, new Color(1f, 1f, 1f, 0.55f));

        // The face card: paper with a photo frame
        float cardY = top - 2.75f, size = 3.0f;
        Box(dossier, "Card", cx, cardY, size + 0.5f, size + 0.5f, Paper, 46);
        Box(dossier, "Photo", cx, cardY, size, size, new Color(0.62f, 0.7f, 0.76f), 47);
        faceRoot = new GameObject("Face").transform;
        faceRoot.SetParent(dossier, false);
        faceRoot.localPosition = new Vector3(cx, cardY - 0.05f, 0f);
        faceRoot.localScale = Vector3.one * (size * 0.95f); // the layers are 1 unit wide

        aiLabel = Text("AI: ...", cx, cardY - size / 2f - 0.5f, 3.6f, 0.24f, new Color(0.45f, 0.85f, 1f));
        Text("IS THIS HUMAN?", cx, cardY - size / 2f - 1.05f, 3.6f, 0.36f, new Color(1f, 0.86f, 0.1f));

        // Time bar and the three checks
        float barY = cardY - size / 2f - 1.55f, barW = 3.4f;
        Box(dossier, "Timer Back", cx, barY, barW, 0.1f, new Color(1f, 1f, 1f, 0.2f), 46);
        Transform pivot = new GameObject("Timer").transform; // scales from the left
        pivot.SetParent(dossier, false);
        pivot.localPosition = new Vector3(cx - barW / 2f, barY, 0f);
        Box(pivot, "Fill", barW / 2f, 0f, barW, 0.1f, new Color(1f, 0.86f, 0.1f), 47);
        timerFill = pivot;
        for (int i = 0; i < Faces; i++)
            pips.Add(Box(dossier, "Check " + (i + 1), cx + (i - 1) * 0.45f, barY - 0.4f, 0.26f, 0.26f, new Color(1f, 1f, 1f, 0.25f), 46));
        Text("PRESS = HUMAN    WAIT = NOT HUMAN", cx, barY - 0.82f, 4f, 0.16f, new Color(1f, 1f, 1f, 0.6f));

        stamp = Text("", cx, cardY, size + 0.6f, 0.5f, Green);
        stamp.sortingOrder = 60;
        stamp.transform.localRotation = Quaternion.Euler(0f, 0f, 10f);
    }

    private void Stamp(string text, Color color)
    {
        stamp.text = text;
        stamp.color = color;
    }

    private TextMeshPro Text(string text, float x, float y, float w, float h, Color color)
    {
        TextMeshPro t = WorldText(dossier, text, x, y, w, h, color, 50);
        t.outlineColor = Color.black;
        t.outlineWidth = 0.25f;
        return t;
    }
}
