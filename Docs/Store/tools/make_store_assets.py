"""Images for the itch.io page and the Ko-fi page, made from the trailer recordings.

  itch/cover.png        630x500   the thumbnail shown in itch.io listings
  itch/banner.png       960x300   top of the game page (Edit theme > Banner)
  itch/background.png   tile      page background (Edit theme > Background, repeat)
  itch/shot_*.png       1920x1080 screenshots
  itch/gif_*.gif        400 wide  one short clip per minigame (itch shows GIFs as screenshots)
  kofi/cover.png        1600x400  Ko-fi page header
  kofi/avatar.png       512x512   Ko-fi profile picture (the pixel drone)

usage: make_store_assets.py <recordings folder with capA/ ...> <output folder>
Needs pillow, numpy and ffmpeg (same venv as the trailer)."""
import os, sys, shutil, subprocess
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "Trailer", "tools"))
from trailer_lib import font, RED, YELLOW, GREEN, WHITE, DRONE, DRONE_COLORS

S, OUT = sys.argv[1], sys.argv[2]
ITCH, KOFI = os.path.join(OUT, "itch"), os.path.join(OUT, "kofi")
for d in (ITCH, KOFI):
    os.makedirs(d, exist_ok=True)

BG = (8, 6, 12)
TAKES = [c for c in ("capA", "capD", "capE", "capOld") if os.path.exists(os.path.join(S, c, "events.txt"))]
# The phone screen in the recordings (1920x1080) is x 600-1300, from y 260 down. Each GIF is cropped to
# the part of it its minigame uses (the piano and the sewer only use the top)
TOP, MID = (600, 260, 1300, 960), (600, 330, 1300, 1030)
CROPS = {"PianoDropStage": (600, 250, 1300, 675), "LightTrafficStage": (600, 260, 1300, 890),
         "ElevatorStage": MID, "SewerStage": (600, 250, 1300, 700),
         "PlazaDroneStage": TOP, "NightDroneStage": TOP, "RcCarStage": MID, "DogFenceStage": MID}
GIF_WIDTH = 400
# The ending without the game's own camera texts (top and bottom)
ENDING_CLEAN = (100, 150, 1820, 930)
NAMES = {"PianoDropStage": "piano", "LightTrafficStage": "traffic", "ElevatorStage": "elevator", "SewerStage": "sewer",
         "PlazaDroneStage": "drone_day", "NightDroneStage": "drone_night", "RcCarStage": "rc_car", "DogFenceStage": "guard_dogs"}


# ---------------- reading the recordings ----------------

def wins(cap):
    """[(stage, card frame, result frame)] of the rounds that were won"""
    out, stage, card = [], None, None
    for line in open(os.path.join(S, cap, "events.txt")):
        fr, ev = line.rstrip("\n").split("\t")
        fr = int(fr)
        if ev.startswith("card ") and len(ev.split()) > 1 and ev.split()[1].endswith("Stage"):
            stage, card = ev.split()[1], fr
        elif ev.startswith("result success") and stage:
            out.append((stage, card, fr))
            stage = None
    return out


def frame(cap, fr):
    return Image.open(os.path.join(S, cap, "frames", "f%06d.jpg" % fr)).convert("RGB")


def first_win(stage):
    for cap in TAKES:
        for st, card, res in wins(cap):
            if st == stage:
                return cap, card, res
    return None


def ending_frame(offset_from_shooters, clean=True):
    for line in open(os.path.join(S, "capA", "events.txt")):
        fr, ev = line.rstrip("\n").split("\t")
        if ev == "ending shooters":
            img = frame("capA", int(fr) + offset_from_shooters)
            return img.crop(ENDING_CLEAN) if clean else img
    return None


# ---------------- drawing helpers ----------------

def scan(img, step=3, alpha=45):
    over = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    for y in range(0, img.height, step):
        d.line([(0, y), (img.width, y)], fill=(0, 0, 0, alpha))
    return Image.alpha_composite(img.convert("RGBA"), over).convert("RGB")


def corners(d, w, h, m, length, width, color=WHITE):
    for (x, y, sx, sy) in [(m, m, 1, 1), (w - m, m, -1, 1), (m, h - m, 1, -1), (w - m, h - m, -1, -1)]:
        d.line([(x, y), (x + length * sx, y)], fill=color, width=width)
        d.line([(x, y), (x, y + length * sy)], fill=color, width=width)


