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
    private BlendMode mode;
    private int startScore, fullScore;

    public void Setup(AudioClip happyClip, AudioClip terrorClip, float volume, BlendMode blend, int terrorStartScore, int terrorFullScore, float fadeSeconds)
    {
        Instance = this;
        masterVolume = volume;
        mode = blend;
        startScore = terrorStartScore;
        fullScore = Mathf.Max(terrorStartScore + 1, terrorFullScore);
        fadeSpeed = 1f / Mathf.Max(0.05f, fadeSeconds);

        happy = NewSource(happyClip);
        terror = NewSource(terrorClip);
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
        SetIntensity(k, instant: false);
    }

    // 0 = all happy, 1 = all terror
    public void SetIntensity(float k, bool instant)
    {
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
        float step = fadeSpeed * Time.deltaTime;
        happy.volume = Mathf.MoveTowards(happy.volume, happyTarget * masterVolume, step);
        terror.volume = Mathf.MoveTowards(terror.volume, terrorTarget * masterVolume, step);
    }
}
