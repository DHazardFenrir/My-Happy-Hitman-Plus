# Arte y prefabs (cómo está armado ahora)

> [!summary]
> El arte del juego son **PNG dentro de Unity** y los **8 minijuegos son prefabs** registrados en `GameManager.minigamePrefabs`. Los scripts solo tienen la lógica. En código solo se genera lo que cambia en cada partida o se acomoda a la pantalla.

> [!note] Wiki completa
> La wiki del proyecto vive en el vault de Obsidian `Youtube/Wiki`. Esta nota es una copia de su nota *Arte y prefabs*.

## Dónde está cada cosa

| Qué | Dónde | Se edita con |
|---|---|---|
| Sprites (drones, mira, productos del anuncio, hotel, perros, sangre...) | `Assets/Resources/Sprites/*.png` | Aseprite (clic derecho ▸ *Open in Aseprite*) |
| Fondos de caminata (calle, cruce, obra, alcantarilla, hotel) | `Assets/Resources/Sprites/Walk/*.png` | Aseprite (128×128, se repiten en vertical) |
| Fondos pintables de escenarios (plaza, noche, perros) | `Assets/Art/StageBackdrops/*_back.png` / `*_front.png` | Aseprite |
| Escenarios de los minijuegos | `Assets/Prefabs/---- … MINI GAME … ----.prefab` | Unity, a mano |
| Lista de minijuegos | `GameManager` en `SampleScene` ▸ *Minigame Prefabs* | Inspector (arrastrar el prefab) |
| Música (feliz → terror según el puntaje) | `GameManager` ▸ *Happy Music* / *Terror Music* (clips en `Assets/Test_Musica`) | Inspector |
| Vidrio roto de la muerte IRL | `Scripts/ProceduralTextures.cs` | Código (aleatorio en cada muerte) |
| HUD del dron, multitud, mira, outlines nocturnos, tráfico del carro RC | `Scripts/Stages/*.cs` | Código (se adaptan a la pantalla o cambian cada ronda) |

## Los 8 minijuegos

| # | Prefab | Script | Posición X |
|---|---|---|---|
| 1 | `---- FIRST MINI GAME ----` | `LightTrafficStage` | (la del prefab) |
| 2 | `---- SECOND MINI GAME ---` | `SewerStage` | (la del prefab) |
| 3 | `---- THIRD MINI GAME ---` | `PianoDropStage` | (la del prefab) |
| 4 | `---- FOURTH MINI GAME (elevator) ----` | `ElevatorStage` | 60 |
| 5 | `---- FIFTH MINI GAME (drone plaza) ----` | `PlazaDroneStage` | 80 |
| 6 | `---- SIXTH MINI GAME (drone night) ----` | `NightDroneStage` | 100 |
| 7 | `---- SEVENTH MINI GAME (rc car) ----` | `RcCarStage` | 120 |
| 8 | `---- EIGHTH MINI GAME (guard dogs) ----` | `DogFenceStage` | 140 |

Los minijuegos 4 a 6 se editan solo a mano. Los minijuegos 7 y 8 todavía se pueden regenerar desde código (ver herramientas abajo).

## Editar un sprite

1. Clic derecho en el PNG (en `Assets/Resources/Sprites`) ▸ **Open in Aseprite**.
2. Píntalo **del mismo tamaño en pixeles** (o cambia *Pixels Per Unit* en el Inspector si lo agrandas).
3. Guarda. Al volver a Unity se reimporta solo.

Los ajustes de importación ya están puestos: *Point (no filter)*, sin compresión, *Mesh Type: Full Rect* (para que se puedan repetir en modo Tiled). Si agregas un PNG nuevo, copia esos ajustes.

El código pide los sprites por nombre: `GameSprites.Crosshair()`, `GameSprites.Dog(0)`, `GameSprites.BloodSplash(2)`... (ver `Scripts/GameSprites.cs`). Para agregar uno, pon el PNG en esa carpeta y agrega una línea `public static Sprite MiSprite() => Get("mi_sprite");`. Los prefabs no necesitan esto: arrastra el PNG directo al SpriteRenderer.

> [!tip] Sangre
> Hay 4 manchas (`bloodsplash0..3.png`) y en cada muerte sale una al azar. Para agregar otra, crea `bloodsplash4.png` y sube `GameSprites.BloodSplashCount`. Las 4 se abren juntas con **Tools ▸ Agent TR8N1 ▸ Open blood splatters in Aseprite**.

## Editar un escenario

Abre el prefab (doble clic) y mueve, cambia o agrega objetos como en cualquier escena. Hay que respetar estas reglas:

- **Partes que se mueven:** van asignadas en el componente del minijuego: `Cab` / `Lever` / `Needle` del elevador, la reja y los perros, etc. No las borres. Si las reemplazas, vuelve a asignarlas en el Inspector.
- **Elevador:** si mueves el hueco, cambia también `Shaft Center X` / `Shaft Width` (la trampa usa esos valores). El piso está en `y = -3`.
- **Drones:** el **rectángulo amarillo** (gizmo) es la parte del celular que se ve en un monitor 16:9; lo de abajo queda tapado. `Crowd Front Row (feet)` marca dónde pisa la primera fila de la multitud (las otras dos van atrás, 0.3 más arriba cada una). Los fondos son más grandes que la vista a propósito, para que no queden huecos en otras resoluciones.
- **Orden de dibujo (Sorting Order):** fondo `-12..-4`, gente `3..5`, oscuridad nocturna `2`, filtro de cámara `26..28`, HUD `30+`.

## Agregar un minijuego nuevo

1. Crea un script que herede de `WarioStage` e implementa `OnStageUpdate()`. Implementa también `TryGetTrapAim()` si quieres que el autopilot lo juegue.
2. Arma el prefab:
   - La raíz con el script, `Spawn Point`, `Focus Camera` y `Npc Prefab` = `Monito`.
   - El escenario.
   - Ponlo lejos de los demás en X: el siguiente libre es **160**.
3. Arrástralo a *Minigame Prefabs* del `GameManager`.

## Herramientas del editor (menú Tools ▸ Agent TR8N1)

Todo esto vive en `Assets/Editor` y no entra en la build del juego.

| Menú | Archivo | Qué hace |
|---|---|---|
| **Bake missing sprites** | `Editor/Art/ArtBaker.cs` | Vuelve a crear los PNG que falten a partir del dibujo original en código (`SpritePainter.cs`, `WalkThemePainter.cs`). **Nunca sobreescribe** un PNG existente: para recuperar el original de uno, bórralo y corre esto. |
| **Bake code stages to prefabs** | `Editor/Art/StageSetBaker.cs` | Crea los prefabs del carro RC y de los perros si no existen. |
| **Rebuild RC car stage** / **Rebuild guard dogs stage** | `Editor/Art/StageSetBaker.cs` | Regenera ese prefab desde código. **Se pierden los cambios hechos a mano** en ese prefab. |
| **Register stage prefabs in open scene** | `Editor/Art/StageSetBaker.cs` | Agrega los prefabs 4 a 8 a la lista del `GameManager` si no están. |
| **Bake stage backdrops to PNG** | `Editor/Art/BackdropBaker.cs` | Junta las piezas fijas de un escenario (rectángulos, texturas, letreros) en un PNG para pintar encima y las reemplaza por ese sprite. Lo que se mueve no se toca. Necesita `SampleScene` abierta. |
| **Open blood splatters in Aseprite** / **Choose Aseprite app...** | `Editor/AsepriteOpener.cs` | Abre archivos en Aseprite (también desde el clic derecho ▸ *Open in Aseprite*). |
| Tools ▸ Export generated assets | `Editor/GeneratedAssetsExport.cs` | Copia todos los PNG (más versiones x8) y los sonidos a `Docs/GeneratedAssets` para Luis. |
| Tools ▸ Build macOS (trailer) | `Editor/TrailerBuild.cs` | Build para grabar el trailer con el autopilot. |

Por línea de comandos (con el editor cerrado o en una copia del proyecto), esto hornea los sprites que falten, crea los prefabs que falten y los registra en la escena:

```bash
Unity -batchmode -quit -projectPath . -executeMethod ArtBaker.MigrateAll
```

## Registro de cambios

### Migración a PNG y prefabs

- `RuntimeSprites`, que pintaba los sprites pixel por pixel cada vez que arrancaba el juego, se dividió en tres:
  - `GameSprites` (runtime): carga los PNG.
  - `SpritePainter` (solo editor): el dibujo original, por si quieres regenerar uno.
  - `ProceduralTextures` (runtime): el vidrio roto.
- `WalkThemeTextures` ahora carga los PNG. El dibujo pasó a `Editor/Art/WalkThemePainter.cs`.
- Se quitaron del runtime `ElevatorStage.BuildRoom()`, `PlazaDroneStage.BuildSet()` y `NightDroneStage.BuildSet()`. El escenario vive en los prefabs.
- `GameManager` ya no tiene `AddCodeStage`, `includeElevatorStage` ni `includeDroneStages`. Todos los minijuegos se registran en el Inspector.

### Limpieza

- Quité de `StageSetBaker` los constructores del elevador y los drones (`BuildElevator`, `BuildDroneStage`, `BuildPlaza`, `BuildNight`). Ya se habían usado una vez para crear los prefabs y esos prefabs ya se editaron a mano (los drones ya usan fondos PNG).
- La sangre de la muerte IRL ya no se genera en código: son los PNG `bloodsplash0..3`. La función vieja de `ProceduralTextures` se eliminó.
- `ElevatorStage.FloorY` / `CabHeight` volvieron a ser privados (ya nada de fuera los usa).
- `TrailerCapture` ya no lanza un error al cerrar el juego después de grabar.
- Revisé con un script todos los scripts de `Assets/Scripts` y `Assets/Editor`: no queda ninguno sin usar. Cada uno lo usa otro script, un prefab, la escena o un menú del editor.
