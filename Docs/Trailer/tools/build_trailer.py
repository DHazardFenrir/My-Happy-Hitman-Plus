"""Edits the Agent TR8N1 trailer (recruitment-propaganda style) from three autopilot recordings:
capA = mostly wins + the ending, capB = only fails (ad), capC = every fail kills you ("real life" death).
Clips are picked automatically from each recording's events.txt.
Music: the synthesized chiptune march; pass --composer to use the game's tracks from
Assets/Test_Musica instead (happy, then terror).

usage: build_trailer.py <folder with capA/ capB/ capC/> <out.mp4> [work folder] [--composer]"""
import os, sys, shutil, subprocess
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(__file__))
from trailer_lib import *

ARGS = [a for a in sys.argv[1:] if not a.startswith("--")]
USE_MARCH = "--composer" not in sys.argv  # the chiptune march is the trailer's music
S, OUT = ARGS[0], ARGS[1]
WORK = ARGS[2] if len(ARGS) > 2 else os.path.join(S, "work")
MUSIC_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "..", "Assets", "Test_Musica")
shutil.rmtree(WORK, ignore_errors=True)
os.makedirs(WORK)

def load_mp3(path):
    """Decodes an mp3 to mono float32 at SR with ffmpeg"""
    raw = subprocess.run(["ffmpeg", "-loglevel", "error", "-i", path, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32)


def composer_bed(total_seconds, sections, happy_path, terror_path, ramp=0.4):
    """The composer's tracks under the trailer, both running from the start (like in the game):
    the happy one for the propaganda, the terror one for the fails and the ending; silent sections mute both.
    Volume changes are smoothed over `ramp` seconds."""
    n = int((total_seconds + 1) * SR)
    gains = {  # level -> (happy, terror)
        "march": (1.0, 0.0), "march_lite": (0.55, 0.0),
        "terror_lite": (0.0, 0.6), "terror": (0.0, 1.0), "drone": (0.0, 1.0), "silent": (0.0, 0.0),
    }
    gh, gt = np.zeros(n, np.float32), np.zeros(n, np.float32)
    for a, b, lv in sections:
        h, te = gains.get(lv, (0.0, 0.0))
        gh[int(a * SR):int(b * SR)] = h
        gt[int(a * SR):int(b * SR)] = te
    k = max(1, int(ramp * SR))
    smooth = lambda g: np.convolve(g, np.ones(k, np.float32) / k, mode="same")
    out = np.zeros(n, np.float32)
    for path, g in ((happy_path, smooth(gh)), (terror_path, smooth(gt))):
        track = load_mp3(path)
        reps = int(np.ceil(n / max(1, len(track))))
        track = np.tile(track, reps)[:n]
        out += track * g
    return out


CROP_PHONE = (420, 160, 1500, 768)    # minigame zoomed in
CROP_LOW = (420, 260, 1500, 868)      # car / elevator (action lower on screen)
CROP_CARD = (200, 235, 1700, 1079)    # menu, target card, neon sign, ad (they fill the phone)
SECONDS_BEFORE_HIT = {"PianoDropStage": 2.6, "LightTrafficStage": 1.4, "ElevatorStage": 2.2, "SewerStage": 1.5,
                     "PlazaDroneStage": 2.4, "NightDroneStage": 2.6, "RcCarStage": 4.2, "DogFenceStage": 2.8}


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


def pick(rs, stage, success, required=True):
    for r in rs:
        if r["stage"] == stage and r["success"] == success:
            return r
    if required:
        raise SystemExit("no %s %s round (record again)" % (stage, success))
    print("note: no %s round, skipped" % stage)
    return None


A, B = rounds("capA"), rounds("capB")


def hit_clip(cap, r, crop):
    if r is None:
        return None
    pre = int(SECONDS_BEFORE_HIT[r["stage"]] * FPS)
    return ("clip", cap, max(r["start"], r["result"] - pre), r["resultcard"], crop)


piano_win = pick(A, "PianoDropStage", True)
car_win = pick(A, "LightTrafficStage", True)
elev_win = pick(A, "ElevatorStage", True)
sewer_win = pick(A, "SewerStage", True)
plaza_win = pick(A, "PlazaDroneStage", True, required=False)
night_win = pick(A, "NightDroneStage", True, required=False)
# The newest minigames may not come up in the wins take: look in every take (capD can be an extra one)
TAKES = [c for c in ("capA", "capD", "capB", "capC") if os.path.exists(os.path.join(S, c, "events.txt"))]


def pick_any(stage, success):
    for cap in TAKES:
        r = pick(rounds(cap), stage, success, required=False)
        if r is not None:
            return r, cap
    return None, None


rc_win, rc_cap = pick_any("RcCarStage", True)
dogs_win, dogs_cap = pick_any("DogFenceStage", True)
# Fails come from the all-fails take, or from the wins take if a minigame didn't show up there
car_fail, car_fail_cap = (pick(B, "LightTrafficStage", False, required=False), "capB")
if car_fail is None: car_fail, car_fail_cap = pick(A, "LightTrafficStage", False), "capA"
# Second fail: the piano if there is one, otherwise any other minigame's fail
piano_fail, piano_fail_cap = None, None
for stage in ("PianoDropStage", "ElevatorStage", "SewerStage", "NightDroneStage", "PlazaDroneStage"):
    for rs, cap in ((B, "capB"), (A, "capA")):
        if piano_fail is None:
            piano_fail, piano_fail_cap = pick(rs, stage, False, required=False), cap
if piano_fail is None: raise SystemExit("no second fail round (record again)")
calm = pick(A, "SewerStage", True)
end_start, end_drones, end_thanks = first_event("capA", "ending start"), first_event("capA", "ending drones"), first_event("capA", "ending thanks")
end_shooters = first_event("capA", "ending shooters")
card_assigned = first_event("capA", "dronecard Assigned")
card_certified = first_event("capA", "dronecard Certified")
ad = first_event("capB", "ad")


def car_irl(cap):
    """The first "real life" death that happens in the car minigame (capC: every fail kills you)"""
    last_card, start = "", None
    for fr, ev in read_events(cap):
        if ev.startswith("card "):
            last_card = ev
        elif ev == "irl start" and "LightTraffic" in last_card:
            start = fr
        elif start is not None and ev == "irl end":
            return start, fr
    raise SystemExit("no car death in " + cap)


irl_start, irl_end = car_irl("capC")
card_destroyed = next(fr for fr, ev in read_events("capC") if ev == "dronecard Destroyed" and fr >= irl_end)

# (item, music level)
TIMELINE = [
    (("card", ["CITY SURVEILLANCE NETWORK"], 2.4, dict(sub="PUBLIC SAFETY ANNOUNCEMENT", size=80)), "march_lite"),
    (("card", ["THEY LOOK LIKE", "ORDINARY CITIZENS."], 2.2, {}), "march_lite"),
    (("clip", "capA", calm["start"], calm["start"] + 40, CROP_PHONE), "march_lite"),
    (("card", ["BUT 1 IN 5 IS A", "POTENTIAL THREAT."], 2.8, dict(color=RED, sub="*ACCORDING TO OUR EVIL-DETECTION ALGORITHM (BETA)")), "march_lite"),
    (("poster", ["YOUR CITY", "NEEDS YOU"], 2.8, "ENLIST AS A DRONE OPERATOR TRAINEE"), "march"),
    (("clip", "capA", 15, 65, CROP_CARD), "march"),                                   # menu
    (("clip", "capA", card_assigned + 2, card_assigned + 60, CROP_CARD), "march"),     # your drone is assigned
    (("card", ["STEP 1:", "IDENTIFY THE THREAT"], 1.8, dict(color=YELLOW)), "march"),
    (("clip", "capA", piano_win["card"] + 8, piano_win["start"] - 4, CROP_CARD), "march"),   # target card
    (hit_clip("capA", piano_win, CROP_PHONE), "march"),
    (("clip", "capA", piano_win["resultcard"], piano_win["resultcard"] + 28, CROP_CARD), "march"),  # GOOD JOB
    (("card", ["STEP 2:", "NEUTRALIZE IT."], 1.8, dict(color=YELLOW, sub="ACCIDENTS HAPPEN.")), "march"),
    (hit_clip("capA", car_win, CROP_LOW), "march"),
    (hit_clip("capA", elev_win, CROP_LOW), "march"),
    (hit_clip("capA", sewer_win, CROP_PHONE), "march"),
    (("clip", "capA", sewer_win["resultcard"], sewer_win["resultcard"] + 28, CROP_CARD), "march"),
    (("card", ["UPGRADE:", "DRONE CAM"], 1.6, dict(color=GREEN, sub="DAY OR NIGHT. NOBODY HIDES.")), "march"),
    (hit_clip("capA", plaza_win, CROP_CARD), "march"),
    (hit_clip("capA", night_win, CROP_CARD), "march"),
    # New: the RC car and the guard dogs
    (("card", ["NEW:", "GROUND UNITS"], 1.6, dict(color=GREEN, sub="REMOTE CONTROLLED. FULLY DENIABLE.")), "march") if rc_win else None,
    (("clip", rc_cap, rc_win["card"] + 8, rc_win["start"] - 4, CROP_CARD), "march") if rc_win else None,  # target vehicle + preparing RC car
    (hit_clip(rc_cap, rc_win, CROP_CARD), "march"),
    (("card", ["NEW:", "COMMUNITY K-9 PROGRAM"], 1.6, dict(color=GREEN, sub="GOOD BOYS. SMART GATES.")), "march") if dogs_win else None,
    (hit_clip(dogs_cap, dogs_win, CROP_LOW), "march"),
    (("clip", dogs_cap, dogs_win["resultcard"], dogs_win["resultcard"] + 28, CROP_CARD), "march") if dogs_win else None,  # GOOD JOB
    (("card", ["STEP 3:", "STAY SHARP."], 1.6, dict(color=RED)), "terror_lite"),
    (hit_clip(car_fail_cap, car_fail, CROP_LOW), "terror_lite"),
    (hit_clip(piano_fail_cap, piano_fail, CROP_PHONE), "terror_lite"),
    (("clip", piano_fail_cap, piano_fail["resultcard"], piano_fail["resultcard"] + 26, CROP_CARD), "terror_lite"),  # FAILED
    (("clip", "capB", ad + 4, ad + 64, CROP_CARD), "terror_lite"),
    (("card", ["HESITATION HAS", "CONSEQUENCES."], 2.0, dict(color=RED)), "silent"),
    (("clip", "capC", irl_start + 6, irl_end - 30, None), "silent"),                # THREAT ALERT + the car + FAILED JOB
    (("clip", "capC", card_destroyed + 2, card_destroyed + 80, CROP_CARD), "silent"),  # ...and your drone pays for it
    (("poster", ["EARN YOUR", "WINGS"], 2.6, "SCORE 1000 POINTS AND GRADUATE"), "march"),
    (("clip", "capA", card_certified + 2, card_certified + 70, CROP_CARD), "march"),   # your drone earns its star
    (("clip", "capA", end_start + 106, end_start + 196, None), "march"),              # TRAINING COMPLETE
    (("clip", "capA", end_drones + 20, min(end_shooters, end_drones + 200), None), "march"),  # two drones carry THANKS FOR PLAYING
    (("clip", "capA", end_shooters, min(end_thanks, end_shooters + 300), None), "drone"),  # the new drones open fire
    (("clip", "capA", end_thanks, end_thanks + 70, None), "silent"),                  # THANKS FOR YOUR SERVICE
    (("card", ["YOUR TRAINING DATA", "HAS BEEN UPLOADED."], 3.2, dict(color=WHITE, size=80)), "drone"),
    (("logo", 6.0), "silent"),
]

TIMELINE = [(item, level) for item, level in TIMELINE if item is not None]

parts, sections, sfx_events, typing_events = [], [], [], []
fire_at = logo_at = None
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
        _, cap, a, b, crop = item
        os.makedirs(folder)
        for k, fr in enumerate(range(a, b)):
            im = Image.open(os.path.join(S, cap, "frames", "f%06d.jpg" % fr))
            if crop:
                im = im.crop(crop).resize((W, H), Image.LANCZOS)
            im.save(os.path.join(folder, "f%06d.jpg" % k), quality=93)
        n = b - a
        encode_sequence(folder, 0, n, out)
        for fr, ev in read_events(cap):
            if ev.startswith("sfx ") and a <= fr < b:
                sfx_events.append((t + (fr - a) / FPS, ev[4:]))
        if level == "drone" and fire_at is None:
            fire_at = t
    else:
        _, secs = item
        n = logo_card(folder, secs, "YOUR CITY NEEDS YOU.",
                      ["DEMO  -  NOVEMBER 15", "8 TRAINING MISSIONS. MORE CLEARANCE LEVELS COMING."])
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
happy_mp3 = os.path.join(MUSIC_DIR, "Level-1 Happy.mp3")
terror_mp3 = os.path.join(MUSIC_DIR, "Level1 Terror.mp3")
if USE_MARCH or not (os.path.exists(happy_mp3) and os.path.exists(terror_mp3)):
    mus = march(total, [(a, b, {"terror_lite": "march_lite", "terror": "drone"}.get(lv, lv)) for a, b, lv in sections])
    mix[:min(len(mix), len(mus))] += mus[:len(mix)] * 0.8
else:
    mus = composer_bed(total, sections, happy_mp3, terror_mp3)
    mix[:min(len(mix), len(mus))] += mus[:len(mix)] * 0.55

sfx_dir = os.path.join(S, "capA", "sfx")
cache = {"_hit": kick(int(0.5 * SR)).astype(np.float32)}
for at, name in sfx_events:
    if name not in cache:
        cache[name] = load_wav(os.path.join(sfx_dir, name + ".wav"))
    gain = 1.0 if name.startswith("Drone") else 0.75  # the gunshots land in silence: louder
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
