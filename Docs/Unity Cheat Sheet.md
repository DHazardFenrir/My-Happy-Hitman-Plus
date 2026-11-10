---
title: Unity Cheat Sheet
aliases:
  - Unity referencia
  - Hoja de Unity
tags:
  - unity
  - gamedev
  - csharp
  - my-happy-hitman
proyecto: My Happy Hitman Plus
unity: 2023.1.22f1
---

# Unity Cheat Sheet

> [!info] Cómo usar esta hoja
> Está pensada para alguien que **ya sabe C#** pero anda oxidado con Unity. Cada sección tiene:
> - lo mínimo que hay que saber,
> - un ejemplo **sacado de My Happy Hitman Plus** (con el archivo donde está, para verlo funcionando),
> - las trampas típicas (muchas son bugs reales que tuvo el proyecto),
> - links a la documentación oficial.
>
> No hay que memorizar nada. La idea es saber **qué clase buscar** y luego leer su página.

## Índice
1. [[#Cómo descubrir el motor]]
2. [[#Ciclo de vida de un MonoBehaviour]]
3. [[#Corrutinas]]
4. [[#GameObject y Transform]]
5. [[#Instantiate, Destroy y prefabs]]
6. [[#El Inspector]]
7. [[#Tiempo e input]]
8. [[#Matemáticas]]
9. [[#Sprites y orden de dibujo]]
10. [[#Física 2D]]
11. [[#Cámaras y RenderTexture]]
12. [[#UI (uGUI)]]
13. [[#TextMeshPro]]
14. [[#Texturas y sprites por código]]
15. [[#Animación por código]]
16. [[#Patrones que usa el proyecto]]
17. [[#Depuración]]
18. [[#Mapa del proyecto]]
19. [[#Recursos para estudiar]]
20. [[#Plan de estudio]]

---

## Cómo descubrir el motor

Cuando quieras hacer algo ("que el texto quepa solo", "que el piano caiga"), no lo busques como pregunta general:

1. Piensa **qué componente lo haría**: un texto → `TextMeshProUGUI`, algo que cae → `Rigidbody2D`.
2. Escribe `miComponente.` en el editor y deja que el autocompletado te muestre todo lo que tiene.
3. Lee los nombres; casi siempre se explican solos (`enableAutoSizing`, `gravityScale`).
4. Si no queda claro, abre su página en el **[Scripting API](https://docs.unity3d.com/ScriptReference/index.html)**.

> [!tip] Autocompletado en VS Code
> Instala la extensión **[Unity para VS Code](https://marketplace.visualstudio.com/items?itemName=visualstudiotoolsforunity.vstuc)** y en Unity ve a *Preferences → External Tools → External Script Editor → Visual Studio Code*. Así ves cada función con su descripción mientras escribes.

**Las 17 clases que cubren casi todo el proyecto:**

| Clase | Para qué | Docs |
|---|---|---|
| `GameObject` | El objeto en sí | [link](https://docs.unity3d.com/ScriptReference/GameObject.html) |
| `Transform` | Posición, rotación, escala, jerarquía | [link](https://docs.unity3d.com/ScriptReference/Transform.html) |
| `MonoBehaviour` | Tus scripts | [link](https://docs.unity3d.com/ScriptReference/MonoBehaviour.html) |
| `Time` | El tiempo | [link](https://docs.unity3d.com/ScriptReference/Time.html) |
| `Input` | Teclado, mouse, touch | [link](https://docs.unity3d.com/ScriptReference/Input.html) |
| `Mathf` | Matemáticas | [link](https://docs.unity3d.com/ScriptReference/Mathf.html) |
| `Vector3` / `Vector2` | Posiciones y direcciones | [link](https://docs.unity3d.com/ScriptReference/Vector3.html) |
| `Random` | Azar | [link](https://docs.unity3d.com/ScriptReference/Random.html) |
| `SpriteRenderer` | Dibujar sprites | [link](https://docs.unity3d.com/ScriptReference/SpriteRenderer.html) |
| `Rigidbody2D` | Física | [link](https://docs.unity3d.com/ScriptReference/Rigidbody2D.html) |
| `Collider2D` | Colisiones | [link](https://docs.unity3d.com/ScriptReference/Collider2D.html) |
| `Physics2D` | Consultas de física | [link](https://docs.unity3d.com/ScriptReference/Physics2D.html) |
| `Camera` | Cámaras | [link](https://docs.unity3d.com/ScriptReference/Camera.html) |
| `RectTransform` | Posición en la UI | [link](https://docs.unity3d.com/ScriptReference/RectTransform.html) |
| `Image` / `RawImage` / `Button` | UI | [Image](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.Image.html) · [RawImage](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.RawImage.html) · [Button](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.Button.html) |
| `TextMeshProUGUI` | Textos | [link](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/api/TMPro.TMP_Text.html) |
| `Texture2D` / `Sprite` | Imágenes generadas | [Texture2D](https://docs.unity3d.com/ScriptReference/Texture2D.html) · [Sprite.Create](https://docs.unity3d.com/ScriptReference/Sprite.Create.html) |

---

## Ciclo de vida de un MonoBehaviour

Unity llama tus métodos; tú no controlas el `main`. Lo que más se usa:

| Método | Cuándo se llama | Úsalo para |
|---|---|---|
| `Awake()` | Al crear el objeto, aunque el script esté desactivado | Inicializar referencias propias |
| `OnEnable()` | Cada vez que se activa | Suscribirse a eventos |
| `Start()` | Antes del primer `Update`, si está activo | Cosas que dependen de otros objetos ya inicializados |
| `Update()` | Cada frame | Input, lógica, movimiento con `Time.deltaTime` |
| `FixedUpdate()` | A intervalo fijo (física) | Mover `Rigidbody2D` |
| `OnDisable()` / `OnDestroy()` | Al desactivar / destruir | Limpiar |

```csharp
// GameManager.cs: Awake crea componentes, Start construye la UI (cuando ya existe todo)
void Awake()
{
    Instance = this;
    ui = gameObject.AddComponent<GameUI>();
}

void Start()
{
    ui.Build(mainCanvas, gameScreen, uiFont, StartGame);
    ShowStartMenu();
}
```

> [!warning] Trampa
> El orden entre **scripts distintos** no está garantizado. El `SingleInput` original guardaba el input en `Update`, y otros scripts lo leían a veces antes y a veces después. La solución fue calcularlo al momento (`SingleInput.cs`).

📖 [Order of execution for event functions](https://docs.unity3d.com/Manual/ExecutionOrder.html)

---

## Corrutinas

Una función que se puede pausar y continuar en otro frame. En Unity casi todo lo que "tarda en el tiempo" es una corrutina.

```csharp
IEnumerator Ejemplo()
{
    yield return null;                         // espera 1 frame
    yield return new WaitForSeconds(2f);       // espera 2 segundos
    yield return new WaitUntil(() => listo);   // espera hasta que la condición sea true
    yield return OtraCorrutina();              // espera a que termine otra
}

StartCoroutine(Ejemplo());
```

**En el proyecto:** todo el juego es una corrutina (`GameManager.GameLoop`):

```csharp
yield return ui.ShowTargetCard(...);                       // 1. card del objetivo
WarioStage stage = Instantiate(prefab);                    // 2. minijuego
stage.StartStage();
yield return new WaitUntil(() => stage.isFinished);        // 3. espera a que acabe
yield return ui.ShowResultCard(success, subtitle, 1.6f);   // 4. letrero de neón
```

> [!warning] Trampas reales
> - `SetActive(false)` **detiene todas las corrutinas** de ese objeto.
> - `StartCoroutine` en un objeto **inactivo** falla. En `NPCWalker.PopIn()` se hace `SetActive(true)` antes de arrancarla.
> - `StopAllCoroutines()` también detiene la corrutina desde la que lo llamas (pasa en `WarioStage.EndMinigame`).

📖 [Coroutines](https://docs.unity3d.com/Manual/Coroutines.html) · [WaitForSeconds](https://docs.unity3d.com/ScriptReference/WaitForSeconds.html) · [WaitUntil](https://docs.unity3d.com/ScriptReference/WaitUntil.html) · [StartCoroutine](https://docs.unity3d.com/ScriptReference/MonoBehaviour.StartCoroutine.html)

---

## GameObject y Transform

```csharp
GameObject go = new GameObject("Nombre");      // crear vacío
go.AddComponent<SpriteRenderer>();             // agregar componente
var sr = go.GetComponent<SpriteRenderer>();    // obtener componente
if (go.TryGetComponent(out NPCWalker npc)) { } // obtener solo si existe
go.SetActive(false);                           // apagar (deja de dibujarse y de correr Update)

Transform t = go.transform;
t.position = new Vector3(0, 1, 0);       // posición en el mundo
t.localPosition = Vector3.zero;          // relativa al padre
t.localScale = new Vector3(2, 2, 1);
t.localRotation = Quaternion.Euler(0, 0, 45);
t.SetParent(otroTransform, false);       // false = conserva la posición local
Vector3 mundo = t.TransformPoint(new Vector3(1, 0, 0)); // de local a mundo
```

**En el proyecto:** `ElevatorStage.BuildRoom()` arma todo el pasillo del hotel así: crea objetos vacíos, les agrega `SpriteRenderer`, los emparenta al stage y los posiciona.

> [!tip] Voltear un sprite
> `NPCWalker.SetWalkDirection` voltea al monito con `localScale.x` negativo cuando camina a la izquierda.

---

## Instantiate, Destroy y prefabs

```csharp
WarioStage stage = Instantiate(prefab);                     // clonar
GameObject npc = Instantiate(npcPrefab, pos, Quaternion.identity, transform); // con padre
Destroy(stage.gameObject);                                  // destruir (al final del frame)
Destroy(gameObject, 0.5f);                                  // destruir en 0.5 s
```

> [!bug] Bug real del proyecto
> Los monitos se creaban **sin padre**, así que al destruir el minijuego seguían existiendo y se acumulaban ronda tras ronda. Ahora se crean como hijos del stage (`WarioStage.SpawnNPC`) y se destruyen con él.

📖 [Instantiate](https://docs.unity3d.com/ScriptReference/Object.Instantiate.html) · [Destroy](https://docs.unity3d.com/ScriptReference/Object.Destroy.html) · [Prefabs](https://docs.unity3d.com/Manual/Prefabs.html)

---

## El Inspector

```csharp
public float speed = 12f;                       // público = aparece en el Inspector
[SerializeField] private float hitWidth = 0.8f; // privado pero visible en el Inspector

[Header("Fails")]                               // título de sección
[Tooltip("Show the retro ad every this many fails")]
public int adEveryFails = 5;

[Range(0.1f, 1f)] public float hitWidth = 0.8f; // slider
```

> [!warning] La trampa número uno al volver a Unity
> **El valor del Inspector gana sobre el del código.** Si cambias `= 12f` en el script y el prefab ya tenía guardado otro número, se usa el del prefab. Revisa siempre el Inspector.

📖 [SerializeField](https://docs.unity3d.com/ScriptReference/SerializeField.html) · [Header](https://docs.unity3d.com/ScriptReference/HeaderAttribute.html) · [Tooltip](https://docs.unity3d.com/ScriptReference/TooltipAttribute.html)

---

## Tiempo e input

```csharp
transform.Translate(dir * speed * Time.deltaTime); // movimiento independiente de los FPS
float t = Time.time;                               // segundos desde que empezó el juego

Input.GetKeyDown(KeyCode.Space);   // true solo el frame en que se presiona
Input.GetKey(KeyCode.Space);       // true mientras está presionada
Input.GetMouseButtonDown(0);       // click izquierdo
Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began; // tap
```

**En el proyecto:** `SingleInput.ActionPressed` junta espacio, click y tap en uno solo. Es el único botón del juego.

📖 [Time](https://docs.unity3d.com/ScriptReference/Time.html) · [Input](https://docs.unity3d.com/ScriptReference/Input.html)

---

## Matemáticas

| Función | Qué hace | Dónde se usa |
|---|---|---|
| `Mathf.Lerp(a, b, t)` | Mezcla entre a y b (t de 0 a 1) | Fades, crecer el charco de sangre |
| `Mathf.Clamp01(x)` | Limita a 0–1 | Casi todas las animaciones |
| `Mathf.Repeat(x, 1)` | "Módulo" para floats (loop) | Desplazamiento del fondo |
| `Mathf.Sin(t)` | Oscilación | Bamboleo del dron, pulso de los textos |
| `Mathf.Atan2(y, x)` | Ángulo de un vector | Puntas de la estrella de oferta |
| `Vector3.MoveTowards(a, b, d)` | Avanza de a hacia b sin pasarse | Balas y dron del ending |
| `Vector3.Distance(a, b)` | Distancia | Si la bala ya llegó |
| `Random.Range(a, b)` | Número al azar (int: b excluido) | Todo lo aleatorio |
| `Random.insideUnitCircle` | Punto al azar en un círculo | Sacudida de pantalla |
| `Quaternion.Euler(0, 0, z)` | Rotación en 2D | Palanca, aguja, celular volando |

📖 [Mathf](https://docs.unity3d.com/ScriptReference/Mathf.html) · [Mathf.Lerp](https://docs.unity3d.com/ScriptReference/Mathf.Lerp.html) · [MoveTowards](https://docs.unity3d.com/ScriptReference/Vector3.MoveTowards.html) · [Random](https://docs.unity3d.com/ScriptReference/Random.html) · [Quaternion.Euler](https://docs.unity3d.com/ScriptReference/Quaternion.Euler.html)

---

## Sprites y orden de dibujo

```csharp
SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
sr.sprite = miSprite;
sr.color = Color.red;            // tiñe el sprite
sr.sortingOrder = 10;            // mayor = se dibuja encima
sr.drawMode = SpriteDrawMode.Tiled;  // repetir el sprite
sr.size = new Vector2(6.8f, 0.25f);  // tamaño del área repetida
```

**En el proyecto (elevador):** pared −10, decoración −9…−5, cubo −4, cabina −2/−1, marco 0, **monitos 3–4**, piso 10.

> [!bug] Bug real del proyecto
> El cubo del elevador y el lambrín tenían el **mismo `sortingOrder`**, así que Unity escogía cuál dibujar encima y parecía que el piso no se abría. Con el mismo orden el resultado no está definido; dale a cada capa su propio número.

📖 [SpriteRenderer](https://docs.unity3d.com/ScriptReference/SpriteRenderer.html) · [2D Sorting](https://docs.unity3d.com/Manual/2DSorting.html) · [SpriteDrawMode](https://docs.unity3d.com/ScriptReference/SpriteDrawMode.html)

---

## Física 2D

### Tipos de Rigidbody2D
| Tipo | Se mueve por | Uso en el proyecto |
|---|---|---|
| **Dynamic** | Física (gravedad, choques) | Piano al caer, carro |
| **Kinematic** | Tu código; no le afecta la gravedad | Piano colgado antes de soltarlo |
| **Static** | No se mueve | Paredes y suelo (`TrapBlocker`) |

### ¿Qué evento se dispara?
- Si **alguno** de los dos colliders es **trigger** → `OnTriggerEnter2D` / `OnTriggerStay2D` / `OnTriggerExit2D`.
- Si **ninguno** es trigger y al menos uno tiene `Rigidbody2D` Dynamic → `OnCollisionEnter2D` (choque físico).

```csharp
// CarTrap.cs: el monito es trigger → OnTrigger; la pared no → OnCollision
void OnTriggerEnter2D(Collider2D other) { /* atropelló a alguien */ }
void OnTriggerStay2D(Collider2D other)  { /* sigue tocándolo (cada paso de física) */ }
void OnCollisionEnter2D(Collision2D c)  { /* chocó con la pared */ }
```

### Consultas sin esperar un evento
```csharp
Collider2D[] hits = Physics2D.OverlapCircleAll(pos, radio); // todo lo que toca un círculo
Bounds b = collider.bounds;                                  // caja del collider en el mundo
```

> [!bug] Bugs reales del proyecto
> - El collider del **monito solo cubría la cabeza**; el cuerpo no tenía collider.
> - El collider del **carro era más ancho que el dibujo** y estaba recorrido hacia adelante, así que "atropellaba" al monito de al lado.
>
> **Lección:** activa los Gizmos y verifica que el collider coincide con el dibujo. Para decidir "a quién le pegué", `WarioStage.FindVictim` usa matemática de `Bounds` en vez de confiar en el primer trigger que llegue.

> [!tip] Objetos rápidos
> `rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;` evita que el piano o el carro atraviesen cosas delgadas.

📖 [Rigidbody2D](https://docs.unity3d.com/ScriptReference/Rigidbody2D.html) · [Body types](https://docs.unity3d.com/Manual/rigidbody2d-body-types.html) · [Collider2D](https://docs.unity3d.com/ScriptReference/Collider2D.html) · [Interacción entre tipos de collider](https://docs.unity3d.com/Manual/collider-types-interaction.html) · [OnTriggerEnter2D](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnTriggerEnter2D.html) · [OverlapCircleAll](https://docs.unity3d.com/ScriptReference/Physics2D.OverlapCircleAll.html) · [Bounds](https://docs.unity3d.com/ScriptReference/Bounds.html) · [MovePosition](https://docs.unity3d.com/ScriptReference/Rigidbody2D.MovePosition.html)

---

## Cámaras y RenderTexture

**Qué área ve una cámara** (útil para que nada aparezca fuera de pantalla):

```csharp
// RenderCameraFollow.GetVisibleWorldRect
float halfH = cam.orthographic
    ? cam.orthographicSize
    : distancia * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
float halfW = halfH * cam.aspect;
```

**RenderTexture:** una cámara puede dibujar en una textura en lugar de la pantalla. Así funciona el celular: la `MiniGameCamera` dibuja en `MiniGameRT` y una `RawImage` (GameScreen) la muestra.

```csharp
cam.targetTexture = null;   // ending: ahora dibuja a pantalla completa
cam.ResetAspect();          // recalcula el aspect para la pantalla
cam.depth = 1;              // se dibuja después (encima) de la Main Camera
```

📖 [Camera](https://docs.unity3d.com/ScriptReference/Camera.html) · [Render Texture](https://docs.unity3d.com/Manual/class-RenderTexture.html)

---

## UI (uGUI)

### Canvas
- **Screen Space – Overlay:** se dibuja encima de todo. El `sortingOrder` decide qué canvas va arriba (fondo −10, celular 2, muerte IRL 95…).
- **CanvasScaler → Scale With Screen Size:** diseñas para una resolución de referencia (800×600 en el proyecto) y escala solo.

### RectTransform (lo que más cuesta)
- **Anchors** (`anchorMin`, `anchorMax`): a qué parte del padre se pega, de 0 a 1.
  - Si min = max → tamaño fijo (`sizeDelta`).
  - Si min ≠ max → se **estira** con el padre; `offsetMin` / `offsetMax` son los márgenes.
- **Pivot:** el punto desde el que se posiciona y rota.

```csharp
// Ocupar del 10% al 90% del ancho del padre, sin importar la resolución
rt.anchorMin = new Vector2(0.1f, 0.4f);
rt.anchorMax = new Vector2(0.9f, 0.5f);
rt.offsetMin = rt.offsetMax = Vector2.zero;
```

> [!bug] Bug real del proyecto
> El título del menú tenía un **tamaño fijo en pixeles** (700×180) y se desbordaba del celular. Con anchors relativos + auto-size ya cabe en cualquier resolución.

### Componentes
```csharp
image.sprite = sprite; image.color = Color.white; image.preserveAspect = true;
rawImage.texture = tex; rawImage.uvRect = new Rect(0, offset, 1, 0.5f); // desplazar la textura
button.onClick.AddListener(() => Debug.Log("click"));
canvasGroup.alpha = 0.5f;          // transparencia de todo un grupo (parpadeo del neón)
go.AddComponent<RectMask2D>();     // recorta a los hijos dentro del rect (transición del fondo)
```

> [!warning] Botones que no responden
> Necesitas un **EventSystem** en la escena y un **GraphicRaycaster** en el Canvas. `GameUI` los crea si faltan.

📖 [uGUI](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/index.html) · [Canvas](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/UICanvas.html) · [Basic Layout (anchors)](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/UIBasicLayout.html) · [Canvas Scaler](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/script-CanvasScaler.html) · [RectTransform](https://docs.unity3d.com/ScriptReference/RectTransform.html) · [CanvasGroup](https://docs.unity3d.com/ScriptReference/CanvasGroup.html) · [RectMask2D](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.RectMask2D.html)

---

## TextMeshPro

```csharp
TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>(); // texto de UI
tmp.text = "TARGET:\n<color=#ffd23f>RED SHIRT</color>";  // rich text
tmp.enableAutoSizing = true;   // ajusta el tamaño para que quepa
tmp.fontSizeMin = 8; tmp.fontSizeMax = 44;
tmp.enableWordWrapping = false;
tmp.outlineWidth = 0.3f; tmp.outlineColor = Color.black;  // contorno
```

- Etiquetas útiles: `<color=#ff0000>`, `<b>`, `<i>`, `<s>` (tachado), `<size=70%>`.
- **Brillo de neón:** `tmp.fontMaterial` (crea una copia del material) + `EnableKeyword("GLOW_ON")` y `SetColor("_GlowColor", ...)`. Ver `GameUI.ApplyNeonColor`.
- Texto en el mundo (no UI): `TextMeshPro` en lugar de `TextMeshProUGUI` (el banner del dron, el letrero "GRAND HOTEL").

> [!warning] Caracteres raros
> Si un símbolo sale como cuadrito, la fuente no lo tiene (por eso el anuncio dice `SKIP AD >>` y no usa ▶).

📖 [TextMeshPro](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/manual/index.html) · [Rich Text](https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/manual/RichText.html) · [API TMP_Text](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/api/TMPro.TMP_Text.html)

---

## Texturas y sprites por código

```csharp
var tex = new Texture2D(24, 12, TextureFormat.RGBA32, false);
tex.filterMode = FilterMode.Point;        // pixel art nítido (sin suavizado)
tex.wrapMode = TextureWrapMode.Repeat;    // se repite (fondos en loop)
tex.SetPixel(x, y, Color.red);            // y = 0 es ABAJO
tex.SetPixels(arregloDeColores);          // mucho más rápido para texturas grandes
tex.Apply();                              // ¡obligatorio! sube los cambios a la GPU

Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                         new Vector2(0.5f, 0.5f), 30f); // 30 pixeles = 1 unidad
```

**En el proyecto:** `RuntimeSprites.cs` (dron, productos, grietas, sangre) y `WalkThemeTextures.cs` (fondos). Para que una textura se repita sin que se note la unión, todo lo que se dibuje cerca del borde de arriba debe "dar la vuelta" al de abajo (`Painter.Set` usa módulo en Y).

> [!tip] Rendimiento
> Generar texturas pequeñas al inicio es barato. Generar texturas grandes en medio del juego puede causar tirones, sobre todo en WebGL. Mejor generarlas antes o guardarlas como PNG.

📖 [Texture2D](https://docs.unity3d.com/ScriptReference/Texture2D.html) · [SetPixels](https://docs.unity3d.com/ScriptReference/Texture2D.SetPixels.html) · [FilterMode](https://docs.unity3d.com/ScriptReference/FilterMode.html) · [TextureWrapMode](https://docs.unity3d.com/ScriptReference/TextureWrapMode.html) · [Sprite.Create](https://docs.unity3d.com/ScriptReference/Sprite.Create.html)

---

## Animación por código

La receta base: una corrutina que avanza `t` de 0 a 1 y aplica una curva.

```csharp
IEnumerator Animar(float duracion)
{
    float t = 0f;
    while (t < duracion)
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / duracion);   // 0 → 1
        float eased = k * k;                     // easing: empieza lento, termina rápido
        transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, eased);
        yield return null;
    }
}
```

| Efecto | Fórmula | Dónde |
|---|---|---|
| Ease in (acelera) | `k * k` o `k * k * k` | Carro que se acerca |
| Ease out back (se pasa y regresa) | `1 + 2.70158*(k-1)^3 + 1.70158*(k-1)^2` | Pop de los monitos y del letrero |
| Gravedad | `vel += g * dt; pos += vel * dt;` | Sangre, celular volando, caída al cubo |
| Sacudida | `pos = base + Random.insideUnitCircle * fuerza * (1 - k)` | Impacto de la muerte IRL |
| Parpadeo | `alpha = Random.value < chance ? bajo : alto` | Neón de GOOD JOB / FAILED |
| Oscilar | `Mathf.Sin(Time.time * velocidad) * amplitud` | Dron, anuncio |

📖 [easings.net](https://easings.net/) (todas las curvas con su fórmula)

---

## Patrones que usa el proyecto

- **Game loop como corrutina / máquina de estados:** menú → (card → minijuego → resultado → anuncio)… → final → menú. Ver `GameManager.GameLoop`.
- **Template method:** `WarioStage` maneja el timer y la salida de los monitos; cada minijuego solo implementa `OnStageUpdate()`. Agregar un minijuego = una clase nueva.
- **`virtual` para personalizar sin `if`s:** cada stage dice su `Theme` (fondo) y su `IrlDeath` (cómo muere el jugador).
- **Singleton simple:** `GameManager.Instance = this;` en `Awake`.
- **Data-driven:** los productos del anuncio son un arreglo (`adProducts`); agregar uno es agregar una entrada, no código nuevo.
- **Valores en el Inspector en lugar de números fijos:** `hitWidth`, `adEveryFails`, `irlDeathAfterFails`…

📖 [Game Programming Patterns](https://gameprogrammingpatterns.com/): [Game Loop](https://gameprogrammingpatterns.com/game-loop.html) · [Update Method](https://gameprogrammingpatterns.com/update-method.html) · [State](https://gameprogrammingpatterns.com/state.html)

---

## Depuración

> [!example] Cuando algo no hace lo que esperas, revisa en este orden
> 1. **Consola:** ¿hay errores rojos? El primero suele ser la causa de los demás.
> 2. **Inspector en Play Mode:** selecciona el objeto, ¿los valores son los que crees?
> 3. **Vista Scene + Gizmos:** ¿los colliders están donde se ve el dibujo? ¿el objeto existe y está activo?

```csharp
Debug.Log($"Golpeé a {npc.name}, target: {npc.isTarget}");
Debug.LogWarning("Algo raro");
Debug.LogError("Falta asignar algo en el Inspector");

void OnDrawGizmosSelected()      // dibuja ayudas en la vista Scene
{
    Gizmos.color = Color.cyan;
    Gizmos.DrawWireCube(centro, tamaño);   // SewerTrap dibuja su zona de captura
}
```

> [!tip] Los .prefab y .unity son texto
> Puedes abrirlos en VS Code y buscar valores (`m_Size`, `m_Offset`, `m_SortingOrder`). Así se encontró que el collider del carro estaba más grande y recorrido.

📖 [Debug.Log](https://docs.unity3d.com/ScriptReference/Debug.Log.html) · [Gizmos](https://docs.unity3d.com/ScriptReference/Gizmos.html)

---

## Mapa del proyecto

| Feature | Archivo | Conceptos |
|---|---|---|
| Flujo del juego | `GameManager.cs` | Corrutinas, singleton, Inspector |
| Base de minijuegos | `WarioStage.cs` | Herencia, `virtual`, Bounds, cámara |
| Semáforo / piano / alcantarilla | `Stages/*.cs`, `Traps/*.cs` | Física 2D, triggers, Rigidbody2D |
| Elevador | `Stages/ElevatorStage.cs` | Construir escenas por código, sorting order |
| Monitos | `NPCWalker.cs`, `NPCAppearance.cs` | Transform, `[Flags]`, collider por código, pop in/out |
| Menú, cards, neón, anuncio | `GameUI.cs` | uGUI, RectTransform, TextMeshPro, CanvasGroup |
| Fondo caminando | `WalkingBackground.cs`, `WalkThemeTextures.cs` | RawImage `uvRect`, RectMask2D, texturas tileables |
| Muerte IRL | `IrlDeathSequence.cs` | Animación por código, sacudida, gravedad |
| Final CCTV | `EndingSequence.cs`, `CinematicOverlay.cs` | Cámara a pantalla completa, overlays |
| Sprites generados | `RuntimeSprites.cs`, `BloodSplatter.cs` | Texture2D, Sprite.Create, partículas simples |
| Input | `SingleInput.cs` | `Input`, orden de ejecución |

---

## Recursos para estudiar

### Documentación oficial
- [Scripting API](https://docs.unity3d.com/ScriptReference/index.html): referencia de todas las clases.
- [Order of execution](https://docs.unity3d.com/Manual/ExecutionOrder.html): el diagrama del ciclo de vida.
- [Introduction to collision](https://docs.unity3d.com/Manual/CollidersOverview.html) y [Interacción entre tipos de collider](https://docs.unity3d.com/Manual/collider-types-interaction.html).
- [uGUI manual](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/index.html).
- [TextMeshPro manual](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/manual/index.html).
- [WebGL](https://docs.unity3d.com/Manual/webgl.html): para cuando exportes al navegador.

### Cursos (gratis)
- [Unity Essentials](https://learn.unity.com/pathway/unity-essentials): el editor desde cero. Útil para quitarse lo oxidado del editor en sí.
- [Junior Programmer](https://learn.unity.com/pathway/junior-programmer): programación en Unity; salta lo que ya sepas de C#.

### YouTube
- [Code Monkey](https://www.youtube.com/@CodeMonkeyUnity): videos cortos sobre cosas puntuales de Unity, para quien ya programa.
- [Brackeys](https://www.youtube.com/@Brackeys): tutoriales clásicos de Unity, muy claros para repasar bases.
- [Game Maker's Toolkit](https://www.youtube.com/@GMTK): diseño de juegos (por qué algo se siente bien o mal).
- [Juice it or lose it](https://www.youtube.com/watch?v=Fy0aCDmgnxg) (Martin Jonasson y Petri Purho): cómo pequeños efectos hacen que un juego se sienta vivo.
- [The art of screenshake](https://www.youtube.com/watch?v=AJdEqssNZ-U) (Jan Willem Nijman, Vlambeer): lo mismo, enfocado en impacto y sacudidas.

### Lectura
- [Game Programming Patterns](https://gameprogrammingpatterns.com/) (Robert Nystrom, gratis en línea): empieza por Game Loop, Update Method y State.
- [Catlike Coding](https://catlikecoding.com/unity/tutorials/): tutoriales profundos de Unity, ideales para la parte procedural.
- [Red Blob Games](https://www.redblobgames.com/): matemáticas y algoritmos para juegos, explicados con interactivos.
- [easings.net](https://easings.net/): curvas de animación con sus fórmulas.

---

## Plan de estudio

### Semana 1: volver a agarrar el motor
- [ ] Unity Essentials: solo las partes del editor (vistas Scene/Game, Inspector, jerarquía, prefabs).
- [ ] Leer el diagrama de [[#Ciclo de vida de un MonoBehaviour]].
- [ ] Leer `GameManager.cs` siguiendo `GameLoop`; poner un `Debug.Log` en cada paso y jugar viendo la consola.
- [ ] En Play Mode, cambiar en vivo `hitWidth`, `npcMoveSpeed` y `timeLimit`, y ver el efecto.
- [ ] Activar Gizmos y revisar los colliders de carro, piano, alcantarilla y monito.
- [ ] Ver *Juice it or lose it*.

### Semana 2: hacerlo tú
- [ ] Cambiar algo visible de la UI: colores del neón, textos del menú.
- [ ] Agregar un producto nuevo al anuncio (copiar una entrada de `adProducts` y dibujar su sprite en `RuntimeSprites`).
- [ ] Hacer un **quinto minijuego** (por ejemplo, una maceta que cae de un balcón) copiando la estructura de `ElevatorStage.cs`:
  - [ ] Heredar de `WarioStage` e implementar `OnStageUpdate`.
  - [ ] Construir el escenario y elegir los sorting orders.
  - [ ] Detectar al monito con `FindVictim`.
  - [ ] Elegir su `Theme` y su `IrlDeath`.
  - [ ] Agregarlo al `GameManager`.
- [ ] Leer *Game Programming Patterns*: Game Loop, Update Method, State.

### Después
- [ ] Repasar [[#UI (uGUI)]] a fondo: anchors y pivots hasta que se sientan naturales.
- [ ] Ajustar la sensación del juego (tiempos, velocidades, sacudidas) jugando y repitiendo.
- [ ] Exportar a WebGL y probar en el navegador.
