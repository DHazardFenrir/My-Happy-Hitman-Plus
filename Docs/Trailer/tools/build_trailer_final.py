"""Final Agent TR8N1 trailer ("FINAL BROADCAST"), vertical 1080x1920 for Instagram Reels.
Closes the propaganda campaign in the style of the itch pamphlet (paper, the Colonel, the sunglasses troops):
all 12 missions back to back, the threat alert + real-life death, graduation, and the demo end card.

Edited from two autopilot recordings made at 2880x1620 (so the phone screen is ~1080 wide):
  capA = wins of every minigame + the ending, capB = fails (fingerprint, threat alert, real-life death),
  capC (optional) = extra wins for any minigame capA only failed
The sound effects use Carlos's recordings (Assets/Resources/Sounds) where the game does, otherwise the
synth .wav the capture exported. Music: the chiptune march from trailer_lib.

usage: build_trailer_final.py <folder with capA/ capB/> <out.mp4> [work folder]
Needs ffmpeg, pillow, numpy."""
import os, sys, shutil, subprocess
import numpy as np
from PIL import Image, ImageDraw, ImageOps

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..", "..", "..")
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(ROOT, "Docs", "Store", "tools"))
import trailer_lib as tl
import make_pamphlet_assets as pm

W, H, FPS, SR = 1080, 1920, tl.FPS, tl.SR
tl.W, tl.H = W, H   # encode_sequence scales to these

S, OUT = sys.argv[1], sys.argv[2]
WORK = sys.argv[3] if len(sys.argv) > 3 else os.path.join(S, "work_final")
shutil.rmtree(WORK, ignore_errors=True)
os.makedirs(WORK)

PAPER, INK, RED, GOLD = pm.PAPER, pm.INK, pm.RED, pm.GOLD
IMPACT, PLATE, TYPE, TYPE_B = pm.IMPACT, pm.PLATE, pm.TYPE, pm.TYPE_B

# The phone screen inside a 2880x1620 recording, and where it goes in the vertical frame
PHONE_CROP = (894, 396, 1956, 1620)
GAME_BOX = (0, 330, W, 1590)
# Minigames that only use the top of the phone (the rest of the screen is black)
TOP_ONLY = {"PianoDropStage": (894, 396, 1956, 980), "SewerStage": (894, 396, 1956, 1050)}
# Full-screen moments (real-life death, ending): the whole screen, so their banners aren't cut
WIDE_CROP = (0, 0, 2880, 1620)
WIDE_Y = 520

MISSIONS = [  # stage, action verb (as in the game), tagline, seconds before the hit
    ("PianoDropStage", "DROP!", "Music education, delivered.", 2.4),
    ("LightTrafficStage", "GREEN LIGHT!", "Green means go.", 1.6),
    ("SewerStage", "OPEN!", "Mind the gap.", 1.6),
    ("ElevatorStage", "PULL!", "Going down.", 2.2),
    ("PlazaDroneStage", "SHOOT!", "Smile! The drones keep you safe.", 2.2),
    ("NightDroneStage", "SHOOT!", "The Agency never sleeps.", 2.2),
    ("RcCarStage", "RAM!", "Ground units now available.", 2.8),
    ("DogFenceStage", "RELEASE!", "Community K-9 Program.", 2.4),
    ("CaptchaStage", "LABEL!", "Select all squares with a threat.", 2.6),
    ("SignalScanStage", "SCAN!", "Your phone is talking to us.", 2.4),
    ("SafetyOverrideStage", "MASH!", "Ethics lock? What ethics lock?", 2.6),
    ("HumanCheckStage", "VERIFY!", "Are you human? Prove it.", 2.6),
]
TAIL = 16   # frames after the hit


# ---------------- recordings ----------------
def read_events(cap):
    rows = []
    for line in open(os.path.join(S, cap, "events.txt")):
        fr, ev = line.rstrip("\n").split("\t")
        rows.append((int(fr), ev))
    return rows


