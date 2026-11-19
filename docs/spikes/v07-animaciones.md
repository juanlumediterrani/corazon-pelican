# Spike V-07 · Animaciones de Leah y mapeo al vocabulario de acciones

Fecha: 2026-11-19
Tarea: [CP-012](../../backlog/tareas/CP-012-spike-animaciones-leah.md)
Fuente comprobada: decompilado de Stardew Valley **1.6.15.24356** y assets del juego leídos con [`tools/xnbdump`](../../tools/xnbdump/) (sin versionar contenido del juego).

---

## 1. Cómo funcionan `Data/animationDescriptions` y el sprite de Leah

### 1.1 Formato y carga en el motor

`DataLoader.AnimationDescriptions(Game1.content)` carga el diccionario `Data\animationDescriptions` (`StardewValley/DataLoader.cs:75-80`). Cada entrada tiene el formato:

```text
<frames_intro>/<frames_bucle>/<frames_salida>[/<texto_fin_ruta>][/<metadato>...]
```

`NPC.loadEndOfRouteBehavior(string name)` (`StardewValley/NPC.cs:4656-4690`) divide la cadena por `'/'` y rellena:

* `routeEndIntro` (`int[]`), `routeEndAnimation` (`int[]`) y `routeEndOutro` (`int[]`) con `Utility.parseStringToIntArray`.
* `nextEndOfRouteMessage` si el 4.º segmento no está vacío.
* `routeAnimationMetadata` (`laying_down`, `offset X Y`) si hay 5.º segmento o posteriores (`NPC.cs:4418-4437`).

Todos los frames de `routeEndIntro`, `routeEndAnimation` y `routeEndOutro` se instancian como `FarmerSprite.AnimationFrame` con duración fija de **`100` ms por frame**, `secondaryArm: false` y `flip: false` (`NPC.cs:4449-4459, 4471-4475, 4199-4204`). Las pausas largas se consiguen repitiendo el mismo índice de frame en la secuencia.

### 1.2 Entradas de Leah en `Data/animationDescriptions`

Leídas con `tools/xnbdump`:

| Clave | Intro | Bucle (a 100 ms/frame) | Salida | Notas de comportamiento |
|---|---:|---|---|---|
| `leah_draw` | `35` | 58 frames (`35` ×10, trazos `32–34`, pausa `35` ×7, trazos `32–34`) = **5,8 s/ciclo** | `35` | Sin `drawOffset` ni metadatos extra |
| `leah_sculpt` | `19` | 18 frames (`16 17 18` + `19` ×15) = **1,8 s/ciclo** | `19` | También usada por `SpousePatio` en `Data/Characters["Leah"]` |
| `leah_sleep` | `51` | `51` (1 frame) | `51` | Sufijo `_sleep` activa `layingDown = true`, `HideShadow = true` y `playSleepingAnimation()` (`NPC.cs:4413-4417, 4506-4510, 4549-4569`) |
| `leah_beach_drink` | `16` | 63 frames (`16`, `17`, `18`, `19`) = **6,3 s/ciclo** | `16` | Usada en `IslandSouth` (`Resort_Bar`) con `Characters/Leah_Beach` (`IslandSouth.cs:127`) |

No existe ninguna entrada `leah_sit_*` en `Data/animationDescriptions` (a diferencia de otros aldeanos como `abigail_sit_down`, `elliott_sit_down`, `evelyn_sit_left`, `pam_sit_down`, `alex_sit_left`, `maru_sit_down`, `gus_sit_down`, `jodi_sit_down`, `penny_sit_down` o `leo_sit_up`).

### 1.3 Disposición del spritesheet de Leah

Comprobado sobre `Data/Characters["Leah"]` y las texturas `Characters/Leah`, `Characters/Leah_Winter` y `Characters/Leah_Beach`:

* **`Characters/Leah`** y **`Characters/Leah_Winter`**: `64×416` px → cuadrícula de `4` columnas × `13` filas = **`52` frames** (`0..51`) de `16×32` px (`Data/Characters["Leah"].Size = {X:16 Y:32}`; `Appearance` de invierno apunta a `Characters/Leah_Winter`).
  * `0..3`: caminar abajo (`0` reposo mirando abajo).
  * `4..7`: caminar derecha (`4` reposo mirando derecha).
  * `8..11`: caminar arriba (`8` reposo mirando arriba).
  * `12..15`: caminar izquierda (`12` reposo mirando izquierda).
  * `16..19`: **`leah_sculpt`** (perfil izquierdo con cincel; `19` es la postura de reposo).
  * `20..27`: frames de eventos (gestos/interacción; `22–23` usados en eventos del bosque).
  * `28..29`: mitad inferior recortada (usados en agua/eventos con `positionOffset`, no son sentado).
  * `30..31`: de pie hacia la izquierda (eventos).
  * `32..35`: **`leah_draw`** (Leah de frente sujetando el cuaderno; en `32–34` inclina la cabeza mirando al cuaderno mientras dibuja con la mano izquierda; en `35` levanta la cabeza mirando al frente con el cuaderno en reposo).
  * `36..38` y `40..47`: baile/vestido del Festival de las Flores (`39` vacío).
  * `48..49`: inclinada/observando (`50` vacío).
  * `51`: **`leah_sleep`** (tumbada hacia la izquierda).
* **`Characters/Leah_Beach`**: `64×160` px → `4` columnas × `5` filas = **`20` frames** (`0..19`). No contiene los frames `32..35`. Si se intentara reproducir `leah_draw` con la apariencia de playa puesta (`shouldWearIslandAttire == true` en Ginger Island), `AnimatedSprite.UpdateSourceRect()` (`AnimatedSprite.cs:555-559`) detectaría `SourceRect.Bottom > textureHeight` y resetearía el frame a `0`.

---

## 2. Orientaciones (`facingDirection`) y validez fuera del horario

En Stardew Valley (`AnimatedSprite.cs:322-343`, `NPC.cs:3487-3495`), las direcciones son: `0` arriba, `1` derecha, `2` abajo y `3` izquierda.

### 2.1 Qué usa el horario real de Leah

En `Characters/schedules/Leah` (contrariamente a la suposición de partida en el enunciado de CP-012 de que `leah_draw` miraba a la izquierda):

* `spring`: `1500 Forest 33 25 2 leah_draw` → **`facingDirection = 2` (abajo)**.
* `summer`: `1200 Beach 72 18 2 leah_draw` → **`facingDirection = 2` (abajo)**.
* `summer_noBridge`: `1200 Beach 38 24 2 leah_draw` → **`facingDirection = 2` (abajo)**.
* `900 LeahHouse 3 7 3 leah_sculpt` → **`facingDirection = 3` (izquierda)**.
* `2400 LeahHouse 2 4 3 leah_sleep` → **`facingDirection = 3` (izquierda)**.

### 2.2 Orientación de cada animación y comportamiento en otras direcciones

`reallyDoAnimationAtEndOfScheduleRoute` y `doMiddleAnimation` (`NPC.cs:4448-4475`) cargan índices absolutos de frame con `flip: false`, y `AnimatedSprite.faceDirection(int)` (`AnimatedSprite.cs:316-319`) **sale inmediatamente sin hacer nada mientras `CurrentAnimation != null`**. Por tanto, ninguna animación de `Data/animationDescriptions` rota con `FacingDirection`:

| Clave | Orientación visual real | ¿Variantes en otras direcciones? | Qué pasa si `FacingDirection` es otro al iniciarla |
|---|---|---|---|
| `leah_draw` (`32–35`) | **`2` (abajo / frente)** | No | Se dibuja siempre mirando abajo mientras dura la animación; al parar, `StopAnimation()` vuelve a `oldFrame` (la orientación previa), produciendo un giro brusco si no miraba abajo. |
| `leah_sculpt` (`16–19`) | **`3` (izquierda)** | No | Se dibuja siempre mirando a la izquierda; pensada para estar frente a su mesa en `(3,7)`. |
| `leah_sleep` (`51`) | **`3` (izquierda, tumbada)** | No | Activa `layingDown = true` y oculta la sombra (`NPC.cs:4413-4417`). |
| `leah_beach_drink` (`16–19`) | **`3` (izquierda)** | No (solo en `Leah_Beach`) | Sobre `Characters/Leah` normal los frames `16–19` muestran `leah_sculpt`, no una bebida. |