def rec(d, x, y, size):
    mono = font("Courier New Bold.ttf", size)
    d.ellipse([x, y + size * 0.15, x + size * 0.8, y + size * 0.95], fill=(230, 30, 30))
    d.text((x + size * 1.1, y), "REC", font=mono, fill=WHITE)


def logo(d, cx, cy, size, glow_img=None):
    """AGENT (red) TR8N1 (yellow) in Impact, centered on (cx, cy)"""
    big = font("Impact.ttf", size)
    wa, wb, gap = d.textlength("AGENT", font=big), d.textlength("TR8N1", font=big), size * 0.23
    x0 = cx - (wa + gap + wb) / 2
    d.text((x0, cy), "AGENT", font=big, fill=RED, anchor="lm")
    d.text((x0 + wa + gap, cy), "TR8N1", font=big, fill=YELLOW, anchor="lm")


def with_glow(base, draw_fn, radius, strength=1.6):
    """Draws draw_fn on a layer, blurs a copy behind it (neon halo)"""
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(layer))
    halo = layer.filter(ImageFilter.GaussianBlur(radius))
    a = np.asarray(halo).astype(np.float32)
    a[..., 3] = np.clip(a[..., 3] * strength, 0, 255)
    halo = Image.fromarray(a.astype(np.uint8))
    out = Image.alpha_composite(base.convert("RGBA"), halo)
    return Image.alpha_composite(out, layer).convert("RGB")


def darken(img, k):
    return Image.blend(img, Image.new("RGB", img.size, (0, 0, 0)), k)