def rounds(cap):
    out, cur = [], None
    for fr, ev in read_events(cap):
        if ev.startswith("card "):
            cur = {"stage": ev.split()[1], "card": fr}
        elif ev.startswith("stage ") and cur:
            cur["start"] = fr
        elif ev.startswith("result ") and cur:
            cur["result"], cur["success"] = fr, "success" in ev
        elif ev == "resultcard" and cur:
            cur["resultcard"] = fr
            out.append(cur)
            cur = None
    return out


def first_event(cap, name, after=0):
    return next(fr for fr, ev in read_events(cap) if ev == name and fr >= after)


def win(stage):
    for cap in ("capA", "capC", "capB"):
        for r in rounds(cap):
            if r["stage"] == stage and r["success"]:
                return r, cap
    return None, None


def frame(cap, fr):
    return Image.open(os.path.join(S, cap, "frames", "f%06d.jpg" % fr))


# ---------------- drawing ----------------
PAPER_BG = pm.paper(W, H, 21)


def text_center(d, y, s, f, fill):
    d.text((W / 2, y), s, font=f, fill=fill, anchor="mm")


def fit_font(d, s, maker, size, max_w):
    while size > 20 and d.textlength(s, font=maker(size)) > max_w:
        size -= 4
    return maker(size)


def band(d, text):
    d.rectangle((0, 0, W, 70), fill=INK)
    text_center(d, 36, text, PLATE(24), PAPER)


