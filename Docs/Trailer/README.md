# Agent TR8N1 — trailer

- `AgentTR8N1_Trailer.mp4` — 92 s, 1920x1080, 30 fps. Estilo anuncio de reclutamiento ("YOUR CITY NEEDS YOU"), marcha chiptune que se corta cuando los drones disparan.
- `AgentTR8N1_Trailer_v2.mp4` — 113 s. Agrega el carro RC ("NEW: GROUND UNITS") y los perros ("NEW: COMMUNITY K-9 PROGRAM"), el THREAT ALERT, el nuevo final. Música: la marcha chiptune.
- `sfx/` — los efectos del juego exportados a WAV (generados con el sintetizador sfxr/bfxr de `RuntimeSounds.cs`).

## Volver a grabar y editar

1. Compilar el juego (desde Unity: *Tools > Build macOS (trailer)*, o en terminal con el editor cerrado):
   `Unity -batchmode -quit -projectPath . -executeMethod TrailerBuild.BuildMac -buildPath Builds/AgentTR8N1.app`
2. Grabar con el piloto automático:
   - Toma A (aciertos + final): `"Builds/AgentTR8N1.app/Contents/MacOS/Agent TR8N1" -autopilot -missChance 0.2 -capture cap/capA -screen-fullscreen 0 -screen-width 1920 -screen-height 1080`
   - Toma B (fallos, anuncio, muerte IRL): igual con `-missChance 1 -failsForIrl 5 -capture cap/capB` (cerrarla después de la muerte IRL).
   - Toma C (cada fallo te mata; se usa la muerte atropellado): `-missChance 1 -failsForIrl 0 -capture cap/capC` (cerrarla después de una muerte en el minijuego del semáforo).
   - Opcional, toma D: otra de aciertos, por si algún minijuego no salió en la A.
3. Editar (necesita `ffmpeg`, `pillow`, `numpy`; p. ej. `python3 -m venv venv && venv/bin/pip install numpy pillow`): `venv/bin/python Docs/Trailer/tools/build_trailer.py cap Docs/Trailer/AgentTR8N1_Trailer_v2.mp4`
   Música: la marcha chiptune sintetizada; con `--composer` usa los tracks del juego de `Assets/Test_Musica` (Happy → Terror).
   Los clips se eligen solos a partir de `events.txt` de cada toma; los textos y el orden están en `TIMELINE` dentro de `build_trailer.py`.
   Las grabaciones pesan ~1.5 GB: guárdalas en el disco externo (las de la v1 están en `/Volumes/TOSHIBA EXT/AgentTR8N1-trailer-work` y las de la v2 en `...-work-v2`).