### 2.3 ¿Sirve `leah_draw` fuera de los sitios de su horario?

**Sí.** En el código de Stardew Valley 1.6.15 no hay ninguna comprobación de mapa ni casilla asociada a `leah_draw`, ni usa `drawOffset` ni props externos (el cuaderno forma parte de los frames `32..35`, tanto en `Characters/Leah` como en `Characters/Leah_Winter`). Sirve en cualquier lugar del valle (p. ej. `pasarela_lago` u `orilla_norte` en `Mountain`) y en cualquier estación, incluido invierno, con dos requisitos operativos:

1. **Alinear la orientación antes de empezar:** llamar a `npc.faceDirection(2)` (abajo) antes de iniciar `leah_draw`, para que `Sprite.oldFrame` quede mirando al frente y la entrada/salida en el frame `35` sea continua. (Si en una casilla como la pasarela queda de espaldas al agua, visualmente seguirá mirando hacia la cámara/`2`, igual que en el bosque y la playa en vanilla.)
2. **Guardia de textura de isla:** no lanzarla si `npc.shouldWearIslandAttire.Value` es `true` (`Characters/Leah_Beach` solo tiene frames `0..19`).

---

## 3. Llamadas exactas en Stardew Valley 1.6.15 para iniciar y detener sin pasar por el horario

> **Nota sobre nombres heredados de versiones anteriores:** en el decompilado de Stardew Valley **1.6.15** **no existen** los métodos `startPrepareForEndOfRouteAnimation` ni `resetCurrentAnimation`. Los mecanismos reales están en `NPC.cs`, `Character.cs` y `AnimatedSprite.cs`.

### 3.1 Vía recomendada (v1): API pública de actividad en `NPC` (`StartActivityRouteEndBehavior` / `EndActivityRouteEndBehavior`)

`StardewValley/NPC.cs` expone dos métodos públicos diseñados precisamente para lanzar y terminar animaciones de `Data/animationDescriptions` fuera de una ruta de horario:

#### Iniciar la animación

```csharp
npc.faceDirection(2); // Recomendado para leah_draw antes de fijar oldFrame
npc.StartActivityRouteEndBehavior("leah_draw", null);
```

Traza exacta en el decompilado:

1. `NPC.StartActivityRouteEndBehavior(string behavior_name, string end_message)` (`NPC.cs:4617-4620`) llama a `getRouteEndBehaviorFunction(behavior_name, end_message)?.Invoke(this, base.currentLocation)`.
   * Atención al segundo parámetro: pasar `null` (no `""`), porque si `end_message != null` asigna `nextEndOfRouteMessage = endMessage.Replace("\"", "")` (`NPC.cs:4624-4627`), y si quedara en `endOfRouteMessage.Value`, `NPC.checkAction` lo trataría como diálogo temporal (`NPC.cs:2794, 4326`).
