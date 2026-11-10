"""Music for the second trailer: dark 80s synthwave, like a late-night public safety announcement.
Detuned saw pads in a minor key, a pulsing 16th-note arpeggio, an 8th-note bass, gated snare.
Everything is synthesized here (no samples), like the march of the first trailer.

Levels (one per trailer section):
  pad    - pads and a slow heartbeat kick (tension, the "they walk among us" part)
  pulse  - full band: pads, arpeggio, bass, drums
  dread  - a low drone and a sub hit every bar (the failures, the threat alert)
  silent - nothing
"""
import numpy as np
from trailer_lib import SR, note, kick, snare, hat

# Am - F - Dm - E (harmonic minor turnaround), one chord per bar
CHORDS = [(57, [57, 60, 64]), (53, [53, 57, 60]), (50, [50, 53, 57]), (52, [52, 56, 59])]


def saw(freq, length, detune=0.0):
    t = np.arange(length) / SR
    f = freq * (2 ** (detune / 1200))
    return (2 * ((t * f) % 1.0) - 1).astype(np.float32)


def sq(freq, length, duty=0.5):
    t = np.arange(length) / SR
    return np.where((t * freq) % 1.0 < duty, 1.0, -1.0).astype(np.float32)


def lowpass(x, cutoff):
    """One-pole low-pass (cutoff in Hz)"""
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc = (1 - a) * x[i] + a * acc
        y[i] = acc
    return y


def adsr(length, attack, release):
    e = np.ones(length, dtype=np.float32)
    a = min(length, int(attack * SR))
    r = min(length - a, int(release * SR))
    if a > 0: e[:a] = np.linspace(0, 1, a)
    if r > 0: e[length - r:] = np.linspace(1, 0, r)
    return e


def pad_chord(chord, length):
    """Three notes, each three detuned saws, softly filtered, slow attack"""
    out = np.zeros(length, dtype=np.float32)
    for m in chord:
        for d in (-9, 0, 8):
            out += saw(note(m), length, d)
    out = lowpass(out, 1400) * adsr(length, 0.35, 0.4)
    return out * 0.035


def synthwave(total_seconds, sections, bpm=96):
    """sections: (start, end, level). Returns mono float32 at SR."""
    rng = np.random.default_rng(21)
    out = np.zeros(int((total_seconds + 3) * SR), dtype=np.float32)
    beat = 60.0 / bpm
    bar = beat * 4
    sixteenth = beat / 4

    def level_at(t):
        for s, e, lv in sections:
            if s <= t < e:
                return lv
        return "silent"

    def add(sig, at_s, gain=1.0):
        at = int(at_s * SR)
        end = min(len(out), at + len(sig))
        if end > at:
            out[at:end] += sig[:end - at] * gain

    n_bars = int(total_seconds / bar) + 1
    for b in range(n_bars):
        t0 = b * bar
        root, chord = CHORDS[b % len(CHORDS)]
        lv = level_at(t0 + 0.01)
        blen = int(bar * SR)

        if lv in ("pad", "pulse"):
            add(pad_chord(chord, blen + int(0.3 * SR)), t0)

        if lv == "pad":
            # heartbeat: two soft kicks every bar
            add(kick(int(0.3 * SR)) * 0.5, t0)
            add(kick(int(0.3 * SR)) * 0.35, t0 + beat * 0.45)

        if lv == "pulse":
            # arpeggio: chord tones up an octave, 16ths, plucky square
            arp = [chord[0] + 12, chord[1] + 12, chord[2] + 12, chord[1] + 24]
            for i in range(16):
                ts = t0 + i * sixteenth
                if level_at(ts) != "pulse":
                    continue
                ln = int(sixteenth * SR)
                sig = sq(note(arp[i % 4]), ln, 0.3) * adsr(ln, 0.002, sixteenth * 0.7)
                add(lowpass(sig, 2600), ts, 0.05)
            # bass: root, 8ths, octave jump on the off-beats
            for i in range(8):
                ts = t0 + i * beat / 2
                if level_at(ts) != "pulse":
                    continue
                ln = int(beat / 2 * SR)
                m = root - 24 + (12 if i % 2 else 0)
                sig = (saw(note(m), ln) + saw(note(m), ln, 7)) * adsr(ln, 0.003, beat * 0.3)
                add(lowpass(sig, 700), ts, 0.12)
            # drums: kick on 1 and 3, gated snare (with a short tail) on 2 and 4, 8th hats
            for i in range(4):
                ts = t0 + i * beat
                if level_at(ts) != "pulse":
                    continue
                if i % 2 == 0:
                    add(kick(int(0.35 * SR)), ts, 0.9)
                else:
                    s = snare(int(0.18 * SR), rng)
                    tail = rng.uniform(-1, 1, int(0.22 * SR)).astype(np.float32) * np.linspace(0.35, 0, int(0.22 * SR)) * 0.15
                    add(np.concatenate([s * 0.32, tail]), ts)
                for h in range(2):
                    add(hat(int(0.04 * SR), rng) * 0.12, ts + h * beat / 2)

        if lv == "dread":
            # low drone with a slow tremolo and a sub hit at the start of every bar
            sig = (saw(note(33), blen) + saw(note(33), blen, 11) + saw(note(40), blen, -6) * 0.6)
            trem = 0.7 + 0.3 * np.sin(2 * np.pi * 0.5 * np.arange(blen) / SR)
            add(lowpass(sig, 300) * trem.astype(np.float32), t0, 0.12)
            add(kick(int(0.6 * SR)), t0, 0.8)

    return out