def mission_frame(idx, stage, verb, tagline, game, stamp_k):
    img = PAPER_BG.copy()
    d = ImageDraw.Draw(img)
    band(d, "DEPARTMENT OF CITY SURVEILLANCE  •  FINAL BROADCAST")
    d.text((48, 104), "MISSION %02d / 12" % (idx + 1), font=PLATE(34), fill=GOLD)
    # Progress: one box per mission, ticked when done
    for i in range(12):
        x = W - 48 - (12 - i) * 40
        d.rectangle((x, 104, x + 28, 132), outline=INK, width=3)
        if i < idx or (i == idx and stamp_k > 0):
            d.line((x + 5, 118, x + 12, 126), fill=RED, width=5)
            d.line((x + 12, 126, x + 27, 104), fill=RED, width=5)
    f = fit_font(d, verb, IMPACT, 150, W - 96)
    d.text((48, 150), verb, font=f, fill=RED)
    box_w, box_h = GAME_BOX[2] - GAME_BOX[0], GAME_BOX[3] - GAME_BOX[1]
    if stage in TOP_ONLY:
        # Only the top of the phone is used: show that part at full width, centered in the box
        g = game.crop(TOP_ONLY[stage])
        g = g.resize((box_w, int(g.height * box_w / g.width)), Image.LANCZOS)
        box = (0, GAME_BOX[1] + (box_h - g.height) // 2)
    else:
        g = game.crop(PHONE_CROP).resize((box_w, box_h), Image.LANCZOS)
        box = GAME_BOX[:2]
    img.paste(g, box)
    d = ImageDraw.Draw(img)
    d.rectangle((box[0], box[1], box[0] + g.width - 1, box[1] + g.height), outline=INK, width=6)
    text_center(d, 1660, tagline, fit_font(d, tagline, TYPE_B, 46, W - 96), INK)
    if stamp_k > 0:
        # "NEUTRALIZED" slams onto the footage when the hit lands
        size = int(70 + 40 * max(0, 1 - stamp_k * 4))
        pm.stamp(img, "NEUTRALIZED", W / 2, min(1420, box[1] + g.height - 60), size, 8)
    return img


def save_frames(folder, frames_iter):
    os.makedirs(folder, exist_ok=True)
    n = 0
    for im in frames_iter:
        im.convert("RGB").save(os.path.join(folder, "f%06d.jpg" % n), quality=92)
        n += 1
    return n


def zoom(img, k):
    """Slow push-in: k from 0 to 1 -> 1.0x to 1.04x"""
    s = 1 + 0.04 * k
    w, h = int(W * s), int(H * s)
    big = img.resize((w, h), Image.LANCZOS)
    return big.crop(((w - W) // 2, (h - H) // 2, (w - W) // 2 + W, (h - H) // 2 + H))


def still(img, seconds):
    n = int(seconds * FPS)
    return (zoom(img, i / max(1, n - 1)) for i in range(n))


def card(lines, seconds, colors=None, sub=None, size=170):
    img = PAPER_BG.copy()
    pm.sunburst(img, W / 2, H / 2, rays=32)
    img = Image.blend(img, PAPER_BG, 0.4)
    d = ImageDraw.Draw(img)
    band(d, "DEPARTMENT OF CITY SURVEILLANCE  •  FINAL BROADCAST")
    colors = colors or [INK] * len(lines)
    total = len(lines) * size * 1.05
    y = H / 2 - total / 2 + size * 0.5
    for line, c in zip(lines, colors):
        text_center(d, y, line, fit_font(d, line, IMPACT, size, W - 100), c)
        y += size * 1.05
    if sub:
        text_center(d, y + 30, sub, PLATE(30), GOLD)
    return still(img, seconds)


def colonel_on_ledge(img, cx, top, bottom, k):
    """The Colonel (scale k) standing on a stone ledge from y=top down to the ground at y=bottom"""
    d = ImageDraw.Draw(img)
    rock = [(cx - 200, bottom), (cx - 180, top + 60), (cx - 130, top + 8), (cx + 150, top), (W + 10, top + 20), (W + 10, bottom)]
    d.polygon(rock, fill=(150, 128, 104), outline=(96, 80, 66), width=5)
    d.line((cx - 130, top + 8, cx + 150, top), fill=(190, 170, 140), width=6)
    boss = ImageOps.mirror(pm.scaled(pm.colonel, k))
    img.paste(boss, (int(cx - boss.width / 2), int(top + 4 - boss.height)), boss)


def bubble(d, x0, y0, x1, y1, text, tail_to):
    d.rounded_rectangle((x0, y0, x1, y1), radius=20, fill=PAPER, outline=INK, width=6)
    tx, ty = tail_to
    d.polygon([(x1 - 8, (y0 + y1) / 2 - 18), (tx, ty), (x1 - 8, (y0 + y1) / 2 + 18)], fill=PAPER)
    d.line((x1 - 4, (y0 + y1) / 2 - 18, tx, ty, x1 - 4, (y0 + y1) / 2 + 18), fill=INK, width=6)
    d.text(((x0 + x1) / 2, (y0 + y1) / 2), text, font=IMPACT(64), fill=RED, anchor="mm")


def marchers(img, t, rows):
    """rows: (scale, ground_y, spacing, count, alpha). Walk frame changes every 3 video frames."""
    walk = pm.SOLDIER_WALK
    step = t // 3
    for k, ground, spacing, count, fade in rows:
        loop = spacing * count
        frames = walk if fade == 0 else [Image.blend(f, Image.new("RGBA", f.size, PAPER + (255,)), fade) for f in walk]
        if fade:
            for f, src in zip(frames, walk):
                f.putalpha(src.getchannel("A"))
        for i in range(count):
            x = int((i * spacing + t * 1.1 * k) % loop - 32 * k)
            fr = pm.scaled(frames[step % len(walk)], k)
            img.paste(fr, (x, ground - fr.height + 1), fr)


def march_scene(seconds, title_lines, footer=None):
    n = int(seconds * FPS)
    bg = PAPER_BG.copy()
    pm.sunburst(bg, 820, 760, rays=32)
    bg = Image.blend(bg, PAPER_BG, 0.35)
    d = ImageDraw.Draw(bg)
    band(d, "DEPARTMENT OF CITY SURVEILLANCE  •  FINAL BROADCAST")
    y = 150
    for line, c in title_lines:
        d.text((56, y), line, font=IMPACT(150), fill=c)
        y += 160
    for t in range(n):
        img = bg.copy()
        marchers(img, t, [(4, 1330, 150, 9, 0.35)])
        colonel_on_ledge(img, 840, 1180, 1562, 7)
        marchers(img, t, [(6, 1560, 230, 6, 0)])
        d = ImageDraw.Draw(img)
        d.line((0, 1562, W, 1562), fill=INK, width=6)
        call = "LEFT!" if (t // 3) % 6 < 3 else "RIGHT!"
        bubble(d, 430, 760, 720, 870, call, (770, 830))
        if footer:
            text_center(d, 1680, footer, fit_font(d, footer, TYPE_B, 44, W - 96), INK)
        yield img


def full_clip(cap, ranges, label=None):
    """Full-screen moments of the game with a caption under each. ranges: [(first frame, end frame, caption)]"""
    for a, b, caption in ranges:
        for fr in range(a, b):
            img = PAPER_BG.copy()
            d = ImageDraw.Draw(img)
            band(d, label or "DEPARTMENT OF CITY SURVEILLANCE  •  FINAL BROADCAST")
            g = frame(cap, fr).crop(WIDE_CROP)
            g = g.resize((W, int(g.height * W / g.width)), Image.LANCZOS)
            img.paste(g, (0, WIDE_Y))
            d.rectangle((0, WIDE_Y, W - 1, WIDE_Y + g.height), outline=INK, width=6)
            if caption:
                text_center(d, WIDE_Y + g.height + 170, caption, fit_font(d, caption, IMPACT, 96, W - 96), INK)
            yield img


def end_card(seconds):
    n = int(seconds * FPS)
    bg = PAPER_BG.copy()
    pm.sunburst(bg, W / 2, 1100, rays=32)
    bg = Image.blend(bg, PAPER_BG, 0.35)
    d = ImageDraw.Draw(bg)
    band(d, "END OF BROADCAST  •  THANK YOU FOR YOUR SERVICE")
    big = IMPACT(190)
    wa, wb = d.textlength("AGENT", font=big), d.textlength("TR8N1", font=big)
    x0 = (W - wa - wb - 40) / 2
    d.text((x0, 140), "AGENT", font=big, fill=INK)
    d.text((x0 + wa + 40, 140), "TR8N1", font=big, fill=RED)
    text_center(d, 400, "DRONE OPERATOR TRAINING PROGRAM", PLATE(36), INK)
    text_center(d, 500, "THE CAMPAIGN IS OVER.", IMPACT(80), INK)
    text_center(d, 590, "THE TRAINING IS NOT.", IMPACT(80), RED)
    d.rectangle((90, 1660, W - 90, 1760), fill=RED)
    text_center(d, 1710, "FREE DEMO  •  NOVEMBER 15  •  ITCH.IO", PLATE(36), PAPER)
    for t in range(n):
        img = bg.copy()
        marchers(img, t, [(4, 1430, 150, 9, 0.35)])
        colonel_on_ledge(img, 840, 1260, 1642, 7)
        marchers(img, t, [(6, 1640, 230, 6, 0)])
        d = ImageDraw.Draw(img)
        d.line((0, 1642, W, 1642), fill=INK, width=6)
        if t > FPS * 0.6:
            bubble(d, 330, 820, 720, 930, "DISMISSED!", (770, 900))
        yield img


# ---------------- timeline ----------------
irl_start = first_event("capB", "irl start")
irl_end = first_event("capB", "irl end", irl_start)
last_fail = [fr for fr, ev in read_events("capB") if ev.startswith("result ") and fr < irl_start][-1]
fingerprint = first_event("capB", "fingerprint", last_fail)
irl_impact = first_event("capB", "irl impact", irl_start)
end_start = first_event("capA", "ending start")
end_drones = first_event("capA", "ending drones")
end_shooters = first_event("capA", "ending shooters")
end_thanks = first_event("capA", "ending thanks")

segments = []   # (name, frame generator, music level, [(frame offset, sfx, cap)])


def clip_sfx(cap, a, b):
    return [(fr - a, ev[4:], cap) for fr, ev in read_events(cap) if ev.startswith("sfx ") and a <= fr < b]


segments.append(("open", card(["THIS IS THE", "FINAL", "BROADCAST."], 2.2, [INK, RED, INK], "OF THE DRONE OPERATOR TRAINING CAMPAIGN", 190), "march_lite", []))
segments.append(("march", march_scene(3.2, [("YOUR CITY", INK), ("NEEDS YOU.", RED)], "Your fellow trainees are ready. Are you?"), "march_lite", []))
segments.append(("count", card(["12 MISSIONS.", "ONE BUTTON."], 1.6, [INK, RED]), "march", []))

for idx, (stage, verb, tagline, pre) in enumerate(MISSIONS):
    r, cap = win(stage)
    if r is None:
        print("note: no %s win recorded, skipped" % stage)
        continue
    a = max(r["start"], r["result"] - int(pre * FPS))
    b = r["result"] + TAIL

    def gen(cap=cap, a=a, b=b, r=r, idx=idx, stage=stage, verb=verb, tagline=tagline):
        for fr in range(a, b):
            k = (fr - r["result"]) / TAIL if fr >= r["result"] else 0
            yield mission_frame(idx, stage, verb, tagline, frame(cap, fr), k if fr >= r["result"] else 0)
    segments.append(("m%02d" % (idx + 1), gen(), "march", clip_sfx(cap, a, b) + [(r["result"] - a, "_stamp", None)]))

segments.append(("hesitate", card(["HESITATION", "IS A", "THREAT."], 1.6, [RED, INK, RED]), "silent", [(0, "_hit", None)]))
# Fail -> fingerprint (start and the failed end) -> THREAT ALERT -> the car -> FAILED JOB
irl_ranges = [(last_fail - 18, fingerprint, "FAIL TOO OFTEN..."),
              (fingerprint + 14, fingerprint + 70, "...AND THE SYSTEM CHECKS WHO YOU ARE."),
              (irl_start - 38, irl_start + 14, "AUTHENTICATION FAILED."),
              (irl_start + 14, irl_impact + 18, "YOU ARE THE THREAT NOW."),
              (irl_impact + 70, irl_impact + 118, "YOU ARE THE THREAT NOW.")]
irl_sfx, done = [], 0
for a, b, _ in irl_ranges:
    irl_sfx += [(off + done, s, c) for off, s, c in clip_sfx("capB", a, b)]
    done += b - a
segments.append(("irl", full_clip("capB", irl_ranges, "WARNING  •  OPERATOR UNDER REVIEW"), "drone", irl_sfx))
segments.append(("graduate", card(["SCORE 1500.", "EARN YOUR", "STAR."], 1.6, [INK, INK, RED]), "march", [(0, "_hit", None)]))
ea, eb = end_start + 100, min(end_drones, end_start + 170)
segments.append(("ending1", full_clip("capA", [(ea, eb, "TRAINING COMPLETE.")]), "march", clip_sfx("capA", ea, eb)))
ea2, eb2 = end_shooters - 20, min(end_thanks, end_shooters + 130)
segments.append(("ending2", full_clip("capA", [(ea2, eb2, "NEW DRONES. NEW OPERATORS.")]), "drone", clip_sfx("capA", ea2, eb2)))
segments.append(("thanks", full_clip("capA", [(end_thanks + 10, end_thanks + 66, "")]), "silent", []))
segments.append(("everywhere", card(["THE THREAT", "IS EVERYWHERE."], 1.6, [INK, RED]), "silent", []))
segments.append(("sowe", card(["SO ARE WE."], 1.3, [RED], size=200), "silent", [(0, "_hit", None)]))
segments.append(("end", end_card(5.0), "march", [(0, "_stinger", None)]))

parts, sections, sfx_events = [], [], []
t = 0.0
for i, (name, gen, level, sfx) in enumerate(segments):
    folder = os.path.join(WORK, "seg%02d" % i)
    out = os.path.join(WORK, "p%02d.mp4" % i)
    n = save_frames(folder, gen)
    tl.encode_sequence(folder, 0, n, out, fade_out=0.5 if name == "end" else 0)
    shutil.rmtree(folder, ignore_errors=True)
    for off, s, cap in sfx:
        sfx_events.append((t + off / FPS, s, cap))
    sections.append((t, t + n / FPS, level))
    parts.append(out)
    print("%-10s %6.2f -> %6.2f  %s" % (name, t, t + n / FPS, level))
    t += n / FPS

total = t
video = os.path.join(WORK, "video.mp4")
tl.concat(parts, video)


# ---------------- audio ----------------
RECORDED = {"CarHit": "Car Brake", "CarCrash": "Car Crash", "DroneShot": "Drone Shot", "PianoWhistle": "Piano Fall",
            "PianoCrush": "Piano Impact", "PianoCrash": "Piano Impact", "SewerFall": "Fall", "ElevatorFall": "Fall",
            "Scream": "Scream", "PhoneCrack": "Phone Crack"}
ALL_AT_ONCE = {"PianoCrush", "PianoCrash"}
SOUNDS = os.path.join(ROOT, "Assets", "Resources", "Sounds")
decoded, picks = {}, {}


def decode(path):
    if path not in decoded:
        wav = os.path.join(WORK, "snd%03d.wav" % len(decoded))
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", path, "-ac", "1", "-ar", str(SR), "-sample_fmt", "s16", wav], check=True)
        decoded[path] = tl.load_wav(wav)
    return decoded[path]


def recorded(name):
    folder = os.path.join(SOUNDS, RECORDED[name])
    files = sorted(f for f in os.listdir(folder) if not f.endswith(".meta") and not f.startswith("."))
    if name in ALL_AT_ONCE:
        clips = [decode(os.path.join(folder, f)) for f in files]
        out = np.zeros(max(len(c) for c in clips), dtype=np.float32)
        for c in clips:
            out[:len(c)] += c
        return out
    i = picks.get(name, -1) + 1
    picks[name] = i
    clip = decode(os.path.join(folder, files[i % len(files)]))
    if name == "DroneShot":   # the game fires a short burst
        gap = int(0.09 * SR)
        out = np.zeros(len(clip) + 2 * gap, dtype=np.float32)
        for s in range(3):
            out[s * gap:s * gap + len(clip)] += clip * (1 - 0.15 * s)
        return out
    return clip


mix = np.zeros(int((total + 1) * SR), dtype=np.float32)
mus = tl.march(total, sections)
mix[:min(len(mix), len(mus))] += mus[:len(mix)] * 0.8

synth_cache = {}
for at, name, cap in sfx_events:
    if name == "_hit":
        tl.place(mix, tl.kick(int(0.5 * SR)).astype(np.float32), at, 0.9)
    elif name == "_stamp":
        tl.place(mix, tl.kick(int(0.25 * SR)).astype(np.float32), at, 0.6)
    elif name == "_stinger":
        tl.place(mix, tl.stinger(), at, 0.9)
    elif name in RECORDED:
        tl.place(mix, recorded(name), at, 0.55 if name == "Scream" else 0.8)
    else:
        path = os.path.join(S, cap, "sfx", name + ".wav")
        if os.path.exists(path):
            synth_cache.setdefault(path, tl.load_wav(path))
            tl.place(mix, synth_cache[path], at, 0.7)

mix *= 0.92 / np.max(np.abs(mix))
audio = os.path.join(WORK, "audio.wav")
tl.save_wav(audio, mix)
subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", video, "-i", audio, "-c:v", "copy", "-c:a", "aac",
                "-b:a", "192k", "-shortest", "-movflags", "+faststart", OUT], check=True)
print("total %.1fs  sfx %d  ->  %s" % (total, len(sfx_events), OUT))
