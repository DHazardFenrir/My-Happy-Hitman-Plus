"""Images for the itch.io description styled as a parody military recruitment pamphlet.

  itch/pamphlet/masthead.png        630 wide  top of the description ("YOUR CITY NEEDS YOU")
  itch/pamphlet/section_*.png       630x64    section headers (1 ... 7)
  itch/pamphlet/march.gif           630x320   the sunglasses soldiers march past while the colonel watches from a ledge
  itch/pamphlet/report_poster.png   630 wide  "HAVE YOU SEEN THIS CITIZEN?"
  itch/pamphlet/equipment.png       630 wide  controls, pamphlet style
  itch/pamphlet/war_bond.png        420x110   Ko-fi button
  itch/pamphlet/fine_print.png      630 wide  footer
  itch/pamphlet/sprites/            the colonel and the sunglasses soldier without background (1x, 8x),
                                    their walk cycles as strips and transparent GIFs

The walk cycles put each sprite's upper body on the legs of the game's base walk
(tripleT/base_characterr/base_character.png); the colonel's legs are drawn 2 px wide like his trousers.

usage: make_pamphlet_assets.py <output folder>   (run from the project root; needs pillow)"""
import os, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = SPRITES = None  # set in __main__; the trailer imports the drawing helpers from here
BASE_WALK = os.path.join(HERE, "..", "..", "..", "Assets", "tripleT", "base_characterr", "base_character.png")

PAPER = (236, 226, 200)
PAPER_DARK = (214, 200, 166)
INK = (41, 38, 64)          # the colonel's navy
RED = (190, 36, 40)
GOLD = (196, 150, 40)
W = 630

SUP = "/System/Library/Fonts/Supplemental/"
def font(name, size, index=0):
    return ImageFont.truetype(SUP + name, size, index=index)
IMPACT = lambda s: font("Impact.ttf", s)
PLATE = lambda s: font("Copperplate.ttc", s, 2)
TYPE = lambda s: font("AmericanTypewriter.ttc", s, 0)
TYPE_B = lambda s: font("AmericanTypewriter.ttc", s, 2)


# ---------------- sprites ----------------
N, G, GREEN_STRIPE, CLEAR = (41, 38, 64, 255), (75, 78, 94, 255), (216, 255, 180, 255), (0, 0, 0, 0)

def hip_row(img):
    # Lowest row of the shorts/trousers block: 8+ navy pixels in a row
    return max(y for y in range(img.height) if sum(img.getpixel((x, y)) == N for x in range(img.width)) >= 8)

