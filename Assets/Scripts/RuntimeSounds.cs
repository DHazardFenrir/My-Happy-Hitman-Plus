using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum Sfx
{
    CarStart, CarHit, CarCrash,
    PianoWhistle, PianoCrush, PianoCrash,
    DroneShot, DroneHit,
    SewerOpen, SewerFall,
    ElevatorLever, ElevatorFall,
    Success, Fail, MenuSelect,
    CarHorn, GlassBreak, Explosion,
    TargetAlert,
    DogBark, DogBite, GateOpen,
    ScanPing, ScanLock, FingerTap, FingerOk,
    Scream, PhoneCrack // recorded only (Assets/Resources/Sounds): silent without their files
}

// Retro sound effects generated in code, with the same synth bfxr/sfxr use
// (square/saw/sine/noise oscillator, envelope, frequency slide, vibrato, arpeggio, filters, phaser).
// The parameters use bfxr's 0..1 ranges, so a sound designed on bfxr.net can be copied here by hand.
// To use a .wav exported from bfxr instead, assign it in the SoundFX component.
public static class RuntimeSounds
{
    public const int SampleRate = 44100;

    public enum Wave { Square, Saw, Sine, Noise }

    public class Params
    {
        public Wave wave = Wave.Square;
        public float attack, sustain = 0.3f, punch, decay = 0.4f;
        public float baseFreq = 0.3f, freqLimit, freqRamp, freqDeltaRamp;
        public float vibStrength, vibSpeed;
        public float arpMod, arpSpeed;
        public float duty, dutyRamp;
        public float repeatSpeed;
        public float phaserOffset, phaserRamp;
        public float lpfFreq = 1f, lpfRamp, lpfResonance;
        public float hpfFreq, hpfRamp;
        public float volume = 1f;
    }

    private static readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();

