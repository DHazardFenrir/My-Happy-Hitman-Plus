"""Second Agent TR8N1 trailer ("PART II"): same propaganda line as the first one (hidden threats among us,
your city needs you), new missions, dark synthwave music (trailer_music.py).
Edited from three autopilot recordings, like the first trailer:
  capA = mostly wins + the ending, capB = only fails (ads), capC = every fail kills you (threat alert + death).
Clips are picked automatically from each recording's events.txt; texts and order are in TIMELINE.

usage: build_trailer_v3.py <folder with capA/ capB/ capC/> <out.mp4> [work folder]"""
import os, sys, shutil, subprocess
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(__file__))
from trailer_lib import *
from trailer_music import synthwave

S, OUT = sys.argv[1], sys.argv[2]
WORK = sys.argv[3] if len(sys.argv) > 3 else os.path.join(S, "work_v3")
shutil.rmtree(WORK, ignore_errors=True)
os.makedirs(WORK)

CROP_PHONE = (420, 160, 1500, 768)    # minigame zoomed in
CROP_LOW = (420, 260, 1500, 868)      # car / elevator / dogs (action lower on screen)
CROP_CARD = (200, 235, 1700, 1079)    # menu, cards, neon sign, ad, RC road (they fill the phone)
SECONDS_BEFORE_HIT = {"PianoDropStage": 2.6, "LightTrafficStage": 1.4, "ElevatorStage": 2.2, "SewerStage": 1.5,
                      "PlazaDroneStage": 2.4, "NightDroneStage": 2.6, "RcCarStage": 4.2, "DogFenceStage": 2.8}
# capOld: optional fallback (a link to an older recording) for minigames the autopilot failed in these takes
TAKES = [c for c in ("capA", "capD", "capE", "capB", "capC", "capOld") if os.path.exists(os.path.join(S, c, "events.txt"))]


def read_events(cap):
    rows = []
    for line in open(os.path.join(S, cap, "events.txt")):
        fr, ev = line.rstrip("\n").split("\t")
        rows.append((int(fr), ev))
    return rows


def rounds(cap):
    """[{stage, card, start, result, resultcard, success}]"""
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


def first_event(cap, name):
    return next(fr for fr, ev in read_events(cap) if ev == name)


def win(stage):
    """(round, take) of the first win of a minigame, looking in every take"""
    for cap in TAKES:
        for r in rounds(cap):
            if r["stage"] == stage and r["success"]:
                return r, cap
    print("note: no %s win, skipped" % stage)
    return None, None


def hit(stage, crop, pre=None, tail=None):
    """The moment before the hit until the result card (or `tail` frames after the result)"""
    r, cap = win(stage)
    if r is None:
        return None
    a = max(r["start"], r["result"] - int((pre or SECONDS_BEFORE_HIT[stage]) * FPS))
    b = r["resultcard"] if tail is None else min(r["resultcard"], r["result"] + tail)
    return ("clip", cap, a, b, crop)


def irl_death(cap, stage_hint):
    """First "real life" death after a card of that minigame -> (start, end, drone card frame)"""
    last_card, start = "", None
    for fr, ev in read_events(cap):
        if ev.startswith("card "):
            last_card = ev
        elif ev == "irl start" and stage_hint in last_card:
            start = fr
        elif start is not None and ev == "irl end":
            destroyed = next(f for f, e in read_events(cap) if e == "dronecard Destroyed" and f >= fr)
            return start, fr, destroyed
    return None


# The threat alert + death: the guard dogs (K-9 UNIT) if it was recorded, otherwise the car
death = irl_death("capC", "DogFence") or irl_death("capC", "LightTraffic") or irl_death("capC", "")
irl_start, irl_end, card_destroyed = death
rc, rc_cap = win("RcCarStage")
dogs, dogs_cap = win("DogFenceStage")
calm, calm_cap = win("SewerStage")
end_start, end_drones, end_thanks = first_event("capA", "ending start"), first_event("capA", "ending drones"), first_event("capA", "ending thanks")
end_shooters = first_event("capA", "ending shooters")
card_assigned = first_event("capA", "dronecard Assigned")
card_certified = first_event("capA", "dronecard Certified")
ad = first_event("capB", "ad")