def walk_cycle(sprite, thick_legs):
    base = Image.open(BASE_WALK).convert("RGBA")
    top_end = hip_row(sprite)
    frames = []
    for f in range(base.width // 32):
        legs = base.crop((32 * f, 0, 32 * f + 32, 64))
        hip = hip_row(legs)
        out = Image.new("RGBA", (32, 64), CLEAR)
        # Back leg first, then the front leg (with the colonel's green stripe on its outer side)
        for color, paint in ((G, (G, G)), (N, (GREEN_STRIPE, N))):
            if not thick_legs:
                paint = (color,)
            for y in range(hip + 1, 64):
                for x in range(32):
                    if legs.getpixel((x, y)) == color:
                        for dx, c in enumerate(paint):
                            if x + dx < 32:
                                out.putpixel((x + dx, y), c)
        dy = hip - top_end
        for y in range(top_end + 1):
            for x in range(32):
                p = sprite.getpixel((x, y))
                if p[3] and 0 <= y + dy < 64:
                    out.putpixel((x, y + dy), p)
        frames.append(out)
    return frames

def scaled(img, k):
    return img.resize((img.width * k, img.height * k), Image.NEAREST)

def save_transparent_gif(frames, path, ms):
    # GIF transparency: one palette slot is fully transparent
    pal_frames = []
    for fr in frames:
        p = fr.convert("RGBA")
        alpha = p.getchannel("A")
        q = p.convert("RGB").quantize(colors=255)
        mask = Image.eval(alpha, lambda a: 255 if a < 128 else 0)
        q.paste(255, mask=mask)
        pal_frames.append(q)
    pal_frames[0].save(path, save_all=True, append_images=pal_frames[1:], duration=ms, loop=0,
                       transparency=255, disposal=2)

colonel = Image.open(os.path.join(HERE, "sprites", "colonel.png")).convert("RGBA")
soldier = Image.open(os.path.join(HERE, "sprites", "soldier.png")).convert("RGBA")
COLONEL_WALK = walk_cycle(colonel, thick_legs=True)
SOLDIER_WALK = walk_cycle(soldier, thick_legs=False)
WALK_MS = 110

def sprites():
    for name, img, walk in (("colonel", colonel, COLONEL_WALK), ("soldier", soldier, SOLDIER_WALK)):
        img.save(os.path.join(SPRITES, name + ".png"))
        scaled(img, 8).save(os.path.join(SPRITES, name + "_x8.png"))
        strip = Image.new("RGBA", (32 * len(walk), 64), CLEAR)
        for i, fr in enumerate(walk):
            strip.paste(fr, (32 * i, 0))
        strip.save(os.path.join(SPRITES, name + "_walk_strip.png"))
        save_transparent_gif([scaled(fr, 4) for fr in walk], os.path.join(SPRITES, name + "_walk_x4.gif"), WALK_MS)


# ---------------- paper ----------------
def paper(w, h, seed=0):
    import random
    rnd = random.Random(seed)
    img = Image.new("RGB", (w, h), PAPER)
    d = ImageDraw.Draw(img)
    for _ in range(w * h // 40):
        x, y = rnd.randrange(w), rnd.randrange(h)
        c = rnd.randint(-14, 8)
        d.point((x, y), fill=(PAPER[0] + c, PAPER[1] + c, PAPER[2] + c))
    # Darker, worn edges
    edge = Image.new("L", (w, h), 0)
    ImageDraw.Draw(edge).rectangle((0, 0, w - 1, h - 1), outline=255, width=6)
    edge = edge.filter(ImageFilter.GaussianBlur(5))
    img = Image.composite(Image.new("RGB", (w, h), PAPER_DARK), img, edge)
    return img

def text_width(d, s, f):
    return d.textlength(s, font=f)

def stamp(img, text, cx, cy, size, angle, color=RED):
    f = IMPACT(size)
    tmp = Image.new("RGBA", (int(size * len(text) * 0.75) + 40, size + 40), (0, 0, 0, 0))
    d = ImageDraw.Draw(tmp)
    tw = d.textlength(text, font=f)
    d.rounded_rectangle((6, 6, tw + 34, size + 30), radius=6, outline=color + (220,), width=4)
    d.text((20, 16), text, font=f, fill=color + (220,))
    tmp = tmp.crop((0, 0, int(tw) + 42, size + 38)).rotate(angle, expand=True, resample=Image.BICUBIC)
    img.paste(tmp, (int(cx - tmp.width / 2), int(cy - tmp.height / 2)), tmp)

def sunburst(img, cx, cy, rays=24, color=PAPER_DARK):
    import math
    d = ImageDraw.Draw(img)
    r = max(img.size) * 2
    for i in range(0, rays, 2):
        a0, a1 = 2 * math.pi * i / rays, 2 * math.pi * (i + 1) / rays
        d.polygon([(cx, cy), (cx + r * math.cos(a0), cy + r * math.sin(a0)), (cx + r * math.cos(a1), cy + r * math.sin(a1))], fill=color)


# ---------------- pieces ----------------
def masthead():
    h = 440
    img = paper(W, h, 1)
    sunburst(img, 470, 250)
    img = Image.blend(img, paper(W, h, 1), 0.35)
    d = ImageDraw.Draw(img)
    # Top band
    d.rectangle((0, 0, W, 34), fill=INK)
    d.text((W / 2, 18), "DEPARTMENT OF CITY SURVEILLANCE  •  PUBLIC INFORMATION PAMPHLET No. TR-8", font=PLATE(14), fill=PAPER, anchor="mm")
    # Headline
    d.text((28, 62), "YOUR", font=IMPACT(84), fill=INK)
    d.text((28, 142), "CITY", font=IMPACT(84), fill=RED)
    d.text((28, 222), "NEEDS", font=IMPACT(84), fill=INK)
    d.text((28, 302), "YOU", font=IMPACT(84), fill=RED)
    d.text((196, 318), "TO PRESS ONE BUTTON", font=TYPE_B(17), fill=INK)
    d.text((196, 342), "AND ASK NO QUESTIONS.", font=TYPE_B(17), fill=INK)
    # The soldier at attention
    big = scaled(colonel, 6)
    img.paste(big, (470 - big.width // 2, 404 - big.height), big)
    d = ImageDraw.Draw(img)
    d.line((360, 404, 590, 404), fill=INK, width=3)
    # Bottom band
    d.rectangle((0, h - 34, W, h), fill=RED)
    d.text((W / 2, h - 17), "ENLIST IN THE DRONE OPERATOR TRAINING PROGRAM TODAY", font=PLATE(16), fill=PAPER, anchor="mm")
    stamp(img, "APPROVED", 300, 168, 26, 14)
    img.save(os.path.join(OUT, "masthead.png"))

SECTIONS = [
    ("1", "why_enlist", "WHY ENLIST?"),
    ("2", "duties", "YOUR DUTIES"),
    ("3", "spot", "HOW TO SPOT A THREAT"),
    ("4", "equipment", "FIELD EQUIPMENT"),
    ("5", "faq", "QUESTIONS (APPROVED)"),
    ("6", "support", "SUPPORT THE EFFORT"),
    ("7", "command", "CHAIN OF COMMAND"),
]

def sections():
    for num, key, title in SECTIONS:
        h = 64
        img = paper(W, h, int(num) + 10)
        d = ImageDraw.Draw(img)
        d.rectangle((0, 10, W, h - 10), fill=INK)
        d.rectangle((0, 10, 70, h - 10), fill=RED)
        d.text((35, h / 2), num, font=IMPACT(32), fill=PAPER, anchor="mm")
        d.text((88, h / 2 + 1), title, font=IMPACT(34), fill=PAPER, anchor="lm")
        d.text((W - 16, h / 2), "SECTION %s OF 7" % num, font=PLATE(12), fill=GOLD, anchor="rm")
        img.save(os.path.join(OUT, "section_%s_%s.png" % (num, key)))

def march():
    from PIL import ImageOps
    h = 320
    # Each row loops when frames * speed == spacing * count; both rows use the same 84 frames
    # (a multiple of the 6-frame walk). (scale, ground, spacing, count, speed)
    back_row, front_row = (2, 246, 84, 9, 9), (3, 312, 120, 7, 10)
    frames_n = 84
    walk_n = len(SOLDIER_WALK)

    bg = paper(W, h, 3)
    sunburst(bg, 556, 70, rays=28)
    bg = Image.blend(bg, paper(W, h, 3), 0.3)
    d = ImageDraw.Draw(bg)
    d.text((24, 12), "MARCH.", font=IMPACT(58), fill=INK)
    d.text((27, 84), "THE COLONEL IS WATCHING.", font=PLATE(15), fill=RED)
    d.line((0, back_row[1] + 1, W, back_row[1] + 1), fill=PAPER_DARK, width=2)

    # The colonel on his ledge, facing the troops
    ledge = Image.new("RGBA", (W, h), (0, 0, 0, 0))
    ld = ImageDraw.Draw(ledge)
    # Stone, not navy, so the troops' trousers still read in front of it
    rock = [(446, h), (456, 236), (482, 214), (604, 208), (W, 216), (W, h)]
    ld.polygon(rock, fill=(150, 128, 104), outline=(96, 80, 66), width=3)
    for y in range(236, h, 22):
        ld.line((470 + (y % 3) * 10, y, 520 + (y % 5) * 14, y + 4), fill=(122, 104, 86), width=2)
    ld.line((482, 214, 604, 208), fill=(190, 170, 140), width=3)
    boss = ImageOps.mirror(scaled(colonel, 3))
    ledge.paste(boss, (556 - boss.width // 2, 211 - boss.height), boss)

    faded = [Image.blend(fr, Image.new("RGBA", fr.size, PAPER + (255,)), 0.35) for fr in SOLDIER_WALK]
    for fr, src in zip(faded, SOLDIER_WALK):
        fr.putalpha(src.getchannel("A"))

    def row(img, t, walk, params):
        k, ground, spacing, count, speed = params
        loop = spacing * count
        for i in range(count):
            x = (i * spacing + t * speed) % loop - 32 * k
            fr = scaled(walk[t % walk_n], k)
            img.paste(fr, (x, ground - fr.height + 1), fr)

    frames = []
    for t in range(frames_n):
        img = bg.copy()
        row(img, t, faded, back_row)
        img.paste(ledge, (0, 0), ledge)
        dd = ImageDraw.Draw(img)
        dd.line((0, front_row[1] + 1, W, front_row[1] + 1), fill=INK, width=3)
        row(img, t, SOLDIER_WALK, front_row)
        # Calls the cadence in step with the march
        call = "LEFT!" if t % walk_n < walk_n // 2 else "RIGHT!"
        dd = ImageDraw.Draw(img)
        bx0, by0, bx1, by1 = 404, 48, 516, 90
        dd.rounded_rectangle((bx0, by0, bx1, by1), radius=10, fill=PAPER, outline=INK, width=3)
        dd.polygon([(bx1 - 4, 62), (bx1 + 16, 70), (bx1 - 4, 76)], fill=PAPER)
        dd.line((bx1 - 2, 62, bx1 + 16, 70, bx1 - 2, 77), fill=INK, width=3)
        dd.text(((bx0 + bx1) / 2, (by0 + by1) / 2), call, font=IMPACT(26), fill=RED, anchor="mm")
        frames.append(img.convert("P", palette=Image.ADAPTIVE, colors=64))
    frames[0].save(os.path.join(OUT, "march.gif"), save_all=True, append_images=frames[1:], duration=WALK_MS, loop=0)

def report_poster():
    h = 400
    img = paper(W, h, 4)
    d = ImageDraw.Draw(img)
    d.rectangle((10, 10, W - 10, h - 10), outline=INK, width=3)
    d.rectangle((18, 18, W - 18, h - 18), outline=INK, width=1)
    d.text((W / 2, 52), "HAVE YOU SEEN THIS CITIZEN?", font=IMPACT(40), fill=RED, anchor="mm")
    # Mugshot with height lines
    mx, my, mw, mh = 40, 92, 200, 270
    d.rectangle((mx, my, mx + mw, my + mh), fill=(222, 210, 180), outline=INK, width=2)
    for i in range(1, 9):
        y = my + mh - i * 30
        d.line((mx, y, mx + 14, y), fill=INK, width=1)
        d.text((mx + 18, y), "%d'" % (i // 2 + 3) if i % 2 == 0 else "", font=TYPE(10), fill=INK, anchor="lm")
    # The suspect is the game's base pedestrian (red shirt, on his phone)
    suspect = Image.open(BASE_WALK).convert("RGBA").crop((0, 0, 32, 64))
    big = scaled(suspect, 4)
    img.paste(big, (mx + mw // 2 - big.width // 2 + 4, my + mh - big.height - 34), big)
    d = ImageDraw.Draw(img)
    d.rectangle((mx + 40, my + mh - 30, mx + mw - 40, my + mh - 8), fill=INK)
    d.text((mx + mw / 2, my + mh - 19), "SUBJECT R-457", font=IMPACT(17), fill=PAPER, anchor="mm")
    # Warning signs
    x = 268
    d.text((x, 104), "KNOWN WARNING SIGNS:", font=TYPE_B(18), fill=INK)
    signs = ["Wears a shirt", "Stares at his phone (filming us?)", "Walks in public", "Has legs, most likely two",
             "Is not wearing Agency sunglasses"]
    for i, s in enumerate(signs):
        y = 140 + i * 30
        d.rectangle((x, y, x + 16, y + 16), outline=INK, width=2)
        d.line((x + 3, y + 8, x + 7, y + 13), fill=RED, width=3)
        d.line((x + 7, y + 13, x + 16, y - 2), fill=RED, width=3)
        d.text((x + 26, y + 8), s, font=TYPE(15), fill=INK, anchor="lm")
    d.text((x, 306), "IF YOU SEE SOMETHING,", font=IMPACT(28), fill=INK)
    d.text((x, 338), "PRESS SOMETHING.", font=IMPACT(28), fill=RED)
    stamp(img, "SUSPICIOUS", 150, 286, 22, -12)
    img.save(os.path.join(OUT, "report_poster.png"))

def equipment():
    h = 210
    img = paper(W, h, 5)
    d = ImageDraw.Draw(img)

    def key(x, y, kw, label):
        d.rounded_rectangle((x, y + 4, x + kw, y + 54), radius=7, fill=INK)
        d.rounded_rectangle((x, y, x + kw, y + 48), radius=7, fill=PAPER, outline=INK, width=3)
        d.text((x + kw / 2, y + 24), label, font=IMPACT(24), fill=INK, anchor="mm")

    def item(y, ref, text):
        d.text((W - 24, y + 14), ref, font=PLATE(12), fill=GOLD, anchor="ra")
        d.text((W - 24, y + 30), text, font=TYPE_B(17), fill=INK, anchor="ra")

    key(24, 22, 230, "SPACE")
    d.text((268, 46), "/ CLICK / TAP", font=TYPE_B(17), fill=INK, anchor="lm")
    item(18, "ITEM 01", "THE BUTTON")
    key(24, 116, 54, "A")
    key(86, 116, 54, "D")
    d.text((154, 140), "or", font=TYPE(16), fill=INK, anchor="lm")
    key(184, 116, 54, "←")
    key(246, 116, 54, "→")
    item(112, "ITEM 02 (RC CAR ONLY)", "STEERING")
    d.line((24, 96, W - 24, 96), fill=INK, width=1)
    d.text((W / 2, h - 16), "Equipment remains property of the Agency. So do you.", font=TYPE(13), fill=INK, anchor="mm")
    img.save(os.path.join(OUT, "equipment.png"))

def war_bond():
    w, h = 420, 110
    img = paper(w, h, 6)
    d = ImageDraw.Draw(img)
    d.rectangle((4, 4, w - 5, h - 5), outline=GOLD, width=3)
    d.rectangle((11, 11, w - 12, h - 12), outline=INK, width=1)
    d.text((w / 2, 42), "BUY A WAR BOND", font=IMPACT(36), fill=RED, anchor="mm")
    d.text((w / 2, 78), "(it's a coffee on Ko-fi)", font=TYPE_B(17), fill=INK, anchor="mm")
    for cx in (34, w - 34):
        d.ellipse((cx - 16, h / 2 - 16, cx + 16, h / 2 + 16), outline=GOLD, width=3)
        d.text((cx, h / 2), "$", font=IMPACT(20), fill=GOLD, anchor="mm")
    img.save(os.path.join(OUT, "war_bond.png"))

def fine_print():
    h = 74
    img = paper(W, h, 7)
    d = ImageDraw.Draw(img)
    d.line((24, 12, W - 24, 12), fill=INK, width=1)
    lines = ["This pamphlet is a work of satire. No real monitos were harmed.",
             "The Agency is not real. The Agency would like you to stop asking.",
             "PRINTED BY THE DEPARTMENT OF CITY SURVEILLANCE  •  FORM GOOD CITIZENS  •  KEEP FOR YOUR RECORDS"]
    for i, s in enumerate(lines):
        f = PLATE(10) if i == 2 else TYPE(12)
        d.text((W / 2, 26 + i * 17), s, font=f, fill=INK, anchor="mm")
    img.save(os.path.join(OUT, "fine_print.png"))


if __name__ == "__main__":
    OUT = os.path.join(sys.argv[1], "itch", "pamphlet")
    SPRITES = os.path.join(OUT, "sprites")
    os.makedirs(SPRITES, exist_ok=True)
    sprites()
    masthead()
    sections()
    march()
    report_poster()
    equipment()
    war_bond()
    fine_print()
    print("done:", OUT)
