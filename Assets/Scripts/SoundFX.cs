using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Plays the game's sound effects. Recorded sounds in Assets/Resources/Sounds/<folder> come first
// (see Recorded): a folder with several files plays one at random each time, so screams and shots
// don't repeat; the piano impact plays all its files at once (the hit + the strings ringing); the
// drone fires a short burst. Anything without recordings is generated in code (RuntimeSounds); to
// replace one with a .wav exported from bfxr.net, assign it in this component's overrides.
public class SoundFX : MonoBehaviour
{
    [Serializable]
    public struct Override
    {
        public Sfx sound;
        public AudioClip clip;
    }

    public static SoundFX Instance;

    [Range(0f, 1f)] public float volume = 0.7f;
    [Tooltip("Optional bfxr .wav files that replace the generated sounds")]
    public Override[] overrides;
    [Tooltip("Shots in each drone burst, and the seconds between them")]
    public int droneBurstShots = 3;
    public float droneBurstGap = 0.09f;

    // Which folder of Assets/Resources/Sounds each sound comes from
    private static readonly Dictionary<Sfx, string> Recorded = new Dictionary<Sfx, string>
    {
        { Sfx.CarHit, "Car Brake" },
        { Sfx.CarCrash, "Car Crash" },
        { Sfx.DroneShot, "Drone Shot" },
        { Sfx.PianoWhistle, "Piano Fall" },
        { Sfx.PianoCrush, "Piano Impact" },
        { Sfx.PianoCrash, "Piano Impact" },
        { Sfx.SewerFall, "Fall" },
        { Sfx.ElevatorFall, "Fall" },
        { Sfx.Scream, "Scream" },
        { Sfx.PhoneCrack, "Phone Crack" },
    };
    private static readonly HashSet<Sfx> AllFilesAtOnce = new HashSet<Sfx> { Sfx.PianoCrush, Sfx.PianoCrash };
    private const float ScreamGap = 0.15f; // several deaths at once don't stack into noise

    private readonly Dictionary<string, AudioClip[]> loaded = new Dictionary<string, AudioClip[]>();
    private readonly Dictionary<string, int> lastPick = new Dictionary<string, int>();
    private float lastScream = -1f;
    private AudioSource source;

    void Awake()
    {
        Instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
    }

    public static void Play(Sfx sound, float volumeScale = 1f)
    {
        TrailerCapture.Log("sfx " + sound);
        if (Instance == null || Instance.source == null) return;
        if (sound == Sfx.DroneShot && Instance.droneBurstShots > 1)
            Instance.StartCoroutine(Instance.Burst(volumeScale));
        else
            Instance.PlayNow(sound, volumeScale);
    }

    // A single sound even where Play makes a sequence (e.g. one drone shot of a longer volley)
    public static void PlaySingle(Sfx sound, float volumeScale = 1f)
    {
        TrailerCapture.Log("sfx " + sound);
        if (Instance != null && Instance.source != null) Instance.PlayNow(sound, volumeScale);
    }

    private void PlayNow(Sfx sound, float volumeScale)
    {
        if (sound == Sfx.Scream)
        {
            if (Time.unscaledTime - lastScream < ScreamGap) return;
            lastScream = Time.unscaledTime;
        }

        float v = volume * volumeScale;
        AudioClip[] files = RecordedFiles(sound);
        if (files.Length == 0) source.PlayOneShot(ClipFor(sound), v);
        else if (AllFilesAtOnce.Contains(sound)) foreach (AudioClip clip in files) source.PlayOneShot(clip, v);
        else source.PlayOneShot(files[RandomIndex(Recorded[sound], files.Length)], v);
    }

    private IEnumerator Burst(float volumeScale)
    {
        for (int i = 0; i < droneBurstShots; i++)
        {
            PlayNow(Sfx.DroneShot, volumeScale);
            yield return new WaitForSeconds(droneBurstGap);
        }
    }

    private AudioClip[] RecordedFiles(Sfx sound)
    {
        if (!Recorded.TryGetValue(sound, out string folder)) return Array.Empty<AudioClip>();
        if (!loaded.TryGetValue(folder, out AudioClip[] files))
            loaded[folder] = files = Resources.LoadAll<AudioClip>("Sounds/" + folder);
        return files;
    }

    // Random, but never the same file twice in a row
    private int RandomIndex(string folder, int count)
    {
        lastPick.TryGetValue(folder, out int last);
        int pick = UnityEngine.Random.Range(0, count);
        if (count > 1 && pick == last) pick = (pick + 1) % count;
        lastPick[folder] = pick;
        return pick;
    }

    private AudioClip ClipFor(Sfx sound)
    {
        if (overrides != null)
            foreach (Override o in overrides)
                if (o.sound == sound && o.clip != null) return o.clip;
        return RuntimeSounds.Clip(sound);
    }
}
