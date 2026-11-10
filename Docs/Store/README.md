# Agent TR8N1: página de itch.io y Ko-fi

Todo lo de esta carpeta sale de las grabaciones del trailer. Para regenerarlo después de grabar de nuevo:

```
venv/bin/python Docs/Store/tools/make_store_assets.py "/Volumes/TOSHIBA EXT/AgentTR8N1-trailer-work-v6" Docs/Store
```

## Paleta (para *Edit theme*)

| Uso | Color |
|---|---|
| Fondo de la página | `#08060C` |
| Fondo del contenido | `#110E18` |
| Texto | `#EBEBEB` |
| Links / botones | `#FFD23F` (amarillo) |
| Títulos / acentos | `#FF4040` (rojo) |
| Detalles "CCTV" | `#5AFF8C` (verde) |

Tipografía sugerida: títulos en algo grueso tipo Impact/Anton (en itch: **Anton** u **Oswald**), texto en monoespaciada (**Courier Prime** o **Space Mono**).

## itch.io

1. **Edit game**
   - *Kind of project*: HTML (cuando subas el WebGL) o Downloadable.
   - *Pricing*: **$0 or donate**, con *Suggested donation* (por ejemplo $2).
   - *Cover image*: `itch/cover.png` (630×500).
   - *Gameplay video*: link de YouTube del trailer.
   - *Screenshots*: los `itch/gif_*.gif` y los `itch/shot_*.png` (los GIFs primero).
   - *Description*: pega `itch_description.html` en el editor en modo HTML y cambia `YOUR_KOFI`.
2. **Edit theme** (en la página del juego, arriba)
   - *Banner*: `itch/banner.png`.
   - *Background*: `itch/background.png`, en modo repetir.
   - Colores y tipografías de la tabla.
3. Para el CSS personalizado hay que pedírselo a soporte de itch.

## Ko-fi

1. *Profile picture*: `kofi/avatar.png`.
2. *Cover / header image*: `kofi/cover.png` (1600×400; si Ko-fi lo recorta, avísame y lo ajusto a su medida).
3. *Theme color*: `#FFD23F` (amarillo) o `#FF4040` (rojo).
4. *About* y textos opcionales: `kofi_about.txt`.
5. Pon el link de itch en Ko-fi y el de Ko-fi en itch.