# (item, music level: pad / pulse / dread / silent)
TIMELINE = [
    # Cold open: the broadcast resumes
    (("card", ["CITY SURVEILLANCE NETWORK"], 2.4, dict(sub="PUBLIC SAFETY ANNOUNCEMENT   -   PART II", size=80)), "pad"),
    (("card", ["YOU HAVE", "ANSWERED THE CALL."], 2.4, {}), "pad"),
    (("clip", calm_cap, calm["start"], calm["start"] + 45, CROP_PHONE), "pad"),
    (("card", ["BUT THE THREATS", "HAVE ADAPTED."], 2.4, dict(color=RED)), "pad"),
    # The new missions
    (("card", ["THEY DRIVE", "AMONG US."], 1.6, dict(color=YELLOW)), "pulse"),
    (("clip", rc_cap, rc["card"] + 8, rc["start"] - 4, CROP_CARD), "pulse") if rc else None,   # target vehicle + preparing the RC car
    (hit("RcCarStage", CROP_CARD), "pulse"),
    (("card", ["THEY SHOP", "AMONG US."], 1.6, dict(color=YELLOW)), "pulse"),
    (hit("DogFenceStage", CROP_LOW), "pulse"),
    (("clip", dogs_cap, dogs["resultcard"], dogs["resultcard"] + 26, CROP_CARD), "pulse") if dogs else None,  # GOOD JOB
    (("card", ["THEY CHECK INTO", "OUR HOTELS."], 1.6, dict(color=YELLOW)), "pulse"),
    (hit("ElevatorStage", CROP_LOW), "pulse"),
    # The call
    (("poster", ["WE STILL", "NEED YOU"], 2.8, "NEW TRAINING MISSIONS AVAILABLE"), "pulse"),
    (("clip", "capA", card_assigned + 2, card_assigned + 60, CROP_CARD), "pulse"),   # a new drone to train
    (("card", ["8 MISSIONS.", "ZERO EXCUSES."], 1.5, dict(color=GREEN)), "pulse"),
    (hit("PianoDropStage", CROP_PHONE, pre=1.4, tail=10), "pulse"),
    (hit("LightTrafficStage", CROP_LOW, pre=1.0, tail=10), "pulse"),
    (hit("SewerStage", CROP_PHONE, pre=1.0, tail=10), "pulse"),
    (hit("PlazaDroneStage", CROP_CARD, pre=1.4, tail=10), "pulse"),
    (hit("NightDroneStage", CROP_CARD, pre=1.4, tail=12), "pulse"),
    # Failing is not an option
    (("card", ["A WORD FROM", "OUR SPONSORS"], 1.4, dict(color=WHITE, size=80)), "pad"),
    (("clip", "capB", ad + 4, ad + 64, CROP_CARD, dict(glitch_out=12)), "pad"),   # the ad gets cut by a hijacked signal
    (("card", ["HESITATION", "IS A THREAT."], 2.0, dict(color=RED)), "dread"),
    (("clip", "capC", irl_start + 6, irl_end - 30, None), "dread"),                 # THREAT ALERT -> K-9 UNIT -> FAILED JOB
    (("clip", "capC", card_destroyed + 2, card_destroyed + 80, CROP_CARD), "silent"),  # ...and your drone pays for it
    # Graduation
    (("poster", ["SERVE YOUR", "CITY"], 2.6, "SCORE 1000 POINTS. EARN YOUR STAR."), "pulse"),
    (("clip", "capA", card_certified + 2, card_certified + 70, CROP_CARD), "pulse"),
    (("clip", "capA", end_start + 106, min(end_start + 196, end_drones + 20), None), "pulse"),  # TRAINING COMPLETE (stops where the banner clip starts). Recordings made before the TextGlow fix flash giant text on end_drones: add dict(hide=[end_drones])
    (("clip", "capA", end_drones + 20, min(end_shooters, end_drones + 200), None), "pulse"),  # two drones carry THANKS FOR PLAYING
    (("clip", "capA", end_shooters, min(end_thanks, end_shooters + 300), None), "dread"),     # the new drones open fire
    (("clip", "capA", end_thanks, end_thanks + 80, None), "silent"),                # THANKS FOR YOUR SERVICE
    (("card", ["THE THREAT", "IS EVERYWHERE."], 2.2, dict(color=WHITE)), "dread"),
    (("card", ["SO ARE WE."], 1.8, dict(color=RED, size=120)), "silent"),
    (("logo", 6.0), "silent"),
]
TIMELINE = [(item, level) for item, level in TIMELINE if item is not None]

glitch_rng = np.random.default_rng(5)


def glitch(im, p):
    """Signal interference, p from 0 (barely) to 1 (pure static): torn horizontal bands, RGB split, noise"""
    a = np.asarray(im.convert("RGB")).astype(np.int16)
    h, w, _ = a.shape
    out = a.copy()
    for _ in range(int(3 + p * 14)):
        y = int(glitch_rng.integers(0, h))
        bh = int(glitch_rng.integers(4, int(20 + p * 140)))
        reach = int(40 + p * 320)
        out[y:y + bh] = np.roll(out[y:y + bh], int(glitch_rng.integers(-reach, reach)), axis=1)
    d = int(4 + p * 34)
    out[..., 0] = np.roll(out[..., 0], d, axis=1)
    out[..., 2] = np.roll(out[..., 2], -d, axis=1)
    noise = glitch_rng.integers(0, 256, (h, w, 1)).astype(np.int16)
    mix = min(1.0, p ** 1.6)
    out = out * (1 - mix) + noise * mix
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


