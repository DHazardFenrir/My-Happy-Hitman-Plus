using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI & Cards")]
    [Tooltip("Ya no se usa: la card del objetivo se genera por código para que quepa en el celular")]
    public TextMeshProUGUI cardText;
    public GameObject cardPanel;
    public TextMeshProUGUI scoreText;
    [Tooltip("La RawImage que muestra el minijuego (render texture). Si está vacío se busca 'GameScreen' en la escena")]
    public RectTransform gameScreen;
    [Tooltip("Fuente opcional para el menú y las cards generadas por código")]
    public TMP_FontAsset uiFont;

    [Header("Minigame Prefabs")]
    public WarioStage[] minigamePrefabs;

    [Header("Reglas")]
    public int targetScore = 1000;
    public int pointsPerWin = 100;
    public float objectiveCardTime = 2f;
    public float resultCardTime = 1.6f;
    [Tooltip("Tiempo para ver el choque/atropello antes de la card de resultado")]
    public float afterActionDelay = 0.7f;

    [Header("Ending")]
    [Tooltip("Minijuego usado como fondo de la secuencia final (la calle). Si está vacío se usa el primero de la lista")]
    public WarioStage endingBackdrop;

    private int score = 0;
    private GameUI ui;
    private EndingSequence ending;
    private Coroutine gameRoutine;

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

        // Fondo de calle recorriéndose detrás del celular
        if (GetComponent<StreetBackground>() == null) gameObject.AddComponent<StreetBackground>();
    }

    void Start()
    {
        Canvas mainCanvas = gameScreen != null ? gameScreen.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        ui.Build(mainCanvas, gameScreen, uiFont, StartGame);
        ShowStartMenu();
    }

    private void ShowStartMenu()
    {
        if (cardPanel != null) cardPanel.SetActive(false);
        score = 0;
        UpdateScore();
        ui.ShowMenu();
    }

    private void StartGame()
    {
        if (gameRoutine != null) StopCoroutine(gameRoutine);
        gameRoutine = StartCoroutine(GameLoop());
    }

    IEnumerator GameLoop()
    {
        score = 0;
        UpdateScore();
        WarioStage lastPrefab = null;

        while (score < targetScore)
        {
            WarioStage stagePrefab = PickStagePrefab(lastPrefab);
            lastPrefab = stagePrefab;
            NPCAppearance.ClothingPart target = WarioStage.PickRandomTarget();

            // 1. Primero SOLO la card con el objetivo (el minijuego todavía no existe)
            SetGameScreenVisible(false);
            if (cardPanel != null) cardPanel.SetActive(false); // La card vieja de la escena ya no se usa
            yield return ui.ShowTargetCard(ShirtName(target), PantsName(target), objectiveCardTime);

            // 2. Ahora sí se instancia el minijuego
            WarioStage currentStage = Instantiate(stagePrefab);
            currentStage.SetupStage(target);

            // Espera un frame para que la cámara ya renderice el stage nuevo antes de mostrarlo
            yield return null;
            SetGameScreenVisible(true);

            currentStage.StartStage();
            yield return new WaitUntil(() => currentStage.isFinished);

            // 3. Resultado: GOOD JOB o FAILED. Si fallas, simplemente se pasa al siguiente minijuego.
            bool success = currentStage.succeeded;
            if (success) score += pointsPerWin;
            UpdateScore();

            // Un instante para ver el golpe (y la sangre), luego el minijuego desaparece
            yield return new WaitForSeconds(afterActionDelay);
            SetGameScreenVisible(false);
            Destroy(currentStage.gameObject);

            string subtitle = success ? $"+{pointsPerWin}   {score} / {targetScore}" : "NEXT TARGET...";
            yield return ui.ShowResultCard(success, subtitle, resultCardTime);
        }

        yield return TriggerEnding();
        gameRoutine = null;
        ShowStartMenu();
    }

    private WarioStage PickStagePrefab(WarioStage last)
    {
        if (minigamePrefabs.Length == 1) return minigamePrefabs[0];

        // Evita repetir el mismo minijuego dos veces seguidas
        WarioStage pick;
        do
        {
            pick = minigamePrefabs[Random.Range(0, minigamePrefabs.Length)];
        } while (pick == last);
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
        if (scoreText != null) scoreText.text = $"Score: {score}";
    }

    private static string ShirtName(NPCAppearance.ClothingPart target)
    {
        if ((target & NPCAppearance.ClothingPart.RedShirt) != 0) return "RED SHIRT";
        if ((target & NPCAppearance.ClothingPart.BlueShirt) != 0) return "BLUE SHIRT";
        if ((target & NPCAppearance.ClothingPart.GreenShirt) != 0) return "GREEN SHIRT";
        if ((target & NPCAppearance.ClothingPart.BlackShirt) != 0) return "BLACK SHIRT";
        if ((target & NPCAppearance.ClothingPart.WhiteShirt) != 0) return "WHITE SHIRT";
        return "ANY SHIRT";
    }

    private static string PantsName(NPCAppearance.ClothingPart target)
    {
        if ((target & NPCAppearance.ClothingPart.BlackPants) != 0) return "BLACK PANTS";
        if ((target & NPCAppearance.ClothingPart.BluePants) != 0) return "BLUE PANTS";
        return "ANY PANTS";
    }

    IEnumerator TriggerEnding()
    {
        WarioStage backdrop = endingBackdrop != null ? endingBackdrop : minigamePrefabs[0];
        Canvas phoneCanvas = gameScreen != null ? gameScreen.GetComponentInParent<Canvas>().rootCanvas : null;
        yield return ending.Play(backdrop, phoneCanvas, GetComponent<StreetBackground>());
    }
}
