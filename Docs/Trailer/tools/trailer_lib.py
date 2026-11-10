"""Helpers for building the Agent TR8N1 trailer: title cards, chiptune music, sfx mix."""
import os, wave, subprocess
import numpy as np
from PIL import Image, ImageDraw, ImageFont

W, H, FPS, SR = 1920, 1080, 30, 44100
FONTS = "/System/Library/Fonts/Supplemental/"
RED, YELLOW, GREEN, WHITE = (255, 64, 64), (255, 210, 63), (90, 255, 140), (235, 235, 235)


def font(name, size):
    return ImageFont.truetype(FONTS + name, size)


# ---------------- title cards ----------------

def scanlines(img, alpha=40):
    over = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    for y in range(0, H, 4):
        d.line([(0, y), (W, y)], fill=(0, 0, 0, alpha), width=2)
    return Image.alpha_composite(img.convert("RGBA"), over).convert("RGB")


def cctv_frame(img, t, label="CAM 04  //  TRAINING DIVISION"):
    d = ImageDraw.Draw(img)
    mono = font("Courier New Bold.ttf", 34)
    if int(t * 2) % 2 == 0:
        d.ellipse([70, 64, 104, 98], fill=(230, 30, 30))
    d.text((122, 60), "REC", font=mono, fill=WHITE)
    d.text((W - 70, 60), label, font=mono, fill=WHITE, anchor="ra")
    secs = 3 * 3600 + 14 * 60 + 7 + t
    d.text((70, H - 100), "2026-11-02  %02d:%02d:%05.2f" % (secs // 3600, secs // 60 % 60, secs % 60), font=mono, fill=WHITE)
    for (x, y, sx, sy) in [(40, 40, 1, 1), (W - 40, 40, -1, 1), (40, H - 40, 1, -1), (W - 40, H - 40, -1, -1)]:
        d.line([(x, y), (x + 60 * sx, y)], fill=WHITE, width=4)
        d.line([(x, y), (x, y + 60 * sy)], fill=WHITE, width=4)


def text_card(folder, lines, seconds, color=GREEN, typing=True, size=96, sub=None):
    """Monospace text typed out over a CCTV frame. lines: list of strings."""
    os.makedirs(folder, exist_ok=True)
    f = font("Courier New Bold.ttf", size)
    fsub = font("Courier New Bold.ttf", 44)
    total_chars = sum(len(l) for l in lines)
    n = int(seconds * FPS)
    for i in range(n):
        t = i / FPS
        shown = total_chars if not typing else int(min(1.0, t / max(0.4, seconds * 0.45)) * total_chars)
        img = Image.new("RGB", (W, H), (6, 8, 10))
        d = ImageDraw.Draw(img)
        y = H / 2 - (len(lines) - 1) * size * 0.65
        left = shown
        for line in lines:
            part = line[:max(0, left)]
            left -= len(line)
            d.text((W / 2, y), part, font=f, fill=color, anchor="mm")
            if 0 <= left < len(line) or (part == line and left <= 0 and int(t * 3) % 2 == 0 and line is lines[-1]):
                pass
            y += size * 1.3
        if sub and shown >= total_chars:
            d.text((W / 2, y + 30), sub, font=fsub, fill=WHITE, anchor="mm")
        cctv_frame(img, t)
        img = scanlines(img)
        img.save(os.path.join(folder, "f%06d.jpg" % i), quality=92)
    return n


def logo_card(folder, seconds, tagline, footer_lines):
    """Final title: AGENT TR8N1 logo with a punch-in."""
    os.makedirs(folder, exist_ok=True)
    big = font("Impact.ttf", 300)
    tag = font("Courier New Bold.ttf", 52)
    foot = font("Courier New Bold.ttf", 40)
    n = int(seconds * FPS)
    for i in range(n):
        t = i / FPS
        img = Image.new("RGB", (W, H), (8, 6, 12))
        layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        wa, wb, gap = d.textlength("AGENT", font=big), d.textlength("TR8N1", font=big), 70
        x0 = (W - (wa + gap + wb)) / 2
        d.text((x0, 400), "AGENT", font=big, fill=RED, anchor="lm")
        d.text((x0 + wa + gap, 400), "TR8N1", font=big, fill=YELLOW, anchor="lm")
        k = min(1.0, t / 0.25)
        scale = 1.6 - 0.6 * (1 - (1 - k) ** 3)
        lw, lh = int(W * scale), int(H * scale)
        scaled = layer.resize((lw, lh), Image.LANCZOS)
        img.paste(scaled, ((W - lw) // 2, (H - lh) // 2), scaled)
        d2 = ImageDraw.Draw(img)
        if t > 0.6:
            d2.text((W / 2, 620), tagline, font=tag, fill=WHITE, anchor="mm")
        if t > 1.3:
            for j, line in enumerate(footer_lines):
                d2.text((W / 2, 800 + j * 62), line, font=foot, fill=GREEN if j == 0 else (170, 170, 170), anchor="mm")
        if t < 0.12:  # white flash on the hit
            img = Image.blend(img, Image.new("RGB", (W, H), (255, 255, 255)), 1 - t / 0.12)
        img = scanlines(img, 30)
        img.save(os.path.join(folder, "f%06d.jpg" % i), quality=92)
    return n


# ---------------- video ----------------

def encode_sequence(folder, start, count, out, pattern="f%06d.jpg", fade_in=0, fade_out=0):
    vf = ["scale=%d:%d:flags=lanczos" % (W, H), "format=yuv420p"]
    if fade_in:
        vf.append("fade=t=in:st=0:d=%.2f" % fade_in)
    if fade_out:
        vf.append("fade=t=out:st=%.2f:d=%.2f" % (count / FPS - fade_out, fade_out))
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(FPS), "-start_number", str(start),
                    "-i", os.path.join(folder, pattern), "-frames:v", str(count), "-vf", ",".join(vf),
                    "-c:v", "libx264", "-preset", "medium", "-crf", "17", "-r", str(FPS), out], check=True)


def concat(parts, out):
    lst = out + ".txt"
    with open(lst, "w") as fh:
        for p in parts:
            fh.write("file '%s'\n" % p)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", lst, "-c", "copy", out], check=True)


# ---------------- audio ----------------

def load_wav(path):
    with wave.open(path) as w:
        data = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768
    return data


def save_wav(path, data):
    data = np.clip(data, -1, 1)
    stereo = np.repeat((data * 32767).astype(np.int16)[:, None], 2, axis=1) if data.ndim == 1 else (data * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(stereo.tobytes())


def note(n):  # MIDI -> Hz
    return 440.0 * 2 ** ((n - 69) / 12)


def env(length, attack=0.005, decay=None):
    t = np.arange(length) / SR
    e = np.minimum(1, t / attack)
    if decay:
        e *= np.exp(-t / decay)
    return e


def square(freq, length, duty=0.5):
    t = np.arange(length) / SR
    return np.where((t * freq) % 1 < duty, 1.0, -1.0)


def kick(length):
    t = np.arange(length) / SR
    f = 45 + 110 * np.exp(-t * 30)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9)


def snare(length, rng):
    t = np.arange(length) / SR
    return (rng.uniform(-1, 1, length) * 0.7 + np.sin(2 * np.pi * 190 * t) * 0.4) * np.exp(-t * 18)


def hat(length, rng):
    t = np.arange(length) / SR
    n = rng.uniform(-1, 1, length)
    n = np.diff(n, prepend=0)  # crude high-pass
    return n * np.exp(-t * 60) * 0.5


def music(total_seconds, sections, bpm=124):
    """sections: list of (start_s, end_s, level) with level in {'intro','drive','tense','silent'}.
    A minor chiptune: kick/snare/hat, square bass on 8ths, pulse arpeggio lead."""
    rng = np.random.default_rng(7)
    out = np.zeros(int((total_seconds + 3) * SR), dtype=np.float32)
    beat = 60 / bpm
    step = beat / 2  # 8th notes
    prog = [(57, [69, 72, 76]), (53, [65, 69, 72]), (48, [67, 72, 76]), (55, [67, 71, 74])]  # Am F C G
    lead_motif = [0, 2, 1, 2, 0, 2, 1, 0]

    def level_at(t):
        for s, e, lv in sections:
            if s <= t < e:
                return lv
        return "silent"

    i = 0
    while i * step < total_seconds:
        t0 = i * step
        lv = level_at(t0)
        pos = int(t0 * SR)
        bar = int(i // 8)
        root, chord = prog[bar % 4]
        slen = int(step * SR)
        if lv != "silent":
            # bass
            bn = root - 12 + (12 if i % 2 else 0)
            b = square(note(bn), slen, 0.5) * env(slen, 0.003, step * 0.6) * 0.16
            if lv == "tense":
                b *= 0.7
            out[pos:pos + slen] += b[:len(out) - pos]
            # hats
            if lv in ("drive", "intro"):
                hl = int(0.08 * SR)
                out[pos:pos + hl] += hat(hl, rng)[:len(out) - pos] * (0.25 if lv == "intro" else 0.35)
        if lv in ("drive", "tense"):
            if i % 2 == 0:
                kl = int(0.35 * SR)
                if lv == "drive" or i % 4 == 0:
                    out[pos:pos + kl] += kick(kl)[:len(out) - pos] * 0.55
            if lv == "drive" and i % 4 == 2:
                sl = int(0.2 * SR)
                out[pos:pos + sl] += snare(sl, rng)[:len(out) - pos] * 0.3
        if lv == "drive":
            # 16th arpeggio lead
            for half in range(2):
                ln = chord[lead_motif[(i * 2 + half) % 8] % 3] + (12 if bar % 8 >= 4 else 0)
                ll = int(step / 2 * SR)
                p2 = pos + half * ll
                l = square(note(ln), ll, 0.25) * env(ll, 0.002, step * 0.3) * 0.07
                out[p2:p2 + ll] += l[:max(0, len(out) - p2)]
        if lv == "tense":
            # high pulsing note, suspense
            ll = slen
            l = square(note(chord[0] + 12), ll, 0.125) * env(ll, 0.002, step * 0.25) * 0.05
            out[pos:pos + ll] += l[:len(out) - pos]
        i += 1
    return out


def stinger(length_s=3.0):
    """Big final hit: low boom + A minor chord ringing out."""
    n = int(length_s * SR)
    t = np.arange(n) / SR
    s = kick(n) * 0.9
    for m in (45, 57, 60, 64, 69):
        s += square(note(m), n, 0.5) * np.exp(-t * 1.6) * 0.06
    rng = np.random.default_rng(3)
    s += rng.uniform(-1, 1, n) * np.exp(-t * 6) * 0.25
    return s.astype(np.float32)


def place(buf, clip, at_s, gain=1.0):
    p = int(at_s * SR)
    if p >= len(buf):
        return
    end = min(len(buf), p + len(clip))
    buf[p:end] += clip[:end - p] * gain


# ---------------- propaganda poster ----------------

CREAM, NAVY, PRED = (238, 226, 196), (22, 38, 84), (178, 28, 38)
DRONE = [
    "..RRRRRR......RRRRRR..",
    ".....XX........XX.....",
    ".....XXXXXXXXXXXX.....",
    "...XXBBBBBBBBBBBBXX...",
    "..XBBBBBBBBBBBBBBBBX..",
    "..XBBBBBWWWWWWBBBBBX..",
    "..XBBBBWKKLLKKWBBBBX..",
    "..XBBBBBWWWWWWBBBBBX..",
    "...XXBBBBBBBBBBBBXX...",
    ".....XXXXXXXXXXXX.....",
    "......X........X......",
    ".....XX........XX.....",
]
DRONE_COLORS = {"X": (20, 20, 28), "B": (90, 98, 115), "R": (150, 150, 160), "W": (220, 220, 225),
                "K": (20, 20, 28), "L": (230, 30, 30)}


def draw_star(d, cx, cy, r, color):
    import math
    pts = []
    for k in range(10):
        a = -math.pi / 2 + k * math.pi / 5
        rr = r if k % 2 == 0 else r * 0.42
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    d.polygon(pts, fill=color)


def poster_card(folder, seconds, lines, sub, drone=True):
    """Old recruitment-poster look: cream paper, navy/red, stars, a big pixel drone staring at you."""
    os.makedirs(folder, exist_ok=True)
    big = font("Impact.ttf", 170)
    fsub = font("Courier New Bold.ttf", 46)
    rng = np.random.default_rng(5)
    grain = (rng.normal(0, 9, (H, W, 1))).astype(np.int16)
    n = int(seconds * FPS)
    for i in range(n):
        t = i / FPS
        img = Image.new("RGB", (W, H), CREAM)
        d = ImageDraw.Draw(img)
        d.rectangle([30, 30, W - 30, H - 30], outline=NAVY, width=14)
        d.rectangle([58, 58, W - 58, H - 58], outline=PRED, width=6)
        for k in range(9):
            draw_star(d, 200 + k * 190, 130, 30, PRED if k % 2 else NAVY)
        text_x = W * 0.62 if drone else W / 2
        if drone:
            px = 26
            ox, oy = 150, 370
            bob = int(np.sin(t * 5) * 8)
            for r, row in enumerate(DRONE):
                for c, ch in enumerate(row):
                    if ch in DRONE_COLORS:
                        col = DRONE_COLORS[ch]
                        if ch == "L" and int(t * 3) % 2:
                            col = (120, 10, 10)
                        if ch == "R":  # spinning rotors
                            if (c + i) % 3 == 0:
                                continue
                        d.rectangle([ox + c * px, oy + r * px + bob, ox + (c + 1) * px - 1, oy + (r + 1) * px - 1 + bob], fill=col)
        k = min(1.0, t / 0.35)
        y = H / 2 - (len(lines) - 1) * 95 + 20
        for j, line in enumerate(lines):
            if t < j * 0.25:
                continue
            col = PRED if j == len(lines) - 1 else NAVY
            d.text((text_x, y + j * 190), line, font=big, fill=col, anchor="mm")
        if sub and t > 0.25 * len(lines):
            d.text((text_x, H - 150), sub, font=fsub, fill=NAVY, anchor="mm")
        arr = np.clip(np.asarray(img).astype(np.int16) + grain, 0, 255).astype(np.uint8)
        img = Image.fromarray(arr)
        if t < 0.1:
            img = Image.blend(img, Image.new("RGB", (W, H), (255, 255, 255)), 1 - t / 0.1)
        img.save(os.path.join(folder, "f%06d.jpg" % i), quality=92)
    return n


# ---------------- patriotic march ----------------

MARCH_MELODY = [67, 67, 72, 72, 76, 76, 72, 0,
                69, 69, 72, 77, 76, 74, 72, 0,
                71, 71, 74, 79, 77, 76, 74, 0,
                76, 0, 74, 0, 72, 72, 72, 0]
MARCH_CHORDS = [(48, [60, 64, 67]), (53, [60, 65, 69]), (55, [59, 62, 67]), (48, [60, 64, 67])]


def march(total_seconds, sections, bpm=116):
    """sections: (start, end, level) with level in march / march_lite / drone / silent."""
    rng = np.random.default_rng(9)
    out = np.zeros(int((total_seconds + 3) * SR), dtype=np.float32)
    step = 60 / bpm / 2

    def level_at(t):
        for s, e, lv in sections:
            if s <= t < e:
                return lv
        return "silent"

    i = 0
    while i * step < total_seconds:
        t0 = i * step
        lv = level_at(t0)
        pos = int(t0 * SR)
        slen = int(step * SR)
        bar = (i // 8) % 4
        root, chord = MARCH_CHORDS[bar]
        beat8 = i % 8

        def add(sig, at=pos):
            end = min(len(out), at + len(sig))
            if end > at:
                out[at:end] += sig[:end - at]

        if lv in ("march", "march_lite"):
            # oom-pah: bass on the beat, chord on the off-beat
            if beat8 % 2 == 0:
                bn = root - 12 if beat8 % 4 == 0 else root - 5
                add(square(note(bn), slen, 0.5) * env(slen, 0.003, step * 0.7) * 0.18)
            else:
                for m in chord:
                    add(square(note(m), slen, 0.25) * env(slen, 0.002, step * 0.35) * 0.035)
            # snare march pattern + roll at the end of every 4 bars
            sl = int(0.12 * SR)
            if (i // 8) % 4 == 3 and beat8 >= 4:
                for r in range(4):
                    add(snare(sl, rng) * 0.18, pos + r * slen // 4)
            elif beat8 in (2, 6) or (lv == "march" and beat8 in (3, 7)):
                add(snare(sl, rng) * 0.22)
            if beat8 % 4 == 0:
                add(kick(int(0.3 * SR)) * 0.45)
        if lv == "march":
            m = MARCH_MELODY[i % len(MARCH_MELODY)]
            if m:
                # "trumpet": two detuned pulses with a little vibrato
                tt = np.arange(slen) / SR
                f = note(m) * (1 + 0.004 * np.sin(2 * np.pi * 6 * tt))
                ph = np.cumsum(f) / SR
                tr = (np.where(ph % 1 < 0.3, 1.0, -1.0) + np.where((ph * 1.003) % 1 < 0.5, 1.0, -1.0)) * 0.5
                add(tr * env(slen, 0.01, step * 1.5) * 0.09)
        if lv == "drone":
            tt = np.arange(slen) / SR + t0
            add((np.sin(2 * np.pi * 55 * tt) * 0.12 + np.sin(2 * np.pi * 55.6 * tt) * 0.1).astype(np.float32))
        i += 1
    return out