2. `getRouteEndBehaviorFunction` (`NPC.cs:4622-4654`) comprueba `DataLoader.AnimationDescriptions(Game1.content).ContainsKey(behaviorName)`, asigna `endOfRouteBehaviorName.Value = behaviorName`, llama a `loadEndOfRouteBehavior(behaviorName)` (`L4647-4648`), marca `goingToDoEndOfRouteAnimation.Value = true` (`L4649`) y devuelve `doAnimationAtEndOfScheduleRoute` (`L4651`).
3. `doAnimationAtEndOfScheduleRoute` (`NPC.cs:4391-4396`) marca `doingEndOfRouteAnimation.Value = true`, llama a `reallyDoAnimationAtEndOfScheduleRoute()` y fija `currentlyDoingEndOfRouteAnimation = true`.
4. `reallyDoAnimationAtEndOfScheduleRoute()` (`NPC.cs:4398-4466`) encola `routeEndIntro` en `Sprite` (con `doMiddleAnimation` como `frameEndBehavior` del último frame de intro), fija **`freezeMotion = true`** (`L4464`) y guarda `_beforeEndOfRouteAnimationFrame = Sprite.oldFrame` (`L4465`).
5. Al terminar la intro, `doMiddleAnimation` (`NPC.cs:4468-4480`) carga `routeEndAnimation` en bucle (`Sprite.loop = true`). En cada tick, `NPC.update` (`NPC.cs:3318-3321`) avanza `Sprite.animateOnce(time)` en el host (`Game1.IsMasterGame && !Game1.eventUp`).
6. **Ventaja clave en multijugador:** `doingEndOfRouteAnimation` (`NetBool`, `NPC.cs:364, 898`) y `endOfRouteBehaviorName` (`NetString`, `NPC.cs:401, 901`) están sincronizados por `NetFields`. En los clientes remotos, `NPC.update` (`NPC.cs:3203-3221`) detecta el cambio de esos campos, carga la descripción y ejecuta `reallyDoAnimationAtEndOfScheduleRoute()` y `finishEndOfRouteAnimation()` automáticamente.

#### Detener la animación

```csharp
npc.EndActivityRouteEndBehavior();
```

Traza exacta en el decompilado:

1. `NPC.EndActivityRouteEndBehavior()` (`NPC.cs:4612-4615`) llama a `finishEndOfRouteAnimation()` (`NPC.cs:4161-4219`).
2. `finishEndOfRouteAnimation()` limpia `nextEndOfRouteMessage` / `endOfRouteMessage.Value` (`L4185-4186`), y si `currentlyDoingEndOfRouteAnimation && routeEndOutro != null` encola `routeEndOutro` (para `leah_draw`, 1 frame `[35]` = `100` ms) con `routeEndAnimationFinished` como `frameEndBehavior` del último frame (`L4199`).
3. Al concluir el outro, `routeEndAnimationFinished(null)` (`NPC.cs:4276-4305`) ejecuta:
   * `doingEndOfRouteAnimation.Value = false;` (`L4278`)
   * **`freezeMotion = false;`** (`L4279`)
   * restaura `Sprite.oldFrame = _beforeEndOfRouteAnimationFrame` y llama a **`Sprite.StopAnimation()`** (`L4284-4285`)
   * `goingToDoEndOfRouteAnimation.Value = false;` (`L4290`)

> **Trampa crítica (`Halt()` / `StopAnimation()` vs `freezeMotion`):**
> `NPC.Halt()` (`NPC.cs:3507-3520`) → `Character.Halt()` (`Character.cs:686-693`) llama a `Sprite.StopAnimation()`, que pone `CurrentAnimation = null` (`AnimatedSprite.cs:250-255`) **sin tocar `freezeMotion` (`protected bool`, `Character.cs:140`) ni `doingEndOfRouteAnimation.Value`**. Si se llama a `Halt()` o `Sprite.StopAnimation()` durante `doingEndOfRouteAnimation` antes de que `routeEndOutro` complete `routeEndAnimationFinished`, el callback `frameEndBehavior` nunca se dispara (`AnimatedSprite.cs:506-515`) y el NPC queda con `freezeMotion == true`, bloqueando `Character.update` de movimiento y `PathFindController.update` (`Character.cs:1537-1544`).
>
> **Regla para CP-302:** para salir de `dibujar`, llamar **primero** a `npc.EndActivityRouteEndBehavior()` y esperar a que `!npc.doingEndOfRouteAnimation.Value` (≈100 ms en `leah_draw`) antes de iniciar desplazamiento (`PathFindController`) o `Halt()`. Además, asegurarse de limpiar `nextEndOfRouteMessage = null` / `endOfRouteMessage.Value = null` si hubiera alguno previo.
> *(Nota de implementación observada en `NPC.cs:3207-3221`: el mismo tick en que `routeEndAnimationFinished` pone `doingEndOfRouteAnimation.Value = false`, `NPC.update` ve `doingEndOfRouteAnimation.Value != currentlyDoingEndOfRouteAnimation` y vuelve a llamar `finishEndOfRouteAnimation()` una vez más antes de igualar `currentlyDoingEndOfRouteAnimation = false`, añadiendo como máximo otros 100 ms de frame `35`; el estado final sigue limpiándose correctamente).*