    public static AudioClip Clip(Sfx id)
    {
        if (clips.TryGetValue(id, out AudioClip clip) && clip != null) return clip;
        float[] data = Samples(id);
        clip = AudioClip.Create("sfx_" + id, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        clips[id] = clip;
        return clip;
    }

    // Each effect is one or more synth layers mixed together
    public static float[] Samples(Sfx id)
    {
        switch (id)
        {
            case Sfx.CarStart: // Engine cranks and revs up
                return Mix(
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.08f, sustain = 0.18f, decay = 0.1f, repeatSpeed = 0.62f, lpfFreq = 0.35f, volume = 0.7f }, 1),
                    Synth(new Params { wave = Wave.Saw, baseFreq = 0.11f, freqRamp = 0.09f, vibStrength = 0.55f, vibSpeed = 0.72f, attack = 0.05f, sustain = 0.42f, punch = 0.3f, decay = 0.35f, lpfFreq = 0.45f, lpfResonance = 0.4f }, 2));
            case Sfx.CarHit: // Wet thud
                return Mix(
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.18f, freqRamp = -0.25f, sustain = 0.08f, punch = 0.7f, decay = 0.28f }, 3),
                    Synth(new Params { wave = Wave.Square, baseFreq = 0.22f, freqRamp = -0.45f, duty = 0.4f, sustain = 0.05f, decay = 0.2f, volume = 0.6f }, 4));
            case Sfx.CarCrash: // Crashes into the wall: metal crunch
                return Mix(
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.1f, freqRamp = -0.08f, sustain = 0.25f, punch = 0.55f, decay = 0.55f, lpfFreq = 0.8f }, 5),
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.4f, sustain = 0.06f, decay = 0.4f, repeatSpeed = 0.55f, hpfFreq = 0.2f, volume = 0.5f }, 6),
                    Synth(new Params { wave = Wave.Square, baseFreq = 0.5f, freqRamp = -0.2f, vibStrength = 0.4f, vibSpeed = 0.8f, sustain = 0.1f, decay = 0.4f, volume = 0.35f }, 7));
            case Sfx.PianoWhistle: // Cartoon falling whistle
                return Synth(new Params { wave = Wave.Sine, baseFreq = 0.62f, freqRamp = -0.12f, vibStrength = 0.06f, vibSpeed = 0.5f, attack = 0.08f, sustain = 0.4f, decay = 0.12f, volume = 0.8f }, 8);
            case Sfx.PianoCrush: // Lands on a monito: boom + out-of-tune chord
                return Mix(
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.07f, sustain = 0.15f, punch = 0.8f, decay = 0.45f }, 9),
                    Synth(new Params { wave = Wave.Square, baseFreq = 0.32f, arpMod = 0.35f, arpSpeed = 0.6f, duty = 0.2f, sustain = 0.2f, decay = 0.65f, volume = 0.45f }, 10),
                    Synth(new Params { wave = Wave.Saw, baseFreq = 0.24f, arpMod = -0.3f, arpSpeed = 0.55f, sustain = 0.2f, decay = 0.65f, volume = 0.35f }, 11));
            case Sfx.PianoCrash: // Hits the ground: strings snap
                return Mix(
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.12f, sustain = 0.12f, punch = 0.6f, decay = 0.4f }, 12),
                    Synth(new Params { wave = Wave.Saw, baseFreq = 0.45f, freqRamp = -0.3f, vibStrength = 0.3f, vibSpeed = 0.7f, sustain = 0.15f, decay = 0.5f, phaserOffset = 0.2f, volume = 0.5f }, 13));
            case Sfx.DroneShot: // Laser
                return Synth(new Params { wave = Wave.Square, baseFreq = 0.72f, freqLimit = 0.2f, freqRamp = -0.35f, duty = 0.35f, dutyRamp = 0.2f, sustain = 0.12f, punch = 0.25f, decay = 0.22f, hpfFreq = 0.05f, volume = 0.8f }, 14);
            case Sfx.DroneHit: // The shot lands
                return Synth(new Params { wave = Wave.Noise, baseFreq = 0.3f, freqRamp = -0.3f, sustain = 0.05f, punch = 0.6f, decay = 0.25f }, 15);
            case Sfx.SewerOpen: // Metal lid clank
                return Mix(
                    Synth(new Params { wave = Wave.Square, baseFreq = 0.55f, freqRamp = -0.05f, duty = 0.6f, sustain = 0.03f, punch = 0.7f, decay = 0.35f, phaserOffset = 0.3f, phaserRamp = -0.1f, volume = 0.6f }, 16),
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.45f, sustain = 0.03f, decay = 0.15f, volume = 0.5f }, 17));
            case Sfx.SewerFall: // Falls in: slide whistle down + splash
                return Mix(
                    Synth(new Params { wave = Wave.Sine, baseFreq = 0.5f, freqRamp = -0.3f, sustain = 0.2f, decay = 0.1f, volume = 0.6f }, 18),
                    Delay(Synth(new Params { wave = Wave.Noise, baseFreq = 0.25f, freqRamp = -0.1f, sustain = 0.1f, punch = 0.3f, decay = 0.4f, lpfFreq = 0.5f }, 19), 0.25f));
            case Sfx.ElevatorLever: // Clunk + motor whine going up
                return Mix(
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.2f, sustain = 0.02f, punch = 0.5f, decay = 0.12f }, 20),
                    Synth(new Params { wave = Wave.Saw, baseFreq = 0.15f, freqRamp = 0.3f, sustain = 0.35f, decay = 0.2f, lpfFreq = 0.5f, volume = 0.5f }, 21));
            case Sfx.ElevatorFall: // Long falling whistle + far away thud
                return Mix(
                    Synth(new Params { wave = Wave.Sine, baseFreq = 0.55f, freqRamp = -0.2f, sustain = 0.35f, decay = 0.1f, volume = 0.6f }, 22),
                    Delay(Synth(new Params { wave = Wave.Noise, baseFreq = 0.06f, sustain = 0.05f, punch = 0.4f, decay = 0.3f, volume = 0.6f }, 23), 0.55f));
            case Sfx.Success: // Coin-style jingle
                return Synth(new Params { wave = Wave.Square, baseFreq = 0.42f, arpMod = 0.45f, arpSpeed = 0.62f, duty = 0.3f, sustain = 0.12f, punch = 0.45f, decay = 0.4f, volume = 0.6f }, 24);
            case Sfx.Fail: // Sad descending buzz
                return Synth(new Params { wave = Wave.Saw, baseFreq = 0.35f, freqRamp = -0.15f, vibStrength = 0.25f, vibSpeed = 0.55f, sustain = 0.35f, decay = 0.3f, lpfFreq = 0.6f, volume = 0.6f }, 25);
            case Sfx.CarHorn: // HONK... HOOONK (two tones at once, like a real horn)
            {
                float[] Honk(float sustain) => Mix(
                    Synth(new Params { wave = Wave.Square, baseFreq = 0.30f, duty = 0.3f, sustain = sustain, decay = 0.08f, lpfFreq = 0.5f }, 27),
                    Synth(new Params { wave = Wave.Square, baseFreq = 0.36f, duty = 0.3f, sustain = sustain, decay = 0.08f, lpfFreq = 0.5f }, 28));
                return Mix(Honk(0.22f), Delay(Honk(0.45f), 0.3f));
            }
            case Sfx.GlassBreak: // Crash + tinkling shards
                return Mix(
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.65f, sustain = 0.05f, punch = 0.8f, decay = 0.45f, hpfFreq = 0.3f }, 29),
                    Synth(new Params { wave = Wave.Square, baseFreq = 0.8f, arpMod = -0.4f, arpSpeed = 0.7f, repeatSpeed = 0.62f, duty = 0.6f, sustain = 0.15f, decay = 0.35f, volume = 0.35f }, 30));
            case Sfx.Explosion:
                return Mix(
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.1f, freqRamp = -0.08f, sustain = 0.3f, punch = 0.7f, decay = 0.65f }, 31),
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.35f, sustain = 0.05f, punch = 0.5f, decay = 0.3f, volume = 0.6f }, 32));
            case Sfx.TargetAlert: // "Target acquired": low hit + three rising alarm beeps
                return Mix(
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.1f, sustain = 0.02f, punch = 0.8f, decay = 0.25f, lpfFreq = 0.4f, volume = 0.7f }, 33),
                    Synth(new Params { wave = Wave.Square, baseFreq = 0.45f, duty = 0.5f, sustain = 0.07f, decay = 0.03f, volume = 0.55f }, 34),
                    Delay(Synth(new Params { wave = Wave.Square, baseFreq = 0.45f, duty = 0.5f, sustain = 0.07f, decay = 0.03f, volume = 0.55f }, 35), 0.12f),
                    Delay(Synth(new Params { wave = Wave.Square, baseFreq = 0.6f, duty = 0.5f, sustain = 0.16f, decay = 0.12f, vibStrength = 0.15f, vibSpeed = 0.6f, volume = 0.6f }, 36), 0.24f));
            case Sfx.DogBark: // "WOOF WOOF": low square bark with a breathy noise attack, twice
            {
                // (a bright layer on top so it cuts through the music and small speakers)
                float[] Woof(int seed) => Mix(
                    Synth(new Params { wave = Wave.Square, baseFreq = 0.33f, freqRamp = -0.38f, duty = 0.35f, sustain = 0.06f, punch = 0.7f, decay = 0.11f, lpfFreq = 0.6f, lpfResonance = 0.35f }, seed),
                    Synth(new Params { wave = Wave.Saw, baseFreq = 0.48f, freqRamp = -0.45f, sustain = 0.04f, punch = 0.5f, decay = 0.07f, lpfFreq = 0.75f, volume = 0.6f }, seed + 2),
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.4f, sustain = 0.03f, decay = 0.08f, lpfFreq = 0.6f, volume = 0.6f }, seed + 1));
                return Mix(Woof(37), Delay(Woof(39), 0.17f));
            }
            case Sfx.DogBite: // Growl (wobbly low saw) + crunchy snap
                return Mix(
                    Synth(new Params { wave = Wave.Saw, baseFreq = 0.12f, vibStrength = 0.6f, vibSpeed = 0.75f, attack = 0.02f, sustain = 0.35f, decay = 0.15f, lpfFreq = 0.4f, lpfResonance = 0.5f, volume = 0.7f }, 41),
                    Synth(new Params { wave = Wave.Noise, baseFreq = 0.5f, sustain = 0.03f, punch = 0.8f, decay = 0.15f, repeatSpeed = 0.6f, hpfFreq = 0.15f, volume = 0.6f }, 42));
            case Sfx.GateOpen: // "Unlocked" two-tone beep + motor whir + clank
                return Mix(
                    Synth(new Params { wave = Wave.Square, baseFreq = 0.55f, arpMod = 0.3f, arpSpeed = 0.55f, duty = 0.5f, sustain = 0.12f, decay = 0.05f, volume = 0.45f }, 43),
                    Delay(Synth(new Params { wave = Wave.Saw, baseFreq = 0.18f, freqRamp = 0.25f, sustain = 0.3f, decay = 0.1f, lpfFreq = 0.5f, volume = 0.5f }, 44), 0.15f),
                    Delay(Synth(new Params { wave = Wave.Noise, baseFreq = 0.25f, sustain = 0.02f, punch = 0.6f, decay = 0.15f, volume = 0.6f }, 45), 0.5f));
            case Sfx.ScanPing: // Sonar blip while the scan ring grows
                return Synth(new Params { wave = Wave.Sine, baseFreq = 0.62f, freqRamp = -0.08f, sustain = 0.03f, decay = 0.18f, volume = 0.45f }, 46);
            case Sfx.ScanLock: // "Device found": two quick rising beeps
                return Mix(
                    Synth(new Params { wave = Wave.Square, baseFreq = 0.5f, duty = 0.5f, sustain = 0.05f, decay = 0.03f, volume = 0.4f }, 47),
                    Delay(Synth(new Params { wave = Wave.Square, baseFreq = 0.7f, duty = 0.5f, sustain = 0.1f, decay = 0.08f, volume = 0.45f }, 48), 0.08f));
            case Sfx.FingerTap: // Soft click of the fingerprint sensor
                return Synth(new Params { wave = Wave.Noise, baseFreq = 0.6f, sustain = 0.01f, punch = 0.4f, decay = 0.04f, hpfFreq = 0.3f, volume = 0.35f }, 49);
            case Sfx.FingerOk: // Banking-app chime: identity confirmed
                return Mix(
                    Synth(new Params { wave = Wave.Sine, baseFreq = 0.45f, sustain = 0.08f, decay = 0.25f, volume = 0.5f }, 50),
                    Delay(Synth(new Params { wave = Wave.Sine, baseFreq = 0.6f, sustain = 0.12f, decay = 0.4f, volume = 0.5f }, 51), 0.1f));
            case Sfx.MenuSelect:
                return Synth(new Params { wave = Wave.Square, baseFreq = 0.5f, freqRamp = 0.2f, duty = 0.5f, sustain = 0.04f, decay = 0.12f, volume = 0.5f }, 26);
        }
        return new float[1];
    }

    // ---------------- sfxr synth ----------------

    public static float[] Synth(Params p, int seed)
    {
        System.Random rng = new System.Random(seed);
        float Rand() => (float)rng.NextDouble() * 2f - 1f;

        double fperiod = 0, fmaxperiod = 0, fslide = 0, fdslide = 0, arpMod = 0;
        float squareDuty = 0, squareSlide = 0;
        int arpTime = 0, arpLimit = 0;

        void ResetSample()
        {
            fperiod = 100.0 / (p.baseFreq * p.baseFreq + 0.001);
            fmaxperiod = 100.0 / (p.freqLimit * p.freqLimit + 0.001);
            fslide = 1.0 - Math.Pow(p.freqRamp, 3) * 0.01;
            fdslide = -Math.Pow(p.freqDeltaRamp, 3) * 0.000001;
            squareDuty = 0.5f - p.duty * 0.5f;
            squareSlide = -p.dutyRamp * 0.00005f;
            arpMod = p.arpMod >= 0 ? 1.0 - p.arpMod * p.arpMod * 0.9 : 1.0 + p.arpMod * p.arpMod * 10.0;
            arpTime = 0;
            arpLimit = p.arpSpeed >= 1f ? 0 : (int)((1f - p.arpSpeed) * (1f - p.arpSpeed) * 20000 + 32);
        }

        ResetSample();

        float fltp = 0, fltdp = 0, fltphp = 0;
        float fltw = p.lpfFreq * p.lpfFreq * p.lpfFreq * 0.1f;
        float fltwD = 1f + p.lpfRamp * 0.0001f;
        float fltdmp = Mathf.Min(0.8f, 5f / (1f + p.lpfResonance * p.lpfResonance * 20f) * (0.01f + fltw));
        float flthp = p.hpfFreq * p.hpfFreq * 0.1f;
        float flthpD = 1f + p.hpfRamp * 0.0003f;

        float vibPhase = 0, vibSpeed = p.vibSpeed * p.vibSpeed * 0.01f, vibAmp = p.vibStrength * 0.5f;

        int[] envLength =
        {
            Mathf.Max(1, (int)(p.attack * p.attack * 100000f)),
            Mathf.Max(1, (int)(p.sustain * p.sustain * 100000f)),
            Mathf.Max(1, (int)(p.decay * p.decay * 100000f))
        };

        float fphase = p.phaserOffset * p.phaserOffset * 1020f * Mathf.Sign(p.phaserOffset);
        float fdphase = p.phaserRamp * p.phaserRamp * Mathf.Sign(p.phaserRamp);
        int ipp = 0;
        float[] phaserBuffer = new float[1024];

        float[] noise = new float[32];
        for (int i = 0; i < 32; i++) noise[i] = Rand();

        int repTime = 0;
        int repLimit = p.repeatSpeed <= 0f ? 0 : (int)((1f - p.repeatSpeed) * (1f - p.repeatSpeed) * 20000 + 32);

        int envStage = 0, envTime = 0, phase = 0;
        List<float> output = new List<float>(envLength[0] + envLength[1] + envLength[2]);

        while (true)
        {
            if (repLimit != 0 && ++repTime >= repLimit)
            {
                repTime = 0;
                ResetSample();
            }

            if (arpLimit != 0 && ++arpTime >= arpLimit)
            {
                arpLimit = 0;
                fperiod *= arpMod;
            }

            fslide += fdslide;
            fperiod *= fslide;
            if (fperiod > fmaxperiod)
            {
                fperiod = fmaxperiod;
                if (p.freqLimit > 0f) break;
            }

            double rfperiod = fperiod;
            if (vibAmp > 0f)
            {
                vibPhase += vibSpeed;
                rfperiod = fperiod * (1.0 + Math.Sin(vibPhase) * vibAmp);
            }
            int period = Math.Max(8, (int)rfperiod);

            squareDuty = Mathf.Clamp(squareDuty + squareSlide, 0f, 0.5f);

            if (++envTime > envLength[envStage])
            {
                envTime = 0;
                if (++envStage == 3) break;
            }
            float k = (float)envTime / envLength[envStage];
            float envVol = envStage == 0 ? k : envStage == 1 ? 1f + (1f - k) * 2f * p.punch : 1f - k;

            fphase += fdphase;
            int iphase = Mathf.Min(1023, Mathf.Abs((int)fphase));

            if (flthpD != 1f) flthp = Mathf.Clamp(flthp * flthpD, 0.00001f, 0.1f);

            float sum = 0f;
            for (int si = 0; si < 8; si++) // 8x supersampling
            {
                if (++phase >= period)
                {
                    phase %= period;
                    if (p.wave == Wave.Noise)
                        for (int i = 0; i < 32; i++) noise[i] = Rand();
                }

                float fp = (float)phase / period;
                float sample;
                switch (p.wave)
                {
                    case Wave.Square: sample = fp < squareDuty ? 0.5f : -0.5f; break;
                    case Wave.Saw: sample = 1f - fp * 2f; break;
                    case Wave.Sine: sample = Mathf.Sin(fp * 2f * Mathf.PI); break;
                    default: sample = noise[phase * 32 / period]; break;
                }

                // Low-pass
                float pp = fltp;
                fltw = Mathf.Clamp(fltw * fltwD, 0f, 0.1f);
                if (p.lpfFreq < 1f)
                {
                    fltdp += (sample - fltp) * fltw;
                    fltdp -= fltdp * fltdmp;
                }
                else
                {
                    fltp = sample;
                    fltdp = 0f;
                }
                fltp += fltdp;

                // High-pass
                fltphp += fltp - pp;
                fltphp -= fltphp * flthp;
                sample = fltphp;

                // Phaser
                phaserBuffer[ipp & 1023] = sample;
                sample += phaserBuffer[(ipp - iphase + 1024) & 1023];
                ipp = (ipp + 1) & 1023;

                sum += sample * envVol;
            }

            output.Add(sum / 8f);
        }

        // Normalized so every layer has the same loudness before its own volume
        float peak = 0.0001f;
        foreach (float s in output) peak = Mathf.Max(peak, Mathf.Abs(s));
        float gain = 0.9f / peak * p.volume;
        float[] result = output.ToArray();
        for (int i = 0; i < result.Length; i++) result[i] *= gain;
        return result;
    }

    private static float[] Mix(params float[][] layers)
    {
        int length = 0;
        foreach (float[] l in layers) length = Mathf.Max(length, l.Length);
        float[] mix = new float[length];
        foreach (float[] l in layers)
            for (int i = 0; i < l.Length; i++) mix[i] += l[i];

        float peak = 0.0001f;
        foreach (float s in mix) peak = Mathf.Max(peak, Mathf.Abs(s));
        if (peak > 0.95f)
            for (int i = 0; i < mix.Length; i++) mix[i] *= 0.95f / peak;
        return mix;
    }

    private static float[] Delay(float[] samples, float seconds)
    {
        int offset = (int)(seconds * SampleRate);
        float[] delayed = new float[samples.Length + offset];
        Array.Copy(samples, 0, delayed, offset, samples.Length);
        return delayed;
    }

    // Saves every effect as a 16-bit .wav (used to edit the trailer audio)
    public static void ExportAll(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (Sfx id in Enum.GetValues(typeof(Sfx)))
            WriteWav(Path.Combine(folder, id + ".wav"), Samples(id));
    }

    private static void WriteWav(string path, float[] samples)
    {
        using (BinaryWriter w = new BinaryWriter(File.Create(path)))
        {
            int dataSize = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataSize);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(SampleRate);
            w.Write(SampleRate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataSize);
            foreach (float s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }
    }
}
