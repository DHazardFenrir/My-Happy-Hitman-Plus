using UnityEngine;

// Background music. The happy track is the default; as the score goes up the terror track takes over.
// Two ways to do it (try both in the Inspector, on the GameManager):
//  - Crossfade: happy fades out while terror fades in, between terrorStartScore and terrorFullScore.
//  - Layer: happy keeps playing and terror is mixed in on top, getting louder with the score.
// Both tracks play all the time (looping) and only their volumes change, so switching never restarts them.
public class MusicPlayer : MonoBehaviour
{
    public enum BlendMode { Crossfade, Layer }

    public static MusicPlayer Instance;

    private AudioSource happy, terror;
    private float happyTarget = 1f, terrorTarget;
    private float masterVolume = 0.6f;
    private float fadeSpeed = 0.5f;
    private float currentFadeSpeed = 0.5f; // this fade's speed (a slower one after the credits)
    private BlendMode mode;
    private int startScore, fullScore;

    // The mix changes in steps of this many points (200 = 100/0, 80/20, 60/40... every two wins). 0 = smooth
    public int StepScore { get; set; }

    // terrorSpeed: the terror track plays at this fixed speed (below 1 = slower and lower, heavier)
    public void Setup(AudioClip happyClip, AudioClip terrorClip, float volume, BlendMode blend, int terrorStartScore, int terrorFullScore, float fadeSeconds, float terrorSpeed = 1f)
    {
        Instance = this;
        masterVolume = volume;
        mode = blend;
        startScore = terrorStartScore;
        fullScore = Mathf.Max(terrorStartScore + 1, terrorFullScore);
        fadeSpeed = 1f / Mathf.Max(0.05f, fadeSeconds);

        happy = NewSource(happyClip);
        terror = NewSource(terrorClip);
        terror.pitch = terrorSpeed; // set once: it never changes during the game
        SetIntensity(0f, instant: true);
        if (happy.clip != null) happy.Play();
        if (terror.clip != null) terror.Play();
    }

    private AudioSource NewSource(AudioClip clip)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.volume = 0f;
        return s;
    }

    // Called whenever the score changes
    public void OnScoreChanged(int score)
    {
        float k = Mathf.InverseLerp(startScore, fullScore, score);
        if (StepScore > 0)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt((fullScore - startScore) / (float)StepScore));
            k = Mathf.Floor(k * steps + 0.001f) / steps;
        }
        SetIntensity(k, instant: false);
    }

    // 0 = all happy, 1 = all terror. fadeSeconds overrides how long this change takes
    public void SetIntensity(float k, bool instant, float fadeSeconds = 0f)
    {
        // Same target (e.g. the menu resets the score to 0 during the slow return): keep that fade going
        if (!instant && fadeSeconds <= 0f && Mathf.Approximately(k, terrorTarget)) return;
        currentFadeSpeed = fadeSeconds > 0f ? 1f / fadeSeconds : fadeSpeed;
        terrorTarget = k;
        happyTarget = mode == BlendMode.Crossfade ? 1f - k : 1f;
        if (instant && happy != null)
        {
            happy.volume = happyTarget * masterVolume;
            terror.volume = terrorTarget * masterVolume;
        }
    }

    // Lowers or restores everything (e.g. during the "real life" death)
    public void SetMasterVolume(float volume) => masterVolume = volume;

    void Update()
    {
        if (happy == null) return;
        float step = currentFadeSpeed * Time.deltaTime;
        happy.volume = Mathf.MoveTowards(happy.volume, happyTarget * masterVolume, step);
        terror.volume = Mathf.MoveTowards(terror.volume, terrorTarget * masterVolume, step);
    }
}