### 3.2 Vía alternativa (solo local): `AnimatedSprite.setCurrentAnimation` / `ClearAnimation` + `StopAnimation`

También es posible construir manualmente `List<FarmerSprite.AnimationFrame>` y llamar a `npc.Sprite.setCurrentAnimation(frames)` (`AnimatedSprite.cs:485-496`) o `ClearAnimation()` + `AddFrame(...)` (`L468-483`), fijando `npc.Sprite.loop = true`, y detenerlo al instante con `npc.Sprite.StopAnimation()` (`L244-277`) o `npc.Halt()` (`Character.cs:686-693`).

* **Por qué no usarla como vía principal en Corazón Pelican:** `AnimatedSprite.CurrentAnimation` es una `List` local, **no un `NetField`** (`AnimatedSprite.cs:51`). En `NPC.cs:3318` solo avanza en el host, y en clientes remotos `Character.updateSlaveAnimation` (`Character.cs:1633-1635`) únicamente la animaría si el cliente la tuviera también cargada localmente. Solo sería útil en el futuro si se quisiera reproducir una secuencia que no exista en `Data/animationDescriptions`.

---

## 4. Mapeo completo acción ↔ animación (vocabulario v1)

Vocabulario definido en [`CorazonPelican.Core/Intencion/Acciones.cs`](../../CorazonPelican.Core/Intencion/Acciones.cs) y [`schemas/comun.schema.json`](../../schemas/comun.schema.json):

