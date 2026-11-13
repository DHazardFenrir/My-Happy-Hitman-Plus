using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI & Cards")]
    [Tooltip("No longer used: the target card is generated in code so it fits the phone")]
    public TextMeshProUGUI cardText;
    public GameObject cardPanel;
    public TextMeshProUGUI scoreText;
    [Tooltip("The RawImage that shows the minigame (render texture). If empty, 'GameScreen' is searched in the scene")]
    public RectTransform gameScreen;
    [Tooltip("Optional font for the menu and the cards generated in code")]
    public TMP_FontAsset uiFont;

    [Header("Minigame Prefabs")]
    public WarioStage[] minigamePrefabs;

    [Header("Rules")]
    public int targetScore = 1000;
    public int pointsPerWin = 100;
    public float objectiveCardTime = 2.75f;
    public float resultCardTime = 1.6f;
    [Tooltip("How long the assigned / destroyed / certified drone card stays")]
    public float droneCardTime = 2.2f;
    [Tooltip("Seconds the one-word action card (SHOOT!, DROP!...) shows right before each minigame")]
    public float actionCardTime = 0.8f;
    [Tooltip("Seconds the HIT ZONE shows at the start of the trap minigames, while they already play")]
    public float headsUpSeconds = 1.2f;
    [Tooltip("Cone that marks the edges of the HIT ZONE (tripleT/misc/cone)")]
    public Sprite hitZoneCone;
    [Tooltip("Time to see the hit before the result card")]
    public float afterActionDelay = 0.7f;

    [Header("Fails")]
    [Tooltip("Show the retro ad every this many fails")]
    public int adEveryFails = 5;
    [Tooltip("Length of the first ad; each following ad lasts adExtraSeconds longer")]
    public float adFirstDuration = 3f;
    public float adExtraSeconds = 2f;
    [Tooltip("With more fails than this, the trap happens to you \"in real life\"")]
    public int irlDeathAfterFails = 30;
    [Tooltip("Last chance before that death (once per drone): taps needed to fill the fingerprint, and the seconds you get")]
    public int fingerprintTaps = 14;
    public float fingerprintSeconds = 3.5f;

    [Header("Music")]
    [Tooltip("Default track (menu and start of the run). Auto-filled in the editor from Assets/OST (a clip with \"Happy\" in its name)")]
    public AudioClip happyMusic;
    [Tooltip("Track that takes over as the score goes up. Auto-filled in the editor from Assets/OST (a clip with \"Terror\" in its name)")]
    public AudioClip terrorMusic;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Tooltip("Crossfade: happy turns into terror. Layer: terror is added on top of happy")]
    public MusicPlayer.BlendMode musicBlend = MusicPlayer.BlendMode.Crossfade;
    [Tooltip("Score where the terror track starts coming in (both tracks always play; terror starts at volume 0)")]
    public int terrorStartScore = 0;
    [Tooltip("Score where the terror track is fully in and the happy one is gone (the target score)")]
    public int terrorFullScore = 1000;
    [Tooltip("The mix changes in steps of this many points: 200 = happy 100/terror 0, then 80/20, 60/40... every two wins, all terror at the last one. 0 = smooth")]
    public int musicStepScore = 200;
    [Tooltip("Seconds each volume change takes")]
    public float musicFadeSeconds = 2.5f;
    [Tooltip("Seconds the slow crossfade back to the happy track takes after the credits")]
    public float musicReturnSeconds = 10f;
    [Tooltip("Speed of the terror track (fixed for the whole game). Below 1 it plays slower and lower")]
    [Range(0.5f, 1f)] public float terrorMusicSpeed = 0.9f;

    [Header("Ending")]
    [Tooltip("Minigame used as the backdrop of the ending (the street). If empty, the first one in the list is used")]
    public WarioStage endingBackdrop;

    private int score = 0;
    public int Score => score; // minigames that get harder as you progress read it
    private int failCount = 0;
    private bool fingerprintUsed; // the last chance, once per drone
    private GameUI ui;
    private IrlDeathSequence irlDeath;
    private WalkingBackground walking;
    private EndingSequence ending;
    private Coroutine gameRoutine;
    private MusicPlayer music;

    void Awake()
    {
        Instance = this;

        if (gameScreen == null)
        {
            GameObject found = GameObject.Find("GameScreen");
            if (found != null) gameScreen = found.GetComponent<RectTransform>();
        }

        ui = gameObject.AddComponent<GameUI>();
        ending = GetComponent<EndingSequence>();
        if (ending == null) ending = gameObject.AddComponent<EndingSequence>();

        // Scrolling walking background behind the phone
        walking = GetComponent<WalkingBackground>();
        if (walking == null) walking = gameObject.AddComponent<WalkingBackground>();

        irlDeath = gameObject.AddComponent<IrlDeathSequence>();

        if (GetComponent<SoundFX>() == null) gameObject.AddComponent<SoundFX>();
#if UNITY_EDITOR
        AutoAssignMusic();
#endif
        music = gameObject.AddComponent<MusicPlayer>();
        if (Autopilot.FailsForIrl >= 0) irlDeathAfterFails = Autopilot.FailsForIrl;
        TrailerCapture.StartIfRequested(gameObject);
    }

    void Start()
    {
        Canvas mainCanvas = gameScreen != null ? gameScreen.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        ui.Build(mainCanvas, gameScreen, uiFont, StartGame);
        ui.StyleScore(scoreText);
        music.Setup(happyMusic, terrorMusic, musicVolume, musicBlend, terrorStartScore, terrorFullScore, musicFadeSeconds, terrorMusicSpeed);
        music.StepScore = musicStepScore;
        ShowStartMenu();
    }

    private void ShowStartMenu()
    {
        if (cardPanel != null) cardPanel.SetActive(false);
        score = 0;
        UpdateScore();
        walking.TransitionTo(WalkTheme.Street);
        ui.ShowMenu();
        TrailerCapture.Log("menu");
        if (Autopilot.Enabled) StartCoroutine(AutopilotPressPlay());
    }

    private IEnumerator AutopilotPressPlay()
    {
        yield return new WaitForSeconds(2.5f);
        ui.PressPlayFromCode();
    }

    private void StartGame()
    {
        if (gameRoutine != null) StopCoroutine(gameRoutine);
        gameRoutine = StartCoroutine(GameLoop());
    }

    IEnumerator GameLoop()
    {
        score = 0;
        failCount = 0;
        UpdateScore();
        WarioStage lastPrefab = null;
        stageBag.Clear(); // every run starts with all the minigames still to come

        // A new drone to train
        SetGameScreenVisible(false);
        AssignedDrone.AssignNew();
        fingerprintUsed = false;
        yield return ui.ShowDroneCard(GameUI.DroneCard.Assigned, droneCardTime);

        while (score < targetScore)
        {
            WarioStage stagePrefab = PickStagePrefab(lastPrefab);
            lastPrefab = stagePrefab;
            StageTarget target = stagePrefab.PickTarget();
            TrailerCapture.Log($"card {stagePrefab.GetType().Name} {target.line1} / {target.line2}");

            // While you read the card you start walking into this minigame's place
            walking.TransitionTo(stagePrefab.Theme);

            // 1. First ONLY the target card (the minigame doesn't exist yet)
            SetGameScreenVisible(false);
            if (cardPanel != null) cardPanel.SetActive(false); // The old card in the scene is no longer used
            yield return ui.ShowTargetCard(target, objectiveCardTime);
            yield return stagePrefab.BeforeStage(ui, target); // e.g. "preparing the RC car"
            yield return ui.ShowActionCard(stagePrefab.ActionVerb, actionCardTime); // SHOOT! / DROP! ...

            // 2. Now the minigame is instantiated
            WarioStage currentStage = Instantiate(stagePrefab);
            currentStage.SetupStage(target);

            // Wait a frame so the camera renders the new stage before showing it
            yield return null;
            SetGameScreenVisible(true);

            TrailerCapture.Log($"stage {stagePrefab.GetType().Name}");
            currentStage.StartStage();
            currentStage.StartCoroutine(currentStage.HeadsUp(headsUpSeconds, hitZoneCone)); // over the running minigame
            yield return new WaitUntil(() => currentStage.isFinished);

            // 3. Result: GOOD JOB or FAILED. Failing just moves on to the next minigame.
            bool success = currentStage.succeeded;
            if (success) score += pointsPerWin;
            else failCount++;
            UpdateScore();
            if (success) ui.PunchScore(scoreText);
            TrailerCapture.Log($"result {(success ? "success" : "fail")} score={score} fails={failCount}");

            // A moment to see the hit (and the blood)
            yield return new WaitForSeconds(afterActionDelay);

            // Too many fails: this minigame's trap happens to you, in real life
            // ...but first, once per drone, a last chance: re-authenticate with your fingerprint
            bool savedByFingerprint = false;
            if (!success && failCount > irlDeathAfterFails && !fingerprintUsed)
            {
                fingerprintUsed = true;
                SetGameScreenVisible(false);
                TrailerCapture.Log("fingerprint");
                yield return ui.ShowFingerprintCard(fingerprintTaps, fingerprintSeconds);
                savedByFingerprint = ui.FingerprintPassed;
                TrailerCapture.Log(savedByFingerprint ? "fingerprint passed" : "fingerprint failed");
                SetGameScreenVisible(true);
            }

            if (!success && failCount > irlDeathAfterFails && !savedByFingerprint)
            {
                TrailerCapture.Log("irl start");
                music.SetMasterVolume(musicVolume * 0.25f); // the alarm and the impact take over
                yield return irlDeath.Play(currentStage, ui, walking, failCount);
                music.SetMasterVolume(musicVolume);
                TrailerCapture.Log("irl end");
                SetGameScreenVisible(false);
                Destroy(currentStage.gameObject);

                // Your drone pays for it... and you get a new one
                yield return ui.ShowDroneCard(GameUI.DroneCard.Destroyed, droneCardTime + 0.6f);
                AssignedDrone.AssignNew();
                fingerprintUsed = false;
                yield return ui.ShowDroneCard(GameUI.DroneCard.Assigned, droneCardTime);
                score = 0;          // RETRY: start the job over
                failCount = 0;
                UpdateScore();
                continue;
            }

            // The minigame disappears and the sign shows up
            SetGameScreenVisible(false);
            Destroy(currentStage.gameObject);

            // After a confirmed fingerprint the FAILED sign would read as if the print failed: the
            // fingerprint card already said how it went, so it's skipped
            if (!savedByFingerprint)
            {
                string subtitle = success ? $"+{pointsPerWin}   {score} / {targetScore}" : "NEXT TARGET...";
                TrailerCapture.Log("resultcard");
                SoundFX.Play(success ? Sfx.Success : Sfx.Fail);
                yield return ui.ShowResultCard(success, subtitle, resultCardTime);
            }

            // Failing a lot: retro ad, longer each time (5 fails = 3 s, 10 = 5 s, ...)
            if (!success && adEveryFails > 0 && failCount % adEveryFails == 0)
            {
                int adLevel = failCount / adEveryFails;
                TrailerCapture.Log("ad");
                yield return ui.ShowAd(adLevel, adFirstDuration + adExtraSeconds * (adLevel - 1));
            }
        }

        AssignedDrone.Certify();
        yield return ui.ShowDroneCard(GameUI.DroneCard.Certified, droneCardTime + 0.4f);
        TrailerCapture.Log("ending start");
        music.SetIntensity(1f, instant: false); // the ending is all terror
        yield return TriggerEnding();
        music.SetIntensity(0f, instant: false, musicReturnSeconds); // back to the happy track, slowly
        TrailerCapture.Finish();
        gameRoutine = null;
        ShowStartMenu();
    }

    // Shuffle bag: every minigame comes up once, in random order, before any repeats
    // (and never the same one twice in a row across bags)
    private readonly System.Collections.Generic.List<WarioStage> stageBag = new System.Collections.Generic.List<WarioStage>();

    private WarioStage PickStagePrefab(WarioStage last)
    {
        if (minigamePrefabs.Length == 1) return minigamePrefabs[0];

        if (stageBag.Count == 0)
        {
            stageBag.AddRange(minigamePrefabs);
            for (int i = stageBag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (stageBag[i], stageBag[j]) = (stageBag[j], stageBag[i]);
            }
            if (stageBag[0] == last) (stageBag[0], stageBag[stageBag.Count - 1]) = (stageBag[stageBag.Count - 1], stageBag[0]);
        }
        WarioStage pick = stageBag[0];
        stageBag.RemoveAt(0);
        return pick;
    }

    private void SetGameScreenVisible(bool visible)
    {
        if (gameScreen == null) return;
        Graphic graphic = gameScreen.GetComponent<Graphic>();
        if (graphic != null) graphic.enabled = visible;
        else gameScreen.gameObject.SetActive(visible);
    }

    private void UpdateScore()
    {
        if (scoreText != null) scoreText.text = $"SCORE {score}";
        if (music != null) music.OnScoreChanged(score);
    }

    public static string ShirtName(NPCAppearance.ClothingPart target)
    {
        if ((target & NPCAppearance.ClothingPart.RedShirt) != 0) return "RED SHIRT";
        if ((target & NPCAppearance.ClothingPart.BlueShirt) != 0) return "BLUE SHIRT";
        if ((target & NPCAppearance.ClothingPart.GreenShirt) != 0) return "GREEN SHIRT";
        if ((target & NPCAppearance.ClothingPart.BlackShirt) != 0) return "BLACK SHIRT";
        if ((target & NPCAppearance.ClothingPart.WhiteShirt) != 0) return "WHITE SHIRT";
        return "ANY SHIRT";
    }

    public static string PantsName(NPCAppearance.ClothingPart target)
    {
        if ((target & NPCAppearance.ClothingPart.BlackPants) != 0) return "BLACK PANTS";
        if ((target & NPCAppearance.ClothingPart.BluePants) != 0) return "BLUE PANTS";
        return "ANY PANTS";
    }

    IEnumerator TriggerEnding()
    {
        WarioStage backdrop = endingBackdrop != null ? endingBackdrop : minigamePrefabs[0];
        Canvas phoneCanvas = gameScreen != null ? gameScreen.GetComponentInParent<Canvas>().rootCanvas : null;
        yield return ending.Play(backdrop, phoneCanvas, walking);
        walking.SnapTo(WalkTheme.Street);
    }

#if UNITY_EDITOR
    // Finds the composer's test tracks so they work without dragging them into the Inspector.
    // Save the scene afterwards so the references are kept in builds.
    void OnValidate()
    {
        if (happyMusic == null || terrorMusic == null) UnityEditor.EditorApplication.delayCall += AutoAssignMusic;
    }

    private void AutoAssignMusic()
    {
        if (this == null) return;
        bool changed = false;
        if (happyMusic == null) { happyMusic = FindClip("Happy"); changed |= happyMusic != null; }
        if (terrorMusic == null) { terrorMusic = FindClip("Terror"); changed |= terrorMusic != null; }
        if (changed && !Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
    }

    // The official soundtrack in Assets/OST first, then any clip in the project
    private static AudioClip FindClip(string word)
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets(word + " t:AudioClip", new[] { "Assets/OST" });
        if (guids.Length == 0) guids = UnityEditor.AssetDatabase.FindAssets(word + " t:AudioClip");
        foreach (string guid in guids)
        {
            AudioClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null) return clip;
        }
        return null;
    }
#endif
}