def fit(img, w, h):
    """Crop to the aspect ratio, then resize"""
    r = w / h
    if img.width / img.height > r:
        nw = int(img.height * r)
        img = img.crop(((img.width - nw) // 2, 0, (img.width - nw) // 2 + nw, img.height))
    else:
        nh = int(img.width / r)
        img = img.crop((0, (img.height - nh) // 2, img.width, (img.height - nh) // 2 + nh))
    return img.resize((w, h), Image.LANCZOS)


def pixel_drone(px, eye_on=True):
    rows, cols = len(DRONE), max(len(r) for r in DRONE)
    img = Image.new("RGBA", (cols * px, rows * px), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for r, row in enumerate(DRONE):
        for c, ch in enumerate(row):
            if ch in DRONE_COLORS:
                col = DRONE_COLORS[ch]
                if ch == "L" and not eye_on:
                    col = (120, 10, 10)
                d.rectangle([c * px, r * px, (c + 1) * px - 1, (r + 1) * px - 1], fill=col)
    return img


# ---------------- itch ----------------

def cover():
    w, h = 630, 500
    shot = ending_frame(150) or frame(TAKES[0], 100)
    img = darken(fit(shot, w, h), 0.6)
    img = with_glow(img, lambda d: logo(d, w / 2, 215, 96), 10)
    d = ImageDraw.Draw(img)
    d.text((w / 2, 300), "THREATS ARE HIDING AMONG US.", font=font("Courier New Bold.ttf", 22), fill=WHITE, anchor="mm")
    d.text((w / 2, 330), "YOUR CITY NEEDS YOU.", font=font("Courier New Bold.ttf", 22), fill=GREEN, anchor="mm")
    rec(d, 26, 22, 20)
    d.text((w - 26, 22), "CAM 07", font=font("Courier New Bold.ttf", 20), fill=WHITE, anchor="ra")
    corners(d, w, h, 12, 34, 3)
    img = scan(img)
    img.save(os.path.join(ITCH, "cover.png"))


def banner():
    w, h = 960, 300
    img = Image.new("RGB", (w, h), BG)
    img = with_glow(img, lambda d: logo(d, w / 2, 125, 120), 12)
    d = ImageDraw.Draw(img)
    d.text((w / 2, 220), "8 TRAINING MISSIONS.  ONE BUTTON.  ZERO EXCUSES.", font=font("Courier New Bold.ttf", 24), fill=GREEN, anchor="mm")
    rec(d, 28, 22, 20)
    d.text((w - 28, 22), "CAM 04  //  TRAINING DIVISION", font=font("Courier New Bold.ttf", 18), fill=WHITE, anchor="ra")
    corners(d, w, h, 12, 30, 3)
    scan(img).save(os.path.join(ITCH, "banner.png"))


def background():
    """Dark tile with faint scanlines and a CCTV grid, meant to repeat"""
    img = Image.new("RGB", (64, 64), BG)
    d = ImageDraw.Draw(img)
    for y in range(0, 64, 4):
        d.line([(0, y), (63, y)], fill=(14, 11, 20))
    d.line([(0, 0), (63, 0)], fill=(22, 30, 26))
    d.line([(0, 0), (0, 63)], fill=(22, 30, 26))
    img.save(os.path.join(ITCH, "background.png"))


def screenshots():
    shots = []
    for stage in ("RcCarStage", "DogFenceStage", "ElevatorStage", "PlazaDroneStage", "NightDroneStage"):
        w = first_win(stage)
        if w:
            cap, card, res = w
            shots.append((NAMES[stage], frame(cap, res - 6)))
    e = ending_frame(160, clean=False)  # screenshots keep the CCTV texts
    if e is not None:
        shots.append(("ending", e))
    for name, img in shots:
        img.save(os.path.join(ITCH, "shot_%s.png" % name))


def gifs():
    tmp = os.path.join(OUT, "_gif_frames")
    for stage, name in NAMES.items():
        w = first_win(stage)
        if not w:
            print("no win of %s, no gif" % stage)
            continue
        cap, card, res = w
        a, b = max(card, res - 75), res + 25           # ~2.5 s before the hit, a bit after
        shutil.rmtree(tmp, ignore_errors=True)
        os.makedirs(tmp)
        x0, y0, x1, y1 = CROPS[stage]
        size = (GIF_WIDTH, round(GIF_WIDTH * (y1 - y0) / (x1 - x0)))
        for k, fr in enumerate(range(a, b, 2)):          # 15 fps
            frame(cap, fr).crop(CROPS[stage]).resize(size, Image.LANCZOS).save(os.path.join(tmp, "f%04d.png" % k))
        out = os.path.join(ITCH, "gif_%s.gif" % name)
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", "15", "-i", os.path.join(tmp, "f%04d.png"),
                        "-vf", "split[a][b];[a]palettegen=max_colors=96[p];[b][p]paletteuse=dither=none",
                        "-loop", "0", out], check=True)
        print("%-22s %4d KB" % (os.path.basename(out), os.path.getsize(out) // 1024))
    shutil.rmtree(tmp, ignore_errors=True)


# ---------------- Ko-fi ----------------

def kofi_cover():
    w, h = 1600, 400
    shot = ending_frame(150) or frame(TAKES[0], 100)
    img = darken(fit(shot, w, h), 0.62)
    img = with_glow(img, lambda d: logo(d, w / 2, 165, 150), 14)
    d = ImageDraw.Draw(img)
    d.text((w / 2, 285), "SUPPORT THE AGENCY. KEEP THE DRONES FLYING.", font=font("Courier New Bold.ttf", 34), fill=GREEN, anchor="mm")
    rec(d, 40, 30, 28)
    d.text((w - 40, 30), "CAM 07  //  CITY SURVEILLANCE NETWORK", font=font("Courier New Bold.ttf", 26), fill=WHITE, anchor="ra")
    corners(d, w, h, 16, 46, 4)
    scan(img, 4).save(os.path.join(KOFI, "cover.png"))


def kofi_avatar():
    s = 512
    img = Image.new("RGB", (s, s), BG)
    drone = pixel_drone(26)
    if drone.width > s - 60:
        drone = drone.resize((s - 60, int(drone.height * (s - 60) / drone.width)), Image.NEAREST)
    img = img.convert("RGBA")
    img.alpha_composite(drone, ((s - drone.width) // 2, (s - drone.height) // 2 - 30))
    img = img.convert("RGB")
    d = ImageDraw.Draw(img)
    big = font("Impact.ttf", 74)
    d.text((s / 2, s - 92), "TR8N1", font=big, fill=YELLOW, anchor="mm")
    rec(d, 36, 30, 30)
    corners(d, s, s, 16, 40, 4)
    scan(img, 4).save(os.path.join(KOFI, "avatar.png"))


cover(); banner(); background(); screenshots(); gifs(); kofi_cover(); kofi_avatar()
print("done ->", OUT)
