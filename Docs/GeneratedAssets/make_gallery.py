"""Builds index.html: a gallery of every asset the game generates in code (open it in a browser).
Run after exporting from Unity (Tools > Export generated assets):  python3 Docs/GeneratedAssets/make_gallery.py"""
import html, os

HERE = os.path.dirname(os.path.abspath(__file__))
rows = [l.rstrip("\n").split("\t") for l in open(os.path.join(HERE, "index.tsv"))][1:]
used_by = {r[0]: (r[1], r[2]) for r in rows}


def card(path, label, note, pixelated=True):
    preview = path
    x8 = os.path.join(os.path.dirname(path), "x8", os.path.basename(path))
    if os.path.exists(os.path.join(HERE, x8)):
        preview = x8
    style = "image-rendering: pixelated;" if pixelated else ""
    return (f'<figure><a href="{preview}"><img src="{preview}" style="{style}" loading="lazy"></a>'
            f'<figcaption><b>{html.escape(label)}</b><br><span>{html.escape(note)}</span><br>'
            f'<a href="{path}">original</a>{" · <a href=%s>x8</a>" % x8 if preview == x8 else ""}</figcaption></figure>')


def section(title, folder, pixelated=True, intro=""):
    files = sorted(f for f in os.listdir(os.path.join(HERE, folder)) if f.endswith(".png"))
    cards = []
    for f in files:
        path = f"{folder}/{f}"
        size, gen = used_by.get(path, ("", ""))
        cards.append(card(path, f[:-4], f"{size}  {gen}".strip(), pixelated))
    return f"<h2>{title}</h2><p>{intro}</p><div class=grid>{''.join(cards)}</div>"


sounds = "".join(
    f'<figure class=snd><b>{f[:-4]}</b><audio controls src="sfx/{f}"></audio><a href="sfx/{f}">.wav</a></figure>'
    for f in sorted(os.listdir(os.path.join(HERE, "sfx"))) if f.endswith(".wav"))

page = f"""<!doctype html><html lang=es><head><meta charset=utf-8><meta name=viewport content="width=device-width,initial-scale=1">
<title>Agent TR8N1 Assets</title><style>
:root {{ --bg:#14121a; --card:#201d29; --fg:#eee; --dim:#9a96a8; --accent:#ffd23f; }}
body {{ background:var(--bg); color:var(--fg); font:15px/1.45 system-ui,sans-serif; margin:0 auto; max-width:1200px; padding:24px 16px; }}
h1 {{ color:var(--accent); margin:0 0 4px; }} h2 {{ margin-top:40px; border-bottom:2px solid #333; padding-bottom:6px; }}
p {{ color:var(--dim); }} a {{ color:var(--accent); }}
.grid {{ display:grid; grid-template-columns:repeat(auto-fill,minmax(200px,1fr)); gap:14px; }}
figure {{ background:var(--card); border-radius:8px; margin:0; padding:10px; }}
figure img {{ width:100%; height:180px; object-fit:contain; background:repeating-conic-gradient(#2a2733 0 25%,#1d1a24 0 50%) 0 0/16px 16px; border-radius:4px; }}
figcaption {{ font-size:12px; margin-top:6px; word-break:break-word; }} figcaption span {{ color:var(--dim); }}
.snd {{ display:flex; flex-direction:column; gap:6px; }} audio {{ width:100%; }}
</style></head><body>
<h1>AGENT TR8N1 — assets generados en código</h1>
<p>Todo lo que el juego dibuja o sintetiza por código, exportado para pintarle encima o reemplazarlo.
Cada imagen está en su tamaño real (<i>original</i>) y ampliada 8× pixel por pixel (<i>x8</i>), que es la cómoda para editar.
Debajo de cada una dice qué función la genera. Para reemplazar un asset en el juego hay que conectarlo en el script correspondiente
(los sonidos ya se pueden reemplazar desde el componente <code>SoundFX</code>).</p>
{section("Capturas de cada minijuego", "stage-references", False, "Referencia de cómo se ve cada nivel en el teléfono. Los sets del elevador y los drones están hechos con rectángulos en código (StageBuilder): son los mejores candidatos para arte nuevo.")}
{section("Sprites y texturas", "sprites")}
{section("Fondos de caminata (detrás del teléfono)", "walk-backgrounds", True, "128×128, se repiten verticalmente mientras caminas.")}
<h2>Efectos de sonido</h2><p>Sintetizados con el algoritmo de sfxr/bfxr (RuntimeSounds.cs). Se pueden reemplazar con .wav de bfxr.net.</p>
<div class=grid>{sounds}</div>
</body></html>"""

open(os.path.join(HERE, "index.html"), "w").write(page)
print("index.html:", len(rows), "assets")