def static_burst(glitch_seconds):
    """TV static that grows during the glitch and is cut dead when the next card starts"""
    n = int(glitch_seconds * SR)
    noise = np.random.default_rng(9).uniform(-1, 1, n).astype(np.float32)
    crackle = (np.random.default_rng(10).uniform(0, 1, n) > 0.985).astype(np.float32) * 1.5
    rise = np.linspace(0.25, 1.0, n).astype(np.float32) ** 1.5
    return np.clip((noise * 0.55 + crackle) * rise * 0.6, -0.45, 0.45)  # capped so it does not lower the whole mix

parts, sections, sfx_events, typing_events = [], [], [], []
logo_at = None
t = 0.0
for i, (item, level) in enumerate(TIMELINE):
    kind = item[0]
    out = os.path.join(WORK, "p%02d.mp4" % i)
    folder = os.path.join(WORK, "seg%02d" % i)
    if kind == "card":
        _, lines, secs, opts = item
        n = text_card(folder, lines, secs, **opts)
        encode_sequence(folder, 0, n, out, fade_in=0.12, fade_out=0.12)
        typing_events.append((t, secs * 0.45, sum(len(l) for l in lines)))
    elif kind == "poster":
        _, lines, secs, sub = item
        n = poster_card(folder, secs, lines, sub)
        encode_sequence(folder, 0, n, out, fade_out=0.1)
        sfx_events.append((t, "_hit"))
    elif kind == "clip":
        _, cap, a, b, crop = item[:5]
        opts = item[5] if len(item) > 5 else {}
        glitch_out = opts.get("glitch_out", 0)
        os.makedirs(folder)
        n = b - a
        for k, fr in enumerate(range(a, b)):
            if fr in opts.get("hide", []) and fr > a:
                fr -= 1  # a broken frame: repeat the one before
            im = Image.open(os.path.join(S, cap, "frames", "f%06d.jpg" % fr))
            if crop:
                im = im.crop(crop).resize((W, H), Image.LANCZOS)
            if k >= n - glitch_out:
                im = glitch(im, (k - (n - glitch_out) + 1) / glitch_out)
            im.save(os.path.join(folder, "f%06d.jpg" % k), quality=93)
        if glitch_out:
            sfx_events.append((t + (n - glitch_out) / FPS, "_static"))
        encode_sequence(folder, 0, n, out)
        for fr, ev in read_events(cap):
            if ev.startswith("sfx ") and a <= fr < b:
                sfx_events.append((t + (fr - a) / FPS, ev[4:], cap))
    else:
        _, secs = item
        n = logo_card(folder, secs, "THREATS ARE HIDING AMONG US. WE NEED YOU.",
                      ["DEMO  -  NOVEMBER 15", "8 TRAINING MISSIONS. YOUR CITY IS WATCHING."])
        encode_sequence(folder, 0, n, out, fade_out=0.6)
        logo_at = t
    shutil.rmtree(folder, ignore_errors=True)
    dur = n / FPS
    sections.append((t, t + dur, level))
    parts.append(out)
    print("%-6s %6.2f -> %6.2f  %s" % (kind, t, t + dur, level))
    t += dur

total = t
video = os.path.join(WORK, "video.mp4")
concat(parts, video)

# ---------------- audio ----------------
mix = np.zeros(int((total + 1) * SR), dtype=np.float32)
mus = synthwave(total, sections)
mix[:min(len(mix), len(mus))] += mus[:len(mix)] * 0.85

cache = {"_hit": kick(int(0.5 * SR)).astype(np.float32), "_static": static_burst(glitch_seconds=12 / FPS)}
for ev in sfx_events:
    at, name = ev[0], ev[1]
    cap = ev[2] if len(ev) > 2 else "capA"
    if name not in cache:
        path = os.path.join(S, cap, "sfx", name + ".wav")
        if not os.path.exists(path):
            path = os.path.join(S, "capA", "sfx", name + ".wav")
        cache[name] = load_wav(path)
    gain = 1.0 if name.startswith("Drone") else 0.7 if name.startswith("Dog") else 0.75
    place(mix, cache[name], at, gain)

rng = np.random.default_rng(11)
click_len = int(0.025 * SR)
for start, typing_secs, chars in typing_events:
    for c in range(chars):
        tt = np.arange(click_len) / SR
        click = rng.uniform(-1, 1, click_len) * np.exp(-tt * 260) * 0.35
        place(mix, click.astype(np.float32), start + typing_secs * c / max(1, chars))

place(mix, stinger(), logo_at, 0.9)

mix *= 0.92 / np.max(np.abs(mix))
audio = os.path.join(WORK, "audio.wav")
save_wav(audio, mix)

subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", video, "-i", audio, "-c:v", "copy", "-c:a", "aac",
                "-b:a", "192k", "-shortest", "-movflags", "+faststart", OUT], check=True)
print("total %.1fs  sfx events %d  ->  %s" % (total, len(sfx_events), OUT))
