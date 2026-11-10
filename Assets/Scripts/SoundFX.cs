using System;
using UnityEngine;

// Plays the game's sound effects. Every effect is generated in code (RuntimeSounds);
// to replace one with a .wav exported from bfxr.net, add this component to the GameManager
// in the scene and assign the clip in its slot.
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
        Instance.source.PlayOneShot(Instance.ClipFor(sound), Instance.volume * volumeScale);
    }

    private AudioClip ClipFor(Sfx sound)
    {
        if (overrides != null)
            foreach (Override o in overrides)
                if (o.sound == sound && o.clip != null) return o.clip;
        return RuntimeSounds.Clip(sound);
    }
}