| Acción v1 | Animación / mecanismo nativo | Frames / parámetros | Notas y restricciones |
|---|---|---|---|
| `ir_a(lugar)` | Locomoción nativa (`PathFindController`) + `animateInFacingDirection` (`Character.cs:1608-1625`) | Frames `0..15` según `FacingDirection` | Si estaba ejecutando `leah_draw`, ejecutar primero `guardar_cuaderno` (`EndActivityRouteEndBehavior()` y esperar `!doingEndOfRouteAnimation.Value`) para liberar `freezeMotion`. |
| `sentarse(lugar)` | **Ausente** (sin animación ni frame de sentado en Leah) | — | No existe `leah_sit_*` en `Data/animationDescriptions`, `Characters/Leah` (`52` frames) no tiene postura sentada, y `ISittable`/`MapSeat` (`ISittable.cs:8`, `Farmer.cs:4697`) son solo para `Farmer`. **Fallback v1 (`CP-302`):** caminar al punto `sentable` del lugar (`ir_a`), `Halt()` + `faceDirection` adecuada al lugar (p. ej. `2` abajo en `pasarela_lago`), dejando constancia de que visualmente permanece de pie en v1 (sprites nuevos fuera de alcance). |
| `mirar(objetivo)` | Orientación de sprite (`faceGeneralDirection` / `faceDirection`, `Character.cs:715-730, 1293-1301`) | Frames de reposo `0, 4, 8, 12` | No hay animación dedicada de giro. Si `CurrentAnimation != null` (p. ej. dibujando), `AnimatedSprite.faceDirection` lo ignora (`AnimatedSprite.cs:316`): para mirar físicamente a otro lado hay que pausar/salir antes de `leah_draw`. |
| `levantar_la_vista` | Salida / pausa de `leah_draw` (`EndActivityRouteEndBehavior()`) + opcional `faceDirection` | Outro `[35]` → reposo `0` | En `leah_draw`, el frame `35` (intro, pausas del bucle y outro) muestra precisamente a Leah **erguiendo la cabeza** desde el cuaderno y mirando al frente; al completar `EndActivityRouteEndBehavior()` vuelve al sprite en reposo (`0`) y puede girar hacia el interlocutor/foco. Si no estaba dibujando, equivale a actualizar el foco del sensorio sin animación extra. |
| `dibujar(obra)` | **`leah_draw`** vía `npc.StartActivityRouteEndBehavior("leah_draw", null)` (`NPC.cs:4617-4620`) | Intro `35`, bucle `32–35` (`100 ms/frame`), outro `35` | Llamar antes `npc.faceDirection(2)`. No usar si `shouldWearIslandAttire.Value == true` (`Characters/Leah_Beach` solo llega al frame `19`). |
| `guardar_cuaderno` | **`EndActivityRouteEndBehavior()`** sobre `leah_draw` (`NPC.cs:4612-4615`) | Reproduce outro `[35]` (100 ms) y ejecuta `routeEndAnimationFinished` | Limpia `doingEndOfRouteAnimation`, `freezeMotion` y `CurrentAnimation`, restaurando el frame previo (`0`). Si no estaba en `leah_draw`, no-op visual. |
| `gesto(tipo)` | **`Character.doEmote(int whichEmote, bool playSound, bool nextEventCommand = true)`** (`Character.cs:1066-1082`) | Ver tabla §4.1 | `updateEmote` (`Character.cs:1090-1130`) y `NPC.DrawEmote` (`NPC.cs:5182-5203`) funcionan incluso con `freezeMotion == true` durante `leah_draw` (salvo si `Game1.eventUp`). |
| `murmurar(texto)` | **`NPC.showTextAboveHead(string text, ...)`** (`NPC.cs:1373-1384`) | Duración por defecto `3000 ms`, estilo `2` | Actualizado en `NPC.update` (`L3295-3313`) y dibujado en `drawAboveAlwaysFrontLayer` (`L5205+`); compatible con `doingEndOfRouteAnimation`. |
| `esperar(duracion_min)` | Sin animación dedicada | Postura actual (`0/4/8/12`, o manteniendo `leah_draw` si ya estaba dibujando) | Mantiene al NPC inmóvil durante la duración en minutos de juego. |
| `volver_a_rutina` | Cierre de animación (`EndActivityRouteEndBehavior()` si `doingEndOfRouteAnimation.Value`) + devolución al horario (`CP-011`) | — | Esperar a `!doingEndOfRouteAnimation.Value` antes de encolar ruta de reincorporación (`controller` / `checkSchedule`). |

### 4.1 Mapeo de `gesto(tipo)` a constantes nativas (`Character.cs:29-53`)

| `tipo` (`schemas/comun.schema.json`) | Constante en `Character.cs` | Valor entero | Nota |
|---|---|---:|---|
| `duda` | `questionMarkEmote` | `8` | |
| `enfado` | `angryEmote` | `12` | |
| `sorpresa` | `exclamationEmote` | `16` | |
| `corazon` | `heartEmote` | `20` | |
| `tristeza` | `sadEmote` | `28` | |
| `alegria` | `happyEmote` | `32` | |
| `pensativa` | `pauseEmote` | `40` | Puntos suspensivos (`...`) |
| `saludo` | *(Sin constante dedicada)* → usar `happyEmote` | `32` | En vanilla `NPC.sayHiTo` (`NPC.cs:3438-3448`) usa `showTextAboveHead`, y los saludos afectivos usan `happyEmote` (`32`). |

### 4.2 Otras animaciones de Leah fuera del vocabulario v1

* `leah_sculpt` (`16–19`, izquierda): usada solo en su rutina canónica (`900 LeahHouse 3 7 3 leah_sculpt`) y `SpousePatio`. Fuera de v1 (`design.md` §6.2: escultura fuera de alcance).
* `leah_sleep` (`51`, izquierda + `layingDown`): usada al acostarse en su horario (`2400 LeahHouse 2 4 3 leah_sleep` / `bed`).
* `leah_beach_drink` (`16–19` sobre `Characters/Leah_Beach`): exclusiva de `IslandSouth` (`Resort_Bar`).
